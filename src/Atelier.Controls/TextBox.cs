using System;
using Atelier.Core.Animation;
using Atelier.Core.Events;
using Atelier.Core.Platform;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Rendering;

namespace Atelier.Controls;

public class TextBox : Control
{
    public static readonly BindableProperty<string> TextProperty =
        BindableProperty.Register<TextBox, string>(
            nameof(Text),
            string.Empty,
            (s, o, n) => ((TextBox)s).OnTextChanged(o, n)
        );

    public static readonly BindableProperty<string> PlaceholderProperty =
        BindableProperty.Register<TextBox, string>(
            nameof(Placeholder),
            string.Empty,
            (s, o, n) => ((TextBox)s).InvalidateVisual()
        );

    public static readonly BindableProperty<bool> IsReadOnlyProperty =
        BindableProperty.Register<TextBox, bool>(nameof(IsReadOnly), false);

    public static readonly BindableProperty<float> CaretWidthProperty =
        BindableProperty.Register<TextBox, float>(nameof(CaretWidth), 2f, (s, o, n) => ((TextBox)s).InvalidateVisual());

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public float CaretWidth
    {
        get => GetValue(CaretWidthProperty);
        set => SetValue(CaretWidthProperty, value);
    }

    private int _caretIndex = 0;
    public int CaretIndex
    {
        get => _caretIndex;
        set => SetCaretIndex(value, keepSelection: false);
    }

    public int SelectionAnchor { get; private set; } = 0;
    public int SelectionStart => Math.Min(SelectionAnchor, _caretIndex);
    public int SelectionLength => Math.Abs(_caretIndex - SelectionAnchor);
    public bool HasSelection => SelectionLength > 0;

    public string SelectedText => HasSelection && Text.Length >= SelectionStart + SelectionLength
        ? Text.Substring(SelectionStart, SelectionLength)
        : string.Empty;

    public bool CaretVisible { get; private set; } = false;

    public event EventHandler<string>? TextChanged;

    private static AnimationClock? _clock;
    public static void SetGlobalAnimationClock(AnimationClock clock) => _clock = clock;

    private ulong _lastClickTime = 0;
    private Point _lastClickPos = Point.Zero;

    public TextBox()
    {
        IsFocusable = true;
        Padding = new Thickness(14, 12);
        CornerRadius = new CornerRadius(4);
    }

    public TextBox(string text) : this()
    {
        Text = text;
        _caretIndex = text.Length;
        SelectionAnchor = _caretIndex;
    }

    public void SetCaretIndex(int index, bool keepSelection = false)
    {
        _caretIndex = Math.Clamp(index, 0, Text.Length);
        if (!keepSelection)
        {
            SelectionAnchor = _caretIndex;
        }
        InvalidateVisual();
    }

    private void OnTextChanged(string oldText, string newText)
    {
        _caretIndex = Math.Clamp(_caretIndex, 0, newText.Length);
        SelectionAnchor = Math.Clamp(SelectionAnchor, 0, newText.Length);
        InvalidateMeasure();
        TextChanged?.Invoke(this, newText);
    }

    public void Select(int start, int length)
    {
        start = Math.Clamp(start, 0, Text.Length);
        length = Math.Clamp(length, 0, Text.Length - start);
        SelectionAnchor = start;
        _caretIndex = start + length;
        InvalidateVisual();
    }

    public void SelectAll()
    {
        SelectionAnchor = 0;
        _caretIndex = Text.Length;
        InvalidateVisual();
    }

    public void ClearSelection()
    {
        SelectionAnchor = _caretIndex;
        InvalidateVisual();
    }

    public void ReplaceSelection(string replacement)
    {
        if (HasSelection)
        {
            int start = SelectionStart;
            int len = SelectionLength;
            Text = Text.Remove(start, len).Insert(start, replacement);
            SetCaretIndex(start + replacement.Length, keepSelection: false);
        }
        else
        {
            int idx = Math.Clamp(_caretIndex, 0, Text.Length);
            Text = Text.Insert(idx, replacement);
            SetCaretIndex(idx + replacement.Length, keepSelection: false);
        }

        CaretVisible = true;
        InvalidateVisual();
    }

    public void Copy()
    {
        if (HasSelection)
        {
            Clipboard.SetText(SelectedText);
        }
    }

    public void Cut()
    {
        if (HasSelection && !IsReadOnly)
        {
            Clipboard.SetText(SelectedText);
            ReplaceSelection(string.Empty);
        }
    }

