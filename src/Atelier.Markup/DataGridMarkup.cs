using System;
using System.Collections;
using Atelier.Controls;

namespace Atelier.Markup;

/// <summary>Fluent methods for <see cref="DataGrid"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class DataGridMarkup
{
    /// <summary>Adds columns.</summary>
    public static T Columns<T>(this T grid, params DataGridColumn[] columns) where T : DataGrid
    {
        foreach (var column in columns)
        {
            grid.Columns.Add(column);
        }
        return grid;
    }

    /// <summary>Sets the items.</summary>
    public static T ItemsSource<T>(this T grid, IEnumerable? items) where T : DataGrid => grid.Set(DataGrid.ItemsSourceProperty, items);

    /// <summary>Sets how rows are selected: single (the default), multiple with check boxes, or extended (Ctrl/Shift).</summary>
    public static T SelectionMode<T>(this T grid, DataGridSelectionMode mode) where T : DataGrid => grid.Set(DataGrid.SelectionModeProperty, mode);

    /// <summary>Sets whether clicks select rows (the default) or cells.</summary>
    public static T SelectionUnit<T>(this T grid, DataGridSelectionUnit unit) where T : DataGrid => grid.Set(DataGrid.SelectionUnitProperty, unit);

    /// <summary>Sets what Ctrl+C copies (the selection without headers by default).</summary>
    public static T ClipboardCopyMode<T>(this T grid, DataGridClipboardCopyMode mode) where T : DataGrid => grid.Set(DataGrid.ClipboardCopyModeProperty, mode);

    /// <summary>Selects <paramref name="item"/>.</summary>
    public static T SelectedItem<T>(this T grid, object? item) where T : DataGrid => grid.Set(DataGrid.SelectedItemProperty, item);

    /// <summary>Sets the height of each row (40 by default).</summary>
    public static T RowHeight<T>(this T grid, float height) where T : DataGrid => grid.Set(DataGrid.RowHeightProperty, height);

    /// <summary>Sets the height of the header row (44 by default).</summary>
    public static T HeaderHeight<T>(this T grid, float height) where T : DataGrid => grid.Set(DataGrid.HeaderHeightProperty, height);

    /// <summary>Shows a row of filter boxes under the headers.</summary>
    public static T ShowFilterRow<T>(this T grid, bool show = true) where T : DataGrid => grid.Set(DataGrid.ShowFilterRowProperty, show);

    /// <summary>Sets whether clicking headers sorts (on by default).</summary>
    public static T CanSortColumns<T>(this T grid, bool canSort = true) where T : DataGrid => grid.Set(DataGrid.CanSortColumnsProperty, canSort);

    /// <summary>Sets whether columns can be resized (on by default).</summary>
    public static T CanResizeColumns<T>(this T grid, bool canResize = true) where T : DataGrid => grid.Set(DataGrid.CanResizeColumnsProperty, canResize);

    /// <summary>Sets whether columns can be dragged to a new place (on by default).</summary>
    public static T CanReorderColumns<T>(this T grid, bool canReorder = true) where T : DataGrid => grid.Set(DataGrid.CanReorderColumnsProperty, canReorder);

    /// <summary>Sets whether right-clicking the headers opens the column chooser (on by default).</summary>
    public static T ShowColumnChooser<T>(this T grid, bool show = true) where T : DataGrid => grid.Set(DataGrid.ShowColumnChooserProperty, show);

    /// <summary>Sets what shows while there are no items: a string or an element.</summary>
    public static T EmptyContent<T>(this T grid, object? content) where T : DataGrid => grid.Set(DataGrid.EmptyContentProperty, content);

    /// <summary>Sets what shows when the filters hide all items: a string or an element.</summary>
    public static T NoMatchesContent<T>(this T grid, object? content) where T : DataGrid => grid.Set(DataGrid.NoMatchesContentProperty, content);

    /// <summary>Sets the menu of a row, opened with its DataContext set to the row's item.</summary>
    public static T RowContextMenu<T>(this T grid, ContextMenu? menu) where T : DataGrid => grid.Set(DataGrid.RowContextMenuProperty, menu);

    /// <summary>Sets whether headers have a filter button that opens the column's filter menu (on by default).</summary>
    public static T ShowFilterMenus<T>(this T grid, bool show = true) where T : DataGrid => grid.Set(DataGrid.ShowFilterMenusProperty, show);

    /// <summary>Sets whether no cell can be edited.</summary>
    public static T IsReadOnly<T>(this T grid, bool isReadOnly = true) where T : DataGrid => grid.Set(DataGrid.IsReadOnlyProperty, isReadOnly);

    /// <summary>Handles <see cref="DataGrid.FilterChanged"/>.</summary>
    public static T OnFilterChanged<T>(this T grid, EventHandler handler) where T : DataGrid
    {
        grid.FilterChanged += handler;
        return grid;
    }

    /// <summary>Handles <see cref="DataGrid.CellEditEnding"/>, e.g. to validate an edited value.</summary>
    public static T OnCellEditEnding<T>(this T grid, EventHandler<DataGridCellEditEndingEventArgs> handler) where T : DataGrid
    {
        grid.CellEditEnding += handler;
        return grid;
    }

    /// <summary>Sets a filter of the items.</summary>
    public static T Filter<T>(this T grid, Predicate<object>? filter) where T : DataGrid
    {
        grid.Filter = filter;
        return grid;
    }

    /// <summary>Handles <see cref="DataGrid.SelectionChanged"/>.</summary>
    public static T OnSelectionChanged<T>(this T grid, EventHandler handler) where T : DataGrid
    {
        grid.SelectionChanged += handler;
        return grid;
    }

    /// <summary>Handles <see cref="DataGrid.RowActivated"/>, raised with the item of a double-clicked row or on Enter.</summary>
    public static T OnRowActivated<T>(this T grid, EventHandler<object> handler) where T : DataGrid
    {
        grid.RowActivated += handler;
        return grid;
    }

    /// <summary>Handles <see cref="DataGrid.LayoutChanged"/>, e.g. to save the layout.</summary>
    public static T OnLayoutChanged<T>(this T grid, EventHandler handler) where T : DataGrid
    {
        grid.LayoutChanged += handler;
        return grid;
    }
}
