using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Layout;

public class Border : UIElement
{
    public static readonly BindableProperty<Color> BackgroundProperty =
        BindableProperty.Register<Border, Color>(nameof(Background), Color.Transparent, (s, o, n) => ((Border)s).InvalidateVisual());

    public static readonly BindableProperty<Color> BorderBrushProperty =
        BindableProperty.Register<Border, Color>(nameof(BorderBrush), Color.Transparent, (s, o, n) => ((Border)s).InvalidateVisual());

    public static readonly BindableProperty<Thickness> BorderThicknessProperty =
        BindableProperty.Register<Border, Thickness>(nameof(BorderThickness), Thickness.Zero, (s, o, n) => ((Border)s).InvalidateMeasure());

    public static readonly BindableProperty<CornerRadius> CornerRadiusProperty =
        BindableProperty.Register<Border, CornerRadius>(nameof(CornerRadius), CornerRadius.Zero, (s, o, n) => ((Border)s).InvalidateVisual());

    public static readonly BindableProperty<Thickness> PaddingProperty =
        BindableProperty.Register<Border, Thickness>(nameof(Padding), Thickness.Zero, (s, o, n) => ((Border)s).InvalidateMeasure());

    public static readonly BindableProperty<float> ElevationProperty =
        BindableProperty.Register<Border, float>(nameof(Elevation), 0f, (s, o, n) => ((Border)s).InvalidateVisual());

    public Color Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public Color BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }
    public Thickness BorderThickness { get => GetValue(BorderThicknessProperty); set => SetValue(BorderThicknessProperty, value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }
    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }
    public float Elevation { get => GetValue(ElevationProperty); set => SetValue(ElevationProperty, value); }

    public UIElement? Child
    {
        get => Children.Count > 0 ? Children[0] as UIElement : null;
        set
        {
            ClearChildren();
            if (value != null)
            {
                AddChild(value);
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Thickness totalPadding = new Thickness(
            BorderThickness.Left + Padding.Left,
            BorderThickness.Top + Padding.Top,
            BorderThickness.Right + Padding.Right,
            BorderThickness.Bottom + Padding.Bottom
        );

        if (Child != null)
        {
            if (Child.Visibility == Visibility.Collapsed)
            {
                Child.Measure(availableSize.Deflate(totalPadding));
            }
            else
            {
                Child.Measure(availableSize.Deflate(totalPadding));
                return Child.DesiredSize.Inflate(totalPadding.Horizontal, totalPadding.Vertical);
            }
        }

        return new Size(totalPadding.Horizontal, totalPadding.Vertical);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Thickness totalPadding = new Thickness(
            BorderThickness.Left + Padding.Left,
            BorderThickness.Top + Padding.Top,
            BorderThickness.Right + Padding.Right,
            BorderThickness.Bottom + Padding.Bottom
        );

        if (Child != null)
        {
            if (Child.Visibility == Visibility.Collapsed)
            {
                Child.Arrange(Rect.Zero);
            }
            else
            {
                Child.Arrange(new Rect(Point.Zero, finalSize).Deflate(totalPadding));
            }
        }

        return finalSize;
    }
}
