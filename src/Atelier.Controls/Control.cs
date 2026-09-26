using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

public class Control : UIElement
{
    public static readonly BindableProperty<Color> BackgroundProperty =
        BindableProperty.Register<Control, Color>(nameof(Background), Color.Transparent, options: PropertyOptions.AffectsRender);

    public static readonly BindableProperty<Color> ForegroundProperty =
        BindableProperty.Register<Control, Color>(nameof(Foreground), Color.Black, options: PropertyOptions.AffectsRender, inherits: true);

    public static readonly BindableProperty<Thickness> PaddingProperty =
        BindableProperty.Register<Control, Thickness>(nameof(Padding), Thickness.Zero, options: PropertyOptions.AffectsMeasure);

    public static readonly BindableProperty<CornerRadius> CornerRadiusProperty =
        BindableProperty.Register<Control, CornerRadius>(nameof(CornerRadius), CornerRadius.Zero, options: PropertyOptions.AffectsRender);

    public static readonly BindableProperty<float> FontSizeProperty =
        BindableProperty.Register<Control, float>(nameof(FontSize), 14f, options: PropertyOptions.AffectsMeasure, inherits: true);

    public static readonly BindableProperty<string?> FontFamilyProperty =
        BindableProperty.Register<Control, string?>(nameof(FontFamily), null, options: PropertyOptions.AffectsMeasure, inherits: true);

    public Color Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public Color Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public float FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public string? FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
}
