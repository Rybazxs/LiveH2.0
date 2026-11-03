using System.Diagnostics;
using System.Text.Json;
using System.Globalization;
using LiveH.Contracts.Missions;
using LiveH.Contracts.Groups;
using LiveH.Contracts.Track;
using LiveH.Modules.Missions;

namespace LiveH.App.Views;

public partial class MissionsView : ContentView
{
    private readonly IMissionRepository _repository;
    private readonly IGroupRepository _groupsRepository;
    private readonly ITrackService _tracking;
    private readonly MissionCardCommands _commands;
    private Dictionary<Guid, TimeSpan> _timeByMission = new();
    private IReadOnlyList<GroupRecord> _groups = Array.Empty<GroupRecord>();
    private GroupRecord[] _selectableGroups = Array.Empty<GroupRecord>();
    private GroupsWorkspaceView? _workspace;
    private readonly MissionCalendarView _calendar;
    private IReadOnlyList<MissionRecord> _allMissions = Array.Empty<MissionRecord>();
    private readonly List<MissionCardView> _visibleCards = new();
    // A calendar filter must move existing cards, not recreate Android views.
    // This cache is scoped to a single loaded MissionOS snapshot; every
    // repository reload invalidates it to avoid stale mission data/actions.
    private readonly Dictionary<Guid, MissionCardView> _cardCache = new();
    private readonly Dictionary<Guid, string> _cardVersions = new();
    private string? _dataSignature;
    private int _dataGeneration;
    private string? _renderedListKey;
    private bool _reloadQueued;
    private bool _loadingMissions;
    private bool _undatedExpanded = true;
    private bool _scheduledExpanded = true;
    private CalendarSection? _undatedSection;
    private CalendarSection? _scheduledSection;

    private sealed class CalendarSection
    {
        public required VerticalStackLayout Container { get; init; }
        public required VerticalStackLayout Items { get; init; }
        public required Label Chevron { get; init; }
        public required Label Count { get; init; }
        public required bool Undated { get; init; }
        public MissionRecord[] Missions { get; set; } = Array.Empty<MissionRecord>();
        public string EmptyMessage { get; set; } = "";
    }
    private bool _missionsLoaded;
    private Guid? _preferredGroupId;
    private bool _busy;
    private readonly Color _text = Color.FromArgb("#EEF4FA");
    private readonly Color _muted = Color.FromArgb("#8799A9");
    private readonly Color _cyan = Color.FromArgb("#20D8F2");
    private readonly Color _panel = Color.FromArgb("#0D1C26");
    private readonly Color _border = Color.FromArgb("#243F50");
    private readonly Color _green = Color.FromArgb("#78D8B0");
    private readonly Color _rose = Color.FromArgb("#F7A6B6");

    // Prevent horizontal tab swipes while the group workspace or an editing panel is open.
    public bool IsEditing => FormPanel.IsVisible || GroupsHost.IsVisible ||
        _visibleCards.Any(c => c.IsExpanded);

    public MissionsView(IMissionRepository repository, IGroupRepository groupsRepository,
        ITrackService tracking, MissionCardCommands commands)
    {
        InitializeComponent();
        _repository = repository;
        _groupsRepository = groupsRepository;
        _tracking = tracking;
        _commands = commands;
        _calendar = new MissionCalendarView();
        _calendar.FilterChanged += () => { if (_missionsLoaded) RenderMissionRows(); };
        CalendarHost.Content = _calendar;
        GroupPicker.Items.Add("BEZ GRUPY");
        GroupPicker.SelectedIndex = 0;
        TimingPicker.SelectedIndex = 0;
        PriorityPicker.SelectedIndex = 0;
        var today = DateTime.Today;
        DayPicker.Date = today;
        StartDatePicker.Date = today;
        EndDatePicker.Date = today;
        StartTimePicker.Time = new TimeSpan(9, 0, 0);
        EndTimePicker.Time = new TimeSpan(10, 0, 0);
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs args)
    {
        Loaded -= OnLoaded;
        await LoadMissionsAsync();
    }

    private void OnToggleMyMissions(object? sender, TappedEventArgs args)
    {
        MissionItems.IsVisible = !MissionItems.IsVisible;
        MissionsSectionTitle.Text = MissionItems.IsVisible ? "TWOJE MISJE  ▾" : "TWOJE MISJE  ▸";
    }

