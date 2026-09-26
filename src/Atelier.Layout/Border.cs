using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Layout;

/// <summary>
/// Draws a background, an outline and an optional shadow around a single <see cref="Child"/>, which it insets by
/// <see cref="BorderThickness"/> plus <see cref="Padding"/>.
/// </summary>
/// <remarks>
/// Drawing is done by the active theme's renderer. With <see cref="UIElement.ClipToBounds"/> set, the child is clipped to
/// the rounded <see cref="CornerRadius"/>.
/// </remarks>
public class Border : UIElement
{
    /// <summary>Identifies the <see cref="Background"/> property.</summary>
    public static readonly BindableProperty<Color> BackgroundProperty =
        BindableProperty.Register<Border, Color>(nameof(Background), Color.Transparent, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="BorderBrush"/> property.</summary>
    public static readonly BindableProperty<Color> BorderBrushProperty =
        BindableProperty.Register<Border, Color>(nameof(BorderBrush), Color.Transparent, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="BorderThickness"/> property.</summary>
    public static readonly BindableProperty<Thickness> BorderThicknessProperty =
        BindableProperty.Register<Border, Thickness>(nameof(BorderThickness), Thickness.Zero, options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="CornerRadius"/> property.</summary>
    public static readonly BindableProperty<CornerRadius> CornerRadiusProperty =
        BindableProperty.Register<Border, CornerRadius>(nameof(CornerRadius), CornerRadius.Zero, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="Padding"/> property.</summary>
    public static readonly BindableProperty<Thickness> PaddingProperty =
        BindableProperty.Register<Border, Thickness>(nameof(Padding), Thickness.Zero, options: PropertyOptions.AffectsMeasure);

    /// <summary>Identifies the <see cref="Elevation"/> property.</summary>
    public static readonly BindableProperty<float> ElevationProperty =
        BindableProperty.Register<Border, float>(nameof(Elevation), 0f, options: PropertyOptions.AffectsRender);

    /// <summary>Gets or sets the fill behind the child. The default is transparent.</summary>
    public Color Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>Gets or sets the outline color. The default is transparent.</summary>
    public Color BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    /// <summary>Gets or sets the outline thickness, which also insets the child.</summary>
    public Thickness BorderThickness { get => GetValue(BorderThicknessProperty); set => SetValue(BorderThicknessProperty, value); }

    /// <summary>Gets or sets the radii of the corners.</summary>
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty, value); }

    /// <summary>Gets or sets the space between the outline and the child.</summary>
    public Thickness Padding { get => GetValue(PaddingProperty); set => SetValue(PaddingProperty, value); }

    /// <summary>Gets or sets the shadow depth; 0 (the default) draws no shadow.</summary>
    public float Elevation { get => GetValue(ElevationProperty); set => SetValue(ElevationProperty, value); }

    /// <summary>Gets or sets the single child. Setting it replaces the current child.</summary>
    public UIElement? Child
    {
        get => Children.Count > 0 ? Children[0] as UIElement : null;
        set
        {
            if (ReferenceEquals(Child, value) && Children.Count <= 1)
            {
                return;
            }

            ClearChildren();
            if (value != null)
            {
                AddChild(value);
            }
        }
    }

    // Border thickness and padding together: the space between the border's bounds and the child.
    private Thickness Inset
    {
        get
        {
            var border = BorderThickness;
            var padding = Padding;
            return new Thickness(
                border.Left + padding.Left,
                border.Top + padding.Top,
                border.Right + padding.Right,
                border.Bottom + padding.Bottom);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var inset = Inset;
        var child = Child;
        if (child == null)
        {
            return new Size(inset.Horizontal, inset.Vertical);
        }

        // A collapsed child measures as zero, so the border then desires just its inset.
        child.Measure(availableSize.Deflate(inset));
        return child.DesiredSize.Inflate(inset.Horizontal, inset.Vertical);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var child = Child;
        if (child != null)
        {
            child.Arrange(child.Visibility == Visibility.Collapsed
                ? Rect.Zero
                : new Rect(Point.Zero, finalSize).Deflate(Inset));
        }

        return finalSize;
    }
}
