using System;
using System.ComponentModel;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Theming;
using Atelier.Theming.Material;

namespace Atelier.Gallery.Views;

/// <summary>Applies a <see cref="ColorRole"/> chosen in a view model to a color property.</summary>
internal static class ColorRoleBinding
{
    public static string DisplayName(ColorRole role) => role == ColorRole.Default ? "Theme default" : role.ToString();

    public static Color Resolve(ColorRole role, MaterialColorScheme c) => role switch
    {
        ColorRole.Primary => c.Primary,
        ColorRole.Secondary => c.Secondary,
        ColorRole.Tertiary => c.Tertiary,
        ColorRole.Error => c.Error,
        ColorRole.Muted => c.OnSurfaceVariant,
        _ => c.OnSurface,
    };

    /// <summary>
    /// Keeps <paramref name="property"/> in sync with the role returned by <paramref name="role"/>: the role's color
    /// from the active theme, or no local value for <see cref="ColorRole.Default"/> (so the text or icon uses its
    /// inherited or theme color instead of a fixed, possibly invisible one). Follows theme switches too.
    /// </summary>
    public static T BindColorRole<T, TSource>(this T element, BindableProperty<Color> property, TSource source, string propertyName, Func<TSource, ColorRole> role)
        where T : UIElement
        where TSource : INotifyPropertyChanged
    {
        void Apply()
        {
            var value = role(source);
            if (value == ColorRole.Default)
            {
                element.ClearValue(property);
            }
            else
            {
                element.SetValue(property, Resolve(value, ThemeColors.Current));
            }
        }

        void OnThemeChanged(Theme theme) => Apply();
        void OnSourceChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == propertyName)
            {
                Apply();
            }
        }

        // Subscribed only while displayed, so a long-lived view model doesn't keep discarded views alive.
        Apply();
        element.AttachedToVisualTree += (_, _) =>
        {
            ThemeManager.ThemeChanged += OnThemeChanged;
            source.PropertyChanged += OnSourceChanged;
            Apply();
        };
        element.DetachedFromVisualTree += (_, _) =>
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            source.PropertyChanged -= OnSourceChanged;
        };
        return element;
    }
}
