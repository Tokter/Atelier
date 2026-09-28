using System;
using System.Globalization;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>How a <see cref="DatePicker"/> opens its calendar.</summary>
public enum DatePickerMode
{
    /// <summary>A calendar docked below the field (the MD3 docked date picker).</summary>
    Docked,

    /// <summary>A modal dialog with a calendar (the MD3 modal date picker).</summary>
    Modal,

    /// <summary>A modal dialog with a text field (the MD3 modal date input).</summary>
    ModalInput,
}

/// <summary>
/// A Material Design 3 date field: an outlined text field for typing a date, with a calendar button that opens a
/// docked calendar or a modal date picker.
/// </summary>
/// <remarks>
/// <para>
/// Typed dates (in <see cref="DateFormat"/>, shown as the placeholder and supporting text) are applied on Enter or when
/// the field loses focus; invalid or out-of-range input shows an error. Clearing the text clears the date.
/// </para>
/// <para>
/// The calendar button, or Alt+Down, opens the picker chosen by <see cref="Mode"/>: a calendar below the field
/// (<see cref="DatePickerMode.Docked"/>), or a <see cref="DatePickerDialog"/> in the nearest <see cref="DialogHost"/>
/// (<see cref="DatePickerMode.Modal"/> and <see cref="DatePickerMode.ModalInput"/>). OK applies the picked date; Cancel,
/// Escape or a click outside the docked calendar keep the previous one.
/// </para>
/// </remarks>
public class DatePicker : Control
{
    /// <summary>Identifies the <see cref="SelectedDate"/> property.</summary>
    public static readonly BindableProperty<DateOnly?> SelectedDateProperty =
        BindableProperty.Register<DatePicker, DateOnly?>(nameof(SelectedDate), null, (s, o, n) => ((DatePicker)s).OnSelectedDateChanged(n));

    /// <summary>Identifies the <see cref="Mode"/> property.</summary>
    public static readonly BindableProperty<DatePickerMode> ModeProperty =
        BindableProperty.Register<DatePicker, DatePickerMode>(nameof(Mode), DatePickerMode.Docked);

    /// <summary>Identifies the <see cref="Label"/> property.</summary>
    public static readonly BindableProperty<string> LabelProperty =
        BindableProperty.Register<DatePicker, string>(nameof(Label), "Date", (s, o, n) => ((DatePicker)s).ApplyCompact());

    /// <summary>Identifies the <see cref="IsCompact"/> property.</summary>
    public static readonly BindableProperty<bool> IsCompactProperty =
        BindableProperty.Register<DatePicker, bool>(nameof(IsCompact), false, (s, o, n) => ((DatePicker)s).ApplyCompact());

    /// <summary>Identifies the <see cref="DateFormat"/> property.</summary>
    public static readonly BindableProperty<string?> DateFormatProperty =
        BindableProperty.Register<DatePicker, string?>(nameof(DateFormat), null, (s, o, n) => ((DatePicker)s).OnDateFormatChanged());

    /// <summary>Identifies the <see cref="MinDate"/> property.</summary>
    public static readonly BindableProperty<DateOnly> MinDateProperty =
        BindableProperty.Register<DatePicker, DateOnly>(nameof(MinDate), new DateOnly(1900, 1, 1), (s, o, n) => ((DatePicker)s)._calendar.MinDate = n);

    /// <summary>Identifies the <see cref="MaxDate"/> property.</summary>
    public static readonly BindableProperty<DateOnly> MaxDateProperty =
        BindableProperty.Register<DatePicker, DateOnly>(nameof(MaxDate), new DateOnly(2100, 12, 31), (s, o, n) => ((DatePicker)s)._calendar.MaxDate = n);

    /// <summary>Identifies the <see cref="FirstDayOfWeek"/> property.</summary>
    public static readonly BindableProperty<DayOfWeek> FirstDayOfWeekProperty =
        BindableProperty.Register<DatePicker, DayOfWeek>(
            nameof(FirstDayOfWeek), CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek, (s, o, n) => ((DatePicker)s)._calendar.FirstDayOfWeek = n);

