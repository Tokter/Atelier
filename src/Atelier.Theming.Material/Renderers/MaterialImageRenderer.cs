using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws an <see cref="Image"/>'s source inside its padding, scaled per <see cref="Image.Stretch"/> and
/// <see cref="Image.StretchDirection"/> and clipped to the padded area. The element's opacity is applied by the tree
/// renderer, not here.
/// </summary>
public class MaterialImageRenderer : ControlRenderer<Image>
{
    /// <inheritdoc/>
    public override void Render(Image element, ref DrawingContext context)
    {
        var source = element.Source;
        if (source == null || element.Bounds.Width <= 0 || element.Bounds.Height <= 0) return;

        var padding = element.Padding;
        var content = new Rect(
            padding.Left,
            padding.Top,
            Math.Max(0f, element.Bounds.Width - padding.Horizontal),
            Math.Max(0f, element.Bounds.Height - padding.Vertical));
        if (content.Width <= 0 || content.Height <= 0) return;

        Rect destRect = ComputeDestRect(content, new Size(source.Width, source.Height), element.Stretch, element.StretchDirection,
            element.HorizontalAlignment, element.VerticalAlignment);

        bool overflows = destRect.Left < content.Left - 0.01f || destRect.Top < content.Top - 0.01f ||
                         destRect.Right > content.Right + 0.01f || destRect.Bottom > content.Bottom + 0.01f;

        // Opacity 1: VisualTreeRenderer already applies element.Opacity through a layer.
        if (overflows)
        {
            using (context.PushClip(content))
            {
                context.DrawImage(source, destRect);
            }
        }
        else
        {
            context.DrawImage(source, destRect);
        }
    }

    private static Rect ComputeDestRect(Rect content, Size sourceSize, Stretch stretch, StretchDirection direction,
        HorizontalAlignment hAlign, VerticalAlignment vAlign)
    {
        if (sourceSize.IsEmpty) return Rect.Zero;

        var scale = Image.ComputeScale(new Size(content.Width, content.Height), sourceSize, stretch, direction);
        float drawW = sourceSize.Width * scale.Width;
        float drawH = sourceSize.Height * scale.Height;

        float x = hAlign switch
        {
            HorizontalAlignment.Center or HorizontalAlignment.Stretch => (content.Width - drawW) * 0.5f,
            HorizontalAlignment.Right => content.Width - drawW,
            _ => 0f
        };

        float y = vAlign switch
        {
            VerticalAlignment.Center or VerticalAlignment.Stretch => (content.Height - drawH) * 0.5f,
            VerticalAlignment.Bottom => content.Height - drawH,
            _ => 0f
        };

        return new Rect(content.X + x, content.Y + y, drawW, drawH);
    }
}
