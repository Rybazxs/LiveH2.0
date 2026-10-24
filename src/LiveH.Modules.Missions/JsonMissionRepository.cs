using System.Text.Json;
using LiveH.Contracts.Missions;

namespace LiveH.Modules.Missions;

// Local-only MVP storage behind a replaceable contract. No network access.
// Later SQLite migration can replace this class without changing MissionOS UI.
public sealed class JsonMissionRepository : IMissionRepository
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
        public List<MissionRecord> Missions { get; set; } = new();
    }

    public JsonMissionRepository(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException(nameof(filePath));
        _filePath = filePath;
    }

    public async Task<IReadOnlyList<MissionRecord>> ListAsync(CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try { return (await ReadAsync(ct)).Missions.ToArray(); }
        finally { _mutex.Release(); }
    }

    public async Task<MissionRecord> CreateAsync(MissionDraft draft, CancellationToken ct = default)
    {
        MissionRules.Validate(draft);
        var mission = new MissionRecord(
            Guid.NewGuid(), draft.Title.Trim(), draft.Notes?.Trim(), draft.TimingMode,
            draft.Day, draft.StartsAtLocal, draft.EndsAtLocal, draft.EstimatedMinutes,
            draft.CategoryId, draft.TimeZoneId, MissionStatus.Open,
            DateTimeOffset.UtcNow, null, draft.GroupName?.Trim(),
            Priority: draft.Priority, GroupId: draft.GroupId);

        await _mutex.WaitAsync(ct);
        try
        {
            var store = await ReadAsync(ct);
            store.Missions.Add(mission);
            await SaveAsync(store, ct);
            return mission;
        }
        finally { _mutex.Release(); }
    }

    public async Task CompleteAsync(Guid missionId, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var store = await ReadAsync(ct);
            var index = store.Missions.FindIndex(x => x.Id == missionId);
            if (index < 0) throw new KeyNotFoundException("Nie znaleziono misji.");
            if (store.Missions[index].Status == MissionStatus.Completed) return;
            store.Missions[index] = store.Missions[index] with
            {
                Status = MissionStatus.Completed,
                CompletedAtUtc = DateTimeOffset.UtcNow
            };
            await SaveAsync(store, ct);
        }
        finally { _mutex.Release(); }
    }

    public async Task RescheduleAsync(Guid missionId, DateOnly newDay, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var store = await ReadAsync(ct);
            var index = store.Missions.FindIndex(x => x.Id == missionId);
            if (index < 0) throw new KeyNotFoundException("Nie znaleziono misji.");
            // Validation is done before writing; a failed reschedule cannot corrupt the file.
            var changed = MissionRules.Reschedule(store.Missions[index], newDay,
                DateTime.Now, DateTimeOffset.UtcNow);
            store.Missions[index] = changed;
            await SaveAsync(store, ct);
        }
        finally { _mutex.Release(); }
    }

    public async Task AssignGroupAsync(Guid missionId, Guid? groupId,
        string? groupName, CancellationToken ct = default)
    {
        if (groupId is null && groupName is not null)
            throw new ArgumentException("Nie można przypisać nazwy bez identyfikatora grupy.");
        if (groupName?.Length > 80)
            throw new ArgumentException("Nazwa grupy jest za długa.");
        await _mutex.WaitAsync(ct);
        try
        {
            var store = await ReadAsync(ct);
            var index = store.Missions.FindIndex(m => m.Id == missionId);
            if (index < 0) throw new KeyNotFoundException("Nie znaleziono misji.");
            store.Missions[index] = store.Missions[index] with
            {
                GroupId = groupId, GroupName = groupName
            };
            await SaveAsync(store, ct);
        }
        finally { _mutex.Release(); }
    }

    public async Task DeleteAsync(Guid missionId, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var store = await ReadAsync(ct);
            var removed = store.Missions.RemoveAll(x => x.Id == missionId);
            if (removed == 0) throw new KeyNotFoundException("Nie znaleziono misji.");
            await SaveAsync(store, ct);
        }
        finally { _mutex.Release(); }
    }

    private async Task<Store> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath)) return new Store();
        await using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var data = await JsonSerializer.DeserializeAsync<Store>(stream, Options, ct)
            ?? throw new InvalidDataException("Plik misji jest pusty lub uszkodzony.");
        if (data.SchemaVersion != 1 || data.Missions is null)
            throw new InvalidDataException("Nieobsługiwana wersja bazy misji.");
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
            await using (var stream = new FileStream(tempPath, FileMode.Create,
                FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, store, Options, ct);
                await stream.FlushAsync(ct);
            }
            // The previous data file stays intact until the new JSON is fully written.
            File.Move(tempPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
