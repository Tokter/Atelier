using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Layout;

public enum Dock
{
    Left,
    Top,
    Right,
    Bottom
}

public class DockPanel : Panel
{
    public static readonly BindableProperty<Dock> DockProperty =
        BindableProperty.RegisterAttached<DockPanel, UIElement, Dock>("Dock", Dock.Left, options: PropertyOptions.AffectsMeasure);

    public static readonly BindableProperty<bool> LastChildFillProperty =
        BindableProperty.Register<DockPanel, bool>(nameof(LastChildFill), true, options: PropertyOptions.AffectsArrange);

    public static void SetDock(UIElement element, Dock value) => element.SetValue(DockProperty, value);
    public static Dock GetDock(UIElement element) => element.GetValue(DockProperty);

    public bool LastChildFill
    {
        get => GetValue(LastChildFillProperty);
        set => SetValue(LastChildFillProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        float parentWidth = 0f;
        float parentHeight = 0f;
        float accumulatedWidth = 0f;
        float accumulatedHeight = 0f;

        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is not UIElement child)
                continue;

            if (child.Visibility == Visibility.Collapsed)
            {
                child.Measure(availableSize);
                continue;
            }

            var childConstraint = new Size(
                Math.Max(0, availableSize.Width - accumulatedWidth),
                Math.Max(0, availableSize.Height - accumulatedHeight));

            child.Measure(childConstraint);
            var childDesired = child.DesiredSize;

            switch (GetDock(child))
            {
                case Dock.Left:
                case Dock.Right:
                    parentHeight = Math.Max(parentHeight, accumulatedHeight + childDesired.Height);
                    accumulatedWidth += childDesired.Width;
                    break;
                case Dock.Top:
                case Dock.Bottom:
                    parentWidth = Math.Max(parentWidth, accumulatedWidth + childDesired.Width);
                    accumulatedHeight += childDesired.Height;
                    break;
            }
        }

        return new Size(
            Math.Max(parentWidth, accumulatedWidth),
            Math.Max(parentHeight, accumulatedHeight));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        float left = 0;
        float top = 0;
        float right = finalSize.Width;
        float bottom = finalSize.Height;

        int count = Children.Count;
        for (int i = 0; i < count; i++)
        {
            if (Children[i] is not UIElement child)
                continue;

            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(Rect.Zero);
                continue;
            }

            if (i == count - 1 && LastChildFill)
            {
                child.Arrange(new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top)));
                break;
            }

            var dock = GetDock(child);
            var desired = child.DesiredSize;

            switch (dock)
            {
                case Dock.Left:
                    child.Arrange(new Rect(left, top, desired.Width, Math.Max(0, bottom - top)));
                    left += desired.Width;
                    break;
                case Dock.Right:
                    child.Arrange(new Rect(Math.Max(0, right - desired.Width), top, desired.Width, Math.Max(0, bottom - top)));
                    right -= desired.Width;
                    break;
                case Dock.Top:
                    child.Arrange(new Rect(left, top, Math.Max(0, right - left), desired.Height));
                    top += desired.Height;
                    break;
                case Dock.Bottom:
                    child.Arrange(new Rect(left, Math.Max(0, bottom - desired.Height), Math.Max(0, right - left), desired.Height));
                    bottom -= desired.Height;
                    break;
            }
        }

        return finalSize;
    }
}
