using System;
using System.Collections.Generic;
using System.ComponentModel;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>A value listed in a <see cref="DataGridFilterMenu"/>: its text, how many items have it, and whether it is checked.</summary>
public sealed class DataGridFilterValue : INotifyPropertyChanged
{
    private bool _isChecked;

    internal DataGridFilterValue(string key, int count, bool isChecked)
    {
        Key = key;
        Count = count;
        _isChecked = isChecked;
    }

    /// <summary>Gets the filter key (see <see cref="DataGridColumn.GetFilterKey"/>); empty for blanks.</summary>
    public string Key { get; }

    /// <summary>Gets the text shown: the key, or "(Blanks)".</summary>
    public string Text => Key.Length == 0 ? DataGridFilterMenu.BlanksText : Key;

    /// <summary>Gets the number of items with this value.</summary>
    public int Count { get; }

    /// <summary>Gets or sets whether items with this value are shown.</summary>
    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc/>
    public override string ToString() => Text; // type-ahead search in the list
}

/// <summary>
/// The spreadsheet-style filter menu of a <see cref="DataGrid"/> column, opened from the header's filter button: sort
/// ascending or descending, clear the column's filter, and pick the values to show from a searchable list of the
/// column's values (those of the items the other filters leave, with their counts).
/// </summary>
/// <remarks>
/// OK applies the checked values as <see cref="DataGridColumn.FilterValues"/> (all checked clears it). While the list is
/// searched, OK shows just the checked values among the matches, as spreadsheets do. Enter in the search box is OK,
/// Escape closes the menu. The list is virtualized, so columns with many values open quickly.
/// </remarks>
public class DataGridFilterMenu : Popup
{
    /// <summary>The text shown for blank values.</summary>
    public const string BlanksText = "(Blanks)";

    /// <summary>The width of the menu.</summary>
    public const float MenuWidth = 280f;

    private readonly DataGrid _grid;
    private readonly ListBoxItem _sortAscending;
    private readonly ListBoxItem _sortDescending;
    private readonly ListBoxItem _clearFilter;
    private readonly TextBox _search;
    private readonly CheckBox _selectAll;
    private readonly ValueList _list;
    private readonly TextBlock _noMatches;
    private readonly Button _ok;
    private List<DataGridFilterValue> _values = [];
    private List<DataGridFilterValue> _shown = [];
    private DataGridColumnHeader? _header;
    private bool _isUpdatingSelectAll;

