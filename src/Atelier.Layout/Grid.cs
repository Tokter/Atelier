using System;
using System.Collections.ObjectModel;
using System.Globalization;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Layout;

/// <summary>
/// Specifies how the size of a <see cref="Grid"/> row or column is determined.
/// </summary>
public enum GridUnitType
{
    /// <summary>The size is the largest desired size of the content in the row or column.</summary>
    Auto,

    /// <summary>The size is a fixed number of pixels.</summary>
    Pixel,

    /// <summary>The size is a weighted share of the space left after pixel and auto rows or columns.</summary>
    Star
}

/// <summary>
/// The size of a <see cref="Grid"/> row or column: a fixed pixel size, <see cref="Auto"/> (sized to content), or a
/// weighted share of the remaining space (<see cref="Star"/>).
/// </summary>
public readonly struct GridLength : IEquatable<GridLength>
{
    /// <summary>
    /// Initializes a new <see cref="GridLength"/>.
    /// </summary>
    /// <param name="value">The pixel size or star weight; ignored for <see cref="GridUnitType.Auto"/>.</param>
    /// <param name="type">How the size is determined.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is negative, NaN or infinite.</exception>
    public GridLength(float value, GridUnitType type)
    {
        if (!float.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "A grid length must be a finite, non-negative number.");
        }

        Value = value;
        GridUnitType = type;
    }

    /// <summary>Gets the pixel size (<see cref="GridUnitType.Pixel"/>) or star weight (<see cref="GridUnitType.Star"/>).</summary>
    public float Value { get; }

    /// <summary>Gets how the size is determined.</summary>
    public GridUnitType GridUnitType { get; }

    /// <summary>Gets whether the size is determined by the content.</summary>
    public bool IsAuto => GridUnitType == GridUnitType.Auto;

    /// <summary>Gets whether the size is a weighted share of the remaining space.</summary>
    public bool IsStar => GridUnitType == GridUnitType.Star;

    /// <summary>Gets whether the size is a fixed number of pixels.</summary>
    public bool IsAbsolute => GridUnitType == GridUnitType.Pixel;

    /// <summary>A length sized to content.</summary>
    public static readonly GridLength Auto = new(1, GridUnitType.Auto);

    /// <summary>A star length with weight 1 (<c>*</c>).</summary>
    public static readonly GridLength Star = new(1, GridUnitType.Star);

    /// <summary>Creates a fixed pixel length.</summary>
    public static GridLength Pixels(float pixels) => new(pixels, GridUnitType.Pixel);

    /// <summary>Creates a star length with the given weight (<c>2*</c> is <c>Stars(2)</c>).</summary>
    public static GridLength Stars(float weight) => new(weight, GridUnitType.Star);

    /// <summary>
    /// Parses <c>"Auto"</c>, <c>"*"</c>, <c>"2.5*"</c> or a pixel size such as <c>"120"</c> (case-insensitive, invariant culture).
    /// </summary>
    /// <exception cref="FormatException"><paramref name="text"/> is not a valid grid length.</exception>
    public static GridLength Parse(string text)
    {
        return TryParse(text, out var length)
            ? length
            : throw new FormatException($"'{text}' is not a valid grid length. Use 'Auto', '*', '2*' or a number of pixels.");
    }

    /// <summary>Tries to parse a grid length; see <see cref="Parse(string)"/>.</summary>
    public static bool TryParse(string? text, out GridLength length)
    {
        length = default;
        if (text == null)
        {
            return false;
        }

        var span = text.AsSpan().Trim();
        if (span.Equals("Auto", StringComparison.OrdinalIgnoreCase))
        {
            length = Auto;
            return true;
        }

        var type = GridUnitType.Pixel;
        if (span.EndsWith("*"))
        {
            type = GridUnitType.Star;
            span = span[..^1].TrimEnd();
            if (span.IsEmpty)
            {
                length = Star;
                return true;
            }
        }
        else if (span.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            span = span[..^2].TrimEnd();
        }

        if (!float.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ||
            !float.IsFinite(value) || value < 0)
        {
            return false;
        }

        length = new GridLength(value, type);
        return true;
    }

    /// <summary>Returns <c>"Auto"</c>, <c>"*"</c>, <c>"2*"</c> or the pixel size, in a form <see cref="Parse(string)"/> accepts.</summary>
    public override string ToString() => GridUnitType switch
    {
        GridUnitType.Auto => "Auto",
        GridUnitType.Star => Value == 1 ? "*" : Value.ToString(CultureInfo.InvariantCulture) + "*",
        _ => Value.ToString(CultureInfo.InvariantCulture),
    };

    /// <inheritdoc/>
    /// <remarks>All auto lengths are equal (including <c>default(GridLength)</c>), whatever their <see cref="Value"/>.</remarks>
    public bool Equals(GridLength other) =>
        GridUnitType == other.GridUnitType && (GridUnitType == GridUnitType.Auto || Value == other.Value);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is GridLength other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => GridUnitType == GridUnitType.Auto ? 0 : HashCode.Combine(Value, GridUnitType);

    /// <summary>Determines whether two lengths are equal.</summary>
    public static bool operator ==(GridLength left, GridLength right) => left.Equals(right);

    /// <summary>Determines whether two lengths differ.</summary>
    public static bool operator !=(GridLength left, GridLength right) => !left.Equals(right);
}

