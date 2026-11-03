namespace LiveH.Contracts.Track;

// Domain identifiers never depend on MissionOS / GroupsOS implementation classes.
public enum TrackSessionState { Running = 0, Paused = 1, Stopped = 2 }

public sealed record TrackSession(
    Guid Id,
    Guid MissionId,
    Guid? GroupIdAtStart,
    string MissionTitle,
    DateTimeOffset StartedAtUtc,
    TrackSessionState State,
    DateTimeOffset? ActiveFromUtc,
    DateTimeOffset? CheckpointDueAtUtc,
    long AccruedTicks,
    DateTimeOffset? FinishedAtUtc = null,
    int Confirmations = 0,
    bool EndedByInactivity = false,
    long ActiveSinceCheckTicks = 0);

public sealed record TrackStatus(TrackSession Session, TimeSpan Credited,
    bool NeedsConfirmation, DateTimeOffset? ReplyUntilUtc);

// Clock is injectable so checks survive app closure and can be deterministically tested.
public interface ITrackClock { DateTimeOffset UtcNow { get; } }

// Implemented by platform adapter; Track module must not import Android namespaces.
public interface ITrackReminderGateway
{
    Task ScheduleAsync(Guid sessionId, DateTimeOffset dueAtUtc);
    Task CancelAsync();
}

public interface ITrackService
{
    Task<TrackStatus> StartAsync(Guid missionId, Guid? groupId, string missionTitle,
        CancellationToken ct = default);
    Task<TrackStatus?> CurrentAsync(CancellationToken ct = default);
    Task<TrackStatus> PauseAsync(CancellationToken ct = default);
    Task<TrackStatus> ResumeAsync(CancellationToken ct = default);
    Task<TrackStatus> ConfirmAsync(CancellationToken ct = default);
    Task<TrackStatus> StopAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TrackSession>> SessionsAsync(CancellationToken ct = default);
    Task<TimeSpan> MissionTotalAsync(Guid missionId, CancellationToken ct = default);
    Task<TimeSpan> GroupsTotalAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken ct = default);
    TimeSpan CreditedAt(TrackSession session, DateTimeOffset atUtc);
}
