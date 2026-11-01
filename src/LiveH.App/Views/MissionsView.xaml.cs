using System.Globalization;
using LiveH.Contracts.Missions;
using LiveH.Modules.Missions;

namespace LiveH.App.Views;

public partial class MissionsView : ContentView
{
    private readonly IMissionRepository _repository;
    private bool _busy;
    private VerticalStackLayout? _activeReschedulePanel;
    private readonly Color _text = Color.FromArgb("#EEF4FA");
    private readonly Color _muted = Color.FromArgb("#8799A9");
    private readonly Color _cyan = Color.FromArgb("#20D8F2");
    private readonly Color _panel = Color.FromArgb("#0D1C26");
    private readonly Color _border = Color.FromArgb("#243F50");
    private readonly Color _green = Color.FromArgb("#78D8B0");
    private readonly Color _rose = Color.FromArgb("#F7A6B6");

    public bool IsEditing => FormPanel.IsVisible || _activeReschedulePanel?.IsVisible == true;

    public MissionsView(IMissionRepository repository)
    {
        InitializeComponent();
        _repository = repository;
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

    private void OnToggleForm(object? sender, EventArgs e)
    {
        FormPanel.IsVisible = !FormPanel.IsVisible;
        AddButton.Text = FormPanel.IsVisible ? "ZAMKNIJ" : "+ DODAJ MISJĘ";
        if (_activeReschedulePanel is not null)
            _activeReschedulePanel.IsVisible = false;
        _activeReschedulePanel = null;
        HideError();
    }

    private void OnCancel(object? sender, EventArgs e)
    {
        FormPanel.IsVisible = false;
        AddButton.Text = "+ DODAJ MISJĘ";
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
            var draft = new MissionDraft(
                TitleEntry.Text ?? "", NotesEditor.Text, mode, day, starts, ends,
                estimate, CategoryId: null, TimeZoneId: TimeZoneInfo.Local.Id,
                GroupName: string.IsNullOrWhiteSpace(GroupEntry.Text) ? null : GroupEntry.Text.Trim(),
                Priority: priority);
            MissionRules.Validate(draft);
            _busy = true;
            SaveButton.IsEnabled = false;
            await _repository.CreateAsync(draft);
            TitleEntry.Text = "";
            NotesEditor.Text = "";
            EstimateEntry.Text = "";
            GroupEntry.Text = "";
            PriorityPicker.SelectedIndex = 0;
            FormPanel.IsVisible = false;
            AddButton.Text = "+ DODAJ MISJĘ";
            await LoadMissionsAsync();
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

    private async Task LoadMissionsAsync()
    {
        LoadError.IsVisible = false;
        try
        {
            var list = await _repository.ListAsync();
            MissionItems.Children.Clear();
            CompletedItems.Children.Clear();
            _activeReschedulePanel = null;
            var active = list.Where(x => x.Status == MissionStatus.Open)
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
            var completed = list.Where(x => x.Status == MissionStatus.Completed)
                .OrderByDescending(x => x.CompletedAtUtc).ToArray();
            CountLabel.Text = active.Length.ToString(CultureInfo.GetCultureInfo("pl-PL"));
            CompletedCountLabel.Text = completed.Length.ToString(CultureInfo.GetCultureInfo("pl-PL"));
            CompletedPanel.IsVisible = completed.Length > 0;

            if (active.Length == 0)
                MissionItems.Children.Add(new Label
                {
                    Text = list.Count == 0 ? "Brak misji. Dodaj pierwszą powyżej."
                        : "Brak aktywnych misji.",
                    FontSize = 12, TextColor = _muted
                });
            else
                foreach (var mission in active)
                    MissionItems.Children.Add(MakeRow(mission));

            foreach (var mission in completed)
                CompletedItems.Children.Add(MakeRow(mission));
        }
        catch (Exception ex)
        {
            MissionItems.Children.Clear();
            CompletedItems.Children.Clear();
            CompletedPanel.IsVisible = false;
            _activeReschedulePanel = null;
            CountLabel.Text = "—";
            LoadError.Text = $"Błąd odczytu lokalnych danych: {ex.Message}";
            LoadError.IsVisible = true;
        }
    }

    private View MakeRow(MissionRecord mission)
    {
        var completed = mission.Status == MissionStatus.Completed;
        var overdue = MissionRules.IsOverdue(mission, DateTime.Now);
        var rescheduled = mission.ScheduleHistory is { Count: > 0 };
        var warning = !completed && (overdue || rescheduled);
        var labelColor = completed ? _muted : warning ? _rose : _text;
        var content = new VerticalStackLayout { Spacing = 4 };
        content.Children.Add(new Label { Text = mission.Title, FontSize = 14,
            FontAttributes = FontAttributes.Bold, TextColor = labelColor,
            LineBreakMode = LineBreakMode.WordWrap });
        content.Children.Add(new Label { Text = (completed ? "✓ WYKONANA · "
                : overdue ? "ZALEGŁA · " : rescheduled ? "ODROCZONA · " : "")
            + MissionRules.TimingLabel(mission), FontSize = 11,
            TextColor = completed ? _muted : warning ? _rose : _muted });
        if (mission.Priority != MissionPriority.Normal)
        {
            var (priorityName, color) = mission.Priority switch
            {
                MissionPriority.Low => ("PRIORYTET: NISKI", _muted),
                MissionPriority.High => ("PRIORYTET: WAŻNY", Color.FromArgb("#F2C47B")),
                MissionPriority.Critical => ("PRIORYTET: KRYTYCZNY", _rose),
                _ => ("PRIORYTET: NORMALNY", _muted)
            };
            content.Children.Add(new Label { Text = priorityName,
                FontSize = 10, FontAttributes = FontAttributes.Bold,
                TextColor = completed ? _muted : color });
        }
        if (rescheduled && !completed)
            content.Children.Add(new Label { Text = $"LICZBA ODROCZEŃ: {mission.ScheduleHistory!.Count}",
                FontSize = 10, TextColor = _rose });
        if (mission.EstimatedMinutes is { } estimated)
            content.Children.Add(new Label { Text = $"PLAN: {estimated} min", FontSize = 10,
                TextColor = _muted });
        if (!string.IsNullOrWhiteSpace(mission.GroupName))
            content.Children.Add(new Label { Text = $"GRUPA: {mission.GroupName}",
                FontSize = 10, TextColor = _cyan });
        if (!string.IsNullOrWhiteSpace(mission.Notes))
            content.Children.Add(new Label { Text = mission.Notes, FontSize = 12,
                TextColor = _muted, LineBreakMode = LineBreakMode.WordWrap });
        var buttons = new HorizontalStackLayout { Spacing = 9 };
        VerticalStackLayout? reschedulePanel = null;
        if (!completed)
        {
            var complete = NewActionButton("✓ WYKONAJ", _green);
            complete.Clicked += async (_, _) => await ApplyActionAsync(
                () => _repository.CompleteAsync(mission.Id));
            buttons.Children.Add(complete);

            reschedulePanel = CreateReschedulePanel(mission);
            var postpone = NewActionButton("ODROCZ", _cyan);
            var localPanel = reschedulePanel;
            postpone.Clicked += (_, _) =>
            {
                if (FormPanel.IsVisible) return;
                if (_activeReschedulePanel is not null && _activeReschedulePanel != localPanel)
                    _activeReschedulePanel.IsVisible = false;
                localPanel.IsVisible = !localPanel.IsVisible;
                _activeReschedulePanel = localPanel.IsVisible ? localPanel : null;
            };
            buttons.Children.Add(postpone);
        }
        var delete = NewActionButton("USUŃ", _muted);
        delete.Clicked += async (_, _) =>
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page is null) return;
            if (await page.DisplayAlert("Usunąć misję?", mission.Title,
                "USUŃ", "ANULUJ"))
                await ApplyActionAsync(() => _repository.DeleteAsync(mission.Id));
        };
        buttons.Children.Add(delete);
        content.Children.Add(buttons);
        if (reschedulePanel is not null) content.Children.Add(reschedulePanel);

        return new Border
        {
            Stroke = new SolidColorBrush(completed ? Color.FromArgb("#22313B")
                : warning ? _rose : _border),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.Rectangle(),
            BackgroundColor = completed ? Color.FromArgb("#080E14") : _panel,
            Padding = completed ? new Thickness(10, 8) : new Thickness(11, 10),
            Content = content
        };
    }

