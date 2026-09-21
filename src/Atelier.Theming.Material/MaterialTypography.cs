using System;
using System.Collections.Generic;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Styling;

namespace Atelier.Theming.Material;

/// <summary>
/// Material Design 3 Typography Scale definitions and style keys.
/// Provides styles for standard Display, Headline, Title, Body, and Label scales,
/// plus convenient aliases for Heading1, Heading2, Heading3, NormalText, Subtext, and Caption.
/// </summary>
public static class MaterialTypography
{
    // MD3 Scale Keys
    public const string DisplayLargeKey = "DisplayLarge";
    public const string DisplayMediumKey = "DisplayMedium";
    public const string DisplaySmallKey = "DisplaySmall";

    public const string HeadlineLargeKey = "HeadlineLarge";
    public const string HeadlineMediumKey = "HeadlineMedium";
    public const string HeadlineSmallKey = "HeadlineSmall";

    public const string TitleLargeKey = "TitleLarge";
    public const string TitleMediumKey = "TitleMedium";
    public const string TitleSmallKey = "TitleSmall";

    public const string BodyLargeKey = "BodyLarge";
    public const string BodyMediumKey = "BodyMedium";
    public const string BodySmallKey = "BodySmall";

    public const string LabelLargeKey = "LabelLarge";
    public const string LabelMediumKey = "LabelMedium";
    public const string LabelSmallKey = "LabelSmall";

    // Common Heading, Normal Text, and Subtext Aliases
    public const string Heading1Key = "Heading1";
    public const string Heading2Key = "Heading2";
    public const string Heading3Key = "Heading3";
    public const string NormalTextKey = "NormalText";
    public const string SubtextKey = "Subtext";
    public const string CaptionKey = "Caption";

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

    /// <summary>
    /// Registers all Material Design 3 typography styles into the specified style collection (e.g. StyleManager.GlobalStyles).
    /// </summary>
    public static void RegisterStyles(StyleCollection styles, MaterialColorScheme colors)
    {
        var keys = new HashSet<string>(AllKeys, StringComparer.Ordinal);
        for (int i = styles.Count - 1; i >= 0; i--)
        {
            if (styles[i].Key != null && keys.Contains(styles[i].Key!))
            {
                styles.RemoveAt(i);
            }
        }

        // Display styles
        styles.Add(CreateStyle(DisplayLargeKey, fontSize: 57f, bold: false));
        styles.Add(CreateStyle(DisplayMediumKey, fontSize: 45f, bold: false));
        styles.Add(CreateStyle(DisplaySmallKey, fontSize: 36f, bold: false));

        // Headline styles
        styles.Add(CreateStyle(HeadlineLargeKey, fontSize: 32f, bold: true));
        styles.Add(CreateStyle(HeadlineMediumKey, fontSize: 28f, bold: true));
        styles.Add(CreateStyle(HeadlineSmallKey, fontSize: 24f, bold: true));

        // Title styles
        styles.Add(CreateStyle(TitleLargeKey, fontSize: 22f, bold: true));
        styles.Add(CreateStyle(TitleMediumKey, fontSize: 16f, bold: true));
        styles.Add(CreateStyle(TitleSmallKey, fontSize: 14f, bold: true));

        // Body styles
        styles.Add(CreateStyle(BodyLargeKey, fontSize: 16f, bold: false));
        styles.Add(CreateStyle(BodyMediumKey, fontSize: 14f, bold: false));
        styles.Add(CreateStyle(BodySmallKey, fontSize: 12f, bold: false));

        // Label styles
        styles.Add(CreateStyle(LabelLargeKey, fontSize: 14f, bold: true));
        styles.Add(CreateStyle(LabelMediumKey, fontSize: 12f, bold: true));
        styles.Add(CreateStyle(LabelSmallKey, fontSize: 11f, bold: true));

        // Common Aliases: Heading1, Heading2, Heading3
        styles.Add(CreateStyle(Heading1Key, fontSize: 32f, bold: true));
        styles.Add(CreateStyle(Heading2Key, fontSize: 28f, bold: true));
        styles.Add(CreateStyle(Heading3Key, fontSize: 24f, bold: true));

        // Common Aliases: NormalText, Subtext, Caption
        styles.Add(CreateStyle(NormalTextKey, fontSize: 14f, bold: false));
        styles.Add(CreateStyle(SubtextKey, fontSize: 12f, bold: false, muted: true));
        styles.Add(CreateStyle(CaptionKey, fontSize: 11f, bold: false, muted: true));
    }

    private static Style CreateStyle(string key, float fontSize, bool bold, bool muted = false)
    {
        return new Style(key, typeof(TextBlock))
            .Set(TextBlock.FontSizeProperty, fontSize)
            .Set(TextBlock.BoldProperty, bold)
            .Set(TextBlock.MutedProperty, muted);
    }

    /// <summary>
    /// Looks up a registered MaterialTypography style from StyleManager.GlobalStyles by its key.
    /// If not yet registered, registers default light styles first.
    /// </summary>
    public static Style? GetRegisteredStyle(string key)
    {
        for (int i = 0; i < StyleManager.GlobalStyles.Count; i++)
        {
            if (StyleManager.GlobalStyles[i].Key == key)
            {
                return StyleManager.GlobalStyles[i];
            }
        }

        // Fallback: register default light styles if not yet present in GlobalStyles
        RegisterStyles(StyleManager.GlobalStyles, MaterialColorScheme.Light());
        for (int i = 0; i < StyleManager.GlobalStyles.Count; i++)
        {
            if (StyleManager.GlobalStyles[i].Key == key)
            {
                return StyleManager.GlobalStyles[i];
            }
        }

        return null;
    }
}
