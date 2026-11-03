using LiveH.Contracts.Missions;

namespace LiveH.App.Views;

// Single MissionOS presentation component: reusable in Missions, Groups and Today.
// It owns only expansion/UI state; all writes/navigation go through the app coordinator.
public sealed class MissionCardView : ContentView
{
    private readonly MissionRecord _mission;
    private readonly MissionCardCommands _commands;
    private readonly Func<Task> _reload;
    private readonly VerticalStackLayout _details;
    private readonly Label _chevron;
    private readonly Grid _actions;
    private readonly Grid _moreActions;
    private Button? _moreButton;
    private readonly bool _completed;
    private bool _actionsCreated;
    private bool _moreExpanded;
    private bool _expanded;
    private bool _busy;
    private bool _animating;

    private static readonly Color Cyan = Color.FromArgb("#20D8F2");
    private static readonly Color Text = Color.FromArgb("#EEF4FA");
    private static readonly Color Muted = Color.FromArgb("#8799A9");
    private static readonly Color Rose = Color.FromArgb("#F7A6B6");
    private static readonly Color Green = Color.FromArgb("#78D8B0");

    public bool IsExpanded => _expanded;

    public MissionCardView(MissionRecord mission, string? groupPath, TimeSpan tracked,
        MissionCardCommands commands, Func<Task> reload)
    {
        _mission = mission;
        _commands = commands;
        _reload = reload;
        var completed = mission.Status == MissionStatus.Completed;
        _completed = completed;
        var overdue = !completed && IsOverdue(mission, DateTime.Now);
        var rescheduled = mission.ScheduleHistory is { Count: > 0 };
        var warning = !completed && (overdue || rescheduled);
        var titleColor = completed ? Muted : warning ? Rose : Text;
        var border = completed ? Color.FromArgb("#22313B") : warning ? Rose : Color.FromArgb("#29495A");

        var summary = new VerticalStackLayout { Spacing = 4 };
        summary.Children.Add(new Label { Text = mission.Title, FontSize = 14,
            TextColor = titleColor, FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap });
        var status = completed ? "WYKONANA · " : overdue ? "ZALEGŁA · "
            : rescheduled ? "ODROCZONA · " : "";
        summary.Children.Add(new Label { Text = status + ScheduleLabel(mission),
            FontSize = 11, TextColor = warning ? Rose : Muted });
        if (!string.IsNullOrWhiteSpace(groupPath))
            summary.Children.Add(new Label { Text = groupPath, FontSize = 10,
                TextColor = Cyan, LineBreakMode = LineBreakMode.WordWrap });
        if (tracked > TimeSpan.Zero)
            summary.Children.Add(new Label { Text = "TRACK  " + TrackView.Duration(tracked),
                FontSize = 11, TextColor = Cyan });
        _chevron = new Label { Text = "⌄", TextColor = Muted, FontSize = 19,
            VerticalOptions = LayoutOptions.Center };
        var summaryTap = new TapGestureRecognizer();
        summaryTap.Tapped += async (_, _) => await ToggleDetailsAsync();
        var summaryZone = new Grid { ColumnSpacing = 8 };
        summaryZone.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        summaryZone.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        summaryZone.Children.Add(summary);
        Grid.SetColumn(_chevron, 1);
        summaryZone.Children.Add(_chevron);
        summaryZone.GestureRecognizers.Add(summaryTap);

        var header = new Grid { ColumnSpacing = 10 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(summaryZone);
        // Playback is always on the right, independent of expanded detail state.
        {
            var play = new Button { Text = "▶", TextColor = Green, FontSize = 19,
                BackgroundColor = Color.FromArgb("#14392F"), WidthRequest = 49,
                HeightRequest = 51, Padding = new Thickness(0),
                VerticalOptions = LayoutOptions.Center };
            play.Clicked += async (_, _) => await RunAsync(MissionCardCommand.Track);
            Grid.SetColumn(play, 1);
            header.Children.Add(play);
        }

        _details = new VerticalStackLayout { IsVisible = false, Opacity = 0, Spacing = 6 };
        var line = new BoxView { HeightRequest = 1, Color = Color.FromArgb("#2C4A5B") };
        _details.Children.Add(line);
        if (!string.IsNullOrWhiteSpace(mission.Notes))
            _details.Children.Add(new Label { Text = mission.Notes, FontSize = 12,
                TextColor = Text, LineBreakMode = LineBreakMode.WordWrap });
        _details.Children.Add(new Label
        {
            Text = $"PRIORYTET: {PriorityLabel(mission.Priority)}" +
                (mission.EstimatedMinutes is { } minutes ? $" · PLAN: {minutes} min" : "") +
                (rescheduled ? $" · ODROCZEŃ: {mission.ScheduleHistory!.Count}" : ""),
            FontSize = 11, TextColor = warning ? Rose : Muted
        });
        // Actions are created on first expansion; only three primary actions
        // plus MORE stay visible. Secondary actions never clutter the detail panel.
        _actions = new Grid { ColumnSpacing = 4 };
        for (var i = 0; i < 4; i++)
            _actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        _moreActions = new Grid { ColumnSpacing = 4, IsVisible = false };
        _moreActions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        _moreActions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        _details.Children.Add(_actions);
        _details.Children.Add(_moreActions);

        var body = new VerticalStackLayout { Spacing = 8, Children = { header, _details } };
        Content = new Border
        {
            Stroke = new SolidColorBrush(border), StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.Rectangle(),
            BackgroundColor = completed ? Color.FromArgb("#080E14") : Color.FromArgb("#0D1C26"),
            Padding = new Thickness(10, 9), Content = body
        };
    }

    private void EnsureActionsCreated()
    {
        if (_actionsCreated) return;
        _actionsCreated = true;
        AddAction(_actions, 0, "EDYTUJ", MissionCardCommand.Edit, Cyan);
        if (!_completed)
        {
            AddAction(_actions, 1, "ODROCZ", MissionCardCommand.Postpone, Cyan);
            AddAction(_actions, 2, "WYKONAJ", MissionCardCommand.Complete, Green);
        }
        var more = ActionButton("WIĘCEJ", Cyan);
        _moreButton = more;
        more.Clicked += (_, _) =>
        {
            _moreExpanded = !_moreExpanded;
            _moreActions.IsVisible = _moreExpanded;
            more.TextColor = _moreExpanded ? Text : Cyan;
        };
        Grid.SetColumn(more, 3);
        _actions.Children.Add(more);
        AddAction(_moreActions, 0, "GRUPA", MissionCardCommand.Assign, Cyan);
        AddAction(_moreActions, 1, "USUŃ", MissionCardCommand.Delete, Rose);
    }

    private async Task ToggleDetailsAsync()
    {
        if (_animating) return;
        _animating = true;
        try
        {
            _expanded = !_expanded;
            _chevron.Text = _expanded ? "⌃" : "⌄";
            if (_expanded)
            {
                EnsureActionsCreated();
                _details.Opacity = 0;
                _details.TranslationY = -9;
                _details.IsVisible = true;
                await Task.WhenAll(
                    _details.FadeToAsync(1, 170, Easing.CubicOut),
                    _details.TranslateToAsync(0, 0, 170, Easing.CubicOut));
            }
            else
            {
                await Task.WhenAll(
                    _details.FadeToAsync(0, 120, Easing.CubicIn),
                    _details.TranslateToAsync(0, -9, 120, Easing.CubicIn));
                _details.IsVisible = false;
                _moreExpanded = false;
                _moreActions.IsVisible = false;
                if (_moreButton is not null) _moreButton.TextColor = Cyan;
            }
        }
        catch (Exception ex)
        {
            // A card can detach while its expand animation is awaiting. Avoid
            // an unhandled async-void event exception killing the Android process.
            System.Diagnostics.Debug.WriteLine($"[LiveH MissionCard] animation: {ex}");

            _details.Opacity = _expanded ? 1 : 0;
            _details.IsVisible = _expanded;
            _details.TranslationY = 0;
        }
        finally { _animating = false; }
    }

    private void AddAction(Grid host, int column, string label, MissionCardCommand command, Color color)
    {
        var button = ActionButton(label, color);
        button.Clicked += async (_, _) => await RunAsync(command);
        Grid.SetColumn(button, column);
        host.Children.Add(button);
    }

    private static Button ActionButton(string label, Color color) => new()
    {
        Text = label, TextColor = color, FontSize = 10,
        MinimumHeightRequest = 0, MinimumWidthRequest = 0, HeightRequest = 34,
        HorizontalOptions = LayoutOptions.Fill,
        BackgroundColor = Color.FromArgb("#10202B"),
        CornerRadius = 4, Padding = new Thickness(2, 0)
    };

    private async Task RunAsync(MissionCardCommand action)
    {
        if (_busy) return;
        _busy = true;
        try { await _commands.ExecuteAsync(_mission, action, _reload); }
        finally { _busy = false; }
    }

    private static string PriorityLabel(MissionPriority priority) => priority switch
    {
        MissionPriority.Low => "NISKI", MissionPriority.High => "WAŻNY",
        MissionPriority.Critical => "KRYTYCZNY", _ => "NORMALNY"
    };

    private static bool IsOverdue(MissionRecord mission, DateTime now) => mission.TimingMode switch
    {
        MissionTimingMode.Day => mission.Day < DateOnly.FromDateTime(now),
        MissionTimingMode.TimeRange => mission.EndsAtLocal < now,
        _ => false
    };

    private static string ScheduleLabel(MissionRecord mission) => mission.TimingMode switch
    {
        MissionTimingMode.Urgent => "NA CITO",
        MissionTimingMode.Day => $"{mission.Day:dd.MM.yyyy} · CAŁY DZIEŃ",
        MissionTimingMode.TimeRange => $"{mission.StartsAtLocal:dd.MM HH:mm}–{mission.EndsAtLocal:HH:mm}",
        _ => "BEZ TERMINU"
    };
}
