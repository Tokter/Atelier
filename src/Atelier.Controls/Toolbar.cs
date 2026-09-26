using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// A top app bar / toolbar container control adhering to Material Design specifications.
/// Supports native elevation drop shadows, customizable backgrounds, borders, and content hosting.
/// </summary>
public class Toolbar : ContentControl
{
    public static readonly BindableProperty<float> ElevationProperty =
        BindableProperty.Register<Toolbar, float>(
            nameof(Elevation),
            2f,
            options: PropertyOptions.AffectsRender
        );

    public static readonly BindableProperty<Color> BorderBrushProperty =
        BindableProperty.Register<Toolbar, Color>(
            nameof(BorderBrush),
            Color.Transparent,
            options: PropertyOptions.AffectsRender
        );

    public static readonly BindableProperty<Thickness> BorderThicknessProperty =
        BindableProperty.Register<Toolbar, Thickness>(
            nameof(BorderThickness),
            Thickness.Zero,
            options: PropertyOptions.AffectsMeasure
        );

    /// <summary>
    /// Gets or sets the Material Design elevation level (casting ambient and key drop shadows).
    /// </summary>
    public float Elevation
    {
        get => GetValue(ElevationProperty);
        set => SetValue(ElevationProperty, value);
    }

    /// <summary>
    /// Gets or sets the border brush for the toolbar outline or bottom divider.
    /// </summary>
    public Color BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the border thickness for the toolbar outline or bottom divider.
    /// </summary>
    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    static Toolbar()
    {
        PaddingProperty.OverrideDefaultValue<Toolbar>(new Thickness(10, 8));
    }

    public Toolbar()
    {
    }

    public Toolbar(object? content) : this()
    {
        Content = content;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var totalPadding = new Thickness(
            BorderThickness.Left + Padding.Left,
            BorderThickness.Top + Padding.Top,
            BorderThickness.Right + Padding.Right,
            BorderThickness.Bottom + Padding.Bottom
        );

        if (CurrentView != null)
        {
            if (CurrentView.Visibility == Visibility.Collapsed)
            {
                CurrentView.Measure(availableSize.Deflate(totalPadding));
            }
            else
            {
                CurrentView.Measure(availableSize.Deflate(totalPadding));
                return CurrentView.DesiredSize.Inflate(totalPadding.Horizontal, totalPadding.Vertical);
            }
        }

        return new Size(totalPadding.Horizontal, totalPadding.Vertical);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var totalPadding = new Thickness(
            BorderThickness.Left + Padding.Left,
            BorderThickness.Top + Padding.Top,
            BorderThickness.Right + Padding.Right,
            BorderThickness.Bottom + Padding.Bottom
        );

        if (CurrentView != null)
        {
            if (CurrentView.Visibility == Visibility.Collapsed)
            {
                CurrentView.Arrange(Rect.Zero);
            }
            else
            {
                CurrentView.Arrange(new Rect(Point.Zero, finalSize).Deflate(totalPadding));
            }
        }

        return finalSize;
    }
}
