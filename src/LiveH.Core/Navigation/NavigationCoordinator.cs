using LiveH.Contracts.Navigation;

namespace LiveH.Core.Navigation;

// Owns navigation state; it never references a screen, UI type, or feature module.
public sealed class NavigationCoordinator
{
    private static readonly AppRoute[] MainTabs =
    [
        AppRoute.Today,
        AppRoute.Missions,
        AppRoute.Track
    ];

    public AppRoute Current { get; private set; } = AppRoute.Today;

    public void NavigateTo(AppRoute route) => Current = route;

    public bool TryMoveHorizontal(bool left)
    {
        var currentIndex = Array.IndexOf(MainTabs, Current);
        if (currentIndex < 0) return false; // "More" destinations aren't swipe tabs.

        var nextIndex = currentIndex + (left ? 1 : -1);
        if (nextIndex < 0 || nextIndex >= MainTabs.Length) return false;

        Current = MainTabs[nextIndex];
        return true;
    }
}
