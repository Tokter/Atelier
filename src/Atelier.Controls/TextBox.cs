using System;
using Atelier.Core.Animation;
using Atelier.Core.Events;
using Atelier.Core.Platform;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Rendering;

namespace Atelier.Controls;

public enum TextBoxVariant
{
    Outlined,
    Filled
}

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

    public static readonly BindableProperty<TextBoxVariant> VariantProperty =
        BindableProperty.Register<TextBox, TextBoxVariant>(
            nameof(Variant),
            TextBoxVariant.Outlined,
            (s, o, n) => ((TextBox)s).InvalidateVisual()
        );

    public static readonly BindableProperty<string> LabelProperty =
        BindableProperty.Register<TextBox, string>(
            nameof(Label),
            string.Empty,
            (s, o, n) => ((TextBox)s).OnLabelChanged(o, n)
        );

    public static readonly BindableProperty<MaterialIconKind> LeadingIconKindProperty =
        BindableProperty.Register<TextBox, MaterialIconKind>(
            nameof(LeadingIconKind),
            MaterialIconKind.None,
            (s, o, n) => { ((TextBox)s).InvalidateMeasure(); ((TextBox)s).InvalidateVisual(); }
        );

    public static readonly BindableProperty<string> SupportingTextProperty =
        BindableProperty.Register<TextBox, string>(
            nameof(SupportingText),
            string.Empty,
            (s, o, n) => { ((TextBox)s).InvalidateMeasure(); ((TextBox)s).InvalidateVisual(); }
        );

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

    public TextBoxVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public MaterialIconKind LeadingIconKind
    {
        get => GetValue(LeadingIconKindProperty);
        set => SetValue(LeadingIconKindProperty, value);
    }

    public string SupportingText
    {
        get => GetValue(SupportingTextProperty);
        set => SetValue(SupportingTextProperty, value);
    }

    public bool HasLabel => !string.IsNullOrEmpty(Label);
    public bool HasLeadingIcon => LeadingIconKind != MaterialIconKind.None;
    public bool HasSupportingText => !string.IsNullOrEmpty(SupportingText);

    public float LabelAnimationProgress { get; private set; } = 0f;

    public float ScrollOffset { get; private set; } = 0f;

    public float GetTextContentStartX() => Padding.Left + (HasLeadingIcon ? 32f : 0f);

    public float GetViewportWidth()
    {
        float right = Bounds.Width - Padding.Right;
        float left = GetTextContentStartX();
        return Math.Max(0f, right - left);
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
        Padding = new Thickness(16, 8);
        CornerRadius = new CornerRadius(4);
    }

    public TextBox(string text) : this()
    {
        Text = text;
        _caretIndex = text.Length;
        SelectionAnchor = _caretIndex;
        if (!string.IsNullOrEmpty(text))
        {
            LabelAnimationProgress = 1f;
        }
        EnsureCaretVisible();
    }

    public void SetCaretIndex(int index, bool keepSelection = false)
    {
        _caretIndex = Math.Clamp(index, 0, Text.Length);
        if (!keepSelection)
        {
            SelectionAnchor = _caretIndex;
        }
        EnsureCaretVisible();
        InvalidateVisual();
    }

    public override void OnGotFocus()
    {
        base.OnGotFocus();
        UpdateLabelAnimation(animate: true);
        CaretVisible = true;
        InvalidateVisual();
    }

    public override void OnLostFocus()
    {
        base.OnLostFocus();
        UpdateLabelAnimation(animate: true);
        CaretVisible = false;
        ClearSelection();
        InvalidateVisual();
    }

    private void OnLabelChanged(string oldLabel, string newLabel)
    {
        UpdateLabelAnimation(animate: false);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void UpdateLabelAnimation(bool animate)
    {
        bool shouldFloat = HasLabel && (IsFocused || !string.IsNullOrEmpty(Text));
        float target = shouldFloat ? 1f : 0f;

        if (MathF.Abs(LabelAnimationProgress - target) < 0.001f)
            return;

        if (!animate || _clock == null)
        {
            LabelAnimationProgress = target;
            InvalidateVisual();
            return;
        }

        var anim = new FloatAnimation(
            LabelAnimationProgress,
            target,
            TimeSpan.FromMilliseconds(180),
            p =>
            {
                LabelAnimationProgress = p;
                InvalidateVisual();
            },
            Easing.EmphasizedDecelerate
        );

        _clock.Add(anim);
    }

    private void OnTextChanged(string oldText, string newText)
    {
        _caretIndex = Math.Clamp(_caretIndex, 0, newText.Length);
        SelectionAnchor = Math.Clamp(SelectionAnchor, 0, newText.Length);
        UpdateLabelAnimation(animate: true);
        InvalidateMeasure();
        EnsureCaretVisible();
        InvalidateVisual();
        TextChanged?.Invoke(this, newText);
    }

    public void Select(int start, int length)
    {
        start = Math.Clamp(start, 0, Text.Length);
        length = Math.Clamp(length, 0, Text.Length - start);
        SelectionAnchor = start;
        _caretIndex = start + length;
        EnsureCaretVisible();
        InvalidateVisual();
    }

    public void SelectAll()
    {
        SelectionAnchor = 0;
        _caretIndex = Text.Length;
        EnsureCaretVisible();
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

    public void EnsureCaretVisible()
    {
        float viewportWidth = GetViewportWidth();
        if (viewportWidth <= 0)
        {
            ScrollOffset = 0f;
            return;
        }

        float caretTextX = 0f;
        if (!string.IsNullOrEmpty(Text) && _caretIndex > 0)
        {
            var textBeforeCaret = Text[..Math.Min(_caretIndex, Text.Length)];
            caretTextX = TextMeasurer.Measure(textBeforeCaret, FontSize, FontFamily).Width;
        }

        float totalTextWidth = string.IsNullOrEmpty(Text) ? 0f : TextMeasurer.Measure(Text, FontSize, FontFamily).Width;
        float caretWidth = Math.Max(1f, MathF.Round(CaretWidth));
        float maxScroll = Math.Max(0f, totalTextWidth + caretWidth - viewportWidth);

        float newOffset = ScrollOffset;

        if (caretTextX < newOffset)
        {
            newOffset = caretTextX;
        }
        else if (caretTextX + caretWidth > newOffset + viewportWidth)
        {
            newOffset = caretTextX + caretWidth - viewportWidth;
        }

        newOffset = Math.Clamp(newOffset, 0f, maxScroll);
        if (MathF.Abs(newOffset - ScrollOffset) > 0.001f)
        {
            ScrollOffset = newOffset;
            InvalidateVisual();
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        if (!IsEnabled) return;

        base.OnPointerPressed(e);
        e.Handled = true;
        Focus();
        CapturePointer();
        CaretVisible = true;

        float localX = (e.Position.X - GetTextContentStartX()) + ScrollOffset;
        int idx = EstimateCaretIndex(localX);

        ulong clickTime = e.TimestampMs > 0 ? e.TimestampMs : (ulong)Environment.TickCount64;
        ulong timeDelta = clickTime >= _lastClickTime ? clickTime - _lastClickTime : ulong.MaxValue;
        float distX = MathF.Abs(e.Position.X - _lastClickPos.X);
        float distY = MathF.Abs(e.Position.Y - _lastClickPos.Y);

        bool isDoubleClick = _lastClickTime > 0 && timeDelta < 500 && distX < 10f && distY < 10f;

        if (isDoubleClick)
        {
            SelectWord(idx);
            _lastClickTime = 0;
        }
        else
        {
            SetCaretIndex(idx, keepSelection: false);
            _lastClickTime = clickTime;
            _lastClickPos = e.Position;
        }

        InvalidateVisual();
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (IsPointerCaptured)
        {
            float localX = (e.Position.X - GetTextContentStartX()) + ScrollOffset;
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

    public void SelectWord(int charIndex)
    {
        if (string.IsNullOrEmpty(Text)) return;

        charIndex = Math.Clamp(charIndex, 0, Text.Length);

        int targetIndex = charIndex;
        if (targetIndex >= Text.Length && targetIndex > 0)
        {
            targetIndex = targetIndex - 1;
        }
        else if (targetIndex < Text.Length && char.IsWhiteSpace(Text[targetIndex]) && targetIndex > 0 && !char.IsWhiteSpace(Text[targetIndex - 1]))
        {
            targetIndex = targetIndex - 1;
        }

        char targetChar = Text[targetIndex];
        int cat = GetCharCategory(targetChar);

        int start = targetIndex;
        while (start > 0 && GetCharCategory(Text[start - 1]) == cat)
        {
            start--;
        }

        int end = targetIndex + 1;
        while (end < Text.Length && GetCharCategory(Text[end]) == cat)
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

    private static int GetCharCategory(char c)
    {
        if (char.IsLetterOrDigit(c) || c == '_') return 1;
        if (char.IsWhiteSpace(c)) return 2;
        return 3;
    }

    public static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    public int FindPreviousWordBoundary(int startIndex)
    {
        if (string.IsNullOrEmpty(Text) || startIndex <= 0)
            return 0;

        int i = Math.Clamp(startIndex, 0, Text.Length);

        // 1. Skip whitespace backwards
        while (i > 0 && char.IsWhiteSpace(Text[i - 1]))
        {
            i--;
        }

        if (i <= 0)
            return 0;

        // 2. Skip word characters or non-word/non-whitespace characters backwards
        if (IsWordChar(Text[i - 1]))
        {
            while (i > 0 && IsWordChar(Text[i - 1]))
            {
                i--;
            }
        }
        else
        {
            while (i > 0 && !char.IsWhiteSpace(Text[i - 1]) && !IsWordChar(Text[i - 1]))
            {
                i--;
            }
        }

        return i;
    }

    public int FindNextWordBoundary(int startIndex)
    {
        if (string.IsNullOrEmpty(Text) || startIndex >= Text.Length)
            return string.IsNullOrEmpty(Text) ? 0 : Text.Length;

        int i = Math.Clamp(startIndex, 0, Text.Length);

        if (char.IsWhiteSpace(Text[i]))
        {
            while (i < Text.Length && char.IsWhiteSpace(Text[i]))
            {
                i++;
            }
        }
        else if (IsWordChar(Text[i]))
        {
            while (i < Text.Length && IsWordChar(Text[i]))
            {
                i++;
            }
            while (i < Text.Length && char.IsWhiteSpace(Text[i]))
            {
                i++;
            }
        }
        else
        {
            while (i < Text.Length && !char.IsWhiteSpace(Text[i]) && !IsWordChar(Text[i]))
            {
                i++;
            }
            while (i < Text.Length && char.IsWhiteSpace(Text[i]))
            {
                i++;
            }
        }

        return i;
    }

    internal int EstimateCaretIndex(float localX)
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
                HandleNavigationKey(e.Key, shift, ctrl);
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
                else if (ctrl && _caretIndex > 0)
                {
                    int prevWord = FindPreviousWordBoundary(_caretIndex);
                    int len = _caretIndex - prevWord;
                    Text = Text.Remove(prevWord, len);
                    SetCaretIndex(prevWord, keepSelection: false);
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
                else if (ctrl && _caretIndex < Text.Length)
                {
                    int nextWord = FindNextWordBoundary(_caretIndex);
                    int len = nextWord - _caretIndex;
                    Text = Text.Remove(_caretIndex, len);
                    SetCaretIndex(_caretIndex, keepSelection: false);
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
                HandleNavigationKey(e.Key, shift, ctrl);
                break;

            default:
                // Fallback check on KeyCode
                if (e.KeyCode == 8) // Backspace
                {
                    if (HasSelection)
                    {
                        ReplaceSelection(string.Empty);
                    }
                    else if (ctrl && _caretIndex > 0)
                    {
                        int prevWord = FindPreviousWordBoundary(_caretIndex);
                        int len = _caretIndex - prevWord;
                        Text = Text.Remove(prevWord, len);
                        SetCaretIndex(prevWord, keepSelection: false);
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
                    else if (ctrl && _caretIndex < Text.Length)
                    {
                        int nextWord = FindNextWordBoundary(_caretIndex);
                        int len = nextWord - _caretIndex;
                        Text = Text.Remove(_caretIndex, len);
                        SetCaretIndex(_caretIndex, keepSelection: false);
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

    private void HandleNavigationKey(Key key, bool shift, bool ctrl)
    {
        switch (key)
        {
            case Key.Left:
                if (ctrl)
                {
                    int target = FindPreviousWordBoundary(shift ? _caretIndex : (HasSelection ? SelectionStart : _caretIndex));
                    SetCaretIndex(target, keepSelection: shift);
                }
                else if (shift)
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
                if (ctrl)
                {
                    int target = FindNextWordBoundary(shift ? _caretIndex : (HasSelection ? SelectionStart + SelectionLength : _caretIndex));
                    SetCaretIndex(target, keepSelection: shift);
                }
                else if (shift)
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
        string displayText = !string.IsNullOrEmpty(Text) ? Text : (!string.IsNullOrEmpty(Placeholder) ? Placeholder : (!string.IsNullOrEmpty(Label) ? Label : " "));
        var textSize = TextMeasurer.Measure(displayText, FontSize, FontFamily);
        float contentW = GetTextContentStartX() + textSize.Width + padding.Right + 4;
        if (HasSupportingText)
        {
            var supportSize = TextMeasurer.Measure(SupportingText, 12f, FontFamily);
            contentW = Math.Max(contentW, padding.Left + supportSize.Width + padding.Right);
        }

        float containerH = HasLabel ? 56f : Math.Max(48f, textSize.Height + padding.Vertical);
        float totalH = containerH + (HasSupportingText ? 20f : 0f);

        return new Size(
            Math.Max(140f, contentW),
            totalH
        );
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        EnsureCaretVisible();
        return size;
    }
}
