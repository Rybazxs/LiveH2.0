using LiveH.Contracts.Today;

namespace LiveH.Modules.Today;

// Explicit empty provider. Replaced by a real provider when Missions/BioOS exist.
// Never generates fictional user activity or health data.
public sealed class EmptyTodayReadModelSource : ITodayReadModelSource
{
    public Task<TodayReadModel> ReadAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = new TodayReadModel(
            date,
            new MissionSummary(Total: 0, Completed: 0, Overdue: 0),
            Array.Empty<BioNotice>());

        return Task.FromResult(snapshot);
    }
}
