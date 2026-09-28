using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Numerics;
using Atelier.Core.Events;
using Atelier.Core.Platform;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Rendering;

namespace Atelier.Controls;

/// <summary>How the rows of a <see cref="DataGrid"/> are selected.</summary>
public enum DataGridSelectionMode
{
    /// <summary>One row at a time.</summary>
    Single,

    /// <summary>Several rows with check boxes: a click toggles a row, the header box selects all.</summary>
    Multiple,

    /// <summary>Several rows with Ctrl+click (toggle) and Shift+click (range), like a file explorer.</summary>
    Extended,
}

/// <summary>What a click selects in a <see cref="DataGrid"/>.</summary>
public enum DataGridSelectionUnit
{
    /// <summary>Whole rows (see <see cref="DataGrid.SelectedItems"/>).</summary>
    Row,

    /// <summary>Single cells, like a spreadsheet (see <see cref="DataGrid.SelectedCells"/>).</summary>
    Cell,
}

/// <summary>What <see cref="DataGrid.CopyToClipboard"/> (Ctrl+C) copies.</summary>
public enum DataGridClipboardCopyMode
{
    /// <summary>Nothing: Ctrl+C is left to the application.</summary>
    None,

    /// <summary>The selected cells (or rows) as tab-separated text.</summary>
    ExcludeHeader,

    /// <summary>The selected cells (or rows) as tab-separated text, after a line with the column headers.</summary>
    IncludeHeader,
}

/// <summary>A cell of a <see cref="DataGrid"/>: an item and a column.</summary>
/// <param name="Item">The item (row).</param>
/// <param name="Column">The column.</param>
public readonly record struct DataGridCellInfo(object Item, DataGridColumn Column)
{
    /// <summary>Gets the cell's text (<see cref="DataGridColumn.GetCellText"/>, or else its sort value as text).</summary>
    public string Text => Column.GetCellText(Item) ?? Column.GetSortValue(Item)?.ToString() ?? string.Empty;

    /// <inheritdoc/>
    public bool Equals(DataGridCellInfo other) => ReferenceEquals(Item, other.Item) && ReferenceEquals(Column, other.Column);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Item), System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Column));
}

/// <summary>
/// A table of items in columns, with sorting, filtering, resizable and movable columns, a column chooser, and single,
/// check box or extended selection. Rows are virtualized: only the rows in view exist, so it handles large lists.
/// </summary>
/// <remarks>
/// <para>
/// Items come from <see cref="ItemsSource"/> (changes of an <see cref="INotifyCollectionChanged"/> source show at once;
/// changed item properties update their row). The grid shows a view of them: filtered by <see cref="Filter"/> and the
/// columns' <see cref="DataGridColumn.FilterText"/> (typed into the filter row with <see cref="ShowFilterRow"/>), and
/// sorted by the sort columns. The source is never reordered.
/// </para>
/// <para>
/// Click a header to sort (ascending, descending, off); Shift+click adds a secondary sort. Drag a header to move the
/// column, drag its right edge to resize it (double-click to fit), and right-click the headers for the column chooser.
/// <see cref="SaveLayout"/> and <see cref="RestoreLayout"/> keep the column layout and sort between sessions;
/// <see cref="LayoutChanged"/> tells when it changed.
/// </para>
/// <para>
/// The grid is one tab stop. Up/Down, Page Up/Down and Home/End move the current row (selecting it, or with Shift
/// extending the selection in <see cref="DataGridSelectionMode.Extended"/>); Ctrl moves without selecting, Space
/// toggles, Ctrl+A selects all, Enter or a double-click raise <see cref="RowActivated"/>, and a right-click, the menu
/// key or Shift+F10 open <see cref="RowContextMenu"/> for the row.
/// </para>
/// </remarks>
public class DataGrid : Control, IVirtualItemsGenerator
{
    #region Properties

    /// <summary>Identifies the <see cref="ItemsSource"/> property.</summary>
    public static readonly BindableProperty<IEnumerable?> ItemsSourceProperty =
        BindableProperty.Register<DataGrid, IEnumerable?>(nameof(ItemsSource), null, (s, o, n) => ((DataGrid)s).OnItemsSourceChanged(n));

    /// <summary>Identifies the <see cref="SelectionMode"/> property.</summary>
    public static readonly BindableProperty<DataGridSelectionMode> SelectionModeProperty =
        BindableProperty.Register<DataGrid, DataGridSelectionMode>(nameof(SelectionMode), DataGridSelectionMode.Single, (s, o, n) => ((DataGrid)s).OnSelectionModeChanged());

    /// <summary>Identifies the <see cref="SelectionUnit"/> property.</summary>
    public static readonly BindableProperty<DataGridSelectionUnit> SelectionUnitProperty =
        BindableProperty.Register<DataGrid, DataGridSelectionUnit>(nameof(SelectionUnit), DataGridSelectionUnit.Row, (s, o, n) => ((DataGrid)s).OnSelectionUnitChanged());

    /// <summary>Identifies the <see cref="ClipboardCopyMode"/> property.</summary>
    public static readonly BindableProperty<DataGridClipboardCopyMode> ClipboardCopyModeProperty =
        BindableProperty.Register<DataGrid, DataGridClipboardCopyMode>(nameof(ClipboardCopyMode), DataGridClipboardCopyMode.ExcludeHeader);

    /// <summary>
    /// Gets or sets whether clicks select rows (the default) or cells. With <see cref="DataGridSelectionUnit.Cell"/>,
    /// <see cref="SelectionMode"/> still decides how many: one cell, cells toggled by clicks
    /// (<see cref="DataGridSelectionMode.Multiple"/>, without check boxes), or Ctrl/Shift like a spreadsheet
    /// (<see cref="DataGridSelectionMode.Extended"/>, where Shift selects a rectangle).
    /// </summary>
    public DataGridSelectionUnit SelectionUnit { get => GetValue(SelectionUnitProperty); set => SetValue(SelectionUnitProperty, value); }

    /// <summary>Gets or sets what Ctrl+C copies. The default is the selection without the header.</summary>
    public DataGridClipboardCopyMode ClipboardCopyMode { get => GetValue(ClipboardCopyModeProperty); set => SetValue(ClipboardCopyModeProperty, value); }

    /// <summary>
    /// Gets the selected cells with <see cref="DataGridSelectionUnit.Cell"/>, in the order they were selected (empty
    /// with <see cref="DataGridSelectionUnit.Row"/>). <see cref="SelectedItems"/> then holds the items that have a
    /// selected cell.
    /// </summary>
    public IReadOnlyList<DataGridCellInfo> SelectedCells => _selectedCellList;

    /// <summary>Gets the current cell (the keyboard cursor), or <c>null</c> without a current row or column.</summary>
    public DataGridCellInfo? CurrentCell => CurrentItem is { } item && CurrentColumn is { } column ? new DataGridCellInfo(item, column) : null;

    // Whether rows have selection check boxes: Multiple selection of rows.
    internal bool ShowsSelectionColumn => SelectionMode == DataGridSelectionMode.Multiple && SelectionUnit == DataGridSelectionUnit.Row;

    private bool IsCellUnit => SelectionUnit == DataGridSelectionUnit.Cell;

    /// <summary>Identifies the <see cref="SelectedItem"/> property.</summary>
    public static readonly BindableProperty<object?> SelectedItemProperty =
        BindableProperty.Register<DataGrid, object?>(nameof(SelectedItem), null, (s, o, n) => ((DataGrid)s).OnSelectedItemChanged(n));

    /// <summary>Identifies the <see cref="RowHeight"/> property.</summary>
    public static readonly BindableProperty<float> RowHeightProperty =
        BindableProperty.Register<DataGrid, float>(nameof(RowHeight), 40f, (s, o, n) => ((DataGrid)s).OnRowHeightChanged(), validateValue: static v => v > 0 && float.IsFinite(v));

    /// <summary>Identifies the <see cref="HeaderHeight"/> property.</summary>
    public static readonly BindableProperty<float> HeaderHeightProperty =
        BindableProperty.Register<DataGrid, float>(nameof(HeaderHeight), 44f, options: PropertyOptions.AffectsMeasure, validateValue: static v => v >= 0 && float.IsFinite(v));

    /// <summary>Identifies the <see cref="ShowFilterRow"/> property.</summary>
    public static readonly BindableProperty<bool> ShowFilterRowProperty =
        BindableProperty.Register<DataGrid, bool>(nameof(ShowFilterRow), false, (s, o, n) => ((DataGrid)s).OnColumnsStructureChanged());

    /// <summary>Identifies the <see cref="CanSortColumns"/> property.</summary>
    public static readonly BindableProperty<bool> CanSortColumnsProperty =
        BindableProperty.Register<DataGrid, bool>(nameof(CanSortColumns), true);

    /// <summary>Identifies the <see cref="CanResizeColumns"/> property.</summary>
    public static readonly BindableProperty<bool> CanResizeColumnsProperty =
        BindableProperty.Register<DataGrid, bool>(nameof(CanResizeColumns), true);

    /// <summary>Identifies the <see cref="CanReorderColumns"/> property.</summary>
    public static readonly BindableProperty<bool> CanReorderColumnsProperty =
        BindableProperty.Register<DataGrid, bool>(nameof(CanReorderColumns), true);

    /// <summary>Identifies the <see cref="ShowColumnChooser"/> property.</summary>
    public static readonly BindableProperty<bool> ShowColumnChooserProperty =
        BindableProperty.Register<DataGrid, bool>(nameof(ShowColumnChooser), true);

    /// <summary>Identifies the <see cref="EmptyContent"/> property.</summary>
    public static readonly BindableProperty<object?> EmptyContentProperty =
        BindableProperty.Register<DataGrid, object?>(nameof(EmptyContent), "No items", (s, o, n) => ((DataGrid)s).UpdatePlaceholder());

