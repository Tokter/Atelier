using System;
using System.Collections.Generic;
using System.ComponentModel;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// A row of a <see cref="DataGrid"/>: the cells of one item, laid out in the grid's columns, with an optional selection
/// check box. Rows are created only for the items in view and reused while scrolling.
/// </summary>
public class DataGridRow : Control
{
    /// <summary>The horizontal padding inside each cell.</summary>
    public const float CellPadding = 16f;

    private readonly List<UIElement> _cells = [];
    private readonly List<DataGridColumn> _cellColumns = [];
    private CheckBox? _selectBox;
    private int _columnsVersion = -1;
    private bool _isSelected;
    private bool _isCurrent;
    private bool _isPreparing;
    private INotifyPropertyChanged? _observedItem;

    internal DataGridRow(DataGrid owner)
    {
        Owner = owner;
    }

    /// <summary>Gets the grid.</summary>
    public DataGrid Owner { get; }

    /// <summary>Gets the item the row shows, or <c>null</c> while it isn't in use.</summary>
    public object? Item { get; private set; }

    /// <summary>Gets the index of the item in the grid's (filtered and sorted) view.</summary>
    public int Index { get; private set; } = -1;

    /// <summary>Gets whether the item is selected.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        internal set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            if (_selectBox != null)
            {
                _isPreparing = true;
                _selectBox.IsChecked = value;
                _isPreparing = false;
            }
            InvalidateVisual();
        }
    }

    /// <summary>Gets whether this is the grid's current row (the keyboard cursor).</summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        internal set
        {
            if (_isCurrent == value) return;
            _isCurrent = value;
            InvalidateVisual();
        }
    }

    /// <summary>Gets the cell elements, in the order of the visible columns.</summary>
    public IReadOnlyList<UIElement> Cells => _cells;

    internal void Prepare(object item, int index)
    {
        if (_columnsVersion != Owner.ColumnsVersion) RebuildCells();

        Item = item;
        Index = index;
        DataContext = item;
        for (int i = 0; i < _cells.Count; i++)
        {
            _cellColumns[i].PrepareCell(_cells[i], item);
        }
        IsSelected = Owner.IsItemSelected(item);
        IsCurrent = index == Owner.CurrentIndex;

        if (item is INotifyPropertyChanged inpc && !ReferenceEquals(inpc, _observedItem))
        {
            Unobserve();
            _observedItem = inpc;
            inpc.PropertyChanged += OnItemPropertyChanged;
        }
        InvalidateMeasure();
    }

    internal void Clear()
    {
        Unobserve();
        for (int i = 0; i < _cells.Count; i++)
        {
            _cellColumns[i].ClearCell(_cells[i]);
        }
        Item = null;
        Index = -1;
        DataContext = null;
    }

    private void Unobserve()
    {
        if (_observedItem != null)
        {
            _observedItem.PropertyChanged -= OnItemPropertyChanged;
            _observedItem = null;
        }
    }

    // A property of the item changed: show the new values (sorting and filtering refresh with DataGrid.Refresh).
    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Item == null || !ReferenceEquals(sender, Item)) return;
        for (int i = 0; i < _cells.Count; i++)
        {
            _cellColumns[i].PrepareCell(_cells[i], Item);
        }
        InvalidateMeasure();
    }

    private void RebuildCells()
    {
        foreach (var cell in _cells) RemoveChild(cell);
        _cells.Clear();
        _cellColumns.Clear();

        if (Owner.ShowsSelectionColumn)
        {
            if (_selectBox == null)
            {
                _selectBox = new CheckBox { IsFocusable = false, VerticalAlignment = VerticalAlignment.Center };
                _selectBox.CheckedChanged += (_, isChecked) =>
                {
                    if (!_isPreparing && Index >= 0) Owner.SetItemSelected(Index, isChecked == true);
                };
                AddChild(_selectBox);
            }
        }
        else if (_selectBox != null)
        {
            RemoveChild(_selectBox);
            _selectBox = null;
        }

        foreach (var column in Owner.VisibleColumns)
        {
            var cell = column.CreateCell();
            cell.ClipToBounds = true; // content wider than the column (e.g. a template without trimming) must not spill over
            _cells.Add(cell);
            _cellColumns.Add(column);
            AddChild(cell);
        }
        _columnsVersion = Owner.ColumnsVersion;
    }

    #region Editing

    private UIElement? _editor;
    private int _editIndex = -1;

    /// <summary>Gets the column whose cell is being edited in this row, or <c>null</c>.</summary>
    public DataGridColumn? EditingColumn => _editIndex >= 0 ? _cellColumns[_editIndex] : null;

    /// <summary>Gets the editor shown in the edited cell, or <c>null</c>.</summary>
    public UIElement? Editor => _editor;

    /// <summary>Gets the message of a rejected value while editing, or <c>null</c>.</summary>
    public string? EditError { get; private set; }

    internal void BeginEdit(DataGridColumn column, UIElement editor)
    {
        EndEdit();
        _editIndex = _cellColumns.IndexOf(column);
        if (_editIndex < 0) return;
        _editor = editor;
        _cells[_editIndex].Visibility = Visibility.Hidden;
        AddChild(editor);
        ZIndex = 1; // an editor taller than the row (e.g. with a message) draws over the next rows
        InvalidateMeasure();
        InvalidateVisual();
    }

    internal void EndEdit()
    {
        if (_editIndex < 0) return;
        _cells[_editIndex].Visibility = Visibility.Visible;
        if (_editor != null) RemoveChild(_editor);
        _editor = null;
        _editIndex = -1;
        ZIndex = 0;
        SetEditError(null);
        if (Item != null)
        {
            for (int i = 0; i < _cells.Count; i++) _cellColumns[i].PrepareCell(_cells[i], Item);
        }
        InvalidateMeasure();
        InvalidateVisual();
    }

    internal void SetEditError(string? error)
    {
        EditError = error;
        if (_editor != null)
        {
            ToolTipService.SetToolTip(_editor, error);
            if (error != null) ToolTipService.Show(_editor);
            else ToolTipService.Close();
        }
        InvalidateVisual();
    }

    internal bool IsInEditor(UIElement? element)
    {
        if (_editor == null || element == null) return false;
        for (VisualNode? node = element; node != null; node = node.Parent)
        {
            if (node == _editor) return true;
            if (node == this) return false;
        }
        return false; // (an editor's popups are its children, so their content counts too)
    }

    /// <summary>Gets the column at <paramref name="x"/> (in the row's coordinates), or <c>null</c>.</summary>
    public DataGridColumn? GetColumnAt(float x)
    {
        float left = _selectBox != null ? DataGrid.SelectionColumnWidth : 0;
        foreach (var column in Owner.VisibleColumns)
        {
            if (x >= left && x < left + column.ActualWidth) return column;
            left += column.ActualWidth;
        }
        return null;
    }

    /// <summary>Gets the rectangle of the cell of <paramref name="column"/> in the row's coordinates, or <c>null</c> if it isn't shown.</summary>
    public Rect? GetCellBounds(DataGridColumn? column)
    {
        if (column == null) return null;
        float left = _selectBox != null ? DataGrid.SelectionColumnWidth : 0;
        foreach (var c in Owner.VisibleColumns)
        {
            if (c == column) return new Rect(left, 0, c.ActualWidth, Bounds.Height);
            left += c.ActualWidth;
        }
        return null;
    }

    /// <summary>Gets the current cell's rectangle while this is the current row and the grid has a current column, else <c>null</c>.</summary>
    public Rect? CurrentCellBounds => IsCurrent && EditingColumn == null ? GetCellBounds(Owner.CurrentColumn) : null;

    #endregion

    #region Input

    /// <inheritdoc/>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || Index < 0) return;
        Owner.OnRowPressed(this, e);
    }

    /// <inheritdoc/>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Handled || Index < 0 || e.Button != PointerButtons.Right || IsInEditor(e.Source as UIElement)) return;
        e.Handled = Owner.OpenRowContextMenu(this, byKeyboard: false);
    }

    #endregion

    #region Layout

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        float height = Owner.RowHeight;
        _selectBox?.Measure(new Size(DataGrid.SelectionColumnWidth, height));
        for (int i = 0; i < _cells.Count; i++)
        {
            float width = Math.Max(0, _cellColumns[i].ActualWidth - CellPadding * 2);
            _cells[i].Measure(new Size(width, height));
        }
        if (_editor != null && _editIndex >= 0)
        {
            _editor.Measure(new Size(Math.Max(0, _cellColumns[_editIndex].ActualWidth - EditorInset * 2), float.PositiveInfinity));
        }
        return new Size(Owner.TotalColumnsWidth, height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        float x = 0;
        float height = finalSize.Height;
        if (_selectBox != null)
        {
            var size = _selectBox.DesiredSize;
            _selectBox.Arrange(new Rect((DataGrid.SelectionColumnWidth - size.Width) * 0.5f, (height - size.Height) * 0.5f, size.Width, size.Height));
            x += DataGrid.SelectionColumnWidth;
        }

        for (int i = 0; i < _cells.Count; i++)
        {
            var column = _cellColumns[i];
            float slot = Math.Max(0, column.ActualWidth - CellPadding * 2);
            var cell = _cells[i];
            float width = Math.Min(slot, cell.DesiredSize.Width);
            if (column.CellAlignment == HorizontalAlignment.Stretch) width = slot;
            float offset = column.CellAlignment switch
            {
                HorizontalAlignment.Right => slot - width,
                HorizontalAlignment.Center => (slot - width) * 0.5f,
                _ => 0,
            };
            float cellHeight = Math.Min(height, cell.DesiredSize.Height);
            cell.Arrange(new Rect(x + CellPadding + offset, (height - cellHeight) * 0.5f, width, cellHeight));
            if (i == _editIndex && _editor != null)
            {
                // The editor fills the cell (inset a little), centered on the row; a taller one extends below it.
                float editorHeight = _editor.DesiredSize.Height;
                float top = editorHeight <= height ? (height - editorHeight) * 0.5f : 2;
                _editor.Arrange(new Rect(x + EditorInset, top, Math.Max(0, column.ActualWidth - EditorInset * 2), editorHeight));
            }
            x += column.ActualWidth;
        }
        return finalSize;
    }

    /// <summary>The space between an editor and its cell's edges.</summary>
    public const float EditorInset = 4f;

    #endregion
}

