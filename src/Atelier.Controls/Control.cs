using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

public class Control : UIElement
{
    public static readonly BindableProperty<Color> BackgroundProperty =
        BindableProperty.Register<Control, Color>(nameof(Background), Color.Transparent, (s, o, n) => (s as UIElement)?.InvalidateVisual());

    public static readonly BindableProperty<Color> ForegroundProperty =
        BindableProperty.Register<Control, Color>(nameof(Foreground), Color.Black, (s, o, n) => (s as UIElement)?.InvalidateVisual(), inherits: true);

    public static readonly BindableProperty<Thickness> PaddingProperty =
        BindableProperty.Register<Control, Thickness>(nameof(Padding), Thickness.Zero, (s, o, n) => (s as UIElement)?.InvalidateMeasure());

    public static readonly BindableProperty<CornerRadius> CornerRadiusProperty =
        BindableProperty.Register<Control, CornerRadius>(nameof(CornerRadius), CornerRadius.Zero, (s, o, n) => (s as UIElement)?.InvalidateVisual());

    public static readonly BindableProperty<float> FontSizeProperty =
        BindableProperty.Register<Control, float>(nameof(FontSize), 14f, (s, o, n) => (s as UIElement)?.InvalidateMeasure(), inherits: true);

    public static readonly BindableProperty<string?> FontFamilyProperty =
        BindableProperty.Register<Control, string?>(nameof(FontFamily), null, (s, o, n) => (s as UIElement)?.InvalidateMeasure(), inherits: true);

    public Color Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public Color Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public float FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public string? FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
}
