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
            UpdateOverdue(readModel.Missions.Overdue);
            MissionStatusLabel.Text = readModel.Missions.Total == 0
                ? "Brak misji na dzisiaj."
                : $"Zostało {readModel.Missions.Remaining} misji.";
            BioNoticeLabel.Text = readModel.BioNotices.Count == 0
                ? "Brak nowych powiadomień."
                : string.Join(" • ", readModel.BioNotices.Select(n => n.Title));
        }
        catch (Exception)
        {
            // An unreadable source is NOT proof that the user has no overdue tasks.
            BioNoticeLabel.Text = "Nie udało się odczytać podsumowania.";
            OverdueLabel.Text = "—";
            OverdueDescription.Text = "Nie udało się odczytać stanu zaległości.";
            SetOverdueColor(Color.FromArgb("#8799A9"));
        }
    }

    private void UpdateOverdue(int count)
    {
        // Green when zero, warning rose for one or more overdue tasks.
        OverdueLabel.Text = count.ToString(Polish);
        OverdueDescription.Text = count == 0
            ? "Brak zaległych misji."
            : "Szczegóły zaległości pojawią się po podłączeniu MissionOS.";
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
