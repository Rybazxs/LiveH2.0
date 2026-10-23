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
            OverdueLabel.Text = readModel.Missions.Overdue.ToString(Polish);
            MissionStatusLabel.Text = readModel.Missions.Total == 0
                ? "Brak misji na dzisiaj."
                : $"Zostało {readModel.Missions.Remaining} misji.";
            BioNoticeLabel.Text = readModel.BioNotices.Count == 0
                ? "Brak nowych powiadomień."
                : string.Join(" • ", readModel.BioNotices.Select(n => n.Title));
        }
        catch (Exception)
        {
            // Keep the read-only dashboard usable if a future module provider fails.
            BioNoticeLabel.Text = "Nie udało się odczytać podsumowania.";
        }
    }
}
