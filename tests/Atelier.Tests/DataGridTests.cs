using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Tests;

public class DataGridTests
{
    private sealed class Person(string name, int age, string city)
    {
        public string Name { get; set; } = name;
        public int Age { get; set; } = age;
        public string City { get; } = city;
        public bool IsActive { get; set; }
    }

    private sealed class Observable : System.ComponentModel.INotifyPropertyChanged
    {
        private string _name = "";
        private bool _isReadOnly;

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }

        public bool IsReadOnly { get => _isReadOnly; set { _isReadOnly = value; PropertyChanged?.Invoke(this, new(nameof(IsReadOnly))); } }
    }

    private static ObservableCollection<Person> People() =>
    [
        new("Carol", 35, "Bern"),
        new("alice", 30, "Zurich"),
        new("Bob", 25, "Basel"),
        new("Dave", 30, "Bern"),
        new("Eve", 25, "Zurich"),
    ];

    private static DataGrid Grid(IEnumerable<Person> items, DataGridSelectionMode mode = DataGridSelectionMode.Single) =>
        Layout(new DataGrid()
            .SelectionMode(mode)
            .ItemsSource(items)
            .Columns(
                new DataGridTextColumn<Person>("Name", p => p.Name) { Key = "name", Width = GridLength.Pixels(200) },
                new DataGridTextColumn<Person>("Age", p => p.Age) { Key = "age", Width = GridLength.Pixels(100) },
                new DataGridTextColumn<Person>("City", p => p.City) { Key = "city", Width = GridLength.Pixels(150) }));

    private static DataGrid Layout(DataGrid grid, float width = 800, float height = 400)
    {
        if (!grid.IsAttachedToVisualTree) grid.AttachToHost();
        for (int pass = 0; pass < 2; pass++)
        {
            grid.Measure(new Size(width, height));
            grid.Arrange(new Rect(0, 0, width, height));
        }
        return grid;
    }

    private static IEnumerable<VisualNode> Descendants(VisualNode node)
    {
        foreach (var child in node.Children)
        {
            yield return child;
            foreach (var d in Descendants(child)) yield return d;
        }
    }

    private static string[] Names(DataGrid grid) => grid.View.Cast<Person>().Select(p => p.Name).ToArray();

    private static PointerEventArgs At(Point p, PointerButtons button = PointerButtons.Left, ModifierKeys modifiers = ModifierKeys.None, int clickCount = 1) =>
        new(p, p, button, 0, modifiers, clickCount);

    private static Point Center(UIElement element) =>
        element.PointToScreen(new Point(element.Bounds.Width / 2, element.Bounds.Height / 2));

    private static void Click(DataGrid grid, int index, ModifierKeys modifiers = ModifierKeys.None, int clickCount = 1)
    {
        var row = grid.GetRow(index)!;
        var p = Center(row);
        row.OnPointerPressed(At(p, modifiers: modifiers, clickCount: clickCount));
        row.OnPointerReleased(At(p, modifiers: modifiers, clickCount: clickCount));
    }

    private static void Key(DataGrid grid, Key key, ModifierKeys modifiers = ModifierKeys.None) =>
        grid.OnKeyDown(new KeyEventArgs(key, 0, modifiers, true));

    [Fact]
    public void RealizesOnlyTheRowsInView_AndRecyclesThemWhenScrolling()
    {
        var items = Enumerable.Range(0, 10_000).Select(i => new Person($"P{i}", i % 90, "X")).ToList();
        var grid = Grid(items);

        Assert.Equal(10_000, grid.View.Count);
        int realized = grid.RowsPanel.RealizedContainers.Count;
        Assert.InRange(realized, 8, 20); // ~9 rows in view plus overscan
        Assert.Equal(10_000 * grid.RowHeight, grid.RowsPanel.DesiredSize.Height, 1);
        Assert.Equal("P0", ((Person)grid.GetRow(0)!.Item!).Name);

        grid.ScrollViewer.ScrollTo(0, 5000 * grid.RowHeight, animate: false);
        Layout(grid);
        Assert.Null(grid.GetRow(0));
        var row = grid.GetRow(5002)!;
        Assert.Equal("P5002", ((Person)row.Item!).Name);
        Assert.Equal("P5002", ((TextBlock)row.Cells[0]).Text);
        Assert.InRange(grid.RowsPanel.RealizedContainers.Count, 8, 20);
    }

    [Fact]
    public void HeaderClicks_SortAscendingDescendingAndOff_ShiftAddsASecondColumn()
    {
        var grid = Grid(People());
        var name = grid.Columns[0];
        var age = grid.Columns[1];

        grid.SortBy(name, DataGridSortDirection.Ascending);
        Assert.Equal(new[] { "alice", "Bob", "Carol", "Dave", "Eve" }, Names(grid)); // case-insensitive

        var header = grid.Headers[0];
        var p = Center(header);
        header.OnPointerPressed(At(p));
        header.OnPointerReleased(At(p));
        Assert.Equal(DataGridSortDirection.Descending, name.SortDirection);
        Assert.Equal(new[] { "Eve", "Dave", "Carol", "Bob", "alice" }, Names(grid));

        header.OnPointerPressed(At(p));
        header.OnPointerReleased(At(p));
        Assert.Equal(DataGridSortDirection.None, name.SortDirection);
        Assert.Equal(new[] { "Carol", "alice", "Bob", "Dave", "Eve" }, Names(grid)); // source order

        // Age, then City with Shift: ties in age keep a stable, city-sorted order.
        grid.SortBy(age, DataGridSortDirection.Ascending);
        Assert.Equal(new[] { "Bob", "Eve", "alice", "Dave", "Carol" }, Names(grid));
        var cityHeader = grid.Headers[2];
        var c = Center(cityHeader);
        cityHeader.OnPointerPressed(At(c, modifiers: ModifierKeys.Shift));
        cityHeader.OnPointerReleased(At(c, modifiers: ModifierKeys.Shift));
        Assert.Equal(new[] { age, grid.Columns[2] }, grid.SortColumns);
        Assert.Equal(1, grid.Columns[2].SortOrder);
        Assert.Equal(new[] { "Bob", "Eve", "Dave", "alice", "Carol" }, Names(grid));
    }

    [Fact]
    public void FilterAndColumnFilters_HideItems_AndThePlaceholderTellsEmptyFromNoMatches()
    {
        var people = People();
        var grid = Grid(people);
        Assert.Equal(Visibility.Collapsed, grid.Children.OfType<ContentControl>().Last().Visibility);

        grid.Filter = o => ((Person)o).Age >= 30;
        Assert.Equal(new[] { "Carol", "alice", "Dave" }, Names(grid));

        grid.Columns[2].FilterText = "bern";
        Assert.Equal(new[] { "Carol", "Dave" }, Names(grid));

        grid.Columns[0].FilterText = "zzz";
        Assert.Empty(grid.View);
        var placeholder = grid.Children.OfType<ContentControl>().Last();
        Assert.Equal(Visibility.Visible, placeholder.Visibility);
        Assert.Equal("No matching items", ((TextBlock)placeholder.Content!).Text);

        grid.Columns[0].FilterText = "";
        grid.Filter = null;
        grid.Columns[2].FilterText = "";
        people.Clear();
        Assert.Equal("No items", ((TextBlock)placeholder.Content!).Text);

        people.Add(new Person("New", 1, "Y"));
        Assert.Equal(new[] { "New" }, Names(grid));
        Assert.Equal(Visibility.Collapsed, placeholder.Visibility);
    }

    [Fact]
    public void SingleSelection_ClickAndArrowKeysSelectOneRow()
    {
        var grid = Grid(People());
        int changes = 0;
        grid.SelectionChanged += (_, _) => changes++;

        Click(grid, 1);
        Assert.Same(grid.View[1], grid.SelectedItem);
        Assert.Single(grid.SelectedItems);
        Assert.True(grid.GetRow(1)!.IsSelected);
        Assert.Equal(1, changes);

        Key(grid, Atelier.Core.Events.Key.Down);
        Assert.Same(grid.View[2], grid.SelectedItem);
        Assert.False(grid.GetRow(1)!.IsSelected);
        Key(grid, Atelier.Core.Events.Key.End);
        Assert.Same(grid.View[4], grid.SelectedItem);
        Assert.Equal(4, grid.CurrentIndex);

        // Ctrl+click can't add in single mode, Ctrl+A does nothing.
        Click(grid, 0, ModifierKeys.Control);
        Key(grid, Atelier.Core.Events.Key.A, ModifierKeys.Control);
        Assert.Single(grid.SelectedItems);

        grid.SelectedItem = grid.View[3];
        Assert.True(grid.GetRow(3)!.IsSelected);
        Assert.Equal(3, grid.CurrentIndex);
    }

    [Fact]
    public void ExtendedSelection_CtrlTogglesAndShiftSelectsARange()
    {
        var grid = Grid(People(), DataGridSelectionMode.Extended);

        Click(grid, 1);
        Click(grid, 3, ModifierKeys.Shift);
        Assert.Equal(new[] { 1, 2, 3 }, grid.SelectedItems.Select(i => grid.View.ToList().IndexOf(i)).OrderBy(i => i));

        Click(grid, 2, ModifierKeys.Control);
        Assert.Equal(2, grid.SelectedItems.Count);
        Assert.False(grid.GetRow(2)!.IsSelected);

        Click(grid, 0);
        Assert.Single(grid.SelectedItems);
        Key(grid, Atelier.Core.Events.Key.Down, ModifierKeys.Shift);
        Key(grid, Atelier.Core.Events.Key.Down, ModifierKeys.Shift);
        Assert.Equal(3, grid.SelectedItems.Count);

        // Ctrl+arrows move the current row only; Space toggles it.
        Key(grid, Atelier.Core.Events.Key.Down, ModifierKeys.Control);
        Assert.Equal(3, grid.CurrentIndex);
        Assert.Equal(3, grid.SelectedItems.Count);
        Key(grid, Atelier.Core.Events.Key.Space);
        Assert.Equal(4, grid.SelectedItems.Count);

        Key(grid, Atelier.Core.Events.Key.A, ModifierKeys.Control);
        Assert.Equal(5, grid.SelectedItems.Count);
    }

    [Fact]
    public void MultipleSelection_UsesCheckBoxes_AndTheHeaderBoxSelectsAll()
    {
        var grid = Grid(People(), DataGridSelectionMode.Multiple);
        var row = grid.GetRow(0)!;
        var box = row.Children.OfType<CheckBox>().First();

        Click(grid, 0);
        Click(grid, 2);
        Assert.Equal(2, grid.SelectedItems.Count);
        Assert.True(box.IsChecked);

        Click(grid, 0); // a click toggles
        Assert.Single(grid.SelectedItems);
        Assert.False(box.IsChecked);

        box.IsChecked = true; // the row's box selects it
        Assert.Equal(2, grid.SelectedItems.Count);

        var selectAll = Descendants(grid).OfType<CheckBox>().First(c => c.Parent is not DataGridRow);
        Assert.Null(selectAll.IsChecked); // some selected
        selectAll.IsChecked = true;
        Assert.Equal(5, grid.SelectedItems.Count);
        Assert.True(selectAll.IsChecked);
        selectAll.IsChecked = false;
        Assert.Empty(grid.SelectedItems);
    }

    [Fact]
    public void SelectedItemsCollection_ChangesTheSelection_AndRemovedItemsLeaveIt()
    {
        var people = People();
        var grid = Grid(people, DataGridSelectionMode.Extended);
        grid.SelectedItems.Add(people[1]);
        grid.SelectedItems.Add(people[3]);
        Assert.Same(people[1], grid.SelectedItem);
        Assert.True(grid.GetRow(3)!.IsSelected);

        people.RemoveAt(1);
        Assert.Equal(new object[] { people[2] }, grid.SelectedItems);
        Assert.Same(people[2], grid.SelectedItem);
    }

    [Fact]
    public void DoubleClickAndEnter_RaiseRowActivated()
    {
        var grid = Grid(People());
        var activated = new List<object>();
        grid.RowActivated += (_, item) => activated.Add(item);

        Click(grid, 2, clickCount: 2);
        Key(grid, Atelier.Core.Events.Key.Enter);
        Assert.Equal(new[] { grid.View[2], grid.View[2] }, activated);
    }

    [Fact]
    public void ColumnWidths_PixelAutoAndStar()
    {
        var grid = Layout(new DataGrid()
            .ItemsSource(People())
            .Columns(
                new DataGridTextColumn<Person>("Name", p => p.Name) { Width = GridLength.Pixels(120) },
                new DataGridTextColumn<Person>("Age", p => p.Age) { Width = GridLength.Auto },
                new DataGridTextColumn<Person>("City", p => p.City) { Width = GridLength.Stars(1) },
                new DataGridTextColumn<Person>("Note", p => "") { Width = GridLength.Stars(3) }), width: 1000);

        Assert.Equal(120, grid.Columns[0].ActualWidth);
        float auto = grid.Columns[1].ActualWidth;
        Assert.InRange(auto, grid.Columns[1].MinWidth, 150);
        float rest = 1000 - 120 - auto;
        Assert.Equal(rest / 4, grid.Columns[2].ActualWidth, 1);
        Assert.Equal(rest * 3 / 4, grid.Columns[3].ActualWidth, 1);
        Assert.Equal(1000, grid.TotalColumnsWidth, 1);
    }

    [Fact]
    public void DraggingTheHeaderEdge_ResizesTheColumn()
    {
        var grid = Grid(People());
        var header = grid.Headers[0];
        int layoutChanges = 0;
        grid.LayoutChanged += (_, _) => layoutChanges++;

        var edge = header.PointToScreen(new Point(header.Bounds.Width - 2, header.Bounds.Height / 2));
        header.OnPointerMoved(At(edge, PointerButtons.None));
        Assert.True(header.IsOverResizeGrip);
        header.OnPointerPressed(At(edge));
        var to = new Point(edge.X + 60, edge.Y);
        header.OnPointerMoved(At(to));
        header.OnPointerReleased(At(to));
        Layout(grid);

        Assert.Equal(260, grid.Columns[0].ActualWidth, 1);
        Assert.Equal(0, grid.Columns[0].SortOrder < 0 ? 0 : 1); // resizing doesn't sort
        Assert.True(layoutChanges > 0);
        Assert.Equal(260, grid.GetRow(0)!.Cells[1].Bounds.X - DataGridRow.CellPadding, 1);
    }

    [Fact]
    public void DraggingAHeader_MovesTheColumn()
    {
        var grid = Grid(People());
        var name = grid.Columns[0];
        var header = grid.Headers[0];
        var start = Center(header);

        header.OnPointerPressed(At(start));
        var over = Center(grid.Headers[2]);
        header.OnPointerMoved(At(new Point(start.X + 30, start.Y)));
        header.OnPointerMoved(At(new Point(over.X + 40, start.Y)));
        Assert.True(header.IsDragging);
        Assert.True(float.IsFinite(grid.DropIndicatorX));
        header.OnPointerReleased(At(new Point(over.X + 40, start.Y)));
        Layout(grid);

        Assert.Equal(new[] { "age", "city", "name" }, grid.Columns.Select(c => c.EffectiveKey));
        Assert.Equal(DataGridSortDirection.None, name.SortDirection); // a drag isn't a click
        Assert.False(float.IsFinite(grid.DropIndicatorX));
        Assert.Equal("Carol", ((TextBlock)grid.GetRow(0)!.Cells[2]).Text);
    }

    [Fact]
    public void ColumnChooser_ShowsAndHidesColumns_ButKeepsTheOnesThatCantHide()
    {
        var grid = Grid(People());
        grid.Columns[0].CanHide = false;
        var menu = grid.CreateColumnChooserMenu();
        var items = menu.Items.OfType<MenuItem>().Take(3).ToArray();

        Assert.All(items, i => Assert.True(i.IsChecked));
        Assert.False(items[0].IsEnabled);

        items[1].IsChecked = false;
        Layout(grid);
        Assert.False(grid.Columns[1].IsVisible);
        Assert.Equal(new[] { "name", "city" }, grid.VisibleColumns.Select(c => c.EffectiveKey));
        Assert.Equal(2, grid.GetRow(0)!.Cells.Count);
        Assert.Equal(2, grid.Headers.Count);
    }

    [Fact]
    public void Layout_SavesAndRestores_OrderWidthsVisibilityAndSort_ThroughJson()
    {
        var grid = Grid(People());
        grid.Columns.Move(2, 0);
        grid.Columns[1].Width = GridLength.Pixels(222);
        grid.Columns[2].IsVisible = false;
        grid.SortBy(grid.FindColumn("city"), DataGridSortDirection.Descending);

        string json = grid.SaveLayout().ToJson();

        var other = Grid(People());
        other.RestoreLayout(DataGridLayout.FromJson(json));
        Layout(other);
        Assert.Equal(new[] { "city", "name", "age" }, other.Columns.Select(c => c.EffectiveKey));
        Assert.Equal(222, other.FindColumn("name")!.ActualWidth);
        Assert.False(other.FindColumn("age")!.IsVisible);
        Assert.Equal(DataGridSortDirection.Descending, other.FindColumn("city")!.SortDirection);
        Assert.Equal("Zurich", ((Person)other.View[0]).City);

        // Unknown keys are skipped, and columns the layout doesn't know stay after the known ones.
        var partial = DataGridLayout.FromJson("""{"columns":[{"key":"age","width":90,"visible":true},{"key":"gone","width":5}],"sort":[]}""");
        other.RestoreLayout(partial);
        Assert.Equal("age", other.Columns[0].EffectiveKey);
        Assert.Empty(other.SortColumns);
    }

    [Fact]
    public void ItemChanges_UpdateTheirRow_AndCheckBoxColumnsEditTheItem()
    {
        var file = new Observable { Name = "a.txt" };
        var items = new ObservableCollection<Observable> { file };
        var grid = Layout(new DataGrid().ItemsSource(items).Columns(
            new DataGridTextColumn<Observable>("Name", f => f.Name),
            new DataGridCheckBoxColumn("RO", f => ((Observable)f).IsReadOnly, (f, on) => ((Observable)f).IsReadOnly = on)));

        var row = grid.GetRow(0)!;
        file.Name = "b.txt";
        Assert.Equal("b.txt", ((TextBlock)row.Cells[0]).Text);

        var box = (CheckBox)row.Cells[1];
        box.IsChecked = true;
        Assert.True(file.IsReadOnly);
        file.IsReadOnly = false;
        Assert.False(box.IsChecked);
    }

    [Fact]
    public void RowContextMenu_OpensForTheRowItem()
    {
        var grid = Grid(People(), DataGridSelectionMode.Extended);
        var menu = new ContextMenu().Items(new MenuItem("Delete"));
        grid.RowContextMenu = menu;

        var row = grid.GetRow(3)!;
        var p = Center(row);
        row.OnPointerPressed(At(p, PointerButtons.Right));
        row.OnPointerReleased(At(p, PointerButtons.Right));

        Assert.True(menu.IsOpen);
        Assert.Same(grid.View[3], menu.DataContext);
        Assert.Same(grid.View[3], grid.SelectedItem); // right-click selects the row
        menu.IsOpen = false;
    }

    // An editable grid: Name (text), Age (number), City (read-only).
    private static DataGrid EditableGrid(IEnumerable<Person> items) =>
        Layout(new DataGrid()
            .ItemsSource(items)
            .Columns(
                new DataGridTextColumn<Person>("Name", p => p.Name, (p, v) => p.Name = (string)v!) { Key = "name", Width = GridLength.Pixels(200) },
                new DataGridTextColumn<Person>("Age", p => p.Age, (p, v) => p.Age = (int)v!) { Key = "age", Width = GridLength.Pixels(100) },
                new DataGridTextColumn<Person>("City", p => p.City) { Key = "city", Width = GridLength.Pixels(150) }));

    [Fact]
    public void FilterRow_LinesUpWithTheHeaders_InCheckBoxSelectionMode()
    {
        var grid = Grid(People(), DataGridSelectionMode.Multiple);
        grid.ShowFilterRow = true;
        Layout(grid);

        var boxes = Descendants(grid).OfType<TextBox>().ToArray();
        Assert.Equal(3, boxes.Length);
        for (int i = 0; i < 3; i++)
        {
            float headerX = grid.Headers[i].PointToScreen(Point.Zero).X;
            float boxX = boxes[i].PointToScreen(Point.Zero).X;
            Assert.Equal(headerX + 4, boxX, 1); // the boxes are inset by 4
        }
        Assert.Equal(DataGrid.SelectionColumnWidth, grid.Headers[0].PointToScreen(Point.Zero).X - grid.PointToScreen(Point.Zero).X, 1);
    }

    [Fact]
    public void FilterMenu_ListsTheValuesWithCounts_AndFiltersByTheCheckedOnes()
    {
        var grid = Grid(People());
        var city = grid.Columns[2];
        int changes = 0;
        grid.FilterChanged += (_, _) => changes++;

        Assert.True(grid.OpenFilterMenu(city));
        var menu = grid.OpenedFilterMenu!;
        Assert.Same(city, menu.Column);
        Assert.Equal(new[] { "Basel", "Bern", "Zurich" }, menu.Values.Select(v => v.Text));
        Assert.Equal(new[] { 1, 2, 2 }, menu.Values.Select(v => v.Count));
        Assert.All(menu.Values, v => Assert.True(v.IsChecked));
        Assert.True(menu.SelectAllBox.IsChecked);

        menu.Values[2].IsChecked = false; // Zurich
        menu.Apply();
        Assert.False(menu.IsOpen);
        Assert.Equal(new[] { "Carol", "Bob", "Dave" }, Names(grid));
        Assert.True(city.IsFiltered);
        Assert.Equal(new[] { "Basel", "Bern" }, city.FilterValues!.OrderBy(k => k));
        Assert.Equal(1, changes);
        Assert.Equal(Visibility.Visible, grid.Headers[2].FilterButton.Visibility); // shown while filtered

        // The Age menu lists only the ages the City filter leaves.
        grid.OpenFilterMenu(grid.Columns[1]);
        Assert.Equal(new[] { "25", "30", "35" }, grid.OpenedFilterMenu!.Values.Select(v => v.Text));
        Assert.Equal(new[] { 1, 1, 1 }, grid.OpenedFilterMenu!.Values.Select(v => v.Count));
        grid.OpenedFilterMenu!.IsOpen = false;

        // Checking everything again removes the filter.
        grid.OpenFilterMenu(city);
        menu = grid.OpenedFilterMenu!;
        Assert.False(menu.Values.Single(v => v.Text == "Zurich").IsChecked);
        Assert.Null(menu.SelectAllBox.IsChecked);
        menu.SelectAllBox.IsChecked = true;
        Assert.All(menu.Values, v => Assert.True(v.IsChecked));
        menu.Apply();
        Assert.Null(city.FilterValues);
        Assert.Equal(5, grid.View.Count);
    }

    [Fact]
    public void FilterMenu_SearchShowsTheMatches_AndOkKeepsOnlyThose()
    {
        var grid = Grid(People());
        grid.OpenFilterMenu(grid.Columns[0]);
        var menu = grid.OpenedFilterMenu!;

        menu.SearchBox.Text = "a";
        Assert.Equal(new[] { "alice", "Carol", "Dave" }, menu.ShownValues.Select(v => v.Text));
        menu.SelectAllBox.IsChecked = false;
        Assert.DoesNotContain(menu.ShownValues, v => v.IsChecked);
        menu.Apply(); // nothing checked: stays open
        Assert.True(menu.IsOpen);

        menu.ShownValues[1].IsChecked = true;
        menu.ShownValues[2].IsChecked = true;
        menu.Apply();
        Assert.Equal(new[] { "Carol", "Dave" }, Names(grid));

        grid.Columns[0].ClearFilter();
        Assert.Equal(5, grid.View.Count);
        Assert.False(grid.Columns[0].IsFiltered);
    }

    [Fact]
    public void FilterMenu_CheckBoxColumns_ListCheckedAndUnchecked_AndBlanksComeLast()
    {
        var people = People();
        people[0].IsActive = true;
        people.Add(new Person("", 40, ""));
        var grid = Layout(new DataGrid().ItemsSource(people).Columns(
            new DataGridTextColumn<Person>("City", p => p.City),
            new DataGridCheckBoxColumn("Active", p => ((Person)p).IsActive)));

        grid.OpenFilterMenu(grid.Columns[0]);
        Assert.Equal(DataGridFilterMenu.BlanksText, grid.OpenedFilterMenu!.Values[^1].Text);
        grid.OpenedFilterMenu!.IsOpen = false;

        grid.OpenFilterMenu(grid.Columns[1]);
        var menu = grid.OpenedFilterMenu!;
        Assert.Equal(new[] { DataGridCheckBoxColumn.UncheckedKey, DataGridCheckBoxColumn.CheckedKey }, menu.Values.Select(v => v.Key));
        menu.Values[0].IsChecked = false;
        menu.Apply();
        Assert.Equal(new[] { "Carol" }, Names(grid));

        grid.ClearFilters();
        Assert.Equal(6, grid.View.Count);
        Assert.False(grid.HasColumnFilters);
    }

    [Fact]
    public void F2_EditsTheCurrentCell_EnterCommits_EscapeCancels()
    {
        var people = People();
        var grid = EditableGrid(people);
        var ended = new List<bool>();
        grid.CellEditEnded += (_, committed) => ended.Add(committed);

        Click(grid, 1); // alice, clicked in the Name cell
        Key(grid, Atelier.Core.Events.Key.F2);
        Assert.True(grid.IsEditing);
        Assert.Equal("name", grid.EditContext!.Column.EffectiveKey);
        var row = grid.GetRow(1)!;
        var box = Assert.IsType<TextBox>(row.Editor);
        Assert.Equal("alice", box.Text);
        Assert.Equal(Visibility.Hidden, row.Cells[0].Visibility);

        box.Text = "Alice";
        Key(grid, Atelier.Core.Events.Key.Enter);
        Assert.False(grid.IsEditing);
        Assert.Equal("Alice", people[1].Name);
        Assert.Null(row.Editor);
        Assert.Equal("Alice", ((TextBlock)row.Cells[0]).Text);

        // Right moves to Age; F2 edits it; Escape keeps the old value.
        Key(grid, Atelier.Core.Events.Key.Right);
        Assert.Equal("age", grid.CurrentColumn!.EffectiveKey);
        Key(grid, Atelier.Core.Events.Key.F2);
        Assert.Equal("age", grid.EditContext!.Column.EffectiveKey);
        ((TextBox)grid.GetRow(1)!.Editor!).Text = "99";
        Key(grid, Atelier.Core.Events.Key.Escape);
        Assert.Equal(30, people[1].Age);
        Assert.Equal(new[] { true, false }, ended);

        // F2 on a read-only cell edits the next editable one.
        Key(grid, Atelier.Core.Events.Key.Right);
        Assert.Equal("city", grid.CurrentColumn!.EffectiveKey);
        Key(grid, Atelier.Core.Events.Key.F2);
        Assert.Equal("name", grid.EditContext!.Column.EffectiveKey);
        grid.CancelEdit();
    }

    [Fact]
    public void InvalidValues_KeepTheEditorOpen_WithTheMessage()
    {
        var people = People();
        var grid = EditableGrid(people);
        grid.CellEditEnding += (_, e) =>
        {
            if (e.Column.EffectiveKey == "age" && (int)e.Value! > 150) e.Error = "Too old";
            if (e.Column.EffectiveKey == "name") e.Value = ((string)e.Value!).Trim();
        };

        Assert.True(grid.BeginEdit(people[0], grid.Columns[1]));
        var row = grid.GetRow(0)!;
        ((TextBox)row.Editor!).Text = "abc";
        Assert.False(grid.CommitEdit());
        Assert.True(grid.IsEditing);
        Assert.NotNull(row.EditError);

        ((TextBox)row.Editor!).Text = "200";
        Assert.False(grid.CommitEdit());
        Assert.Equal("Too old", row.EditError);

        ((TextBox)row.Editor!).Text = "36";
        Assert.True(grid.CommitEdit());
        Assert.Equal(36, people[0].Age);
        Assert.Null(row.EditError);

        // CellEditEnding can change the value.
        grid.BeginEdit(people[0], grid.Columns[0]);
        ((TextBox)grid.GetRow(0)!.Editor!).Text = "  Caroline ";
        grid.CommitEdit();
        Assert.Equal("Caroline", people[0].Name);

        // Read-only columns and grids can't be edited.
        Assert.False(grid.BeginEdit(people[0], grid.Columns[2]));
        grid.IsReadOnly = true;
        Assert.False(grid.BeginEdit(people[0], grid.Columns[0]));
    }

    [Fact]
    public void EditorTemplate_EditsAnyType_ThroughTheContext()
    {
        var people = People();
        DataGridEditContext? seen = null;
        var ageColumn = new DataGridTextColumn<Person>("Age", p => p.Age, (p, v) => p.Age = (int)v!)
        {
            // A slider editor: it writes the context's value, and the value comes back as an int.
            EditorTemplate = context =>
            {
                seen = context;
                var slider = new Slider { Minimum = 0, Maximum = 120, Value = (int)context.Column.GetSortValue(context.Item)! };
                slider.ValueChanged += (_, v) => context.Value = (int)Math.Round(v);
                return slider;
            },
            Parser = null,
        };
        var grid = Layout(new DataGrid().ItemsSource(people).Columns(
            new DataGridTextColumn<Person>("Name", p => p.Name) { Width = GridLength.Pixels(200) },
            ageColumn));

        Assert.True(grid.BeginEdit(people[2], ageColumn));
        Assert.IsType<Slider>(grid.GetRow(2)!.Editor);
        Assert.Same(people[2], seen!.Item);

        ((Slider)seen.Editor!).Value = 64.2f;
        Assert.True(seen.Commit());
        Assert.Equal(64, people[2].Age);
        Assert.False(grid.IsEditing);
    }

    [Fact]
    public void ClickingAnotherRow_CommitsTheEdit_AndSetsTheCurrentCell()
    {
        var people = People();
        var grid = EditableGrid(people);
        grid.BeginEdit(people[0], grid.Columns[0]);
        ((TextBox)grid.GetRow(0)!.Editor!).Text = "Carla";

        // A click on the Age cell of another row.
        var row = grid.GetRow(3)!;
        var p = row.PointToScreen(new Point(250, row.Bounds.Height / 2));
        row.OnPointerPressed(At(p));
        row.OnPointerReleased(At(p));

        Assert.False(grid.IsEditing);
        Assert.Equal("Carla", people[0].Name);
        Assert.Equal("age", grid.CurrentColumn!.EffectiveKey);
        Assert.Equal(3, grid.CurrentIndex);
        Assert.Equal(new Rect(200, 0, 100, grid.RowHeight), row.CurrentCellBounds);

        // Sorting (which rebuilds the rows) commits too.
        grid.BeginEdit(people[3], grid.Columns[0]);
        ((TextBox)grid.GetRow(3)!.Editor!).Text = "Dan";
        grid.SortBy(grid.Columns[0], DataGridSortDirection.Ascending);
        Assert.False(grid.IsEditing);
        Assert.Equal("Dan", people[3].Name);
    }

    [Fact]
    public void CompactComboBox_FitsARow()
    {
        var combo = new ComboBox { IsCompact = true, ItemsSource = new[] { "Me", "Team" } };
        combo.Measure(new Size(120, 100));
        Assert.Equal(ComboBox.CompactHeight, combo.DesiredSize.Height);
        Assert.True(combo.DesiredSize.Width <= 120);

        combo.IsCompact = false;
        combo.Measure(new Size(120, 100));
        Assert.True(combo.DesiredSize.Height >= 40);
    }

    private static DataGrid CellGrid(IEnumerable<Person> items, DataGridSelectionMode mode = DataGridSelectionMode.Extended)
    {
        var grid = Grid(items, mode);
        grid.SelectionUnit = DataGridSelectionUnit.Cell;
        return Layout(grid);
    }

    // Clicks the cell of a column (0-based among the visible columns) in a row.
    private static void ClickCell(DataGrid grid, int index, int column, ModifierKeys modifiers = ModifierKeys.None)
    {
        var row = grid.GetRow(index)!;
        float x = 0;
        for (int i = 0; i < column; i++) x += grid.VisibleColumns[i].ActualWidth;
        var p = row.PointToScreen(new Point(x + grid.VisibleColumns[column].ActualWidth / 2, row.Bounds.Height / 2));
        row.OnPointerPressed(At(p, modifiers: modifiers));
        row.OnPointerReleased(At(p, modifiers: modifiers));
    }

    private static string[] SelectedCellTexts(DataGrid grid) => grid.SelectedCells.Select(c => c.Text).ToArray();

    [Fact]
    public void CellUnit_ClickSelectsACell_AndSelectedItemsHoldItsRow()
    {
        var grid = CellGrid(People(), DataGridSelectionMode.Single);
        ClickCell(grid, 1, 2); // alice, City

        Assert.Equal(new[] { "Zurich" }, SelectedCellTexts(grid));
        Assert.Same(grid.View[1], grid.SelectedItem);
        Assert.Equal(new DataGridCellInfo(grid.View[1], grid.Columns[2]), grid.CurrentCell);
        Assert.False(grid.GetRow(1)!.IsSelected); // no row highlight, just the cell
        Assert.True(grid.IsCellSelected(grid.View[1], grid.Columns[2]));
        Assert.Equal(new Rect(300, 0, 150, grid.RowHeight), grid.GetRow(1)!.CurrentCellBounds);

        ClickCell(grid, 3, 0, ModifierKeys.Control); // single: Ctrl doesn't add
        Assert.Equal(new[] { "Dave" }, SelectedCellTexts(grid));
    }

    [Fact]
    public void CellUnit_Extended_CtrlTogglesCells_ShiftSelectsARectangle()
    {
        var grid = CellGrid(People());
        ClickCell(grid, 0, 0);
        ClickCell(grid, 2, 1, ModifierKeys.Shift);
        Assert.Equal(6, grid.SelectedCells.Count); // rows 0-2 x columns 0-1
        Assert.Equal(3, grid.SelectedItems.Count);
        Assert.False(grid.IsCellSelected(grid.View[0], grid.Columns[2]));

        ClickCell(grid, 1, 0, ModifierKeys.Control);
        Assert.Equal(5, grid.SelectedCells.Count);
        ClickCell(grid, 4, 2, ModifierKeys.Control);
        Assert.Equal(6, grid.SelectedCells.Count);
        Assert.Equal(4, grid.SelectedItems.Count);

        grid.OnKeyDown(new KeyEventArgs(Atelier.Core.Events.Key.A, 0, ModifierKeys.Control, true));
        Assert.Equal(15, grid.SelectedCells.Count);
    }

    [Fact]
    public void CellUnit_ArrowsMoveTheCell_ShiftExtends_HomeEndGoToTheRowEnds()
    {
        var grid = CellGrid(People());
        ClickCell(grid, 0, 0);

        Key(grid, Atelier.Core.Events.Key.Right);
        Key(grid, Atelier.Core.Events.Key.Down);
        Assert.Equal(new[] { "30" }, SelectedCellTexts(grid)); // alice's age
        Assert.Equal(1, grid.CurrentIndex);

        Key(grid, Atelier.Core.Events.Key.Right, ModifierKeys.Shift);
        Key(grid, Atelier.Core.Events.Key.Down, ModifierKeys.Shift);
        Assert.Equal(4, grid.SelectedCells.Count); // rows 1-2 x Age, City

        Key(grid, Atelier.Core.Events.Key.Home);
        Assert.Equal("name", grid.CurrentColumn!.EffectiveKey);
        Assert.Equal(new[] { "Bob" }, SelectedCellTexts(grid));
        Key(grid, Atelier.Core.Events.Key.End, ModifierKeys.Control);
        Assert.Equal(4, grid.CurrentIndex);
        Assert.Equal("city", grid.CurrentColumn!.EffectiveKey);

        // Ctrl moves without selecting; Space toggles.
        Key(grid, Atelier.Core.Events.Key.Up, ModifierKeys.Control);
        Key(grid, Atelier.Core.Events.Key.Space);
        Assert.Equal(2, grid.SelectedCells.Count);
    }

    [Fact]
    public void CellUnit_Multiple_ClicksToggleCells_WithoutCheckBoxes()
    {
        var grid = CellGrid(People(), DataGridSelectionMode.Multiple);
        Assert.Empty(grid.GetRow(0)!.Children.OfType<CheckBox>());
        Assert.Equal(0, grid.Headers[0].PointToScreen(Point.Zero).X - grid.PointToScreen(Point.Zero).X, 1);

        ClickCell(grid, 0, 0);
        ClickCell(grid, 1, 1);
        Assert.Equal(2, grid.SelectedCells.Count);
        ClickCell(grid, 0, 0);
        Assert.Equal(new[] { "30" }, SelectedCellTexts(grid));
    }

    [Fact]
    public void CellUnit_SelectionFollowsFiltersSortingAndHiddenColumns()
    {
        var grid = CellGrid(People());
        ClickCell(grid, 0, 0);
        ClickCell(grid, 4, 2, ModifierKeys.Shift); // everything
        Assert.Equal(15, grid.SelectedCells.Count);

        grid.SortBy(grid.Columns[1], DataGridSortDirection.Ascending);
        Assert.Equal(15, grid.SelectedCells.Count); // cells are items and columns, not positions

        grid.Filter = o => ((Person)o).Age > 25;
        Assert.Equal(9, grid.SelectedCells.Count);
        grid.Columns[2].IsVisible = false;
        Assert.Equal(6, grid.SelectedCells.Count);

        grid.SelectionUnit = DataGridSelectionUnit.Row;
        Assert.Empty(grid.SelectedCells);
        Assert.Empty(grid.SelectedItems);
    }

    [Fact]
    public void Copy_GivesTabSeparatedRows_OrTheSelectedCells()
    {
        var grid = Grid(People(), DataGridSelectionMode.Extended);
        Click(grid, 0);
        Click(grid, 2, ModifierKeys.Control);
        Assert.Equal("Carol\t35\tBern\r\nBob\t25\tBasel\r\n", grid.GetClipboardText());

        grid.ClipboardCopyMode = DataGridClipboardCopyMode.IncludeHeader;
        Assert.StartsWith("Name\tAge\tCity\r\n", grid.GetClipboardText());

        grid.ClipboardCopyMode = DataGridClipboardCopyMode.ExcludeHeader;
        grid.SelectionUnit = DataGridSelectionUnit.Cell;
        Layout(grid);
        ClickCell(grid, 0, 0);
        ClickCell(grid, 1, 1, ModifierKeys.Shift);
        Assert.Equal("Carol\t35\r\nalice\t30\r\n", grid.GetClipboardText());

        // Scattered cells: the rows and columns they span, with empty gaps.
        ClickCell(grid, 0, 0);
        ClickCell(grid, 2, 2, ModifierKeys.Control);
        Assert.Equal("Carol\t\r\n\tBasel\r\n", grid.GetClipboardText());
    }

    [Fact]
    public void CellUnit_TypingStartsAnEditWithTheTypedText()
    {
        var people = People();
        var grid = EditableGrid(people);
        grid.SelectionUnit = DataGridSelectionUnit.Cell;
        Layout(grid);
        ClickCell(grid, 0, 0);

        grid.OnTextInput(new TextInputEventArgs("K"));
        Assert.True(grid.IsEditing);
        Assert.Equal("K", ((TextBox)grid.GetRow(0)!.Editor!).Text);
        grid.CommitEdit();
        Assert.Equal("K", people[0].Name);
    }

    [Fact]
    public void CustomEditors_GetTheTypedTextThatStartedTheEdit()
    {
        var people = People();
        string? typed = "unset";
        var age = new DataGridTextColumn<Person>("Age", p => p.Age, (p, v) => p.Age = (int)v!)
        {
            EditorTemplate = context =>
            {
                typed = context.InitialText;
                if (context.InitialText is [var d] && char.IsDigit(d)) context.Value = (d - '0') * 10;
                return new Border { IsFocusable = true };
            },
        };
        var grid = Layout(new DataGrid().SelectionUnit(DataGridSelectionUnit.Cell).ItemsSource(people).Columns(
            new DataGridTextColumn<Person>("Name", p => p.Name) { Width = GridLength.Pixels(200) }, age));

        grid.SelectCell(people[1], age);
        grid.OnTextInput(new TextInputEventArgs("4"));
        Assert.Equal("4", typed);
        Assert.Equal(40, grid.EditContext!.Value);
        Assert.True(grid.CommitEdit());
        Assert.Equal(40, people[1].Age);

        grid.OnKeyDown(new KeyEventArgs(Atelier.Core.Events.Key.F2));
        Assert.Null(typed); // F2: no typed text
        Assert.Equal(40, grid.EditContext!.OriginalValue); // custom editors get the value itself
        grid.CancelEdit();
    }
}