    private async void OnToggleGroups(object? sender, EventArgs args)
    {
        GroupsHost.IsVisible = !GroupsHost.IsVisible;
        GroupsButton.Text = GroupsHost.IsVisible ? "▾ TWOJE GRUPY" : "▸ TWOJE GRUPY";
        CalendarHost.IsVisible = true;
        if (!GroupsHost.IsVisible)
        {
            await LoadMissionsAsync();
            return;
        }
        FormPanel.IsVisible = false;
        AddButton.Text = "+ MISJA";
        _workspace ??= new GroupsWorkspaceView(_groupsRepository, _repository, _tracking,
            OnCreateInGroupAsync, OnGroupsChangedAsync, _commands);
        GroupsHost.Content = _workspace;
        try { await _workspace.RefreshAsync(); }
        catch (Exception ex)
        {
            LoadError.Text = "Błąd grup: " + ex.Message;
            LoadError.IsVisible = true;
        }
    }

    private async Task OnGroupsChangedAsync()
    {
        await LoadMissionsAsync();
    }

    private Task OnCreateInGroupAsync(Guid groupId)
    {
        _preferredGroupId = groupId;
        SetGroupPicker(groupId);
        GroupsHost.IsVisible = false;
        GroupsButton.Text = "▸ TWOJE GRUPY";
        CalendarHost.IsVisible = true;
        FormPanel.IsVisible = true;
        AddButton.Text = "ZAMKNIJ";
        HideError();
        return Task.CompletedTask;
    }

