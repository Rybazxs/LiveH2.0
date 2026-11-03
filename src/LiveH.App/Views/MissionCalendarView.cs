using System.Globalization;
using LiveH.Contracts.Missions;
using LiveH.Modules.Missions;

namespace LiveH.App.Views;

// MissionOS-only read view: no new storage, no writes, no GroupsOS/TrackOS logic.
// Day cells are created once and rebound as the selected month/day changes.
public sealed class MissionCalendarView : ContentView
{
    public enum ViewMode { All, Day, Range, Month }

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");
    private static readonly Color Cyan = Color.FromArgb("#20D8F2");
    private static readonly Color Muted = Color.FromArgb("#8799A9");
    private static readonly Color Text = Color.FromArgb("#EEF4FA");
    private static readonly Color Panel = Color.FromArgb("#0D1C26");
    private static readonly Color BorderColor = Color.FromArgb("#29495A");

    private readonly Button[] _modes = new Button[4];
    private readonly DatePicker _dayPicker;
    private readonly DatePicker _fromPicker;
    private readonly DatePicker _toPicker;
    private readonly Grid _monthCells;
    private readonly List<DayCell> _dayCells = new(42);
    private bool _monthGridReady;

    private sealed class DayCell
    {
        public required Border Border { get; init; }
        public required Label Number { get; init; }
        public required Label Count { get; init; }
        public DateOnly? Date { get; set; }
    }
    private readonly Label _monthTitle;
    private readonly VerticalStackLayout _daySection;
    private readonly VerticalStackLayout _rangeSection;
    private readonly VerticalStackLayout _monthSection;
    private readonly Label _filterHint;
    private IReadOnlyList<MissionRecord> _missions = Array.Empty<MissionRecord>();
    private DateOnly _selectedDay = DateOnly.FromDateTime(DateTime.Today);
    private DateOnly _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private bool _updatingDates;

    public ViewMode Mode { get; private set; } = ViewMode.All;
    public DateOnly SelectedDay => _selectedDay;
    public DateOnly RangeFrom => DateOnly.FromDateTime(_fromPicker.Date ?? DateTime.Today);
    public DateOnly RangeTo => DateOnly.FromDateTime(_toPicker.Date ?? DateTime.Today);
    public event Action? FilterChanged;

    public MissionCalendarView()
    {
        var root = new VerticalStackLayout { Spacing = 6 };
        var modeRow = new Grid { ColumnSpacing = 4 };
        for (var i = 0; i < 4; i++)
        {
            modeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            var mode = (ViewMode)i;
            var button = new Button
            {
                Text = mode switch
                {
                    ViewMode.All => "WSZYSTKIE", ViewMode.Day => "DZIEŃ",
                    ViewMode.Range => "ZAKRES", _ => "MIESIĄC"
                },
                FontSize = 10, HeightRequest = 35, MinimumWidthRequest = 0,
                Padding = new Thickness(2, 0), HorizontalOptions = LayoutOptions.Fill,
                CornerRadius = 4, BackgroundColor = Panel, TextColor = Muted
            };
            button.Clicked += (_, _) => SelectMode(mode);
            _modes[i] = button;
            Grid.SetColumn(button, i);
            modeRow.Children.Add(button);
        }
        root.Children.Add(modeRow);

        _dayPicker = DateInput(DateTime.Today);
        _dayPicker.DateSelected += (_, _) =>
        {
            if (_updatingDates) return;
            _selectedDay = DateOnly.FromDateTime(_dayPicker.Date ?? DateTime.Today);
            FilterChanged?.Invoke();
        };
        _daySection = new VerticalStackLayout { IsVisible = false, Spacing = 3 };
        _daySection.Children.Add(_dayPicker);
        root.Children.Add(_daySection);

        _fromPicker = DateInput(DateTime.Today);
        _toPicker = DateInput(DateTime.Today.AddDays(6));
        _fromPicker.DateSelected += (_, _) => NormalizeRange(changedStart: true);
        _toPicker.DateSelected += (_, _) => NormalizeRange(changedStart: false);
        var rangeRow = new Grid { ColumnSpacing = 6 };
        rangeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        rangeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        var from = DateColumn("OD", _fromPicker);
        var to = DateColumn("DO", _toPicker);
        rangeRow.Children.Add(from);
        Grid.SetColumn(to, 1);
        rangeRow.Children.Add(to);
        _rangeSection = new VerticalStackLayout { IsVisible = false, Children = { rangeRow } };
        root.Children.Add(_rangeSection);

        _monthTitle = new Label
        {
            TextColor = Cyan, FontSize = 12, FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.Center
        };
        var previous = MonthArrow("‹", -1);
        var next = MonthArrow("›", 1);
        var navigation = new Grid { ColumnDefinitions =
        {
            new ColumnDefinition { Width = GridLength.Auto },
            new ColumnDefinition { Width = GridLength.Star },
            new ColumnDefinition { Width = GridLength.Auto }
        } };
        navigation.Children.Add(previous);
        Grid.SetColumn(_monthTitle, 1);
        navigation.Children.Add(_monthTitle);
        Grid.SetColumn(next, 2);
        navigation.Children.Add(next);
        _monthCells = new Grid { ColumnSpacing = 3, RowSpacing = 3 };
        for (var c = 0; c < 7; c++)
            _monthCells.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        _filterHint = new Label { FontSize = 10, TextColor = Muted };
        _monthSection = new VerticalStackLayout
        {
            IsVisible = false, Spacing = 6, Children = { navigation, _monthCells, _filterHint }
        };
        root.Children.Add(_monthSection);

        Content = root;
        UpdateMode();
    }

