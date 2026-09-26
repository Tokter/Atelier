using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// The base class of themed controls: adds the common appearance properties (colors, padding, corner radius and font)
/// that renderers and layouts read.
/// </summary>
/// <remarks>
/// <see cref="Foreground"/>, <see cref="FontSize"/> and <see cref="FontFamily"/> are inherited, so setting them on a
/// container applies to all controls and text inside it that don't set their own.
/// </remarks>
public class Control : UIElement
{
    /// <summary>Identifies the <see cref="Background"/> property.</summary>
    public static readonly BindableProperty<Color> BackgroundProperty =
        BindableProperty.Register<Control, Color>(nameof(Background), Color.Transparent, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="Foreground"/> property.</summary>
    public static readonly BindableProperty<Color> ForegroundProperty =
        BindableProperty.Register<Control, Color>(nameof(Foreground), Color.Black, options: PropertyOptions.AffectsRender, inherits: true);

    /// <summary>Identifies the <see cref="Padding"/> property.</summary>
    public static readonly BindableProperty<Thickness> PaddingProperty =
        BindableProperty.Register<Control, Thickness>(nameof(Padding), Thickness.Zero, options: PropertyOptions.AffectsMeasure);

    /// <summary>Identifies the <see cref="CornerRadius"/> property.</summary>
    public static readonly BindableProperty<CornerRadius> CornerRadiusProperty =
        BindableProperty.Register<Control, CornerRadius>(nameof(CornerRadius), CornerRadius.Zero, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="FontSize"/> property.</summary>
    public static readonly BindableProperty<float> FontSizeProperty =
        BindableProperty.Register<Control, float>(nameof(FontSize), 14f, options: PropertyOptions.AffectsMeasure, inherits: true);

    /// <summary>Identifies the <see cref="FontFamily"/> property.</summary>
    public static readonly BindableProperty<string?> FontFamilyProperty =
        BindableProperty.Register<Control, string?>(nameof(FontFamily), null, options: PropertyOptions.AffectsMeasure, inherits: true);

    /// <summary>
    /// Gets or sets the background color. The default is transparent; many renderers then use their theme color.
    /// </summary>
    public Color Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>Gets or sets the foreground (text and icon) color. Inherited; the default is black.</summary>
    public Color Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    /// <summary>Gets or sets the space between the control's edge and its content. The default is 0.</summary>
    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    /// <summary>Gets or sets the rounding of the control's corners. The default is 0.</summary>
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>Gets or sets the font size in pixels. Inherited; the default is 14.</summary>
    public float FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    /// <summary>Gets or sets the font family name, or <c>null</c> for the default font. Inherited.</summary>
    public string? FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
}