    public void Paste()
    {
        if (!IsReadOnly)
        {
            string? clip = Clipboard.GetText();
            if (!string.IsNullOrEmpty(clip))
            {
                ReplaceSelection(clip);
            }
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
        Focus();
        CapturePointer();
        CaretVisible = true;

        float localX = e.Position.X - Padding.Left;
        int idx = EstimateCaretIndex(localX);

        bool isDoubleClick = (e.TimestampMs > 0 && e.TimestampMs - _lastClickTime < 350)
            || (_lastClickTime > 0 && MathF.Abs(e.Position.X - _lastClickPos.X) < 6);

        if (isDoubleClick)
        {
            SelectWord(idx);
        }
        else
        {
            SetCaretIndex(idx, keepSelection: false);
        }

        _lastClickTime = e.TimestampMs;
        _lastClickPos = e.Position;
        InvalidateVisual();
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (IsPointerCaptured)
        {
            float localX = e.Position.X - Padding.Left;
            int idx = EstimateCaretIndex(localX);
            SetCaretIndex(idx, keepSelection: true);
            CaretVisible = true;
        }
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (IsPointerCaptured)
        {
            ReleasePointerCapture();
            InvalidateVisual();
        }
    }

    private void SelectWord(int charIndex)
    {
        if (string.IsNullOrEmpty(Text)) return;

        charIndex = Math.Clamp(charIndex, 0, Text.Length);

        // Find word boundary to the left
        int start = charIndex;
        while (start > 0 && !char.IsWhiteSpace(Text[start - 1]) && !char.IsPunctuation(Text[start - 1]))
        {
            start--;
        }

        // Find word boundary to the right
        int end = charIndex;
        while (end < Text.Length && !char.IsWhiteSpace(Text[end]) && !char.IsPunctuation(Text[end]))
        {
            end++;
        }

        if (start < end)
        {
            Select(start, end - start);
        }
        else
        {
            SetCaretIndex(charIndex, keepSelection: false);
        }
    }

    private int EstimateCaretIndex(float localX)
    {
        if (string.IsNullOrEmpty(Text) || localX <= 0) return 0;

        float prevWidth = 0;
        for (int i = 1; i <= Text.Length; i++)
        {
            var substring = Text[..i];
            var size = TextMeasurer.Measure(substring, FontSize, FontFamily);
            float curWidth = size.Width;

            if (localX <= curWidth)
            {
                float distPrev = MathF.Abs(localX - prevWidth);
                float distCur = MathF.Abs(localX - curWidth);
                return distPrev < distCur ? i - 1 : i;
            }
            prevWidth = curWidth;
        }

        return Text.Length;
    }

    public override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (IsReadOnly || !IsEnabled || !IsFocused || string.IsNullOrEmpty(e.Text))
            return;

        ReplaceSelection(e.Text);
        e.Handled = true;
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsEnabled || !IsFocused)
            return;

        bool shift = e.Modifiers.HasFlag(ModifierKeys.Shift);
        bool ctrl = e.Modifiers.HasFlag(ModifierKeys.Control);

        // Clipboard & Selection shortcuts
        if (ctrl)
        {
            if (e.Key == Key.A)
            {
                SelectAll();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.C)
            {
                Copy();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.X)
            {
                Cut();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.V)
            {
                Paste();
                e.Handled = true;
                return;
            }
        }

        if (IsReadOnly)
        {
            // Allow navigation and copy while ReadOnly
            if (e.Key is Key.Left or Key.Right or Key.Home or Key.End)
            {
                HandleNavigationKey(e.Key, shift);
                e.Handled = true;
                InvalidateVisual();
            }
            return;
        }

        bool handled = true;
        switch (e.Key)
        {
            case Key.Backspace:
                if (HasSelection)
                {
                    ReplaceSelection(string.Empty);
                }
                else if (_caretIndex > 0 && Text.Length > 0)
                {
                    int delIdx = _caretIndex - 1;
                    Text = Text.Remove(delIdx, 1);
                    SetCaretIndex(delIdx, keepSelection: false);
                }
                break;

            case Key.Delete:
                if (HasSelection)
                {
                    ReplaceSelection(string.Empty);
                }
                else if (_caretIndex < Text.Length)
                {
                    Text = Text.Remove(_caretIndex, 1);
                    SetCaretIndex(_caretIndex, keepSelection: false);
                }
                break;

            case Key.Left:
            case Key.Right:
            case Key.Home:
            case Key.End:
                HandleNavigationKey(e.Key, shift);
                break;

            default:
                // Fallback check on KeyCode
                if (e.KeyCode == 8) // Backspace
                {
                    if (HasSelection)
                    {
                        ReplaceSelection(string.Empty);
                    }
                    else if (_caretIndex > 0 && Text.Length > 0)
                    {
                        int delIdx = _caretIndex - 1;
                        Text = Text.Remove(delIdx, 1);
                        SetCaretIndex(delIdx, keepSelection: false);
                    }
                }
                else if (e.KeyCode == 46) // Delete
                {
                    if (HasSelection)
                    {
                        ReplaceSelection(string.Empty);
                    }
                    else if (_caretIndex < Text.Length)
                    {
                        Text = Text.Remove(_caretIndex, 1);
                        SetCaretIndex(_caretIndex, keepSelection: false);
                    }
                }
                else
                {
                    handled = false;
                }
                break;
        }

        if (handled)
        {
            CaretVisible = true;
            e.Handled = true;
            InvalidateVisual();
        }
    }

    private void HandleNavigationKey(Key key, bool shift)
    {
        switch (key)
        {
            case Key.Left:
                if (shift)
                {
                    if (_caretIndex > 0) SetCaretIndex(_caretIndex - 1, keepSelection: true);
                }
                else
                {
                    if (HasSelection)
                    {
                        SetCaretIndex(SelectionStart, keepSelection: false);
                    }
                    else if (_caretIndex > 0)
                    {
                        SetCaretIndex(_caretIndex - 1, keepSelection: false);
                    }
                }
                break;

            case Key.Right:
                if (shift)
                {
                    if (_caretIndex < Text.Length) SetCaretIndex(_caretIndex + 1, keepSelection: true);
                }
                else
                {
                    if (HasSelection)
                    {
                        SetCaretIndex(SelectionStart + SelectionLength, keepSelection: false);
                    }
                    else if (_caretIndex < Text.Length)
                    {
                        SetCaretIndex(_caretIndex + 1, keepSelection: false);
                    }
                }
                break;

            case Key.Home:
                SetCaretIndex(0, keepSelection: shift);
                break;

            case Key.End:
                SetCaretIndex(Text.Length, keepSelection: shift);
                break;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        string displayText = !string.IsNullOrEmpty(Text) ? Text : (!string.IsNullOrEmpty(Placeholder) ? Placeholder : " ");
        var textSize = TextMeasurer.Measure(displayText, FontSize, FontFamily);
        return new Size(
            Math.Max(120, textSize.Width + padding.Horizontal + 4),
            Math.Max(40, textSize.Height + padding.Vertical)
        );
    }
}
