# LiveH OS / PATCH-009 — mission presentation

Scope: read-only UI projections + completed section. No changes to JSON persistence, mission status, scheduling, delete, Core or navigation.

- Active missions stay inside TWOJE MISJE.
- Completed missions are presented in a separate, visually subdued WYKONANE // ARCHIWUM section.
- Today's ToDo list: separate outlined cards for each mission.
- A postponed mission (`ScheduleHistory.Count > 0`) gets a rose warning border and ODROCZONA label, even if its new date is in the future.
- Truly overdue tasks keep their warning treatment in the separate ZALEGŁE panel.
- If there is no unfinished task today, Today displays up to 3 nearest unfinished scheduled future missions, with their real future dates. Today's progress counters do not include them.
- If nothing is scheduled, show an honest empty state (no invented tasks).

Manual test:
1. Create a mission for today, a mission for tomorrow and a mission for later.
2. Confirm outlined cards for today and no future tasks in the today list.
3. Complete today's mission; confirm it appears under WYKONANE // ARCHIWUM in Missions and nearest future tasks show in Today.
4. Postpone a mission to a future date; confirm its warning outline and ODROCZONA marker even before the new due date.
5. Confirm that genuinely overdue tasks are shown in the separate red ZALEGŁE panel.
6. Restart the application. All stored missions remain present.

This patch does not alter mission data files on the device.
