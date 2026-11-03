using System.Text.Json;
using LiveH.Contracts.Track;

namespace LiveH.Modules.Track;

public sealed class SystemTrackClock : ITrackClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

public sealed class NullTrackReminderGateway : ITrackReminderGateway
{
    public Task ScheduleAsync(Guid sessionId, DateTimeOffset dueAtUtc) => Task.CompletedTask;
    public Task CancelAsync() => Task.CompletedTask;
}

// Independent private store: a single session is the source of truth for mission AND group totals.
public sealed class JsonTrackService : ITrackService
{
    private readonly string _path;
    private readonly ITrackClock _clock;
    private readonly ITrackReminderGateway _reminder;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private sealed class Store
    {
        public int SchemaVersion { get; set; } = 1;
        public List<TrackSession> Sessions { get; set; } = new();
    }

    public JsonTrackService(string filePath, ITrackClock clock, ITrackReminderGateway reminder)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException(nameof(filePath));
        _path = filePath;
        _clock = clock;
        _reminder = reminder;
    }

    public TimeSpan CreditedAt(TrackSession session, DateTimeOffset atUtc) =>
        TrackRules.CreditedAt(session, atUtc);

    public async Task<TrackStatus> StartAsync(Guid missionId, Guid? groupId, string missionTitle,
        CancellationToken ct = default)
    {
        if (missionId == Guid.Empty || string.IsNullOrWhiteSpace(missionTitle))
            throw new ArgumentException("Wybierz prawidłową misję.");
        return await ChangeAsync(store =>
        {
            if (store.Sessions.Any(s => s.State != TrackSessionState.Stopped))
                throw new InvalidOperationException("Najpierw zakończ albo wznów poprzednią sesję.");
            var now = _clock.UtcNow;
            var entry = new TrackSession(Guid.NewGuid(), missionId, groupId,
                missionTitle.Trim(), now, TrackSessionState.Running, now,
                now + TrackRules.CheckEvery, 0);
            store.Sessions.Add(entry);
            return entry;
        }, ct);
    }

    public Task<TrackStatus> PauseAsync(CancellationToken ct = default) => ChangeAsync(store =>
    {
        var entry = RequiredOpen(store);
        if (entry.State != TrackSessionState.Running)
            throw new InvalidOperationException("Sesja nie jest uruchomiona.");
        var result = entry with
        {
            State = TrackSessionState.Paused,
            AccruedTicks = TrackRules.CreditedAt(entry, _clock.UtcNow).Ticks,
            ActiveSinceCheckTicks = checked(entry.ActiveSinceCheckTicks +
                (TrackRules.CreditedAt(entry, _clock.UtcNow).Ticks - entry.AccruedTicks)),
            ActiveFromUtc = null, CheckpointDueAtUtc = null
        };
        Replace(store, result);
        return result;
    }, ct);

    public Task<TrackStatus> ResumeAsync(CancellationToken ct = default) => ChangeAsync(store =>
    {
        var entry = RequiredOpen(store);
        if (entry.State != TrackSessionState.Paused)
            throw new InvalidOperationException("Wznawiać można tylko wstrzymaną sesję.");
        var now = _clock.UtcNow;
        var result = entry with
        {
            State = TrackSessionState.Running,
            ActiveFromUtc = now,
            CheckpointDueAtUtc = now + (TrackRules.CheckEvery -
                TimeSpan.FromTicks(entry.ActiveSinceCheckTicks))
        };
        Replace(store, result);
        return result;
    }, ct);

    public Task<TrackStatus> ConfirmAsync(CancellationToken ct = default) => ChangeAsync(store =>
    {
        var entry = RequiredOpen(store);
        var now = _clock.UtcNow;
        if (entry.State != TrackSessionState.Running || entry.CheckpointDueAtUtc is not { } due || now < due)
            throw new InvalidOperationException("Kontrola aktywności nie jest jeszcze wymagana.");
        if (now > due + TrackRules.ReplyGrace)
            throw new InvalidOperationException("Minął czas na potwierdzenie. Rozpocznij nową sesję.");
        var result = entry with
        {
            AccruedTicks = checked(entry.AccruedTicks + (now - entry.ActiveFromUtc!.Value).Ticks),
            ActiveFromUtc = now,
            CheckpointDueAtUtc = now + TrackRules.CheckEvery,
            Confirmations = entry.Confirmations + 1,
            ActiveSinceCheckTicks = 0
        };
        Replace(store, result);
        return result;
    }, ct);

    public Task<TrackStatus> StopAsync(CancellationToken ct = default) => ChangeAsync(store =>
    {
        var entry = RequiredOpen(store);
        var now = _clock.UtcNow;
        var result = entry with
        {
            State = TrackSessionState.Stopped,
            AccruedTicks = TrackRules.CreditedAt(entry, now).Ticks,
            ActiveFromUtc = null, CheckpointDueAtUtc = null, FinishedAtUtc = now
        };
        Replace(store, result);
        return result;
    }, ct);

    public async Task<TrackStatus?> CurrentAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var data = await ReadAsync(ct);
            var changed = CloseExpired(data);
            if (changed) await SaveAsync(data, ct);
            var open = data.Sessions.LastOrDefault(s => s.State != TrackSessionState.Stopped);
            return open is null ? null : TrackRules.Describe(open, _clock.UtcNow);
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<TrackSession>> SessionsAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var store = await ReadAsync(ct);
            if (CloseExpired(store)) await SaveAsync(store, ct);
            return store.Sessions.ToArray();
        }
        finally { _gate.Release(); }
    }

    public async Task<TimeSpan> MissionTotalAsync(Guid missionId, CancellationToken ct = default)
    {
        var sessions = await SessionsAsync(ct);
        return Sum(sessions.Where(s => s.MissionId == missionId));
    }

    public async Task<TimeSpan> GroupsTotalAsync(IReadOnlyCollection<Guid> groupIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        var ids = groupIds.ToHashSet();
        var sessions = await SessionsAsync(ct);
        return Sum(sessions.Where(s => s.GroupIdAtStart is { } id && ids.Contains(id)));
    }

    private TimeSpan Sum(IEnumerable<TrackSession> sessions)
    {
        long ticks = 0;
        foreach (var s in sessions) ticks = checked(ticks + TrackRules.CreditedAt(s, _clock.UtcNow).Ticks);
        return TimeSpan.FromTicks(ticks);
    }

    private async Task<TrackStatus> ChangeAsync(Func<Store, TrackSession> mutate, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var data = await ReadAsync(ct);
            var wasChanged = CloseExpired(data);
            TrackSession item;
            try { item = mutate(data); }
            catch
            {
                if (wasChanged) await SaveAsync(data, ct);
                throw;
            }
            await SaveAsync(data, ct); // Persistence first; reminder failure must not lose counted time.
            try
            {
                if (item.State == TrackSessionState.Running && item.CheckpointDueAtUtc is { } due)
                    await _reminder.ScheduleAsync(item.Id, due);
                else await _reminder.CancelAsync();
            }
            catch { /* Reminder is best-effort; checkpoint logic is independently enforced. */ }
            return TrackRules.Describe(item, _clock.UtcNow);
        }
        finally { _gate.Release(); }
    }

    private bool CloseExpired(Store store)
    {
        var modified = false;
        for (int i = 0; i < store.Sessions.Count; i++)
        {
            var updated = TrackRules.CloseExpired(store.Sessions[i], _clock.UtcNow);
            if (updated == store.Sessions[i]) continue;
            store.Sessions[i] = updated;
            modified = true;
        }
        return modified;
    }

    private static TrackSession RequiredOpen(Store store) =>
        store.Sessions.LastOrDefault(s => s.State != TrackSessionState.Stopped)
        ?? throw new InvalidOperationException("Nie ma aktywnej sesji. Uruchom TRACK na misji.");

    private static void Replace(Store store, TrackSession session)
    {
        var index = store.Sessions.FindIndex(s => s.Id == session.Id);
        if (index < 0) throw new InvalidDataException("Brak sesji w magazynie.");
        store.Sessions[index] = session;
    }

    private async Task<Store> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return new Store();
        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var store = await JsonSerializer.DeserializeAsync<Store>(stream, JsonOptions, ct)
            ?? throw new InvalidDataException("Niepoprawny plik sesji.");
        if (store.SchemaVersion != 1 || store.Sessions is null)
            throw new InvalidDataException("Nieobsługiwana baza TrackOS.");
        if (store.Sessions.Count(s => s.State != TrackSessionState.Stopped) > 1)
            throw new InvalidDataException("W bazie jest więcej niż jedna otwarta sesja.");
        return store;
    }

    private async Task SaveAsync(Store store, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_path) ?? throw new InvalidOperationException("Brak katalogu danych.");
        Directory.CreateDirectory(dir);
        var temp = _path + ".writing";
        try
        {
            await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, store, JsonOptions, ct);
                await stream.FlushAsync(ct);
            }
            File.Move(temp, _path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
