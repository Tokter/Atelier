using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Layout;

/// <summary>
/// Arranges children in a grid of equally sized cells, filling each row left to right before moving to the next.
/// </summary>
/// <remarks>
/// With neither <see cref="Rows"/> nor <see cref="Columns"/> set, the grid is as square as possible: the column count is
/// the square root of the number of visible children, rounded up. Setting one of them derives the other from the child
/// count; setting both fixes the layout, and children that don't fit are not shown. Every cell is as large as the
/// largest child. Collapsed children don't occupy a cell.
/// </remarks>
public class UniformGrid : Panel
{
    /// <summary>Identifies the <see cref="Rows"/> property.</summary>
    public static readonly BindableProperty<int> RowsProperty =
        BindableProperty.Register<UniformGrid, int>(nameof(Rows), 0, options: PropertyOptions.AffectsMeasure, validateValue: IsNotNegative);

    /// <summary>Identifies the <see cref="Columns"/> property.</summary>
    public static readonly BindableProperty<int> ColumnsProperty =
        BindableProperty.Register<UniformGrid, int>(nameof(Columns), 0, options: PropertyOptions.AffectsMeasure, validateValue: IsNotNegative);

    /// <summary>Identifies the <see cref="FirstColumn"/> property.</summary>
    public static readonly BindableProperty<int> FirstColumnProperty =
        BindableProperty.Register<UniformGrid, int>(nameof(FirstColumn), 0, options: PropertyOptions.AffectsMeasure, validateValue: IsNotNegative);

    /// <summary>Identifies the <see cref="RowSpacing"/> property.</summary>
    public static readonly BindableProperty<float> RowSpacingProperty =
        BindableProperty.Register<UniformGrid, float>(nameof(RowSpacing), 0f, options: PropertyOptions.AffectsMeasure, validateValue: IsValidSpacing);

    /// <summary>Identifies the <see cref="ColumnSpacing"/> property.</summary>
    public static readonly BindableProperty<float> ColumnSpacingProperty =
        BindableProperty.Register<UniformGrid, float>(nameof(ColumnSpacing), 0f, options: PropertyOptions.AffectsMeasure, validateValue: IsValidSpacing);

    /// <summary>Gets or sets the number of rows; 0 (the default) derives it from the child count.</summary>
    public int Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <summary>Gets or sets the number of columns; 0 (the default) derives it from the child count.</summary>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of empty cells before the first child in the first row, e.g. to start a calendar month on
    /// the right weekday. Ignored when it is not smaller than the column count.
    /// </summary>
    public int FirstColumn
    {
        get => GetValue(FirstColumnProperty);
        set => SetValue(FirstColumnProperty, value);
    }

    /// <summary>Gets or sets the gap between rows. The default is 0.</summary>
    public float RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    /// <summary>Gets or sets the gap between columns. The default is 0.</summary>
    public float ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    private static bool IsNotNegative(int value) => value >= 0;

    // Layout computed by the last measure, used by arrange.
    private int _rows;
    private int _columns;
    private int _firstColumn;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateDimensions();
        float columnSpacing = ColumnSpacing;
        float rowSpacing = RowSpacing;

        var cellConstraint = new Size(
            CellLength(availableSize.Width, _columns, columnSpacing),
            CellLength(availableSize.Height, _rows, rowSpacing));

        float cellWidth = 0, cellHeight = 0;
        var children = Children;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not UIElement child)
            {
                continue;
            }

            child.Measure(cellConstraint);
            cellWidth = Math.Max(cellWidth, child.DesiredSize.Width);
            cellHeight = Math.Max(cellHeight, child.DesiredSize.Height);
        }

        return new Size(
            _columns == 0 ? 0 : cellWidth * _columns + columnSpacing * (_columns - 1),
            _rows == 0 ? 0 : cellHeight * _rows + rowSpacing * (_rows - 1));
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        float columnSpacing = ColumnSpacing;
        float rowSpacing = RowSpacing;
        float cellWidth = CellLength(finalSize.Width, _columns, columnSpacing);
        float cellHeight = CellLength(finalSize.Height, _rows, rowSpacing);

        int cellIndex = _firstColumn;
        int cellCount = _rows * _columns;
        var children = Children;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not UIElement child)
            {
                continue;
            }

            // Collapsed children and children beyond the last cell are not shown.
            if (child.Visibility == Visibility.Collapsed || cellIndex >= cellCount)
            {
                child.Arrange(Rect.Zero);
                continue;
            }

            int row = cellIndex / _columns;
            int column = cellIndex % _columns;
            child.Arrange(new Rect(
                column * (cellWidth + columnSpacing),
                row * (cellHeight + rowSpacing),
                cellWidth,
                cellHeight));
            cellIndex++;
        }

        return finalSize;
    }

    private static float CellLength(float total, int count, float spacing) =>
        count == 0 ? 0 : Math.Max(0, (total - spacing * (count - 1)) / count);

    private void UpdateDimensions()
    {
        int visible = 0;
        var children = Children;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is UIElement { Visibility: not Visibility.Collapsed })
            {
                visible++;
            }
        }

        int rows = Rows;
        int columns = Columns;
        int firstColumn = FirstColumn;

        if (columns > 0 && firstColumn >= columns)
        {
            firstColumn = 0;
        }

        if (rows == 0 && columns == 0)
        {
            // As square as possible; FirstColumn's empty cells count as occupied.
            columns = (int)Math.Ceiling(Math.Sqrt(visible));
            if (firstColumn >= columns)
            {
                firstColumn = 0;
            }
        }

        if (rows == 0)
        {
            rows = columns == 0 ? 0 : (visible + firstColumn + columns - 1) / columns;
        }
        else if (columns == 0)
        {
            columns = (visible + rows - 1) / rows;
            firstColumn = 0; // without a fixed column count, leading empty cells would change the column count
        }

        _rows = rows;
        _columns = columns;
        _firstColumn = columns == 0 ? 0 : firstColumn;
    }
}