    private async Task<bool> LoadGroupOptionsAsync(IReadOnlyList<MissionRecord> missions)
    {
        var migrated = false;
        _groups = await _groupsRepository.ListAsync();
        // Idempotent bridge from PATCH-008 legacy free-text labels.
        // Group database and mission database remain independently owned.
        foreach (var m in missions.Where(m => m.GroupId is null &&
            !string.IsNullOrWhiteSpace(m.GroupName)))
        {
            var name = m.GroupName!.Trim();
            if (name.Length > 80) continue;
            var root = _groups.FirstOrDefault(g => g.ParentGroupId is null &&
                string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
            if (root is null)
            {
                try { root = await _groupsRepository.CreateAsync(name); }
                catch (InvalidOperationException)
                {
                    _groups = await _groupsRepository.ListAsync();
                    root = _groups.FirstOrDefault(g => g.ParentGroupId is null &&
                        string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
                }
            }
            if (root is not null)
            {
                await _repository.AssignGroupAsync(m.Id, root.Id, root.Name);
                migrated = true;
            }
        }
        var selected = _preferredGroupId;
        if (selected is null && GroupPicker.SelectedIndex > 0 &&
            GroupPicker.SelectedIndex <= _selectableGroups.Length)
            selected = _selectableGroups[GroupPicker.SelectedIndex - 1].Id;
        if (migrated) _groups = await _groupsRepository.ListAsync();
        _selectableGroups = _groups.Where(g => !IsArchivedForAssignment(g.Id))
            .OrderBy(g => GroupHierarchy.PathFor(_groups, g.Id), StringComparer.OrdinalIgnoreCase).ToArray();
        var titles = new[] { "BEZ GRUPY" }.Concat(_selectableGroups.Select(g =>
            GroupHierarchy.PathFor(_groups, g.Id))).ToArray();
        // Updating Picker.Items triggers Android layout, even if its contents did
        // not change. Navigation must not churn a native picker on every visit.
        if (GroupPicker.Items.Count != titles.Length ||
            !GroupPicker.Items.SequenceEqual(titles))
        {
            GroupPicker.Items.Clear();
            foreach (var title in titles) GroupPicker.Items.Add(title);
        }
        SetGroupPicker(selected);
        return migrated;
    }

    private bool IsArchivedForAssignment(Guid groupId)
    {
        var seen = new HashSet<Guid>();
        Guid? cursor = groupId;
        while (cursor is { } id && seen.Add(id))
        {
            var group = _groups.FirstOrDefault(g => g.Id == id);
            if (group is null || group.IsArchived) return true;
            cursor = group.ParentGroupId;
        }
        return false;
    }

    private void SetGroupPicker(Guid? id)
    {
        var index = id is { } groupId ? Array.FindIndex(_selectableGroups, g => g.Id == groupId) : -1;
        GroupPicker.SelectedIndex = index + 1;
        _preferredGroupId = null;
    }

    private Guid? SelectedGroupId => GroupPicker.SelectedIndex > 0 &&
        GroupPicker.SelectedIndex <= _selectableGroups.Length
            ? _selectableGroups[GroupPicker.SelectedIndex - 1].Id : null;

    private void OnToggleForm(object? sender, EventArgs e)
    {
        FormPanel.IsVisible = !FormPanel.IsVisible;
        AddButton.Text = FormPanel.IsVisible ? "ZAMKNIJ" : "+ MISJA";
        if (FormPanel.IsVisible)
        {
            GroupsHost.IsVisible = false;
            GroupsButton.Text = "▸ TWOJE GRUPY";
            CalendarHost.IsVisible = true;
        }
        HideError();
    }

    private void OnCancel(object? sender, EventArgs e)
    {
        FormPanel.IsVisible = false;
        AddButton.Text = "+ MISJA";
        HideError();
    }

    private void OnTimingChanged(object? sender, EventArgs args)
    {
        DayFields.IsVisible = TimingPicker.SelectedIndex == 1;
        RangeFields.IsVisible = TimingPicker.SelectedIndex == 2;
    }

    private async void OnSave(object? sender, EventArgs args)
    {
        if (_busy) return;
        HideError();
        try
        {
            int? estimate = null;
            if (!string.IsNullOrWhiteSpace(EstimateEntry.Text))
            {
                if (!int.TryParse(EstimateEntry.Text.Trim(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var parsed) || parsed < 1 || parsed > 10080)
                    throw new ArgumentException("Szacowany czas: podaj liczbę 1–10080 minut.");
                estimate = parsed;
            }

            var mode = (MissionTimingMode)Math.Max(0, TimingPicker.SelectedIndex);
            DateOnly? day = null;
            DateTime? starts = null;
            DateTime? ends = null;
            if (mode == MissionTimingMode.Day)
            {
                day = DateOnly.FromDateTime(DayPicker.Date ??
                    throw new ArgumentException("Wybierz dzień misji."));
            }
            else if (mode == MissionTimingMode.TimeRange)
            {
                starts = (StartDatePicker.Date ??
                    throw new ArgumentException("Wybierz datę rozpoczęcia.")).Date
                    + (StartTimePicker.Time ??
                    throw new ArgumentException("Wybierz godzinę rozpoczęcia."));
                ends = (EndDatePicker.Date ??
                    throw new ArgumentException("Wybierz datę zakończenia.")).Date
                    + (EndTimePicker.Time ??
                    throw new ArgumentException("Wybierz godzinę zakończenia."));
            }

            var priority = (MissionPriority)Math.Max(0, PriorityPicker.SelectedIndex);
            var groupId = SelectedGroupId;
            var group = _selectableGroups.FirstOrDefault(g => g.Id == groupId);
            var draft = new MissionDraft(
                TitleEntry.Text ?? "", NotesEditor.Text, mode, day, starts, ends,
                estimate, CategoryId: null, TimeZoneId: TimeZoneInfo.Local.Id,
                GroupName: group?.Name, Priority: priority, GroupId: groupId);
            MissionRules.Validate(draft);
            _busy = true;
            SaveButton.IsEnabled = false;
            await _repository.CreateAsync(draft);
            TitleEntry.Text = "";
            NotesEditor.Text = "";
            EstimateEntry.Text = "";
            SetGroupPicker(null);
            PriorityPicker.SelectedIndex = 0;
            FormPanel.IsVisible = false;
            AddButton.Text = "+ MISJA";
            await LoadMissionsAsync();
            if (_workspace is not null) await _workspace.RefreshAsync();
        }
        catch (Exception ex)
        {
            FormError.Text = $"Nie udało się zapisać misji: {ex.Message}";
            FormError.IsVisible = true;
        }
        finally
        {
            _busy = false;
            SaveButton.IsEnabled = true;
        }
    }

    private void HideError()
    {
        FormError.Text = "";
        FormError.IsVisible = false;
    }

    // MainPage reuses the Missions tab; refresh its data after navigating back.
    // The View and calendar selection remain alive across tab transitions.
    public Task RefreshAfterNavigationAsync() => LoadMissionsAsync();

    private async Task LoadMissionsAsync()
    {
        if (_loadingMissions) { _reloadQueued = true; return; }
        _loadingMissions = true;
        LoadError.IsVisible = false;
        try
        {
            var list = await _repository.ListAsync();
            var migrated = await LoadGroupOptionsAsync(list);
            var missions = migrated ? await _repository.ListAsync() : list;
            var sessions = await _tracking.SessionsAsync();
            // Ignore refreshes which did not change any source record. This is
            // especially important when returning from Today/Track: previously
            // every tab switch destroyed and recreated every native mission card.
            var signature = JsonSerializer.Serialize(new
            {
                Missions = missions, Groups = _groups, Sessions = sessions
            });
            if (_missionsLoaded && signature == _dataSignature)
                return;
            _dataSignature = signature;
            _dataGeneration++;
            _allMissions = missions;
            var now = DateTimeOffset.UtcNow;
            _timeByMission = sessions.GroupBy(s => s.MissionId).ToDictionary(
                g => g.Key, g => TimeSpan.FromTicks(g.Sum(s => _tracking.CreditedAt(s, now).Ticks)));
            // Invalidate only deleted cards. Changed records are detected by
            // AddMissionCard's own version key, leaving other cards untouched.
            var knownIds = missions.Select(x => x.Id).ToHashSet();
            foreach (var id in _cardCache.Keys.Where(id => !knownIds.Contains(id)).ToArray())
            {
                if (_cardCache[id].Parent is VerticalStackLayout parent)
                    parent.Children.Remove(_cardCache[id]);
                _cardCache.Remove(id);
                _cardVersions.Remove(id);
            }
            _missionsLoaded = true;
            _calendar.SetMissions(_allMissions);
            RenderMissionRows();
        }
        catch (Exception ex)
        {
            _missionsLoaded = false;
            _renderedListKey = null;
            MissionItems.Children.Clear();
            CompletedItems.Children.Clear();
            _visibleCards.Clear();
            CompletedPanel.IsVisible = false;
            CountLabel.Text = "—";
            LoadError.Text = $"Błąd odczytu lokalnych danych: {ex.Message}";
            LoadError.IsVisible = true;
        }
        finally
        {
            _loadingMissions = false;
            var pending = _reloadQueued;
            _reloadQueued = false;
            if (pending && IsLoaded) await LoadMissionsAsync();
        }
    }

    private void RenderMissionRows()
    {
        if (!_missionsLoaded) return;
#if DEBUG
        var startedAt = Stopwatch.GetTimestamp();
#endif
        var visible = _allMissions.Where(_calendar.Includes).ToArray();
        var active = visible.Where(x => x.Status == MissionStatus.Open)
            .OrderBy(x => MissionRules.IsOverdue(x, DateTime.Now) ? 0 : 1)
            .ThenByDescending(x => x.Priority switch
            {
                MissionPriority.Critical => 3,
                MissionPriority.High => 2,
                MissionPriority.Normal => 1,
                _ => 0
            })
            .ThenBy(x => x.TimingMode == MissionTimingMode.Urgent ? 0 : 1)
            .ThenBy(x => x.StartsAtLocal ?? x.Day?.ToDateTime(TimeOnly.MinValue) ?? DateTime.MaxValue)
            .ThenBy(x => x.CreatedAtUtc).ToArray();
        var completed = visible.Where(x => x.Status == MissionStatus.Completed)
            .OrderByDescending(x => x.CompletedAtUtc).ToArray();
        // Selecting another day with an identical result must be a NO-OP for
        // native layout. Only the calendar cell highlights should change.
        var listKey = $"{_dataGeneration}:{(_calendar.Mode == MissionCalendarView.ViewMode.All ? 0 : 1)}:" +
            string.Join(",", active.Select(x => x.Id)) + ":" +
            string.Join(",", completed.Select(x => x.Id));
        if (listKey == _renderedListKey) return;
        _renderedListKey = listKey;
        // Detach before reusing a card on another section; MAUI requires one parent.
        foreach (var card in _cardCache.Values)
            if (card.Parent is VerticalStackLayout parent)
                parent.Children.Remove(card);
        MissionItems.Children.Clear();
        CompletedItems.Children.Clear();
        _visibleCards.Clear();
        CountLabel.Text = active.Length.ToString(CultureInfo.GetCultureInfo("pl-PL"));
        CompletedCountLabel.Text = completed.Length.ToString(CultureInfo.GetCultureInfo("pl-PL"));
        CompletedPanel.IsVisible = completed.Length > 0;

        if (_calendar.Mode == MissionCalendarView.ViewMode.All)
        {
            if (active.Length == 0)
                MissionItems.Children.Add(EmptyLabel(_allMissions.Count == 0
                    ? "Brak misji. Dodaj pierwszą powyżej."
                    : "Brak aktywnych misji."));
            else
                foreach (var mission in active) AddMissionCard(MissionItems, mission);
        }
        else
        {
            // The calendar is a view of the MissionOS repository, not a second
            // calendar store. Exactly one group owns each active mission.
            var undated = active.Where(MissionCalendarQuery.IsUndated).ToArray();
            var scheduled = active.Where(MissionCalendarQuery.IsScheduled).ToArray();
            AddCollapsibleSection("BIEŻĄCE MISJE", undated, _undatedExpanded, true,
                "Brak bieżących misji bez terminu.");
            AddCollapsibleSection("ZAPLANOWANE", scheduled, _scheduledExpanded, false,
                "Brak misji zaplanowanych na wybrany dzień lub zakres.");
        }
        foreach (var mission in completed) AddMissionCard(CompletedItems, mission);
#if DEBUG
        // Measures synchronous UI tree work, not full Android frame time.
        // A repeated >16 ms result is a concrete target for profiling.
        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        if (elapsed > TimeSpan.FromMilliseconds(16))
            Debug.WriteLine($"[LiveH UX] Missions render {elapsed.TotalMilliseconds:F1} ms, " +
                $"cards={active.Length + completed.Length}, mode={_calendar.Mode}");
#endif
    }

    private Label EmptyLabel(string message) => new()
    {
        Text = message, FontSize = 12, TextColor = _muted
    };

    private void AddMissionCard(VerticalStackLayout parent, MissionRecord mission)
    {
        var group = mission.GroupId is { } id && _groups.Any(g => g.Id == id)
            ? GroupHierarchy.PathFor(_groups, id) : mission.GroupName;
        _timeByMission.TryGetValue(mission.Id, out var time);
        var version = JsonSerializer.Serialize(new
        {
            Mission = mission, GroupPath = group, TrackedTicks = time.Ticks
        });
        if (!_cardCache.TryGetValue(mission.Id, out var card) ||
            !_cardVersions.TryGetValue(mission.Id, out var oldVersion) || oldVersion != version)
        {
            card = new MissionCardView(mission, group, time, _commands, LoadMissionsAsync);
            _cardCache[mission.Id] = card;
            _cardVersions[mission.Id] = version;
        }
        parent.Children.Add(card);
        _visibleCards.Add(card);
    }

    private void AddCollapsibleSection(string title, MissionRecord[] missions,
        bool isExpanded, bool undated, string emptyMessage)
    {
        // Headers and their recognizers are created once. Only the contents of
        // the changed calendar date are rebound. This avoids GC churn and
        // gesture-handler accumulation during rapid calendar navigation.
        var section = undated
            ? (_undatedSection ??= CreateCalendarSection(title, true))
            : (_scheduledSection ??= CreateCalendarSection(title, false));
        section.Missions = missions;
        section.EmptyMessage = emptyMessage;
        section.Count.Text = missions.Length.ToString(CultureInfo.GetCultureInfo("pl-PL"));
        section.Chevron.Text = isExpanded ? "⌄" : "›";
        section.Items.Children.Clear();
        section.Items.IsVisible = isExpanded;
        if (isExpanded) PopulateCalendarSection(section);
        MissionItems.Children.Add(section.Container);
    }

    private CalendarSection CreateCalendarSection(string title, bool undated)
    {
        var header = new Grid
        {
            ColumnSpacing = 8, Padding = new Thickness(8, 7),
            BackgroundColor = Color.FromArgb("#102631")
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var chevron = new Label { Text = "⌄", TextColor = _cyan,
            FontSize = 16, VerticalOptions = LayoutOptions.Center };
        var label = new Label { Text = title, TextColor = _cyan, FontSize = 11,
            FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
        var count = new Label { Text = "0", TextColor = _text, FontSize = 11,
            VerticalOptions = LayoutOptions.Center };
        header.Children.Add(chevron);
        Grid.SetColumn(label, 1);
        header.Children.Add(label);
        Grid.SetColumn(count, 2);
        header.Children.Add(count);
        var items = new VerticalStackLayout { Spacing = 8 };
        var container = new VerticalStackLayout { Spacing = 7, Children = { header, items } };
        var section = new CalendarSection
        {
            Container = container, Items = items, Chevron = chevron,
            Count = count, Undated = undated
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (_loadingMissions) return;
            var expanded = section.Undated ? !_undatedExpanded : !_scheduledExpanded;
            if (section.Undated) _undatedExpanded = expanded;
            else _scheduledExpanded = expanded;
            if (expanded) PopulateCalendarSection(section);
            else
            {
                foreach (var card in section.Items.Children.OfType<MissionCardView>())
                    _visibleCards.Remove(card);
            }
            section.Items.IsVisible = expanded;
            section.Chevron.Text = expanded ? "⌄" : "›";
        };
        header.GestureRecognizers.Add(tap);
        return section;
    }

    private void PopulateCalendarSection(CalendarSection section)
    {
        section.Items.Children.Clear();
        if (section.Missions.Length == 0)
            section.Items.Children.Add(EmptyLabel(section.EmptyMessage));
        else
            foreach (var mission in section.Missions)
                AddMissionCard(section.Items, mission);
    }

}