    internal DataGridFilterMenu(DataGrid grid)
    {
        _grid = grid;
        Placement = PlacementMode.Bottom;
        StaysOpen = false;
        VerticalOffset = 2;

        _sortAscending = MenuRow(MaterialIconKind.ArrowUpward, "Sort ascending");
        _sortAscending.Clicked += (_, _) => SortAndClose(DataGridSortDirection.Ascending);
        _sortDescending = MenuRow(MaterialIconKind.ArrowDownward, "Sort descending");
        _sortDescending.Clicked += (_, _) => SortAndClose(DataGridSortDirection.Descending);
        _clearFilter = MenuRow(MaterialIconKind.FilterAltOff, "Clear filter");
        _clearFilter.Clicked += (_, _) =>
        {
            Column?.ClearFilter();
            IsOpen = false;
        };

        _search = new TextBox
        {
            Placeholder = "Search",
            LeadingIconKind = MaterialIconKind.Search,
            FieldHeight = 40,
            Margin = new Thickness(12, 8, 12, 4),
        };
        _search.TextChanged += (_, _) => UpdateShownValues();
        _search.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Apply();
                e.Handled = true;
            }
            else if (e.Key == Key.Down && _shown.Count > 0)
            {
                _list!.Focus(); // (created below; this runs later)
                if (_list.SelectedIndex < 0) _list.SelectedIndex = 0;
                e.Handled = true;
            }
        };

        _selectAll = new CheckBox("(Select all)") { IsThreeState = true, IsFocusable = false, Margin = new Thickness(12, 4, 12, 0) };
        _selectAll.CheckedChanged += (_, isChecked) => OnSelectAllChanged(isChecked);

        _list = new ValueList(this) { Height = 220, Margin = new Thickness(4, 0) };
        _list.ItemTemplate = item => CreateValueRow((DataGridFilterValue)item);

        _noMatches = new TextBlock { Text = "No matching values", Muted = true, Margin = new Thickness(16, 8), Visibility = Visibility.Collapsed };

        var cancel = new Button("Cancel") { Variant = ButtonVariant.Text };
        cancel.Click += (_, _) => IsOpen = false;
        _ok = new Button("OK") { Variant = ButtonVariant.Filled };
        _ok.Click += (_, _) => Apply();
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 8, 12, 12) };
        actions.WithChildren(cancel, _ok);

        var divider = new Border { Height = 1, Margin = new Thickness(0, 4), StyleKey = DividerStyleKey };
        var content = new StackPanel { Width = MenuWidth, Margin = new Thickness(0, 8, 0, 0) };
        content.WithChildren(_sortAscending, _sortDescending, _clearFilter, divider, _search, _selectAll, _list, _noMatches, actions);
        Child = content;

        Closed += (_, _) =>
        {
            var header = _header;
            _header = null;
            header?.UpdateFilterButton();
        };
        grid.AddChild(this);
    }

    /// <summary>Identifies the style of the divider under the menu rows (a <see cref="Border"/>).</summary>
    public const string DividerStyleKey = "DataGridFilterMenuDivider";

    /// <summary>Gets the column the menu filters, while it is open.</summary>
    public DataGridColumn? Column => _header?.Column;

    /// <summary>Gets the search box.</summary>
    public TextBox SearchBox => _search;

    /// <summary>Gets the "(Select all)" check box.</summary>
    public CheckBox SelectAllBox => _selectAll;

    /// <summary>Gets the list of values.</summary>
    public ListBox ValueListBox => _list;

    /// <summary>Gets all values of the column, in sort order.</summary>
    public IReadOnlyList<DataGridFilterValue> Values => _values;

    /// <summary>Gets the values the search leaves, in sort order.</summary>
    public IReadOnlyList<DataGridFilterValue> ShownValues => _shown;

    internal void Open(DataGridColumnHeader header)
    {
        var column = header.Column;
        IsOpen = false;
        _header = header;
        PlacementTarget = header;

        var filter = column.FilterValues;
        _values = [];
        foreach (var (key, count) in _grid.GetFilterValues(column))
        {
            _values.Add(new DataGridFilterValue(key, count, filter == null || ContainsKey(filter, key)));
        }
        // Checked values the other filters hide still count as checked, so they stay when OK is pressed.
        _hiddenCheckedKeys = [];
        if (filter != null)
        {
            var listed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in _values) listed.Add(value.Key);
            foreach (var key in filter)
            {
                if (!listed.Contains(key)) _hiddenCheckedKeys.Add(key);
            }
        }

        bool sortable = _grid.CanSortColumns && column.IsSortable;
        _sortAscending.Visibility = _sortDescending.Visibility = sortable ? Visibility.Visible : Visibility.Collapsed;
        _clearFilter.IsEnabled = column.IsFiltered;
        _search.Text = string.Empty;
        UpdateShownValues();

        IsOpen = true;
        header.UpdateFilterButton();
        _search.Focus();
    }

    private List<string> _hiddenCheckedKeys = [];

    private static bool ContainsKey(IReadOnlyCollection<string> keys, string key)
    {
        if (keys is ISet<string> set) return set.Contains(key);
        foreach (var k in keys)
        {
            if (k == key) return true;
        }
        return false;
    }

    /// <summary>Applies the checked values to the column and closes the menu.</summary>
    public void Apply()
    {
        var column = Column;
        if (column == null) return;
        bool searching = _search.Text.Length > 0;
        var source = searching ? _shown : _values;

        var keys = new List<string>();
        foreach (var value in source)
        {
            if (value.IsChecked) keys.Add(value.Key);
        }
        if (keys.Count == 0) return; // nothing to show: OK is disabled

        if (!searching) keys.AddRange(_hiddenCheckedKeys);
        bool all = !searching && keys.Count == _values.Count + _hiddenCheckedKeys.Count && _hiddenCheckedKeys.Count == 0;
        IsOpen = false;
        column.FilterValues = all ? null : keys;
    }

    private void SortAndClose(DataGridSortDirection direction)
    {
        var column = Column;
        IsOpen = false;
        if (column != null) _grid.SortBy(column, direction);
    }

    private void UpdateShownValues()
    {
        string search = _search.Text;
        _shown = [];
        foreach (var value in _values)
        {
            if (search.Length == 0 || value.Text.Contains(search, StringComparison.CurrentCultureIgnoreCase)) _shown.Add(value);
        }

        // Searching starts from the matches, all checked, like a spreadsheet.
        if (search.Length > 0)
        {
            foreach (var value in _values) value.IsChecked = _shown.Contains(value);
        }
        else if (Column is { } column)
        {
            var filter = column.FilterValues;
            foreach (var value in _values) value.IsChecked = filter == null || ContainsKey(filter, value.Key);
        }

        _list.ItemsSource = _shown;
        _list.Visibility = _shown.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _noMatches.Visibility = _shown.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        _list.Height = Math.Min(220f, Math.Max(1, _shown.Count) * 36f + 4);
        UpdateSelectAll();
    }

    private void UpdateSelectAll()
    {
        int checkedCount = 0;
        foreach (var value in _shown)
        {
            if (value.IsChecked) checkedCount++;
        }
        _isUpdatingSelectAll = true;
        try
        {
            _selectAll.IsChecked = checkedCount == 0 ? false : checkedCount == _shown.Count ? true : null;
            _selectAll.IsEnabled = _shown.Count > 0;
        }
        finally
        {
            _isUpdatingSelectAll = false;
        }
        _ok.IsEnabled = checkedCount > 0;
    }

    private void OnSelectAllChanged(bool? isChecked)
    {
        if (_isUpdatingSelectAll) return;
        bool check = isChecked != false; // the indeterminate state (from a click on an unchecked box) checks all
        foreach (var value in _shown) value.IsChecked = check;
        UpdateSelectAll();
    }

    internal void Toggle(DataGridFilterValue value)
    {
        value.IsChecked = !value.IsChecked;
        UpdateSelectAll();
    }

    private static ListBoxItem MenuRow(MaterialIconKind icon, string text)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        row.WithChildren(
            new Icon(icon, 20) { VerticalAlignment = VerticalAlignment.Center },
            new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
        return new ListBoxItem { Content = row, Padding = new Thickness(16, 8), CornerRadius = CornerRadius.Zero };
    }

    private static UIElement CreateValueRow(DataGridFilterValue value)
    {
        var box = new CheckBox { IsChecked = value.IsChecked, IsFocusable = false, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center };
        value.PropertyChanged += (_, _) => box.IsChecked = value.IsChecked;
        var text = new TextBlock
        {
            Text = value.Text,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Italic = value.Key.Length == 0,
        };
        var count = new TextBlock { Text = value.Count.ToString("N0"), Muted = true, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        var row = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(box, Dock.Left);
        DockPanel.SetDock(count, Dock.Right);
        box.Margin = new Thickness(0, 0, 8, 0);
        count.Margin = new Thickness(8, 0, 0, 0);
        row.Add(box);
        row.Add(count);
        row.Add(text);
        return row;
    }

    // The value list: a click or Space toggles a value instead of only selecting it.
    private sealed class ValueList(DataGridFilterMenu menu) : ListBox
    {
        protected override UIElement CreateVirtualContainer()
        {
            var container = (ListBoxItem)base.CreateVirtualContainer();
            container.Padding = new Thickness(12, 4);
            container.Clicked += (s, _) =>
            {
                if (((ListBoxItem)s!).ItemValue is DataGridFilterValue value) menu.Toggle(value);
            };
            return container;
        }

        public override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Space && SelectedItem is DataGridFilterValue value)
            {
                menu.Toggle(value);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Enter)
            {
                menu.Apply();
                e.Handled = true;
                return;
            }
            base.OnKeyDown(e);
        }
    }
}
