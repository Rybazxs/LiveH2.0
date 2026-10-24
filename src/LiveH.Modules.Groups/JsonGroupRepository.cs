using System.Text.Json;
using LiveH.Contracts.Groups;

namespace LiveH.Modules.Groups;

// Only this module owns groups-v1.json; other modules access groups via IGroupRepository.
public sealed class JsonGroupRepository : IGroupRepository
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private sealed class Store
    {
        public int SchemaVersion { get; set; } = 1;
        public List<GroupRecord> Groups { get; set; } = new();
    }

    public JsonGroupRepository(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException(nameof(filePath));
        _filePath = filePath;
    }

    public async Task<IReadOnlyList<GroupRecord>> ListAsync(CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try { return (await ReadAsync(ct)).Groups.ToArray(); }
        finally { _mutex.Release(); }
    }

    public async Task<GroupRecord> CreateAsync(string name, Guid? parentGroupId = null,
        string? description = null, CancellationToken ct = default)
    {
        name = GroupRules.ValidateName(name);
        if (description?.Length > 2000) throw new ArgumentException("Opis grupy jest zbyt długi.");
        await _mutex.WaitAsync(ct);
        try
        {
            var store = await ReadAsync(ct);
            GroupRules.ValidatePlacement(store.Groups, null, parentGroupId, name);
            if (parentGroupId is { } parent && IsArchivedBranch(store.Groups, parent))
                throw new InvalidOperationException("Nie można tworzyć podgrup w archiwum.");
            var group = new GroupRecord(Guid.NewGuid(), parentGroupId,
                name, description?.Trim(), false, DateTimeOffset.UtcNow);
            store.Groups.Add(group);
            await SaveAsync(store, ct);
            return group;
        }
        finally { _mutex.Release(); }
    }

    public async Task RenameAsync(Guid id, string name, CancellationToken ct = default)
    {
        name = GroupRules.ValidateName(name);
        await _mutex.WaitAsync(ct);
        try
        {
            var store = await ReadAsync(ct);
            var index = store.Groups.FindIndex(g => g.Id == id);
            if (index < 0) throw new KeyNotFoundException("Nie znaleziono grupy.");
            GroupRules.ValidatePlacement(store.Groups, id, store.Groups[index].ParentGroupId, name);
            store.Groups[index] = store.Groups[index] with { Name = name };
            await SaveAsync(store, ct);
        }
        finally { _mutex.Release(); }
    }

    public async Task MoveAsync(Guid id, Guid? parentGroupId, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var store = await ReadAsync(ct);
            var index = store.Groups.FindIndex(g => g.Id == id);
            if (index < 0) throw new KeyNotFoundException("Nie znaleziono grupy.");
            var group = store.Groups[index];
            GroupRules.ValidatePlacement(store.Groups, id, parentGroupId, group.Name);
            if (parentGroupId is { } parent && IsArchivedBranch(store.Groups, parent))
                throw new InvalidOperationException("Nie można przenosić do archiwum.");
            if (group.ParentGroupId == parentGroupId) return;
            store.Groups[index] = group with { ParentGroupId = parentGroupId };
            await SaveAsync(store, ct);
        }
        finally { _mutex.Release(); }
    }

    public async Task SetArchivedAsync(Guid id, bool archived, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var store = await ReadAsync(ct);
            var index = store.Groups.FindIndex(g => g.Id == id);
            if (index < 0) throw new KeyNotFoundException("Nie znaleziono grupy.");
            if (store.Groups[index].IsArchived == archived) return;
            store.Groups[index] = store.Groups[index] with { IsArchived = archived };
            await SaveAsync(store, ct);
        }
        finally { _mutex.Release(); }
    }

    private static bool IsArchivedBranch(IReadOnlyList<GroupRecord> groups, Guid id)
    {
        var visited = new HashSet<Guid>();
        Guid? cursor = id;
        while (cursor is { } current && visited.Add(current))
        {
            var group = groups.FirstOrDefault(g => g.Id == current);
            if (group is null || group.IsArchived) return true;
            cursor = group.ParentGroupId;
        }
        return false;
    }

    private async Task<Store> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath)) return new Store();
        await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var data = await JsonSerializer.DeserializeAsync<Store>(stream, Options, ct)
            ?? throw new InvalidDataException("Plik grup jest pusty lub uszkodzony.");
        if (data.SchemaVersion != 1 || data.Groups is null)
            throw new InvalidDataException("Nieobsługiwana wersja magazynu grup.");
        return data;
    }

    private async Task SaveAsync(Store store, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(_filePath)
            ?? throw new InvalidOperationException("Niepoprawna ścieżka danych.");
        Directory.CreateDirectory(directory);
        var tempPath = _filePath + ".writing";
        try
        {
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, store, Options, ct);
                await stream.FlushAsync(ct);
            }
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
