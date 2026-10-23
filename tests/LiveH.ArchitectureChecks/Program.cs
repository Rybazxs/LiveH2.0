using LiveH.Contracts.Navigation;
using LiveH.Core.Navigation;
using LiveH.Modules.Today;

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

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
