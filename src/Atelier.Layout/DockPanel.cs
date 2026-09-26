using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Layout;

/// <summary>
/// The edge of a <see cref="DockPanel"/> a child is docked to.
/// </summary>
public enum Dock
{
    /// <summary>Docked to the left edge, taking the full remaining height.</summary>
    Left,

    /// <summary>Docked to the top edge, taking the full remaining width.</summary>
    Top,

    /// <summary>Docked to the right edge, taking the full remaining height.</summary>
    Right,

    /// <summary>Docked to the bottom edge, taking the full remaining width.</summary>
    Bottom
}

/// <summary>
/// Docks children to its edges in order: each child takes a strip along the edge given by the attached
/// <see cref="DockProperty"/> from the space the previous children left, and with <see cref="LastChildFill"/> the last
/// child fills whatever remains.
/// </summary>
/// <remarks>
/// Order matters: a child docked to the top before a child docked to the left spans the full width, while one docked
/// after it spans only the width to the right of the left child. <see cref="HorizontalSpacing"/> separates a left or
/// right child from the next child, <see cref="VerticalSpacing"/> a top or bottom child. Docked children are never
/// larger than the space left, so they don't overlap when the panel is too small.
/// </remarks>
public class DockPanel : Panel
{
    /// <summary>Identifies the attached edge a child is docked to (default <see cref="Dock.Left"/>).</summary>
    public static readonly BindableProperty<Dock> DockProperty =
        BindableProperty.RegisterAttached<DockPanel, UIElement, Dock>("Dock", Dock.Left, options: PropertyOptions.AffectsMeasure);

    /// <summary>Identifies the <see cref="LastChildFill"/> property.</summary>
    public static readonly BindableProperty<bool> LastChildFillProperty =
        BindableProperty.Register<DockPanel, bool>(nameof(LastChildFill), true, options: PropertyOptions.AffectsArrange);

    /// <summary>Identifies the <see cref="HorizontalSpacing"/> property.</summary>
    public static readonly BindableProperty<float> HorizontalSpacingProperty =
        BindableProperty.Register<DockPanel, float>(nameof(HorizontalSpacing), 0f, options: PropertyOptions.AffectsMeasure, validateValue: IsValidSpacing);

    /// <summary>Identifies the <see cref="VerticalSpacing"/> property.</summary>
    public static readonly BindableProperty<float> VerticalSpacingProperty =
        BindableProperty.Register<DockPanel, float>(nameof(VerticalSpacing), 0f, options: PropertyOptions.AffectsMeasure, validateValue: IsValidSpacing);

    /// <summary>Sets the edge <paramref name="element"/> is docked to.</summary>
    public static void SetDock(UIElement element, Dock value) => element.SetValue(DockProperty, value);

    /// <summary>Gets the edge <paramref name="element"/> is docked to.</summary>
    public static Dock GetDock(UIElement element) => element.GetValue(DockProperty);

    /// <summary>
    /// Gets or sets whether the last child fills the space left by the others (ignoring its <see cref="DockProperty"/>).
    /// The default is <c>true</c>.
    /// </summary>
    public bool LastChildFill
    {
        get => GetValue(LastChildFillProperty);
        set => SetValue(LastChildFillProperty, value);
    }

    /// <summary>Gets or sets the gap after a child docked to the left or right. The default is 0.</summary>
    public float HorizontalSpacing
    {
        get => GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    /// <summary>Gets or sets the gap after a child docked to the top or bottom. The default is 0.</summary>
    public float VerticalSpacing
    {
        get => GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        float horizontalSpacing = HorizontalSpacing;
        float verticalSpacing = VerticalSpacing;
        int lastVisible = LastVisibleIndex();

        // usedWidth/usedHeight: the strips taken by the children so far. maxWidth/maxHeight: the extent needed across
        // those strips (a top child's width plus the left/right strips beside it, and so on).
        float usedWidth = 0, usedHeight = 0;
        float maxWidth = 0, maxHeight = 0;

        var children = Children;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not UIElement child)
            {
                continue;
            }

            child.Measure(new Size(
                Math.Max(0, availableSize.Width - usedWidth),
                Math.Max(0, availableSize.Height - usedHeight)));
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var desired = child.DesiredSize;
            bool hasNext = i < lastVisible;
            switch (GetDock(child))
            {
                case Dock.Left:
                case Dock.Right:
                    maxHeight = Math.Max(maxHeight, usedHeight + desired.Height);
                    usedWidth += desired.Width + (hasNext ? horizontalSpacing : 0);
                    break;
                case Dock.Top:
                case Dock.Bottom:
                    maxWidth = Math.Max(maxWidth, usedWidth + desired.Width);
                    usedHeight += desired.Height + (hasNext ? verticalSpacing : 0);
                    break;
            }
        }

        return new Size(Math.Max(maxWidth, usedWidth), Math.Max(maxHeight, usedHeight));
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        float horizontalSpacing = HorizontalSpacing;
        float verticalSpacing = VerticalSpacing;
        int lastVisible = LastVisibleIndex();

        var children = Children;
        // As in WPF, the fill child is the last child, even if it is collapsed (then nothing fills).
        int fillIndex = LastChildFill ? children.Count - 1 : -1;

        float left = 0, top = 0, right = finalSize.Width, bottom = finalSize.Height;
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

            float remainingWidth = Math.Max(0, right - left);
            float remainingHeight = Math.Max(0, bottom - top);
            if (i == fillIndex)
            {
                child.Arrange(new Rect(left, top, remainingWidth, remainingHeight));
                break;
            }

            var desired = child.DesiredSize;
            bool hasNext = i < lastVisible;
            switch (GetDock(child))
            {
                case Dock.Left:
                {
                    float width = Math.Min(desired.Width, remainingWidth);
                    child.Arrange(new Rect(left, top, width, remainingHeight));
                    left += width + (hasNext ? horizontalSpacing : 0);
                    break;
                }
                case Dock.Right:
                {
                    float width = Math.Min(desired.Width, remainingWidth);
                    child.Arrange(new Rect(right - width, top, width, remainingHeight));
                    right -= width + (hasNext ? horizontalSpacing : 0);
                    break;
                }
                case Dock.Top:
                {
                    float height = Math.Min(desired.Height, remainingHeight);
                    child.Arrange(new Rect(left, top, remainingWidth, height));
                    top += height + (hasNext ? verticalSpacing : 0);
                    break;
                }
                case Dock.Bottom:
                {
                    float height = Math.Min(desired.Height, remainingHeight);
                    child.Arrange(new Rect(left, bottom - height, remainingWidth, height));
                    bottom -= height + (hasNext ? verticalSpacing : 0);
                    break;
                }
            }
        }

        return finalSize;
    }

    // Spacing only separates children, so the last visible child gets none after it.
    private int LastVisibleIndex()
    {
        var children = Children;
        for (int i = children.Count - 1; i >= 0; i--)
        {
            if (children[i] is UIElement { Visibility: not Visibility.Collapsed })
            {
                return i;
            }
        }
        return -1;
    }
}
