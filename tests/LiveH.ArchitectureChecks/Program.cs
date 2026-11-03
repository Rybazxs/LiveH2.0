using LiveH.Contracts.Groups;
using LiveH.Modules.Groups;
using LiveH.Contracts.Navigation;
using LiveH.Core.Navigation;
using LiveH.Modules.Today;
using LiveH.Modules.Missions;
using LiveH.Contracts.Missions;
using LiveH.Contracts.Track;
using LiveH.Modules.Track;

var nav = new NavigationCoordinator();
Assert(nav.Current == AppRoute.Today, "The app must start on Today.");
Assert(nav.TryMoveHorizontal(left: true) && nav.Current == AppRoute.Missions,
    "A left swipe must open Missions.");
Assert(nav.TryMoveHorizontal(left: true) && nav.Current == AppRoute.Track,
    "A second left swipe must open Track.");
Assert(!nav.TryMoveHorizontal(left: true) && nav.Current == AppRoute.Track,
    "The last primary tab must not wrap around.");
Assert(nav.TryMoveHorizontal(left: false) && nav.Current == AppRoute.Missions,
    "Right swipe must navigate backward.");
nav.NavigateTo(AppRoute.BioOS);
Assert(!nav.TryMoveHorizontal(left: false) && nav.Current == AppRoute.BioOS,
    "Swiping must not force a BioOS route into primary tabs.");
var today = await new EmptyTodayReadModelSource().ReadAsync(DateOnly.FromDateTime(DateTime.Today));
Assert(today.Missions.Total == 0 && today.Missions.Overdue == 0 && today.BioNotices.Count == 0,
    "The first release must not fabricate private user data.");
Console.WriteLine("PASS: Navigation, module boundaries and empty Today data.");

// PATCH-016: Calendar is a projection over MissionOS, never another entity table.
// Undated OPEN missions follow the current day and future days with no badge;
// explicitly scheduled OPEN missions alone contribute to day badges.
var calendarBase = new DateOnly(2026, 10, 25);
var calendarDraft = new MissionDraft("Calendar single day", null, MissionTimingMode.Day,
    calendarBase, null, null, null, null, TimeZoneInfo.Local.Id);
var calendarDay = new MissionRecord(Guid.NewGuid(), calendarDraft.Title, null,
    MissionTimingMode.Day, calendarBase, null, null, null, null, TimeZoneInfo.Local.Id,
    MissionStatus.Open, DateTimeOffset.UtcNow, null);
var calendarRange = calendarDay with
{
    Id = Guid.NewGuid(), Title = "Cross midnight", TimingMode = MissionTimingMode.TimeRange,
    Day = null, StartsAtLocal = new DateTime(2026, 10, 25, 23, 45, 0),
    EndsAtLocal = new DateTime(2026, 10, 26, 0, 0, 0)
};
var calendarUrgent = calendarDay with
{
    Id = Guid.NewGuid(), TimingMode = MissionTimingMode.Urgent, Day = null
};
var calendarSpanning = calendarRange with
{
    Id = Guid.NewGuid(), StartsAtLocal = new DateTime(2026, 10, 31, 22, 0, 0),
    EndsAtLocal = new DateTime(2026, 11, 1, 1, 0, 0)
};
Assert(MissionCalendarQuery.IsUndated(calendarUrgent) &&
    !MissionCalendarQuery.IsScheduled(calendarUrgent) &&
    MissionCalendarQuery.IsScheduled(calendarDay),
    "An undated mission and an explicitly scheduled mission are disjoint projections.");
Assert(MissionCalendarQuery.OccursOn(calendarDay, calendarBase) &&
    !MissionCalendarQuery.OccursOn(calendarDay, calendarBase.AddDays(1)),
    "Day missions occur on their explicitly assigned date only.");
Assert(MissionCalendarQuery.OccursOn(calendarRange, calendarBase) &&
    !MissionCalendarQuery.OccursOn(calendarRange, calendarBase.AddDays(1)),
    "A time range ending at midnight does not appear on the following day.");
