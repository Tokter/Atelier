using System;
using System.Collections.Generic;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Layout;

/// <summary>
/// Base class of layout containers: elements that position a collection of child elements.
/// </summary>
/// <remarks>
/// Derived panels implement <c>MeasureOverride</c> and <c>ArrangeOverride</c>. Margins, alignment and size limits of the
/// children are applied by <see cref="UIElement.Measure"/> and <see cref="UIElement.Arrange"/>, so panels only compute
/// each child's slot.
/// </remarks>
public abstract class Panel : UIElement
{
    /// <summary>Identifies the <see cref="Background"/> property.</summary>
    public static readonly BindableProperty<Color> BackgroundProperty =
        BindableProperty.Register<Panel, Color>(nameof(Background), Color.Transparent, options: PropertyOptions.AffectsRender);

    /// <summary>
    /// Gets or sets the color that fills the panel's bounds behind its children. The default is transparent.
    /// </summary>
    public Color Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>Adds <paramref name="element"/> as the last child.</summary>
    public void Add(UIElement element) => AddChild(element);

    /// <summary>Removes <paramref name="element"/> if it is a child of this panel.</summary>
    public void Remove(UIElement element) => RemoveChild(element);

    /// <summary>Removes all children.</summary>
    public void Clear() => ClearChildren();

    /// <summary>
    /// Gets the children that are <see cref="UIElement"/>s. Enumerating allocates an iterator; in hot paths index
    /// <see cref="VisualNode.Children"/> instead.
    /// </summary>
    public IEnumerable<UIElement> Elements
    {
        get
        {
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i] is UIElement ui)
                {
                    yield return ui;
                }
            }
        }
    }

    /// <summary>Validation callback for spacing properties: finite and not negative.</summary>
    internal static bool IsValidSpacing(float value) => float.IsFinite(value) && value >= 0;
}

/// <summary>
/// The direction in which a panel stacks or flows its children.
/// </summary>
public enum Orientation
{
    /// <summary>Top to bottom.</summary>
    Vertical,

    /// <summary>Left to right.</summary>
    Horizontal
}

/// <summary>
/// Arranges children in a single line, top to bottom (<see cref="Orientation.Vertical"/>, the default) or left to right
/// (<see cref="Orientation.Horizontal"/>), with an optional <see cref="Spacing"/> between them.
/// </summary>
/// <remarks>
/// Children get unlimited space along the stacking direction and the panel's space across it, so a vertical stack
/// measures its children with infinite height. Collapsed children take no space and no spacing.
/// </remarks>
public class StackPanel : Panel
{
    /// <summary>Identifies the <see cref="Orientation"/> property.</summary>
    public static readonly BindableProperty<Orientation> OrientationProperty =
        BindableProperty.Register<StackPanel, Orientation>(nameof(Orientation), Orientation.Vertical, options: PropertyOptions.AffectsMeasure);

    /// <summary>Identifies the <see cref="Spacing"/> property.</summary>
    public static readonly BindableProperty<float> SpacingProperty =
        BindableProperty.Register<StackPanel, float>(nameof(Spacing), 0f, options: PropertyOptions.AffectsMeasure, validateValue: IsValidSpacing);

    /// <summary>Gets or sets the stacking direction. The default is <see cref="Orientation.Vertical"/>.</summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Gets or sets the gap between adjacent visible children. The default is 0.</summary>
    public float Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        bool isHorizontal = Orientation == Orientation.Horizontal;
        var childConstraint = isHorizontal
            ? new Size(float.PositiveInfinity, availableSize.Height)
            : new Size(availableSize.Width, float.PositiveInfinity);

        float stacked = 0;
        float across = 0;
        int visibleCount = 0;

        var children = Children;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not UIElement child)
            {
                continue;
            }

            // Collapsed children are measured too (which just marks them measured) but take no space.
            child.Measure(childConstraint);
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            visibleCount++;
            var desired = child.DesiredSize;
            stacked += isHorizontal ? desired.Width : desired.Height;
            across = Math.Max(across, isHorizontal ? desired.Height : desired.Width);
        }

        if (visibleCount > 1)
        {
            stacked += Spacing * (visibleCount - 1);
        }

        return isHorizontal ? new Size(stacked, across) : new Size(across, stacked);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        bool isHorizontal = Orientation == Orientation.Horizontal;
        float spacing = Spacing;
        float offset = 0;

        var children = Children;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not UIElement child)
            {
                continue;
            }

            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(Rect.Zero);
                continue;
            }

            // Each child gets its desired length along the stack and the panel's full size across it; the child's
            // alignment then positions it within that slot.
            if (isHorizontal)
            {
                float width = child.DesiredSize.Width;
                child.Arrange(new Rect(offset, 0, width, finalSize.Height));
                offset += width + spacing;
            }
            else
            {
                float height = child.DesiredSize.Height;
                child.Arrange(new Rect(0, offset, finalSize.Width, height));
                offset += height + spacing;
            }
        }

        return finalSize;
    }
}
