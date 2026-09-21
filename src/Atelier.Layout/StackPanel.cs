using System;
using System.Collections.Generic;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;

namespace Atelier.Layout;

public abstract class Panel : UIElement
{
    public void Add(UIElement element) => AddChild(element);
    public void Remove(UIElement element) => RemoveChild(element);
    public void Clear() => ClearChildren();

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
}

public enum Orientation
{
    Vertical,
    Horizontal
}

public class StackPanel : Panel
{
    public static readonly Core.Properties.BindableProperty<Orientation> OrientationProperty =
        Core.Properties.BindableProperty.Register<StackPanel, Orientation>(
            nameof(Orientation),
            Orientation.Vertical,
            (s, o, n) => ((StackPanel)s).InvalidateMeasure()
        );

    public static readonly Core.Properties.BindableProperty<float> SpacingProperty =
        Core.Properties.BindableProperty.Register<StackPanel, float>(
            nameof(Spacing),
            0f,
            (s, o, n) => ((StackPanel)s).InvalidateMeasure()
        );

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public float Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        float totalWidth = 0;
        float totalHeight = 0;
        bool isHoriz = Orientation == Orientation.Horizontal;
        float spacing = Spacing;
        int visibleCount = 0;

        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is not UIElement child)
                continue;

            if (child.Visibility == Visibility.Collapsed)
            {
                child.Measure(availableSize);
                continue;
            }

            visibleCount++;

            if (isHoriz)
            {
                child.Measure(new Size(float.PositiveInfinity, availableSize.Height));
                totalWidth += child.DesiredSize.Width;
                totalHeight = Math.Max(totalHeight, child.DesiredSize.Height);
            }
            else
            {
                child.Measure(new Size(availableSize.Width, float.PositiveInfinity));
                totalWidth = Math.Max(totalWidth, child.DesiredSize.Width);
                totalHeight += child.DesiredSize.Height;
            }
        }

        if (visibleCount > 1)
        {
            if (isHoriz)
                totalWidth += spacing * (visibleCount - 1);
            else
                totalHeight += spacing * (visibleCount - 1);
        }

        return new Size(totalWidth, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        bool isHoriz = Orientation == Orientation.Horizontal;
        float offset = 0;
        float spacing = Spacing;

        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is not UIElement child)
                continue;

            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(Rect.Zero);
                continue;
            }

            if (isHoriz)
            {
                float childWidth = child.DesiredSize.Width;
                child.Arrange(new Rect(offset, 0, childWidth, finalSize.Height));
                offset += childWidth + spacing;
            }
            else
            {
                float childHeight = child.DesiredSize.Height;
                child.Arrange(new Rect(0, offset, finalSize.Width, childHeight));
                offset += childHeight + spacing;
            }
        }

        return finalSize;
    }
}
