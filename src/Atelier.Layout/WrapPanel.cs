using System;
using System.Collections.Generic;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Layout;

/// <summary>
/// Positions child elements in sequential position from left to right (Horizontal)
/// or top to bottom (Vertical), breaking content to the next line at the edge of the containing box.
/// </summary>
public class WrapPanel : Panel
{
    public static readonly BindableProperty<Orientation> OrientationProperty =
        BindableProperty.Register<WrapPanel, Orientation>(
            nameof(Orientation),
            Orientation.Horizontal,
            options: PropertyOptions.AffectsMeasure
        );

    public static readonly BindableProperty<float> ItemWidthProperty =
        BindableProperty.Register<WrapPanel, float>(
            nameof(ItemWidth),
            float.NaN,
            options: PropertyOptions.AffectsMeasure
        );

    public static readonly BindableProperty<float> ItemHeightProperty =
        BindableProperty.Register<WrapPanel, float>(
            nameof(ItemHeight),
            float.NaN,
            options: PropertyOptions.AffectsMeasure
        );

    public static readonly BindableProperty<float> HorizontalSpacingProperty =
        BindableProperty.Register<WrapPanel, float>(
            nameof(HorizontalSpacing),
            0f,
            options: PropertyOptions.AffectsMeasure
        );

    public static readonly BindableProperty<float> VerticalSpacingProperty =
        BindableProperty.Register<WrapPanel, float>(
            nameof(VerticalSpacing),
            0f,
            options: PropertyOptions.AffectsMeasure
        );

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public float ItemWidth
    {
        get => GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public float ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public float HorizontalSpacing
    {
        get => GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    public float VerticalSpacing
    {
        get => GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        bool isHoriz = Orientation == Orientation.Horizontal;
        float itemWidth = ItemWidth;
        float itemHeight = ItemHeight;
        bool hasItemWidth = !float.IsNaN(itemWidth) && itemWidth > 0;
        bool hasItemHeight = !float.IsNaN(itemHeight) && itemHeight > 0;

        float hSpacing = HorizontalSpacing;
        float vSpacing = VerticalSpacing;

        float maxMain = isHoriz ? availableSize.Width : availableSize.Height;
        if (float.IsInfinity(maxMain) || maxMain <= 0)
        {
            maxMain = float.PositiveInfinity;
        }

        float currentLineMain = 0;
        float currentLineCross = 0;
        int itemsOnLine = 0;

        float totalCross = 0;
        float maxMainUsed = 0;
        int lineCount = 0;

        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is not UIElement child || child.Visibility == Visibility.Collapsed)
                continue;

            Size childConstraint = new(
                hasItemWidth ? itemWidth : (isHoriz ? availableSize.Width : float.PositiveInfinity),
                hasItemHeight ? itemHeight : (isHoriz ? float.PositiveInfinity : availableSize.Height)
            );

            child.Measure(childConstraint);

            float childMain = isHoriz
                ? (hasItemWidth ? itemWidth : child.DesiredSize.Width)
                : (hasItemHeight ? itemHeight : child.DesiredSize.Height);

            float childCross = isHoriz
                ? (hasItemHeight ? itemHeight : child.DesiredSize.Height)
                : (hasItemWidth ? itemWidth : child.DesiredSize.Width);

            float spacing = itemsOnLine > 0 ? (isHoriz ? hSpacing : vSpacing) : 0;

            if (itemsOnLine > 0 && currentLineMain + spacing + childMain > maxMain)
            {
                // Wrap to next line
                totalCross += (lineCount > 0 ? (isHoriz ? vSpacing : hSpacing) : 0) + currentLineCross;
                maxMainUsed = Math.Max(maxMainUsed, currentLineMain);
                lineCount++;

                currentLineMain = childMain;
                currentLineCross = childCross;
                itemsOnLine = 1;
            }
            else
            {
                currentLineMain += spacing + childMain;
                currentLineCross = Math.Max(currentLineCross, childCross);
                itemsOnLine++;
            }
        }

        if (itemsOnLine > 0)
        {
            totalCross += (lineCount > 0 ? (isHoriz ? vSpacing : hSpacing) : 0) + currentLineCross;
            maxMainUsed = Math.Max(maxMainUsed, currentLineMain);
        }

        return isHoriz
            ? new Size(maxMainUsed, totalCross)
            : new Size(totalCross, maxMainUsed);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        bool isHoriz = Orientation == Orientation.Horizontal;
        float itemWidth = ItemWidth;
        float itemHeight = ItemHeight;
        bool hasItemWidth = !float.IsNaN(itemWidth) && itemWidth > 0;
        bool hasItemHeight = !float.IsNaN(itemHeight) && itemHeight > 0;

        float hSpacing = HorizontalSpacing;
        float vSpacing = VerticalSpacing;

        float maxMain = isHoriz ? finalSize.Width : finalSize.Height;
        if (float.IsInfinity(maxMain) || maxMain <= 0)
        {
            maxMain = float.PositiveInfinity;
        }

        float currentCross = 0;
        int lineStart = 0;
        float lineMain = 0;
        float lineCross = 0;
        int itemsOnLine = 0;

        List<UIElement> visibleChildren = new(Children.Count);
        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is UIElement child && child.Visibility != Visibility.Collapsed)
            {
                visibleChildren.Add(child);
            }
            else if (Children[i] is UIElement collapsedChild)
            {
                collapsedChild.Arrange(Rect.Zero);
            }
        }

        for (int i = 0; i < visibleChildren.Count; i++)
        {
            UIElement child = visibleChildren[i];

            float childMain = isHoriz
                ? (hasItemWidth ? itemWidth : child.DesiredSize.Width)
                : (hasItemHeight ? itemHeight : child.DesiredSize.Height);

            float childCross = isHoriz
                ? (hasItemHeight ? itemHeight : child.DesiredSize.Height)
                : (hasItemWidth ? itemWidth : child.DesiredSize.Width);

            float spacing = itemsOnLine > 0 ? (isHoriz ? hSpacing : vSpacing) : 0;

            if (itemsOnLine > 0 && lineMain + spacing + childMain > maxMain)
            {
                // Arrange previous line
                ArrangeLine(visibleChildren, lineStart, i, currentCross, lineCross, isHoriz, hasItemWidth, hasItemHeight, itemWidth, itemHeight, isHoriz ? hSpacing : vSpacing);
                currentCross += lineCross + (isHoriz ? vSpacing : hSpacing);

                lineStart = i;
                lineMain = childMain;
                lineCross = childCross;
                itemsOnLine = 1;
            }
            else
            {
                lineMain += spacing + childMain;
                lineCross = Math.Max(lineCross, childCross);
                itemsOnLine++;
            }
        }

        if (itemsOnLine > 0)
        {
            ArrangeLine(visibleChildren, lineStart, visibleChildren.Count, currentCross, lineCross, isHoriz, hasItemWidth, hasItemHeight, itemWidth, itemHeight, isHoriz ? hSpacing : vSpacing);
        }

        return finalSize;
    }

    private static void ArrangeLine(
        List<UIElement> children,
        int start,
        int end,
        float crossOffset,
        float lineCross,
        bool isHoriz,
        bool hasItemWidth,
        bool hasItemHeight,
        float fixedWidth,
        float fixedHeight,
        float lineSpacing)
    {
        float mainOffset = 0;

        for (int i = start; i < end; i++)
        {
            UIElement child = children[i];
            float childWidth = hasItemWidth ? fixedWidth : child.DesiredSize.Width;
            float childHeight = hasItemHeight ? fixedHeight : child.DesiredSize.Height;

            if (isHoriz)
            {
                child.Arrange(new Rect(mainOffset, crossOffset, childWidth, hasItemHeight ? fixedHeight : lineCross));
                mainOffset += childWidth + lineSpacing;
            }
            else
            {
                child.Arrange(new Rect(crossOffset, mainOffset, hasItemWidth ? fixedWidth : lineCross, childHeight));
                mainOffset += childHeight + lineSpacing;
            }
        }
    }
}