/// <summary>
/// The header of a <see cref="DataGrid"/> column: the header content and the sort arrow. A click sorts (Shift+click adds
/// a sort column), dragging moves the column, dragging the right edge resizes it (a double-click there fits it to its
/// content), and a right-click opens the column chooser.
/// </summary>
public class DataGridColumnHeader : Control
{
    /// <summary>The width of the resize grip at the right edge.</summary>
    public const float ResizeGripWidth = 6f;

    private readonly ContentControl _content = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Icon _sortIcon = new(MaterialIconKind.ArrowUpward, 16) { Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };
    private bool _overGrip;
    private bool _isResizing;
    private bool _isPressed;
    private bool _isDragging;
    private float _pressX;
    private float _startWidth;

    private readonly Button _filterButton;

    /// <summary>Identifies the filter button's style (see <see cref="UIElement.StyleKey"/>).</summary>
    public const string FilterButtonStyleKey = "DataGridFilterButton";

    /// <summary>Identifies the filter button's style while the column is filtered.</summary>
    public const string ActiveFilterButtonStyleKey = "DataGridActiveFilterButton";

    internal DataGridColumnHeader(DataGrid owner, DataGridColumn column)
    {
        Owner = owner;
        Column = column;
        _filterButton = new Button
        {
            Variant = ButtonVariant.Text,
            IsFocusable = false,
            Width = FilterButtonSize,
            Height = FilterButtonSize,
            MinWidth = 0,
            MinHeight = 0,
            Padding = Thickness.Zero,
            CornerRadius = new CornerRadius(FilterButtonSize / 2),
            VerticalAlignment = VerticalAlignment.Center,
            Content = new Icon(MaterialIconKind.FilterList, 18),
        };
        ToolTipService.SetToolTip(_filterButton, "Filter");
        _filterButton.Click += (_, _) => Owner.OpenFilterMenu(Column);
        AddChild(_content);
        AddChild(_sortIcon);
        AddChild(_filterButton);
        UpdateContent();
    }

