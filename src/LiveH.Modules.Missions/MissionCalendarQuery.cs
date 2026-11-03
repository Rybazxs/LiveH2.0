using LiveH.Contracts.Missions;

namespace LiveH.Modules.Missions;

// MissionOS read-model projection. There is no calendar database, calendar record,
// or mutation of MissionRecord: the date belongs to the mission itself.
// Undated active missions are a rolling to-do list for today and future days;
// calendar badges count ONLY open missions explicitly scheduled on a date.
public static class MissionCalendarQuery
{
    public static bool IsUndated(MissionRecord mission) =>
        mission.TimingMode == MissionTimingMode.Urgent;

    public static bool IsScheduled(MissionRecord mission) =>
        mission.TimingMode is MissionTimingMode.Day or MissionTimingMode.TimeRange;

    public static bool ScheduledOn(MissionRecord mission, DateOnly day) =>
        ScheduledOverlaps(mission, day, day);

    public static bool ScheduledOverlaps(MissionRecord mission, DateOnly firstDay, DateOnly lastDay)
    {
        if (lastDay < firstDay) return false;
        return mission.TimingMode switch
        {
            MissionTimingMode.Day => mission.Day is { } due &&
                due >= firstDay && due <= lastDay,
            MissionTimingMode.TimeRange => mission.StartsAtLocal is { } start &&
                mission.EndsAtLocal is { } end &&
                start < EndExclusive(lastDay) && end > firstDay.ToDateTime(TimeOnly.MinValue),
            _ => false
        };
    }

    // Completed unscheduled missions may be inspected in the archive on the
    // local completion date. They must not persist in the rolling active list.
    public static bool UndatedOn(MissionRecord mission, DateOnly day, DateOnly asOfDay)
    {
        if (!IsUndated(mission)) return false;
        if (mission.Status == MissionStatus.Open) return day >= asOfDay;
        return mission.Status == MissionStatus.Completed &&
            mission.CompletedAtUtc is { } completed &&
            DateOnly.FromDateTime(completed.ToLocalTime().DateTime) == day;
    }

    public static bool UndatedOverlaps(MissionRecord mission, DateOnly firstDay,
        DateOnly lastDay, DateOnly asOfDay)
    {
        if (lastDay < firstDay || !IsUndated(mission)) return false;
        if (mission.Status == MissionStatus.Open) return lastDay >= asOfDay;
        if (mission.Status != MissionStatus.Completed || mission.CompletedAtUtc is not { } completed)
            return false;
        var completedDay = DateOnly.FromDateTime(completed.ToLocalTime().DateTime);
        return completedDay >= firstDay && completedDay <= lastDay;
    }

    public static bool OccursOn(MissionRecord mission, DateOnly day) =>
        OccursOn(mission, day, DateOnly.FromDateTime(DateTime.Today));

    public static bool OccursOn(MissionRecord mission, DateOnly day, DateOnly asOfDay) =>
        IsUndated(mission) ? UndatedOn(mission, day, asOfDay) : ScheduledOn(mission, day);

    public static bool Overlaps(MissionRecord mission, DateOnly firstDay, DateOnly lastDay) =>
        Overlaps(mission, firstDay, lastDay, DateOnly.FromDateTime(DateTime.Today));

    public static bool Overlaps(MissionRecord mission, DateOnly firstDay, DateOnly lastDay,
        DateOnly asOfDay) => IsUndated(mission)
            ? UndatedOverlaps(mission, firstDay, lastDay, asOfDay)
            : ScheduledOverlaps(mission, firstDay, lastDay);

    public static IReadOnlyDictionary<DateOnly, int> CountsForMonth(
        IEnumerable<MissionRecord> missions, int year, int month, DateOnly? asOfDay = null)
    {
        // Retain asOfDay for API compatibility with PATCH-015B tests/callers.
        // It cannot affect badges: only dates stored on open missions count.
        _ = asOfDay;
        var scheduled = missions.Where(m => m.Status == MissionStatus.Open && IsScheduled(m))
            .ToArray();
        var result = new Dictionary<DateOnly, int>();
        for (var number = 1; number <= DateTime.DaysInMonth(year, month); number++)
        {
            var day = new DateOnly(year, month, number);
            var count = scheduled.Count(m => ScheduledOn(m, day));
            if (count > 0) result.Add(day, count);
        }
        return result;
    }

    private static DateTime EndExclusive(DateOnly lastDay) => lastDay == DateOnly.MaxValue
        ? DateTime.MaxValue : lastDay.AddDays(1).ToDateTime(TimeOnly.MinValue);
}