/// <summary>
/// Base class of <see cref="RowDefinition"/> and <see cref="ColumnDefinition"/>: a size with optional limits, plus the
/// layout results. Changing a definition that belongs to a <see cref="Grid"/> re-lays out the grid.
/// </summary>
public abstract class DefinitionBase
{
    private GridLength _length;
    private float _minSize;
    private float _maxSize = float.PositiveInfinity;

    private protected DefinitionBase(GridLength length)
    {
        _length = length;
    }

    /// <summary>The grid whose definition collection contains this definition, or <c>null</c>.</summary>
    internal Grid? Owner { get; set; }

    internal GridLength Length
    {
        get => _length;
        set
        {
            if (_length != value)
            {
                _length = value;
                Owner?.InvalidateMeasure();
            }
        }
    }

    internal float MinSize
    {
        get => _minSize;
        set
        {
            ValidateLimit(value, allowInfinity: false);
            if (_minSize != value)
            {
                _minSize = value;
                Owner?.InvalidateMeasure();
            }
        }
    }

    internal float MaxSize
    {
        get => _maxSize;
        set
        {
            ValidateLimit(value, allowInfinity: true);
            if (_maxSize != value)
            {
                _maxSize = value;
                Owner?.InvalidateMeasure();
            }
        }
    }

    // Layout state. Measure and arrange keep separate sizes, so an arrange with a different size than the measure (for
    // example after an infinite measure) doesn't disturb the auto sizes the next arrange relies on.

    /// <summary>Whether this definition is treated as a star during the current measure (not when that axis is infinite).</summary>
    internal bool IsStarInMeasure;

    /// <summary>Whether this definition is sized to content during the current measure (auto, or star on an infinite axis).</summary>
    internal bool IsAutoInMeasure;

    /// <summary>The size used to measure children and, for auto definitions, the size used by arrange.</summary>
    internal float MeasureSize;

    /// <summary>The largest desired size of the children that span only this definition (and, for spans, their share).</summary>
    internal float ContentSize;

    /// <summary>Scratch flag of the star resolution loop.</summary>
    internal bool IsStarResolved;

    /// <summary>The final size, set by the last arrange.</summary>
    internal float ActualSize { get; set; }

    /// <summary>The final position, set by the last arrange.</summary>
    internal float ActualOffset { get; set; }

    internal float Clamp(float size) => Math.Max(_minSize, Math.Min(_maxSize, size));

    private static void ValidateLimit(float value, bool allowInfinity)
    {
        if (float.IsNaN(value) || value < 0 || (!allowInfinity && float.IsInfinity(value)))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Row and column size limits must be non-negative numbers.");
        }
    }
}

/// <summary>
/// Defines a <see cref="Grid"/> row: its <see cref="Height"/>, optional limits, and the size and position it got.
/// </summary>
public class RowDefinition : DefinitionBase
{
    /// <summary>Initializes a row with a <see cref="GridLength.Star"/> height.</summary>
    public RowDefinition() : base(GridLength.Star) { }

    /// <summary>Initializes a row with the given height.</summary>
    public RowDefinition(GridLength height) : base(height) { }

    /// <summary>Gets or sets the height of the row. The default is <see cref="GridLength.Star"/>.</summary>
    public GridLength Height { get => Length; set => Length = value; }

    /// <summary>Gets or sets the minimum height of the row, applied to every <see cref="GridUnitType"/>.</summary>
    public float MinHeight { get => MinSize; set => MinSize = value; }

