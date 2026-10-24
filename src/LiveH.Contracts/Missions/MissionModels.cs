namespace LiveH.Contracts.Missions;

// Stable public identifiers: an external TrackOS event can reference MissionRecord.Id.
public enum MissionTimingMode { Urgent = 0, Day = 1, TimeRange = 2 }
public enum MissionStatus { Open = 0, Completed = 1 }
// Normal is zero so historical JSON missions (without Priority) stay NORMAL.
public enum MissionPriority { Normal = 0, Low = 1, High = 2, Critical = 3 }

// DateOnly and wall-clock DateTime represent the user's local calendar intent.
// TimeZoneId keeps the originating zone for future calendars/integrations.
public sealed record MissionDraft(
    string Title,
    string? Notes,
    MissionTimingMode TimingMode,
    DateOnly? Day,
    DateTime? StartsAtLocal,
    DateTime? EndsAtLocal,
    int? EstimatedMinutes,
    string? CategoryId,
    string TimeZoneId,
    string? GroupName = null,
    MissionPriority Priority = MissionPriority.Normal,
    Guid? GroupId = null);

public sealed record MissionRecord(
    Guid Id,
    string Title,
    string? Notes,
    MissionTimingMode TimingMode,
    DateOnly? Day,
    DateTime? StartsAtLocal,
    DateTime? EndsAtLocal,
    int? EstimatedMinutes,
    string? CategoryId,
    string TimeZoneId,
    MissionStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? GroupName = null,
    IReadOnlyList<MissionScheduleChange>? ScheduleHistory = null,
    MissionPriority Priority = MissionPriority.Normal,
    Guid? GroupId = null);

// Stored locally with each mission; future analytics can inspect rescheduling events.
public sealed record MissionScheduleChange(
    DateTimeOffset ChangedAtUtc,
    MissionTimingMode OldTimingMode,
    DateOnly? OldDay,
    DateTime? OldStartsAtLocal,
    DateTime? OldEndsAtLocal,
    MissionTimingMode NewTimingMode,
    DateOnly? NewDay,
    DateTime? NewStartsAtLocal,
    DateTime? NewEndsAtLocal);

// Only summary data crosses into the Today module, not the mission database.
public sealed record MissionPreview(string Id, string Title, string ScheduleLabel,
    bool WasRescheduled = false);

// A small, bounded read-only digest for Today's postponed-mission panel.
public sealed record MissionReminder(string Id, string Title, string ScheduleLabel,
    MissionPriority Priority, int RescheduleCount, DateTimeOffset LastRescheduledAtUtc);
public sealed record MissionDaySnapshot(
    int Total,
    int Completed,
    int Overdue,
    IReadOnlyList<MissionPreview> ToDo,
    IReadOnlyList<string> OverdueTitles,
    IReadOnlyList<MissionPreview>? Upcoming = null,
    IReadOnlyList<MissionReminder>? RescheduledReminders = null);
