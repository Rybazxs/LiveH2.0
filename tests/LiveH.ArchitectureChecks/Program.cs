using LiveH.Contracts.Groups;
using LiveH.Modules.Groups;
using LiveH.Contracts.Navigation;
using LiveH.Core.Navigation;
using LiveH.Modules.Today;
using LiveH.Modules.Missions;
using LiveH.Contracts.Missions;

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

}
finally { Directory.Delete(testFolder, recursive: true); }

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
