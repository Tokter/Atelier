using System;
using System.Collections.Generic;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Layout;

public enum GridUnitType
{
    Auto,
    Pixel,
    Star
}

public readonly struct GridLength(float value, GridUnitType type) : IEquatable<GridLength>
{
    public float Value { get; } = value;
    public GridUnitType GridUnitType { get; } = type;

    public bool IsAuto => GridUnitType == GridUnitType.Auto;
    public bool IsStar => GridUnitType == GridUnitType.Star;
    public bool IsAbsolute => GridUnitType == GridUnitType.Pixel;

    public static readonly GridLength Auto = new(1, GridUnitType.Auto);
    public static readonly GridLength Star = new(1, GridUnitType.Star);
    public static GridLength Pixels(float pixels) => new(pixels, GridUnitType.Pixel);
    public static GridLength Stars(float weight) => new(weight, GridUnitType.Star);

    public bool Equals(GridLength other) => MathF.Abs(Value - other.Value) < 1e-5f && GridUnitType == other.GridUnitType;
    public override bool Equals(object? obj) => obj is GridLength other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Value, GridUnitType);
}

public class RowDefinition
{
    public GridLength Height { get; set; } = GridLength.Star;
    internal float ActualHeight { get; set; }
    internal float ActualOffset { get; set; }

    public RowDefinition() { }
    public RowDefinition(GridLength height) => Height = height;
}

public class ColumnDefinition
{
    public GridLength Width { get; set; } = GridLength.Star;
    internal float ActualWidth { get; set; }
    internal float ActualOffset { get; set; }

    public ColumnDefinition() { }
    public ColumnDefinition(GridLength width) => Width = width;
}

public class Grid : Panel
{
    public static readonly BindableProperty<int> RowProperty =
        BindableProperty.RegisterAttached<Grid, UIElement, int>("Row", 0, (s, o, n) => (s as UIElement)?.InvalidateMeasure());

    public static readonly BindableProperty<int> ColumnProperty =
        BindableProperty.RegisterAttached<Grid, UIElement, int>("Column", 0, (s, o, n) => (s as UIElement)?.InvalidateMeasure());

    public static readonly BindableProperty<int> RowSpanProperty =
        BindableProperty.RegisterAttached<Grid, UIElement, int>("RowSpan", 1, (s, o, n) => (s as UIElement)?.InvalidateMeasure());

    public static readonly BindableProperty<int> ColumnSpanProperty =
        BindableProperty.RegisterAttached<Grid, UIElement, int>("ColumnSpan", 1, (s, o, n) => (s as UIElement)?.InvalidateMeasure());

    public static void SetRow(UIElement element, int value) => element.SetValue(RowProperty, value);
    public static int GetRow(UIElement element) => element.GetValue(RowProperty);

    public static void SetColumn(UIElement element, int value) => element.SetValue(ColumnProperty, value);
    public static int GetColumn(UIElement element) => element.GetValue(ColumnProperty);

    public static void SetRowSpan(UIElement element, int value) => element.SetValue(RowSpanProperty, value);
    public static int GetRowSpan(UIElement element) => element.GetValue(RowSpanProperty);

    public static void SetColumnSpan(UIElement element, int value) => element.SetValue(ColumnSpanProperty, value);
    public static int GetColumnSpan(UIElement element) => element.GetValue(ColumnSpanProperty);

    public static readonly BindableProperty<float> RowSpacingProperty =
        BindableProperty.Register<Grid, float>(nameof(RowSpacing), 0f, (s, o, n) => ((Grid)s).InvalidateMeasure());

    public static readonly BindableProperty<float> ColumnSpacingProperty =
        BindableProperty.Register<Grid, float>(nameof(ColumnSpacing), 0f, (s, o, n) => ((Grid)s).InvalidateMeasure());

    public float RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    public float ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public List<RowDefinition> RowDefinitions { get; } = [];
    public List<ColumnDefinition> ColumnDefinitions { get; } = [];

