using System;
using System.Collections.Generic;
using Atelier.Core;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Controls;

/// <summary>
/// Specifies the horizontal alignment of text lines within the available width.
/// </summary>
public enum TextAlignment
{
    /// <summary>Lines start at the left edge.</summary>
    Left,
    /// <summary>Lines are centered.</summary>
    Center,
    /// <summary>Lines end at the right edge.</summary>
    Right
}

/// <summary>
/// Specifies whether text wraps when it reaches the edge of the available width.
/// </summary>
public enum TextWrapping
{
    /// <summary>Lines only break at line breaks ('\n'); longer lines extend beyond the available width.</summary>
    NoWrap = 0,
    /// <summary>Lines also break at spaces so they fit the available width; words that are too long are broken between characters.</summary>
    Wrap
}

/// <summary>
/// Specifies how text that doesn't fit is trimmed.
/// </summary>
public enum TextTrimming
{
    /// <summary>Text is not trimmed.</summary>
    None = 0,
    /// <summary>Text is cut after the last character that fits and an ellipsis ("…") is appended.</summary>
    CharacterEllipsis,
    /// <summary>Text is cut after the last word that fits and an ellipsis ("…") is appended.</summary>
    WordEllipsis
}

/// <summary>
/// Displays a read-only block of text in a single font and color.
/// </summary>
/// <remarks>
/// <para>
/// Line breaks ('\n', or "\r\n") always start a new line. With <see cref="TextWrapping.Wrap"/> lines also wrap at the
/// available width; <see cref="TextTrimming"/> and <see cref="MaxLines"/> cut off text that doesn't fit.
/// </para>
/// <para>
/// The line layout is computed during measure and cached (keyed by text, font, width and the layout properties), so
/// measuring again with unchanged inputs and rendering do not allocate or re-measure. The layout is only redone for the
/// arranged width when it differs from the measured one in a way that changes the lines.
/// </para>
/// <para>A text block is not hit-test visible by default, so pointer input goes to the element behind it.</para>
/// </remarks>
public class TextBlock : UIElement
{
    /// <summary>Identifies the <see cref="Text"/> property.</summary>
    public static readonly BindableProperty<string> TextProperty =
        BindableProperty.Register<TextBlock, string>(
            nameof(Text),
            string.Empty,
            coerceValue: static (_, value) => value ?? string.Empty,
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="FontSize"/> property; shared with <see cref="Control.FontSizeProperty"/>.</summary>
    // Shared with Control, so a style, local value or inherited value set through either field affects both.
    public static readonly BindableProperty<float> FontSizeProperty = Control.FontSizeProperty.AddOwner<TextBlock>();

    /// <summary>Identifies the <see cref="Foreground"/> property; shared with <see cref="Control.ForegroundProperty"/>.</summary>
    public static readonly BindableProperty<Color> ForegroundProperty = Control.ForegroundProperty.AddOwner<TextBlock>();

    /// <summary>Identifies the <see cref="FontFamily"/> property; shared with <see cref="Control.FontFamilyProperty"/>.</summary>
    public static readonly BindableProperty<string?> FontFamilyProperty = Control.FontFamilyProperty.AddOwner<TextBlock>();

    /// <summary>Identifies the <see cref="TextAlignment"/> property.</summary>
    public static readonly BindableProperty<TextAlignment> TextAlignmentProperty =
        BindableProperty.Register<TextBlock, TextAlignment>(
            nameof(TextAlignment),
            TextAlignment.Left,
            options: PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="TextWrapping"/> property.</summary>
    public static readonly BindableProperty<TextWrapping> TextWrappingProperty =
        BindableProperty.Register<TextBlock, TextWrapping>(
            nameof(TextWrapping),
            TextWrapping.NoWrap,
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="TextTrimming"/> property.</summary>
    public static readonly BindableProperty<TextTrimming> TextTrimmingProperty =
        BindableProperty.Register<TextBlock, TextTrimming>(
            nameof(TextTrimming),
            TextTrimming.None,
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="MaxLines"/> property.</summary>
    public static readonly BindableProperty<int> MaxLinesProperty =
        BindableProperty.Register<TextBlock, int>(
            nameof(MaxLines),
            0,
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender,
            validateValue: static value => value >= 0
        );

    /// <summary>Identifies the <see cref="LineHeight"/> property.</summary>
    public static readonly BindableProperty<float> LineHeightProperty =
        BindableProperty.Register<TextBlock, float>(
            nameof(LineHeight),
            0f,
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender,
            validateValue: static value => float.IsFinite(value) && value >= 0
        );

    /// <summary>Identifies the <see cref="Bold"/> property.</summary>
    public static readonly BindableProperty<bool> BoldProperty =
        BindableProperty.Register<TextBlock, bool>(
            nameof(Bold),
            false,
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="Italic"/> property.</summary>
    public static readonly BindableProperty<bool> ItalicProperty =
        BindableProperty.Register<TextBlock, bool>(
            nameof(Italic),
            false,
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="Muted"/> property.</summary>
    public static readonly BindableProperty<bool> MutedProperty =
        BindableProperty.Register<TextBlock, bool>(
            nameof(Muted),
            false,
            options: PropertyOptions.AffectsRender
        );

    /// <summary>Gets or sets the displayed text. <c>null</c> is stored as an empty string. The default is empty.</summary>
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>Gets or sets the font size in pixels. Inherited; the default is 14.</summary>
    public float FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>
    /// Gets or sets the text color. Inherited. While it is not set anywhere (the default, black), the theme's text color is
    /// used, so text stays readable in dark themes; any explicitly set, styled or inherited color, black included, is honored.
    /// </summary>
    public Color Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>Gets or sets the font family name, or <c>null</c> for the default font. Inherited.</summary>
    public string? FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    /// <summary>Gets or sets the horizontal alignment of each line within the text block's width. The default is <see cref="TextAlignment.Left"/>.</summary>
    public TextAlignment TextAlignment { get => GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }

    /// <summary>Gets or sets whether lines wrap at the available width. The default is <see cref="TextWrapping.NoWrap"/>.</summary>
    public TextWrapping TextWrapping { get => GetValue(TextWrappingProperty); set => SetValue(TextWrappingProperty, value); }

    /// <summary>
    /// Gets or sets how lines wider than the available width, and the last line when <see cref="MaxLines"/> cuts off
    /// text, are trimmed. The default is <see cref="TextTrimming.None"/>.
    /// </summary>
    public TextTrimming TextTrimming { get => GetValue(TextTrimmingProperty); set => SetValue(TextTrimmingProperty, value); }

    /// <summary>
    /// Gets or sets the maximum number of lines shown; further lines are dropped (and the last shown line ends with an
    /// ellipsis if <see cref="TextTrimming"/> is set). 0, the default, means no limit. Must not be negative.
    /// </summary>
    public int MaxLines { get => GetValue(MaxLinesProperty); set => SetValue(MaxLinesProperty, value); }

    /// <summary>
    /// Gets or sets the distance between the baselines of consecutive lines in pixels. 0, the default, uses the font's
    /// line spacing. Must be finite and not negative.
    /// </summary>
    public float LineHeight { get => GetValue(LineHeightProperty); set => SetValue(LineHeightProperty, value); }

    /// <summary>Gets or sets whether the text uses the bold weight. The default is <c>false</c>.</summary>
    public bool Bold { get => GetValue(BoldProperty); set => SetValue(BoldProperty, value); }

    /// <summary>Gets or sets whether the text uses the italic slant. The default is <c>false</c>.</summary>
    public bool Italic { get => GetValue(ItalicProperty); set => SetValue(ItalicProperty, value); }

    /// <summary>Gets or sets whether the text is drawn de-emphasized (the theme's secondary text color, or the foreground at reduced opacity).</summary>
    public bool Muted { get => GetValue(MutedProperty); set => SetValue(MutedProperty, value); }

    private readonly List<TextLine> _lines = new();
    private string?[] _lineTexts = Array.Empty<string?>();
    private LayoutKey _layoutKey;
    private bool _hasLayout;
    private Size _layoutSize;
    private float _lineAdvance;
    private float _firstBaseline;

    /// <summary>Initializes an empty text block.</summary>
    public TextBlock()
    {
        IsHitTestVisible = false;
    }

    /// <summary>Initializes a text block showing <paramref name="text"/>; text containing emoji uses the "Segoe UI Emoji" font.</summary>
    /// <param name="text">The text to show.</param>
    public TextBlock(string text) : this()
    {
        Text = text;

        if (Text.ContainsEmoji())
        {
            FontFamily = "Segoe UI Emoji";
        }
    }

    /// <summary>
    /// Gets the laid-out lines for the current width (see <see cref="GetLineText"/> for their text). Reuses the layout
    /// from measure and arrange when the text, font and width still match.
    /// </summary>
    public IReadOnlyList<TextLine> GetLines()
    {
        float width = Bounds.Width;
        if (width > 0)
        {
            EnsureArrangedLayout(width);
        }
        else if (!_hasLayout || !CurrentKey(_layoutKey.MaxWidth).Equals(_layoutKey))
        {
            // Not arranged yet: keep the measured layout unless an input changed.
            EnsureLayout(_hasLayout ? _layoutKey.MaxWidth : float.PositiveInfinity);
        }
        return _lines;
    }

    /// <summary>
    /// Gets the text of line <paramref name="index"/> of <see cref="GetLines"/>, including a trailing ellipsis if the
    /// line was trimmed. The string is created once per layout and cached.
    /// </summary>
    /// <param name="index">The line index.</param>
    public string GetLineText(int index)
    {
        var line = _lines[index];
        ref string? cached = ref _lineTexts[index];
        if (cached == null)
        {
            string text = _layoutKey.Text ?? string.Empty;
            if (line.HasEllipsis)
            {
                cached = string.Concat(text.AsSpan(line.Start, line.Length), TextMeasurer.Ellipsis);
            }
            else
            {
                cached = line.Start == 0 && line.Length == text.Length ? text : text.Substring(line.Start, line.Length);
            }
        }
        return cached;
    }

    /// <summary>Gets the distance between the baselines of consecutive lines of the current layout.</summary>
    public float LineAdvance => _lineAdvance;

    /// <summary>Gets the y coordinate of the first line's baseline, relative to the top of the text block.</summary>
    public float FirstBaseline => _firstBaseline;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureLayout(availableSize.Width);
        return _layoutSize;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        EnsureArrangedLayout(finalSize.Width);
        return finalSize;
    }

    // Keeps the measured layout if it is also valid for the arranged width, so layout rounding doesn't re-wrap text.
    private void EnsureArrangedLayout(float width)
    {
        if (_hasLayout && _layoutKey.IsWidthDependent && CurrentKey(_layoutKey.MaxWidth).Equals(_layoutKey))
        {
            // Greedy wrapping and trimming give the same lines for any width between the widest line and the width
            // they were computed for.
            const float tolerance = 0.5f;
            if (width >= _layoutSize.Width - tolerance && width <= _layoutKey.MaxWidth + tolerance)
            {
                return;
            }
        }

        EnsureLayout(width);
    }

    private LayoutKey CurrentKey(float maxWidth)
    {
        var wrapping = TextWrapping;
        var trimming = TextTrimming;
        bool widthDependent = wrapping == TextWrapping.Wrap || trimming != TextTrimming.None;
        float widthKey = widthDependent && maxWidth > 0 && float.IsFinite(maxWidth) ? maxWidth : float.PositiveInfinity;
        return new LayoutKey(Text, FontSize, FontFamily, Bold, Italic, wrapping, trimming, MaxLines, LineHeight, widthKey);
    }

    private void EnsureLayout(float maxWidth)
    {
        var key = CurrentKey(maxWidth);
        if (_hasLayout && key.Equals(_layoutKey))
        {
            return;
        }

        _layoutKey = key;
        _hasLayout = true;
        Array.Clear(_lineTexts);

        string text = key.Text ?? string.Empty;
        if (text.Length == 0 || key.FontSize <= 0)
        {
            _lines.Clear();
            _layoutSize = Size.Zero;
            _lineAdvance = 0f;
            _firstBaseline = 0f;
            return;
        }

        var font = TextMeasurer.GetFont(key.FontSize, key.FontFamily, key.Bold, key.Italic);
        TextMeasurer.LayoutLines(text, key.MaxWidth, key.Wrapping == TextWrapping.Wrap, font, _lines);

        bool cutOff = false;
        if (key.MaxLines > 0 && _lines.Count > key.MaxLines)
        {
            _lines.RemoveRange(key.MaxLines, _lines.Count - key.MaxLines);
            cutOff = true;
        }

        if (key.Trimming != TextTrimming.None)
        {
            bool wordEllipsis = key.Trimming == TextTrimming.WordEllipsis;
            for (int i = 0; i < _lines.Count; i++)
            {
                bool force = cutOff && i == _lines.Count - 1;
                var line = _lines[i];
                if (force || line.Width > key.MaxWidth)
                {
                    _lines[i] = TextMeasurer.TrimLine(text, line, key.MaxWidth, wordEllipsis, force, font);
                }
            }
        }

        float widest = 0f;
        for (int i = 0; i < _lines.Count; i++)
        {
            widest = Math.Max(widest, _lines[i].Width);
        }

        float spacing = font.Spacing;
        _lineAdvance = key.LineHeight > 0 ? key.LineHeight : spacing;
        _firstBaseline = key.FontSize + (_lineAdvance - spacing) * 0.5f;
        _layoutSize = new Size(widest, _lines.Count * _lineAdvance);

        if (_lineTexts.Length < _lines.Count)
        {
            _lineTexts = new string?[Math.Max(_lines.Count, _lineTexts.Length * 2)];
        }
    }

    private readonly record struct LayoutKey(
        string? Text,
        float FontSize,
        string? FontFamily,
        bool Bold,
        bool Italic,
        TextWrapping Wrapping,
        TextTrimming Trimming,
        int MaxLines,
        float LineHeight,
        float MaxWidth)
    {
        public bool IsWidthDependent => !float.IsPositiveInfinity(MaxWidth);
    }
}
