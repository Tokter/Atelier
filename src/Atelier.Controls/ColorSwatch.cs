using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;

namespace Atelier.Controls;

/// <summary>
/// Shows a color as a rounded patch; a translucent color is drawn over a checkerboard so its transparency is visible.
/// </summary>
/// <remarks>The swatch is 24×24 unless sized otherwise. Set <see cref="Control.CornerRadius"/> to round it.</remarks>
public class ColorSwatch : Control
{
    /// <summary>Identifies the <see cref="Color"/> property.</summary>
    public static readonly BindableProperty<Color> ColorProperty =
        BindableProperty.Register<ColorSwatch, Color>(nameof(Color), Color.Transparent, options: PropertyOptions.AffectsRender);

    static ColorSwatch()
    {
        CornerRadiusProperty.OverrideDefaultValue<ColorSwatch>(new CornerRadius(4));
    }

    /// <summary>Initializes a transparent swatch.</summary>
    public ColorSwatch()
    {
    }

    /// <summary>Initializes a swatch showing <paramref name="color"/>.</summary>
    public ColorSwatch(Color color)
    {
        Color = color;
    }

    /// <summary>Gets or sets the color shown. The default is transparent.</summary>
    public Color Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    /// <inheritdoc/>
    /// <remarks>24×24, or smaller when less space is available.</remarks>
    protected override Size MeasureOverride(Size availableSize) =>
        new(Math.Min(24f, availableSize.Width), Math.Min(24f, availableSize.Height));
}
