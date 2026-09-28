using System;
using System.Collections.Generic;
using System.Globalization;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>The header layout of a <see cref="Calendar"/>, following the two Material Design 3 date pickers.</summary>
public enum CalendarLayout
{
    /// <summary>
    /// The modal date picker's layout: a "Month Year" button that opens the year grid, and previous/next month buttons.
    /// </summary>
    Modal,

    /// <summary>
    /// The docked date picker's layout: separate month and year menus, each with previous/next buttons.
    /// </summary>
    Docked,
}

/// <summary>What a <see cref="Calendar"/> shows.</summary>
public enum CalendarView
{
    /// <summary>The days of the displayed month.</summary>
    Days,

    /// <summary>The months, to pick the displayed month (docked layout).</summary>
    Months,

    /// <summary>The years of the date range, to pick the displayed year.</summary>
    Years,
}

/// <summary>
/// A Material Design 3 month calendar for picking a date: the body of the docked and modal date pickers.
/// </summary>
/// <remarks>
/// <para>
/// The grid shows the days of <see cref="DisplayDate"/>'s month under the weekday initials, starting on
/// <see cref="FirstDayOfWeek"/>. Clicking a day selects it; today is outlined and the selected date filled. Days outside
/// <see cref="MinDate"/>..<see cref="MaxDate"/> are disabled.
/// </para>
/// <para>
/// The <see cref="CalendarLayout.Modal"/> header has a "Month Year" button that opens the year grid and previous/next
/// month buttons; the <see cref="CalendarLayout.Docked"/> header has month and year menus with their own arrows.
/// </para>
/// <para>
/// The day grid is one tab stop. Arrow keys move by a day or a week, Home/End to the start and end of the week, Page
/// Up/Page Down by a month (with Shift: a year), and Enter or Space select the focused day; moving past the month shows
/// the next one.
/// </para>
/// </remarks>
public class Calendar : Control
{
    /// <summary>Identifies the <see cref="SelectedDate"/> property.</summary>
    public static readonly BindableProperty<DateOnly?> SelectedDateProperty =
        BindableProperty.Register<Calendar, DateOnly?>(nameof(SelectedDate), null, (s, o, n) => ((Calendar)s).OnSelectedDateChanged(n));

    /// <summary>Identifies the <see cref="DisplayDate"/> property.</summary>
    public static readonly BindableProperty<DateOnly> DisplayDateProperty =
        BindableProperty.Register<Calendar, DateOnly>(
            nameof(DisplayDate), default, (s, o, n) => ((Calendar)s).Refresh(),
            coerceValue: (s, v) => ((Calendar)s).CoerceDisplayDate(v));

    /// <summary>Identifies the <see cref="MinDate"/> property.</summary>
    public static readonly BindableProperty<DateOnly> MinDateProperty =
        BindableProperty.Register<Calendar, DateOnly>(nameof(MinDate), new DateOnly(1900, 1, 1), (s, o, n) => ((Calendar)s).OnRangeChanged());

    /// <summary>Identifies the <see cref="MaxDate"/> property.</summary>
    public static readonly BindableProperty<DateOnly> MaxDateProperty =
        BindableProperty.Register<Calendar, DateOnly>(nameof(MaxDate), new DateOnly(2100, 12, 31), (s, o, n) => ((Calendar)s).OnRangeChanged());

    /// <summary>Identifies the <see cref="FirstDayOfWeek"/> property.</summary>
    public static readonly BindableProperty<DayOfWeek> FirstDayOfWeekProperty =
        BindableProperty.Register<Calendar, DayOfWeek>(
            nameof(FirstDayOfWeek), CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek, (s, o, n) => ((Calendar)s).OnFirstDayOfWeekChanged());

    /// <summary>Identifies the <see cref="Layout"/> property.</summary>
    public static readonly BindableProperty<CalendarLayout> LayoutProperty =
        BindableProperty.Register<Calendar, CalendarLayout>(nameof(Layout), CalendarLayout.Modal, (s, o, n) => ((Calendar)s).OnLayoutChanged());

    /// <summary>Identifies the <see cref="View"/> property.</summary>
    public static readonly BindableProperty<CalendarView> ViewProperty =
        BindableProperty.Register<Calendar, CalendarView>(nameof(View), CalendarView.Days, (s, o, n) => ((Calendar)s).OnViewChanged());