    /// <summary>Gets or sets the maximum height of the row, applied to every <see cref="GridUnitType"/>.</summary>
    public float MaxHeight { get => MaxSize; set => MaxSize = value; }

    /// <summary>Gets the height the row got in the last arrange pass.</summary>
    public float ActualHeight => ActualSize;

    /// <summary>Gets the distance from the top of the grid to the row, as of the last arrange pass.</summary>
    public float Offset => ActualOffset;
}

/// <summary>
/// Defines a <see cref="Grid"/> column: its <see cref="Width"/>, optional limits, and the size and position it got.
/// </summary>
public class ColumnDefinition : DefinitionBase
{
    /// <summary>Initializes a column with a <see cref="GridLength.Star"/> width.</summary>
    public ColumnDefinition() : base(GridLength.Star) { }

    /// <summary>Initializes a column with the given width.</summary>
    public ColumnDefinition(GridLength width) : base(width) { }

    /// <summary>Gets or sets the width of the column. The default is <see cref="GridLength.Star"/>.</summary>
    public GridLength Width { get => Length; set => Length = value; }

    /// <summary>Gets or sets the minimum width of the column, applied to every <see cref="GridUnitType"/>.</summary>
    public float MinWidth { get => MinSize; set => MinSize = value; }

    /// <summary>Gets or sets the maximum width of the column, applied to every <see cref="GridUnitType"/>.</summary>
    public float MaxWidth { get => MaxSize; set => MaxSize = value; }

    /// <summary>Gets the width the column got in the last arrange pass.</summary>
    public float ActualWidth => ActualSize;

    /// <summary>Gets the distance from the left of the grid to the column, as of the last arrange pass.</summary>
    public float Offset => ActualOffset;
}

/// <summary>
/// The row or column definitions of a <see cref="Grid"/>. Adding, removing or replacing a definition re-lays out the grid;
/// a definition can belong to only one grid at a time.
/// </summary>
/// <typeparam name="T"><see cref="RowDefinition"/> or <see cref="ColumnDefinition"/>.</typeparam>
public sealed class DefinitionCollection<T> : Collection<T> where T : DefinitionBase
{
    private readonly Grid _owner;

    internal DefinitionCollection(Grid owner)
    {
        _owner = owner;
    }

    /// <inheritdoc/>
    protected override void InsertItem(int index, T item)
    {
        Adopt(item);
        base.InsertItem(index, item);
        _owner.OnDefinitionsChanged();
    }

    /// <inheritdoc/>
    protected override void SetItem(int index, T item)
    {
        var old = this[index];
        if (ReferenceEquals(old, item))
        {
            return;
        }

        Adopt(item);
        old.Owner = null;
        base.SetItem(index, item);
        _owner.OnDefinitionsChanged();
    }

    /// <inheritdoc/>
    protected override void RemoveItem(int index)
    {
        this[index].Owner = null;
        base.RemoveItem(index);
        _owner.OnDefinitionsChanged();
    }

    /// <inheritdoc/>
    protected override void ClearItems()
    {
        for (int i = 0; i < Count; i++)
        {
            this[i].Owner = null;
        }
        base.ClearItems();
        _owner.OnDefinitionsChanged();
    }

    private void Adopt(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Owner != null)
        {
            throw new InvalidOperationException("The definition already belongs to a Grid; remove it there first or create a new one.");
        }
        item.Owner = _owner;
    }
}

/// <summary>
/// Arranges children in a table of rows and columns. Children pick their cell with the attached <see cref="RowProperty"/>
/// and <see cref="ColumnProperty"/> and can span several cells with <see cref="RowSpanProperty"/> and
/// <see cref="ColumnSpanProperty"/>.
/// </summary>
/// <remarks>
/// <para>
/// Rows and columns are sized in three ways (see <see cref="GridLength"/>): pixel sizes are fixed, auto sizes fit the
/// largest child, and star sizes share the remaining space by weight. <see cref="RowDefinition.MinHeight"/>,
/// <see cref="RowDefinition.MaxHeight"/> and the column equivalents limit every kind. Without definitions, the grid has
/// a single star row and column, so all children overlap and fill it.
/// </para>
/// <para>
/// When the grid is measured with infinite space on an axis (for example inside a <c>ScrollViewer</c> or a
/// <see cref="StackPanel"/>), star rows or columns on that axis size to their content, keeping their weights'
/// proportions. As in WPF, the grid's desired size is based on content, so a grid that is not stretched shrinks to fit
/// its children; in the arrange pass star rows and columns share whatever space the grid is given.
/// </para>
/// <para>
/// Row and column indexes beyond the definitions are clamped to the last row or column, and spans are clamped to the
/// available rows and columns.
/// </para>
/// </remarks>
public class Grid : Panel
{
    #region Attached properties

