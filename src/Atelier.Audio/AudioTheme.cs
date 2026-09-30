using Atelier.Audio.Renderers;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Styling;
using Atelier.Theming;
using Atelier.Theming.Material;

namespace Atelier.Audio;

/// <summary>
/// Adds the audio controls' renderers and styles to themes (see <see cref="ThemeExtensions"/>): Material colors for
/// Material themes, neutral grays for others. The audio controls register it themselves.
/// </summary>
public static class AudioTheme
{
    /// <summary>The font size of timeline labels (Material's label small).</summary>
    public const float TimelineFontSize = 11f;

    /// <summary>Registers the extension; calling it again does nothing.</summary>
    public static void Register() => ThemeExtensions.Register(Extend);

    /// <summary>Adds the renderers and styles to <paramref name="theme"/>.</summary>
    public static void Extend(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        theme.Renderers.Register(new TimelineRulerRenderer());
        theme.Renderers.Register(new TimelineLaneRenderer());
        if (theme is MaterialTheme material)
        {
            theme.Renderers.Register(new MaterialKnobRenderer(material.Colors));
            theme.Styles.AddRange(CreateMaterialStyles(material.Colors, material.IsDark));
        }
        else
        {
            theme.Styles.AddRange(CreateNeutralStyles(theme.IsDark));
        }
    }

    /// <summary>
    /// Creates the styles for a Material theme: a surface-container ruler with on-surface labels, on-surface-variant
    /// ticks and second row, and surface-container-low lanes with outline-variant grid lines.
    /// </summary>
    public static List<Style> CreateMaterialStyles(MaterialColorScheme colors, bool isDark)
    {
        ArgumentNullException.ThrowIfNull(colors);
        return CreateStyles(
            rulerBackground: colors.SurfaceContainer,
            laneBackground: colors.SurfaceContainerLow,
            label: colors.OnSurface,
            secondaryLabel: colors.OnSurfaceVariant,
            rulerLine: colors.OutlineVariant,
            rulerMajorLine: colors.OnSurfaceVariant,
            laneLine: colors.OutlineVariant.WithAlpha(isDark ? 0.45f : 0.6f),
            laneMajorLine: colors.OutlineVariant.WithAlpha(isDark ? 0.9f : 1f));
    }

    /// <summary>Creates the styles for other themes, in neutral grays.</summary>
    public static List<Style> CreateNeutralStyles(bool isDark)
    {
        var foreground = isDark ? Color.FromRgb(0xE0, 0xE0, 0xE0) : Color.FromRgb(0x20, 0x20, 0x20);
        var gray = Color.FromRgb(0x80, 0x80, 0x80);
        return CreateStyles(
            rulerBackground: isDark ? Color.FromRgb(0x2A, 0x2A, 0x2A) : Color.FromRgb(0xEC, 0xEC, 0xEC),
            laneBackground: isDark ? Color.FromRgb(0x20, 0x20, 0x20) : Color.FromRgb(0xF6, 0xF6, 0xF6),
            label: foreground,
            secondaryLabel: gray,
            rulerLine: gray.WithAlpha(0.5f),
            rulerMajorLine: gray,
            laneLine: gray.WithAlpha(0.25f),
            laneMajorLine: gray.WithAlpha(0.55f));
    }

    private static List<Style> CreateStyles(Color rulerBackground, Color laneBackground, Color label, Color secondaryLabel,
        Color rulerLine, Color rulerMajorLine, Color laneLine, Color laneMajorLine) =>
    [
        new Style(typeof(TimelineRuler))
            .Set(Control.BackgroundProperty, rulerBackground)
            .Set(Control.ForegroundProperty, label)
            .Set(Control.FontSizeProperty, TimelineFontSize)
            .Set(TimelineRuler.SecondaryForegroundProperty, secondaryLabel)
            .Set(TimelineControl.LineColorProperty, rulerLine)
            .Set(TimelineControl.MajorLineColorProperty, rulerMajorLine),
        new Style(typeof(TimelineLane))
            .Set(Control.BackgroundProperty, laneBackground)
            .Set(Control.FontSizeProperty, TimelineFontSize)
            .Set(TimelineControl.LineColorProperty, laneLine)
            .Set(TimelineControl.MajorLineColorProperty, laneMajorLine),
    ];
}
