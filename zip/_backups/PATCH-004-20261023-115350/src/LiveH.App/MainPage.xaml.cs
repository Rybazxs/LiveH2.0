using LiveH.App.Views;
using LiveH.Contracts.Navigation;
using LiveH.Contracts.Today;
using LiveH.Core.Navigation;

namespace LiveH.App;

public partial class MainPage : ContentPage
{
    private readonly NavigationCoordinator _navigation;
    private readonly ITodayReadModelSource _todaySource;

    private readonly Color _activeBackground = Color.FromArgb("#0D2633");
    private readonly Color _inactiveBackground = Color.FromArgb("#081019");
    private readonly Color _activeText = Color.FromArgb("#20D8F2");
    private readonly Color _inactiveText = Color.FromArgb("#8799A9");

    public MainPage(NavigationCoordinator navigation, ITodayReadModelSource todaySource)
    {
        InitializeComponent();
        _navigation = navigation;
        _todaySource = todaySource;
        RenderCurrentRoute();
    }

    private void OnTabTapped(object? sender, TappedEventArgs args)
    {
        switch (args.Parameter as string)
        {
            case "today": NavigateTo(AppRoute.Today); break;
            case "missions": NavigateTo(AppRoute.Missions); break;
            case "track": NavigateTo(AppRoute.Track); break;
            case "more":
                MorePanel.IsVisible = !MorePanel.IsVisible;
                SetActiveTabColors();
                break;
        }
    }

    private void OnMoreDestinationClicked(object? sender, EventArgs e)
    {
        var route = ((sender as Button)?.CommandParameter as string) switch
        {
            "campus" => AppRoute.CampusLightOS,
            "bio" => AppRoute.BioOS,
            "analytics" => AppRoute.Analytics,
            "settings" => AppRoute.Settings,
            _ => AppRoute.Today
        };
        NavigateTo(route);
    }

    private void OnSwiped(object? sender, SwipedEventArgs e)
    {
        if (MorePanel.IsVisible) return;
        if (_navigation.TryMoveHorizontal(e.Direction == SwipeDirection.Left))
            RenderCurrentRoute();
    }

    private void NavigateTo(AppRoute route)
    {
        _navigation.NavigateTo(route);
        MorePanel.IsVisible = false;
        RenderCurrentRoute();
    }

    private void RenderCurrentRoute()
    {
        ModuleHost.Content = _navigation.Current switch
        {
            AppRoute.Today => new TodayView(_todaySource),
            AppRoute.Missions => new PlaceholderView("MISJE", "MissionOS"),
            AppRoute.Track => new PlaceholderView("TRACK", "TrackOS"),
            AppRoute.CampusLightOS => new PlaceholderView("CAMPUS LIGHTOS", "Campus LightOS"),
            AppRoute.BioOS => new PlaceholderView("BIO OS", "BioOS"),
            AppRoute.Analytics => new PlaceholderView("ANALITYKA", "AnalyticsOS"),
            AppRoute.Settings => new PlaceholderView("USTAWIENIA", "System settings"),
            _ => new TodayView(_todaySource)
        };
        SetActiveTabColors();
    }

    private void SetActiveTabColors()
    {
        SetTab(TodayTab, TodayTabLabel, _navigation.Current == AppRoute.Today && !MorePanel.IsVisible);
        SetTab(MissionsTab, MissionsTabLabel, _navigation.Current == AppRoute.Missions && !MorePanel.IsVisible);
        SetTab(TrackTab, TrackTabLabel, _navigation.Current == AppRoute.Track && !MorePanel.IsVisible);
        SetTab(MoreTab, MoreTabLabel, MorePanel.IsVisible ||
            _navigation.Current is AppRoute.CampusLightOS or AppRoute.BioOS or AppRoute.Analytics or AppRoute.Settings);
    }

    private void SetTab(Border border, Label label, bool selected)
    {
        border.BackgroundColor = selected ? _activeBackground : _inactiveBackground;
        label.TextColor = selected ? _activeText : _inactiveText;
    }
}