    public bool Includes(MissionRecord mission) => Mode switch
    {
        ViewMode.Day or ViewMode.Month => MissionCalendarQuery.OccursOn(mission, SelectedDay),
        ViewMode.Range => MissionCalendarQuery.Overlaps(mission, RangeFrom, RangeTo),
        _ => true
    };

    public void SetMissions(IReadOnlyList<MissionRecord> missions)
    {
        _missions = missions;
        if (Mode == ViewMode.Month) RenderMonth();
    }

    public string ActiveFilterCaption => Mode switch
    {
        ViewMode.Day or ViewMode.Month => SelectedDay.ToString("dd.MM.yyyy", Polish),
        ViewMode.Range => $"{RangeFrom:dd.MM.yyyy} – {RangeTo:dd.MM.yyyy}",
        _ => "WSZYSTKIE TERMINY"
    };

    private void SelectMode(ViewMode mode)
    {
        if (Mode == mode) return;
        Mode = mode;
        if (mode == ViewMode.Month)
            _month = new DateOnly(_selectedDay.Year, _selectedDay.Month, 1);
        UpdateMode();
        FilterChanged?.Invoke();
    }

    private void UpdateMode()
    {
        for (var i = 0; i < _modes.Length; i++)
        {
            var selected = (int)Mode == i;
            _modes[i].BackgroundColor = selected ? Color.FromArgb("#123747") : Panel;
            _modes[i].TextColor = selected ? Cyan : Muted;
        }
        _daySection.IsVisible = Mode == ViewMode.Day;
        _rangeSection.IsVisible = Mode == ViewMode.Range;
        _monthSection.IsVisible = Mode == ViewMode.Month;
        if (Mode == ViewMode.Month) RenderMonth();
    }

    private void NormalizeRange(bool changedStart)
    {
        if (_updatingDates) return;
        _updatingDates = true;
        try
        {
            if (changedStart && RangeFrom > RangeTo)
                _toPicker.Date = _fromPicker.Date;
            else if (!changedStart && RangeTo < RangeFrom)
                _fromPicker.Date = _toPicker.Date;
        }
        finally { _updatingDates = false; }
        FilterChanged?.Invoke();
    }

    private void MoveMonth(int delta)
    {
        if ((delta < 0 && _month.Year == 1 && _month.Month == 1) ||
            (delta > 0 && _month.Year == 9999 && _month.Month == 12)) return;
        _month = _month.AddMonths(delta);
        _selectedDay = new DateOnly(_month.Year, _month.Month,
            Math.Min(_selectedDay.Day, DateTime.DaysInMonth(_month.Year, _month.Month)));
        _updatingDates = true;
        try { _dayPicker.Date = _selectedDay.ToDateTime(TimeOnly.MinValue); }
        finally { _updatingDates = false; }
        RenderMonth();
        FilterChanged?.Invoke();
    }