    protected override Size MeasureOverride(Size availableSize)
    {
        int rowCount = Math.Max(1, RowDefinitions.Count);
        int colCount = Math.Max(1, ColumnDefinitions.Count);

        // Ensure default definitions if empty
        var rows = RowDefinitions.Count > 0 ? RowDefinitions : [new RowDefinition(GridLength.Star)];
        var cols = ColumnDefinitions.Count > 0 ? ColumnDefinitions : [new ColumnDefinition(GridLength.Star)];

        // Measure children and calculate auto/pixel sizes
        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is UIElement child)
            {
                int colIdx = Math.Clamp(GetColumn(child), 0, cols.Count - 1);
                int rowIdx = Math.Clamp(GetRow(child), 0, rows.Count - 1);

                float w = cols[colIdx].Width.IsAuto ? float.PositiveInfinity : availableSize.Width;
                if (cols[colIdx].Width.IsAbsolute) w = cols[colIdx].Width.Value;

                float h = rows[rowIdx].Height.IsAuto ? float.PositiveInfinity : availableSize.Height;
                if (rows[rowIdx].Height.IsAbsolute) h = rows[rowIdx].Height.Value;

                child.Measure(new Size(w, h));
            }
        }

        // Compute column widths
        float totalColSpacing = Math.Max(0, cols.Count - 1) * ColumnSpacing;
        float remainingWidth = float.IsPositiveInfinity(availableSize.Width) ? 0 : Math.Max(0, availableSize.Width - totalColSpacing);
        float totalStarsX = 0;

        foreach (var col in cols)
        {
            if (col.Width.IsAbsolute)
            {
                col.ActualWidth = col.Width.Value;
                remainingWidth -= col.ActualWidth;
            }
            else if (col.Width.IsAuto)
            {
                // Find max desired width of children in this column
                float maxColW = 0;
                for (int i = 0; i < Children.Count; i++)
                {
                    if (Children[i] is UIElement child && child.Visibility != Visibility.Collapsed && GetColumn(child) == cols.IndexOf(col) && GetColumnSpan(child) == 1)
                    {
                        maxColW = Math.Max(maxColW, child.DesiredSize.Width);
                    }
                }
                col.ActualWidth = maxColW;
                remainingWidth -= col.ActualWidth;
            }
            else if (col.Width.IsStar)
            {
                totalStarsX += col.Width.Value;
            }
        }

        if (float.IsPositiveInfinity(availableSize.Width))
        {
            // When width is unconstrained (e.g. inside horizontal ScrollViewer), Star columns behave like Auto
            foreach (var col in cols)
            {
                if (col.Width.IsStar)
                {
                    float maxColW = 0;
                    for (int i = 0; i < Children.Count; i++)
                    {
                        if (Children[i] is UIElement child && child.Visibility != Visibility.Collapsed && GetColumn(child) == cols.IndexOf(col) && GetColumnSpan(child) == 1)
                        {
                            maxColW = Math.Max(maxColW, child.DesiredSize.Width);
                        }
                    }
                    col.ActualWidth = maxColW;
                }
            }
        }
        else if (totalStarsX > 0 && remainingWidth > 0)
        {
            foreach (var col in cols)
            {
                if (col.Width.IsStar)
                {
                    col.ActualWidth = (col.Width.Value / totalStarsX) * remainingWidth;
                }
            }
        }

        // Re-measure children with their assigned column widths so that width-dependent
        // elements (such as text wrapping) compute their correct DesiredSize.Height
        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is UIElement child && child.Visibility != Visibility.Collapsed)
            {
                int colIdx = Math.Clamp(GetColumn(child), 0, cols.Count - 1);
                int colSpan = Math.Clamp(GetColumnSpan(child), 1, cols.Count - colIdx);

                float childWidth = 0;
                for (int c = 0; c < colSpan; c++)
                {
                    childWidth += cols[colIdx + c].ActualWidth;
                }
                if (colSpan > 1)
                {
                    childWidth += (colSpan - 1) * ColumnSpacing;
                }

                int rowIdx = Math.Clamp(GetRow(child), 0, rows.Count - 1);
                int rowSpan = Math.Clamp(GetRowSpan(child), 1, rows.Count - rowIdx);

                float childHeight = availableSize.Height;
                bool isAutoRow = true;
                float totalAbsoluteH = 0f;
                for (int r = 0; r < rowSpan; r++)
                {
                    if (!rows[rowIdx + r].Height.IsAuto) isAutoRow = false;
                    if (rows[rowIdx + r].Height.IsAbsolute) totalAbsoluteH += rows[rowIdx + r].Height.Value;
                }

                if (isAutoRow)
                {
                    childHeight = float.PositiveInfinity;
                }
                else if (totalAbsoluteH > 0 && !rows[rowIdx].Height.IsStar)
                {
                    childHeight = totalAbsoluteH;
                }

                child.Measure(new Size(childWidth, childHeight));
            }
        }

        // Compute row heights
        float totalRowSpacing = Math.Max(0, rows.Count - 1) * RowSpacing;
        float remainingHeight = float.IsPositiveInfinity(availableSize.Height) ? 0 : Math.Max(0, availableSize.Height - totalRowSpacing);
        float totalStarsY = 0;

        foreach (var row in rows)
        {
            if (row.Height.IsAbsolute)
            {
                row.ActualHeight = row.Height.Value;
                remainingHeight -= row.ActualHeight;
            }
            else if (row.Height.IsAuto)
            {
                float maxRowH = 0;
                for (int i = 0; i < Children.Count; i++)
                {
                    if (Children[i] is UIElement child && child.Visibility != Visibility.Collapsed && GetRow(child) == rows.IndexOf(row) && GetRowSpan(child) == 1)
                    {
                        maxRowH = Math.Max(maxRowH, child.DesiredSize.Height);
                    }
                }
                row.ActualHeight = maxRowH;
                remainingHeight -= row.ActualHeight;
            }
            else if (row.Height.IsStar)
            {
                totalStarsY += row.Height.Value;
            }
        }

        if (float.IsPositiveInfinity(availableSize.Height))
        {
            // When height is unconstrained (e.g. inside vertical ScrollViewer), Star rows behave like Auto
            foreach (var row in rows)
            {
                if (row.Height.IsStar)
                {
                    float maxRowH = 0;
                    for (int i = 0; i < Children.Count; i++)
                    {
                        if (Children[i] is UIElement child && child.Visibility != Visibility.Collapsed && GetRow(child) == rows.IndexOf(row) && GetRowSpan(child) == 1)
                        {
                            maxRowH = Math.Max(maxRowH, child.DesiredSize.Height);
                        }
                    }
                    row.ActualHeight = maxRowH;
                }
            }
        }
        else if (totalStarsY > 0 && remainingHeight > 0)
        {
            foreach (var row in rows)
            {
                if (row.Height.IsStar)
                {
                    row.ActualHeight = (row.Height.Value / totalStarsY) * remainingHeight;
                }
            }
        }

        float totalW = 0;
        foreach (var col in cols) totalW += col.ActualWidth;
        totalW += totalColSpacing;

        float totalH = 0;
        foreach (var row in rows) totalH += row.ActualHeight;
        totalH += totalRowSpacing;

        return new Size(totalW, totalH);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rows = RowDefinitions.Count > 0 ? RowDefinitions : [new RowDefinition(GridLength.Star)];
        var cols = ColumnDefinitions.Count > 0 ? ColumnDefinitions : [new ColumnDefinition(GridLength.Star)];

        float totalColSpacing = Math.Max(0, cols.Count - 1) * ColumnSpacing;
        float totalRowSpacing = Math.Max(0, rows.Count - 1) * RowSpacing;

        // Distribute star space according to finalSize
        float nonStarWidth = 0;
        float totalStarsX = 0;
        foreach (var col in cols)
        {
            if (col.Width.IsStar) totalStarsX += col.Width.Value;
            else nonStarWidth += col.ActualWidth;
        }

        if (totalStarsX > 0)
        {
            float availableStarWidth = Math.Max(0, finalSize.Width - nonStarWidth - totalColSpacing);
            foreach (var col in cols)
            {
                if (col.Width.IsStar)
                {
                    col.ActualWidth = (col.Width.Value / totalStarsX) * availableStarWidth;
                }
            }
        }

        float nonStarHeight = 0;
        float totalStarsY = 0;
        foreach (var row in rows)
        {
            if (row.Height.IsStar) totalStarsY += row.Height.Value;
            else nonStarHeight += row.ActualHeight;
        }

        if (totalStarsY > 0)
        {
            float availableStarHeight = Math.Max(0, finalSize.Height - nonStarHeight - totalRowSpacing);
            foreach (var row in rows)
            {
                if (row.Height.IsStar)
                {
                    row.ActualHeight = (row.Height.Value / totalStarsY) * availableStarHeight;
                }
            }
        }

        // Calculate offsets
        float curX = 0;
        foreach (var col in cols)
        {
            col.ActualOffset = curX;
            curX += col.ActualWidth + ColumnSpacing;
        }

        float curY = 0;
        foreach (var row in rows)
        {
            row.ActualOffset = curY;
            curY += row.ActualHeight + RowSpacing;
        }

        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is not UIElement child)
                continue;

            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(Rect.Zero);
                continue;
            }

            int colIdx = Math.Clamp(GetColumn(child), 0, cols.Count - 1);
            int rowIdx = Math.Clamp(GetRow(child), 0, rows.Count - 1);
            int colSpan = Math.Clamp(GetColumnSpan(child), 1, cols.Count - colIdx);
            int rowSpan = Math.Clamp(GetRowSpan(child), 1, rows.Count - rowIdx);

            float x = cols[colIdx].ActualOffset;
            float y = rows[rowIdx].ActualOffset;

            float w = 0;
            for (int c = 0; c < colSpan; c++) w += cols[colIdx + c].ActualWidth;
            if (colSpan > 1) w += (colSpan - 1) * ColumnSpacing;

            float h = 0;
            for (int r = 0; r < rowSpan; r++) h += rows[rowIdx + r].ActualHeight;
            if (rowSpan > 1) h += (rowSpan - 1) * RowSpacing;

            child.Arrange(new Rect(x, y, w, h));
        }

        return finalSize;
    }
}
