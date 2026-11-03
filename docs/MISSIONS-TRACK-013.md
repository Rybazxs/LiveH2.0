# LiveH OS PATCH-013 — Unified Mission Cards and Track Controls

## Scope
- Shared app presentation `MissionCardView`: same layout/actions in Missions, Groups, Today.
- Always-visible play button on the right. The summary toggles actions with a short fade.
- `MissionCardCommands` is the only UI action router; Groups/Today do not write through another module's implementation.
- Mission edit updates the same ID and leaves `ScheduleHistory`, completion and prior track sessions intact.
- Independent Track controls (PAUSE / RESUME / STOP) stay pinned below its scrollable content.
- Track quick-creates a regular mission and starts a session, with an optional GroupId.
- Starting another mission explicitly asks the user before stopping the current session.
- Session owner is TrackOS; group reports are projections based on session history.
- PATCH-012D Android ANR protection is bundled and verified; no blocking GetResult in receiver.

## Compatibility
- Mission JSON schema v1 unchanged; optional contract method is implemented by JsonMissionRepository.
- Tracking history remains in track-sessions-v1.json and is never moved/deleted by installer.
- The archive never touches Android app storage or GitHub.

## Manual smoke tests (Android)
1. Start a mission from Missions using the right-hand play; PAUSE, RESUME and STOP must react instantly.
2. While one mission is active, play a different one. Confirm only after prompt; ANULUJ must preserve the original session.
3. In Track, create a new mission by title and start it; check it appears in Missions.
4. Edit mission name, schedule, notes, group; verify same mission ID and accumulated time.
5. Open a mission card in Missions, inside a nested Group and in Today; verify consistent buttons.
6. Verify Android reminder does not block UI and inactivity does not credit unconfirmed work.
