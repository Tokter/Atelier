using System;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// A time field: an outlined text field for typing a time, with a clock button that opens the Material Design 3 time
/// picker (<see cref="TimePickerDialog"/>) with a dial or text fields.
/// </summary>
/// <remarks>
/// <para>
/// The time shows as <c>7:05 PM</c> on a 12-hour clock or <c>19:05</c> on a 24-hour clock (<see cref="ClockFormat"/>).
/// Typed times such as <c>7:05 pm</c>, <c>7p</c>, <c>19:05</c> or <c>1905</c> are applied on Enter or when the field
/// loses focus; invalid input shows an error. Clearing the text clears the time.
/// </para>
/// <para>
/// The clock button, or Alt+Down, opens the time picker in the nearest <see cref="DialogHost"/>, with the dial or the
/// text fields (<see cref="InputMode"/>). OK applies the picked time.
/// </para>
/// </remarks>
public class TimePicker : Control
{
    /// <summary>Identifies the <see cref="SelectedTime"/> property.</summary>
    public static readonly BindableProperty<TimeOnly?> SelectedTimeProperty =
        BindableProperty.Register<TimePicker, TimeOnly?>(nameof(SelectedTime), null, (s, o, n) => ((TimePicker)s).OnSelectedTimeChanged(n));

    /// <summary>Identifies the <see cref="InputMode"/> property.</summary>
    public static readonly BindableProperty<TimePickerInputMode> InputModeProperty =
        BindableProperty.Register<TimePicker, TimePickerInputMode>(nameof(InputMode), TimePickerInputMode.Dial);

    /// <summary>Identifies the <see cref="ClockFormat"/> property.</summary>
    public static readonly BindableProperty<ClockFormat> ClockFormatProperty =
        BindableProperty.Register<TimePicker, ClockFormat>(nameof(ClockFormat), ClockFormat.Auto, (s, o, n) => ((TimePicker)s).OnClockFormatChanged());

    /// <summary>Identifies the <see cref="Label"/> property.</summary>
    public static readonly BindableProperty<string> LabelProperty =
        BindableProperty.Register<TimePicker, string>(nameof(Label), "Time", (s, o, n) => ((TimePicker)s)._textBox.Label = n);

    private readonly TextBox _textBox;
    private bool _isSyncingText;
    private TimePickerDialog? _dialog;

    /// <summary>Initializes an empty time field.</summary>
    public TimePicker()
    {
        _textBox = new TextBox { Label = "Time", TrailingIconKind = MaterialIconKind.Schedule };
        _textBox.TrailingIconClick += (_, _) => OpenPicker();
        _textBox.TextChanged += (_, _) =>
        {
            if (!_isSyncingText) Validation.SetErrors(_textBox, this, null);
        };
        _textBox.LostFocus += (_, _) => CommitText();
        _textBox.KeyDown += (_, e) =>
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
        };
        AddChild(_textBox);
        OnClockFormatChanged();
    }

    /// <summary>Gets or sets the time, or <c>null</c> for none. The default is <c>null</c>.</summary>
    public TimeOnly? SelectedTime { get => GetValue(SelectedTimeProperty); set => SetValue(SelectedTimeProperty, value); }

    /// <summary>Gets or sets whether the picker opens with the dial (the default) or the text fields.</summary>
    public TimePickerInputMode InputMode { get => GetValue(InputModeProperty); set => SetValue(InputModeProperty, value); }

    /// <summary>Gets or sets the clock: 12-hour with AM/PM, 24-hour, or the culture's (the default).</summary>
    public ClockFormat ClockFormat { get => GetValue(ClockFormatProperty); set => SetValue(ClockFormatProperty, value); }

    /// <summary>Gets or sets the field's label. The default is "Time".</summary>
    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }

    /// <summary>Gets whether the field uses a 24-hour clock.</summary>
    public bool Is24Hour => ClockFormat == ClockFormat.TwentyFourHour || (ClockFormat == ClockFormat.Auto && PickerFormat.Uses24HourClock());

    /// <summary>Occurs when <see cref="SelectedTime"/> changes, with the new time.</summary>
    public event EventHandler<TimeOnly?>? SelectedTimeChanged;

    /// <summary>Gets the text field.</summary>
    public TextBox TextBox => _textBox;

    /// <summary>Gets the open time picker dialog, or <c>null</c>.</summary>
    public TimePickerDialog? OpenDialog => _dialog;

    private void OnSelectedTimeChanged(TimeOnly? time)
    {
        ShowText(time);
        SelectedTimeChanged?.Invoke(this, time);
    }

    private void OnClockFormatChanged()
    {
        _textBox.Placeholder = Is24Hour ? "hh:mm" : $"hh:mm {PickerFormat.GetAmDesignator()}";
        ShowText(SelectedTime);
    }

    private void ShowText(TimeOnly? time)
    {
        _isSyncingText = true;
        try
        {
            _textBox.Text = time is { } t ? PickerFormat.FormatTime(t, Is24Hour) : string.Empty;
            Validation.SetErrors(_textBox, this, null);
        }
        finally
        {
            _isSyncingText = false;
        }
    }

    private void CommitText()
    {
        string text = _textBox.Text.Trim();
        if (text.Length == 0)
        {
            SelectedTime = null;
            return;
        }

        if (!PickerFormat.TryParseTime(text, out var time))
        {
            Validation.SetErrors(_textBox, this, ["Invalid time"]);
            return;
        }

        if (SelectedTime == time)
        {
            ShowText(time); // normalize, e.g. 7p -> 7:00 PM
        }
        SelectedTime = time;
    }

    /// <summary>Opens the time picker dialog, starting at the typed time if it is valid, else at <see cref="SelectedTime"/>.</summary>
    public async void OpenPicker()
    {
        if (!IsEnabled || _dialog != null) return;

        TimeOnly? start = PickerFormat.TryParseTime(_textBox.Text, out var typed) ? typed : SelectedTime;
        var dialog = new TimePickerDialog { SelectedTime = start, InputMode = InputMode, ClockFormat = ClockFormat };
        _dialog = dialog;
        try
        {
            var response = await dialog.ShowAsync(this);
            if (response.Result == DialogResult.Ok)
            {
                SelectedTime = dialog.SelectedTime;
                ShowText(SelectedTime);
            }
        }
        finally
        {
            _dialog = null;
        }
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
