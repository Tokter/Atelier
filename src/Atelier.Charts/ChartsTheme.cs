using Atelier.Charts.Rendering;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Styling;
using Atelier.Theming;
using Atelier.Theming.Material;

namespace Atelier.Charts;

/// <summary>
/// Adds the charts' renderers and styles to themes (see <see cref="ThemeExtensions"/>): Material colors for Material
/// themes, neutral grays for others. The charts register it themselves.
/// </summary>
public static class ChartsTheme
{
    /// <summary>The font size of chart text (Material's body small); tick labels and annotations are 1.5 smaller.</summary>
    public const float ChartFontSize = 13f;

    /// <summary>Registers the extension; calling it again does nothing.</summary>
    public static void Register() => ThemeExtensions.Register(Extend);

    /// <summary>Adds the renderers and styles to <paramref name="theme"/>.</summary>
    public static void Extend(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        theme.Renderers.Register(new XYChartRenderer());
        theme.Styles.AddRange(theme is MaterialTheme material
            ? CreateMaterialStyles(material.Colors, material.IsDark)
            : CreateNeutralStyles(theme.IsDark));
    }

    /// <summary>
    /// Creates the styles for a Material theme: on-surface text on a surface-container-lowest plot, outline-variant grid
    /// lines, outline axes, on-surface-variant tick labels, surface-container-high labels, and a palette starting with
    /// primary, tertiary and error.
    /// </summary>
    public static List<Style> CreateMaterialStyles(MaterialColorScheme colors, bool isDark)
    {
        ArgumentNullException.ThrowIfNull(colors);
        return CreateStyles(
            foreground: colors.OnSurface,
            plotBackground: colors.SurfaceContainerLowest,
            grid: colors.OutlineVariant.WithAlpha(isDark ? 0.5f : 0.7f),
            axis: colors.Outline,
            secondary: colors.OnSurfaceVariant,
            label: colors.SurfaceContainerHigh.WithAlpha(0.92f),
            palette: [colors.Primary, colors.Tertiary, colors.Error, .. Neutral(isDark).Skip(3)]);
    }

    /// <summary>Creates the styles for other themes, in neutral grays.</summary>
    public static List<Style> CreateNeutralStyles(bool isDark)
    {
        var gray = Color.FromRgb(0x80, 0x80, 0x80);
        return CreateStyles(
            foreground: isDark ? Color.FromRgb(0xE0, 0xE0, 0xE0) : Color.FromRgb(0x20, 0x20, 0x20),
            plotBackground: isDark ? Color.FromRgb(0x1C, 0x1C, 0x1C) : Color.White,
            grid: gray.WithAlpha(isDark ? 0.3f : 0.25f),
            axis: gray,
            secondary: isDark ? Color.FromRgb(0xA8, 0xA8, 0xA8) : Color.FromRgb(0x60, 0x60, 0x60),
            label: (isDark ? Color.FromRgb(0x30, 0x30, 0x30) : Color.White).WithAlpha(0.92f),
            palette: Neutral(isDark));
    }

    // Distinct colors, lighter on dark backgrounds.
    private static Color[] Neutral(bool isDark) => isDark
        ?
        [
            Color.FromRgb(0x64, 0xB5, 0xF6), Color.FromRgb(0xFF, 0xB7, 0x4D), Color.FromRgb(0x81, 0xC7, 0x84),
            Color.FromRgb(0xCE, 0x93, 0xD8), Color.FromRgb(0xE5, 0x73, 0x73), Color.FromRgb(0x4D, 0xD0, 0xE1),
        ]
        : [.. XYChart.DefaultPalette];

    private static List<Style> CreateStyles(Color foreground, Color plotBackground, Color grid, Color axis, Color secondary,
        Color label, IReadOnlyList<Color> palette) =>
    [
        new Style(typeof(XYChart))
            .Set(Control.ForegroundProperty, foreground)
            .Set(Control.FontSizeProperty, ChartFontSize)
            .Set(XYChart.PlotBackgroundProperty, plotBackground)
            .Set(XYChart.GridColorProperty, grid)
            .Set(XYChart.AxisColorProperty, axis)
            .Set(XYChart.SecondaryForegroundProperty, secondary)
            .Set(XYChart.LabelBackgroundProperty, label)
            .Set(XYChart.PaletteProperty, palette),
    ];
}