    /// <summary>The style key of the header's text and icon buttons (MD3: on-surface-variant).</summary>
    public const string HeaderButtonStyleKey = "CalendarHeaderButton";

    private const float CellSize = 48f;
    private const float WeekdayRowHeight = 40f;
    private const float BodyHeight = WeekdayRowHeight + 6 * CellSize;

    private static TimeProvider s_timeProvider = TimeProvider.System;

    private readonly Grid _header = new() { Height = 48 };
    private readonly Button _monthYearButton;
    private readonly TextBlock _monthYearText = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Icon _monthYearArrow = new(MaterialIconKind.ArrowDropDown, 18) { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _previousMonth;
    private readonly Button _nextMonth;
    private readonly Button _monthMenuButton;
    private readonly TextBlock _monthMenuText = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _previousYear;
    private readonly Button _nextYear;
    private readonly Button _yearMenuButton;
    private readonly TextBlock _yearMenuText = new() { VerticalAlignment = VerticalAlignment.Center };

    private readonly TextBlock[] _weekdayLabels = new TextBlock[7];
    private readonly CalendarDayButton[] _days = new CalendarDayButton[42];
    private readonly StackPanel _daysView = new();
    private readonly ScrollViewer _yearsView = new() { Height = BodyHeight, Visibility = Visibility.Collapsed };
    private readonly ScrollViewer _monthsView = new() { Height = BodyHeight, Visibility = Visibility.Collapsed };
    private readonly StackPanel _monthsList = new();
    private readonly List<CalendarYearButton> _yearButtons = [];
    private readonly List<CalendarMenuItem> _yearMenuItems = [];
    private readonly CalendarMenuItem[] _monthMenuItems = new CalendarMenuItem[12];

    private DateOnly _focusDate;
    private bool _isRefreshing;

    /// <summary>
    /// Gets or sets the clock that decides today's date for all calendars; tests can use a fake one. The default is
    /// <see cref="TimeProvider.System"/>.
    /// </summary>
    public static TimeProvider TimeProvider
    {
        get => s_timeProvider;
        set => s_timeProvider = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Gets today's date in the local time zone, from <see cref="TimeProvider"/>.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(s_timeProvider.GetLocalNow().DateTime);

    /// <summary>Initializes a calendar showing the current month.</summary>
    public Calendar()
    {
        _monthYearButton = HeaderButton(Row(_monthYearText, _monthYearArrow));
        _monthYearButton.Click += (_, _) => View = View == CalendarView.Years ? CalendarView.Days : CalendarView.Years;
        _previousMonth = IconButton(MaterialIconKind.ChevronLeft, "Previous month");
        _previousMonth.Click += (_, _) => ShowMonth(-1);
        _nextMonth = IconButton(MaterialIconKind.ChevronRight, "Next month");
        _nextMonth.Click += (_, _) => ShowMonth(1);

        _monthMenuButton = HeaderButton(Row(_monthMenuText, new Icon(MaterialIconKind.ArrowDropDown, 18) { VerticalAlignment = VerticalAlignment.Center }));
        _monthMenuButton.Click += (_, _) => View = View == CalendarView.Months ? CalendarView.Days : CalendarView.Months;
        _previousYear = IconButton(MaterialIconKind.ChevronLeft, "Previous year");
        _previousYear.Click += (_, _) => ShowMonth(-12);
        _nextYear = IconButton(MaterialIconKind.ChevronRight, "Next year");
        _nextYear.Click += (_, _) => ShowMonth(12);
        _yearMenuButton = HeaderButton(Row(_yearMenuText, new Icon(MaterialIconKind.ArrowDropDown, 18) { VerticalAlignment = VerticalAlignment.Center }));
        _yearMenuButton.Click += (_, _) => View = View == CalendarView.Years ? CalendarView.Days : CalendarView.Years;

        BuildDaysView();
        BuildMonthsView();

        var body = new Grid();
        body.Add(_daysView);
        body.Add(_yearsView);
        body.Add(_monthsView);

        var root = new StackPanel();
        root.Add(_header);
        root.Add(body);
        AddChild(root);

        DisplayDate = Today;
        OnLayoutChanged();
    }

    #region Properties

    /// <summary>Gets or sets the selected date, or <c>null</c> for none. The default is <c>null</c>.</summary>
    public DateOnly? SelectedDate { get => GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }

    /// <summary>
    /// Gets or sets a date in the month shown; it is kept on the first of the month within
    /// <see cref="MinDate"/>..<see cref="MaxDate"/>. The default is the current month.
    /// </summary>
    public DateOnly DisplayDate { get => GetValue(DisplayDateProperty); set => SetValue(DisplayDateProperty, value); }

    /// <summary>Gets or sets the earliest date that can be picked. The default is January 1, 1900.</summary>
    public DateOnly MinDate { get => GetValue(MinDateProperty); set => SetValue(MinDateProperty, value); }

    /// <summary>Gets or sets the latest date that can be picked. The default is December 31, 2100.</summary>
    public DateOnly MaxDate { get => GetValue(MaxDateProperty); set => SetValue(MaxDateProperty, value); }

    /// <summary>Gets or sets the day each week row starts with. The default comes from the current culture.</summary>
    public DayOfWeek FirstDayOfWeek { get => GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }

    /// <summary>Gets or sets the header layout. The default is <see cref="CalendarLayout.Modal"/>.</summary>
    public CalendarLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    /// <summary>Gets or sets what the calendar shows: days, the month menu or the years. The default is days.</summary>
    public CalendarView View { get => GetValue(ViewProperty); set => SetValue(ViewProperty, value); }

    /// <summary>Occurs when <see cref="SelectedDate"/> changes, with the new date.</summary>
    public event EventHandler<DateOnly?>? SelectedDateChanged;

    /// <summary>Occurs when the user picks a day (by pointer or keyboard), with the date; also when it was already selected.</summary>
    public event EventHandler<DateOnly>? DayPicked;

    /// <summary>Gets the button of <paramref name="date"/> if it is shown, otherwise <c>null</c>.</summary>
    public CalendarDayButton? GetDayButton(DateOnly date)
    {
        foreach (var day in _days)
        {
            if (day.Visibility == Visibility.Visible && day.Date == date) return day;
        }
        return null;
    }

    /// <summary>Gets the date the day grid's keyboard focus is on (the one tab stop of the grid).</summary>
    public DateOnly FocusDate => _focusDate;

    /// <summary>Gets the year buttons of the year grid (<see cref="CalendarLayout.Modal"/>).</summary>
    public IReadOnlyList<CalendarYearButton> YearButtons => _yearButtons;

    #endregion

    #region Building

    private static StackPanel Row(params UIElement[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var child in children) row.Add(child);
        return row;
    }

    private static Button HeaderButton(UIElement content) => new()
    {
        Variant = ButtonVariant.Text,
        Content = content,
        Padding = new Thickness(12, 8, 8, 8),
        VerticalAlignment = VerticalAlignment.Center,
        StyleKey = HeaderButtonStyleKey,
    };

    private static Button IconButton(MaterialIconKind kind, string toolTip)
    {
        var button = new Button
        {
            Variant = ButtonVariant.Text,
            Content = new Icon(kind, 24),
            Padding = new Thickness(8),
            MinWidth = 40,
            Width = 40,
            Height = 40,
            CornerRadius = new CornerRadius(20),
            VerticalAlignment = VerticalAlignment.Center,
            StyleKey = HeaderButtonStyleKey,
        };
        ToolTipService.SetToolTip(button, toolTip);
        return button;
    }

    private void BuildDaysView()
    {
        var weekdays = new UniformGrid { Columns = 7, Height = WeekdayRowHeight };
        for (int i = 0; i < 7; i++)
        {
            _weekdayLabels[i] = new TextBlock { FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            weekdays.Add(_weekdayLabels[i]);
        }

        var grid = new UniformGrid { Columns = 7, Rows = 6, Height = 6 * CellSize, Width = 7 * CellSize };
        for (int i = 0; i < _days.Length; i++)
        {
            var day = new CalendarDayButton { IsFocusable = false };
            day.Click += (s, _) => PickDay(((CalendarDayButton)s!).Date);
            _days[i] = day;
            grid.Add(day);
        }

        weekdays.Width = 7 * CellSize;
        _daysView.Add(weekdays);
        _daysView.Add(grid);
        UpdateWeekdayLabels();
    }

    private void BuildMonthsView()
    {
        var names = CultureInfo.CurrentCulture.DateTimeFormat.MonthNames;
        for (int m = 1; m <= 12; m++)
        {
            var item = new CalendarMenuItem(names[m - 1], m) { HorizontalAlignment = HorizontalAlignment.Stretch };
            item.Click += (s, _) =>
            {
                var d = DisplayDate;
                DisplayDate = new DateOnly(d.Year, ((CalendarMenuItem)s!).Value, 1);
                View = CalendarView.Days;
            };
            _monthMenuItems[m - 1] = item;
            _monthsList.Add(item);
        }
        _monthsView.Content = _monthsList;
    }

    // The years of the range: a 3-column grid of pills (modal) or a menu list (docked).
    private void BuildYears()
    {
        _yearButtons.Clear();
        _yearMenuItems.Clear();
        int first = MinDate.Year, last = Math.Max(first, MaxDate.Year);

        if (Layout == CalendarLayout.Modal)
        {
            var grid = new UniformGrid { Columns = 3, RowSpacing = 16, Margin = new Thickness(0, 8) };
            for (int year = first; year <= last; year++)
            {
                var button = new CalendarYearButton(year);
                button.Click += (s, _) => PickYear(((CalendarYearButton)s!).Year);
                _yearButtons.Add(button);
                grid.Add(button);
            }
            _yearsView.Content = grid;
        }
        else
        {
            var list = new StackPanel();
            for (int year = first; year <= last; year++)
            {
                var item = new CalendarMenuItem(year.ToString(CultureInfo.CurrentCulture), year) { HorizontalAlignment = HorizontalAlignment.Stretch };
                item.Click += (s, _) => PickYear(((CalendarMenuItem)s!).Value);
                _yearMenuItems.Add(item);
                list.Add(item);
            }
            _yearsView.Content = list;
        }
    }

    private void OnLayoutChanged()
    {
        _header.Clear();
        _header.ColumnDefinitions.Clear();

        if (Layout == CalendarLayout.Modal)
        {
            _header.Margin = Thickness.Zero;
            AddHeaderColumn(_monthYearButton, GridLength.Auto);
            AddHeaderColumn(null, GridLength.Star);
            AddHeaderColumn(_previousMonth, GridLength.Auto);
            AddHeaderColumn(_nextMonth, GridLength.Auto);
        }
        else
        {
            _header.Margin = new Thickness(4, 4, 4, 0);
            AddHeaderColumn(_previousMonth, GridLength.Auto);
            AddHeaderColumn(_monthMenuButton, GridLength.Auto);
            AddHeaderColumn(_nextMonth, GridLength.Auto);
            AddHeaderColumn(null, GridLength.Star);
            AddHeaderColumn(_previousYear, GridLength.Auto);
            AddHeaderColumn(_yearMenuButton, GridLength.Auto);
            AddHeaderColumn(_nextYear, GridLength.Auto);
        }

        BuildYears();
        View = CalendarView.Days;
        Refresh();
    }

    private void AddHeaderColumn(UIElement? element, GridLength width)
    {
        _header.ColumnDefinitions.Add(new ColumnDefinition(width));
        if (element != null)
        {
            Grid.SetColumn(element, _header.ColumnDefinitions.Count - 1);
            _header.Add(element);
        }
    }

    #endregion

    #region State

    private DateOnly CoerceDisplayDate(DateOnly date)
    {
        if (date == default && !IsInitialized) return date;
        var first = new DateOnly(date.Year, date.Month, 1);
        var min = new DateOnly(MinDate.Year, MinDate.Month, 1);
        var max = new DateOnly(MaxDate.Year, MaxDate.Month, 1);
        return first < min ? min : first > max && max >= min ? max : first;
    }

    // DisplayDate's default (0001-01-01) is replaced in the constructor; until then coercion leaves it alone.
    private bool IsInitialized => _days[^1] != null;

    private void OnSelectedDateChanged(DateOnly? date)
    {
        if (date is { } selected)
        {
            _focusDate = selected;
            DisplayDate = selected;
        }
        Refresh();
        SelectedDateChanged?.Invoke(this, date);
    }

    private void OnRangeChanged()
    {
        CoerceValue(DisplayDateProperty);
        if (GetValueSource(DisplayDateProperty) == ValueSource.Default) DisplayDate = Today;
        BuildYears();
        Refresh();
    }

    private void OnFirstDayOfWeekChanged()
    {
        UpdateWeekdayLabels();
        Refresh();
    }

    private void OnViewChanged()
    {
        var view = View;
        if (view == CalendarView.Months && Layout == CalendarLayout.Modal)
        {
            View = CalendarView.Years; // the modal layout has no month menu
            return;
        }

        _daysView.Visibility = view == CalendarView.Days ? Visibility.Visible : Visibility.Collapsed;
        _yearsView.Visibility = view == CalendarView.Years ? Visibility.Visible : Visibility.Collapsed;
        _monthsView.Visibility = view == CalendarView.Months ? Visibility.Visible : Visibility.Collapsed;
        _monthYearArrow.Kind = view == CalendarView.Years ? MaterialIconKind.ArrowDropUp : MaterialIconKind.ArrowDropDown;
        Refresh();

        // Center the current year or month once the list is laid out (see ArrangeOverride).
        _scrollToSelectionPending = view != CalendarView.Days;
        InvalidateArrange();
    }

    private bool _scrollToSelectionPending;

    // Centers the entry of the shown year (year view) or month (month menu) in its list.
    private void ScrollToSelection()
    {
        var month = DisplayDate;
        ScrollViewer viewer;
        UIElement? target;
        if (View == CalendarView.Months)
        {
            viewer = _monthsView;
            target = _monthMenuItems[month.Month - 1];
        }
        else
        {
            viewer = _yearsView;
            int year = (SelectedDate ?? month).Year; // the highlighted year
            target = Layout == CalendarLayout.Modal
                ? _yearButtons.Find(b => b.Year == year)
                : _yearMenuItems.Find(i => i.Value == year);
        }
        if (target == null || viewer.Content is not UIElement list) return;

        float top = target.GetTransformToAncestor(list).Translation.Y;
        viewer.ScrollTo(0, top - (BodyHeight - target.Bounds.Height) * 0.5f, animate: false);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        if (_scrollToSelectionPending)
        {
            _scrollToSelectionPending = false;
            ScrollToSelection();
        }
        return size;
    }

    private void UpdateWeekdayLabels()
    {
        var names = CultureInfo.CurrentCulture.DateTimeFormat.ShortestDayNames;
        for (int i = 0; i < 7; i++)
        {
            string name = names[((int)FirstDayOfWeek + i) % 7];
            _weekdayLabels[i].Text = StringInfo.GetNextTextElement(name, 0);
        }
    }

    private void ShowMonth(int months)
    {
        DisplayDate = DisplayDate.AddMonths(months);
        _focusDate = ClampToMonth(_focusDate.AddMonths(months));
        Refresh();
    }

    private DateOnly ClampToMonth(DateOnly date)
    {
        var first = DisplayDate;
        var last = first.AddMonths(1).AddDays(-1);
        return date < first ? first : date > last ? last : date;
    }

    private void PickDay(DateOnly date)
    {
        _focusDate = date;
        SelectedDate = date;
        DayPicked?.Invoke(this, date);
    }

    private void PickYear(int year)
    {
        var d = DisplayDate;
        DisplayDate = new DateOnly(year, d.Month, 1);
        _focusDate = ClampToMonth(new DateOnly(year, _focusDate.Month, Math.Min(_focusDate.Day, DateTime.DaysInMonth(year, _focusDate.Month))));
        View = CalendarView.Days;
    }

    // Shows DisplayDate's month and the selection state everywhere.
    private void Refresh()
    {
        if (!IsInitialized || _isRefreshing) return;
        _isRefreshing = true;
        try
        {
            var culture = CultureInfo.CurrentCulture;
            var month = DisplayDate;
            var today = Today;
            var selected = SelectedDate;
            var min = MinDate;
            var max = MaxDate;

            _monthYearText.Text = month.ToString("MMMM yyyy", culture);
            _monthMenuText.Text = month.ToString("MMM", culture);
            _yearMenuText.Text = month.ToString("yyyy", culture);

            var monthStart = month;
            var monthEnd = month.AddMonths(1).AddDays(-1);
            _previousMonth.IsEnabled = monthStart > min;
            _nextMonth.IsEnabled = monthEnd < max;
            _previousYear.IsEnabled = monthStart.AddMonths(-12).AddMonths(1).AddDays(-1) >= min;
            _nextYear.IsEnabled = monthStart.AddMonths(12) <= max;

            bool daysShown = View == CalendarView.Days;
            if (Layout == CalendarLayout.Modal)
            {
                _previousMonth.Visibility = _nextMonth.Visibility = daysShown ? Visibility.Visible : Visibility.Hidden;
            }
            else
            {
                // The arrows of the other menu don't apply while a menu is open.
                bool monthArrows = View != CalendarView.Years;
                bool yearArrows = View != CalendarView.Months;
                _previousMonth.Visibility = _nextMonth.Visibility = daysShown && monthArrows ? Visibility.Visible : Visibility.Hidden;
                _previousYear.Visibility = _nextYear.Visibility = daysShown && yearArrows ? Visibility.Visible : Visibility.Hidden;
                _yearMenuButton.IsEnabled = View != CalendarView.Months;
                _monthMenuButton.IsEnabled = View != CalendarView.Years;
            }

            if (_focusDate < monthStart || _focusDate > monthEnd)
            {
                _focusDate = selected is { } s && s >= monthStart && s <= monthEnd ? s
                    : today >= monthStart && today <= monthEnd ? today
                    : monthStart;
            }

            int offset = ((int)monthStart.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
            for (int i = 0; i < _days.Length; i++)
            {
                var day = _days[i];
                int dayNumber = i - offset + 1;
                if (dayNumber < 1 || dayNumber > monthEnd.Day)
                {
                    day.Visibility = Visibility.Hidden;
                    day.IsFocusable = false;
                    continue;
                }

                var date = new DateOnly(month.Year, month.Month, dayNumber);
                day.SetDate(date, dayNumber.ToString(culture));
                day.Visibility = Visibility.Visible;
                day.IsEnabled = date >= min && date <= max;
                day.IsSelected = date == selected;
                day.IsToday = date == today;
                day.IsFocusable = date == _focusDate;
            }

            int selectedYear = (selected ?? month).Year;
            foreach (var button in _yearButtons)
            {
                button.IsSelected = button.Year == selectedYear;
                button.IsCurrent = button.Year == today.Year;
            }
            foreach (var item in _yearMenuItems)
            {
                item.IsSelected = item.Value == month.Year;
            }
            foreach (var item in _monthMenuItems)
            {
                item.IsSelected = item.Value == month.Month;
                item.IsEnabled = new DateOnly(month.Year, item.Value, 1).AddMonths(1).AddDays(-1) >= min
                                 && new DateOnly(month.Year, item.Value, 1) <= max;
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    #endregion

    #region Keyboard

    /// <inheritdoc/>
    /// <remarks>Moves the focus between days; see the class remarks for the keys.</remarks>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || View != CalendarView.Days || e.Source is not CalendarDayButton) return;

        bool shift = (e.Modifiers & ModifierKeys.Shift) != 0;
        int column = ((int)_focusDate.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
        DateOnly target;
        switch (e.Key)
        {
            case Key.Left: target = _focusDate.AddDays(-1); break;
            case Key.Right: target = _focusDate.AddDays(1); break;
            case Key.Up: target = _focusDate.AddDays(-7); break;
            case Key.Down: target = _focusDate.AddDays(7); break;
            case Key.Home: target = _focusDate.AddDays(-column); break;
            case Key.End: target = _focusDate.AddDays(6 - column); break;
            case Key.PageUp: target = shift ? _focusDate.AddYears(-1) : _focusDate.AddMonths(-1); break;
            case Key.PageDown: target = shift ? _focusDate.AddYears(1) : _focusDate.AddMonths(1); break;
            default: return;
        }

        e.Handled = true;
        MoveFocusTo(target < MinDate ? MinDate : target > MaxDate ? MaxDate : target);
    }

    /// <summary>Moves the day grid's keyboard focus to <paramref name="date"/>, showing its month.</summary>
    public void MoveFocusTo(DateOnly date)
    {
        _focusDate = date;
        DisplayDate = date;
        Refresh();
        GetDayButton(date)?.Focus();
    }

    #endregion
}
