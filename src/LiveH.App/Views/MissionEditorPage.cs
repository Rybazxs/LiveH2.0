using System.Globalization;
using LiveH.Contracts.Missions;
using LiveH.Contracts.Groups;

namespace LiveH.App.Views;

// One editor for the MissionOS, GroupsOS and Today surfaces. The host owns navigation;
// this view uses only public contracts. It never deletes/recreates a mission on edit.
public sealed class MissionEditorPage : ContentPage
{
    private readonly IMissionRepository _missions;
    private readonly MissionRecord _original;
    private readonly GroupRecord[] _groups;
    private readonly TaskCompletionSource<bool> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Entry _title;
    private readonly Editor _notes;
    private readonly Entry _estimate;
    private readonly Picker _timing;
    private readonly DatePicker _day;
    private readonly DatePicker _startDate;
    private readonly DatePicker _endDate;
    private readonly TimePicker _startTime;
    private readonly TimePicker _endTime;
    private readonly Picker _priority;
    private readonly Picker _group;
    private readonly VerticalStackLayout _dayFields;
    private readonly VerticalStackLayout _rangeFields;
    private readonly Label _error;
    private bool _saving;

    private static readonly Color Cyan = Color.FromArgb("#20D8F2");
    private static readonly Color Text = Color.FromArgb("#EEF4FA");
    private static readonly Color Muted = Color.FromArgb("#8799A9");

    public Task<bool> Completion => _completion.Task;

    public MissionEditorPage(MissionRecord mission, IMissionRepository missions,
        IReadOnlyList<GroupRecord> groups)
    {
        _original = mission;
        _missions = missions;
        _groups = groups.Where(g => !g.IsArchived || g.Id == mission.GroupId)
            .OrderBy(g => GroupHierarchy.PathFor(groups, g.Id)).ToArray();
        BackgroundColor = Color.FromArgb("#05090E");
        Title = "EDYTUJ MISJĘ";

        _title = new Entry { Text = mission.Title, MaxLength = 160, TextColor = Text, FontSize = 15 };
        _notes = new Editor { Text = mission.Notes, MaxLength = 4000, TextColor = Text,
            AutoSize = EditorAutoSizeOption.TextChanges, HeightRequest = 80 };
        _estimate = new Entry { Text = mission.EstimatedMinutes?.ToString(CultureInfo.InvariantCulture),
            Keyboard = Keyboard.Numeric, Placeholder = "Minuty (opcjonalnie)", TextColor = Text };
        _timing = new Picker { Title = "Termin", TextColor = Text, TitleColor = Muted };
        foreach (var label in new[] { "Bez terminu / na cito", "Konkretny dzień", "Godziny od–do" })
            _timing.Items.Add(label);
        _day = new DatePicker { Date = mission.Day?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today,
            Format = "dd.MM.yyyy", TextColor = Text };
        _startDate = new DatePicker { Date = mission.StartsAtLocal?.Date ?? DateTime.Today,
            Format = "dd.MM.yyyy", TextColor = Text };
        _endDate = new DatePicker { Date = mission.EndsAtLocal?.Date ?? DateTime.Today,
            Format = "dd.MM.yyyy", TextColor = Text };
        _startTime = new TimePicker { Time = mission.StartsAtLocal?.TimeOfDay ?? new TimeSpan(9, 0, 0),
            Format = "HH:mm", TextColor = Text };
        _endTime = new TimePicker { Time = mission.EndsAtLocal?.TimeOfDay ?? new TimeSpan(10, 0, 0),
            Format = "HH:mm", TextColor = Text };
        _priority = new Picker { Title = "Priorytet", TextColor = Text, TitleColor = Muted };
        foreach (var label in new[] { "Normalny", "Niski", "Ważny", "Krytyczny" })
            _priority.Items.Add(label);
        _priority.SelectedIndex = (int)mission.Priority;
        _group = new Picker { Title = "Grupa", TextColor = Text, TitleColor = Muted };
        _group.Items.Add("BEZ GRUPY");
        foreach (var group in _groups) _group.Items.Add(GroupHierarchy.PathFor(groups, group.Id));
        _group.SelectedIndex = Array.FindIndex(_groups, x => x.Id == mission.GroupId) + 1;
        _dayFields = new VerticalStackLayout { Spacing = 5,
            Children = { Caption("DZIEŃ"), _day } };
        _rangeFields = new VerticalStackLayout { Spacing = 5,
            Children = { Caption("OD"), _startDate, _startTime,
                Caption("DO"), _endDate, _endTime } };
        _timing.SelectedIndexChanged += (_, _) => RefreshFields();
        _timing.SelectedIndex = (int)mission.TimingMode;
        RefreshFields();
        _error = new Label { TextColor = Color.FromArgb("#F7A6B6"), FontSize = 12 };

        var save = Command("ZAPISZ ZMIANY", Cyan);
        save.Clicked += async (_, _) => await SaveAsync();
        var cancel = Command("ANULUJ", Muted);
        cancel.Clicked += async (_, _) => await CloseAsync(false);
        var content = new VerticalStackLayout
        {
            Padding = new Thickness(22, 28, 22, 38), Spacing = 12,
            Children = {
                new Label { Text = "MISSION OS // EDYCJA", FontSize = 21,
                    FontAttributes = FontAttributes.Bold, TextColor = Cyan },
                Caption("NAZWA MISJI"), _title, Caption("OPIS"), _notes,
                Caption("TERMIN"), _timing, _dayFields, _rangeFields,
                Caption("PRIORYTET"), _priority, Caption("PLANOWANY CZAS"), _estimate,
                Caption("GRUPA"), _group, _error, save, cancel
            }
        };
        Content = new ScrollView { Content = content };
    }