    /// <summary>The size of the filter button.</summary>
    public const float FilterButtonSize = 28f;

    /// <summary>Gets the button that opens the column's filter menu.</summary>
    public Button FilterButton => _filterButton;

    private bool HasFilterButton => Owner.ShowFilterMenus && Column.IsFilterable;

    // Shown while hovered, filtered, or its menu is open; otherwise hidden but keeping its space, so the text doesn't move.
    internal void UpdateFilterButton()
    {
        if (!HasFilterButton)
        {
            _filterButton.Visibility = Visibility.Collapsed;
            return;
        }
        bool menuOpen = Owner.OpenedFilterMenu?.Column == Column;
        _filterButton.Visibility = IsHovered || Column.IsFiltered || menuOpen ? Visibility.Visible : Visibility.Hidden;
        _filterButton.StyleKey = Column.IsFiltered ? ActiveFilterButtonStyleKey : FilterButtonStyleKey;
        ((Icon)_filterButton.Content!).Kind = Column.IsFiltered ? MaterialIconKind.FilterAlt : MaterialIconKind.FilterList;
        ToolTipService.SetToolTip(_filterButton, Column.IsFiltered ? "Filtered: change or clear the filter" : "Filter");
    }

    /// <inheritdoc/>
    public override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        UpdateFilterButton();
    }

    /// <summary>Gets the grid.</summary>
    public DataGrid Owner { get; }

    /// <summary>Gets the column.</summary>
    public DataGridColumn Column { get; }

    /// <summary>Gets whether the pointer is over the resize grip.</summary>
    public bool IsOverResizeGrip => _overGrip;

    /// <summary>Gets whether the column is being resized.</summary>
    public bool IsResizing => _isResizing;

    /// <summary>Gets whether the header is being dragged to a new place.</summary>
    public bool IsDragging => _isDragging;

    private bool CanResize => Column.CanResize && Owner.CanResizeColumns;

    internal void UpdateContent()
    {
        _content.Content = Column.HeaderTemplate != null ? Column.HeaderTemplate(Column)
            : Column.Header is UIElement element ? element
            : new TextBlock { Text = Column.Header?.ToString() ?? string.Empty, TextTrimming = TextTrimming.CharacterEllipsis, ShowsToolTipWhenTrimmed = true };
        UpdateSort();
        UpdateFilterButton();
    }

    internal void UpdateSort()
    {
        _sortIcon.Visibility = Column.SortDirection == DataGridSortDirection.None ? Visibility.Collapsed : Visibility.Visible;
        _sortIcon.Kind = Column.SortDirection == DataGridSortDirection.Descending ? MaterialIconKind.ArrowDownward : MaterialIconKind.ArrowUpward;
        ToolTipService.SetToolTip(_sortIcon, Column.SortOrder > 0 ? $"Sort level {Column.SortOrder + 1}" : null);
        InvalidateMeasure();
    }

    /// <inheritdoc/>
    protected override CursorType GetCursor() => _overGrip || _isResizing ? CursorType.SizeWestEast : base.GetCursor();

    private bool IsInGrip(float x) => CanResize && x >= Bounds.Width - ResizeGripWidth;

    private float LocalX(PointerEventArgs e) => e.ScreenPosition.X - PointToScreen(Point.Zero).X;

    /// <inheritdoc/>
    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        float x = LocalX(e);
        if (_isResizing)
        {
            Owner.ResizeColumn(Column, _startWidth + (e.ScreenPosition.X - _pressX));
            return;
        }
        if (_isPressed)
        {
            if (!_isDragging && Math.Abs(e.ScreenPosition.X - _pressX) > 6 && Column.CanReorder && Owner.CanReorderColumns)
            {
                _isDragging = true;
                ZIndex = 1;
            }
            if (_isDragging) Owner.OnHeaderDragged(this, e.ScreenPosition.X - _pressX);
            return;
        }

        bool grip = IsInGrip(x);
        if (grip != _overGrip)
        {
            _overGrip = grip;
            InvalidateVisual();
        }
    }

    /// <inheritdoc/>
    public override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        UpdateFilterButton();
        if (_overGrip && !_isResizing)
        {
            _overGrip = false;
            InvalidateVisual();
        }
    }

    /// <inheritdoc/>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || e.Button != PointerButtons.Left) return;
        e.Handled = true;

        if (IsInGrip(LocalX(e)))
        {
            if (e.ClickCount == 2)
            {
                Owner.AutoFitColumn(Column);
                return;
            }
            _isResizing = true;
            _startWidth = Column.ActualWidth;
        }
        else
        {
            _isPressed = true;
        }
        _pressX = e.ScreenPosition.X;
        CapturePointer();
        InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Button == PointerButtons.Right && !_isResizing && !_isPressed)
        {
            e.Handled = Owner.OpenColumnChooser(this);
            return;
        }

        bool wasResizing = _isResizing, wasDragging = _isDragging, wasPressed = _isPressed;
        EndInteraction();
        if (IsPointerCaptured) ReleasePointerCapture();

        if (wasResizing)
        {
            Owner.OnColumnResized();
        }
        else if (wasDragging)
        {
            Owner.OnHeaderDropped(this);
        }
        else if (wasPressed && LocalX(e) is var x && x >= 0 && x <= Bounds.Width)
        {
            Owner.OnHeaderClicked(Column, (e.Modifiers & ModifierKeys.Shift) != 0);
        }
        e.Handled = wasPressed || wasResizing;
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture()
    {
        base.OnLostPointerCapture();
        if (_isDragging) Owner.OnHeaderDragCanceled(this);
        EndInteraction();
    }

    private void EndInteraction()
    {
        _isResizing = _isPressed = _isDragging = false;
        ZIndex = 0;
        RenderTransform = System.Numerics.Matrix3x2.Identity;
        InvalidateVisual();
    }

    /// <inheritdoc/>
    // The space the filter button takes at the right (kept while it is hidden).
    private float FilterButtonSpace => _filterButton.Visibility == Visibility.Collapsed ? 0 : Math.Max(0, FilterButtonSize + ResizeGripWidth + 2 - DataGridRow.CellPadding);

    protected override Size MeasureOverride(Size availableSize)
    {
        _sortIcon.Measure(availableSize);
        _filterButton.Measure(new Size(FilterButtonSize, FilterButtonSize));
        float iconWidth = _sortIcon.Visibility == Visibility.Visible ? _sortIcon.DesiredSize.Width + 4 : 0;
        _content.Measure(new Size(Math.Max(0, Column.ActualWidth - DataGridRow.CellPadding * 2 - iconWidth - FilterButtonSpace), availableSize.Height));
        return new Size(Column.ActualWidth, Owner.HeaderHeight);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        float padding = DataGridRow.CellPadding;
        float buttonSpace = FilterButtonSpace;
        if (buttonSpace > 0)
        {
            // Right of the text, left of the resize grip; the text's right padding shrinks to make room.
            float bx = finalSize.Width - ResizeGripWidth - FilterButtonSize;
            _filterButton.Arrange(new Rect(bx, (finalSize.Height - FilterButtonSize) * 0.5f, FilterButtonSize, FilterButtonSize));

        }
        float iconWidth = _sortIcon.Visibility == Visibility.Visible ? _sortIcon.DesiredSize.Width + 4 : 0;
        float slot = Math.Max(0, finalSize.Width - padding * 2 - iconWidth - buttonSpace);
        var content = _content.DesiredSize;
        float width = Math.Min(slot, content.Width);
        float x = Column.CellAlignment switch
        {
            HorizontalAlignment.Right => padding + iconWidth + slot - width,
            HorizontalAlignment.Center => padding + (slot - width) * 0.5f,
            _ => padding,
        };
        _content.Arrange(new Rect(x, (finalSize.Height - content.Height) * 0.5f, width, content.Height));
        if (iconWidth > 0)
        {
            var icon = _sortIcon.DesiredSize;
            float iconX = Column.CellAlignment == HorizontalAlignment.Right ? x - iconWidth : x + width + 4;
            _sortIcon.Arrange(new Rect(iconX, (finalSize.Height - icon.Height) * 0.5f, icon.Width, icon.Height));
        }
        return finalSize;
    }
}