    /// <summary>Identifies the <see cref="NoMatchesContent"/> property.</summary>
    public static readonly BindableProperty<object?> NoMatchesContentProperty =
        BindableProperty.Register<DataGrid, object?>(nameof(NoMatchesContent), "No matching items", (s, o, n) => ((DataGrid)s).UpdatePlaceholder());

    /// <summary>Identifies the <see cref="RowContextMenu"/> property.</summary>
    public static readonly BindableProperty<ContextMenu?> RowContextMenuProperty =
        BindableProperty.Register<DataGrid, ContextMenu?>(nameof(RowContextMenu), null);

    /// <summary>The width of the selection check box column in <see cref="DataGridSelectionMode.Multiple"/>.</summary>
    public const float SelectionColumnWidth = 52f;

    /// <summary>Gets or sets the items.</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>Gets or sets how rows are selected. The default is <see cref="DataGridSelectionMode.Single"/>.</summary>
    public DataGridSelectionMode SelectionMode { get => GetValue(SelectionModeProperty); set => SetValue(SelectionModeProperty, value); }

    /// <summary>
    /// Gets or sets the selected item (with several selected, the first one); setting it selects only that item.
    /// </summary>
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }

    /// <summary>Gets or sets the height of each row. The default is 40.</summary>
    public float RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    /// <summary>Gets or sets the height of the header row. The default is 44.</summary>
    public float HeaderHeight { get => GetValue(HeaderHeightProperty); set => SetValue(HeaderHeightProperty, value); }

    /// <summary>Gets or sets whether a row of filter boxes (one per column) shows under the headers. The default is <c>false</c>.</summary>
    public bool ShowFilterRow { get => GetValue(ShowFilterRowProperty); set => SetValue(ShowFilterRowProperty, value); }

    /// <summary>Gets or sets whether clicking headers sorts. The default is <c>true</c>.</summary>
    public bool CanSortColumns { get => GetValue(CanSortColumnsProperty); set => SetValue(CanSortColumnsProperty, value); }

    /// <summary>Gets or sets whether columns can be resized. The default is <c>true</c>.</summary>
    public bool CanResizeColumns { get => GetValue(CanResizeColumnsProperty); set => SetValue(CanResizeColumnsProperty, value); }

    /// <summary>Gets or sets whether columns can be dragged to a new place. The default is <c>true</c>.</summary>
    public bool CanReorderColumns { get => GetValue(CanReorderColumnsProperty); set => SetValue(CanReorderColumnsProperty, value); }

    /// <summary>Gets or sets whether right-clicking the headers opens the column chooser. The default is <c>true</c>.</summary>
    public bool ShowColumnChooser { get => GetValue(ShowColumnChooserProperty); set => SetValue(ShowColumnChooserProperty, value); }

    /// <summary>Gets or sets what shows while there are no items (a string or an element). The default is "No items".</summary>
    public object? EmptyContent { get => GetValue(EmptyContentProperty); set => SetValue(EmptyContentProperty, value); }

    /// <summary>Gets or sets what shows when the filters hide all items. The default is "No matching items".</summary>
    public object? NoMatchesContent { get => GetValue(NoMatchesContentProperty); set => SetValue(NoMatchesContentProperty, value); }

    /// <summary>Gets or sets the menu of a row, opened with its DataContext set to the row's item.</summary>
    public ContextMenu? RowContextMenu { get => GetValue(RowContextMenuProperty); set => SetValue(RowContextMenuProperty, value); }

    /// <summary>Identifies the <see cref="ShowFilterMenus"/> property.</summary>
    public static readonly BindableProperty<bool> ShowFilterMenusProperty =
        BindableProperty.Register<DataGrid, bool>(nameof(ShowFilterMenus), true, (s, o, n) => ((DataGrid)s).OnColumnsStructureChanged());

    /// <summary>Identifies the <see cref="IsReadOnly"/> property.</summary>
    public static readonly BindableProperty<bool> IsReadOnlyProperty =
        BindableProperty.Register<DataGrid, bool>(nameof(IsReadOnly), false, (s, o, n) => ((DataGrid)s).OnColumnEditabilityChanged());

    /// <summary>
    /// Gets or sets whether headers have a filter button (shown on hover, and while the column is filtered) that opens a
    /// spreadsheet-style filter menu: sort, clear, and a searchable list of the column's values to check. The default is
    /// <c>true</c>.
    /// </summary>
    public bool ShowFilterMenus { get => GetValue(ShowFilterMenusProperty); set => SetValue(ShowFilterMenusProperty, value); }

    /// <summary>Gets or sets whether no cell can be edited. The default is <c>false</c>.</summary>
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>
    /// Gets the column of the current cell (moved with Left and Right, set by clicking a cell), or <c>null</c> before
    /// one was chosen. F2 edits the current cell.
    /// </summary>
    public DataGridColumn? CurrentColumn { get; private set; }

    /// <summary>Gets the edit in progress, or <c>null</c>.</summary>
    public DataGridEditContext? EditContext { get; private set; }

    /// <summary>Gets whether a cell is being edited.</summary>
    public bool IsEditing => EditContext != null;

    /// <summary>Occurs when a column's filter changed (by the filter row, a filter menu or code).</summary>
    public event EventHandler? FilterChanged;

    /// <summary>Occurs before an edited value is stored: change the value, reject it with an error, or cancel the edit.</summary>
    public event EventHandler<DataGridCellEditEndingEventArgs>? CellEditEnding;

    /// <summary>Occurs after an edit ended, with <c>true</c> if the value was stored.</summary>
    public event EventHandler<bool>? CellEditEnded;

    /// <summary>Gets the columns, in display order.</summary>
    public ObservableCollection<DataGridColumn> Columns { get; } = [];

    /// <summary>Gets or sets a filter of the items: items it returns <c>false</c> for are hidden. Call <see cref="Refresh"/> after it changes its mind.</summary>
    public Predicate<object>? Filter
    {
        get => _filter;
        set
        {
            _filter = value;
            RefreshView();
        }
    }

    /// <summary>Gets the selected items, in the order they were selected. Changing it changes the selection.</summary>
    public ObservableCollection<object> SelectedItems { get; } = [];

    /// <summary>Gets the items shown, filtered and sorted.</summary>
    public IReadOnlyList<object> View => _view;

    /// <summary>Gets the index of the current row (the keyboard cursor) in <see cref="View"/>, or -1.</summary>
    public int CurrentIndex { get; private set; } = -1;

    /// <summary>Gets the item of the current row, or <c>null</c>.</summary>
    public object? CurrentItem => CurrentIndex >= 0 && CurrentIndex < _view.Count ? _view[CurrentIndex] : null;

    /// <summary>Gets the sort columns, the primary one first.</summary>
    public IReadOnlyList<DataGridColumn> SortColumns => _sortColumns;

    /// <summary>Gets the visible columns in display order.</summary>
    public IReadOnlyList<DataGridColumn> VisibleColumns => _visibleColumns;

    /// <summary>Gets the total width of the columns (and the selection column).</summary>
    public float TotalColumnsWidth { get; private set; }

    /// <summary>Gets the virtualizing panel of the rows.</summary>
    public VirtualizingStackPanel RowsPanel => _rows;

    /// <summary>Gets the scroll viewer of the rows.</summary>
    public ScrollViewer ScrollViewer => _scrollViewer;

    /// <summary>Gets the headers of the visible columns.</summary>
    public IReadOnlyList<DataGridColumnHeader> Headers => _headers;

    /// <summary>Gets the x of the drop marker while a header is dragged (in this control's coordinates), or <see cref="float.NaN"/>.</summary>
    public float DropIndicatorX { get; private set; } = float.NaN;

    /// <summary>Gets the bottom of the header area (headers and filter row).</summary>
    public float HeaderAreaHeight => HeaderHeight + (ShowFilterRow ? FilterRowHeight : 0);

    /// <summary>Occurs when the selection changed.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Occurs when a row is activated with a double-click or Enter, with its item.</summary>
    public event EventHandler<object>? RowActivated;

    /// <summary>Occurs when the column layout or sort changed (by the user or in code), e.g. to save it.</summary>
    public event EventHandler? LayoutChanged;

    internal int ColumnsVersion { get; private set; }

    #endregion

    private const float FilterRowHeight = 48f;

    private readonly VirtualizingStackPanel _rows = new() { OverscanCount = 4 };
    private readonly ScrollViewer _scrollViewer = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly Border _headerHost = new() { ClipToBounds = true };
    private readonly Border _filterHost = new() { ClipToBounds = true, Visibility = Visibility.Collapsed };
    private readonly DataGridColumnsPanel _headerPanel;
    private readonly DataGridColumnsPanel _filterPanel;
    private readonly CheckBox _selectAllBox = new() { IsFocusable = false, IsThreeState = false };
    private readonly ContentControl _placeholder = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
    private readonly List<DataGridColumn> _visibleColumns = [];
    private readonly List<DataGridColumnHeader> _headers = [];
    private readonly List<DataGridColumn> _sortColumns = [];
    private readonly HashSet<object> _selected = new(ReferenceEqualityComparer.Instance);
    private List<object> _view = [];
    private int _sourceCount;
    private Predicate<object>? _filter;
    private WeakCollectionChangedSubscription<DataGrid>? _sourceSubscription;
    private int _anchorIndex = -1;
    private bool _isUpdatingSelection;
    private bool _isUpdatingSelectAll;
    private int _dropIndex = -1;

    /// <summary>Initializes an empty grid.</summary>
    public DataGrid()
    {
        IsFocusable = true;
        _headerPanel = new DataGridColumnsPanel(this) { Leading = _selectAllBox };
        _filterPanel = new DataGridColumnsPanel(this) { RowHeight = FilterRowHeight };
        _headerHost.Child = _headerPanel;
        _filterHost.Child = _filterPanel;
        _headerPanel.Add(_selectAllBox);
        _selectAllBox.CheckedChanged += (_, _) => OnSelectAllClicked();

        _rows.ItemHeight = RowHeight;
        _rows.Generator = this;
        _scrollViewer.Content = _rows;
        _scrollViewer.ScrollChanged += (_, _) => SyncHeaderScroll();

        AddChild(_headerHost);
        AddChild(_filterHost);
        AddChild(_scrollViewer);
        AddChild(_placeholder);

        Columns.CollectionChanged += (_, _) => OnColumnsStructureChanged();
        SelectedItems.CollectionChanged += OnSelectedItemsCollectionChanged;
        OnColumnsStructureChanged();
        UpdatePlaceholder();
    }

    #region Items and view

    private void OnItemsSourceChanged(IEnumerable? source)
    {
        _sourceSubscription?.Dispose();
        _sourceSubscription = null;
        if (source is INotifyCollectionChanged incc)
        {
            _sourceSubscription = new WeakCollectionChangedSubscription<DataGrid>(incc, this, static (grid, _) => grid.RefreshView());
        }
        foreach (var column in Columns) column.AutoWidth = float.NaN; // fit Auto columns to the new items
        RefreshView();
        _scrollViewer.ScrollTo(0, 0, animate: false);
    }

    /// <summary>Filters and sorts the items again, e.g. after <see cref="Filter"/> or sorted values changed.</summary>
    public void Refresh() => RefreshView();

    internal void RefreshView()
    {
        var current = CurrentItem;
        var items = new List<object>();
        int sourceCount = 0;
        if (ItemsSource != null)
        {
            foreach (var item in ItemsSource)
            {
                if (item == null) continue;
                sourceCount++;
                if (_filter != null && !_filter(item)) continue;
                bool passes = true;
                foreach (var column in Columns)
                {
                    if (!column.PassesFilter(item))
                    {
                        passes = false;
                        break;
                    }
                }
                if (passes) items.Add(item);
            }
        }
        _sourceCount = sourceCount;

        if (_sortColumns.Count > 0 && items.Count > 1)
        {
            items = SortItems(items);
        }
        _view = items;

        // Keep the selection to what is still shown.
        var visible = new HashSet<object>(items, ReferenceEqualityComparer.Instance);
        bool selectionChanged = false;
        _isUpdatingSelection = true;
        try
        {
            for (int i = SelectedItems.Count - 1; i >= 0; i--)
            {
                if (!visible.Contains(SelectedItems[i]))
                {
                    _selected.Remove(SelectedItems[i]);
                    SelectedItems.RemoveAt(i);
                    selectionChanged = true;
                }
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
        if (IsCellUnit && PruneSelectedCells()) selectionChanged = true;

        CurrentIndex = current != null ? items.IndexOf(current) : -1;
        if (CurrentIndex < 0 && items.Count > 0 && current != null) CurrentIndex = 0;
        _anchorIndex = Math.Min(_anchorIndex, items.Count - 1);

        _rows.Reset();
        UpdatePlaceholder();
        UpdateSelectAllBox();
        if (selectionChanged) OnSelectionChanged();
        InvalidateMeasure();
    }

    private List<object> SortItems(List<object> items)
    {
        int keyCount = _sortColumns.Count;
        var keys = new object?[items.Count][];
        for (int i = 0; i < items.Count; i++)
        {
            var row = new object?[keyCount];
            for (int k = 0; k < keyCount; k++) row[k] = _sortColumns[k].GetSortValue(items[i]);
            keys[i] = row;
        }

        var order = new int[items.Count];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        Array.Sort(order, (a, b) =>
        {
            for (int k = 0; k < keyCount; k++)
            {
                var column = _sortColumns[k];
                int c = column.Compare(keys[a][k], keys[b][k]);
                if (c != 0) return column.SortDirection == DataGridSortDirection.Descending ? -c : c;
            }
            return a.CompareTo(b); // stable
        });

        var sorted = new List<object>(items.Count);
        foreach (int i in order) sorted.Add(items[i]);
        return sorted;
    }

    private void UpdatePlaceholder()
    {
        bool empty = _view.Count == 0;
        _placeholder.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        if (!empty) return;
        object? content = _sourceCount > 0 ? NoMatchesContent : EmptyContent;
        _placeholder.Content = content is string text ? new TextBlock { Text = text, Muted = true } : content;
    }

    #endregion

    #region IVirtualItemsGenerator

    int IVirtualItemsGenerator.ItemCount => _view.Count;

    UIElement IVirtualItemsGenerator.CreateContainer() => new DataGridRow(this);

    void IVirtualItemsGenerator.PrepareContainer(UIElement container, int index) => ((DataGridRow)container).Prepare(_view[index], index);

    void IVirtualItemsGenerator.ClearContainer(UIElement container)
    {
        var row = (DataGridRow)container;
        if (EditContext != null && row.EditingColumn != null) EndEditForReuse();
        row.Clear();
    }

    /// <summary>Gets the row of the item at <paramref name="index"/> in <see cref="View"/> if it is in view, otherwise <c>null</c>.</summary>
    public DataGridRow? GetRow(int index) => _rows.ContainerFromIndex(index) as DataGridRow;

    private void UpdateRealizedRows()
    {
        foreach (var container in _rows.RealizedContainers.Values)
        {
            var row = (DataGridRow)container;
            if (row.Item == null) continue;
            row.IsSelected = !IsCellUnit && _selected.Contains(row.Item);
            if (IsCellUnit) row.InvalidateVisual(); // its selected cells
            row.IsCurrent = row.Index == CurrentIndex;
        }
    }

    private void OnRowHeightChanged()
    {
        _rows.ItemHeight = RowHeight;
        InvalidateMeasure();
    }

    #endregion

    #region Columns

    internal void OnColumnsStructureChanged()
    {
        if (EditContext != null) EndEditForReuse();
        foreach (var column in Columns) column.Owner = this;

        _visibleColumns.Clear();
        foreach (var column in Columns)
        {
            if (column.IsVisible) _visibleColumns.Add(column);
        }

        // Headers and filter boxes for the visible columns.
        foreach (var (_, element) in _headerPanel.Items) _headerPanel.Remove(element);
        foreach (var (_, element) in _filterPanel.Items) _filterPanel.Remove(element);
        _headerPanel.Items.Clear();
        _filterPanel.Items.Clear();
        _headers.Clear();

        foreach (var column in _visibleColumns)
        {
            var header = new DataGridColumnHeader(this, column);
            _headers.Add(header);
            _headerPanel.Items.Add((column, header));
            _headerPanel.Add(header);

            if (ShowFilterRow)
            {
                var box = new TextBox { Placeholder = "Filter", FieldHeight = 36, Text = column.FilterText, TrailingIconKind = MaterialIconKind.None };
                var target = column;
                box.TextChanged += (_, text) =>
                {
                    if (target.FilterText != text) target.FilterText = text;
                };
                _filterPanel.Items.Add((column, box));
                _filterPanel.Add(box);
            }
        }

        if (CurrentColumn != null && !_visibleColumns.Contains(CurrentColumn)) CurrentColumn = null;
        bool cellsDropped = PruneSelectedCells(); // cells of hidden columns
        _anchorColumn = Math.Min(_anchorColumn, _visibleColumns.Count - 1);
        _selectAllBox.Visibility = ShowsSelectionColumn ? Visibility.Visible : Visibility.Collapsed;
        _filterHost.Visibility = ShowFilterRow ? Visibility.Visible : Visibility.Collapsed;
        _headerPanel.RowHeight = HeaderHeight;

        // Sort columns that were removed or hidden stop sorting.
        _sortColumns.RemoveAll(c => !Columns.Contains(c));
        UpdateSortState();

        ColumnsVersion++;
        _rows.Reset();
        InvalidateMeasure();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
        if (cellsDropped) SyncItemsFromCells();
    }

    internal void OnColumnHeaderChanged(DataGridColumn column)
    {
        foreach (var header in _headers)
        {
            if (header.Column == column) header.UpdateContent();
        }
    }

    internal void OnColumnLayoutChanged() => InvalidateMeasure();

    // A column's FilterText or FilterValues changed: update its filter box and header, and filter again.
    internal void OnColumnFilterChanged(DataGridColumn column)
    {
        foreach (var (owner, element) in _filterPanel.Items)
        {
            if (owner == column && element is TextBox box && box.Text != column.FilterText) box.Text = column.FilterText;
        }
        foreach (var header in _headers)
        {
            if (header.Column == column) header.UpdateFilterButton();
        }
        RefreshView();
        FilterChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void OnColumnEditabilityChanged()
    {
        if (EditContext != null && (IsReadOnly || !EditContext.Column.CanEdit)) CancelEdit();
        _rows.RefreshRealized(); // check box cells follow IsReadOnly
    }

    /// <summary>Clears the filters of all columns (<see cref="Filter"/> stays).</summary>
    public void ClearFilters()
    {
        foreach (var column in Columns)
        {
            if (column.IsFiltered) column.ClearFilter();
        }
    }

    /// <summary>Gets whether any column filters the items.</summary>
    public bool HasColumnFilters
    {
        get
        {
            foreach (var column in Columns)
            {
                if (column.IsFiltered) return true;
            }
            return false;
        }
    }

    /// <summary>Gets the number of items in <see cref="ItemsSource"/> (before filtering).</summary>
    public int SourceCount => _sourceCount;

    /// <summary>
    /// Opens the filter menu of <paramref name="column"/> under its header (see <see cref="ShowFilterMenus"/>); returns
    /// <c>false</c> if the column isn't shown or has nothing to filter by.
    /// </summary>
    public bool OpenFilterMenu(DataGridColumn column)
    {
        if (!column.IsFilterable) return false;
        foreach (var header in _headers)
        {
            if (header.Column == column)
            {
                _filterMenu ??= new DataGridFilterMenu(this);
                _filterMenu.Open(header);
                return true;
            }
        }
        return false;
    }

    /// <summary>Gets the open filter menu, or <c>null</c>.</summary>
    public DataGridFilterMenu? OpenedFilterMenu => _filterMenu is { IsOpen: true } menu ? menu : null;

    private DataGridFilterMenu? _filterMenu;

    // The distinct filter keys of the items that pass every filter except the column's own (as a spreadsheet does), in
    // sort order, with their counts.
    internal List<(string Key, int Count)> GetFilterValues(DataGridColumn column)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var sortValues = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (ItemsSource != null)
        {
            foreach (var item in ItemsSource)
            {
                if (item == null) continue;
                if (_filter != null && !_filter(item)) continue;
                bool passes = true;
                foreach (var other in Columns)
                {
                    if (other != column && !other.PassesFilter(item))
                    {
                        passes = false;
                        break;
                    }
                }
                if (!passes) continue;

                string key = column.GetFilterKey(item);
                if (counts.TryGetValue(key, out int count))
                {
                    counts[key] = count + 1;
                }
                else
                {
                    counts[key] = 1;
                    sortValues[key] = column.GetSortValue(item) ?? key;
                }
            }
        }

        var list = new List<(string Key, int Count)>(counts.Count);
        foreach (var (key, count) in counts) list.Add((key, count));
        list.Sort((a, b) =>
        {
            // Blanks last, the rest in the column's sort order.
            if (a.Key.Length == 0 || b.Key.Length == 0) return (a.Key.Length == 0 ? 1 : 0) - (b.Key.Length == 0 ? 1 : 0);
            int c = column.Compare(sortValues[a.Key], sortValues[b.Key]);
            return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key);
        });
        return list;
    }

    private void OnSelectionModeChanged()
    {
        if (SelectionMode == DataGridSelectionMode.Single && SelectedItems.Count > 1)
        {
            var first = SelectedItems[0];
            SetSelection([first]);
        }
        OnColumnsStructureChanged();
    }

    // Computes the actual widths: pixel widths, Auto columns fitted to their content, star columns sharing the rest.
    private void UpdateColumnWidths(float viewportWidth)
    {
        float fixedWidth = ShowsSelectionColumn ? SelectionColumnWidth : 0;
        float stars = 0;
        foreach (var column in _visibleColumns)
        {
            var width = column.Width;
            if (width.IsStar)
            {
                stars += Math.Max(0, width.Value);
                continue;
            }
            float w = width.IsAuto ? (float.IsNaN(column.AutoWidth) ? column.AutoWidth = MeasureColumnContent(column) : column.AutoWidth) : width.Value;
            column.ActualWidth = Math.Clamp(w, column.MinWidth, Math.Max(column.MinWidth, column.MaxWidth));
            fixedWidth += column.ActualWidth;
        }

        float remaining = float.IsFinite(viewportWidth) ? Math.Max(0, viewportWidth - fixedWidth) : 0;
        float total = fixedWidth;
        foreach (var column in _visibleColumns)
        {
            if (!column.Width.IsStar) continue;
            float w = stars > 0 ? remaining * Math.Max(0, column.Width.Value) / stars : 0;
            column.ActualWidth = Math.Clamp(w, column.MinWidth, Math.Max(column.MinWidth, column.MaxWidth));
            total += column.ActualWidth;
        }
        TotalColumnsWidth = total;
    }

    // The width that shows the header and the text of the first items without trimming.
    private float MeasureColumnContent(DataGridColumn column)
    {
        float padding = DataGridRow.CellPadding * 2;
        float width = TextMeasurer.Measure(column.Header?.ToString() ?? string.Empty, FontSize, FontFamily).Width + padding + 24;
        int count = Math.Min(_view.Count, 200);
        for (int i = 0; i < count; i++)
        {
            if (column.GetCellText(_view[i]) is { Length: > 0 } text)
            {
                width = Math.Max(width, TextMeasurer.Measure(text, FontSize, FontFamily).Width + padding);
            }
        }
        return width;
    }

    /// <summary>Sets the width of <paramref name="column"/> to fit its header and content.</summary>
    public void AutoFitColumn(DataGridColumn column)
    {
        column.Width = GridLength.Pixels(Math.Clamp(MeasureColumnContent(column), column.MinWidth, Math.Max(column.MinWidth, column.MaxWidth)));
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    internal void ResizeColumn(DataGridColumn column, float width)
    {
        column.Width = GridLength.Pixels(Math.Clamp(width, column.MinWidth, Math.Max(column.MinWidth, column.MaxWidth)));
    }

    internal void OnColumnResized() => LayoutChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>Moves <paramref name="column"/> to <paramref name="index"/> among all columns.</summary>
    public void MoveColumn(DataGridColumn column, int index)
    {
        int from = Columns.IndexOf(column);
        if (from < 0 || index < 0 || index >= Columns.Count || from == index) return;
        Columns.Move(from, index);
    }

    #endregion

    #region Header dragging

    internal void OnHeaderDragged(DataGridColumnHeader header, float dx)
    {
        header.RenderTransform = Matrix3x2.CreateTranslation(dx, 0);

        // The drop position: before the first header whose middle is right of the dragged header's middle.
        float center = header.Bounds.X + header.Bounds.Width * 0.5f + dx;
        int visibleIndex = _headers.Count;
        for (int i = 0; i < _headers.Count; i++)
        {
            var other = _headers[i];
            if (center < other.Bounds.X + other.Bounds.Width * 0.5f)
            {
                visibleIndex = i;
                break;
            }
        }
        _dropIndex = visibleIndex;

        float x = visibleIndex < _headers.Count ? _headers[visibleIndex].Bounds.X : (_headers.Count > 0 ? _headers[^1].Bounds.Right : 0);
        DropIndicatorX = _headerHost.Bounds.X + x - _scrollViewer.ScrollOffsetX;
        InvalidateVisual();
    }

    internal void OnHeaderDropped(DataGridColumnHeader header)
    {
        int visibleIndex = _dropIndex;
        DropIndicatorX = float.NaN;
        _dropIndex = -1;
        InvalidateVisual();
        if (visibleIndex < 0) return;

        var column = header.Column;
        int currentVisible = _visibleColumns.IndexOf(column);
        if (visibleIndex == currentVisible || visibleIndex == currentVisible + 1) return;

        // Translate the visible position into a position among all columns.
        int target;
        if (visibleIndex >= _visibleColumns.Count)
        {
            target = Columns.Count - 1;
        }
        else
        {
            target = Columns.IndexOf(_visibleColumns[visibleIndex]);
            if (Columns.IndexOf(column) < target) target--;
        }
        MoveColumn(column, target);
    }

    internal void OnHeaderDragCanceled(DataGridColumnHeader header)
    {
        DropIndicatorX = float.NaN;
        _dropIndex = -1;
        InvalidateVisual();
    }

    private void SyncHeaderScroll()
    {
        var shift = Matrix3x2.CreateTranslation(-_scrollViewer.ScrollOffsetX, 0);
        _headerPanel.RenderTransform = shift;
        _filterPanel.RenderTransform = shift;
    }

    #endregion

    #region Sorting

    internal void OnHeaderClicked(DataGridColumn column, bool addToSort)
    {
        if (!CanSortColumns || !column.IsSortable) return;

        int position = _sortColumns.IndexOf(column);
        if (addToSort && _sortColumns.Count > 0)
        {
            if (position < 0)
            {
                column.SortDirection = DataGridSortDirection.Ascending;
                _sortColumns.Add(column);
            }
            else if (column.SortDirection == DataGridSortDirection.Ascending)
            {
                column.SortDirection = DataGridSortDirection.Descending;
            }
            else
            {
                _sortColumns.RemoveAt(position);
                column.SortDirection = DataGridSortDirection.None;
            }
        }
        else
        {
            var next = position == 0 && _sortColumns.Count == 1
                ? column.SortDirection switch
                {
                    DataGridSortDirection.Ascending => DataGridSortDirection.Descending,
                    DataGridSortDirection.Descending => DataGridSortDirection.None,
                    _ => DataGridSortDirection.Ascending,
                }
                : DataGridSortDirection.Ascending;
            SortBy(column, next);
            return;
        }

        UpdateSortState();
        RefreshView();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sorts by <paramref name="column"/> alone (<see cref="DataGridSortDirection.None"/> removes the sort).</summary>
    public void SortBy(DataGridColumn? column, DataGridSortDirection direction)
    {
        foreach (var sorted in _sortColumns) sorted.SortDirection = DataGridSortDirection.None;
        _sortColumns.Clear();
        if (column != null && direction != DataGridSortDirection.None)
        {
            column.SortDirection = direction;
            _sortColumns.Add(column);
        }
        UpdateSortState();
        RefreshView();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSortState()
    {
        foreach (var column in Columns)
        {
            int order = _sortColumns.IndexOf(column);
            column.SortOrder = order;
            if (order < 0) column.SortDirection = DataGridSortDirection.None;
        }
        foreach (var header in _headers) header.UpdateSort();
    }

    #endregion

    #region Selection

    internal bool IsItemSelected(object item) => !IsCellUnit && _selected.Contains(item);

    private void OnSelectedItemChanged(object? item)
    {
        if (_isUpdatingSelection) return;
        if (item == null)
        {
            SetSelection([]);
        }
        else if (!_selected.Contains(item) || _selected.Count > 1)
        {
            SetSelection([item]);
            int index = _view.IndexOf(item);
            if (index >= 0) SetCurrent(index, scroll: true);
        }
    }

    private void OnSelectedItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isUpdatingSelection) return;
        if (IsCellUnit)
        {
            SetSelection([.. SelectedItems]);
            return;
        }
        _selected.Clear();
        foreach (var item in SelectedItems) _selected.Add(item);
        OnSelectionChanged();
    }

    // Replaces the selection (in cell mode: with all cells of the items).
    private void SetSelection(IEnumerable<object> items)
    {
        if (IsCellUnit)
        {
            var cells = new List<DataGridCellInfo>();
            foreach (var item in items)
            {
                foreach (var column in _visibleColumns) cells.Add(new DataGridCellInfo(item, column));
            }
            SetCellSelection(cells);
            return;
        }

        _isUpdatingSelection = true;
        try
        {
            _selected.Clear();
            SelectedItems.Clear();
            foreach (var item in items)
            {
                if (_selected.Add(item)) SelectedItems.Add(item);
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
        OnSelectionChanged();
    }

    internal void SetItemSelected(int index, bool selected)
    {
        if (index < 0 || index >= _view.Count) return;
        var item = _view[index];
        if (selected == _selected.Contains(item)) return;
        _isUpdatingSelection = true;
        try
        {
            if (selected)
            {
                if (SelectionMode == DataGridSelectionMode.Single)
                {
                    _selected.Clear();
                    SelectedItems.Clear();
                }
                _selected.Add(item);
                SelectedItems.Add(item);
            }
            else
            {
                _selected.Remove(item);
                SelectedItems.Remove(item);
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
        SetCurrent(index, scroll: false);
        _anchorIndex = index;
        OnSelectionChanged();
    }

    private void SelectRange(int from, int to, bool additive)
    {
        var items = new List<object>();
        if (additive) items.AddRange(SelectedItems);
        int start = Math.Min(from, to), end = Math.Max(from, to);
        for (int i = Math.Max(0, start); i <= end && i < _view.Count; i++) items.Add(_view[i]);
        SetSelection(items);
    }

    /// <summary>
    /// Selects all shown items, or all their cells with <see cref="DataGridSelectionUnit.Cell"/> (in
    /// <see cref="DataGridSelectionMode.Multiple"/> and <see cref="DataGridSelectionMode.Extended"/>).
    /// </summary>
    public void SelectAll()
    {
        if (SelectionMode == DataGridSelectionMode.Single) return;
        SetSelection(_view);
    }

    /// <summary>Clears the selection.</summary>
    public void UnselectAll() => SetSelection([]);

    #region Cells

    private readonly HashSet<DataGridCellInfo> _selectedCells = [];
    private readonly List<DataGridCellInfo> _selectedCellList = [];
    private int _anchorColumn = -1; // index among the visible columns

    /// <summary>Gets whether the cell of <paramref name="item"/> in <paramref name="column"/> is selected (cell selection).</summary>
    public bool IsCellSelected(object item, DataGridColumn column) => _selectedCells.Contains(new DataGridCellInfo(item, column));

    /// <summary>
    /// Selects the cell of <paramref name="item"/> in <paramref name="column"/> (with <see cref="DataGridSelectionUnit.Cell"/>),
    /// replacing the selection unless <paramref name="add"/> (and the mode allows several), and makes it current.
    /// </summary>
    public void SelectCell(object item, DataGridColumn column, bool add = false)
    {
        if (!IsCellUnit) return;
        int index = _view.IndexOf(item);
        if (index < 0 || !_visibleColumns.Contains(column)) return;
        var cell = new DataGridCellInfo(item, column);
        if (add && SelectionMode != DataGridSelectionMode.Single)
        {
            if (_selectedCells.Add(cell))
            {
                _selectedCellList.Add(cell);
                SyncItemsFromCells();
            }
        }
        else
        {
            SetCellSelection([cell]);
        }
        SetCurrentCell(index, column, scroll: true, anchor: true);
    }

    /// <summary>Replaces the cell selection (with <see cref="DataGridSelectionUnit.Cell"/>); cells not shown are skipped.</summary>
    public void SelectCells(IEnumerable<DataGridCellInfo> cells)
    {
        if (!IsCellUnit) return;
        var shown = new HashSet<object>(_view, ReferenceEqualityComparer.Instance);
        var list = new List<DataGridCellInfo>();
        foreach (var cell in cells)
        {
            if (shown.Contains(cell.Item) && _visibleColumns.Contains(cell.Column)) list.Add(cell);
        }
        SetCellSelection(list);
    }

    private void SetCellSelection(IEnumerable<DataGridCellInfo> cells)
    {
        _selectedCells.Clear();
        _selectedCellList.Clear();
        foreach (var cell in cells)
        {
            if (_selectedCells.Add(cell)) _selectedCellList.Add(cell);
        }
        if (SelectionMode == DataGridSelectionMode.Single && _selectedCellList.Count > 1)
        {
            var first = _selectedCellList[0];
            _selectedCells.Clear();
            _selectedCellList.Clear();
            _selectedCells.Add(first);
            _selectedCellList.Add(first);
        }
        SyncItemsFromCells();
    }

    private void ToggleCell(DataGridCellInfo cell)
    {
        if (_selectedCells.Remove(cell)) _selectedCellList.Remove(cell);
        else if (_selectedCells.Add(cell)) _selectedCellList.Add(cell);
        SyncItemsFromCells();
    }

    // SelectedItems holds the items that have a selected cell.
    private void SyncItemsFromCells()
    {
        _isUpdatingSelection = true;
        try
        {
            _selected.Clear();
            SelectedItems.Clear();
            foreach (var cell in _selectedCellList)
            {
                if (_selected.Add(cell.Item)) SelectedItems.Add(cell.Item);
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
        OnSelectionChanged();
    }

    // The cells of the rectangle between two cells (view rows, visible columns).
    private List<DataGridCellInfo> CellRange(int row1, int column1, int row2, int column2)
    {
        var cells = new List<DataGridCellInfo>();
        int top = Math.Max(0, Math.Min(row1, row2)), bottom = Math.Min(_view.Count - 1, Math.Max(row1, row2));
        int left = Math.Max(0, Math.Min(column1, column2)), right = Math.Min(_visibleColumns.Count - 1, Math.Max(column1, column2));
        for (int r = top; r <= bottom; r++)
        {
            for (int c = left; c <= right; c++) cells.Add(new DataGridCellInfo(_view[r], _visibleColumns[c]));
        }
        return cells;
    }

    private void SelectCellRange(int toRow, int toColumn, bool additive)
    {
        int anchorRow = _anchorIndex >= 0 ? _anchorIndex : Math.Max(0, CurrentIndex);
        int anchorColumn = _anchorColumn >= 0 ? _anchorColumn : Math.Max(0, CurrentColumn != null ? _visibleColumns.IndexOf(CurrentColumn) : 0);
        var cells = additive ? new List<DataGridCellInfo>(_selectedCellList) : [];
        cells.AddRange(CellRange(anchorRow, anchorColumn, toRow, toColumn));
        SetCellSelection(cells);
    }

    private void SetCurrentCell(int index, DataGridColumn column, bool scroll, bool anchor)
    {
        CurrentColumn = column;
        if (anchor)
        {
            _anchorIndex = index;
            _anchorColumn = _visibleColumns.IndexOf(column);
        }
        SetCurrent(index, scroll);
        if (scroll) ScrollColumnIntoView(column);
    }

    // A click on a cell with cell selection.
    private void OnCellClicked(int index, DataGridColumn column, bool ctrl, bool shift, int clickCount)
    {
        var cell = new DataGridCellInfo(_view[index], column);
        int columnIndex = _visibleColumns.IndexOf(column);
        switch (SelectionMode)
        {
            case DataGridSelectionMode.Single:
                SetCellSelection([cell]);
                SetCurrentCell(index, column, scroll: false, anchor: true);
                break;
            case DataGridSelectionMode.Multiple:
                if (shift && _anchorIndex >= 0) SelectCellRange(index, columnIndex, additive: true);
                else if (clickCount < 2) ToggleCell(cell);
                SetCurrentCell(index, column, scroll: false, anchor: !shift);
                break;
            default:
                if (shift && _anchorIndex >= 0) SelectCellRange(index, columnIndex, additive: ctrl);
                else if (ctrl) ToggleCell(cell);
                else SetCellSelection([cell]);
                SetCurrentCell(index, column, scroll: false, anchor: !shift);
                break;
        }
    }

    // Keyboard movement with cell selection: the current cell moves; the selection follows as the mode says.
    private void MoveCurrentCell(int row, int columnIndex, bool ctrl, bool shift)
    {
        row = Math.Clamp(row, 0, _view.Count - 1);
        columnIndex = Math.Clamp(columnIndex, 0, _visibleColumns.Count - 1);
        var column = _visibleColumns[columnIndex];
        switch (SelectionMode)
        {
            case DataGridSelectionMode.Single:
                SetCellSelection([new DataGridCellInfo(_view[row], column)]);
                SetCurrentCell(row, column, scroll: true, anchor: true);
                break;
            default:
                if (shift && SelectionMode == DataGridSelectionMode.Extended)
                {
                    SelectCellRange(row, columnIndex, additive: ctrl);
                    SetCurrentCell(row, column, scroll: true, anchor: false);
                }
                else if (ctrl || SelectionMode == DataGridSelectionMode.Multiple)
                {
                    SetCurrentCell(row, column, scroll: true, anchor: false); // Space selects
                }
                else
                {
                    SetCellSelection([new DataGridCellInfo(_view[row], column)]);
                    SetCurrentCell(row, column, scroll: true, anchor: true);
                }
                break;
        }
    }

    // Drops selected cells whose item or column is no longer shown; returns whether any were dropped.
    private bool PruneSelectedCells()
    {
        if (_selectedCellList.Count == 0) return false;
        var shown = new HashSet<object>(_view, ReferenceEqualityComparer.Instance);
        int removed = _selectedCellList.RemoveAll(c => !shown.Contains(c.Item) || !_visibleColumns.Contains(c.Column));
        if (removed == 0) return false;
        _selectedCells.Clear();
        foreach (var cell in _selectedCellList) _selectedCells.Add(cell);
        return true;
    }

    private void OnSelectionUnitChanged()
    {
        _selectedCells.Clear();
        _selectedCellList.Clear();
        _anchorColumn = -1;
        OnColumnsStructureChanged(); // rows gain or lose the check box column
        SetSelection([]);
    }

    #endregion

    #region Clipboard

    /// <summary>
    /// Copies the selection to the clipboard as tab-separated text (see <see cref="GetClipboardText"/>); returns
    /// <c>false</c> if there is nothing to copy or <see cref="ClipboardCopyMode"/> is <see cref="DataGridClipboardCopyMode.None"/>.
    /// </summary>
    public bool CopyToClipboard()
    {
        if (ClipboardCopyMode == DataGridClipboardCopyMode.None) return false;
        string? text = GetClipboardText();
        if (string.IsNullOrEmpty(text)) return false;
        Clipboard.SetText(text);
        return true;
    }

    /// <summary>
    /// Gets the selection as tab-separated text, one line per row, in view order and column order, as spreadsheets paste
    /// it: selected rows with all visible columns, or the rows and columns of the selected cells (cells between them that
    /// aren't selected stay empty). Without a selection, the current cell or row. With
    /// <see cref="DataGridClipboardCopyMode.IncludeHeader"/> a line of headers comes first. Returns <c>null</c> if there
    /// is nothing to copy.
    /// </summary>
    public string? GetClipboardText()
    {
        var rows = new List<object>();
        var columns = new List<DataGridColumn>();
        Func<object, DataGridColumn, bool> included;

        if (IsCellUnit && _selectedCellList.Count > 0)
        {
            var columnSet = new HashSet<DataGridColumn>();
            foreach (var cell in _selectedCellList) columnSet.Add(cell.Column);
            foreach (var item in _view)
            {
                if (_selected.Contains(item)) rows.Add(item);
            }
            foreach (var column in _visibleColumns)
            {
                if (columnSet.Contains(column)) columns.Add(column);
            }
            included = IsCellSelected;
        }
        else if (!IsCellUnit && _selected.Count > 0)
        {
            foreach (var item in _view)
            {
                if (_selected.Contains(item)) rows.Add(item);
            }
            columns.AddRange(_visibleColumns);
            included = static (_, _) => true;
        }
        else if (CurrentItem is { } current)
        {
            rows.Add(current);
            if (IsCellUnit && CurrentColumn != null) columns.Add(CurrentColumn);
            else columns.AddRange(_visibleColumns);
            included = static (_, _) => true;
        }
        else
        {
            return null;
        }

        var text = new System.Text.StringBuilder();
        if (ClipboardCopyMode == DataGridClipboardCopyMode.IncludeHeader)
        {
            for (int c = 0; c < columns.Count; c++)
            {
                if (c > 0) text.Append('\t');
                text.Append(Clean(columns[c].Header?.ToString() ?? columns[c].EffectiveKey));
            }
            text.Append("\r\n");
        }
        foreach (var item in rows)
        {
            for (int c = 0; c < columns.Count; c++)
            {
                if (c > 0) text.Append('\t');
                if (included(item, columns[c])) text.Append(Clean(new DataGridCellInfo(item, columns[c]).Text));
            }
            text.Append("\r\n");
        }
        return text.ToString();

        // Tabs and line breaks would split the cell.
        static string Clean(string value) => value.IndexOfAny(['\t', '\r', '\n']) < 0 ? value : value.Replace("\r\n", " ").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
    }

    #endregion

    private void OnSelectionChanged()
    {
        _isUpdatingSelection = true;
        try
        {
            SelectedItem = SelectedItems.Count > 0 ? SelectedItems[0] : null;
        }
        finally
        {
            _isUpdatingSelection = false;
        }
        UpdateRealizedRows();
        UpdateSelectAllBox();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSelectAllBox()
    {
        _isUpdatingSelectAll = true;
        try
        {
            _selectAllBox.IsThreeState = true;
            _selectAllBox.IsChecked = _selected.Count == 0 ? false : _selected.Count >= _view.Count && _view.Count > 0 ? true : null;
        }
        finally
        {
            _isUpdatingSelectAll = false;
        }
    }

    private void OnSelectAllClicked()
    {
        if (_isUpdatingSelectAll) return;
        if (_selected.Count >= _view.Count && _view.Count > 0) UnselectAll();
        else SelectAll();
    }

    private void SetCurrent(int index, bool scroll)
    {
        CurrentIndex = Math.Clamp(index, -1, _view.Count - 1);
        UpdateRealizedRows();
        if (scroll && CurrentIndex >= 0) _rows.ScrollIntoView(CurrentIndex);
    }

    /// <summary>Scrolls <paramref name="item"/> into view.</summary>
    public void ScrollIntoView(object item)
    {
        int index = _view.IndexOf(item);
        if (index >= 0) _rows.ScrollIntoView(index, animate: true);
    }

    #endregion

    #region Pointer and keyboard

    internal void OnRowPressed(DataGridRow row, PointerEventArgs e)
    {
        // A press elsewhere ends the edit first; one in the editor is the editor's.
        if (EditContext != null)
        {
            if (row.IsInEditor(e.Source as UIElement)) return;
            if (!CommitEdit()) CancelEdit();
        }

        Focus();
        int index = row.Index;
        var clickedColumn = row.GetColumnAt(e.ScreenPosition.X - row.PointToScreen(Point.Zero).X);
        if (clickedColumn != null) CurrentColumn = clickedColumn;
        bool ctrl = (e.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (e.Modifiers & ModifierKeys.Shift) != 0;

        if (IsCellUnit)
        {
            clickedColumn ??= CurrentColumn ?? (_visibleColumns.Count > 0 ? _visibleColumns[^1] : null);
            if (clickedColumn == null) return;
            if (e.Button == PointerButtons.Right)
            {
                // Right-click selects the cell unless it is already part of the selection.
                if (!IsCellSelected(row.Item!, clickedColumn)) OnCellClicked(index, clickedColumn, ctrl: false, shift: false, clickCount: 1);
                else SetCurrentCell(index, clickedColumn, scroll: false, anchor: false);
                return;
            }
            if (e.Button != PointerButtons.Left) return;
            e.Handled = true;
            OnCellClicked(index, clickedColumn, ctrl, shift, e.ClickCount);
            if (e.ClickCount == 2 && row.Item != null) RowActivated?.Invoke(this, row.Item);
            return;
        }

        if (e.Button == PointerButtons.Right)
        {
            // Right-click selects the row unless it is already part of the selection.
            if (!_selected.Contains(row.Item!) && SelectionMode != DataGridSelectionMode.Multiple) SelectOnly(index);
            else SetCurrent(index, scroll: false);
            return;
        }
        if (e.Button != PointerButtons.Left) return;
        e.Handled = true;

        switch (SelectionMode)
        {
            case DataGridSelectionMode.Single:
                SelectOnly(index);
                break;
            case DataGridSelectionMode.Multiple:
                if (e.ClickCount < 2) SetItemSelected(index, !_selected.Contains(row.Item!));
                break;
            default:
                if (shift && _anchorIndex >= 0)
                {
                    SelectRange(_anchorIndex, index, additive: ctrl);
                    SetCurrent(index, scroll: false);
                }
                else if (ctrl)
                {
                    SetItemSelected(index, !_selected.Contains(row.Item!));
                }
                else
                {
                    SelectOnly(index);
                }
                break;
        }

        if (e.ClickCount == 2 && row.Item != null)
        {
            RowActivated?.Invoke(this, row.Item);
        }
    }

    private void SelectOnly(int index)
    {
        if (index < 0 || index >= _view.Count) return;
        _anchorIndex = index;
        SetSelection([_view[index]]);
        SetCurrent(index, scroll: false);
    }

    internal bool OpenRowContextMenu(DataGridRow row, bool byKeyboard) =>
        RowContextMenu is { } menu && row.Item != null && menu.Open(row, byKeyboard);

    internal bool OpenColumnChooser(DataGridColumnHeader header)
    {
        if (!ShowColumnChooser) return false;
        return CreateColumnChooserMenu().Open(header, byKeyboard: false);
    }

    /// <summary>
    /// Creates the column chooser: a menu with a check item per column (to show or hide it), and commands to fit all
    /// columns and to clear the sort. Right-clicking the headers opens it; use it for a "Columns" button, too.
    /// </summary>
    public ContextMenu CreateColumnChooserMenu()
    {
        var menu = new ContextMenu();
        foreach (var column in Columns)
        {
            var target = column;
            var item = new MenuItem(column.Header?.ToString() ?? column.EffectiveKey)
            {
                IsCheckable = true,
                IsChecked = column.IsVisible,
                StaysOpenOnClick = true,
                IsEnabled = column.CanHide,
            };
            item.CheckedChanged += (_, visible) =>
            {
                // Keep at least one column.
                if (!visible && _visibleColumns.Count <= 1)
                {
                    item.IsChecked = true;
                    return;
                }
                target.IsVisible = visible;
            };
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var fit = new MenuItem("Fit all columns") { Icon = MaterialIconKind.ViewColumn };
        fit.Click += (_, _) =>
        {
            foreach (var column in _visibleColumns) AutoFitColumn(column);
        };
        menu.Items.Add(fit);
        var clearSort = new MenuItem("Clear sort") { IsEnabled = _sortColumns.Count > 0 };
        clearSort.Click += (_, _) => SortBy(null, DataGridSortDirection.None);
        menu.Items.Add(clearSort);
        return menu;
    }

    /// <inheritdoc/>
    /// <remarks>See the class remarks for the keys.</remarks>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;

        // While editing, the editor has the keys; Enter and Escape it leaves unhandled end the edit.
        if (EditContext != null)
        {
            if (e.Key == Key.Enter)
            {
                CommitEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelEdit();
                e.Handled = true;
            }
            return;
        }

        if (_view.Count == 0) return;

        bool ctrl = (e.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (e.Modifiers & ModifierKeys.Shift) != 0;
        int current = CurrentIndex;

        switch (e.Key)
        {
            case Key.F2 when current >= 0:
                BeginEdit();
                e.Handled = true;
                return;
            case Key.C when ctrl && ClipboardCopyMode != DataGridClipboardCopyMode.None:
                e.Handled = CopyToClipboard();
                return;
            case Key.Left or Key.Right when !IsCellUnit && e.Modifiers == ModifierKeys.None && _visibleColumns.Count > 0:
                MoveCurrentColumn(e.Key == Key.Right ? 1 : -1);
                e.Handled = true;
                return;
        }

        if (IsCellUnit && _visibleColumns.Count > 0 && HandleCellKey(e, ctrl, shift))
        {
            e.Handled = true;
            return;
        }

        int page = _rows.ItemsPerPage;
        int target = e.Key switch
        {
            Key.Down => current + 1,
            Key.Up => current - 1,
            Key.PageDown => current + page,
            Key.PageUp => current - page,
            Key.Home => 0,
            Key.End => _view.Count - 1,
            _ => int.MinValue,
        };

        if (target != int.MinValue)
        {
            target = Math.Clamp(target, 0, _view.Count - 1);
            if (current < 0) target = e.Key is Key.Up or Key.End or Key.PageUp ? _view.Count - 1 : 0;
            MoveCurrent(target, ctrl, shift);
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.Space when current >= 0:
                if (SelectionMode == DataGridSelectionMode.Single) SelectOnly(current);
                else SetItemSelected(current, !_selected.Contains(_view[current]));
                break;
            case Key.Enter when current >= 0:
                RowActivated?.Invoke(this, _view[current]);
                break;
            case Key.A when ctrl:
                SelectAll();
                break;
            case Key.Menu:
            case Key.F10 when shift:
                if (current < 0) return;
                _rows.ScrollIntoView(current);
                UpdateLayoutForRow(current);
                if (GetRow(current) is not { } row || !OpenRowContextMenu(row, byKeyboard: true)) return;
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    // Keys of cell selection: arrows, Home/End (in the row; with Ctrl the first/last cell), Page Up/Down, Space and Ctrl+A.
    private bool HandleCellKey(KeyEventArgs e, bool ctrl, bool shift)
    {
        int row = Math.Max(0, CurrentIndex);
        int column = CurrentColumn != null ? Math.Max(0, _visibleColumns.IndexOf(CurrentColumn)) : 0;
        bool hadCurrent = CurrentIndex >= 0 && CurrentColumn != null;
        int page = _rows.ItemsPerPage;
        int lastRow = _view.Count - 1, lastColumn = _visibleColumns.Count - 1;

        (int Row, int Column)? target = e.Key switch
        {
            Key.Up => (row - 1, column),
            Key.Down => (row + 1, column),
            Key.Left => (row, column - 1),
            Key.Right => (row, column + 1),
            Key.PageUp => (row - page, column),
            Key.PageDown => (row + page, column),
            Key.Home => ctrl ? (0, 0) : (row, 0),
            Key.End => ctrl ? (lastRow, lastColumn) : (row, lastColumn),
            _ => null,
        };
        if (target is { } t)
        {
            // Home/End with Ctrl move; plain Ctrl (Extended, Multiple) moves the current cell without selecting.
            bool moveOnly = ctrl && e.Key is not (Key.Home or Key.End);
            if (!hadCurrent) t = (row, column); // the first key only shows the current cell
            MoveCurrentCell(t.Row, t.Column, moveOnly || (ctrl && shift), shift);
            return true;
        }

        switch (e.Key)
        {
            case Key.Space when CurrentIndex >= 0:
                var cell = new DataGridCellInfo(_view[row], _visibleColumns[column]);
                if (SelectionMode == DataGridSelectionMode.Single) SetCellSelection([cell]);
                else ToggleCell(cell);
                SetCurrentCell(row, _visibleColumns[column], scroll: false, anchor: true);
                return true;
            case Key.A when ctrl:
                SelectAll();
                return true;
            default:
                return false;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// With cell selection, typing on a current cell that can be edited starts editing it with the typed text (text box
    /// editors), like a spreadsheet.
    /// </remarks>
    public override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || !IsCellUnit || EditContext != null || IsReadOnly || string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0])) return;
        if (CurrentIndex < 0 || CurrentColumn is not { CanEdit: true } column) return;
        BeginEdit(_view[CurrentIndex], column, e.Text); // the editor gets the typed text as DataGridEditContext.InitialText
        e.Handled = true;
    }

    // Makes sure the row of the index exists (after scrolling to it) before using it.
    private void UpdateLayoutForRow(int index)
    {
        if (GetRow(index) != null) return;
        Measure(_lastMeasureSize);
        Arrange(Bounds);
    }

    private Size _lastMeasureSize;

    private void MoveCurrent(int target, bool ctrl, bool shift)
    {
        switch (SelectionMode)
        {
            case DataGridSelectionMode.Single:
                SelectOnly(target);
                break;
            case DataGridSelectionMode.Multiple:
                SetCurrent(target, scroll: false);
                break;
            default:
                if (shift)
                {
                    if (_anchorIndex < 0) _anchorIndex = Math.Max(0, CurrentIndex);
                    SelectRange(_anchorIndex, target, additive: ctrl);
                    SetCurrent(target, scroll: false);
                }
                else if (ctrl)
                {
                    SetCurrent(target, scroll: false);
                }
                else
                {
                    SelectOnly(target);
                }
                break;
        }
        _rows.ScrollIntoView(target);
    }

    private void MoveCurrentColumn(int direction)
    {
        int index = CurrentColumn == null ? (direction > 0 ? 0 : _visibleColumns.Count - 1)
            : Math.Clamp(_visibleColumns.IndexOf(CurrentColumn) + direction, 0, _visibleColumns.Count - 1);
        CurrentColumn = _visibleColumns[index];
        if (CurrentIndex < 0 && _view.Count > 0) SetCurrent(0, scroll: true);
        ScrollColumnIntoView(CurrentColumn);
        UpdateRealizedRows();
    }

    // Scrolls horizontally so the column is visible.
    private void ScrollColumnIntoView(DataGridColumn column)
    {
        float left = ShowsSelectionColumn ? SelectionColumnWidth : 0;
        foreach (var c in _visibleColumns)
        {
            if (c == column) break;
            left += c.ActualWidth;
        }
        float right = left + column.ActualWidth;
        float offset = _scrollViewer.ScrollOffsetX;
        float viewport = _scrollViewer.Viewport.Width;
        if (left < offset) _scrollViewer.ScrollTo(left, _scrollViewer.ScrollOffsetY, animate: false);
        else if (right > offset + viewport && viewport > 0) _scrollViewer.ScrollTo(Math.Min(left, right - viewport), _scrollViewer.ScrollOffsetY, animate: false);
    }

    #endregion

    #region Editing

    /// <summary>
    /// Edits the current cell (<see cref="CurrentColumn"/> of the current row), or the first editable cell of the row
    /// when the current column can't be edited. Returns <c>false</c> if nothing can be edited.
    /// </summary>
    public bool BeginEdit()
    {
        if (CurrentIndex < 0 || CurrentIndex >= _view.Count) return false;
        var column = CurrentColumn is { CanEdit: true } current ? current : null;
        if (column == null)
        {
            // From the current column on, then from the start.
            int start = CurrentColumn != null ? Math.Max(0, _visibleColumns.IndexOf(CurrentColumn)) : 0;
            for (int i = 0; i < _visibleColumns.Count && column == null; i++)
            {
                var candidate = _visibleColumns[(start + i) % _visibleColumns.Count];
                if (candidate.CanEdit) column = candidate;
            }
        }
        return column != null && BeginEdit(_view[CurrentIndex], column);
    }

    /// <summary>
    /// Edits the cell of <paramref name="item"/> in <paramref name="column"/>: scrolls it into view and shows the column's
    /// editor in it. An edit in progress is committed first. Returns <c>false</c> if the cell can't be edited.
    /// </summary>
    public bool BeginEdit(object item, DataGridColumn column) => BeginEdit(item, column, null);

    private bool BeginEdit(object item, DataGridColumn column, string? initialText)
    {
        if (IsReadOnly || !column.CanEdit || !_visibleColumns.Contains(column)) return false;
        if (EditContext != null && !CommitEdit()) return false;

        int index = _view.IndexOf(item);
        if (index < 0) return false;

        SetCurrent(index, scroll: false);
        CurrentColumn = column;
        _rows.ScrollIntoView(index);
        ScrollColumnIntoView(column);
        UpdateLayoutForRow(index);
        if (GetRow(index) is not { } row) return false;

        var context = new DataGridEditContext(this, column, item, index) { InitialText = initialText };
        var editor = column.CreateEditor(context);
        if (editor == null) return false;
        context.Editor = editor;
        EditContext = context;
        row.BeginEdit(column, editor);
        UpdateLayoutForRow(index);
        FocusManager.FocusChanged += OnFocusChangedWhileEditing;
        FocusEditor(editor);
        return true;
    }

    private static void FocusEditor(UIElement editor)
    {
        var target = FindFocusable(editor);
        target?.Focus();
    }

    private static UIElement? FindFocusable(UIElement element)
    {
        if (element.IsFocusable && element.IsEnabled && element.Visibility == Visibility.Visible) return element;
        foreach (var child in element.Children)
        {
            if (child is UIElement ui && ui.Visibility == Visibility.Visible && FindFocusable(ui) is { } found) return found;
        }
        return null;
    }

    /// <summary>
    /// Stores the edited value and ends the edit. The column converts the value (see
    /// <see cref="DataGridColumn.TryConvertEditValue"/>) and <see cref="CellEditEnding"/> may change or reject it; a
    /// rejected value keeps the editor open with the message (see <see cref="DataGridEditContext.Error"/>). Returns
    /// <c>true</c> if the edit ended (or none was in progress).
    /// </summary>
    public bool CommitEdit()
    {
        var context = EditContext;
        if (context == null) return true;

        if (!context.Column.TryConvertEditValue(context, out var value, out var error))
        {
            ShowEditError(context, error ?? "Invalid value");
            return false;
        }

        var args = new DataGridCellEditEndingEventArgs(context, value);
        CellEditEnding?.Invoke(this, args);
        if (args.Cancel)
        {
            EndEdit(committed: false);
            return true;
        }
        if (args.Error != null)
        {
            ShowEditError(context, args.Error);
            return false;
        }

        try
        {
            context.Column.ValueSetter?.Invoke(context.Item, args.Value);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or InvalidCastException or OverflowException)
        {
            ShowEditError(context, ex.Message);
            return false;
        }

        EndEdit(committed: true);
        return true;
    }

    /// <summary>Ends the edit without storing the value.</summary>
    public void CancelEdit()
    {
        if (EditContext != null) EndEdit(committed: false);
    }

    private void ShowEditError(DataGridEditContext context, string error)
    {
        context.Error = error;
        GetEditingRow()?.SetEditError(error);
    }

    private DataGridRow? GetEditingRow()
    {
        foreach (var container in _rows.RealizedContainers.Values)
        {
            if (container is DataGridRow { EditingColumn: not null } row) return row;
        }
        return null;
    }

    private void EndEdit(bool committed)
    {
        var context = EditContext!;
        EditContext = null;
        FocusManager.FocusChanged -= OnFocusChangedWhileEditing;

        var row = GetEditingRow();
        bool editorHadFocus = row?.IsInEditor(FocusManager.GetFocusedElement(this)) == true;
        row?.EndEdit();
        if (editorHadFocus) Focus();
        CellEditEnded?.Invoke(this, committed);
    }

    // Recycling the row, or rebuilding columns: keep a valid value, drop an invalid one.
    private void EndEditForReuse()
    {
        if (!CommitEdit()) CancelEdit();
    }

    // Focus left the editor (Tab, a click elsewhere): commit, or cancel an invalid value.
    private void OnFocusChangedWhileEditing(UIElement? oldFocus, UIElement? newFocus)
    {
        if (EditContext == null || newFocus == null) return;
        var row = GetEditingRow();
        if (row == null || row.IsInEditor(newFocus) || newFocus == this) return;
        EndEditForReuse();
    }

    #endregion

    #region Focus

    /// <inheritdoc/>
    public override void OnGotFocus()
    {
        base.OnGotFocus();
        if (CurrentIndex < 0 && _view.Count > 0) CurrentIndex = 0;
        UpdateRealizedRows();
    }

    /// <inheritdoc/>
    public override void OnLostFocus()
    {
        base.OnLostFocus();
        UpdateRealizedRows();
    }

    #endregion

    #region Layout persistence

    /// <summary>Gets the column order, widths, visibility and sort, to restore later with <see cref="RestoreLayout"/>.</summary>
    public DataGridLayout SaveLayout()
    {
        var layout = new DataGridLayout();
        foreach (var column in Columns)
        {
            float width = column.Width.IsAbsolute ? column.Width.Value : column.ActualWidth;
            layout.Columns.Add(new DataGridColumnLayout(column.EffectiveKey, width, column.IsVisible));
        }
        foreach (var column in _sortColumns)
        {
            layout.Sort.Add(new DataGridSortLayout(column.EffectiveKey, column.SortDirection));
        }
        return layout;
    }

    /// <summary>
    /// Applies a layout from <see cref="SaveLayout"/>: columns are matched by <see cref="DataGridColumn.EffectiveKey"/>;
    /// columns the layout doesn't know keep their settings and follow the known ones.
    /// </summary>
    public void RestoreLayout(DataGridLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var byKey = new Dictionary<string, DataGridColumn>();
        foreach (var column in Columns) byKey.TryAdd(column.EffectiveKey, column);

        var ordered = new List<DataGridColumn>();
        foreach (var saved in layout.Columns)
        {
            if (!byKey.Remove(saved.Key, out var column)) continue;
            if (float.IsFinite(saved.Width) && saved.Width > 0) column.Width = GridLength.Pixels(saved.Width);
            column.IsVisible = saved.IsVisible || !column.CanHide;
            ordered.Add(column);
        }
        foreach (var column in Columns)
        {
            if (!ordered.Contains(column)) ordered.Add(column);
        }
        for (int i = 0; i < ordered.Count; i++)
        {
            int from = Columns.IndexOf(ordered[i]);
            if (from != i) Columns.Move(from, i);
        }

        foreach (var column in _sortColumns) column.SortDirection = DataGridSortDirection.None;
        _sortColumns.Clear();
        foreach (var sort in layout.Sort)
        {
            var column = FindColumn(sort.Key);
            if (column != null && sort.Direction != DataGridSortDirection.None && !_sortColumns.Contains(column))
            {
                column.SortDirection = sort.Direction;
                _sortColumns.Add(column);
            }
        }
        UpdateSortState();
        RefreshView();
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Gets the column with the key <paramref name="key"/>, or <c>null</c>.</summary>
    public DataGridColumn? FindColumn(string key)
    {
        foreach (var column in Columns)
        {
            if (column.EffectiveKey == key) return column;
        }
        return null;
    }

    #endregion

    #region Layout

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        _lastMeasureSize = availableSize;
        float width = float.IsFinite(availableSize.Width) ? availableSize.Width : 800;
        UpdateColumnWidths(width);
        InvalidateColumnsIfWidthsChanged();

        float headerHeight = HeaderHeight;
        _headerPanel.RowHeight = headerHeight;
        _headerHost.Measure(new Size(width, headerHeight));
        float filterHeight = ShowFilterRow ? FilterRowHeight : 0;
        _filterHost.Measure(new Size(width, filterHeight));

        float bodyHeight = float.IsFinite(availableSize.Height) ? Math.Max(0, availableSize.Height - headerHeight - filterHeight) : RowHeight * 10;
        _scrollViewer.Measure(new Size(width, bodyHeight));
        _placeholder.Measure(new Size(width, bodyHeight));

        float desiredWidth = float.IsFinite(availableSize.Width) ? availableSize.Width : TotalColumnsWidth;
        return new Size(desiredWidth, headerHeight + filterHeight + Math.Max(_scrollViewer.DesiredSize.Height, float.IsFinite(availableSize.Height) ? bodyHeight : 0));
    }

    // Rows, headers and filter boxes lay out by the column widths, which they don't know changed.
    private void InvalidateColumnsIfWidthsChanged()
    {
        bool changed = _lastWidths.Count != _visibleColumns.Count;
        for (int i = 0; !changed && i < _visibleColumns.Count; i++)
        {
            changed = _lastWidths[i] != _visibleColumns[i].ActualWidth;
        }
        if (!changed) return;

        _lastWidths.Clear();
        foreach (var column in _visibleColumns) _lastWidths.Add(column.ActualWidth);
        foreach (var row in _rows.RealizedContainers.Values) row.InvalidateMeasure();
        foreach (var header in _headers) header.InvalidateMeasure();
        _headerPanel.InvalidateMeasure();
        _filterPanel.InvalidateMeasure();
        _rows.InvalidateMeasure();
    }

    private readonly List<float> _lastWidths = [];

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        float headerHeight = HeaderHeight;
        float filterHeight = ShowFilterRow ? FilterRowHeight : 0;
        _headerHost.Arrange(new Rect(0, 0, finalSize.Width, headerHeight));
        _headerPanel.Arrange(new Rect(0, 0, Math.Max(finalSize.Width, TotalColumnsWidth), headerHeight));
        _filterHost.Arrange(new Rect(0, headerHeight, finalSize.Width, filterHeight));
        _filterPanel.Arrange(new Rect(0, 0, Math.Max(finalSize.Width, TotalColumnsWidth), filterHeight));

        var body = new Rect(0, headerHeight + filterHeight, finalSize.Width, Math.Max(0, finalSize.Height - headerHeight - filterHeight));
        _scrollViewer.Arrange(body);
        var p = _placeholder.DesiredSize;
        _placeholder.Arrange(new Rect(body.X + (body.Width - p.Width) * 0.5f, body.Y + Math.Min(48, (body.Height - p.Height) * 0.5f), p.Width, p.Height));
        SyncHeaderScroll();
        return finalSize;
    }

    #endregion
}
