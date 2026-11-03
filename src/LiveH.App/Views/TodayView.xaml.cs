using System.Globalization;
using LiveH.Contracts.Today;
using LiveH.Contracts.Missions;
using LiveH.Contracts.Groups;
using LiveH.Contracts.Track;

namespace LiveH.App.Views;

// Today is still an aggregator: mission mutations route to the one app coordinator.
// Counters come exclusively from Today's read model, cards use live mission IDs.
public partial class TodayView : ContentView
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");
    private readonly ITodayReadModelSource _source;
    private readonly IMissionRepository _missions;
    private readonly IGroupRepository _groups;
    private readonly ITrackService _tracking;
    private readonly MissionCardCommands _commands;
    private Dictionary<Guid, MissionRecord> _missionById = new();
    private IReadOnlyList<GroupRecord> _groupList = Array.Empty<GroupRecord>();
    private Dictionary<Guid, TimeSpan> _timeByMission = new();
    private bool _loading;
    private bool _refreshQueued;

    public TodayView(ITodayReadModelSource source, IMissionRepository missions,
        IGroupRepository groups, ITrackService tracking, MissionCardCommands commands)
    {
        InitializeComponent();
        _source = source;
        _missions = missions;
        _groups = groups;
        _tracking = tracking;
        _commands = commands;
        DateLabel.Text = DateTime.Today.ToString("dddd, d MMMM yyyy", Polish);
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        // A reused tab gets Loaded again after Android reattaches it.
        // Read fresh MissionOS state only when the view is actually visible.
        await ReloadAsync();
    }

    // Reused Today tabs must refresh without issuing concurrent repository reads.
    public Task RefreshAfterNavigationAsync() => ReloadAsync();

    private async Task ReloadAsync()
    {
        if (_loading) { _refreshQueued = true; return; }
        _loading = true;
        try
        {
            var date = DateOnly.FromDateTime(DateTime.Today);
            var readModel = await _source.ReadAsync(date);
            var missions = await _missions.ListAsync();
            _missionById = missions.ToDictionary(m => m.Id);
            _groupList = await _groups.ListAsync();
            var sessions = await _tracking.SessionsAsync();
            var now = DateTimeOffset.UtcNow;
            _timeByMission = sessions.GroupBy(s => s.MissionId).ToDictionary(g => g.Key,
                g => TimeSpan.FromTicks(g.Sum(s => _tracking.CreditedAt(s, now).Ticks)));
            if (!IsLoaded) return; // A navigation change may have detached this page during awaits.
            DayProgressLabel.Text = $"{readModel.Missions.Completed}/{readModel.Missions.Total}";
            RemainingLabel.Text = readModel.Missions.Remaining.ToString(Polish);
            UpdateOverdue(readModel.Missions.Overdue);
            UpdateTodo(readModel.Todo, readModel.Upcoming);
            var live = await _tracking.CurrentAsync();
            if (!IsLoaded) return;
            if (live is { } status)
            {
                var session = status.Session;
                var credited = _tracking.CreditedAt(session, DateTimeOffset.UtcNow);
                var state = session.State == TrackSessionState.Paused ? "â…¡ PAUZA" : "â— TRWA POMIAR";
                var time = $"{(long)credited.TotalHours:D2}:{credited.Minutes:D2}:{credited.Seconds:D2}";
                var highlight = new VerticalStackLayout { Spacing = 5 };
                highlight.Children.Add(new Label { Text = state + " Â· " + time,
                    FontSize = 11, FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#78D8B0") });
                highlight.Children.Add(new Label { Text = session.MissionTitle,
                    FontSize = 15, FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#EEF4FA") });
                var panel = new Border { Padding = new Thickness(12, 10),
                    BackgroundColor = Color.FromArgb("#0C2825"),
                    Stroke = Color.FromArgb("#49BD8E"), StrokeThickness = 1,
                    Content = highlight };
                TodoItems.Children.Insert(0, panel);
                TodoHeading.Text = "AKTYWNA MISJA Â· " + TodoHeading.Text;
            }
            UpdateRescheduled(readModel.RescheduledReminders);
            MissionStatusLabel.Text = readModel.Missions.Total == 0
                ? "Brak misji na dzisiaj."
                : readModel.Missions.Remaining == 0
                    ? "Wszystkie dzisiejsze misje wykonane."
                    : $"Zostało {readModel.Missions.Remaining} misji.";
            BioNoticeLabel.Text = readModel.BioNotices.Count == 0
                ? "Brak nowych powiadomień."
                : string.Join(" • ", readModel.BioNotices.Select(n => n.Title));
        }
        catch (Exception ex)
        {
            BioNoticeLabel.Text = "Nie udało się odczytać podsumowania.";
            TodoItems.Children.Clear();
            TodoItems.Children.Add(new Label { Text = "Błąd odczytu: " + ex.Message,
                FontSize = 12, TextColor = Color.FromArgb("#F7A6B6") });
            OverdueLabel.Text = "—";
            OverdueDescription.Text = "Nie udało się odczytać zaległości.";
            OverdueItems.Children.Clear();
            RescheduledItems.Children.Clear();
            RescheduledPanel.IsVisible = false;
            SetOverdueColor(Color.FromArgb("#8799A9"));
        }
        finally
        {
            _loading = false;
            var refreshAgain = _refreshQueued;
            _refreshQueued = false;
            if (refreshAgain && IsLoaded) await ReloadAsync();
        }
    }

    private MissionRecord? ById(string? id) => Guid.TryParse(id, out var key) &&
        _missionById.TryGetValue(key, out var record) ? record : null;

    private View Card(MissionRecord mission)
    {
        var groupName = mission.GroupId is { } groupId && _groupList.Any(g => g.Id == groupId)
            ? GroupHierarchy.PathFor(_groupList, groupId) : mission.GroupName;
        _timeByMission.TryGetValue(mission.Id, out var time);
        return new MissionCardView(mission, groupName, time, _commands, ReloadAsync);
    }

    private void UpdateTodo(IReadOnlyList<TodayTodoItem>? today, IReadOnlyList<TodayTodoItem>? upcoming)
    {
        TodoItems.Children.Clear();
        var current = today?.Count > 0 ? today : upcoming;
        TodoHeading.Text = today?.Count > 0 || current is not { Count: > 0 }
            ? "DO ZROBIENIA" : "NAJBLIŻSZE MISJE";
        var shown = (current ?? Array.Empty<TodayTodoItem>())
            .Select(x => ById(x.MissionId)).Where(x => x is not null).Cast<MissionRecord>()
            .Take(5).ToArray();
        if (shown.Length == 0)
        {
            TodoItems.Children.Add(new Label { Text = "Brak misji do zrobienia.",
                FontSize = 12, TextColor = Color.FromArgb("#8799A9") });
            return;
        }
        foreach (var mission in shown) TodoItems.Children.Add(Card(mission));
        if (current!.Count > 5) TodoItems.Children.Add(new Label
        {
            Text = $"+ {current.Count - 5} więcej w Misjach", FontSize = 11,
            TextColor = Color.FromArgb("#20D8F2")
        });
    }

    private void UpdateRescheduled(IReadOnlyList<TodayReminder>? reminders)
    {
        RescheduledItems.Children.Clear();
        var shown = (reminders ?? Array.Empty<TodayReminder>())
            .Select(r => ById(r.MissionId)).Where(x => x is not null)
            .Cast<MissionRecord>().Take(3).ToArray();
        RescheduledPanel.IsVisible = shown.Length > 0;
        RescheduledCountLabel.Text = shown.Length.ToString(Polish);
        foreach (var mission in shown) RescheduledItems.Children.Add(Card(mission));
    }

    private void UpdateOverdue(int count)
    {
        OverdueLabel.Text = count.ToString(Polish);
        OverdueDescription.Text = count == 0
            ? "Brak zaległych misji." : "Misje po terminie wymagają uwagi.";
        OverdueItems.Children.Clear();
        if (count > 0)
        {
            var overdue = _missionById.Values.Where(m => m.Status == MissionStatus.Open &&
                (m.TimingMode == MissionTimingMode.Day && m.Day < DateOnly.FromDateTime(DateTime.Now) ||
                 m.TimingMode == MissionTimingMode.TimeRange && m.EndsAtLocal < DateTime.Now))
                .Take(5).ToArray();
            foreach (var mission in overdue) OverdueItems.Children.Add(Card(mission));
            if (count > overdue.Length)
                OverdueItems.Children.Add(new Label { Text = $"+ {count - overdue.Length} kolejnych w Misjach",
                    FontSize = 11, TextColor = Color.FromArgb("#F7A6B6") });
        }
        SetOverdueColor(Color.FromArgb(count == 0 ? "#78D8B0" : "#F7A6B6"));
    }

    private void SetOverdueColor(Color color)
    {
        OverduePanel.Stroke = new SolidColorBrush(color);
        OverdueAccent.Color = color;
        OverdueTitle.TextColor = color;
        OverdueLabel.TextColor = color;
        OverdueCaption.TextColor = color;
    }
}
