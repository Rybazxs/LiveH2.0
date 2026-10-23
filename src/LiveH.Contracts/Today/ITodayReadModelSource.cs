namespace LiveH.Contracts.Today;

public interface ITodayReadModelSource
{
    Task<TodayReadModel> ReadAsync(DateOnly date, CancellationToken cancellationToken = default);
}
