using LiveH.Contracts.Missions;

namespace LiveH.Modules.Missions;

public sealed class MissionDashboardSource : IMissionDashboardSource
{
    private readonly IMissionRepository _repository;

    public MissionDashboardSource(IMissionRepository repository) => _repository = repository;

    public async Task<MissionDaySnapshot> ReadAsync(DateOnly day, CancellationToken ct = default)
    {
        var all = await _repository.ListAsync(ct);
        var now = DateTime.Now;
        var overdue = all.Where(x => MissionRules.IsOverdue(x, now)).ToArray();
        var todays = all.Where(x => MissionRules.IsOnDay(x, day)
            && !MissionRules.IsOverdue(x, now)).ToArray();
        var open = todays.Where(x => x.Status == MissionStatus.Open)
            .OrderBy(x => x.TimingMode == MissionTimingMode.Urgent ? 0 : 1)
            .ThenBy(x => x.StartsAtLocal ?? (x.Day?.ToDateTime(TimeOnly.MinValue) ?? DateTime.MaxValue))
            .ToArray();
        // If there is nothing left to do today, offer only future scheduled missions.
        // Upcoming is read-only and never changes today's counters or a mission's due date.
        var upcoming = open.Length == 0
            ? all.Where(x => x.Status == MissionStatus.Open
                && !MissionRules.IsOverdue(x, now)
                && !MissionRules.IsOnDay(x, day))
                .Where(x => x.TimingMode switch
                {
                    MissionTimingMode.Day => x.Day is { } d && d > day,
                    MissionTimingMode.TimeRange => x.StartsAtLocal is { } start
                        && DateOnly.FromDateTime(start) > day,
                    _ => false
                })
                // Postponed missions have their own Today panel: no duplicate tiles.
                .Where(x => x.ScheduleHistory is not { Count: > 0 })
                .OrderBy(x => x.StartsAtLocal ??
                    x.Day?.ToDateTime(TimeOnly.MinValue) ?? DateTime.MaxValue)
                .ThenBy(x => x.CreatedAtUtc)
                .Take(3).Select(ToPreview).ToArray()
            : Array.Empty<MissionPreview>();

        // These hints are independent from TODAY counters and from overdue status.
        // Only the three most relevant postponed missions appear on the home screen.
        var reminders = all.Where(x => MissionRules.ShouldRemindOfReschedule(x, day, now))
            .OrderByDescending(x => x.Priority switch
            {
                MissionPriority.Critical => 3,
                MissionPriority.High => 2,
                MissionPriority.Normal => 1,
                _ => 0
            })
            .ThenBy(x => x.StartsAtLocal ??
                x.Day?.ToDateTime(TimeOnly.MinValue) ?? DateTime.MaxValue)
            .ThenByDescending(x => x.ScheduleHistory!.Count)
            .Take(3)
            .Select(x => new MissionReminder(x.Id.ToString(), x.Title,
                MissionRules.TimingLabel(x), x.Priority,
                x.ScheduleHistory!.Count, x.ScheduleHistory[^1].ChangedAtUtc))
            .ToArray();

        return new MissionDaySnapshot(
            Total: todays.Length,
            Completed: todays.Count(x => x.Status == MissionStatus.Completed),
            Overdue: overdue.Length,
            ToDo: open.Select(ToPreview).ToArray(),
            OverdueTitles: overdue.OrderBy(x => x.TimingMode == MissionTimingMode.Day
                        ? x.Day?.ToDateTime(TimeOnly.MinValue) : x.EndsAtLocal)
                .Take(3).Select(x => x.Title).ToArray(),
            Upcoming: upcoming,
            RescheduledReminders: reminders);
    }

    private static MissionPreview ToPreview(MissionRecord mission) =>
        new(mission.Id.ToString(), mission.Title, MissionRules.TimingLabel(mission),
            mission.ScheduleHistory is { Count: > 0 });
}
