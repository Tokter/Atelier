using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a <see cref="MenuItem"/> as an MD3 menu list item: an on-surface state layer across the item for hover, focus
/// and press (and while its submenu is open), a secondary-container background behind a checked item's icon, and the
/// access key underline while access keys are shown. A menu bar header gets a rounded state layer instead.
/// </summary>
/// <param name="colors">The color scheme.</param>
public class MaterialMenuItemRenderer(MaterialColorScheme colors) : ControlRenderer<MenuItem>, IContentColorProvider
{
    /// <inheritdoc/>
    public override void Render(MenuItem item, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, item.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        float opacity = !item.IsEnabled ? 0f
            : item.IsPressed ? MaterialState.PressedOpacity
            : item.IsSubmenuOpen ? (item.IsTopLevel ? MaterialState.PressedOpacity : MaterialState.HoverOpacity)
            : item.IsFocused && FocusManager.IsFocusVisible ? MaterialState.FocusOpacity
            : item.IsFocused || item.IsHovered ? MaterialState.HoverOpacity
            : 0f;

        if (item.IsTopLevel)
        {
            var shape = new Rect(0, 2, bounds.Width, bounds.Height - 4);
            MaterialDrawing.DrawStateLayer(ref context, shape, new CornerRadius(MaterialShape.ExtraSmall), colors.OnSurface, opacity);
        }
        else
        {
            MaterialDrawing.DrawStateLayer(ref context, bounds, CornerRadius.Zero, colors.OnSurface, opacity);
        }

        if (item.ShowsCheckBehindIcon)
        {
            var icon = item.IconBounds;
            var back = new Rect(icon.X - 4, icon.Y - 4, icon.Width + 8, icon.Height + 8);
            context.DrawRoundedRect(back, new CornerRadius(MaterialShape.Small), colors.SecondaryContainer);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Underlines the access key while <see cref="MenuManager.AccessKeysVisible"/>.</remarks>
    public override void RenderOverlay(MenuItem item, ref DrawingContext context)
    {
        if (!MenuManager.AccessKeysVisible || !item.HasExplicitAccessKey || item.HeaderTextBlock is not { } text) return;

        string content = text.Text;
        int index = item.AccessKeyIndex;
        if (index < 0 || index >= content.Length || text.Bounds.Width <= 0) return;

        var origin = text.GetTransformToAncestor(item).Translation;
        float size = text.FontSize;
        float start = context.MeasureText(content[..index], size, text.FontFamily, text.FontWeight).Width;
        float width = context.MeasureText(content[index].ToString(), size, text.FontFamily, text.FontWeight).Width;
        float baseline = origin.Y + (text.Bounds.Height + size * 0.72f) * 0.5f;
        Color color = item.IsEnabled ? colors.OnSurface : MaterialDrawing.DisabledContent(colors);
        context.DrawRect(new Rect(origin.X + start, baseline + 2f, width, 1f), color);
    }

    /// <inheritdoc/>
    public bool TryGetContentColor(UIElement element, out Color color)
    {
        color = element.IsEnabled ? colors.OnSurface : MaterialDrawing.DisabledContent(colors);
        return true;
    }
}

/// <summary>Draws a <see cref="Separator"/> as an MD3 divider: a 1 px outline-variant line.</summary>
/// <param name="colors">The color scheme.</param>
public class MaterialSeparatorRenderer(MaterialColorScheme colors) : ControlRenderer<Separator>
{
    /// <inheritdoc/>
    public override void Render(Separator separator, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, separator.Bounds.Size);
        if (bounds.Width <= 0) return;
        context.DrawRect(new Rect(0, 0, bounds.Width, 1f), colors.OutlineVariant);
    }
}
