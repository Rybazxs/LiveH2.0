using LiveH.App.Views;
using LiveH.Contracts.Navigation;
using LiveH.Contracts.Missions;
using LiveH.Contracts.Groups;
using LiveH.Contracts.Track;
using LiveH.Contracts.Today;
using Microsoft.Maui.Dispatching;
using Microsoft.Maui.ApplicationModel;
using LiveH.Core.Navigation;

namespace LiveH.App;

public partial class MainPage : ContentPage
{
    private readonly NavigationCoordinator _navigation;
    private readonly ITodayReadModelSource _todaySource;
    private readonly IMissionRepository _missionRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly ITrackService _trackService;
    private readonly MissionCardCommands _cardCommands;
    // Serialize navigation-triggered starts so two rapid PLAY taps cannot race.
    private readonly SemaphoreSlim _trackSwitchGate = new(1, 1);
    // Keep the heaviest tab's native controls, selected date and scroll state.
    // Other tabs continue to use their existing lifecycle/refresh semantics.
    private MissionsView? _missionsView;
    private TodayView? _todayView;
    private TrackView? _trackView;
    private bool _missionsShownBefore;
    private bool _todayGestureAttached;
    private bool _trackGestureAttached;
    private IDispatcherTimer? _globalTrackTimer;
    private bool _headerRefreshBusy;

    private readonly Color _activeBackground = Color.FromArgb("#0D2633");
    private readonly Color _inactiveBackground = Color.FromArgb("#081019");
    private readonly Color _activeText = Color.FromArgb("#20D8F2");
    private readonly Color _inactiveText = Color.FromArgb("#8799A9");

