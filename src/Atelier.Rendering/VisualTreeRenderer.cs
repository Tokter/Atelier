using System;
using System.Numerics;
using SkiaSharp;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;

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

        int opacitySave = -1;
        if (element.Opacity < 1.0f && element.Opacity > 0f)
        {
            opacitySave = context.SaveOpacityLayer(element.Opacity);
        }

        // Delegate rendering of the element itself (background, border, shadow) to the presenter
        // This ensures drop shadows cast outside bounds are not clipped by the element's own ClipToBounds
        presenter?.Render(element, ref context);

        int clipSave = -1;
        if (element.ClipToBounds && element.Bounds.Width > 0 && element.Bounds.Height > 0)
        {
            clipSave = context.Canvas.Save();

            if (element is Border border &&
                (border.CornerRadius.TopLeft > 0 || border.CornerRadius.TopRight > 0 ||
                 border.CornerRadius.BottomRight > 0 || border.CornerRadius.BottomLeft > 0))
            {
                // Children of a rounded border are clipped to its rounded shape (reusing a cached round rect).
                context.ClipRoundedRect(new Rect(0, 0, element.Bounds.Width, element.Bounds.Height), border.CornerRadius);
            }
            else
            {
                context.Canvas.ClipRect(new SKRect(0, 0, element.Bounds.Width, element.Bounds.Height), SKClipOperation.Intersect, antialias: true);
            }
        }

        try
        {
            // Render children (clipped to bounds/corners if ClipToBounds is enabled)
            var children = element.Children;
            for (int i = 0; i < children.Count; i++)
            {
                UIElement? child = null;
                try
                {
                    if (i < children.Count)
                    {
                        child = children[i] as UIElement;
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    break;
                }

                if (child != null)
                {
                    RenderElement(child, ref context, presenter, isRoot: false);
                }
            }
        }
        finally
        {
            if (clipSave >= 0)
            {
                context.Canvas.RestoreToCount(clipSave);
            }

            // Render overlay on top of children (e.g. scrollbars) within the element's opacity layer
            presenter?.RenderOverlay(element, ref context);

            if (opacitySave >= 0)
            {
                context.Canvas.RestoreToCount(opacitySave);
            }
        }
    }
}
