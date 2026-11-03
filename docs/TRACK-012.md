# LiveH OS PATCH-012 — TrackOS (hermetic session accounting)

## Ownership / module boundaries

- `LiveH.Modules.Track`: independent domain, UTC clock, activity checkpoints and a private JSON session store.
- `LiveH.Contracts.Track`: public communication surface, IDs and read-only session data.
- `LiveH.App`: presentation, dependency wiring and Android notification adapter. No Android reference in TrackOS.
- `MissionOS`: unchanged database and persistence. Each mission can be tracked through the app-level composition root.
- `GroupsOS`: unchanged database. Aggregation uses the session's `GroupIdAtStart` and hierarchy IDs.

## Semantics

- Only one unfinished session per user. Paused session resumes as the same session.
- Every running segment requires confirmation every 30 minutes. A 5-minute response grace applies.
- Until confirmation, credited time is capped at the last 30-minute checkpoint. A late/no answer ends that session at the checkpoint. Upon timely confirmation, the intervening time is counted.
- If Android delays a notification due to battery policies or notification permission denial, the time cap is still enforced by the domain clock. Notifications are best-effort, never the source of truth.
- Tapping the Android alert opens the Track tab. Confirmation must be made in the app.
- Session history remains even when a mission is marked complete, moved to another group, archived or deleted. Original group ID is immutable.
- Single local file `track-sessions-v1.json` in the app's private data directory. Mission/group v1 files are not migrated or rewritten by TrackOS.
- Totals for parent groups include descendants without incrementing duplicate counters.

## Platform limitations

- Android uses inexact `AlarmManager.SetAndAllowWhileIdle` so notification delivery can be late (especially under Doze/restricted battery). The system never guarantees a notification at the exact 30:00 mark.
- Notification permission is required on Android 13+. If denied, the in-app prompt and automatic accounting limit still work. Reboot clears pending OS alarms; opening the app reconciles sessions.
- Manual completion of a mission never automatically stops the tracker. Track data is separate from mission status.

## Tests

`dotnet run --project tests/LiveH.ArchitectureChecks/LiveH.ArchitectureChecks.csproj`

`dotnet build src/LiveH.App/LiveH.App.csproj -f net10.0-android`

Patch installs with full SHA-256 check + backup and rollback. It does not push Git or alter Android user data.
