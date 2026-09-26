using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws the header row of a <see cref="TreeViewItem"/> like an MD3 list item and supplies its content color: the
/// selected node gets a secondary-container background (content on-secondary-container), a hovered header an on-surface
/// state layer, and keyboard focus in the tree an inward focus ring around the selected header.
/// </summary>
/// <remarks>
/// Every item reports a content color, so a child node's text uses its own item's state, never its parent's.
/// </remarks>
public class MaterialTreeViewItemRenderer(MaterialColorScheme colors) : ControlRenderer<TreeViewItem>, IContentColorProvider
{
    private static readonly CornerRadius DefaultCorner = new(MaterialShape.ExtraSmall);

    /// <inheritdoc/>
    public override void Render(TreeViewItem item, ref DrawingContext context)
    {
        var bounds = item.HeaderBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var corner = item.CornerRadius.TopLeft > 0 ? item.CornerRadius : DefaultCorner;

        if (item.IsSelected)
        {
            context.DrawRoundedRect(bounds, corner, item.IsEnabled ? colors.SecondaryContainer : MaterialDrawing.DisabledContainer(colors));
        }

        if (item.IsEnabled && item.IsHeaderHovered)
        {
            MaterialDrawing.DrawStateLayer(ref context, bounds, corner, ContentColorOf(item), MaterialState.HoverOpacity);
        }

        if (item.IsSelected && item.ParentTreeView?.IsFocusVisible == true)
        {
            MaterialDrawing.DrawFocusRing(ref context, bounds, corner, colors.Secondary, offset: -MaterialFocusRing.Width);
        }
    }

    /// <inheritdoc/>
    public bool TryGetContentColor(UIElement element, out Color color)
    {
        color = element is TreeViewItem item ? ContentColorOf(item) : colors.OnSurface;
        return element is TreeViewItem;
    }

    private Color ContentColorOf(TreeViewItem item) => item.IsSelected ? colors.OnSecondaryContainer : colors.OnSurface;
}
