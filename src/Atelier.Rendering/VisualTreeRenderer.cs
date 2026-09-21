using System;
using System.Numerics;
using SkiaSharp;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;

namespace Atelier.Rendering;

public interface IElementVisualPresenter
{
    void Render(UIElement element, ref DrawingContext context);
    void RenderOverlay(UIElement element, ref DrawingContext context) { }
}

public static class VisualTreeRenderer
{
    public static void Render(UIElement root, ref DrawingContext context, IElementVisualPresenter? presenter = null)
    {
        if (root.Visibility != Visibility.Visible || root.Opacity <= 0)
            return;

        RenderElement(root, ref context, presenter, isRoot: true);
    }

    private static void RenderElement(UIElement element, ref DrawingContext context, IElementVisualPresenter? presenter, bool isRoot)
    {
        if (element.Visibility != Visibility.Visible || element.Opacity <= 0)
            return;

        // Overlay elements (e.g. Popups) are rendered exclusively in the overlay layer, not in-flow with parent
        if (!isRoot && element.IsOverlayElement)
            return;

        // Position translation & 2D transformation
        Matrix3x2 transform = isRoot
            ? element.GetEffectiveTransform()
            : element.GetLocalTransform();

        using var transformScope = context.PushTransform(transform);

        int clipSave = -1;
        if (element.ClipToBounds && element.Bounds.Width > 0 && element.Bounds.Height > 0)
        {
            clipSave = context.Canvas.Save();
            context.Canvas.ClipRect(new SKRect(0, 0, element.Bounds.Width, element.Bounds.Height), SKClipOperation.Intersect, antialias: true);
        }

        try
        {
            // Delegate rendering of the element to the presenter (Theme renderer)
            presenter?.Render(element, ref context);

            // Render children
            int count = element.Children.Count;
            for (int i = 0; i < count; i++)
            {
                if (element.Children[i] is UIElement child)
                {
                    RenderElement(child, ref context, presenter, isRoot: false);
                }
            }

            // Render overlay on top of children (e.g. scrollbars)
            presenter?.RenderOverlay(element, ref context);
        }
        finally
        {
            if (clipSave >= 0)
            {
                context.Canvas.RestoreToCount(clipSave);
            }
        }
    }
}
