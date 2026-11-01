using LiveH.Contracts.Missions;

namespace LiveH.Modules.Missions;

// Pure mission-domain rules. No UI, MAUI references or other modules.
public static class MissionRules
{
    public static void Validate(MissionDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Title) || draft.Title.Trim().Length > 160)
            throw new ArgumentException("Nazwa misji musi mieć od 1 do 160 znaków.");
        if (draft.GroupName?.Trim().Length > 80)
            throw new ArgumentException("Nazwa grupy może mieć maksymalnie 80 znaków.");
        if (draft.Notes?.Length > 4000)
            throw new ArgumentException("Opis misji jest za długi (maks. 4000 znaków).");
        if (draft.EstimatedMinutes is < 1 or > 10080)
            throw new ArgumentException("Szacowany czas musi wynosić 1–10080 minut.");
        if (!Enum.IsDefined(draft.Priority))
            throw new ArgumentException("Nieznany priorytet misji.");
        if (string.IsNullOrWhiteSpace(draft.TimeZoneId))
            throw new ArgumentException("Brak strefy czasowej.");
        if (!string.IsNullOrEmpty(draft.CategoryId) && draft.CategoryId.Length > 120)
            throw new ArgumentException("Nieprawidłowy identyfikator kategorii.");

        switch (draft.TimingMode)
        {
            case MissionTimingMode.Urgent:
                if (draft.Day.HasValue || draft.StartsAtLocal.HasValue || draft.EndsAtLocal.HasValue)
                    throw new ArgumentException("Misja na cito nie ma terminu.");
                break;
            case MissionTimingMode.Day:
                if (!draft.Day.HasValue || draft.StartsAtLocal.HasValue || draft.EndsAtLocal.HasValue)
                    throw new ArgumentException("Misja na dzień wymaga jednej daty.");
                break;
            case MissionTimingMode.TimeRange:
                if (draft.Day.HasValue || !draft.StartsAtLocal.HasValue || !draft.EndsAtLocal.HasValue
                    || draft.EndsAtLocal.Value <= draft.StartsAtLocal.Value)
                    throw new ArgumentException("Koniec misji musi wypadać po jej początku.");
                break;
            default:
                throw new ArgumentException("Nieznany typ terminu.");
        }
    }

    // Pure domain operation: no UI, network or storage dependencies.
    // A time range keeps its original local start time and duration, even across midnight.
    public static MissionRecord Reschedule(
        MissionRecord mission, DateOnly newDay, DateTime nowLocal, DateTimeOffset changedAtUtc)
    {
        if (mission.Status != MissionStatus.Open)
            throw new InvalidOperationException("Nie można odroczyć wykonanej misji.");
        if (newDay < DateOnly.FromDateTime(nowLocal))
            throw new ArgumentException("Nie można przenieść misji na miniony dzień.");

        var newMode = mission.TimingMode == MissionTimingMode.TimeRange
            ? MissionTimingMode.TimeRange : MissionTimingMode.Day;
        DateTime? newStart = null;
        DateTime? newEnd = null;
        DateOnly? day = newMode == MissionTimingMode.Day ? newDay : null;
        if (newMode == MissionTimingMode.TimeRange)
        {
            if (mission.StartsAtLocal is not { } oldStart || mission.EndsAtLocal is not { } oldEnd)
                throw new InvalidDataException("Niekompletny termin misji.");
            var duration = oldEnd - oldStart;
            if (duration <= TimeSpan.Zero)
                throw new InvalidDataException("Niepoprawny przedział godzin misji.");
            newStart = newDay.ToDateTime(TimeOnly.FromDateTime(oldStart));
            newEnd = newStart.Value + duration;
        }

        var eventLog = (mission.ScheduleHistory ?? Array.Empty<MissionScheduleChange>()).ToList();
        eventLog.Add(new MissionScheduleChange(
            changedAtUtc, mission.TimingMode, mission.Day,
            mission.StartsAtLocal, mission.EndsAtLocal,
            newMode, day, newStart, newEnd));
        return mission with
        {
            TimingMode = newMode,
            Day = day,
            StartsAtLocal = newStart,
            EndsAtLocal = newEnd,
            ScheduleHistory = eventLog
        };
    }

    // Shows postponed work due in the next seven calendar days, not far-future plans.
    // The complete reschedule history is kept; a week-old event can be surfaced again
    // once its NEW deadline approaches. Overdue work uses the separate overdue panel.
    public static bool ShouldRemindOfReschedule(MissionRecord mission,
        DateOnly today, DateTime nowLocal)
    {
        if (mission.Status != MissionStatus.Open || mission.ScheduleHistory is not { Count: > 0 }
            || IsOverdue(mission, nowLocal) || IsOnDay(mission, today))
            return false;

        DateOnly? due = mission.TimingMode switch
        {
            MissionTimingMode.Day => mission.Day,
            MissionTimingMode.TimeRange when mission.StartsAtLocal is { } start
                => DateOnly.FromDateTime(start),
            _ => null
        };
        return due is { } date && date > today && date <= today.AddDays(7);
    }

    public static bool IsOverdue(MissionRecord mission, DateTime nowLocal)
    {
        if (mission.Status == MissionStatus.Completed) return false;
        return mission.TimingMode switch
        {
            MissionTimingMode.Day => mission.Day is { } dueDay
                && dueDay < DateOnly.FromDateTime(nowLocal),
            MissionTimingMode.TimeRange => mission.EndsAtLocal is { } end && end < nowLocal,
            _ => false // Urgent has priority, but no synthetic deadline.
        };
    }

    public static bool IsOnDay(MissionRecord mission, DateOnly day)
    {
        return mission.TimingMode switch
        {
            MissionTimingMode.Urgent => mission.Status == MissionStatus.Open ||
                (mission.CompletedAtUtc?.ToLocalTime().Date == day.ToDateTime(TimeOnly.MinValue)),
            MissionTimingMode.Day => mission.Day == day,
            MissionTimingMode.TimeRange => mission.StartsAtLocal is { } start
                && mission.EndsAtLocal is { } end
                && DateOnly.FromDateTime(start) <= day && DateOnly.FromDateTime(end) >= day,
            _ => false
        };
    }

    public static string TimingLabel(MissionRecord mission) => mission.TimingMode switch
    {
        MissionTimingMode.Urgent => "NA CITO",
        MissionTimingMode.Day => $"{mission.Day:dd.MM.yyyy} · cały dzień",
        MissionTimingMode.TimeRange => mission.StartsAtLocal?.Date == mission.EndsAtLocal?.Date
            ? $"{mission.StartsAtLocal:dd.MM.yyyy} · {mission.StartsAtLocal:HH:mm}–{mission.EndsAtLocal:HH:mm}"
            : $"{mission.StartsAtLocal:dd.MM HH:mm} → {mission.EndsAtLocal:dd.MM HH:mm}",
        _ => "NIEZNANY TERMIN"
    };
}