// Lays out a row of elements (headers or filter boxes) in the grid's column widths, after the selection column.
internal sealed class DataGridColumnsPanel : Panel
{
    private readonly DataGrid _owner;

    public DataGridColumnsPanel(DataGrid owner)
    {
        _owner = owner;
    }

    public UIElement? Leading { get; set; }

    public List<(DataGridColumn Column, UIElement Element)> Items { get; } = [];

    public float RowHeight { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        Leading?.Measure(new Size(DataGrid.SelectionColumnWidth, RowHeight));
        foreach (var (column, element) in Items)
        {
            element.Measure(new Size(column.ActualWidth, RowHeight));
        }
        return new Size(_owner.TotalColumnsWidth, RowHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // Every row of the grid (headers, filter boxes, items) starts after the check box column in Multiple mode.
        float x = 0;
        if (_owner.ShowsSelectionColumn)
        {
            if (Leading != null)
            {
                var size = Leading.DesiredSize;
                Leading.Arrange(new Rect((DataGrid.SelectionColumnWidth - size.Width) * 0.5f, (finalSize.Height - size.Height) * 0.5f, size.Width, size.Height));
            }
            x = DataGrid.SelectionColumnWidth;
        }
        foreach (var (column, element) in Items)
        {
            if (element is DataGridColumnHeader)
            {
                element.Arrange(new Rect(x, 0, column.ActualWidth, finalSize.Height));
            }
            else
            {
                // Filter boxes: inset a little, vertically centered.
                float h = Math.Min(finalSize.Height - 8, element.DesiredSize.Height);
                element.Arrange(new Rect(x + 4, (finalSize.Height - h) * 0.5f, Math.Max(0, column.ActualWidth - 8), h));
            }
            x += column.ActualWidth;
        }
        return finalSize;
    }
}
