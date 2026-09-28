using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a <see cref="Badge"/> over the top-right corner of its content, after the content: the MD3 small badge (a 6 px
/// error dot) or large badge (a 16 px high error pill with the label in label-small on-error text).
/// </summary>
/// <remarks><see cref="Badge.BadgeBackground"/> and <see cref="Badge.BadgeForeground"/>, when set, replace the colors.</remarks>
/// <param name="colors">The color scheme.</param>
public class MaterialBadgeRenderer(MaterialColorScheme colors) : ControlRenderer<Badge>
{
    /// <inheritdoc/>
    public override void RenderOverlay(Badge badge, ref DrawingContext context)
    {
        if (!badge.IsBadgeShown) return;

        var bounds = badge.BadgeBounds;
        Color background = badge.BadgeBackground.A > 0 ? badge.BadgeBackground : colors.Error;
        context.DrawRoundedRect(bounds, new CornerRadius(bounds.Height * 0.5f), background);

        if (badge.Label is { } label)
        {
            Color foreground = badge.BadgeForeground.A > 0 ? badge.BadgeForeground : colors.OnError;
            float width = context.MeasureText(label, Badge.LabelFontSize, badge.FontFamily, FontWeight.Medium).Width;
            float x = bounds.X + (bounds.Width - width) * 0.5f;
            float y = bounds.Y + (bounds.Height + Badge.LabelFontSize * 0.72f) * 0.5f;
            context.DrawText(label, new Point(x, y), foreground, Badge.LabelFontSize, badge.FontFamily, FontWeight.Medium);
        }
    }
}
