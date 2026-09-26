using System;
using System.Collections.Generic;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Styling;

namespace Atelier.Theming.Material;

/// <summary>
/// Keyed <see cref="TextBlock"/> styles for the Material Design 3 type scale (Display, Headline, Title, Body, Label),
/// plus convenience aliases (Heading1-3, NormalText, Subtext, Caption).
/// </summary>
/// <remarks>
/// The MD3 styles set the font size, line height and weight of the type scale (<see cref="MaterialTypescale"/>).
/// <see cref="MaterialTheme"/> includes them in its <see cref="Theme.Styles"/>, so they are available by key
/// (<c>StyleKey = "TitleMedium"</c>) while the theme is active.
/// </remarks>
public static class MaterialTypography
{
    /// <summary>Display Large (57/64, regular).</summary>
    public const string DisplayLargeKey = "DisplayLarge";
    /// <summary>Display Medium (45/52, regular).</summary>
    public const string DisplayMediumKey = "DisplayMedium";
    /// <summary>Display Small (36/44, regular).</summary>
    public const string DisplaySmallKey = "DisplaySmall";

    /// <summary>Headline Large (32/40, regular).</summary>
    public const string HeadlineLargeKey = "HeadlineLarge";
    /// <summary>Headline Medium (28/36, regular).</summary>
    public const string HeadlineMediumKey = "HeadlineMedium";
    /// <summary>Headline Small (24/32, regular).</summary>
    public const string HeadlineSmallKey = "HeadlineSmall";

    /// <summary>Title Large (22/28, regular).</summary>
    public const string TitleLargeKey = "TitleLarge";
    /// <summary>Title Medium (16/24, medium).</summary>
    public const string TitleMediumKey = "TitleMedium";
    /// <summary>Title Small (14/20, medium).</summary>
    public const string TitleSmallKey = "TitleSmall";

    /// <summary>Body Large (16/24, regular).</summary>
    public const string BodyLargeKey = "BodyLarge";
    /// <summary>Body Medium (14/20, regular).</summary>
    public const string BodyMediumKey = "BodyMedium";
    /// <summary>Body Small (12/16, regular).</summary>
    public const string BodySmallKey = "BodySmall";

    /// <summary>Label Large (14/20, medium).</summary>
    public const string LabelLargeKey = "LabelLarge";
    /// <summary>Label Medium (12/16, medium).</summary>
    public const string LabelMediumKey = "LabelMedium";
    /// <summary>Label Small (11/16, medium).</summary>
    public const string LabelSmallKey = "LabelSmall";

    /// <summary>Alias: a bold 32 px page heading.</summary>
    public const string Heading1Key = "Heading1";
    /// <summary>Alias: a bold 28 px section heading.</summary>
    public const string Heading2Key = "Heading2";
    /// <summary>Alias: a bold 24 px subsection heading.</summary>
    public const string Heading3Key = "Heading3";
    /// <summary>Alias of Body Medium.</summary>
    public const string NormalTextKey = "NormalText";
    /// <summary>Alias of Body Small in the muted (on-surface-variant) color.</summary>
    public const string SubtextKey = "Subtext";
    /// <summary>11 px muted caption text.</summary>
    public const string CaptionKey = "Caption";

    /// <summary>All style keys this class provides.</summary>
    public static readonly string[] AllKeys =
    [
        DisplayLargeKey, DisplayMediumKey, DisplaySmallKey,
        HeadlineLargeKey, HeadlineMediumKey, HeadlineSmallKey,
        TitleLargeKey, TitleMediumKey, TitleSmallKey,
        BodyLargeKey, BodyMediumKey, BodySmallKey,
        LabelLargeKey, LabelMediumKey, LabelSmallKey,
        Heading1Key, Heading2Key, Heading3Key,
        NormalTextKey, SubtextKey, CaptionKey
    ];

