using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// A top app bar / toolbar container control adhering to Material Design specifications.
/// Supports native elevation drop shadows, customizable backgrounds, borders, and content hosting.
/// </summary>
/// <remarks>
/// The content is placed inside <see cref="BorderThickness"/> plus <see cref="Control.Padding"/> (default 10×8).
/// </remarks>
public class Toolbar : ContentControl
{
    /// <summary>Identifies the <see cref="Elevation"/> property.</summary>
    public static readonly BindableProperty<float> ElevationProperty =
        BindableProperty.Register<Toolbar, float>(
            nameof(Elevation),
            2f,
            options: PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="BorderBrush"/> property.</summary>
    public static readonly BindableProperty<Color> BorderBrushProperty =
        BindableProperty.Register<Toolbar, Color>(
            nameof(BorderBrush),
            Color.Transparent,
            options: PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="BorderThickness"/> property.</summary>
    public static readonly BindableProperty<Thickness> BorderThicknessProperty =
        BindableProperty.Register<Toolbar, Thickness>(
            nameof(BorderThickness),
            Thickness.Zero,
            options: PropertyOptions.AffectsMeasure
        );

    /// <summary>
    /// Gets or sets the Material Design elevation level (casting ambient and key drop shadows). The default is 2.
    /// </summary>
    public float Elevation
    {
        get => GetValue(ElevationProperty);
        set => SetValue(ElevationProperty, value);
    }

    /// <summary>
    /// Gets or sets the border brush for the toolbar outline or bottom divider. The default is transparent.
    /// </summary>
    public Color BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the border thickness for the toolbar outline or bottom divider. The default is 0.
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

    /// <summary>Initializes a new, empty toolbar.</summary>
    public Toolbar()
    {
    }

    /// <summary>Initializes a new toolbar displaying <paramref name="content"/>.</summary>
    public Toolbar(object? content) : this()
    {
        Content = content;
    }

    // The border and the padding together.
    private Thickness GetContentInset()
    {
        var border = BorderThickness;
        var padding = Padding;
        return new Thickness(
            border.Left + padding.Left,
            border.Top + padding.Top,
            border.Right + padding.Right,
            border.Bottom + padding.Bottom);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var inset = GetContentInset();

        // A collapsed view measures to zero, so it needs no special case.
        if (CurrentView is { } view)
        {
            view.Measure(availableSize.Deflate(inset));
            return view.DesiredSize.Inflate(inset.Horizontal, inset.Vertical);
        }

        return new Size(inset.Horizontal, inset.Vertical);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        CurrentView?.Arrange(new Rect(Point.Zero, finalSize).Deflate(GetContentInset()));
        return finalSize;
    }
}
