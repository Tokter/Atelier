using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a <see cref="DataGrid"/>: a surface body, a surface-container header area with an outline-variant divider under
/// it, an outline-variant border, and the primary drop marker while a column header is dragged.
/// </summary>
/// <param name="colors">The color scheme.</param>
public class MaterialDataGridRenderer(MaterialColorScheme colors) : ControlRenderer<DataGrid>
{
    /// <inheritdoc/>
    public override void Render(DataGrid grid, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, grid.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var corner = grid.CornerRadius;
        context.DrawRoundedRect(bounds, corner, colors.Surface);
        float header = grid.HeaderAreaHeight;
        using (context.PushClip(bounds))
        {
            context.DrawRoundedRect(new Rect(0, 0, bounds.Width, header), new CornerRadius(corner.TopLeft, corner.TopRight, 0, 0), colors.SurfaceContainer);
        }
        context.DrawRect(new Rect(0, header - 1, bounds.Width, 1), colors.OutlineVariant);
    }

    /// <inheritdoc/>
    public override void RenderOverlay(DataGrid grid, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, grid.Bounds.Size);
        context.DrawRoundedRectOutline(bounds.Deflate(new Thickness(0.5f)), grid.CornerRadius, colors.OutlineVariant, 1f);
        if (grid.IsFocusVisible && grid.CurrentIndex < 0)
        {
            MaterialDrawing.DrawFocusRing(ref context, bounds, grid.CornerRadius, colors.Secondary, offset: -MaterialFocusRing.Width);
        }

        float x = grid.DropIndicatorX;
        if (float.IsFinite(x))
        {
            context.DrawRect(new Rect(x - 1, 4, 2, grid.HeaderHeight - 8), colors.Primary);
        }
    }
}