    /// <summary>Identifies the attached row index of a child (0-based, default 0).</summary>
    public static readonly BindableProperty<int> RowProperty =
        BindableProperty.RegisterAttached<Grid, UIElement, int>("Row", 0, options: PropertyOptions.AffectsMeasure, validateValue: IsValidIndex);

    /// <summary>Identifies the attached column index of a child (0-based, default 0).</summary>
    public static readonly BindableProperty<int> ColumnProperty =
        BindableProperty.RegisterAttached<Grid, UIElement, int>("Column", 0, options: PropertyOptions.AffectsMeasure, validateValue: IsValidIndex);

    /// <summary>Identifies the attached number of rows a child spans (at least 1, default 1).</summary>
    public static readonly BindableProperty<int> RowSpanProperty =
        BindableProperty.RegisterAttached<Grid, UIElement, int>("RowSpan", 1, options: PropertyOptions.AffectsMeasure, validateValue: IsValidSpan);

    /// <summary>Identifies the attached number of columns a child spans (at least 1, default 1).</summary>
    public static readonly BindableProperty<int> ColumnSpanProperty =
        BindableProperty.RegisterAttached<Grid, UIElement, int>("ColumnSpan", 1, options: PropertyOptions.AffectsMeasure, validateValue: IsValidSpan);

    /// <summary>Sets the row of <paramref name="element"/>.</summary>
    public static void SetRow(UIElement element, int value) => element.SetValue(RowProperty, value);

    /// <summary>Gets the row of <paramref name="element"/>.</summary>
    public static int GetRow(UIElement element) => element.GetValue(RowProperty);

    /// <summary>Sets the column of <paramref name="element"/>.</summary>
    public static void SetColumn(UIElement element, int value) => element.SetValue(ColumnProperty, value);

    /// <summary>Gets the column of <paramref name="element"/>.</summary>
    public static int GetColumn(UIElement element) => element.GetValue(ColumnProperty);

    /// <summary>Sets the number of rows <paramref name="element"/> spans.</summary>
    public static void SetRowSpan(UIElement element, int value) => element.SetValue(RowSpanProperty, value);

    /// <summary>Gets the number of rows <paramref name="element"/> spans.</summary>
    public static int GetRowSpan(UIElement element) => element.GetValue(RowSpanProperty);

    /// <summary>Sets the number of columns <paramref name="element"/> spans.</summary>
    public static void SetColumnSpan(UIElement element, int value) => element.SetValue(ColumnSpanProperty, value);

    /// <summary>Gets the number of columns <paramref name="element"/> spans.</summary>
    public static int GetColumnSpan(UIElement element) => element.GetValue(ColumnSpanProperty);

    private static bool IsValidIndex(int value) => value >= 0;

    private static bool IsValidSpan(int value) => value >= 1;

    #endregion

    /// <summary>Identifies the <see cref="RowSpacing"/> property.</summary>
    public static readonly BindableProperty<float> RowSpacingProperty =
        BindableProperty.Register<Grid, float>(nameof(RowSpacing), 0f, options: PropertyOptions.AffectsMeasure, validateValue: IsValidSpacing);

    /// <summary>Identifies the <see cref="ColumnSpacing"/> property.</summary>
    public static readonly BindableProperty<float> ColumnSpacingProperty =
        BindableProperty.Register<Grid, float>(nameof(ColumnSpacing), 0f, options: PropertyOptions.AffectsMeasure, validateValue: IsValidSpacing);

