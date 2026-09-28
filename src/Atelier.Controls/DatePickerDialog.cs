using System;
using System.Globalization;
using System.Threading.Tasks;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>How a date picker dialog lets the user enter the date.</summary>
public enum DatePickerInputMode
{
    /// <summary>A calendar to pick the day from (the MD3 modal date picker).</summary>
    Calendar,

    /// <summary>A text field to type the date into (the MD3 modal date input).</summary>
    Input,
}

/// <summary>
/// The Material Design 3 modal date picker: a dialog with a calendar, or a text field to type the date, and Cancel and
/// OK actions.
/// </summary>
/// <remarks>
/// <para>
/// The header shows the title (<see cref="HeaderText"/>, "Select date") and the chosen date as the headline, with a
/// button that switches between the calendar and the text field. OK (or Enter) closes the dialog with
/// <see cref="DialogResult.Ok"/> and sets <see cref="SelectedDate"/>; Cancel or Escape leave it unchanged. Typed dates
/// are checked on OK: an invalid or out-of-range date shows an error instead of closing.
/// </para>
/// <para>
/// Show it with <see cref="Dialog.ShowAsync(UIElement)"/> like any dialog, or use <see cref="PickAsync"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// DateOnly? date = await DatePickerDialog.PickAsync(button, vm.Birthday);
/// if (date != null) vm.Birthday = date;
/// </code>
/// </example>
public class DatePickerDialog : Dialog
{
    /// <summary>Identifies the <see cref="SelectedDate"/> property.</summary>
    public static readonly BindableProperty<DateOnly?> SelectedDateProperty =
        BindableProperty.Register<DatePickerDialog, DateOnly?>(nameof(SelectedDate), null, (s, o, n) => ((DatePickerDialog)s).ShowDate(n));

    /// <summary>Identifies the <see cref="InputMode"/> property.</summary>
    public static readonly BindableProperty<DatePickerInputMode> InputModeProperty =
        BindableProperty.Register<DatePickerDialog, DatePickerInputMode>(nameof(InputMode), DatePickerInputMode.Calendar, (s, o, n) => ((DatePickerDialog)s).OnInputModeChanged());

    /// <summary>Identifies the <see cref="HeaderText"/> property.</summary>
    public static readonly BindableProperty<string> HeaderTextProperty =
        BindableProperty.Register<DatePickerDialog, string>(nameof(HeaderText), "Select date", (s, o, n) => ((DatePickerDialog)s)._title.Text = n);

    /// <summary>Identifies the <see cref="DateFormat"/> property.</summary>
    public static readonly BindableProperty<string?> DateFormatProperty =
        BindableProperty.Register<DatePickerDialog, string?>(nameof(DateFormat), null, (s, o, n) => ((DatePickerDialog)s).OnDateFormatChanged());

    private readonly TextBlock _title = new() { Text = "Select date", StyleKey = PickerStyleKeys.Title };
    private readonly TextBlock _headline = new() { StyleKey = PickerStyleKeys.Headline, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Button _modeButton;
    private readonly Icon _modeIcon = new(MaterialIconKind.Edit, 24);
    private readonly Calendar _calendar = new() { Margin = new Thickness(12, 0) };
    private readonly TextBox _input;
    private readonly Border _inputArea;
    private readonly Button _cancelButton = new("Cancel") { Variant = ButtonVariant.Text };
    private readonly Button _okButton = new("OK") { Variant = ButtonVariant.Text };

    /// <summary>Initializes a date picker dialog showing the calendar.</summary>
    public DatePickerDialog()
    {
        Padding = Thickness.Zero;
        Width = 360;

        _modeButton = new Button
        {
            Variant = ButtonVariant.Text,
            Content = _modeIcon,
            Padding = new Thickness(8),
            Width = 48,
            Height = 48,
            MinWidth = 48,
            CornerRadius = new CornerRadius(24),
            VerticalAlignment = VerticalAlignment.Center,
            StyleKey = PickerStyleKeys.HeaderButton,
        };
        _modeButton.Click += (_, _) => InputMode = InputMode == DatePickerInputMode.Calendar ? DatePickerInputMode.Input : DatePickerInputMode.Calendar;

        var headlineRow = new Grid();
        headlineRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        headlineRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        Grid.SetColumn(_modeButton, 1);
        headlineRow.Add(_headline);
        headlineRow.Add(_modeButton);

        var header = new StackPanel { Margin = new Thickness(24, 16, 12, 12), Spacing = 28 };
        header.WithChildren(_title, headlineRow);

        _input = new TextBox { Label = "Date", Margin = new Thickness(0) };
        _input.TextChanged += (_, _) => Validation.SetErrors(_input, this, null);
        _inputArea = new Border { Padding = new Thickness(24, 16, 24, 8), Child = _input, Visibility = Visibility.Collapsed };

        _calendar.SelectedDateChanged += (_, _) => UpdateHeadline();

        _cancelButton.Click += (_, _) => Close(DialogResult.Cancel);
        _okButton.Click += (_, _) => Confirm();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 8, 12, 12) };
        actions.WithChildren(_cancelButton, _okButton);

