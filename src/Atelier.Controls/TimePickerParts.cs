using System;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// The hour or minute box of a time picker: in the dial mode a selector showing the value in large digits (MD3 time
/// selector), in the input mode an editable two-digit field (MD3 time input field).
/// </summary>
/// <remarks>
/// <para>
/// As a selector, a click, Space or Enter raises <see cref="Activated"/>; the time picker then shows this part on the
/// dial. As an input (<see cref="IsEditable"/>), typing digits replaces the text on the first key after focusing and
/// then appends, up to two digits; <see cref="AcceptText"/> can reject a text (for example an hour of 13 on a 12-hour
/// clock), in which case the typed digit starts over. Backspace removes a digit and Up/Down raise
/// <see cref="StepRequested"/>.
/// </para>
/// </remarks>
public class TimePickerSegment : Control
{
    /// <summary>Identifies the <see cref="Text"/> property.</summary>
    public static readonly BindableProperty<string> TextProperty =
        BindableProperty.Register<TimePickerSegment, string>(nameof(Text), "00", options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="IsSelected"/> property.</summary>
    public static readonly BindableProperty<bool> IsSelectedProperty =
        BindableProperty.Register<TimePickerSegment, bool>(nameof(IsSelected), false, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="IsEditable"/> property.</summary>
    public static readonly BindableProperty<bool> IsEditableProperty =
        BindableProperty.Register<TimePickerSegment, bool>(nameof(IsEditable), false, options: PropertyOptions.AffectsRender | PropertyOptions.AffectsMeasure);

    private bool _replaceOnType = true;

    static TimePickerSegment()
    {
        IsFocusableProperty.OverrideDefaultValue<TimePickerSegment>(true);
        CornerRadiusProperty.OverrideDefaultValue<TimePickerSegment>(new CornerRadius(8));
        CursorProperty.OverrideDefaultValue<TimePickerSegment>(CursorType.Hand);
    }

    /// <summary>Gets or sets the digits shown. The default is "00".</summary>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>Gets or sets whether the dial shows this part (dial mode). The default is <c>false</c>.</summary>
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    /// <summary>Gets or sets whether the box is an editable input field instead of a selector. The default is <c>false</c>.</summary>
    public bool IsEditable { get => GetValue(IsEditableProperty); set => SetValue(IsEditableProperty, value); }

    /// <summary>Gets or sets a check of edited texts; an edit it rejects starts over with the typed digit.</summary>
    public Func<string, bool>? AcceptText { get; set; }

    /// <summary>Occurs when the selector is clicked or activated from the keyboard.</summary>
    public event EventHandler? Activated;

    /// <summary>Occurs when the user edited the text of the input field, with the new text.</summary>
    public event EventHandler<string>? TextEdited;

    /// <summary>Occurs when Up (+1) or Down (−1) is pressed in the input field.</summary>
    public event EventHandler<int>? StepRequested;

    /// <summary>Gets whether the caret is shown: an editable box with the keyboard focus.</summary>
    public bool ShowsCaret => IsEditable && IsFocused;

    /// <inheritdoc/>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Button != PointerButtons.Left || !IsEnabled) return;

        e.Handled = true;
        Focus();
        if (!IsEditable)
        {
            Activated?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc/>
    public override void OnGotFocus()
    {
        base.OnGotFocus();
        _replaceOnType = true;
        InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void OnLostFocus()
    {
        base.OnLostFocus();
        InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEnabled) return;

        if (!IsEditable)
        {
            if (e.Key is Key.Space or Key.Enter)
            {
                Activated?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
            return;
        }

        switch (e.Key)
        {
            case Key.Backspace:
                if (Text.Length > 0)
                {
                    Edit(Text[..^1]);
                }
                _replaceOnType = false;
                e.Handled = true;
                break;
            case Key.Up:
                StepRequested?.Invoke(this, 1);
                _replaceOnType = true;
                e.Handled = true;
                break;
            case Key.Down:
                StepRequested?.Invoke(this, -1);
                _replaceOnType = true;
                e.Handled = true;
                break;
        }
    }

    /// <inheritdoc/>
    public override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (!IsEditable || !IsEnabled || string.IsNullOrEmpty(e.Text)) return;

        foreach (char c in e.Text)
        {
            if (c is < '0' or > '9') continue;
            string digit = c.ToString();
            string next = _replaceOnType || Text.Length >= 2 ? digit : Text + digit;
            if (AcceptText != null && !AcceptText(next))
            {
                next = digit;
                if (!AcceptText(next)) continue;
            }
            _replaceOnType = false;
            Edit(next);

            if (IsComplete(next))
            {
                Completed?.Invoke(this, EventArgs.Empty);
                _replaceOnType = true;
            }
        }
        e.Handled = true;
    }

    // Two digits, or one that no further digit can extend (e.g. 7 as an hour).
    private bool IsComplete(string text)
    {
        if (text.Length >= 2) return true;
        if (AcceptText == null) return false;
        for (char d = '0'; d <= '9'; d++)
        {
            if (AcceptText(text + d)) return false;
        }
        return true;
    }

    /// <summary>
    /// Occurs when typing completed the value of the input field: two digits, or one digit that can't be extended; the
    /// time picker then moves on to the next field.
    /// </summary>
    public event EventHandler? Completed;

    private void Edit(string text)
    {
        Text = text;
        TextEdited?.Invoke(this, text);
    }

    /// <inheritdoc/>
    /// <remarks>96×80 as a selector and 96×72 as an input field (MD3).</remarks>
    protected override Size MeasureOverride(Size availableSize) => new(96, IsEditable ? 72 : 80);
}

/// <summary>
/// The AM/PM selector of a 12-hour time picker: two stacked (or side-by-side) segments in an outline, the selected
/// one filled.
/// </summary>
/// <remarks>
/// Clicking a segment selects it; from the keyboard, the arrow keys and Space switch between AM and PM.
/// </remarks>
public class TimePeriodSelector : Control
{
    /// <summary>Identifies the <see cref="IsPm"/> property.</summary>
    public static readonly BindableProperty<bool> IsPmProperty =
        BindableProperty.Register<TimePeriodSelector, bool>(nameof(IsPm), false, (s, o, n) => ((TimePeriodSelector)s).IsPmChanged?.Invoke(s, n), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="Orientation"/> property.</summary>
    public static readonly BindableProperty<Layout.Orientation> OrientationProperty =
        BindableProperty.Register<TimePeriodSelector, Layout.Orientation>(nameof(Orientation), Layout.Orientation.Vertical, options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender);

    private int _hoveredSegment = -1;

    static TimePeriodSelector()
    {
        IsFocusableProperty.OverrideDefaultValue<TimePeriodSelector>(true);
        CornerRadiusProperty.OverrideDefaultValue<TimePeriodSelector>(new CornerRadius(8));
        CursorProperty.OverrideDefaultValue<TimePeriodSelector>(CursorType.Hand);
    }

    /// <summary>Initializes a selector with the current culture's AM and PM designators.</summary>
    public TimePeriodSelector()
    {
        AmText = PickerFormat.GetAmDesignator();
        PmText = PickerFormat.GetPmDesignator();
    }

    /// <summary>Gets or sets whether PM is selected. The default is <c>false</c> (AM).</summary>
    public bool IsPm { get => GetValue(IsPmProperty); set => SetValue(IsPmProperty, value); }

    /// <summary>Gets or sets whether the segments are stacked (the default) or side by side.</summary>
    public Layout.Orientation Orientation { get => GetValue(OrientationProperty); set => SetValue(OrientationProperty, value); }

    /// <summary>Gets or sets the text of the AM segment. The default is the culture's AM designator.</summary>
    public string AmText { get; set; }

    /// <summary>Gets or sets the text of the PM segment. The default is the culture's PM designator.</summary>
    public string PmText { get; set; }

    /// <summary>Gets the segment under the pointer: 0 for AM, 1 for PM, −1 for none.</summary>
    public int HoveredSegment => _hoveredSegment;

    /// <summary>Occurs when <see cref="IsPm"/> changes, with the new value.</summary>
    public event EventHandler<bool>? IsPmChanged;

    /// <summary>Gets the bounds of the AM (0) or PM (1) segment in the element's coordinates.</summary>
    public Rect GetSegmentBounds(int segment)
    {
        var size = Bounds.Size;
        return Orientation == Layout.Orientation.Vertical
            ? new Rect(0, segment * size.Height * 0.5f, size.Width, size.Height * 0.5f)
            : new Rect(segment * size.Width * 0.5f, 0, size.Width * 0.5f, size.Height);
    }

    private int SegmentAt(Point point) =>
        !new Rect(Point.Zero, Bounds.Size).Contains(point) ? -1 : GetSegmentBounds(1).Contains(point) ? 1 : 0;

    /// <inheritdoc/>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Button != PointerButtons.Left || !IsEnabled) return;

        e.Handled = true;
        Focus();
        int segment = SegmentAt(e.Position);
        if (segment >= 0) IsPm = segment == 1;
    }

    /// <inheritdoc/>
    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var origin = PointToScreen(Point.Zero);
        SetHovered(SegmentAt(new Point(e.ScreenPosition.X - origin.X, e.ScreenPosition.Y - origin.Y)));
    }

    /// <inheritdoc/>
    public override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetHovered(-1);
    }

    private void SetHovered(int segment)
    {
        if (_hoveredSegment == segment) return;
        _hoveredSegment = segment;
        InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEnabled) return;

        switch (e.Key)
        {
            case Key.Up or Key.Left: IsPm = false; break;
            case Key.Down or Key.Right: IsPm = true; break;
            case Key.Space: IsPm = !IsPm; break;
            default: return;
        }
        e.Handled = true;
    }

    /// <inheritdoc/>
    /// <remarks>52×80 stacked (MD3), 216×38 side by side; the time input uses 52×72 (set by its height).</remarks>
    protected override Size MeasureOverride(Size availableSize) =>
        Orientation == Layout.Orientation.Vertical ? new Size(52, 80) : new Size(216, 38);
}
