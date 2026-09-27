using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Theming;
using Atelier.Theming.Material;

namespace Atelier.Gallery.Infrastructure;

/// <summary>
/// Theme-aware colors for the gallery's own visuals (badges, swatches, demo blocks). Controls get their colors from the
/// theme's renderers; custom elements use <see cref="Themed{T}"/> so they follow light/dark switches too.
/// </summary>
public static class ThemeColors
{
    /// <summary>Gets the color scheme of the active Material theme.</summary>
    public static MaterialColorScheme Current =>
        ThemeManager.HasTheme && ThemeManager.Current is MaterialTheme material ? material.Colors : MaterialColorScheme.Light();

    /// <summary>
    /// Sets <paramref name="property"/> to a color picked from the active scheme, and picks it again whenever the theme
    /// changes while the element is displayed.
    /// </summary>
    public static T Themed<T>(this T element, BindableProperty<Color> property, Func<MaterialColorScheme, Color> pick) where T : UIElement
    {
        void Apply() => element.SetValue(property, pick(Current));
        void OnThemeChanged(Theme theme) => Apply();

        Apply();
        element.AttachedToVisualTree += (_, _) =>
        {
            ThemeManager.ThemeChanged += OnThemeChanged;
            Apply();
        };
        element.DetachedFromVisualTree += (_, _) => ThemeManager.ThemeChanged -= OnThemeChanged;
        return element;
    }
}
