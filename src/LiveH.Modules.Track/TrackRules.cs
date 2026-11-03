using LiveH.Contracts.Track;

namespace LiveH.Modules.Track;

// Pure rules: no IO, timers, UI or Android dependencies.
public static class TrackRules
{
    public static readonly TimeSpan CheckEvery = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan ReplyGrace = TimeSpan.FromMinutes(5);

    public static TimeSpan CreditedAt(TrackSession item, DateTimeOffset now)
    {
        var ticks = Math.Max(0, item.AccruedTicks);
        if (item.State == TrackSessionState.Running && item.ActiveFromUtc is { } from &&
            item.CheckpointDueAtUtc is { } due)
        {
            var effectiveEnd = now < due ? now : due;
            if (effectiveEnd > from) ticks = checked(ticks + (effectiveEnd - from).Ticks);
        }
        return TimeSpan.FromTicks(ticks);
    }

    public static bool IsExpired(TrackSession item, DateTimeOffset now) =>
        item.State == TrackSessionState.Running &&
        item.CheckpointDueAtUtc is { } due && now > due + ReplyGrace;

    public static TrackSession CloseExpired(TrackSession item, DateTimeOffset now)
    {
        if (!IsExpired(item, now)) return item;
        var due = item.CheckpointDueAtUtc!.Value;
        return item with
        {
            State = TrackSessionState.Stopped,
            AccruedTicks = CreditedAt(item, due).Ticks,
            ActiveFromUtc = null,
            CheckpointDueAtUtc = null,
            FinishedAtUtc = due,
            EndedByInactivity = true
        };
    }

    public static TrackStatus Describe(TrackSession item, DateTimeOffset now) => new(
        item, CreditedAt(item, now),
        item.State == TrackSessionState.Running && item.CheckpointDueAtUtc <= now,
        item.State == TrackSessionState.Running ? item.CheckpointDueAtUtc + ReplyGrace : null);
}