Assert(MissionCalendarQuery.OccursOn(calendarUrgent, calendarBase, calendarBase) &&
    MissionCalendarQuery.OccursOn(calendarUrgent, calendarBase.AddDays(1), calendarBase) &&
    MissionCalendarQuery.OccursOn(calendarUrgent, calendarBase.AddDays(30), calendarBase) &&
    !MissionCalendarQuery.OccursOn(calendarUrgent, calendarBase.AddDays(-1), calendarBase),
    "An active undated mission rolls forward, never backfills yesterday.");
Assert(MissionCalendarQuery.Overlaps(calendarUrgent, calendarBase.AddDays(-5),
        calendarBase, calendarBase) &&
    MissionCalendarQuery.Overlaps(calendarUrgent, calendarBase.AddDays(4),
        calendarBase.AddDays(6), calendarBase) &&
    !MissionCalendarQuery.Overlaps(calendarUrgent, calendarBase.AddDays(-3),
        calendarBase.AddDays(-2), calendarBase),
    "Range filtering shows undated tasks on today and future ranges only.");
Assert(MissionCalendarQuery.Overlaps(calendarSpanning, new DateOnly(2026, 11, 1),
    new DateOnly(2026, 11, 3)) &&
    !MissionCalendarQuery.Overlaps(calendarSpanning, new DateOnly(2026, 11, 2),
    new DateOnly(2026, 11, 3)), "Scheduled ranges cross months with an exclusive end.");
var octCounts = MissionCalendarQuery.CountsForMonth(
    new[] { calendarDay, calendarRange, calendarUrgent, calendarSpanning },
    2026, 10, calendarBase);
Assert(octCounts[calendarBase] == 2 && octCounts[new DateOnly(2026, 10, 31)] == 1 &&
    !octCounts.ContainsKey(calendarBase.AddDays(1)),
    "Calendar badges count scheduled OPEN missions only, not undated rolling tasks.");
var novCounts = MissionCalendarQuery.CountsForMonth(
    new[] { calendarDay, calendarRange, calendarUrgent, calendarSpanning },
    2026, 11, calendarBase);
Assert(novCounts[new DateOnly(2026, 11, 1)] == 1 && novCounts.Count == 1,
    "A multi-day scheduled task appears in both months without double storage.");
var urgentDone = calendarUrgent with
{
    Status = MissionStatus.Completed,
    CompletedAtUtc = new DateTimeOffset(2026, 10, 23, 13, 0, 0, TimeSpan.Zero)
};
var completedLocalDay = DateOnly.FromDateTime(urgentDone.CompletedAtUtc!.Value.ToLocalTime().DateTime);
Assert(MissionCalendarQuery.OccursOn(urgentDone, completedLocalDay, calendarBase) &&
    !MissionCalendarQuery.OccursOn(urgentDone, calendarBase.AddDays(3), calendarBase),
    "Completed undated missions stop rolling and remain in the completion-day archive.");
Assert(!MissionCalendarQuery.CountsForMonth(new[]
    { calendarDay with { Status = MissionStatus.Completed } },
    2026, 10, calendarBase).ContainsKey(calendarBase),
    "Completed scheduled missions do not inflate remaining-task badges.");
// Regression: three NA CITO and one postponed tomorrow, with no duplicate entities.
var screenshotMissions = new[]
{
    calendarUrgent, calendarUrgent with { Id = Guid.NewGuid() },
    calendarUrgent with { Id = Guid.NewGuid() },
    calendarDay with { Id = Guid.NewGuid(), Day = calendarBase.AddDays(1) }
};
var screenshotCounts = MissionCalendarQuery.CountsForMonth(screenshotMissions,
    2026, 10, calendarBase);
Assert(!screenshotCounts.ContainsKey(calendarBase) &&
    screenshotCounts[calendarBase.AddDays(1)] == 1 &&
    screenshotMissions.Count(m => MissionCalendarQuery.OccursOn(m, calendarBase, calendarBase)) == 3 &&
    screenshotMissions.Count(m => MissionCalendarQuery.OccursOn(m, calendarBase.AddDays(1), calendarBase)) == 4 &&
    screenshotMissions.All(m => m.TimingMode != MissionTimingMode.Urgent || m.Day is null),
    "3 rolling tasks show on today; tomorrow also shows 1 planned task; badge only 1.");
