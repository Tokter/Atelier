using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a <see cref="TabControl"/>'s strip. Primary and secondary tabs (MD3) get a 1 px outline-variant divider under
/// the strip and the primary indicator (3 px with rounded top corners under the label, or 2 px under the whole tab),
/// drawn over the tabs as it slides. Browser tabs get a surface-container strip over a surface page.
/// </summary>
/// <param name="colors">The color scheme.</param>
public class MaterialTabControlRenderer(MaterialColorScheme colors) : ControlRenderer<TabControl>
{
    /// <inheritdoc/>
    public override void Render(TabControl tabs, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, tabs.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var strip = tabs.StripBounds;
        if (tabs.TabStyle == TabStyle.Browser)
        {
            var corner = tabs.CornerRadius;
            context.DrawRoundedRect(bounds, corner, colors.Surface);
            context.DrawRoundedRect(strip, new CornerRadius(corner.TopLeft, corner.TopRight, 0, 0), colors.SurfaceContainer);
        }
        else
        {
            context.DrawRect(new Rect(strip.X, strip.Bottom - 1f, strip.Width, 1f), colors.OutlineVariant);
        }
    }

    /// <inheritdoc/>
    public override void RenderOverlay(TabControl tabs, ref DrawingContext context)
    {
        var indicator = tabs.IndicatorBounds;
        if (indicator.Width <= 0) return;

        using var clip = context.PushClip(tabs.TabsViewportBounds);
        var corner = tabs.TabStyle == TabStyle.Primary ? new CornerRadius(3, 3, 0, 0) : CornerRadius.Zero;
        context.DrawRoundedRect(indicator, corner, tabs.IsEnabled ? colors.Primary : MaterialDrawing.DisabledContent(colors));
    }
}

/// <summary>
/// Draws a <see cref="TabItem"/>: for MD3 tabs a state layer over the tab and the label color (primary or on-surface
/// when selected, on-surface-variant otherwise); for browser tabs the selected tab as a surface shape with rounded top
/// corners joining the page, and a rounded hover layer on the others. A dragged tab is lifted with a shadow.
/// </summary>
/// <param name="colors">The color scheme.</param>
public class MaterialTabItemRenderer(MaterialColorScheme colors) : ControlRenderer<TabItem>, IContentColorProvider
{
    private const float BrowserCorner = 8f;

    /// <inheritdoc/>
    public override void Render(TabItem tab, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, tab.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var style = tab.Owner?.TabStyle ?? TabStyle.Primary;
        float opacity = MaterialDrawing.StateLayerOpacity(tab);

        if (style == TabStyle.Browser)
        {
            var shape = new Rect(bounds.X + 2, bounds.Y + 4, bounds.Width - 4, bounds.Height - 4);
            var corner = new CornerRadius(BrowserCorner, BrowserCorner, 0, 0);
            if (tab.IsDragging)
            {
                context.DrawShadow(shape, corner, MaterialElevation.Level2, colors.Shadow);
            }
            if (tab.IsSelected || tab.IsDragging)
            {
                context.DrawRoundedRect(shape, corner, tab.IsSelected ? colors.Surface : colors.SurfaceContainerHigh);
            }
            else
            {
                MaterialDrawing.DrawStateLayer(ref context, shape, corner, colors.OnSurface, opacity);
            }
            if (tab.IsFocusVisible)
            {
                MaterialDrawing.DrawFocusRing(ref context, shape, corner, colors.Secondary, offset: -3f);
            }
            return;
        }

        if (tab.IsDragging)
        {
            context.DrawShadow(bounds, CornerRadius.Zero, MaterialElevation.Level2, colors.Shadow);
            context.DrawRect(bounds, colors.SurfaceContainer);
        }
        MaterialDrawing.DrawStateLayer(ref context, bounds, CornerRadius.Zero, GetContentColor(tab), opacity);
        if (tab.IsFocusVisible)
        {
            MaterialDrawing.DrawFocusRing(ref context, bounds, new CornerRadius(4), colors.Secondary, offset: -3f);
        }
    }

    private Color GetContentColor(TabItem tab)
    {
        if (!tab.IsEnabled) return MaterialDrawing.DisabledContent(colors);
        if (!tab.IsSelected) return colors.OnSurfaceVariant;
        return tab.Owner?.TabStyle == TabStyle.Primary ? colors.Primary : colors.OnSurface;
    }

    /// <inheritdoc/>
    public bool TryGetContentColor(UIElement element, out Color color)
    {
        color = GetContentColor((TabItem)element);
        return true;
    }
}
