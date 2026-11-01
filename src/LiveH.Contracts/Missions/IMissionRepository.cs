namespace LiveH.Contracts.Missions;

// AppData folder location is injected by the Android composition root.
// Other modules must not open the mission file directly.
public interface IMissionRepository
{
    Task<IReadOnlyList<MissionRecord>> ListAsync(CancellationToken ct = default);
    Task<MissionRecord> CreateAsync(MissionDraft draft, CancellationToken ct = default);
    Task CompleteAsync(Guid missionId, CancellationToken ct = default);
    // Moves an open mission to a chosen local day; timed missions retain their hours.
    Task RescheduleAsync(Guid missionId, DateOnly newDay, CancellationToken ct = default);
    Task DeleteAsync(Guid missionId, CancellationToken ct = default);
}

public interface IMissionDashboardSource
{
    Task<MissionDaySnapshot> ReadAsync(DateOnly day, CancellationToken ct = default);
}