    /// <summary>Gets or sets the gap between adjacent rows. The default is 0.</summary>
    public float RowSpacing
    {
        get => GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    /// <summary>Gets or sets the gap between adjacent columns. The default is 0.</summary>
    public float ColumnSpacing
    {
        get => GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <summary>Gets the row definitions. Without any, the grid has a single star row.</summary>
    public DefinitionCollection<RowDefinition> RowDefinitions { get; }

    /// <summary>Gets the column definitions. Without any, the grid has a single star column.</summary>
    public DefinitionCollection<ColumnDefinition> ColumnDefinitions { get; }

    // Snapshots of the definitions, rebuilt only when the collections change, so layout works on arrays and never
    // allocates. The implicit single row/column is created once per grid.
    private DefinitionBase[] _rows = [];
    private DefinitionBase[] _columns = [];
    private RowDefinition? _implicitRow;
    private ColumnDefinition? _implicitColumn;
    private bool _definitionsChanged = true;

    // Per-child cell information of the current measure, reused between passes (grown when there are more children).
    private CellInfo[] _cells = [];
    private int _cellCount;

    /// <summary>Initializes an empty grid.</summary>
    public Grid()
    {
        RowDefinitions = new DefinitionCollection<RowDefinition>(this);
        ColumnDefinitions = new DefinitionCollection<ColumnDefinition>(this);
    }

    internal void OnDefinitionsChanged()
    {
        _definitionsChanged = true;
        InvalidateMeasure();
    }

    [Flags]
    private enum SpanKind : byte
    {
        /// <summary>The span contains only pixel definitions.</summary>
        Fixed = 0,
        /// <summary>The span contains a definition sized to content.</summary>
        Auto = 1,
        /// <summary>The span contains a star definition (on an axis with finite space).</summary>
        Star = 2,
    }

    private struct CellInfo
    {
        public UIElement? Child;   // null: not laid out (collapsed or not a UIElement)
        public int Column, ColumnSpan, Row, RowSpan;
        public SpanKind ColumnKind, RowKind;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateDefinitionSnapshots();
        var columns = _columns;
        var rows = _rows;
        float columnSpacing = ColumnSpacing;
        float rowSpacing = RowSpacing;

        bool infiniteWidth = float.IsPositiveInfinity(availableSize.Width);
        bool infiniteHeight = float.IsPositiveInfinity(availableSize.Height);
        PrepareForMeasure(columns, infiniteWidth);
        PrepareForMeasure(rows, infiniteHeight);

        int count = PrepareCells(columns, rows);
        var cells = _cells;

        // The measure runs in phases so that each child is measured with the width it will get, which matters for
        // content whose height depends on its width (wrapping text). Children are grouped by whether their cell spans
        // a star column and/or a star row, since star sizes are only known after the auto sizes are.

        // 1. Children without star columns: they determine the auto column widths. Star-row children among them get a
        //    provisional height here and are measured again in step 5.
        for (int i = 0; i < count; i++)
        {
            ref var cell = ref cells[i];
            if (cell.Child != null && (cell.ColumnKind & SpanKind.Star) == 0)
            {
                float height = (cell.RowKind & SpanKind.Star) != 0
                    ? availableSize.Height
                    : SpanConstraint(rows, cell.Row, cell.RowSpan, rowSpacing);
                cell.Child.Measure(new Size(SpanConstraint(columns, cell.Column, cell.ColumnSpan, columnSpacing), height));
            }
        }

        // 2. Column widths: auto columns from their content, then star columns share the rest.
        ResolveContentSizes(columns, cells, count, columnSpacing, isColumn: true);
        ResolveStarsForMeasure(columns, availableSize.Width, columnSpacing);

        // 3. Children in star columns and non-star rows: their widths are known now, and they determine auto row heights.
        for (int i = 0; i < count; i++)
        {
            ref var cell = ref cells[i];
            if (cell.Child != null && (cell.ColumnKind & SpanKind.Star) != 0 && (cell.RowKind & SpanKind.Star) == 0)
            {
                cell.Child.Measure(new Size(
                    SpanConstraint(columns, cell.Column, cell.ColumnSpan, columnSpacing),
                    SpanConstraint(rows, cell.Row, cell.RowSpan, rowSpacing)));
            }
        }

        // 4. Row heights: auto rows from their content, then star rows share the rest.
        ResolveContentSizes(rows, cells, count, rowSpacing, isColumn: false);
        ResolveStarsForMeasure(rows, availableSize.Height, rowSpacing);

        // 5. Children in star rows, now that the star heights are known.
        for (int i = 0; i < count; i++)
        {
            ref var cell = ref cells[i];
            if (cell.Child != null && (cell.RowKind & SpanKind.Star) != 0)
            {
                cell.Child.Measure(new Size(
                    SpanConstraint(columns, cell.Column, cell.ColumnSpan, columnSpacing),
                    SpanConstraint(rows, cell.Row, cell.RowSpan, rowSpacing)));
            }
        }

        // 6. The desired size is based on content, with star content scaled to keep the star proportions.
        UpdateStarContentSizes(columns, cells, count, columnSpacing, isColumn: true);
        UpdateStarContentSizes(rows, cells, count, rowSpacing, isColumn: false);

        return new Size(
            DesiredLength(columns, columnSpacing),
            DesiredLength(rows, rowSpacing));
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        UpdateDefinitionSnapshots();
        var columns = _columns;
        var rows = _rows;
        float columnSpacing = ColumnSpacing;
        float rowSpacing = RowSpacing;

        ResolveForArrange(columns, finalSize.Width, columnSpacing);
        ResolveForArrange(rows, finalSize.Height, rowSpacing);

        var children = Children;
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

            int column = Math.Min(GetColumn(child), columns.Length - 1);
            int row = Math.Min(GetRow(child), rows.Length - 1);
            int columnSpan = Math.Min(GetColumnSpan(child), columns.Length - column);
            int rowSpan = Math.Min(GetRowSpan(child), rows.Length - row);

            child.Arrange(new Rect(
                columns[column].ActualOffset,
                rows[row].ActualOffset,
                SpanActualSize(columns, column, columnSpan, columnSpacing),
                SpanActualSize(rows, row, rowSpan, rowSpacing)));
        }

        return finalSize;
    }

    #region Measure helpers

    private void UpdateDefinitionSnapshots()
    {
        if (!_definitionsChanged)
        {
            return;
        }

        _definitionsChanged = false;
        _columns = ColumnDefinitions.Count > 0
            ? Snapshot(ColumnDefinitions)
            : [_implicitColumn ??= new ColumnDefinition()];
        _rows = RowDefinitions.Count > 0
            ? Snapshot(RowDefinitions)
            : [_implicitRow ??= new RowDefinition()];
    }

    private static DefinitionBase[] Snapshot<T>(DefinitionCollection<T> definitions) where T : DefinitionBase
    {
        var array = new DefinitionBase[definitions.Count];
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = definitions[i];
        }
        return array;
    }

