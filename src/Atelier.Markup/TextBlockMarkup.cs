using System;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Properties;
using Atelier.Core.Primitives;
using Atelier.Theming.Material;

namespace Atelier.Markup;

/// <summary>
/// Fluent methods for <see cref="TextBlock"/>: text, font, color, wrapping and the Material type scale. See
/// <see cref="MarkupExtensions"/> for the conventions.
/// </summary>
public static class TextBlockMarkup
{
    #region Text and font

    /// <summary>Sets the displayed text.</summary>
    public static T Text<T>(this T textBlock, string? text) where T : TextBlock => textBlock.Set(TextBlock.TextProperty, text ?? string.Empty);

    /// <summary>Sets the font size in pixels.</summary>
    public static T FontSize<T>(this T textBlock, float fontSize) where T : TextBlock => textBlock.Set(TextBlock.FontSizeProperty, fontSize);

    /// <summary>Sets the font family, e.g. <c>"Consolas"</c>. <c>null</c> uses the inherited or default font.</summary>
    public static T FontFamily<T>(this T textBlock, string? fontFamily) where T : TextBlock => textBlock.Set(TextBlock.FontFamilyProperty, fontFamily);

    /// <summary>Sets the font weight, e.g. <see cref="Core.Primitives.FontWeight.Medium"/>. <see cref="Bold"/> is a shortcut for bold.</summary>
    public static T FontWeight<T>(this T textBlock, FontWeight fontWeight) where T : TextBlock => textBlock.Set(TextBlock.FontWeightProperty, fontWeight);

    /// <summary>Draws the text bold: the effective weight becomes at least bold, whatever the <c>FontWeight</c>.</summary>
    public static T Bold<T>(this T textBlock, bool bold = true) where T : TextBlock => textBlock.Set(TextBlock.BoldProperty, bold);

    /// <summary>Draws the text in italics.</summary>
    public static T Italic<T>(this T textBlock, bool italic = true) where T : TextBlock => textBlock.Set(TextBlock.ItalicProperty, italic);

    /// <summary>
    /// Sets the text color. When it isn't set, the text uses the inherited foreground or the theme's text color for its
    /// container.
    /// </summary>
    public static T Foreground<T>(this T textBlock, Color foreground) where T : TextBlock => textBlock.Set(TextBlock.ForegroundProperty, foreground);

    /// <summary>Draws the text in the theme's muted (secondary) text color, for less important text.</summary>
    public static T Muted<T>(this T textBlock, bool muted = true) where T : TextBlock => textBlock.Set(TextBlock.MutedProperty, muted);

    #endregion

    #region Layout

    /// <summary>Sets how lines are aligned within the text block's width.</summary>
    public static T TextAlignment<T>(this T textBlock, TextAlignment alignment) where T : TextBlock => textBlock.Set(TextBlock.TextAlignmentProperty, alignment);

    /// <summary>Sets whether long lines wrap at word boundaries (<see cref="Controls.TextWrapping.Wrap"/>) or stay on one line.</summary>
    public static T TextWrapping<T>(this T textBlock, TextWrapping wrapping = Controls.TextWrapping.Wrap) where T : TextBlock =>
        textBlock.Set(TextBlock.TextWrappingProperty, wrapping);

    /// <summary>Sets how text that doesn't fit is cut off. The ellipsis variants end it with "…".</summary>
    public static T TextTrimming<T>(this T textBlock, TextTrimming trimming = Controls.TextTrimming.CharacterEllipsis) where T : TextBlock =>
        textBlock.Set(TextBlock.TextTrimmingProperty, trimming);

    /// <summary>Limits wrapped text to <paramref name="maxLines"/> lines; with trimming, the last line ends with an ellipsis. 0 means no limit.</summary>
    public static T MaxLines<T>(this T textBlock, int maxLines) where T : TextBlock => textBlock.Set(TextBlock.MaxLinesProperty, maxLines);

    /// <summary>Sets the distance between the baselines of consecutive lines in pixels. 0 (the default) uses the font's line spacing.</summary>
    public static T LineHeight<T>(this T textBlock, float lineHeight) where T : TextBlock => textBlock.Set(TextBlock.LineHeightProperty, lineHeight);

    #endregion

    #region Typography

