namespace LiveH.Contracts.Today;

public sealed record MissionSummary(int Total, int Completed, int Overdue)
{
    public int Remaining => Math.Max(0, Total - Completed);
}

public sealed record BioNotice(string Id, string Title, string Message);
// View-only information. No mission entities or storage types leak to the dashboard.
public sealed record TodayTodoItem(string Title, string ScheduleLabel,
    bool WasRescheduled = false);

public sealed record TodayReminder(string Title, string ScheduleLabel,
    string PriorityLabel, int RescheduleCount,
    DateTimeOffset LastRescheduledAtUtc, bool IsHighPriority);

// Immutable projection for the Today UI. No health or mission business logic here.
public sealed record TodayReadModel(
    DateOnly Date,
    MissionSummary Missions,
    IReadOnlyList<BioNotice> BioNotices,
    IReadOnlyList<TodayTodoItem>? Todo = null,
    IReadOnlyList<string>? OverdueTitles = null,
    IReadOnlyList<TodayTodoItem>? Upcoming = null,
    IReadOnlyList<TodayReminder>? RescheduledReminders = null);