    private static void PrepareForMeasure(DefinitionBase[] definitions, bool infinite)
    {
        foreach (var definition in definitions)
        {
            var length = definition.Length;
            definition.IsStarInMeasure = length.IsStar && !infinite;
            definition.IsAutoInMeasure = length.IsAuto || (length.IsStar && infinite);
            definition.MeasureSize = length.IsAbsolute ? definition.Clamp(length.Value) : definition.MinSize;
            definition.ContentSize = 0;
        }
    }

    // Records each laid-out child's clamped cell and what kinds of definitions it spans. Returns the child count.
    private int PrepareCells(DefinitionBase[] columns, DefinitionBase[] rows)
    {
        var children = Children;
        int count = children.Count;
        if (_cells.Length < count)
        {
            _cells = new CellInfo[Math.Max(count, _cells.Length * 2)];
        }

        var cells = _cells;
        for (int i = 0; i < count; i++)
        {
            ref var cell = ref cells[i];
            if (children[i] is not UIElement child || child.Visibility == Visibility.Collapsed)
            {
                // Collapsed children take no space; Measure just marks them measured.
                (children[i] as UIElement)?.Measure(Size.Zero);
                cell.Child = null;
                continue;
            }

            cell.Child = child;
            cell.Column = Math.Min(GetColumn(child), columns.Length - 1);
            cell.Row = Math.Min(GetRow(child), rows.Length - 1);
            cell.ColumnSpan = Math.Min(GetColumnSpan(child), columns.Length - cell.Column);
            cell.RowSpan = Math.Min(GetRowSpan(child), rows.Length - cell.Row);
            cell.ColumnKind = GetSpanKind(columns, cell.Column, cell.ColumnSpan);
            cell.RowKind = GetSpanKind(rows, cell.Row, cell.RowSpan);
        }

        // Drop references to removed children so the cache doesn't keep them alive.
        for (int i = count; i < _cellCount; i++)
        {
            cells[i].Child = null;
        }
        _cellCount = count;

        return count;
    }

    private static SpanKind GetSpanKind(DefinitionBase[] definitions, int start, int span)
    {
        var kind = SpanKind.Fixed;
        for (int i = start; i < start + span; i++)
        {
            if (definitions[i].IsStarInMeasure) kind |= SpanKind.Star;
            else if (definitions[i].IsAutoInMeasure) kind |= SpanKind.Auto;
        }
        return kind;
    }

