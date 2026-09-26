using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Layout;

public class Canvas : Panel
{
    public static readonly BindableProperty<float> LeftProperty =
        BindableProperty.RegisterAttached<Canvas, UIElement, float>("Left", 0f, options: PropertyOptions.AffectsArrange);

    public static readonly BindableProperty<float> TopProperty =
        BindableProperty.RegisterAttached<Canvas, UIElement, float>("Top", 0f, options: PropertyOptions.AffectsArrange);

    public static void SetLeft(UIElement element, float value) => element.SetValue(LeftProperty, value);
    public static float GetLeft(UIElement element) => element.GetValue(LeftProperty);

    public static void SetTop(UIElement element, float value) => element.SetValue(TopProperty, value);
    public static float GetTop(UIElement element) => element.GetValue(TopProperty);

    protected override Size MeasureOverride(Size availableSize)
    {
        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is UIElement child)
            {
                child.Measure(new Size(float.PositiveInfinity, float.PositiveInfinity));
            }
        }
        return Size.Zero;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is UIElement child)
            {
                if (child.Visibility == Visibility.Collapsed)
                {
                    child.Arrange(Rect.Zero);
                }
                else
                {
                    float x = GetLeft(child);
                    float y = GetTop(child);
                    child.Arrange(new Rect(x, y, child.DesiredSize.Width, child.DesiredSize.Height));
                }
            }
        }
        return finalSize;
    }
}
