using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Layout;

/// <summary>
/// Positions children at explicit coordinates relative to its edges, using the attached <see cref="LeftProperty"/>,
/// <see cref="TopProperty"/>, <see cref="RightProperty"/> and <see cref="BottomProperty"/>.
/// </summary>
/// <remarks>
/// <para>
/// Children are measured with unlimited space and always get their desired size. <c>Left</c> wins over <c>Right</c> and
/// <c>Top</c> over <c>Bottom</c>; a coordinate that is not set (<see cref="float.NaN"/>, the default) counts as 0 from
/// the left or top edge.
/// </para>
/// <para>
/// Like in WPF, a canvas desires no space itself, so give it a size (or stretch it) for its children to be visible
/// inside auto-sized parents. Children may extend beyond the canvas; set <see cref="UIElement.ClipToBounds"/> to clip them.
/// </para>
/// </remarks>
public class Canvas : Panel
{
    /// <summary>Identifies the attached distance between the canvas's left edge and the child's left edge.</summary>
    public static readonly BindableProperty<float> LeftProperty =
        BindableProperty.RegisterAttached<Canvas, UIElement, float>("Left", float.NaN, options: PropertyOptions.AffectsArrange, validateValue: IsValidCoordinate);

    /// <summary>Identifies the attached distance between the canvas's top edge and the child's top edge.</summary>
    public static readonly BindableProperty<float> TopProperty =
        BindableProperty.RegisterAttached<Canvas, UIElement, float>("Top", float.NaN, options: PropertyOptions.AffectsArrange, validateValue: IsValidCoordinate);

    /// <summary>Identifies the attached distance between the canvas's right edge and the child's right edge.</summary>
    public static readonly BindableProperty<float> RightProperty =
        BindableProperty.RegisterAttached<Canvas, UIElement, float>("Right", float.NaN, options: PropertyOptions.AffectsArrange, validateValue: IsValidCoordinate);

    /// <summary>Identifies the attached distance between the canvas's bottom edge and the child's bottom edge.</summary>
    public static readonly BindableProperty<float> BottomProperty =
        BindableProperty.RegisterAttached<Canvas, UIElement, float>("Bottom", float.NaN, options: PropertyOptions.AffectsArrange, validateValue: IsValidCoordinate);

    /// <summary>Sets the distance from the canvas's left edge to <paramref name="element"/>.</summary>
    public static void SetLeft(UIElement element, float value) => element.SetValue(LeftProperty, value);

    /// <summary>Gets the distance from the canvas's left edge to <paramref name="element"/>, or NaN if not set.</summary>
    public static float GetLeft(UIElement element) => element.GetValue(LeftProperty);

    /// <summary>Sets the distance from the canvas's top edge to <paramref name="element"/>.</summary>
    public static void SetTop(UIElement element, float value) => element.SetValue(TopProperty, value);

    /// <summary>Gets the distance from the canvas's top edge to <paramref name="element"/>, or NaN if not set.</summary>
    public static float GetTop(UIElement element) => element.GetValue(TopProperty);

    /// <summary>Sets the distance from the canvas's right edge to <paramref name="element"/>'s right edge.</summary>
    public static void SetRight(UIElement element, float value) => element.SetValue(RightProperty, value);

    /// <summary>Gets the distance from the canvas's right edge to <paramref name="element"/>'s right edge, or NaN if not set.</summary>
    public static float GetRight(UIElement element) => element.GetValue(RightProperty);

    /// <summary>Sets the distance from the canvas's bottom edge to <paramref name="element"/>'s bottom edge.</summary>
    public static void SetBottom(UIElement element, float value) => element.SetValue(BottomProperty, value);

    /// <summary>Gets the distance from the canvas's bottom edge to <paramref name="element"/>'s bottom edge, or NaN if not set.</summary>
    public static float GetBottom(UIElement element) => element.GetValue(BottomProperty);

    // NaN means "not set"; infinite coordinates are rejected.
    private static bool IsValidCoordinate(float value) => !float.IsInfinity(value);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var unlimited = new Size(float.PositiveInfinity, float.PositiveInfinity);
        var children = Children;
        for (int i = 0; i < children.Count; i++)
        {
            (children[i] as UIElement)?.Measure(unlimited);
        }
        return Size.Zero;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
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

            var desired = child.DesiredSize;
            float x = GetLeft(child);
            if (float.IsNaN(x))
            {
                float right = GetRight(child);
                x = float.IsNaN(right) ? 0 : finalSize.Width - right - desired.Width;
            }

            float y = GetTop(child);
            if (float.IsNaN(y))
            {
                float bottom = GetBottom(child);
                y = float.IsNaN(bottom) ? 0 : finalSize.Height - bottom - desired.Height;
            }

            child.Arrange(new Rect(x, y, desired.Width, desired.Height));
        }
        return finalSize;
    }
}
