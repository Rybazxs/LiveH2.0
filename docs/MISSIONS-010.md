# LiveH OS / PATCH-010 — Smart Reminders

Scope: MissionOS domain, its Today projection and the two existing screens. No Core, navigation, Android configuration or user data file changes.

- MissionDraft and MissionRecord get OPTIONAL Priority enum (Normal=0, Low=1, High=2, Critical=3). Existing schemaVersion=1 JSON files without the property deserialize as Normal. No destructive migrations.
- Creation form defaults to Normal. The Missions list shows non-normal priority and the number of deferrals.
- Existing reschedule operation and full schedule history stay unchanged. Completion and deletion behavior unchanged.
- Today shows up to 3 postponed **open** missions whose new due date is within the next 7 calendar days. Overdue missions remain only in the overdue panel, today's active missions remain in the Today list, and far-future postponed missions stay hidden. Critical/High rank before Normal/Low; due date orders ties.
- A task postponed a year out returns to the reminder panel when the new due date eventually enters the 7-day horizon. Its full historical events remain available even if older than 7 days.
- The Today reminder panel is view-only and reads the bounded summary contract, never the MissionOS data store.
- Priority is *not* a medical or safety risk score. It is a user-chosen mission label.

Android private data remain on the device. Repo script is fail-closed; makes a local backup of changed source files and skips already-installed files. Run `dotnet run --project tests/LiveH.ArchitectureChecks` after applying when .NET 10 is installed.
