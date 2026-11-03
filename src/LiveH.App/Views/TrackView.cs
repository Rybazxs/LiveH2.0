using LiveH.Contracts.Groups;
using LiveH.Contracts.Track;
using Microsoft.Maui.Dispatching;

namespace LiveH.App.Views;

// TrackOS screen.
//
// STABILITY RULE (PATCH-014A): the visual tree is built exactly ONCE, in the constructor.
// A refresh only mutates properties of views that already exist (text, visibility, enabled)
// and replaces the history rows with brand-new views. No view instance is ever added to a
// second parent, so Android can no longer throw
//   "The specified child already has a parent. You must call removeView() ..."
// The PAUSE / STOP bar and the quick-create form are permanent, so refreshing can never
// remove them either.
public sealed class TrackView : ContentView
{
    private readonly ITrackService _track;
    private readonly IGroupRepository _groups;
    private readonly Func<string, Guid?, Task<bool>> _quickCreate;

    // Permanent visual tree.
    private readonly VerticalStackLayout _body = new() { Spacing = 12 };
    private readonly Grid _controls;
    private readonly Button _pauseButton;
    private readonly Button _stopButton;
    private readonly Button _startButton;
    private readonly Button _confirmButton;
    private readonly Entry _quickTitle;
    private readonly Picker _quickGroup;
    private readonly Label _errorLabel;
    private readonly Border _statusPanel;
    private readonly Border _emptyPanel;
    private readonly Label _dockHint;
    private readonly Label _stateLabel;
    private readonly Label _missionLabel;
    private readonly Label _timeLabel;
    private readonly Label _checkpointLabel;
    private readonly VerticalStackLayout _confirmBlock;
    private readonly Label _historyHeader;
    private readonly VerticalStackLayout _historyHost;

    public ScrollView SwipeSurface { get; }

    // State.
    private IDispatcherTimer? _clock;
    private bool _busy;
    private bool _loading;
    private GroupRecord[] _availableGroups = Array.Empty<GroupRecord>();
    private string? _errorText;
    private TrackStatus? _current;
    private IReadOnlyList<TrackSession> _history = Array.Empty<TrackSession>();
    private string _historySignature = "";

    private static readonly Color Cyan = Color.FromArgb("#20D8F2");
    private static readonly Color Text = Color.FromArgb("#EEF4FA");
    private static readonly Color Muted = Color.FromArgb("#8799A9");
    private static readonly Color Rose = Color.FromArgb("#F7A6B6");