    // The size to measure a child with: infinite if the span contains a definition sized to content, otherwise the sum
    // of the (already resolved) definition sizes plus the spacing between them.
    private static float SpanConstraint(DefinitionBase[] definitions, int start, int span, float spacing)
    {
        float size = (span - 1) * spacing;
        for (int i = start; i < start + span; i++)
        {
            if (definitions[i].IsAutoInMeasure)
            {
                return float.PositiveInfinity;
            }
            size += definitions[i].MeasureSize;
        }
        return size;
    }

    private static float DesiredOf(in CellInfo cell, bool isColumn) =>
        isColumn ? cell.Child!.DesiredSize.Width : cell.Child!.DesiredSize.Height;

    // Sizes the auto definitions (and, on an infinite axis, the stars) from the children that span no star definition.
    private static void ResolveContentSizes(DefinitionBase[] definitions, CellInfo[] cells, int count, float spacing, bool isColumn)
    {
        // Children spanning a single definition first...
        for (int i = 0; i < count; i++)
        {
            ref var cell = ref cells[i];
            var kind = isColumn ? cell.ColumnKind : cell.RowKind;
            int span = isColumn ? cell.ColumnSpan : cell.RowSpan;
            if (cell.Child == null || span != 1 || (kind & SpanKind.Auto) == 0)
            {
                continue;
            }

            var definition = definitions[isColumn ? cell.Column : cell.Row];
            definition.ContentSize = Math.Max(definition.ContentSize, DesiredOf(cell, isColumn));
        }

        foreach (var definition in definitions)
        {
            if (definition.IsAutoInMeasure)
            {
                definition.MeasureSize = definition.Clamp(definition.ContentSize);
            }
        }

        // ...then spanning children grow the auto definitions they cover by an equal share of what is missing.
        for (int i = 0; i < count; i++)
        {
            ref var cell = ref cells[i];
            var kind = isColumn ? cell.ColumnKind : cell.RowKind;
            int span = isColumn ? cell.ColumnSpan : cell.RowSpan;
            if (cell.Child == null || span == 1 || kind != SpanKind.Auto)
            {
                continue;
            }

            int start = isColumn ? cell.Column : cell.Row;
            float current = (span - 1) * spacing;
            int autoCount = 0;
            for (int d = start; d < start + span; d++)
            {
                current += definitions[d].MeasureSize;
                if (definitions[d].IsAutoInMeasure) autoCount++;
            }

            float missing = DesiredOf(cell, isColumn) - current;
            if (missing <= 0)
            {
                continue;
            }

            float share = missing / autoCount;
            for (int d = start; d < start + span; d++)
            {
                if (definitions[d].IsAutoInMeasure)
                {
                    definitions[d].MeasureSize = definitions[d].Clamp(definitions[d].MeasureSize + share);
                    definitions[d].ContentSize = Math.Max(definitions[d].ContentSize, definitions[d].MeasureSize);
                }
            }
        }
    }

    // Shares the space left after the pixel and auto definitions among the star definitions (finite axis only).
    private static void ResolveStarsForMeasure(DefinitionBase[] definitions, float available, float spacing)
    {
        if (float.IsPositiveInfinity(available))
        {
            return;
        }

        float remaining = available - (definitions.Length - 1) * spacing;
        bool hasStars = false;
        foreach (var definition in definitions)
        {
            if (definition.IsStarInMeasure) hasStars = true;
            else remaining -= definition.MeasureSize;
        }

        if (hasStars)
        {
            DistributeStars(definitions, Math.Max(0, remaining), forArrange: false);
        }
    }

    // After all children are measured: the content size of each star definition, for the desired size.
    private static void UpdateStarContentSizes(DefinitionBase[] definitions, CellInfo[] cells, int count, float spacing, bool isColumn)
    {
        for (int i = 0; i < count; i++)
        {
            ref var cell = ref cells[i];
            var kind = isColumn ? cell.ColumnKind : cell.RowKind;
            if (cell.Child == null || (kind & SpanKind.Star) == 0)
            {
                continue;
            }

            int start = isColumn ? cell.Column : cell.Row;
            int span = isColumn ? cell.ColumnSpan : cell.RowSpan;
            float needed = DesiredOf(cell, isColumn) - (span - 1) * spacing;
            float starWeight = 0;
            for (int d = start; d < start + span; d++)
            {
                var definition = definitions[d];
                if (definition.IsStarInMeasure) starWeight += definition.Length.Value;
                else needed -= definition.MeasureSize;
            }

            if (needed <= 0)
            {
                continue;
            }

            // Spread what the star definitions must provide by weight (evenly if all weights are zero).
            int starCount = 0;
            for (int d = start; d < start + span; d++)
            {
                if (definitions[d].IsStarInMeasure) starCount++;
            }

            for (int d = start; d < start + span; d++)
            {
                var definition = definitions[d];
                if (definition.IsStarInMeasure)
                {
                    float part = starWeight > 0 ? needed * definition.Length.Value / starWeight : needed / starCount;
                    definition.ContentSize = Math.Max(definition.ContentSize, part);
                }
            }
        }
    }