    private void EnsureMonthGrid()
    {
        if (_monthGridReady) return;
        // Six week rows cover all possible 28–31 day months.
        for (var row = 0; row < 7; row++)
            _monthCells.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var headings = new[] { "PN", "WT", "ŚR", "CZ", "PT", "SO", "ND" };
        for (var column = 0; column < 7; column++)
        {
            var title = new Label { Text = headings[column], FontSize = 9, TextColor = Muted,
                HorizontalTextAlignment = TextAlignment.Center };
            Grid.SetColumn(title, column);
            _monthCells.Children.Add(title);
        }
        for (var i = 0; i < 42; i++)
        {
            var dayLabel = new Label { FontSize = 12, TextColor = Text,
                HorizontalTextAlignment = TextAlignment.Center };
            var countLabel = new Label { FontSize = 9, TextColor = Cyan,
                HorizontalTextAlignment = TextAlignment.Center };
            var content = new VerticalStackLayout { Spacing = 0,
                VerticalOptions = LayoutOptions.Center,
                Children = { dayLabel, countLabel } };
            var border = new Border
            {
                Content = content, Padding = new Thickness(0, 4), MinimumHeightRequest = 40,
                StrokeThickness = 1, Stroke = new SolidColorBrush(BorderColor),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
                    { CornerRadius = new CornerRadius(4) },
                BackgroundColor = Panel, IsVisible = false
            };
            var cell = new DayCell { Border = border, Number = dayLabel, Count = countLabel };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                if (cell.Date is not { } date || _selectedDay == date) return;
                var previous = _selectedDay;
                _selectedDay = date;
                _updatingDates = true;
                try { _dayPicker.Date = date.ToDateTime(TimeOnly.MinValue); }
                finally { _updatingDates = false; }
                // Only the old and new selection colors changed; repainting all
                // 42 Android cells per tap causes avoidable UI-thread work.
                var today = DateOnly.FromDateTime(DateTime.Today);
                foreach (var changed in _dayCells.Where(x => x.Date == previous || x.Date == date))
                    PaintSelection(changed, today);
                _filterHint.Text = $"WYBRANY: {_selectedDay:dd.MM.yyyy}  ·  LICZBY = ZAPLANOWANE";
                FilterChanged?.Invoke();
            };
            border.GestureRecognizers.Add(tap);
            Grid.SetRow(border, i / 7 + 1);
            Grid.SetColumn(border, i % 7);
            _monthCells.Children.Add(border);
            _dayCells.Add(cell);
        }
        _monthGridReady = true;
    }

    private void RenderMonth()
    {
        if (Mode != ViewMode.Month) return;
        EnsureMonthGrid();
        _monthTitle.Text = _month.ToDateTime(TimeOnly.MinValue)
            .ToString("MMMM yyyy", Polish).ToUpper(Polish);
        var counts = MissionCalendarQuery.CountsForMonth(_missions, _month.Year, _month.Month);
        var startingColumn = ((int)_month.DayOfWeek + 6) % 7;
        var numberOfDays = DateTime.DaysInMonth(_month.Year, _month.Month);
        var today = DateOnly.FromDateTime(DateTime.Today);
        for (var i = 0; i < _dayCells.Count; i++)
        {
            var cell = _dayCells[i];
            var number = i - startingColumn + 1;
            if (number < 1 || number > numberOfDays)
            {
                cell.Date = null;
                cell.Border.IsVisible = false;
                continue;
            }
            var date = new DateOnly(_month.Year, _month.Month, number);
            cell.Date = date;
            cell.Border.IsVisible = true;
            counts.TryGetValue(date, out var count);
            cell.Number.Text = number.ToString(Polish);
            cell.Count.Text = count > 0 ? count.ToString(Polish) : " ";
            PaintSelection(cell, today);
            SemanticProperties.SetDescription(cell.Border,
                $"{date:dd.MM.yyyy}, {count} zaplanowanych misji. Wybierz dzień.");
        }
        _filterHint.Text = $"WYBRANY: {_selectedDay:dd.MM.yyyy}  ·  LICZBY = ZAPLANOWANE";
    }

    private void PaintSelection(DayCell cell, DateOnly today)
    {
        if (cell.Date is not { } date) return;
        var selected = date == _selectedDay;
        cell.Number.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
        cell.Number.TextColor = selected ? Cyan : Text;
        cell.Border.BackgroundColor = selected ? Color.FromArgb("#123747") : Panel;
        cell.Border.Stroke = new SolidColorBrush(
            selected ? Cyan : date == today ? Muted : BorderColor);
    }

    private Button MonthArrow(string symbol, int offset)
    {
        var button = new Button { Text = symbol, WidthRequest = 36, HeightRequest = 30,
            Padding = new Thickness(0), FontSize = 18, BackgroundColor = Panel, TextColor = Cyan };
        button.Clicked += (_, _) => MoveMonth(offset);
        return button;
    }

    private static DatePicker DateInput(DateTime day) => new()
    {
        Date = day, Format = "dd.MM.yyyy", FontSize = 12,
        TextColor = Text, BackgroundColor = Panel
    };

    private static VerticalStackLayout DateColumn(string title, DatePicker picker) => new()
    {
        Spacing = 2,
        Children =
        {
            new Label { Text = title, FontSize = 10, TextColor = Muted }, picker
        }
    };
}