    public MainPage(NavigationCoordinator navigation, ITodayReadModelSource todaySource,
        IMissionRepository missionRepository, IGroupRepository groupRepository, ITrackService trackService)
    {
        InitializeComponent();
        _navigation = navigation;
        _todaySource = todaySource;
        _missionRepository = missionRepository;
        _groupRepository = groupRepository;
        _trackService = trackService;
        _cardCommands = new MissionCardCommands(this, _missionRepository,
            _groupRepository, StartTrackingMissionAsync);
        VersionLabel.Text = "Version " + AppInfo.Current.VersionString;
        RenderCurrentRoute();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_globalTrackTimer is null)
        {
            _globalTrackTimer = Dispatcher.CreateTimer();
            _globalTrackTimer.Interval = TimeSpan.FromSeconds(1);
            _globalTrackTimer.Tick += (_, _) => _ = RefreshGlobalTrackHeaderAsync();
        }
        if (!_globalTrackTimer.IsRunning) _globalTrackTimer.Start();
        _ = RefreshGlobalTrackHeaderAsync();
    }

    protected override void OnDisappearing()
    {
        _globalTrackTimer?.Stop();
        base.OnDisappearing();
    }

    private async Task RefreshGlobalTrackHeaderAsync()
    {
        if (_headerRefreshBusy) return;
        _headerRefreshBusy = true;
        try
        {
            var current = await _trackService.CurrentAsync();
            if (current is null)
            {
                SystemStatusLabel.Text = "LOCAL SYSTEM / ONLINE NOT REQUIRED";
                SystemStatusLabel.TextColor = Color.FromArgb("#8799A9");
                return;
            }
            var session = current.Session;
            var elapsed = _trackService.CreditedAt(session, DateTimeOffset.UtcNow);
            var state = session.State == TrackSessionState.Paused ? "PAUZA" : "TRACKING";
            var time = $"{(long)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
            SystemStatusLabel.Text = $"{state} · {session.MissionTitle} · {time}";
            SystemStatusLabel.TextColor = Color.FromArgb("#78D8B0");
            SystemStatusLabel.LineBreakMode = LineBreakMode.TailTruncation;
            SystemStatusLabel.MaxLines = 1;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Global TRACK header: {ex}");
        }
        finally { _headerRefreshBusy = false; }
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
        // Never navigate from the More overlay or non-horizontal gestures.
        if (ModuleHost.Content is MissionsView { IsEditing: true } || MorePanel.IsVisible ||
            (e.Direction != SwipeDirection.Left && e.Direction != SwipeDirection.Right))
            return;

        if (_navigation.TryMoveHorizontal(e.Direction == SwipeDirection.Left))
            RenderCurrentRoute();
    }

    private void AttachHorizontalNavigation(View moduleView)
    {
        // Scrolling takes precedence in Missions. Its large ScrollView must
        // not compete with a horizontal navigation recognizer on Android.
        // Bottom tabs remain the reliable navigation method on this screen.
        if (moduleView is MissionsView) return;

        // The shell owns navigation; feature views do not import the coordinator.
        // Attach to the scrolling surface instead of its parent Grid, because
        // a ScrollView can consume touch input before it reaches that Grid.
        var surface = moduleView switch
        {
            TrackView track => (View)track.SwipeSurface,
            ContentView { Content: ScrollView scroll } => scroll,
            _ => moduleView
        };

        var gesture = new SwipeGestureRecognizer
        {
            Direction = SwipeDirection.Left | SwipeDirection.Right,
            Threshold = 65
        };
        gesture.Swiped += OnSwiped;
        surface.GestureRecognizers.Add(gesture);
    }

    public void OpenTrack() => NavigateTo(AppRoute.Track);

    private async Task<bool> ConfirmSwitchAsync(Guid? nextMissionId)
    {
        var current = await _trackService.CurrentAsync();
        if (current is null || current.Session.MissionId == nextMissionId) return true;
        return await DisplayAlertAsync("TRACK OS // PRZEŁĄCZ MISJĘ?",
            $"Obecnie mierzysz: {current.Session.MissionTitle}.\n" +
            "Zakończyć i zapisać jej czas, a następnie rozpocząć nową sesję?",
            "PRZEŁĄCZ", "ANULUJ");
    }

    private async Task StartTrackingMissionAsync(MissionRecord mission)
    {
        await _trackSwitchGate.WaitAsync();
        try
        {
            var current = await _trackService.CurrentAsync();
            if (current is not null && current.Session.MissionId == mission.Id)
            {
                OpenTrack(); // Existing session: never create a second one.
                return;
            }
            if (!await ConfirmSwitchAsync(mission.Id)) return;
            if (current is not null) await _trackService.StopAsync();
            await _trackService.StartAsync(mission.Id, mission.GroupId, mission.Title);
            OpenTrack();
        }
        catch (Exception ex) { await DisplayAlertAsync("TRACK OS", ex.Message, "OK"); }
        finally { _trackSwitchGate.Release(); }
    }

    private async Task<bool> QuickCreateAndTrackAsync(MissionDraft draft)
    {
        await _trackSwitchGate.WaitAsync();
        try
        {
            // Ask BEFORE creating the task, to avoid an orphan if switching is cancelled.
            if (!await ConfirmSwitchAsync(null)) return false;
            var groups = await _groupRepository.ListAsync();
            if (draft.GroupId is { } groupId &&
                !groups.Any(g => g.Id == groupId && !g.IsArchived))
                throw new InvalidOperationException("Ta grupa nie jest dostępna.");
            var mission = await _missionRepository.CreateAsync(draft);
            var running = await _trackService.CurrentAsync();
            if (running is not null) await _trackService.StopAsync();
            await _trackService.StartAsync(mission.Id, mission.GroupId, mission.Title);
            return true;
        }
        finally { _trackSwitchGate.Release(); }
    }

    private async Task OpenHistoryMissionAsync(Guid missionId)
    {
        var mission = (await _missionRepository.ListAsync()).FirstOrDefault(m => m.Id == missionId);
        if (mission is null)
        {
            await DisplayAlertAsync("HISTORIA TRACK", "Ta misja nie istnieje juÅ¼ w katalogu. Historia czasu pozostaje zapisana.", "OK");
            return;
        }
        var action = await DisplayActionSheetAsync(mission.Title, "ANULUJ", null,
            "EDYTUJ MISJÄ˜", "ROZPOCZNIJ / WRÃ“Ä† DO TRACK");
        if (action == "EDYTUJ MISJÄ˜")
            await _cardCommands.ExecuteAsync(mission, MissionCardCommand.Edit,
                async () => { if (_trackView is not null) await _trackView.RefreshAfterHistoryActionAsync(); });
        else if (action == "ROZPOCZNIJ / WRÃ“Ä† DO TRACK")
            await StartTrackingMissionAsync(mission);
    }

    private void NavigateTo(AppRoute route)
    {
        // Reopening the same tab must not discard the existing native view tree.
        // This avoids needless Android button creation and prevents stale controls
        // from racing against detached pages during async navigation.
        if (_navigation.Current == route)
        {
            MorePanel.IsVisible = false;
            SetActiveTabColors();
            return;
        }
        _navigation.NavigateTo(route);
        MorePanel.IsVisible = false;
        RenderCurrentRoute();
    }

    private void RenderCurrentRoute()
    {
        View moduleView = _navigation.Current switch
        {
            AppRoute.Today => _todayView ??= new TodayView(
                _todaySource, _missionRepository, _groupRepository, _trackService, _cardCommands),
            AppRoute.Missions => _missionsView ??= new MissionsView(
                _missionRepository, _groupRepository, _trackService, _cardCommands),
            AppRoute.Track => _trackView ??= new TrackView(
                _trackService, _groupRepository, QuickCreateAndTrackAsync, OpenHistoryMissionAsync),
            AppRoute.CampusLightOS => new PlaceholderView("CAMPUS LIGHTOS", "Campus LightOS"),
            AppRoute.BioOS => new PlaceholderView("BIO OS", "BioOS"),
            AppRoute.Analytics => new PlaceholderView("ANALITYKA", "AnalyticsOS"),
            AppRoute.Settings => new PlaceholderView("USTAWIENIA", "System settings"),
            _ => new TodayView(_todaySource, _missionRepository, _groupRepository, _trackService, _cardCommands)
        };
        // Attach gestures only on newly constructed screens. Reusing the
        // Missions tab avoids rebuilding its 42 calendar cells at every switch.
        if (moduleView is MissionsView missions)
        {
            var needsRefresh = _missionsShownBefore;
            _missionsShownBefore = true;
            ModuleHost.Content = missions;
            if (needsRefresh) _ = missions.RefreshAfterNavigationAsync();
        }
        else if (moduleView is TodayView today)
        {
            // Today is read-only. Reuse its controls, but refresh its read model
            // after navigation so external MissionOS changes remain visible.
            if (!_todayGestureAttached)
            {
                AttachHorizontalNavigation(today);
                _todayGestureAttached = true;
            }
            // TodayView.Loaded handles a fresh read after reattachment.
            ModuleHost.Content = today;
        }
        else if (moduleView is TrackView track)
        {
            // TrackOS Loaded/Unloaded starts/stops its existing timer. Recreating
            // the page on every switch churned Android handlers and gesture views.
            if (!_trackGestureAttached)
            {
                AttachHorizontalNavigation(track);
                _trackGestureAttached = true;
            }
            ModuleHost.Content = track;
        }
        else
        {
            AttachHorizontalNavigation(moduleView);
            ModuleHost.Content = moduleView;
        }
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