    private void RefreshFields()
    {
        _dayFields.IsVisible = _timing.SelectedIndex == (int)MissionTimingMode.Day;
        _rangeFields.IsVisible = _timing.SelectedIndex == (int)MissionTimingMode.TimeRange;
    }

    private async Task SaveAsync()
    {
        if (_saving) return;
        _saving = true;
        try
        {
            int? estimate = null;
            if (!string.IsNullOrWhiteSpace(_estimate.Text))
            {
                if (!int.TryParse(_estimate.Text, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out var parsed) || parsed < 1 || parsed > 10080)
                    throw new ArgumentException("Planowany czas: od 1 do 10080 minut.");
                estimate = parsed;
            }
            var timing = (MissionTimingMode)Math.Max(0, _timing.SelectedIndex);
            DateOnly? day = timing == MissionTimingMode.Day
                ? DateOnly.FromDateTime(_day.Date ?? DateTime.Today) : null;
            DateTime? start = timing == MissionTimingMode.TimeRange
                ? (_startDate.Date ?? DateTime.Today).Date + (_startTime.Time ?? TimeSpan.Zero) : null;
            DateTime? end = timing == MissionTimingMode.TimeRange
                ? (_endDate.Date ?? DateTime.Today).Date + (_endTime.Time ?? TimeSpan.Zero) : null;
            GroupRecord? group = _group.SelectedIndex > 0 && _group.SelectedIndex <= _groups.Length
                ? _groups[_group.SelectedIndex - 1] : null;
            var draft = new MissionDraft(_title.Text ?? "", _notes.Text, timing,
                day, start, end, estimate, _original.CategoryId, _original.TimeZoneId,
                // Do not silently erase a PATCH-008 free-text group label during edit.
                group?.Name ?? (_original.GroupId is null ? _original.GroupName : null),
                (MissionPriority)Math.Max(0, _priority.SelectedIndex), group?.Id);
            await _missions.UpdateAsync(_original.Id, draft);
            await CloseAsync(true);
        }
        catch (Exception ex) { _error.Text = ex.Message; }
        finally { _saving = false; }
    }

    private async Task CloseAsync(bool saved)
    {
        _completion.TrySetResult(saved);
        await Navigation.PopModalAsync();
    }

    protected override void OnDisappearing()
    {
        _completion.TrySetResult(false);
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        _completion.TrySetResult(false);
        return base.OnBackButtonPressed();
    }

    private static Label Caption(string text) => new() { Text = text, FontSize = 11, TextColor = Muted };
    private static Button Command(string label, Color foreground) => new()
    {
        Text = label, TextColor = foreground, FontSize = 12, HeightRequest = 45,
        BackgroundColor = Color.FromArgb("#11232E")
    };
}