    // Pixel and auto sizes, plus the stars sized by content: the size per unit of weight is the largest any star needs,
    // so arranging the grid at its desired size gives every star at least its content without changing the ratios.
    private static float DesiredLength(DefinitionBase[] definitions, float spacing)
    {
        float unit = 0;
        foreach (var definition in definitions)
        {
            if (definition.Length.IsStar && definition.Length.Value > 0)
            {
                float content = definition.IsAutoInMeasure ? definition.MeasureSize : definition.ContentSize;
                unit = Math.Max(unit, content / definition.Length.Value);
            }
        }

        float total = (definitions.Length - 1) * spacing;
        foreach (var definition in definitions)
        {
            if (definition.Length.IsStar)
            {
                total += definition.Clamp(unit * definition.Length.Value);
            }
            else
            {
                total += definition.MeasureSize;
            }
        }
        return total;
    }

    #endregion

    #region Arrange helpers

    private static void ResolveForArrange(DefinitionBase[] definitions, float finalLength, float spacing)
    {
        float remaining = finalLength - (definitions.Length - 1) * spacing;
        bool hasStars = false;
        foreach (var definition in definitions)
        {
            if (definition.Length.IsStar)
            {
                hasStars = true;
                continue;
            }

            // Pixel sizes are fixed; auto sizes come from the measure pass.
            definition.ActualSize = definition.MeasureSize;
            remaining -= definition.ActualSize;
        }

        if (hasStars)
        {
            DistributeStars(definitions, Math.Max(0, remaining), forArrange: true);
        }

        float offset = 0;
        foreach (var definition in definitions)
        {
            definition.ActualOffset = offset;
            offset += definition.ActualSize + spacing;
        }
    }

    private static float SpanActualSize(DefinitionBase[] definitions, int start, int span, float spacing)
    {
        float size = (span - 1) * spacing;
        for (int i = start; i < start + span; i++)
        {
            size += definitions[i].ActualSize;
        }
        return size;
    }

    #endregion

    /// <summary>
    /// Shares <paramref name="space"/> among the star definitions by weight, honoring their min/max limits: a star
    /// whose share violates a limit is fixed at that limit and the rest is shared among the others again.
    /// </summary>
    private static void DistributeStars(DefinitionBase[] definitions, float space, bool forArrange)
    {
        int unresolved = 0;
        foreach (var definition in definitions)
        {
            bool isStar = forArrange ? definition.Length.IsStar : definition.IsStarInMeasure;
            definition.IsStarResolved = !isStar;
            if (isStar) unresolved++;
        }

        while (unresolved > 0)
        {
            float weight = 0;
            foreach (var definition in definitions)
            {
                if (!definition.IsStarResolved) weight += definition.Length.Value;
            }

            // Fix the definitions whose share violates a limit, then share again; the loop ends when none does.
            bool fixedAny = false;
            float spaceAfterFixed = space;
            foreach (var definition in definitions)
            {
                if (definition.IsStarResolved)
                {
                    continue;
                }

                float share = weight > 0 ? space * definition.Length.Value / weight : 0;
                float clamped = definition.Clamp(share);
                if (clamped != share)
                {
                    SetStarSize(definition, clamped, forArrange);
                    definition.IsStarResolved = true;
                    spaceAfterFixed -= clamped;
                    unresolved--;
                    fixedAny = true;
                }
            }

            if (!fixedAny)
            {
                foreach (var definition in definitions)
                {
                    if (!definition.IsStarResolved)
                    {
                        SetStarSize(definition, weight > 0 ? space * definition.Length.Value / weight : 0, forArrange);
                        definition.IsStarResolved = true;
                    }
                }
                break;
            }

            space = Math.Max(0, spaceAfterFixed);
        }
    }

    private static void SetStarSize(DefinitionBase definition, float size, bool forArrange)
    {
        if (forArrange) definition.ActualSize = size;
        else definition.MeasureSize = size;
    }
}