    /// <summary>Applies the Material Display Large style (57/64): the largest text, for short, important numbers or words.</summary>
    public static T DisplayLarge<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.DisplayLargeKey);

    /// <summary>Applies the Material Display Medium style (45/52).</summary>
    public static T DisplayMedium<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.DisplayMediumKey);

    /// <summary>Applies the Material Display Small style (36/44).</summary>
    public static T DisplaySmall<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.DisplaySmallKey);

    /// <summary>Applies the Material Headline Large style (32/40), for page headings.</summary>
    public static T HeadlineLarge<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.HeadlineLargeKey);

    /// <summary>Applies the Material Headline Medium style (28/36).</summary>
    public static T HeadlineMedium<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.HeadlineMediumKey);

    /// <summary>Applies the Material Headline Small style (24/32).</summary>
    public static T HeadlineSmall<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.HeadlineSmallKey);

    /// <summary>Applies the Material Title Large style (22/28), for section titles.</summary>
    public static T TitleLarge<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.TitleLargeKey);

    /// <summary>Applies the Material Title Medium style (16/24, medium weight).</summary>
    public static T TitleMedium<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.TitleMediumKey);

    /// <summary>Applies the Material Title Small style (14/20, medium weight).</summary>
    public static T TitleSmall<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.TitleSmallKey);

    /// <summary>Applies the Material Body Large style (16/24), for longer passages.</summary>
    public static T BodyLarge<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.BodyLargeKey);

    /// <summary>Applies the Material Body Medium style (14/20), the default body text.</summary>
    public static T BodyMedium<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.BodyMediumKey);

    /// <summary>Applies the Material Body Small style (12/16).</summary>
    public static T BodySmall<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.BodySmallKey);

    /// <summary>Applies the Material Label Large style (14/20, medium weight), used by buttons.</summary>
    public static T LabelLarge<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.LabelLargeKey);

    /// <summary>Applies the Material Label Medium style (12/16, medium weight).</summary>
    public static T LabelMedium<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.LabelMediumKey);

    /// <summary>Applies the Material Label Small style (11/16, medium weight).</summary>
    public static T LabelSmall<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.LabelSmallKey);

    /// <summary>Applies the Heading 1 style: a bold 32 px page heading.</summary>
    public static T Heading1<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.Heading1Key);

    /// <summary>Applies the Heading 2 style: a bold 28 px section heading.</summary>
    public static T Heading2<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.Heading2Key);

    /// <summary>Applies the Heading 3 style: a bold 24 px subsection heading.</summary>
    public static T Heading3<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.Heading3Key);

    /// <summary>Applies the normal text style (Body Medium).</summary>
    public static T NormalText<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.NormalTextKey);

    /// <summary>Applies the subtext style: Body Small in the muted text color.</summary>
    public static T Subtext<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.SubtextKey);

    /// <summary>Applies the caption style: 11 px muted text.</summary>
    public static T Caption<T>(this T textBlock) where T : TextBlock => textBlock.StyleKey(MaterialTypography.CaptionKey);

    #endregion

    #region Bindings

    /// <summary>Binds the text to a value of <paramref name="source"/>.</summary>
    public static T BindText<T, TSource>(this T textBlock, TSource source, Func<TSource, string> getter, Action<TSource, string>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TSource : class =>
        textBlock.BindToSource(TextBlock.TextProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the text to a value of the DataContext: <c>.BindText((PersonViewModel p) =&gt; p.Name)</c>.</summary>
    public static T BindText<T, TDataContext>(this T textBlock, Func<TDataContext, string> getter, Action<TDataContext, string>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TDataContext : class =>
        textBlock.BindToDataContext(TextBlock.TextProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the text color to a value of <paramref name="source"/>.</summary>
    public static T BindForeground<T, TSource>(this T textBlock, TSource source, Func<TSource, Color> getter, Action<TSource, Color>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TSource : class =>
        textBlock.BindToSource(TextBlock.ForegroundProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the text color to a value of the DataContext.</summary>
    public static T BindForeground<T, TDataContext>(this T textBlock, Func<TDataContext, Color> getter, Action<TDataContext, Color>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TDataContext : class =>
        textBlock.BindToDataContext(TextBlock.ForegroundProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the font size to a value of <paramref name="source"/>.</summary>
    public static T BindFontSize<T, TSource>(this T textBlock, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TSource : class =>
        textBlock.BindToSource(TextBlock.FontSizeProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the font size to a value of the DataContext.</summary>
    public static T BindFontSize<T, TDataContext>(this T textBlock, Func<TDataContext, float> getter, Action<TDataContext, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TDataContext : class =>
        textBlock.BindToDataContext(TextBlock.FontSizeProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the font family to a value of <paramref name="source"/>.</summary>
    public static T BindFontFamily<T, TSource>(this T textBlock, TSource source, Func<TSource, string?> getter, Action<TSource, string?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TSource : class =>
        textBlock.BindToSource(TextBlock.FontFamilyProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the font family to a value of the DataContext.</summary>
    public static T BindFontFamily<T, TDataContext>(this T textBlock, Func<TDataContext, string?> getter, Action<TDataContext, string?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TDataContext : class =>
        textBlock.BindToDataContext(TextBlock.FontFamilyProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the font weight to a value of <paramref name="source"/>.</summary>
    public static T BindFontWeight<T, TSource>(this T textBlock, TSource source, Func<TSource, FontWeight> getter, Action<TSource, FontWeight>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TSource : class =>
        textBlock.BindToSource(TextBlock.FontWeightProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the font weight to a value of the DataContext.</summary>
    public static T BindFontWeight<T, TDataContext>(this T textBlock, Func<TDataContext, FontWeight> getter, Action<TDataContext, FontWeight>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TDataContext : class =>
        textBlock.BindToDataContext(TextBlock.FontWeightProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds bold text to a value of <paramref name="source"/>.</summary>
    public static T BindBold<T, TSource>(this T textBlock, TSource source, Func<TSource, bool> getter, Action<TSource, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TSource : class =>
        textBlock.BindToSource(TextBlock.BoldProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds bold text to a value of the DataContext.</summary>
    public static T BindBold<T, TDataContext>(this T textBlock, Func<TDataContext, bool> getter, Action<TDataContext, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TDataContext : class =>
        textBlock.BindToDataContext(TextBlock.BoldProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds italic text to a value of <paramref name="source"/>.</summary>
    public static T BindItalic<T, TSource>(this T textBlock, TSource source, Func<TSource, bool> getter, Action<TSource, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TSource : class =>
        textBlock.BindToSource(TextBlock.ItalicProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds italic text to a value of the DataContext.</summary>
    public static T BindItalic<T, TDataContext>(this T textBlock, Func<TDataContext, bool> getter, Action<TDataContext, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TDataContext : class =>
        textBlock.BindToDataContext(TextBlock.ItalicProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the muted color to a value of <paramref name="source"/>.</summary>
    public static T BindMuted<T, TSource>(this T textBlock, TSource source, Func<TSource, bool> getter, Action<TSource, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TSource : class =>
        textBlock.BindToSource(TextBlock.MutedProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the muted color to a value of the DataContext.</summary>
    public static T BindMuted<T, TDataContext>(this T textBlock, Func<TDataContext, bool> getter, Action<TDataContext, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TDataContext : class =>
        textBlock.BindToDataContext(TextBlock.MutedProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the text alignment to a value of <paramref name="source"/>.</summary>
    public static T BindTextAlignment<T, TSource>(this T textBlock, TSource source, Func<TSource, TextAlignment> getter, Action<TSource, TextAlignment>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TSource : class =>
        textBlock.BindToSource(TextBlock.TextAlignmentProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the text alignment to a value of the DataContext.</summary>
    public static T BindTextAlignment<T, TDataContext>(this T textBlock, Func<TDataContext, TextAlignment> getter, Action<TDataContext, TextAlignment>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TDataContext : class =>
        textBlock.BindToDataContext(TextBlock.TextAlignmentProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the wrapping mode to a value of <paramref name="source"/>.</summary>
    public static T BindTextWrapping<T, TSource>(this T textBlock, TSource source, Func<TSource, TextWrapping> getter, Action<TSource, TextWrapping>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TSource : class =>
        textBlock.BindToSource(TextBlock.TextWrappingProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the wrapping mode to a value of the DataContext.</summary>
    public static T BindTextWrapping<T, TDataContext>(this T textBlock, Func<TDataContext, TextWrapping> getter, Action<TDataContext, TextWrapping>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : TextBlock where TDataContext : class =>
        textBlock.BindToDataContext(TextBlock.TextWrappingProperty, getter, setter, updateSourceTrigger, getterExpression);

    #endregion
}
