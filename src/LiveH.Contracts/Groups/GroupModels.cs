namespace LiveH.Contracts.Groups;

// Stable public identity: integrations refer to GroupRecord.Id, never its display name.
public sealed record GroupRecord(
    Guid Id, Guid? ParentGroupId, string Name, string? Description,
    bool IsArchived, DateTimeOffset CreatedAtUtc);

public interface IGroupRepository
{
    Task<IReadOnlyList<GroupRecord>> ListAsync(CancellationToken ct = default);
    Task<GroupRecord> CreateAsync(string name, Guid? parentGroupId = null,
        string? description = null, CancellationToken ct = default);
    Task RenameAsync(Guid id, string name, CancellationToken ct = default);
    Task MoveAsync(Guid id, Guid? parentGroupId, CancellationToken ct = default);
    Task SetArchivedAsync(Guid id, bool archived, CancellationToken ct = default);
}
