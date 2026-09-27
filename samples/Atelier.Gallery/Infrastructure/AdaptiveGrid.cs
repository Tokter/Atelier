using System;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Gallery.Infrastructure;

/// <summary>
/// Lays out children in as many equal columns as fit (each at least <see cref="MinColumnWidth"/> wide, at most
/// <see cref="MaxColumns"/>), filling rows left to right. Rows are as tall as their tallest child, so nothing overlaps
/// and narrow windows fall back to fewer columns instead of cutting content off.
/// </summary>
public class AdaptiveGrid : Panel
{
    private float _minColumnWidth = 320;
    private int _maxColumns = 3;
    private float _spacing = 16;

    /// <summary>Gets or sets the narrowest a column may get before the grid uses one column less.</summary>
    public float MinColumnWidth
    {
        get => _minColumnWidth;
        set { _minColumnWidth = Math.Max(1, value); InvalidateMeasure(); }
    }

    /// <summary>Gets or sets the maximum number of columns.</summary>
    public int MaxColumns
    {
        get => _maxColumns;
        set { _maxColumns = Math.Max(1, value); InvalidateMeasure(); }
    }

    /// <summary>Gets or sets the gap between columns and between rows.</summary>
    public float Spacing
    {
        get => _spacing;
        set { _spacing = Math.Max(0, value); InvalidateMeasure(); }
    }

    private int VisibleCount()
    {
        int count = 0;
        var children = Children;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is UIElement { Visibility: not Visibility.Collapsed })
            {
                count++;
            }
        }
        return count;
    }

    private int ColumnCount(float width, int visible)
    {
        if (visible == 0)
        {
            return 1;
        }

        int fit = float.IsFinite(width) ? (int)((width + _spacing) / (_minColumnWidth + _spacing)) : 1;
        return Math.Clamp(fit, 1, Math.Min(_maxColumns, visible));
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        int visible = VisibleCount();
        int columns = ColumnCount(availableSize.Width, visible);
        float columnWidth = float.IsFinite(availableSize.Width)
            ? Math.Max(0, (availableSize.Width - _spacing * (columns - 1)) / columns)
            : _minColumnWidth;

        float height = 0, rowHeight = 0, widest = 0;
        int index = 0;
        var children = Children;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not UIElement child)
            {
                continue;
            }

            child.Measure(new Size(columnWidth, float.PositiveInfinity));
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            widest = Math.Max(widest, child.DesiredSize.Width);
            if (++index % columns == 0 || index == visible)
            {
                height += rowHeight + (index < visible ? _spacing : 0);
                rowHeight = 0;
            }
        }

        float width = float.IsFinite(availableSize.Width) ? availableSize.Width : widest * columns + _spacing * (columns - 1);
        return new Size(width, height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        int visible = VisibleCount();
        int columns = ColumnCount(finalSize.Width, visible);
        float columnWidth = Math.Max(0, (finalSize.Width - _spacing * (columns - 1)) / columns);
        var children = Children;

        // Row heights first, so all children of a row get the same height.
        Span<float> rowHeights = stackalloc float[(visible + columns - 1) / columns + 1];
        int index = 0;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is UIElement { Visibility: not Visibility.Collapsed } child)
            {
                int row = index++ / columns;
                rowHeights[row] = Math.Max(rowHeights[row], child.DesiredSize.Height);
            }
        }

        index = 0;
        float y = 0;
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

            int column = index % columns;
            int row = index / columns;
            child.Arrange(new Rect(column * (columnWidth + _spacing), y, columnWidth, rowHeights[row]));
            if (++index % columns == 0)
            {
                y += rowHeights[row] + _spacing;
            }
        }

        return finalSize;
    }
}
