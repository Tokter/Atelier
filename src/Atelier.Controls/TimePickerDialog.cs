using System;
using System.Globalization;
using System.Threading.Tasks;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>How a time picker dialog lets the user enter the time.</summary>
public enum TimePickerInputMode
{
    /// <summary>A clock dial to pick the hour and minute on (the MD3 time picker dial).</summary>
    Dial,

    /// <summary>Text fields to type the hour and minute into (the MD3 time input).</summary>
    Input,
}

/// <summary>Whether times are shown on a 12-hour clock with AM/PM or a 24-hour clock.</summary>
public enum ClockFormat
{
    /// <summary>The current culture's clock.</summary>
    Auto,

    /// <summary>A 12-hour clock with an AM/PM selector.</summary>
    TwelveHour,

    /// <summary>A 24-hour clock.</summary>
    TwentyFourHour,
}

/// <summary>
/// The Material Design 3 time picker: a dialog with the hour and minute boxes above a clock dial, or two text fields
/// to type the time, and Cancel and OK actions.
/// </summary>
/// <remarks>
/// <para>
/// In the dial mode, the hour box is selected first; picking an hour on the dial moves on to the minutes, and clicking
/// either box shows it on the dial. A 12-hour clock (<see cref="ClockFormat"/>) has an AM/PM selector; a 24-hour dial
/// shows 13–00 on an inner ring. The keyboard button at the bottom switches to the input mode and back.
/// </para>
/// <para>
/// OK (or Enter) closes the dialog with <see cref="DialogResult.Ok"/> and sets <see cref="SelectedTime"/>; Cancel or
/// Escape leave it unchanged.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// TimeOnly? time = await TimePickerDialog.PickAsync(button, vm.Alarm);
/// </code>
/// </example>
public class TimePickerDialog : Dialog
{
    /// <summary>Identifies the <see cref="SelectedTime"/> property.</summary>
    public static readonly BindableProperty<TimeOnly?> SelectedTimeProperty =
        BindableProperty.Register<TimePickerDialog, TimeOnly?>(nameof(SelectedTime), null, (s, o, n) => ((TimePickerDialog)s).ShowTime(n));

    /// <summary>Identifies the <see cref="InputMode"/> property.</summary>
    public static readonly BindableProperty<TimePickerInputMode> InputModeProperty =
        BindableProperty.Register<TimePickerDialog, TimePickerInputMode>(nameof(InputMode), TimePickerInputMode.Dial, (s, o, n) => ((TimePickerDialog)s).OnInputModeChanged());

    /// <summary>Identifies the <see cref="ClockFormat"/> property.</summary>
    public static readonly BindableProperty<ClockFormat> ClockFormatProperty =
        BindableProperty.Register<TimePickerDialog, ClockFormat>(nameof(ClockFormat), ClockFormat.Auto, (s, o, n) => ((TimePickerDialog)s).Sync());

