using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a <see cref="ListBoxItem"/> like an MD3 list or menu item and supplies its content color: selected items get a
/// secondary-container background (content on-secondary-container), hover and press an on-surface state layer.
/// </summary>
/// <remarks>
/// Keyboard focus in the list (see <see cref="UIElement.IsFocusVisible"/>) draws an inward focus ring around the
/// selected item, the one keyboard navigation moves.
/// </remarks>
public class MaterialListBoxItemRenderer(MaterialColorScheme colors) : ControlRenderer<ListBoxItem>, IContentColorProvider
{
    /// <inheritdoc/>
    public override void Render(ListBoxItem item, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, item.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var corner = item.CornerRadius;
        if (item.IsSelected)
        {
            context.DrawRoundedRect(bounds, corner, item.IsEnabled ? colors.SecondaryContainer : MaterialDrawing.DisabledContainer(colors));
        }

        float opacity = !item.IsEnabled ? 0f
            : item.IsPressed ? MaterialState.PressedOpacity
            : item.IsHovered ? MaterialState.HoverOpacity
            : 0f;
        MaterialDrawing.DrawStateLayer(ref context, bounds, corner, ContentColorOf(item), opacity);

        if (item.IsFocusVisible || (item.IsSelected && item.ParentListBox?.IsFocusVisible == true))
        {
            MaterialDrawing.DrawFocusRing(ref context, bounds, corner, colors.Secondary, offset: -MaterialFocusRing.Width);
        }
    }

    /// <inheritdoc/>
    public bool TryGetContentColor(UIElement element, out Color color)
    {
        color = element is ListBoxItem item ? ContentColorOf(item) : colors.OnSurface;
        return element is ListBoxItem;
    }

    private Color ContentColorOf(ListBoxItem item) => item.IsSelected ? colors.OnSecondaryContainer : colors.OnSurface;
}
