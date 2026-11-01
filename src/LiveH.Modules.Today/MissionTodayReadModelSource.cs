using LiveH.Contracts.Missions;
using LiveH.Contracts.Today;

namespace LiveH.Modules.Today;

// A thin adapter: Today does not query Missions storage or implement scheduling logic.
public sealed class MissionTodayReadModelSource : ITodayReadModelSource
{
    private readonly IMissionDashboardSource _missions;
    public MissionTodayReadModelSource(IMissionDashboardSource missions) => _missions = missions;

    public async Task<TodayReadModel> ReadAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var day = await _missions.ReadAsync(date, cancellationToken);
        return new TodayReadModel(
            date,
            new MissionSummary(day.Total, day.Completed, day.Overdue),
            Array.Empty<BioNotice>(),
            day.ToDo.Select(x => new TodayTodoItem(x.Title, x.ScheduleLabel,
                x.WasRescheduled)).ToArray(),
            day.OverdueTitles,
            day.Upcoming?.Select(x => new TodayTodoItem(x.Title, x.ScheduleLabel,
                x.WasRescheduled)).ToArray(),
            day.RescheduledReminders?.Select(x => new TodayReminder(
                x.Title, x.ScheduleLabel,
                x.Priority switch
                {
                    MissionPriority.Critical => "KRYTYCZNA",
                    MissionPriority.High => "WAŻNA",
                    MissionPriority.Low => "NISKA",
                    _ => "NORMALNA"
                },
                x.RescheduleCount, x.LastRescheduledAtUtc,
                x.Priority is MissionPriority.High or MissionPriority.Critical)).ToArray());
    }
}