    public TrackView(ITrackService track, IGroupRepository groups, Func<string, Guid?, Task<bool>> quickCreate)
    {
        _track = track;
        _groups = groups;
        _quickCreate = quickCreate;

        // ---- header ----
        _errorLabel = Lbl("", Rose, 12);
        _errorLabel.IsVisible = false;

        // ---- active session panel (persistent labels, updated in place) ----
        _stateLabel = Lbl("", Cyan, 11, true);
        _missionLabel = Lbl("", Text, 17, true);
        _timeLabel = Lbl("00:00:00", Text, 35, true);
        _timeLabel.HorizontalTextAlignment = TextAlignment.Center;
        _checkpointLabel = Lbl("", Muted, 11);
        _checkpointLabel.IsVisible = false;
        _confirmButton = Control("TAK, PRACUJĘ DALEJ", Cyan);
        _confirmButton.Clicked += async (_, _) =>
            await ExecuteAsync("CHECKPOINT CONFIRM", () => _track.ConfirmAsync());
        _confirmBlock = new VerticalStackLayout
        {
            Spacing = 8,
            IsVisible = false,
            Children =
            {
                Lbl("NADAL WYKONUJESZ MISJĘ?", Rose, 12, true),
                Lbl("Masz 5 minut na potwierdzenie. Brak odpowiedzi zatrzymuje czas na granicy kontroli.", Muted, 11),
                _confirmButton
            }
        };
        _statusPanel = Panel(new VerticalStackLayout
        {
            Spacing = 11,
            Children = { _stateLabel, _missionLabel, _timeLabel, _checkpointLabel, _confirmBlock }
        });
        _statusPanel.IsVisible = false;
        _emptyPanel = Panel(Lbl("Brak aktywnej sesji. Uruchom ▶ przy misji albo rozpocznij nową poniżej.", Muted, 12));
        _dockHint = Lbl("PAUZA I ZAKOŃCZ SĄ ZAWSZE NA DOLE EKRANU", Muted, 10);
        _dockHint.IsVisible = false;

        // ---- quick create (Entry/Picker are created once and never re-parented) ----
        _quickTitle = new Entry
        {
            Placeholder = "Nazwa nowej misji", MaxLength = 160,
            TextColor = Text, PlaceholderColor = Muted, FontSize = 14
        };
        _quickGroup = new Picker { Title = "Grupa (opcjonalnie)", TextColor = Text, TitleColor = Muted };
        _quickGroup.Items.Add("BEZ GRUPY");
        _quickGroup.SelectedIndex = 0;
        _startButton = Control("+ UTWÓRZ I START", Cyan);
        _startButton.Clicked += async (_, _) =>
        {
            _quickTitle.Unfocus(); // close the keyboard before the list refreshes
            await ExecuteAsync("QUICK START", async () =>
            {
                var title = _quickTitle.Text?.Trim();
                if (string.IsNullOrWhiteSpace(title))
                    throw new ArgumentException("Podaj nazwę misji.");
                if (await _quickCreate(title, SelectedGroupId())) _quickTitle.Text = "";
            });
        };
        var createPanel = Panel(new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                Lbl("NOWA MISJA + TRACK", Cyan, 13, true),
                Lbl("Wpisz nazwę. Pozostałe dane uzupełnisz później w Misjach.", Muted, 11),
                _quickTitle,
                _quickGroup,
                _startButton
            }
        });

        // ---- history ----
        _historyHeader = Lbl("HISTORIA · ŁĄCZNIE 0 h 00 min", Cyan, 12, true);
        _historyHost = new VerticalStackLayout { Spacing = 12 };

        _body.Children.Add(Lbl("TRACK OS // LIVEH", Cyan, 26, true));
        _body.Children.Add(Lbl("JEDNA SESJA · LOKALNY POMIAR", Muted, 10));
        _body.Children.Add(_errorLabel);
        _body.Children.Add(_statusPanel);
        _body.Children.Add(_emptyPanel);
        _body.Children.Add(_dockHint);
        _body.Children.Add(createPanel);
        _body.Children.Add(_historyHeader);
        _body.Children.Add(_historyHost);

        // ---- permanent PAUSE / STOP bar ----
        _pauseButton = Control("Ⅱ PAUZA", Cyan);
        _stopButton = Control("■ ZAKOŃCZ", Rose);
        _pauseButton.Clicked += async (_, _) =>
        {
            var resume = _current?.Session.State == TrackSessionState.Paused;
            await ExecuteAsync(resume ? "RESUME" : "PAUSE", async () =>
            {
                if (resume) await _track.ResumeAsync();
                else await _track.PauseAsync();
            });
        };
        _stopButton.Clicked += async (_, _) => await ExecuteAsync("STOP", () => _track.StopAsync());
        _controls = new Grid
        {
            ColumnSpacing = 9,
            Padding = new Thickness(20, 10, 20, 15),
            BackgroundColor = Color.FromArgb("#05090E"),
            IsVisible = false
        };
        _controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        _controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        _controls.Children.Add(_pauseButton);
        Grid.SetColumn(_stopButton, 1);
        _controls.Children.Add(_stopButton);

        var scroll = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(22, 13, 22, 28),
                Children = { _body }
            }
        };
        SwipeSurface = scroll;
        var layout = new Grid { BackgroundColor = Color.FromArgb("#05090E") };
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.Children.Add(scroll);
        Grid.SetRow(_controls, 1);
        layout.Children.Add(_controls);
        Content = layout;

        Loaded += OnLoaded;
        Unloaded += (_, _) => _clock?.Stop();
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        // Loaded can fire again during MAUI navigation. Never overlap initial reads.
        if (_loading) return;
        _loading = true;
        try
        {
            if (_clock is null)
            {
                _clock = Dispatcher.CreateTimer();
                _clock.Interval = TimeSpan.FromSeconds(1);
                _clock.Tick += OnTick;
            }
            await LoadGroupsAsync();
            await ReloadAsync();
            if (IsLoaded && !_clock.IsRunning) _clock.Start();
        }
        catch (Exception ex)
        {
            _errorText = ex.Message;
            System.Diagnostics.Debug.WriteLine($"[LiveH TrackOS] loaded: {ex}");
            SafeApply();
        }
        finally { _loading = false; }
    }

    private async Task LoadGroupsAsync()
    {
        try
        {
            var catalog = await _groups.ListAsync();
            var selected = SelectedGroupId();
            _availableGroups = catalog.Where(g => CanAssign(catalog, g.Id))
                .OrderBy(g => GroupHierarchy.PathFor(catalog, g.Id)).ToArray();
            _quickGroup.Items.Clear();
            _quickGroup.Items.Add("BEZ GRUPY");
            foreach (var group in _availableGroups)
                _quickGroup.Items.Add(GroupHierarchy.PathFor(_availableGroups, group.Id));
            var index = selected is { } id ? Array.FindIndex(_availableGroups, g => g.Id == id) + 1 : 0;
            _quickGroup.SelectedIndex = Math.Max(0, index);
        }
        catch (Exception ex)
        {
            _errorText = ex.Message;
            System.Diagnostics.Debug.WriteLine($"[LiveH TrackOS] groups: {ex}");
            // Keep the warning visible when the state refresh succeeds.
        }
    }

    private Guid? SelectedGroupId() =>
        _quickGroup.SelectedIndex > 0 && _quickGroup.SelectedIndex <= _availableGroups.Length
            ? _availableGroups[_quickGroup.SelectedIndex - 1].Id : null;

    // 1 Hz heartbeat: only touches Label.Text of existing views; never rebuilds anything.
    private void OnTick(object? sender, EventArgs e)
    {
        try
        {
            if (_busy || _current is not { } status) return;
            var now = DateTimeOffset.UtcNow;
            var session = status.Session;
            if (session.State == TrackSessionState.Running && session.CheckpointDueAtUtc is { } due)
            {
                var pastGrace = now > due + TimeSpan.FromMinutes(5);
                var confirmationShouldShow = now >= due;
                if (pastGrace || confirmationShouldShow != _confirmBlock.IsVisible)
                {
                    _ = ReloadAsync(); // the service decides: confirm prompt or auto-stop
                    return;
                }
                SetText(_checkpointLabel, CheckpointText(now, due));
            }
            SetText(_timeLabel, DisplayTime(_track.CreditedAt(session, now)));
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[LiveH TrackOS] tick: {ex}"); }
    }

    private static bool CanAssign(IReadOnlyList<GroupRecord> catalog, Guid id)
    {
        var visited = new HashSet<Guid>();
        Guid? cursor = id;
        while (cursor is { } key && visited.Add(key))
        {
            var group = catalog.FirstOrDefault(g => g.Id == key);
            if (group is null || group.IsArchived) return false;
            cursor = group.ParentGroupId;
        }
        return cursor is null;
    }

    private async Task LoadStateAsync()
    {
        _current = await _track.CurrentAsync();
        _history = await _track.SessionsAsync();
    }

    // Never throws: a failed refresh shows an error line instead of killing the app.
    private async Task ReloadAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            await LoadStateAsync();
        }
        catch (Exception ex)
        {
            _errorText = ex.Message;
            System.Diagnostics.Debug.WriteLine($"[LiveH TrackOS] reload: {ex}");
        }
        finally
        {
            _busy = false;
            SafeApply();
        }
    }

    private async Task ExecuteAsync(string label, Func<Task> action)
    {
        if (_busy)
        {
            System.Diagnostics.Debug.WriteLine($"[LiveH TrackOS] {label} ignored (busy)");
            return;
        }
        _busy = true;
        SafeApply(); // greys out PAUSE / STOP while the action runs

        try
        {
            await action();
            await LoadStateAsync();
            _errorText = null;
        }
        catch (Exception ex)
        {
            _errorText = ex.Message;
            System.Diagnostics.Debug.WriteLine($"[LiveH TrackOS] {label}: {ex}");
        }
        finally
        {
            _busy = false;
            SafeApply();
        }
    }

    private void SafeApply()
    {
        try { Apply(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[LiveH TrackOS] render: {ex}"); }
    }

    // Pure property updates on permanent views.
    private void Apply()
    {
        var now = DateTimeOffset.UtcNow;
        SetText(_errorLabel, _errorText is null ? "" : "BŁĄD: " + _errorText);
        _errorLabel.IsVisible = _errorText is not null;

        if (_current is { } status)
        {
            var session = status.Session;
            var running = session.State == TrackSessionState.Running;
            _statusPanel.IsVisible = true;
            _emptyPanel.IsVisible = false;
            _dockHint.IsVisible = true;
            SetText(_stateLabel, running ? "● TRWA POMIAR" : "Ⅱ PAUZA");
            SetText(_missionLabel, session.MissionTitle);
            SetText(_timeLabel, DisplayTime(_track.CreditedAt(session, now)));
            if (running && session.CheckpointDueAtUtc is { } due)
            {
                _checkpointLabel.IsVisible = true;
                SetText(_checkpointLabel, CheckpointText(now, due));
                _confirmBlock.IsVisible = now >= due;
            }
            else
            {
                _checkpointLabel.IsVisible = false;
                _confirmBlock.IsVisible = false;
            }
        }
        else
        {
            _statusPanel.IsVisible = false;
            _emptyPanel.IsVisible = true;
            _dockHint.IsVisible = false;
            _confirmBlock.IsVisible = false;
        }

        var hasSession = _current is not null;
        _controls.IsVisible = hasSession;
        _pauseButton.IsEnabled = !_busy;
        _stopButton.IsEnabled = !_busy;
        _confirmButton.IsEnabled = !_busy;
        _startButton.IsEnabled = !_busy;
        _pauseButton.Text = _current?.Session.State == TrackSessionState.Paused ? "▶ WZNÓW" : "Ⅱ PAUZA";

        RebuildHistory(now);
    }

    // History rows are brand-new views every time, so there is nothing to re-parent.
    // The rebuild is skipped entirely when nothing visible changed.
    private void RebuildHistory(DateTimeOffset now)
    {
        // The local-first store is immutable on read. Bound the displayed history,
        // and avoid constructing new visual children on every timer tick.
        var shown = _history.OrderByDescending(s => s.StartedAtUtc).Take(25).ToArray();
        var total = _history.Aggregate(TimeSpan.Zero, (sum, s) => sum + _track.CreditedAt(s, now));
        SetText(_historyHeader, "HISTORIA · ŁĄCZNIE " + Duration(total));
        var signature = string.Join('|', shown.Select(s =>
            $"{s.Id:N}:{s.MissionTitle}:{(int)s.State}:{s.EndedByInactivity}:{(long)_track.CreditedAt(s, now).TotalMinutes}"));
        if (signature == _historySignature) return;
        _historySignature = signature;

        _historyHost.Children.Clear();
        foreach (var entry in shown)
        {
            var detail = new VerticalStackLayout { Spacing = 4 };
            detail.Children.Add(Lbl(entry.MissionTitle, Text, 13, true));
            detail.Children.Add(Lbl($"{entry.StartedAtUtc.ToLocalTime():dd.MM.yyyy HH:mm} · " +
                $"{Duration(_track.CreditedAt(entry, now))} · " +
                (entry.State == TrackSessionState.Stopped
                    ? entry.EndedByInactivity ? "AUTO STOP" : "ZAKOŃCZONA"
                    : entry.State == TrackSessionState.Paused ? "PAUZA" : "TRWA"), Muted, 10));
            _historyHost.Children.Add(Panel(detail));
        }
    }

    private static string CheckpointText(DateTimeOffset now, DateTimeOffset due) =>
        now >= due ? "POTWIERDŹ AKTYWNOŚĆ"
            : $"Kontrola za {Math.Max(1, Math.Ceiling((due - now).TotalMinutes)):0} min";

    private static void SetText(Label label, string text)
    {
        if (label.Text != text) label.Text = text;
    }

    private static Button Control(string title, Color color) => new()
    {
        Text = title, TextColor = color, FontSize = 11,
        BackgroundColor = Color.FromArgb("#10202B"), HeightRequest = 47,
        FontAttributes = FontAttributes.Bold
    };

    private static Border Panel(View contents) => new()
    {
        Stroke = Color.FromArgb("#284858"), StrokeThickness = 1,
        StrokeShape = new Microsoft.Maui.Controls.Shapes.Rectangle(),
        Padding = new Thickness(13), BackgroundColor = Color.FromArgb("#0D1C26"),
        Content = contents
    };

    private static Label Lbl(string text, Color color, int size, bool bold = false) => new()
    {
        Text = text, TextColor = color, FontSize = size,
        FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
        LineBreakMode = LineBreakMode.WordWrap
    };

    public static string Duration(TimeSpan time) => $"{(long)time.TotalHours} h {time.Minutes:D2} min";
    private static string DisplayTime(TimeSpan time) =>
        $"{(long)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
}