        var root = new StackPanel();
        root.WithChildren(header, new Border { Height = 1, StyleKey = PickerStyleKeys.Divider }, _calendar, _inputArea, actions);
        Content = root;

        OnDateFormatChanged();
        OnInputModeChanged();
        Opened += (_, _) => FocusInitialElement();
    }

    /// <summary>
    /// Gets or sets the date: the initial choice when the dialog opens, and the picked date after OK. The default is
    /// <c>null</c> (none).
    /// </summary>
    public DateOnly? SelectedDate { get => GetValue(SelectedDateProperty); set => SetValue(SelectedDateProperty, value); }

    /// <summary>Gets or sets whether the dialog shows the calendar or the text field. The default is the calendar.</summary>
    public DatePickerInputMode InputMode { get => GetValue(InputModeProperty); set => SetValue(InputModeProperty, value); }

    /// <summary>Gets or sets the small title above the headline. The default is "Select date".</summary>
    public string HeaderText { get => GetValue(HeaderTextProperty); set => SetValue(HeaderTextProperty, value); }

    /// <summary>
    /// Gets or sets the pattern of typed dates, such as <c>dd.MM.yyyy</c>; <c>null</c> (the default) uses the culture's
    /// numeric short date (see <see cref="PickerFormat.GetDatePattern"/>).
    /// </summary>
    public string? DateFormat { get => GetValue(DateFormatProperty); set => SetValue(DateFormatProperty, value); }

    /// <summary>Gets or sets the earliest date that can be picked. The default is January 1, 1900.</summary>
    public DateOnly MinDate { get => _calendar.MinDate; set => _calendar.MinDate = value; }

    /// <summary>Gets or sets the latest date that can be picked. The default is December 31, 2100.</summary>
    public DateOnly MaxDate { get => _calendar.MaxDate; set => _calendar.MaxDate = value; }

    /// <summary>Gets or sets the first day of the calendar's weeks. The default comes from the current culture.</summary>
    public DayOfWeek FirstDayOfWeek { get => _calendar.FirstDayOfWeek; set => _calendar.FirstDayOfWeek = value; }

    /// <summary>Gets the calendar of the dialog.</summary>
    public Calendar Calendar => _calendar;

    /// <summary>Gets the text field of the input mode.</summary>
    public TextBox InputBox => _input;

    /// <summary>Gets the OK button.</summary>
    public Button OkButton => _okButton;

    private string Pattern => DateFormat ?? PickerFormat.GetDatePattern();

    /// <summary>
    /// Shows a date picker dialog in the <see cref="DialogHost"/> of <paramref name="visualContext"/> and returns the
    /// picked date, or <c>null</c> when it was canceled.
    /// </summary>
    /// <param name="visualContext">An element inside the host, such as the control that opens the picker.</param>
    /// <param name="initialDate">The date selected when the dialog opens.</param>
    /// <param name="inputMode">Whether the dialog starts with the calendar or the text field.</param>
    public static async Task<DateOnly?> PickAsync(UIElement visualContext, DateOnly? initialDate = null, DatePickerInputMode inputMode = DatePickerInputMode.Calendar)
    {
        var dialog = new DatePickerDialog { SelectedDate = initialDate, InputMode = inputMode };
        var response = await dialog.ShowAsync(visualContext);
        return response.Result == DialogResult.Ok ? dialog.SelectedDate : null;
    }

    private void ShowDate(DateOnly? date)
    {
        _calendar.SelectedDate = date;
        if (date is { } d) _calendar.DisplayDate = d;
        _input.Text = date?.ToString(Pattern, CultureInfo.CurrentCulture) ?? string.Empty;
        Validation.SetErrors(_input, this, null);
        UpdateHeadline();
    }

    private void OnDateFormatChanged()
    {
        _input.Placeholder = PickerFormat.GetDatePlaceholder(Pattern);
        _input.SupportingText = PickerFormat.GetDatePlaceholder(Pattern);
        _input.Text = SelectedDate?.ToString(Pattern, CultureInfo.CurrentCulture) ?? string.Empty;
    }

    private void OnInputModeChanged()
    {
        bool calendar = InputMode == DatePickerInputMode.Calendar;
        if (calendar)
        {
            // Carry a valid typed date over to the calendar.
            if (TryReadInput(out var typed, out _) && typed is { } d)
            {
                _calendar.SelectedDate = d;
            }
        }
        else
        {
            _input.Text = _calendar.SelectedDate?.ToString(Pattern, CultureInfo.CurrentCulture) ?? string.Empty;
            Validation.SetErrors(_input, this, null);
        }

        _calendar.Visibility = calendar ? Visibility.Visible : Visibility.Collapsed;
        _inputArea.Visibility = calendar ? Visibility.Collapsed : Visibility.Visible;
        _modeIcon.Kind = calendar ? MaterialIconKind.Edit : MaterialIconKind.CalendarToday;
        ToolTipService.SetToolTip(_modeButton, calendar ? "Switch to text input" : "Switch to calendar");
        UpdateHeadline();

        if (!calendar && IsOpen)
        {
            _input.Focus();
            _input.SelectAll();
        }
    }

    private void UpdateHeadline()
    {
        bool calendar = InputMode == DatePickerInputMode.Calendar;
        _headline.Text = calendar
            ? _calendar.SelectedDate?.ToString("ddd, MMM d", CultureInfo.CurrentCulture) ?? "Selected date"
            : "Enter date";
    }

    // Reads the text field: an empty text is no date; otherwise a valid date within the range.
    private bool TryReadInput(out DateOnly? date, out string? error)
    {
        date = null;
        error = null;
        string text = _input.Text.Trim();
        if (text.Length == 0) return true;

        if (!PickerFormat.TryParseDate(text, Pattern, out var parsed))
        {
            error = $"Invalid format. Use {PickerFormat.GetDatePlaceholder(Pattern)}";
            return false;
        }
        if (parsed < MinDate || parsed > MaxDate)
        {
            error = "Date out of range";
            return false;
        }
        date = parsed;
        return true;
    }

    /// <summary>
    /// Accepts the current choice like the OK button: sets <see cref="SelectedDate"/> and closes with
    /// <see cref="DialogResult.Ok"/>. In the input mode an invalid date shows an error instead.
    /// </summary>
    public void Confirm()
    {
        DateOnly? date;
        if (InputMode == DatePickerInputMode.Input)
        {
            if (!TryReadInput(out date, out string? error))
            {
                Validation.SetErrors(_input, this, [error!]);
                _input.Focus();
                return;
            }
        }
        else
        {
            date = _calendar.SelectedDate;
        }

        SelectedDate = date;
        Close(DialogResult.Ok);
    }

    /// <inheritdoc/>
    /// <remarks>Enter confirms like OK; Escape cancels (see <see cref="Dialog.CloseOnEscape"/>).</remarks>
    public override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Enter && e.Source is not ButtonBase)
        {
            Confirm();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    // Starts at the selected day (or today), or in the text field, so the keyboard works at once.
    private void FocusInitialElement()
    {
        if (InputMode == DatePickerInputMode.Calendar)
        {
            _calendar.MoveFocusTo(_calendar.FocusDate);
        }
        else
        {
            _input.Focus();
            _input.SelectAll();
        }
    }
}