    /// <summary>Creates the typography styles.</summary>
    /// <returns>A new style for each key in <see cref="AllKeys"/>.</returns>
    public static List<Style> CreateStyles()
    {
        return
        [
            Create(DisplayLargeKey, MaterialTypescale.DisplayLarge),
            Create(DisplayMediumKey, MaterialTypescale.DisplayMedium),
            Create(DisplaySmallKey, MaterialTypescale.DisplaySmall),

            Create(HeadlineLargeKey, MaterialTypescale.HeadlineLarge),
            Create(HeadlineMediumKey, MaterialTypescale.HeadlineMedium),
            Create(HeadlineSmallKey, MaterialTypescale.HeadlineSmall),

            Create(TitleLargeKey, MaterialTypescale.TitleLarge),
            Create(TitleMediumKey, MaterialTypescale.TitleMedium),
            Create(TitleSmallKey, MaterialTypescale.TitleSmall),

            Create(BodyLargeKey, MaterialTypescale.BodyLarge),
            Create(BodyMediumKey, MaterialTypescale.BodyMedium),
            Create(BodySmallKey, MaterialTypescale.BodySmall),

            Create(LabelLargeKey, MaterialTypescale.LabelLarge),
            Create(LabelMediumKey, MaterialTypescale.LabelMedium),
            Create(LabelSmallKey, MaterialTypescale.LabelSmall),

            // Aliases: bold app headings, and body/caption text.
            Create(Heading1Key, MaterialTypescale.HeadlineLarge, bold: true),
            Create(Heading2Key, MaterialTypescale.HeadlineMedium, bold: true),
            Create(Heading3Key, MaterialTypescale.HeadlineSmall, bold: true),
            Create(NormalTextKey, MaterialTypescale.BodyMedium),
            Create(SubtextKey, MaterialTypescale.BodySmall, muted: true),
            Create(CaptionKey, MaterialTypescale.LabelSmall with { Weight = FontWeight.Normal }, muted: true),
        ];
    }

    /// <summary>
    /// Adds the typography styles to <paramref name="styles"/>, replacing styles with the same keys.
    /// </summary>
    /// <remarks>
    /// Not needed with <see cref="MaterialTheme"/>, which provides these styles while it is active; use it to make them
    /// available in another collection (e.g. an element's <see cref="Core.Tree.UIElement.Styles"/>).
    /// </remarks>
    /// <param name="styles">The collection to add to.</param>
    /// <param name="colors">Unused; kept for compatibility.</param>
    public static void RegisterStyles(StyleCollection styles, MaterialColorScheme colors)
    {
        ArgumentNullException.ThrowIfNull(styles);

        var keys = new HashSet<string>(AllKeys, StringComparer.Ordinal);
        for (int i = styles.Count - 1; i >= 0; i--)
        {
            if (styles[i].Key != null && keys.Contains(styles[i].Key!))
            {
                styles.RemoveAt(i);
            }
        }

        styles.AddRange(CreateStyles());
    }

    /// <summary>
    /// Returns the style for <paramref name="key"/>: the one in <see cref="StyleManager.GlobalStyles"/> or the active
    /// theme's styles (<see cref="StyleManager.ThemeStyles"/>) if present, otherwise a new default one.
    /// </summary>
    /// <param name="key">One of the keys in <see cref="AllKeys"/>.</param>
    /// <returns>The style, or <c>null</c> for an unknown key.</returns>
    public static Style? GetRegisteredStyle(string key)
    {
        var found = Find(StyleManager.GlobalStyles, key) ?? Find(StyleManager.ThemeStyles, key);
        if (found != null)
        {
            return found;
        }

        foreach (var style in CreateStyles())
        {
            if (style.Key == key)
            {
                return style;
            }
        }
        return null;
    }

    private static Style? Find(StyleCollection styles, string key)
    {
        for (int i = 0; i < styles.Count; i++)
        {
            if (styles[i].Key == key)
            {
                return styles[i];
            }
        }
        return null;
    }

    private static Style Create(string key, MaterialTextStyle textStyle, bool bold = false, bool muted = false)
    {
        return new Style(key, typeof(TextBlock))
            .Set(TextBlock.FontSizeProperty, textStyle.Size)
            .Set(TextBlock.LineHeightProperty, textStyle.LineHeight)
            .Set(TextBlock.FontWeightProperty, textStyle.Weight)
            .Set(TextBlock.BoldProperty, bold)
            .Set(TextBlock.MutedProperty, muted);
    }
}
