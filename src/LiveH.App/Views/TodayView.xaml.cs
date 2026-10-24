using System.Globalization;
using LiveH.Contracts.Today;

namespace LiveH.App.Views;

public partial class TodayView : ContentView
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");
    private readonly ITodayReadModelSource _source;

    public TodayView(ITodayReadModelSource source)
    {
        InitializeComponent();
        _source = source;
        DateLabel.Text = DateTime.Today.ToString("dddd, d MMMM yyyy", Polish);
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        try
        {
            var readModel = await _source.ReadAsync(DateOnly.FromDateTime(DateTime.Today));
            if (!IsLoaded) return;
            DayProgressLabel.Text = $"{readModel.Missions.Completed}/{readModel.Missions.Total}";
            RemainingLabel.Text = readModel.Missions.Remaining.ToString(Polish);
            UpdateOverdue(readModel.Missions.Overdue, readModel.OverdueTitles);
            UpdateTodo(readModel.Todo, readModel.Upcoming);
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
        catch (Exception)
        {
            // An unreadable source is NOT proof that the user has no overdue tasks.
            BioNoticeLabel.Text = "Nie udało się odczytać podsumowania.";
            TodoItems.Children.Clear();
            TodoItems.Children.Add(new Label { Text = "Nie udało się odczytać misji.",
                FontSize = 12, TextColor = Color.FromArgb("#8799A9") });
            OverdueLabel.Text = "—";
            OverdueDescription.Text = "Nie udało się odczytać stanu zaległości.";
            OverdueItems.Children.Clear();
            RescheduledItems.Children.Clear();
            RescheduledPanel.IsVisible = false;
            SetOverdueColor(Color.FromArgb("#8799A9"));
        }
    }

    private void UpdateTodo(IReadOnlyList<TodayTodoItem>? missions,
        IReadOnlyList<TodayTodoItem>? upcoming)
    {
        TodoItems.Children.Clear();
        // This preview does not count future missions as today's work.
        var hasToday = missions is { Count: > 0 };
        var shown = hasToday ? missions! : upcoming ?? Array.Empty<TodayTodoItem>();
        TodoHeading.Text = hasToday || shown.Count == 0 ? "DO ZROBIENIA" : "NAJBLIŻSZE MISJE";
        if (shown.Count == 0)
        {
            TodoItems.Children.Add(new Label { Text = "Brak misji do zrobienia.",
                FontSize = 12, TextColor = Color.FromArgb("#8799A9") });
            return;
        }
        foreach (var mission in shown.Take(5))
            TodoItems.Children.Add(MakeMissionCard(mission.Title,
                mission.ScheduleLabel, mission.WasRescheduled));
        if (shown.Count > 5)
            TodoItems.Children.Add(new Label { Text = $"+ {shown.Count - 5} więcej w zakładce Misje",
                FontSize = 11, TextColor = Color.FromArgb("#20D8F2") });
    }

    // Today renders a small projection only; it never modifies MissionOS data.
    private static Border MakeMissionCard(string title, string subtitle, bool warning,
        string warningLabel = "ODROCZONA")
    {
        var accent = Color.FromArgb(warning ? "#F7A6B6" : "#243F50");
        var stack = new VerticalStackLayout { Spacing = 4 };
        stack.Children.Add(new Label
        {
            Text = title, FontSize = 13, FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb(warning ? "#F7A6B6" : "#EEF4FA"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        stack.Children.Add(new Label
        {
            Text = warning ? warningLabel + " // " + subtitle : subtitle,
            FontSize = 11, TextColor = warning ? accent : Color.FromArgb("#8799A9"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        return new Border
        {
            Stroke = new SolidColorBrush(accent),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.Rectangle(),
            BackgroundColor = Color.FromArgb("#0D1C26"),
            Padding = new Thickness(10, 9),
            Content = stack
        };
    }

    private void UpdateRescheduled(IReadOnlyList<TodayReminder>? reminders)
    {
        RescheduledItems.Children.Clear();
        var shown = reminders?.Take(3).ToArray() ?? Array.Empty<TodayReminder>();
        RescheduledPanel.IsVisible = shown.Length > 0;
        RescheduledCountLabel.Text = shown.Length.ToString(Polish);
        foreach (var reminder in shown)
        {
            var last = reminder.LastRescheduledAtUtc.ToLocalTime().Date;
            var daysAgo = (DateTime.Today - last).Days;
            var changeLabel = daysAgo switch
            {
                0 => "Odroczono dzisiaj",
                1 => "Odroczono wczoraj",
                >= 2 and <= 7 => $"Odroczono {daysAgo} dni temu",
                _ => "Odroczono wcześniej"
            };
            var detail = $"{reminder.ScheduleLabel} · {reminder.PriorityLabel} · " +
                $"{changeLabel} · przesunięć: {reminder.RescheduleCount}";
            RescheduledItems.Children.Add(MakeMissionCard(reminder.Title,
                detail, warning: true, warningLabel: "ODROCZONA"));
        }
    }

    private void UpdateOverdue(int count, IReadOnlyList<string>? titles)
    {
        // Green when zero, warning rose for one or more overdue tasks.
        OverdueLabel.Text = count.ToString(Polish);
        OverdueDescription.Text = count == 0
            ? "Brak zaległych misji."
            : "Misje po terminie wymagają uwagi.";
        OverdueItems.Children.Clear();
        if (count > 0 && titles is { Count: > 0 })
        {
            foreach (var title in titles)
                OverdueItems.Children.Add(MakeMissionCard(title,
                    "Przekroczony termin", warning: true, warningLabel: "ZALEGŁA"));
            if (count > titles.Count)
                OverdueItems.Children.Add(new Label
                {
                    Text = $"+ {count - titles.Count} kolejnych w Misjach",
                    FontSize = 11, TextColor = Color.FromArgb("#F7A6B6")
                });
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