    private readonly TextBlock _title = new() { StyleKey = PickerStyleKeys.Title, Margin = new Thickness(0, 0, 0, 20) };
    private readonly TimePickerSegment _hourBox = new();
    private readonly TimePickerSegment _minuteBox = new();
    private readonly TextBlock _separator = new() { Text = ":", StyleKey = PickerStyleKeys.Headline, Width = 24, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TimePeriodSelector _period = new() { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBlock _hourLabel = new() { Text = "Hour", StyleKey = PickerStyleKeys.Title, Margin = new Thickness(0, 7, 0, 0) };
    private readonly TextBlock _minuteLabel = new() { Text = "Minute", StyleKey = PickerStyleKeys.Title, Margin = new Thickness(0, 7, 0, 0) };
    private readonly ClockDial _dial = new() { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 36, 0, 0) };
    private readonly Button _modeButton;
    private readonly Icon _modeIcon = new(MaterialIconKind.Keyboard, 24);
    private readonly Button _cancelButton = new("Cancel") { Variant = ButtonVariant.Text };
    private readonly Button _okButton = new("OK") { Variant = ButtonVariant.Text };

    private int _hour = 12;
    private int _minute;
    private bool _editingMinutes;
    private bool _isSyncing;

    /// <summary>Initializes a time picker dialog showing the dial.</summary>
    public TimePickerDialog()
    {
        Padding = new Thickness(24, 24, 24, 20);
        Width = 328;

        _hourBox.Activated += (_, _) => ShowOnDial(minutes: false);
        _minuteBox.Activated += (_, _) => ShowOnDial(minutes: true);
        _hourBox.AcceptText = text => IsValidHourText(text);
        _minuteBox.AcceptText = text => int.TryParse(text, out int m) && (text.Length < 2 || m <= 59);
        _hourBox.TextEdited += (_, text) => OnHourTyped(text);
        _minuteBox.TextEdited += (_, text) =>
        {
            if (int.TryParse(text, out int m) && m <= 59) SetTime(_hour, m, _minuteBox);
        };
        _hourBox.Completed += (_, _) => _minuteBox.Focus();
        _hourBox.LostFocus += (_, _) => Sync(); // "5" -> "05"
        _minuteBox.LostFocus += (_, _) => Sync();
        _hourBox.StepRequested += (_, delta) => SetTime(Is24Hour ? (_hour + delta + 24) % 24 : StepHour12(delta), _minute);
        _minuteBox.StepRequested += (_, delta) => SetTime(_hour, (_minute + delta + 60) % 60);

        _period.IsPmChanged += (_, pm) =>
        {
            if (!_isSyncing) SetTime(_hour % 12 + (pm ? 12 : 0), _minute);
        };

        _dial.ValueChanged += (_, value) =>
        {
            if (_isSyncing) return;
            if (_dial.Mode == ClockDialMode.Minutes) SetTime(_hour, value);
            else SetTime(value, _minute);
        };
        _dial.SelectionCompleted += (_, _) =>
        {
            if (_dial.Mode == ClockDialMode.Hours) ShowOnDial(minutes: true);
        };

        _modeButton = new Button
        {
            Variant = ButtonVariant.Text,
            Content = _modeIcon,
            Padding = new Thickness(12),
            Width = 48,
            Height = 48,
            MinWidth = 48,
            CornerRadius = new CornerRadius(24),
            Margin = new Thickness(-12, 0, 0, 0),
            StyleKey = PickerStyleKeys.HeaderButton,
        };
        _modeButton.Click += (_, _) => InputMode = InputMode == TimePickerInputMode.Dial ? TimePickerInputMode.Input : TimePickerInputMode.Dial;

        _cancelButton.Click += (_, _) => Close(DialogResult.Cancel);
        _okButton.Click += (_, _) => Confirm();

        var hourColumn = new StackPanel().WithChildren(_hourBox, _hourLabel);
        var minuteColumn = new StackPanel().WithChildren(_minuteBox, _minuteLabel);
        var selector = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        selector.WithChildren(hourColumn, _separator, minuteColumn, _period);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        actions.WithChildren(_cancelButton, _okButton);
        var bottom = new Grid { Margin = new Thickness(0, 24, 0, 0) };
        bottom.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        bottom.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Grid.SetColumn(actions, 1);
        bottom.Add(_modeButton);
        bottom.Add(actions);
        _modeButton.VerticalAlignment = actions.VerticalAlignment = VerticalAlignment.Center;

        Content = new StackPanel().WithChildren(_title, selector, _dial, bottom);

        ShowTime(null); // starts at the current time
        OnInputModeChanged();
        Opened += (_, _) => (InputMode == TimePickerInputMode.Input ? _hourBox : (UIElement)_dial).Focus();
    }

    /// <summary>
    /// Gets or sets the time: the initial choice when the dialog opens, and the picked time after OK. The default is
    /// <c>null</c>, which starts at the current time.
    /// </summary>
    public TimeOnly? SelectedTime { get => GetValue(SelectedTimeProperty); set => SetValue(SelectedTimeProperty, value); }

    /// <summary>Gets or sets whether the dialog shows the dial or the text fields. The default is the dial.</summary>
    public TimePickerInputMode InputMode { get => GetValue(InputModeProperty); set => SetValue(InputModeProperty, value); }

    /// <summary>Gets or sets the clock: 12-hour with AM/PM, 24-hour, or the culture's (the default).</summary>
    public ClockFormat ClockFormat { get => GetValue(ClockFormatProperty); set => SetValue(ClockFormatProperty, value); }

    /// <summary>Gets whether the dialog uses a 24-hour clock.</summary>
    public bool Is24Hour => ClockFormat == ClockFormat.TwentyFourHour || (ClockFormat == ClockFormat.Auto && PickerFormat.Uses24HourClock());

    /// <summary>Gets the time currently shown (not yet confirmed).</summary>
    public TimeOnly CurrentTime => new(_hour, _minute);

    /// <summary>Gets whether the dial shows the minutes (otherwise the hours).</summary>
    public bool IsEditingMinutes => _editingMinutes;

    /// <summary>Gets the dial.</summary>
    public ClockDial Dial => _dial;

    /// <summary>Gets the hour box.</summary>
    public TimePickerSegment HourBox => _hourBox;

    /// <summary>Gets the minute box.</summary>
    public TimePickerSegment MinuteBox => _minuteBox;

    /// <summary>Gets the AM/PM selector (hidden on a 24-hour clock).</summary>
    public TimePeriodSelector PeriodSelector => _period;

    /// <summary>
    /// Shows a time picker dialog in the <see cref="DialogHost"/> of <paramref name="visualContext"/> and returns the
    /// picked time, or <c>null</c> when it was canceled.
    /// </summary>
    /// <param name="visualContext">An element inside the host, such as the control that opens the picker.</param>
    /// <param name="initialTime">The time shown when the dialog opens; <c>null</c> starts at the current time.</param>
    /// <param name="inputMode">Whether the dialog starts with the dial or the text fields.</param>
    /// <param name="clockFormat">The clock to use.</param>
    public static async Task<TimeOnly?> PickAsync(UIElement visualContext, TimeOnly? initialTime = null,
        TimePickerInputMode inputMode = TimePickerInputMode.Dial, ClockFormat clockFormat = ClockFormat.Auto)
    {
        var dialog = new TimePickerDialog { SelectedTime = initialTime, InputMode = inputMode, ClockFormat = clockFormat };
        var response = await dialog.ShowAsync(visualContext);
        return response.Result == DialogResult.Ok ? dialog.SelectedTime : null;
    }

    private void ShowTime(TimeOnly? time)
    {
        var t = time ?? TimeOnly.FromDateTime(Calendar.TimeProvider.GetLocalNow().DateTime);
        _hour = t.Hour;
        _minute = t.Minute;
        Sync();
    }

    private void SetTime(int hour, int minute, object? source = null)
    {
        _hour = hour;
        _minute = minute;
        Sync(source);
    }

    private int StepHour12(int delta)
    {
        int half = _hour >= 12 ? 12 : 0;
        return half + ((_hour % 12 + delta) % 12 + 12) % 12;
    }

    private bool IsValidHourText(string text)
    {
        if (!int.TryParse(text, out int h)) return false;
        if (text.Length < 2) return true; // a first digit, e.g. the 0 of 07
        return Is24Hour ? h <= 23 : h is >= 1 and <= 12;
    }

    private void OnHourTyped(string text)
    {
        if (!int.TryParse(text, out int h)) return;
        if (Is24Hour)
        {
            if (h <= 23) SetTime(h, _minute, _hourBox);
        }
        else if (h is >= 1 and <= 12)
        {
            SetTime(h % 12 + (_hour >= 12 ? 12 : 0), _minute, _hourBox);
        }
    }

    private void ShowOnDial(bool minutes)
    {
        _editingMinutes = minutes;
        Sync();
    }

    private void OnInputModeChanged()
    {
        bool input = InputMode == TimePickerInputMode.Input;
        _hourBox.IsEditable = _minuteBox.IsEditable = input;
        _hourLabel.Visibility = _minuteLabel.Visibility = input ? Visibility.Visible : Visibility.Collapsed;
        _dial.Visibility = input ? Visibility.Collapsed : Visibility.Visible;
        _title.Text = input ? "Enter time" : "Select time";
        _separator.FontSize = input ? 45 : 57;
        _separator.Height = input ? 72 : 80;
        _period.Height = input ? 72 : 80;
        _modeIcon.Kind = input ? MaterialIconKind.Schedule : MaterialIconKind.Keyboard;
        ToolTipService.SetToolTip(_modeButton, input ? "Switch to clock input" : "Switch to text input");
        Sync();

        if (IsOpen)
        {
            (input ? _hourBox : (UIElement)_dial).Focus();
        }
    }

    // Shows the current time in every part, except in a box being typed in.
    private void Sync(object? source = null)
    {
        _isSyncing = true;
        try
        {
            var culture = CultureInfo.CurrentCulture;
            bool is24 = Is24Hour;
            int displayHour = is24 ? _hour : (_hour % 12 == 0 ? 12 : _hour % 12);
            if (source != _hourBox) _hourBox.Text = displayHour.ToString("00", culture);
            if (source != _minuteBox) _minuteBox.Text = _minute.ToString("00", culture);

            bool dial = InputMode == TimePickerInputMode.Dial;
            _hourBox.IsSelected = dial && !_editingMinutes;
            _minuteBox.IsSelected = dial && _editingMinutes;

            _period.Visibility = is24 ? Visibility.Collapsed : Visibility.Visible;
            _period.IsPm = _hour >= 12;

            _dial.Is24Hour = is24;
            _dial.Mode = _editingMinutes ? ClockDialMode.Minutes : ClockDialMode.Hours;
            _dial.Value = _editingMinutes ? _minute : _hour;
        }
        finally
        {
            _isSyncing = false;
        }
    }

    /// <summary>
    /// Accepts the current time like the OK button: sets <see cref="SelectedTime"/> and closes with
    /// <see cref="DialogResult.Ok"/>.
    /// </summary>
    public void Confirm()
    {
        SelectedTime = new TimeOnly(_hour, _minute);
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
}