/// <summary>
/// Draws a <see cref="DataGridRow"/> and supplies its content color: an outline-variant divider under it, a
/// secondary-container background when selected (content on-secondary-container), an on-surface state layer on hover
/// and press, and an inward focus ring on the current row while the grid shows keyboard focus.
/// </summary>
/// <param name="colors">The color scheme.</param>
public class MaterialDataGridRowRenderer(MaterialColorScheme colors) : ControlRenderer<DataGridRow>, IContentColorProvider
{
    /// <inheritdoc/>
    public override void Render(DataGridRow row, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, row.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (row.IsSelected)
        {
            context.DrawRect(bounds, row.Owner.IsEnabled ? colors.SecondaryContainer : MaterialDrawing.DisabledContainer(colors));
        }
        else if (row.Owner.SelectionUnit == DataGridSelectionUnit.Cell && row.Item is { } item)
        {
            // Cell selection: the selected cells of the row.
            var fill = row.Owner.IsEnabled ? colors.SecondaryContainer : MaterialDrawing.DisabledContainer(colors);
            foreach (var column in row.Owner.VisibleColumns)
            {
                if (row.Owner.IsCellSelected(item, column) && row.GetCellBounds(column) is { } cell) context.DrawRect(cell, fill);
            }
        }
        float opacity = !row.Owner.IsEnabled ? 0f
            : row.IsPressed ? MaterialState.PressedOpacity
            : row.IsHovered ? MaterialState.HoverOpacity
            : 0f;
        MaterialDrawing.DrawStateLayer(ref context, bounds, CornerRadius.Zero, ContentColorOf(row), opacity);
        context.DrawRect(new Rect(0, bounds.Height - 1, bounds.Width, 1), colors.OutlineVariant.WithOpacity(0.6f));

        // The keyboard cursor on a row that isn't selected (Ctrl+arrows, or check box selection) and has no current cell; a
        // selected row already stands out by its background, and a current cell gets its own outline.
        if (row.IsCurrent && row.Owner.IsFocusVisible && !row.IsSelected && row.CurrentCellBounds == null && row.EditingColumn == null)
        {
            MaterialDrawing.DrawFocusRing(ref context, bounds, CornerRadius.Zero, colors.Secondary, offset: -MaterialFocusRing.Width);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Over the cells: the current cell's outline while the grid shows keyboard focus, and the edited cell's (error color for a rejected value).</remarks>
    public override void RenderOverlay(DataGridRow row, ref DrawingContext context)
    {
        if (row.EditingColumn != null)
        {
            // The editor draws its own (focus) outline; a rejected value traces it in the error color.
            if (row.EditError != null && row.Editor is { } editor)
            {
                var corner = editor is Control control ? control.CornerRadius : new CornerRadius(4);
                var b = editor.Bounds;
                context.DrawRoundedRectOutline(new Rect(b.X + 1, b.Y + 1, Math.Max(0, b.Width - 2), Math.Max(0, b.Height - 2)), corner, colors.Error, 2f);
            }
        }
        // The current cell: with the keyboard; with cell selection whenever the grid has the focus (a click shows it, too).
        else if ((row.Owner.IsFocusVisible || (row.Owner.SelectionUnit == DataGridSelectionUnit.Cell && row.Owner.IsFocused)) && row.CurrentCellBounds is { } current)
        {
            context.DrawRoundedRectOutline(Inset(current, 2f), new CornerRadius(2), colors.Primary, 2f);
        }
    }

    private static Rect Inset(Rect rect, float by) => new(rect.X + by, rect.Y + by, Math.Max(0, rect.Width - by * 2), Math.Max(0, rect.Height - by * 2));

    /// <inheritdoc/>
    public bool TryGetContentColor(UIElement element, out Color color)
    {
        color = element is DataGridRow row ? ContentColorOf(row) : colors.OnSurface;
        return element is DataGridRow;
    }

    private Color ContentColorOf(DataGridRow row) => row.IsSelected ? colors.OnSecondaryContainer : colors.OnSurface;
}

/// <summary>
/// Draws a <see cref="DataGridColumnHeader"/> and supplies its content color (on-surface-variant, on-surface while
/// sorted): an on-surface state layer on hover and press, a divider at the right edge that turns primary over the
/// resize grip or while resizing, and a lifted surface-container-high shape with a shadow while dragged.
/// </summary>
/// <param name="colors">The color scheme.</param>
public class MaterialDataGridColumnHeaderRenderer(MaterialColorScheme colors) : ControlRenderer<DataGridColumnHeader>, IContentColorProvider
{
    /// <inheritdoc/>
    public override void Render(DataGridColumnHeader header, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, header.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (header.IsDragging)
        {
            var shape = bounds.Deflate(new Thickness(2));
            var corner = new CornerRadius(6);
            context.DrawShadow(shape, corner, MaterialElevation.Level2, colors.Shadow);
            context.DrawRoundedRect(shape, corner, colors.SurfaceContainerHigh);
            return;
        }

        bool overGrip = header.IsOverResizeGrip || header.IsResizing;
        float opacity = !header.IsEnabled || overGrip ? 0f
            : header.IsPressed ? MaterialState.PressedOpacity
            : header.IsHovered ? MaterialState.HoverOpacity
            : 0f;
        MaterialDrawing.DrawStateLayer(ref context, bounds, CornerRadius.Zero, colors.OnSurface, opacity);

        if (overGrip)
        {
            context.DrawRect(new Rect(bounds.Width - 2, 6, 2, bounds.Height - 12), colors.Primary);
        }
        else
        {
            context.DrawRect(new Rect(bounds.Width - 1, 12, 1, bounds.Height - 24), colors.OutlineVariant);
        }
    }

    /// <inheritdoc/>
    public bool TryGetContentColor(UIElement element, out Color color)
    {
        if (element is not DataGridColumnHeader header)
        {
            color = colors.OnSurface;
            return false;
        }
        color = !header.IsEnabled ? MaterialDrawing.DisabledContent(colors)
            : header.Column.SortDirection != DataGridSortDirection.None ? colors.OnSurface
            : colors.OnSurfaceVariant;
        return true;
    }
}