Console.WriteLine("PASS: Calendar read-model, rolling tasks, scheduled-only badges, ranges, and 3+1 regression.");

// Repository and calendar contract tests run entirely in a temporary directory.
var testFolder = Path.Combine(Path.GetTempPath(), "liveh-missions-tests-" + Guid.NewGuid());
Directory.CreateDirectory(testFolder);
try
{
    var file = Path.Combine(testFolder, "missions.json");
    IMissionRepository repository = new JsonMissionRepository(file);
    var todayDate = DateOnly.FromDateTime(DateTime.Today);
    var urgent = await repository.CreateAsync(new MissionDraft("Pilna misja", null,
        MissionTimingMode.Urgent, null, null, null, 30, null, TimeZoneInfo.Local.Id));
    var dated = await repository.CreateAsync(new MissionDraft("Misja na dziś", "Opis",
        MissionTimingMode.Day, todayDate, null, null, null, null, TimeZoneInfo.Local.Id));
    await repository.CreateAsync(new MissionDraft("Zaległa misja", null,
        MissionTimingMode.Day, todayDate.AddDays(-1), null, null, null, null, TimeZoneInfo.Local.Id));
    var rangeStart = DateTime.Today.AddDays(1).AddHours(9);
    await repository.CreateAsync(new MissionDraft("Jutrzejsza misja", null,
        MissionTimingMode.TimeRange, null, rangeStart, rangeStart.AddHours(2), 120,
        null, TimeZoneInfo.Local.Id));
    // A user-defined group label must remain optional and persist without a group catalog.
    var grouped = await repository.CreateAsync(new MissionDraft("Praca domowa", null,
        MissionTimingMode.Day, todayDate, null, null, 45, null,
        TimeZoneInfo.Local.Id, "Studia"));
    Assert(grouped.GroupName == "Studia", "Optional group should be persisted.");
    await repository.DeleteAsync(grouped.Id);

    var dashboard = new MissionDashboardSource(repository);
    var snap = await dashboard.ReadAsync(todayDate);
    Assert(snap.Total == 2 && snap.Completed == 0 && snap.ToDo.Count == 2 && snap.Overdue == 1,
        "Today must show real urgent and date missions, and yesterday as overdue.");
    // Move an overdue day mission to tomorrow; counters should update after reload.
    var overdueRecord = (await repository.ListAsync()).Single(x => x.Title == "Zaległa misja");
    await repository.RescheduleAsync(overdueRecord.Id, todayDate.AddDays(1));
    var moved = (await repository.ListAsync()).Single(x => x.Id == overdueRecord.Id);
    Assert(moved.Day == todayDate.AddDays(1) && moved.ScheduleHistory?.Count == 1,
        "Postponement must persist the new date and original schedule in history.");
    var afterMove = await dashboard.ReadAsync(todayDate);
    Assert(afterMove.Overdue == 0 && afterMove.Total == 2,
        "Postponed overdue mission must disappear from overdue today.");
    await repository.CompleteAsync(dated.Id);
    var afterDone = await dashboard.ReadAsync(todayDate);
    Assert(afterDone.Completed == 1 && afterDone.Total == 2,
        "Completed progress must be shown on Today.");
    IMissionRepository reopened = new JsonMissionRepository(file);
    Assert((await reopened.ListAsync()).Count == 4,
        "Missions must survive repository recreation (app restart).");
    await reopened.DeleteAsync(urgent.Id);
    Assert((await reopened.ListAsync()).Count == 3, "Delete must persist.");
    try
    {
        await reopened.CreateAsync(new MissionDraft("Bad range", null, MissionTimingMode.TimeRange,
            null, rangeStart, rangeStart.AddMinutes(-1), null, null, TimeZoneInfo.Local.Id));
        throw new Exception("Expected invalid range exception.");
    }
    catch (ArgumentException) { }
    // Keep the local time window length after moving timed missions.
    var timed = (await reopened.ListAsync()).Single(x => x.Title == "Jutrzejsza misja");
    await reopened.RescheduleAsync(timed.Id, todayDate.AddDays(3));
    var timedMoved = (await reopened.ListAsync()).Single(x => x.Id == timed.Id);
    Assert(timedMoved.StartsAtLocal?.Hour == 9 &&
        timedMoved.EndsAtLocal - timedMoved.StartsAtLocal == TimeSpan.FromHours(2),
        "Timed mission must preserve start hour and duration.");
    try
    {
        await reopened.RescheduleAsync(dated.Id, todayDate.AddDays(1));
        throw new Exception("Expected completed mission to reject rescheduling.");
    }
    catch (InvalidOperationException) { }
    // Existing schema v1 data remains readable after a model extension.
    var afterRestart = new JsonMissionRepository(file);
    var migrated = (await afterRestart.ListAsync()).Single(x => x.Id == timed.Id);
    Assert(migrated.ScheduleHistory?.Count == 1,
        "Reschedule history must survive a repository restart.");
    // A clean day with only future tasks must suggest the nearest ones, not pretend
    // that tomorrow is already today. Postponed tasks preserve their warning flag.
    var previewFolder = Path.Combine(testFolder, "previews.json");
    IMissionRepository previews = new JsonMissionRepository(previewFolder);
    var upcomingLater = await previews.CreateAsync(new MissionDraft("Późniejsza", null,
        MissionTimingMode.Day, todayDate.AddDays(4), null, null, null, null,
        TimeZoneInfo.Local.Id));
    var upcomingSoon = await previews.CreateAsync(new MissionDraft("Najbliższa", null,
        MissionTimingMode.Day, todayDate.AddDays(1), null, null, null, null,
        TimeZoneInfo.Local.Id));
    await previews.RescheduleAsync(upcomingLater.Id, todayDate.AddDays(2));
    var previewSource = new MissionDashboardSource(previews);
    var preview = await previewSource.ReadAsync(todayDate);
    // PATCH-010 intentionally separates postponed items from ordinary upcoming tasks.
    // Do not duplicate a postponed mission in both panels.
    Assert(preview.Total == 0 && preview.ToDo.Count == 0 &&
        preview.Upcoming is { Count: 1 } && preview.Upcoming[0].Title == "Najbliższa" &&
        preview.RescheduledReminders is { Count: 1 } &&
        preview.RescheduledReminders[0].Title == "Późniejsza" &&
        preview.RescheduledReminders[0].RescheduleCount == 1,
        "Empty today must separate nearest future tasks from postponed reminders.");
    await previews.CompleteAsync(upcomingSoon.Id);
    var afterPreviewDone = await previewSource.ReadAsync(todayDate);
    Assert(afterPreviewDone.Upcoming is { Count: 0 } &&
        afterPreviewDone.RescheduledReminders is { Count: 1 } &&
        afterPreviewDone.RescheduledReminders[0].Title == "Późniejsza",
        "Completed tasks disappear from upcoming, while postponed reminders remain.");
    Console.WriteLine("PASS: Mission persistence, scheduling, postponing, history, optional group and deletion.");
    Console.WriteLine("PASS: Nearest future tasks and rescheduled visual metadata.");

    // Old v1 files have no Priority. They must deserialize as NORMAL without migration.
    var oldJsonFile = Path.Combine(testFolder, "legacy-priority.json");
    await File.WriteAllTextAsync(oldJsonFile,
        """{"schemaVersion":1,"missions":[{"id":"c470d726-1674-46ce-bc71-f45d44737821","title":"Stara misja","timingMode":0,"timeZoneId":"Europe/Warsaw","status":0,"createdAtUtc":"2026-01-01T09:00:00+00:00"}]}""");
    var legacyMission = (await new JsonMissionRepository(oldJsonFile).ListAsync()).Single();
    Assert(legacyMission.Priority == MissionPriority.Normal,
        "Legacy v1 JSON without Priority must remain NORMAL.");

    var reminderFile = Path.Combine(testFolder, "smart-reminders.json");
    IMissionRepository remindersRepo = new JsonMissionRepository(reminderFile);
    var tomorrow = todayDate.AddDays(1);
    var critical = await remindersRepo.CreateAsync(new MissionDraft("Pilna jutro", null,
        MissionTimingMode.Day, todayDate.AddDays(-2), null, null, 20, null,
        TimeZoneInfo.Local.Id, Priority: MissionPriority.Critical));
    await remindersRepo.RescheduleAsync(critical.Id, tomorrow);
    var normal = await remindersRepo.CreateAsync(new MissionDraft("Normalna za 3 dni", null,
        MissionTimingMode.Day, todayDate.AddDays(1), null, null, null, null,
        TimeZoneInfo.Local.Id));
    await remindersRepo.RescheduleAsync(normal.Id, todayDate.AddDays(3));
    var distant = await remindersRepo.CreateAsync(new MissionDraft("Za rok", null,
        MissionTimingMode.Day, todayDate.AddDays(5), null, null, null, null,
        TimeZoneInfo.Local.Id, Priority: MissionPriority.Critical));
    await remindersRepo.RescheduleAsync(distant.Id, todayDate.AddDays(365));
    var previewWithPriority = await new MissionDashboardSource(remindersRepo).ReadAsync(todayDate);
    Assert(previewWithPriority.Total == 0 && previewWithPriority.RescheduledReminders is { Count: 2 }
        && previewWithPriority.RescheduledReminders[0].Title == "Pilna jutro"
        && previewWithPriority.RescheduledReminders.All(x => x.Title != "Za rok"),
        "Only near-term postponed tasks appear, sorted by priority, not the one due in a year.");
    Assert(previewWithPriority.Upcoming is { Count: 0 },
        "Postponed tasks use their own panel and must not duplicate upcoming tasks.");
    var saved = (await new JsonMissionRepository(reminderFile).ListAsync())
        .Single(x => x.Id == critical.Id);
    Assert(saved.Priority == MissionPriority.Critical && saved.ScheduleHistory is { Count: 1 },
        "Priority and reschedule history must survive restart.");
    Assert(!MissionRules.ShouldRemindOfReschedule(saved, tomorrow, DateTime.Now.AddDays(1)),
        "A postponed task already on today's list must not duplicate in reminder panel.");
    await remindersRepo.CompleteAsync(critical.Id);
    Assert((await new MissionDashboardSource(remindersRepo).ReadAsync(todayDate))
        .RescheduledReminders?.Count == 1,
        "Completed postponed missions must disappear from reminders.");
    try
    {
        await remindersRepo.CreateAsync(new MissionDraft("Błędny priorytet", null,
            MissionTimingMode.Urgent, null, null, null, null, null,
            TimeZoneInfo.Local.Id, Priority: (MissionPriority)99));
        throw new Exception("Expected invalid priority rejection.");
    }
    catch (ArgumentException) { }
    Console.WriteLine("PASS: Smart postponement hints, priorities and legacy JSON compatibility.");

    // GroupsOS: separate local storage and hierarchical public contract.
    IGroupRepository groups = new JsonGroupRepository(Path.Combine(testFolder, "groups-v1.json"));
    var studies = await groups.CreateAsync("Studia");
    var math = await groups.CreateAsync("Matematyka", studies.Id);
    var algebra = await groups.CreateAsync("Algebra", math.Id);
    var tuning = await groups.CreateAsync("Rinet CRS Tuning");
    var allGroups = await groups.ListAsync();
    Assert(GroupHierarchy.PathFor(allGroups, algebra.Id) == "Studia / Matematyka / Algebra",
        "Hierarchy must produce stable nested paths.");
    Assert(GroupHierarchy.IsDescendantOrSelf(allGroups, algebra.Id, studies.Id) &&
        !GroupHierarchy.IsDescendantOrSelf(allGroups, tuning.Id, studies.Id),
        "Folder membership must not cross unrelated branches.");
    try
    {
        await groups.MoveAsync(studies.Id, algebra.Id);
        throw new Exception("Cycle should have been rejected.");
    }
    catch (InvalidOperationException) { }
    try
    {
        await groups.CreateAsync("MATEMATYKA", studies.Id);
        throw new Exception("Duplicate sibling name should have been rejected.");
    }
    catch (InvalidOperationException) { }
    await groups.MoveAsync(algebra.Id, tuning.Id);
    await groups.RenameAsync(algebra.Id, "Warsztat");
    await groups.SetArchivedAsync(tuning.Id, true);
    var reloadedGroups = await new JsonGroupRepository(Path.Combine(testFolder, "groups-v1.json")).ListAsync();
    Assert(reloadedGroups.Single(g => g.Id == algebra.Id).ParentGroupId == tuning.Id &&
        reloadedGroups.Single(g => g.Id == algebra.Id).Name == "Warsztat" &&
        reloadedGroups.Single(g => g.Id == tuning.Id).IsArchived,
        "Move, rename and archive must persist after app restart.");
    try
    {
        await groups.CreateAsync("Nie wolno", tuning.Id);
        throw new Exception("Creating inside archived branch should have failed.");
    }
    catch (InvalidOperationException) { }
    try
    {
        await groups.MoveAsync(math.Id, algebra.Id);
        throw new Exception("Moving into archived branch should have failed.");
    }
    catch (InvalidOperationException) { }


    // Existing v1 mission records remain valid: only a nullable GroupId is added.
    var task = await repository.CreateAsync(new MissionDraft("Notatki na studia", null,
        MissionTimingMode.Day, todayDate, null, null, null, null,
        TimeZoneInfo.Local.Id, GroupName: "Matematyka", GroupId: math.Id));
    Assert(task.GroupId == math.Id, "Mission must store an opaque foreign group ID.");
    await repository.AssignGroupAsync(task.Id, tuning.Id, "Rinet CRS Tuning");
    var groupTask = (await new JsonMissionRepository(file).ListAsync()).Single(m => m.Id == task.Id);
    Assert(groupTask.GroupId == tuning.Id && groupTask.GroupName == "Rinet CRS Tuning",
        "Moving a mission between groups must persist without changing other fields.");
    await repository.AssignGroupAsync(task.Id, null, null);
    Assert((await repository.ListAsync()).Single(m => m.Id == task.Id).GroupId is null,
        "A mission can be unassigned without deletion.");
    Assert(legacyMission.GroupId is null,
        "Old schema v1 missions without GroupId must remain unassigned.");
    Assert(!typeof(JsonGroupRepository).Assembly.GetReferencedAssemblies()
        .Any(a => a.Name == "LiveH.Modules.Missions"),
        "Groups module must not reference MissionOS implementation.");
    Assert(!typeof(JsonMissionRepository).Assembly.GetReferencedAssemblies()
        .Any(a => a.Name == "LiveH.Modules.Groups"),
        "MissionOS must not reference Groups implementation.");
    Console.WriteLine("PASS: Nested groups, cycle checks, persistence, migration-safe group IDs and module isolation.");
    // TrackOS isolation and deterministic time accounting (no Android required).
    var fake = new TestTrackClock(new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero));
    var gateway = new TestTrackGateway();
    var trackPath = Path.Combine(testFolder, "track-sessions-v1.json");
    ITrackService track = new JsonTrackService(trackPath, fake, gateway);
    var first = await track.StartAsync(task.Id, math.Id, "Nauka algebry");
    Assert(first.Session.GroupIdAtStart == math.Id && first.Credited == TimeSpan.Zero,
        "New session must snapshot its original group and start from zero.");
    Assert(gateway.LastDue == fake.UtcNow.AddMinutes(30),
        "A 30-minute activity checkpoint must be scheduled.");
    fake.Advance(TimeSpan.FromMinutes(12));
    Assert((await track.CurrentAsync())?.Credited == TimeSpan.FromMinutes(12),
        "Time must be derived from UTC instants, not UI timer ticks.");
    var paused = await track.PauseAsync();
    fake.Advance(TimeSpan.FromHours(8));
    Assert((await track.CurrentAsync())?.Credited == TimeSpan.FromMinutes(12),
        "Paused work must never accrue time in the background.");
    await track.ResumeAsync();
    fake.Advance(TimeSpan.FromMinutes(18));
    Assert((await track.CurrentAsync())?.NeedsConfirmation == true,
        "At 30 minutes, a positive confirmation must be required.");
    fake.Advance(TimeSpan.FromMinutes(2));
    Assert((await track.CurrentAsync())?.Credited == TimeSpan.FromMinutes(30),
        "Unconfirmed time is capped at the 30-minute boundary.");
    var continued = await track.ConfirmAsync();
    Assert(continued.Session.Confirmations == 1 && continued.Credited == TimeSpan.FromMinutes(32),
        "Confirmation within 5-minute grace must credit valid continued work.");
    fake.Advance(TimeSpan.FromMinutes(4));
    var finalized = await track.StopAsync();
    Assert(finalized.Credited == TimeSpan.FromMinutes(36),
        "Stop must preserve both segments and confirmed work.");
    Assert((await track.MissionTotalAsync(task.Id)) == TimeSpan.FromMinutes(36),
        "A mission total must sum its sessions exactly once.");
    var groupTotal = await track.GroupsTotalAsync(new[] { studies.Id, math.Id });
    Assert(groupTotal == TimeSpan.FromMinutes(36),
        "A parent-group rollup may include several IDs without double-counting.");
    var restoredTrack = new JsonTrackService(trackPath, fake, new TestTrackGateway());
    Assert((await restoredTrack.MissionTotalAsync(task.Id)) == TimeSpan.FromMinutes(36),
        "The private session log must survive a repository restart.");
    await repository.AssignGroupAsync(task.Id, tuning.Id, "Rinet CRS Tuning");
    Assert((await restoredTrack.GroupsTotalAsync(new[] { math.Id })) == TimeSpan.FromMinutes(36) &&
        (await restoredTrack.GroupsTotalAsync(new[] { tuning.Id })) == TimeSpan.Zero,
        "Reassigning a mission must not rewrite old group accounting.");
    var next = await restoredTrack.StartAsync(task.Id, tuning.Id, "Nauka algebry");
    fake.Advance(TimeSpan.FromMinutes(36));
    var expired = await restoredTrack.CurrentAsync();
    Assert(expired is null, "No answer for 5 minutes after checkpoint must close the session.");
    var history = await restoredTrack.SessionsAsync();
    var last = history.Single(s => s.Id == next.Session.Id);
    Assert(last.State == TrackSessionState.Stopped && last.EndedByInactivity &&
        restoredTrack.CreditedAt(last, fake.UtcNow) == TimeSpan.FromMinutes(30),
        "The inactive session must be capped at its last confirmed 30-minute checkpoint.");
    Assert((await restoredTrack.MissionTotalAsync(task.Id)) == TimeSpan.FromMinutes(66) &&
        (await restoredTrack.GroupsTotalAsync(new[] { tuning.Id })) == TimeSpan.FromMinutes(30),
        "All totals must remain independent and correct after auto-stop.");
    try
    {
        await restoredTrack.ConfirmAsync();
        throw new Exception("Expired sessions must not be confirmable.");
    }
    catch (InvalidOperationException) { }
    var newActive = await restoredTrack.StartAsync(task.Id, tuning.Id, "Trzecia sesja");
    try
    {
        await restoredTrack.StartAsync(task.Id, tuning.Id, "Druga aktywna");
        throw new Exception("A second active session must not be allowed.");
    }
    catch (InvalidOperationException) { }
    fake.Advance(TimeSpan.FromMinutes(5));
    await restoredTrack.StopAsync();
    Assert((await restoredTrack.MissionTotalAsync(task.Id)) == TimeSpan.FromMinutes(71),
        "Already finished and manual sessions must be preserved without overwriting.");
    Assert(!typeof(JsonTrackService).Assembly.GetReferencedAssemblies().Any(a =>
        a.Name is "LiveH.Modules.Missions" or "LiveH.Modules.Groups"),
        "Track engine must depend on contracts only, not other module implementations.");
    Console.WriteLine("PASS: TrackOS persistence, pause/resume, checkpoints, timeout, rollups and module isolation.");
    // PATCH-013: Editing a mission must never create a second entity, erase history,
    // or redirect already-recorded TrackOS sessions to a new ID.
    var editable = await repository.CreateAsync(new MissionDraft("Remont silnika", "Stan początkowy",
        MissionTimingMode.Day, todayDate, null, null, 45, null, TimeZoneInfo.Local.Id,
        GroupName: "Rinet", GroupId: tuning.Id));
    await repository.RescheduleAsync(editable.Id, todayDate.AddDays(1));
    var beforeEdit = (await repository.ListAsync()).Single(x => x.Id == editable.Id);
    var edited = await repository.UpdateAsync(editable.Id,
        new MissionDraft("Remont silnika — etap 2", "Nowy opis", MissionTimingMode.Urgent,
            null, null, null, 90, null, TimeZoneInfo.Local.Id,
            GroupName: "Matematyka", Priority: MissionPriority.High, GroupId: math.Id));
    Assert(edited.Id == editable.Id && edited.CreatedAtUtc == beforeEdit.CreatedAtUtc &&
        edited.Status == beforeEdit.Status && edited.ScheduleHistory?.Count == 1 &&
        edited.Title == "Remont silnika — etap 2" && edited.GroupId == math.Id &&
        edited.Priority == MissionPriority.High && edited.TimingMode == MissionTimingMode.Urgent,
        "Editing must preserve the mission identity and postponement history.");
    var persistedEdit = (await new JsonMissionRepository(file).ListAsync())
        .Single(x => x.Id == editable.Id);
    Assert(persistedEdit.Notes == "Nowy opis" && persistedEdit.EstimatedMinutes == 90,
        "Edited fields must survive app restart without schema migration.");
    await repository.CompleteAsync(editable.Id);
    var completedEdit = await repository.UpdateAsync(editable.Id,
        new MissionDraft("Archiwum etap 2", "Zrobione", MissionTimingMode.Urgent,
            null, null, null, null, null, TimeZoneInfo.Local.Id));
    Assert(completedEdit.Status == MissionStatus.Completed && completedEdit.CompletedAtUtc is not null,
        "Editing a completed mission must never silently reopen it.");
    Assert(!typeof(JsonMissionRepository).Assembly.GetReferencedAssemblies().Any(a =>
        a.Name is "LiveH.Modules.Track" or "LiveH.Modules.Groups"),
        "MissionOS must remain independent of TrackOS and GroupsOS implementations.");
    // Pause/stop must also work immediately; closing a paused session cannot count idle time.
    var independentId = Guid.NewGuid();
    var immediate = await restoredTrack.StartAsync(independentId, null, "Nowa misja bez grupy");
    fake.Advance(TimeSpan.FromSeconds(4));
    await restoredTrack.PauseAsync();
    fake.Advance(TimeSpan.FromHours(3));
    var stopPaused = await restoredTrack.StopAsync();
    Assert(stopPaused.Session.State == TrackSessionState.Stopped &&
        stopPaused.Credited == TimeSpan.FromSeconds(4),
        "STOP after PAUSE must work immediately and must not count idle hours.");
    var startedAgain = await restoredTrack.StartAsync(independentId, null, "Nowa sesja");
    await restoredTrack.StopAsync();
    Assert((await restoredTrack.MissionTotalAsync(independentId)) == TimeSpan.FromSeconds(4) &&
        startedAgain.Session.Id != immediate.Session.Id,
        "New sessions keep their own IDs and sum only actual activity.");
    Console.WriteLine("PASS: Editing preserves identity/history, completed state and timing data.");
    Console.WriteLine("PASS: Immediate PAUSE/STOP and restart never accrue hidden idle time.");



}
finally { Directory.Delete(testFolder, recursive: true); }

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

sealed class TestTrackClock : ITrackClock
{
    public DateTimeOffset UtcNow { get; private set; }
    public TestTrackClock(DateTimeOffset now) => UtcNow = now;
    public void Advance(TimeSpan by) => UtcNow += by;
}

sealed class TestTrackGateway : ITrackReminderGateway
{
    public DateTimeOffset? LastDue { get; private set; }
    public Task ScheduleAsync(Guid id, DateTimeOffset due)
    {
        LastDue = due;
        return Task.CompletedTask;
    }
    public Task CancelAsync() { LastDue = null; return Task.CompletedTask; }
}