    private VerticalStackLayout CreateReschedulePanel(MissionRecord mission)
    {
        var panel = new VerticalStackLayout
        {
            IsVisible = false,
            Spacing = 8,
            Padding = new Thickness(9, 11),
            BackgroundColor = Color.FromArgb("#081019")
        };
        panel.Children.Add(new Label
        {
            Text = "PRZENIEŚ MISJĘ",
            TextColor = _cyan, FontSize = 11, FontAttributes = FontAttributes.Bold
        });
        if (mission.TimingMode == MissionTimingMode.TimeRange)
            panel.Children.Add(new Label
            {
                Text = "Godziny od–do i długość misji zostaną zachowane.",
                TextColor = _muted, FontSize = 11
            });

        var tomorrow = NewActionButton("JUTRO", _cyan);
        tomorrow.Clicked += async (_, _) => await ApplyActionAsync(
            () => _repository.RescheduleAsync(mission.Id,
                DateOnly.FromDateTime(DateTime.Today.AddDays(1))));
        panel.Children.Add(tomorrow);
        panel.Children.Add(new Label
        {
            Text = "LUB WYBIERZ DZIEŃ", FontSize = 10, TextColor = _muted
        });
        var datePicker = new DatePicker
        {
            Date = DateTime.Today.AddDays(1),
            MinimumDate = DateTime.Today,
            Format = "dd.MM.yyyy",
            TextColor = _text
        };
        panel.Children.Add(datePicker);
        var dateAction = NewActionButton("PRZENIEŚ NA WYBRANY DZIEŃ", _cyan);
        dateAction.Clicked += async (_, _) => await ApplyActionAsync(
            () => _repository.RescheduleAsync(mission.Id,
                DateOnly.FromDateTime(datePicker.Date ?? DateTime.Today)));
        panel.Children.Add(dateAction);
        var cancel = NewActionButton("ANULUJ", _muted);
        cancel.Clicked += (_, _) =>
        {
            panel.IsVisible = false;
            if (_activeReschedulePanel == panel) _activeReschedulePanel = null;
        };
        panel.Children.Add(cancel);
        return panel;
    }

    private Button NewActionButton(string text, Color color) => new()
    {
        Text = text, FontSize = 11, TextColor = color,
        BackgroundColor = Color.FromArgb("#081019"),
        Padding = new Thickness(12, 7),
        HeightRequest = 37
    };

    private async Task ApplyActionAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        try { await action(); await LoadMissionsAsync(); }
        catch (Exception ex)
        {
            LoadError.Text = $"Nie udało się zmienić misji: {ex.Message}";
            LoadError.IsVisible = true;
        }
        finally { _busy = false; }
    }
}