    private readonly TextBox _textBox;
    private readonly Popup _popup;
    private readonly Calendar _calendar = new() { Layout = CalendarLayout.Docked, Margin = new Thickness(12, 0) };
    private bool _isSyncingText;
    private DatePickerDialog? _dialog;

    /// <summary>Initializes an empty date field with a docked calendar.</summary>
    public DatePicker()
    {
        _textBox = new TextBox { Label = "Date", TrailingIconKind = MaterialIconKind.CalendarToday };
        _textBox.TrailingIconClick += (_, _) => OpenPicker();
        _textBox.TextChanged += (_, _) =>
        {
            if (!_isSyncingText) Validation.SetErrors(_textBox, this, null);
        };
        _textBox.LostFocus += (_, _) => CommitText();
        _textBox.KeyDown += OnTextBoxKeyDown;

        var cancel = new Button("Cancel") { Variant = ButtonVariant.Text };
        cancel.Click += (_, _) => CloseDockedPicker();
        var ok = new Button("OK") { Variant = ButtonVariant.Text };
        ok.Click += (_, _) =>
        {
            SelectedDate = _calendar.SelectedDate;
            CloseDockedPicker();
        };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 8, 12, 12) };
        actions.WithChildren(cancel, ok);

        var content = new StackPanel { Width = 360 };
        content.WithChildren(_calendar, actions);
        content.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                CloseDockedPicker();
                e.Handled = true;
            }
        };

        _popup = new Popup
        {
            PlacementTarget = _textBox,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            VerticalOffset = 4,
            StyleKey = PickerStyleKeys.DockedPopup,
            Child = content,
        };

        AddChild(_textBox);
        AddChild(_popup);
        OnDateFormatChanged();
    }

    #region Properties

    /// <summary>Gets or sets the date, or <c>null</c> for none. The default is <c>null</c>.</summary>
    public DateOnly? SelectedDate { get => GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }

    /// <summary>Gets or sets how the calendar button opens the picker. The default is <see cref="DatePickerMode.Docked"/>.</summary>
    public DatePickerMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    /// <summary>Gets or sets the field's label. The default is "Date".</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>
    /// Gets or sets whether the field is compact: 32 high, without the label and the format hint below it, e.g. as a
    /// <see cref="DataGrid"/> cell editor or in a toolbar. The default is <c>false</c>.
    /// </summary>
    public bool IsCompact { get => GetValue(IsCompactProperty); set => SetValue(IsCompactProperty, value); }

    private void ApplyCompact()
    {
        bool compact = IsCompact;
        _textBox.Label = compact ? string.Empty : Label;
        if (compact) _textBox.FieldHeight = 32;
        else _textBox.ClearValue(TextBox.FieldHeightProperty);
        OnDateFormatChanged();
    }

    /// <summary>
    /// Gets or sets the pattern the date is shown and typed in, such as <c>dd.MM.yyyy</c>; <c>null</c> (the default) uses
    /// the culture's numeric short date (see <see cref="PickerFormat.GetDatePattern"/>).
    /// </summary>
    public string? DateFormat { get => GetValue(DateFormatProperty); set => SetValue(DateFormatProperty, value); }

    /// <summary>Gets or sets the earliest date that can be picked or typed. The default is January 1, 1900.</summary>
    public DateOnly MinDate { get => GetValue(MinDateProperty); set => SetValue(MinDateProperty, value); }

    /// <summary>Gets or sets the latest date that can be picked or typed. The default is December 31, 2100.</summary>
    public DateOnly MaxDate { get => GetValue(MaxDateProperty); set => SetValue(MaxDateProperty, value); }

    /// <summary>Gets or sets the first day of the calendar's weeks. The default comes from the current culture.</summary>
    public DayOfWeek FirstDayOfWeek { get => GetValue(FirstDayOfWeekProperty); set => SetValue(FirstDayOfWeekProperty, value); }

    /// <summary>Occurs when <see cref="SelectedDate"/> changes, with the new date.</summary>
    public event EventHandler<DateOnly?>? SelectedDateChanged;

    /// <summary>Gets the text field.</summary>
    public TextBox TextBox => _textBox;

    /// <summary>Gets the docked calendar (shown in <see cref="DatePickerMode.Docked"/> mode).</summary>
    public Calendar DockedCalendar => _calendar;

    /// <summary>Gets whether the docked calendar is open.</summary>
    public bool IsDockedPickerOpen => _popup.IsOpen;

    /// <summary>Gets the open date picker dialog, or <c>null</c>.</summary>
    public DatePickerDialog? OpenDialog => _dialog;

    private string Pattern => DateFormat ?? PickerFormat.GetDatePattern();

    #endregion

    private void OnSelectedDateChanged(DateOnly? date)
    {
        ShowText(date);
        SelectedDateChanged?.Invoke(this, date);
    }

    private void OnDateFormatChanged()
    {
        string placeholder = PickerFormat.GetDatePlaceholder(Pattern);
        _textBox.Placeholder = placeholder;
        _textBox.SupportingText = IsCompact ? string.Empty : placeholder;
        ShowText(SelectedDate);
    }

    private void ShowText(DateOnly? date)
    {
        _isSyncingText = true;
        try
        {
            _textBox.Text = date?.ToString(Pattern, CultureInfo.CurrentCulture) ?? string.Empty;
            Validation.SetErrors(_textBox, this, null);
        }
        finally
        {
            _isSyncingText = false;
        }
    }

    // Applies the typed text: empty clears the date, a valid date in range sets it, anything else shows an error.
    private void CommitText()
    {
        string text = _textBox.Text.Trim();
        if (text.Length == 0)
        {
            SelectedDate = null;
            return;
        }

        if (!PickerFormat.TryParseDate(text, Pattern, out var date))
        {
            Validation.SetErrors(_textBox, this, [$"Invalid format. Use {PickerFormat.GetDatePlaceholder(Pattern)}"]);
            return;
        }
        if (date < MinDate || date > MaxDate)
        {
            Validation.SetErrors(_textBox, this, ["Date out of range"]);
            return;
        }

        if (SelectedDate == date)
        {
            ShowText(date); // normalize, e.g. 7/4/2026 -> 07/04/2026
        }
        SelectedDate = date;
    }

    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            CommitText();
        }
        else if (e.Key == Key.Down && (e.Modifiers & ModifierKeys.Alt) != 0)
        {
            OpenPicker();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Opens the picker of <see cref="Mode"/>, starting at the typed date if it is valid, else at
    /// <see cref="SelectedDate"/>.
    /// </summary>
    public async void OpenPicker()
    {
        if (!IsEnabled || _popup.IsOpen || _dialog != null) return;

        DateOnly? start = SelectedDate;
        if (PickerFormat.TryParseDate(_textBox.Text, Pattern, out var typed) && typed >= MinDate && typed <= MaxDate)
        {
            start = typed;
        }

        if (Mode == DatePickerMode.Docked)
        {
            _calendar.View = CalendarView.Days;
            _calendar.SelectedDate = start;
            if (start is { } d) _calendar.DisplayDate = d;
            _popup.IsOpen = true;
            _calendar.MoveFocusTo(_calendar.FocusDate);
            return;
        }

        var dialog = new DatePickerDialog
        {
            SelectedDate = start,
            InputMode = Mode == DatePickerMode.ModalInput ? DatePickerInputMode.Input : DatePickerInputMode.Calendar,
            MinDate = MinDate,
            MaxDate = MaxDate,
            FirstDayOfWeek = FirstDayOfWeek,
            DateFormat = DateFormat,
        };
        _dialog = dialog;
        try
        {
            var response = await dialog.ShowAsync(this);
            if (response.Result == DialogResult.Ok)
            {
                SelectedDate = dialog.SelectedDate;
                ShowText(SelectedDate);
            }
        }
        finally
        {
            _dialog = null;
        }
        _textBox.Focus();
    }

    private void CloseDockedPicker()
    {
        _popup.IsOpen = false;
        _textBox.Focus();
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        _textBox.Measure(availableSize);
        return _textBox.DesiredSize;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _textBox.Arrange(new Rect(Point.Zero, finalSize));
        return finalSize;
    }
}
