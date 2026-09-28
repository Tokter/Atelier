using System;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class DataGridView : GalleryPage
{
    private readonly DataGridViewModel _vm;
    private readonly DataGrid _files;
    private DataGridLayout? _defaultLayout;
    private DataGrid? _ratingGrid;

    public DataGridView(DataGridViewModel viewModel)
        : base(MaterialIconKind.TableRows, "Data Grid",
            "A table of items with sortable, resizable and movable columns, filter menus, cell editing, a column chooser and single, check box " +
            "or extended selection. Rows are virtualized, so only the rows in view exist.")
    {
        _vm = viewModel;
        _files = FileGrid();

        Settings(
            Ui.Labeled("Selection", new ComboBox()
                .Items("Single", "Multiple (check boxes)", "Extended (Ctrl/Shift)")
                .BindSelectedIndex(_vm, v => v.SelectionModeIndex, (v, i) => v.SelectionModeIndex = i)),
            Ui.Labeled("Select", new ComboBox()
                .Items("Rows", "Cells")
                .BindSelectedIndex(_vm, v => v.SelectionUnitIndex, (v, i) => v.SelectionUnitIndex = i)),
            new Switch("Filter row").ShowThumbIcon().BindIsChecked(_vm, v => v.ShowFilterRow, (v, on) => v.ShowFilterRow = on));

        Sections(FilesSection(), FilterSection(), EditingSection(), RatingSection(), LayoutSection(), SmallSection());
    }

    private DataGrid FileGrid()
    {
        var grid = new DataGrid()
            .Height(460)
            .ItemsSource(_vm.Files)
            .Bind(DataGrid.SelectionModeProperty, _vm, v => v.SelectionMode)
            .Bind(DataGrid.SelectionUnitProperty, _vm, v => v.SelectionUnit)
            .Bind(DataGrid.ShowFilterRowProperty, _vm, v => v.ShowFilterRow)
            .RowContextMenu(FileMenu())
            .OnRowActivated((_, item) => _vm.Status = $"Opened {((FileEntry)item).Name}")
            .OnSelectionChanged((s, _) => _vm.Status = ((DataGrid)s!).SelectionUnit == DataGridSelectionUnit.Cell
                ? CellStatus((DataGrid)s!)
                : ((DataGrid)s!).SelectedItems.Count switch
            {
                0 => "Nothing selected",
                1 => $"Selected {((FileEntry)((DataGrid)s!).SelectedItems[0]).Name}",
                int n => $"{n:N0} files selected",
            })
            .OnFilterChanged((s, _) =>
            {
                var g = (DataGrid)s!;
                _vm.Status = g.HasColumnFilters ? $"Showing {g.View.Count:N0} of {g.SourceCount:N0} files" : "Filters cleared";
            })
            .OnCellEditEnding((_, e) =>
            {
                // Validation: a file name can't be empty or contain path characters.
                if (e.Column.EffectiveKey == "name" && e.Value is string name)
                {
                    name = name.Trim();
                    if (name.Length == 0) e.Error = "Enter a file name";
                    else if (name.IndexOfAny(['\\', '/', ':', '*', '?', '"', '<', '>', '|']) >= 0) e.Error = "A file name can't contain \\ / : * ? \" < > |";
                    e.Value = name;
                }
            })
            .Columns(
                new DataGridTemplateColumn<FileEntry>("Name", f => new StackPanel().Orientation(Orientation.Horizontal).Spacing(12).Children(
                    new Icon(f.Icon, 20).Themed(Control.ForegroundProperty, c => c.OnSurfaceVariant),
                    new TextBlock().BindText(f, x => x.Name).VerticalAlignment(VerticalAlignment.Center)))
                {
                    Key = "name",
                    Width = GridLength.Stars(1),
                    MinWidth = 200,
                    CanHide = false,
                    SortKey = f => ((FileEntry)f).Name,
                    Text = f => ((FileEntry)f).Name,
                    // F2 renames: a template column edits with any editor, here the text columns' text box.
                    ValueSetter = (f, v) => ((FileEntry)f).Name = (string)v!,
                    EditorTemplate = DataGridTextColumn.CreateTextBoxEditor,
                },
                new DataGridTextColumn<FileEntry>("Type", f => f.Type) { Key = "type", Width = GridLength.Pixels(160) },
                new DataGridTextColumn<FileEntry>("Size", f => f.Size)
                {
                    Key = "size",
                    Width = GridLength.Pixels(100),
                    CellAlignment = HorizontalAlignment.Right,
                    TextConverter = v => v is long bytes ? FileEntry.FormatSize(bytes) : "",
                },
                new DataGridTextColumn<FileEntry>("Modified", f => f.Modified, (f, v) => f.Modified = (DateTime)v!)
                {
                    Key = "modified",
                    Format = "yyyy-MM-dd HH:mm",
                    Width = GridLength.Pixels(170),
                    EditorTemplate = DateEditor,
                },
                new DataGridTextColumn<FileEntry>("Created", f => f.Created) { Key = "created", Format = "yyyy-MM-dd HH:mm", Width = GridLength.Pixels(150), IsVisible = false },
                new DataGridTextColumn<FileEntry>("Folder", f => f.Folder)
                {
                    Key = "folder",
                    Width = GridLength.Auto,
                    IsVisible = false,
                    HeaderTemplate = c => new StackPanel().Orientation(Orientation.Horizontal).Spacing(6).Children(
                        new Icon(MaterialIconKind.Folder, 18),
                        new TextBlock(c.Header?.ToString() ?? "").VerticalAlignment(VerticalAlignment.Center)),
                },
                new DataGridTextColumn<FileEntry>("Owner", f => f.Owner, (f, v) => f.Owner = (string)v!)
                {
                    Key = "owner",
                    Width = GridLength.Pixels(160),
                    EditorTemplate = OwnerEditor,
                },
                new DataGridTextColumn<FileEntry>("Attributes", f => f.Attributes) { Key = "attributes", Width = GridLength.Pixels(110), IsVisible = false },
                OptionalRatingColumn(),
                new DataGridCheckBoxColumn("Read-only", f => ((FileEntry)f).IsReadOnly, (f, on) => ((FileEntry)f).IsReadOnly = on)
                {
                    Key = "readonly",
                    Width = GridLength.Pixels(110),
                    CellAlignment = HorizontalAlignment.Center,
                });
        _defaultLayout = grid.SaveLayout();
        return grid;
    }

    #region Star rating: a custom cell and a custom editor

    // A rating column: the cells show stars, and EditorTemplate supplies the editor (F2, or type 0-5 with cell
    // selection). Sorting and the filter menu use the number; Text gives the filter menu and Ctrl+C readable values.
    private static DataGridTemplateColumn<FileEntry> RatingColumn() => new("Rating", f => Stars(f.Rating, 18))
    {
        Key = "rating",
        Width = GridLength.Pixels(150),
        SortKey = f => ((FileEntry)f).Rating,
        Text = f => ((FileEntry)f).Rating is 0 ? "Not rated" : $"{((FileEntry)f).Rating} of 5",
        ValueSetter = (f, v) => ((FileEntry)f).Rating = (int)v!,
        EditorTemplate = RatingEditor,
    };

    // In the file grid, an optional column: shown with the column chooser.
    private static DataGridColumn OptionalRatingColumn()
    {
        var column = RatingColumn();
        column.IsVisible = false;
        return column;
    }

    // Five stars, the first `rating` filled.
    private static StackPanel Stars(int rating, float size)
    {
        var row = new StackPanel().Orientation(Orientation.Horizontal).Spacing(2).VerticalAlignment(VerticalAlignment.Center);
        for (int i = 0; i < 5; i++) row.Add(Star(i < rating, size));
        return row;
    }

    private static Icon Star(bool filled, float size) =>
        new Icon(MaterialIconKind.Star, size)
            .IsFilled(filled)
            .Themed(Control.ForegroundProperty, c => filled ? c.Tertiary : c.OutlineVariant);

    // The editor: hover previews a rating, a click picks it and ends the edit. Keys: Left/Right (or - and numpad +) change it,
    // 0-5 set it, Enter stores it (the grid handles Enter the editor leaves), Escape cancels.
    private static UIElement RatingEditor(DataGridEditContext context)
    {
        int rating = context.OriginalValue is int value ? value : 0;
        if (context.InitialText is [var typed] && typed is >= '0' and <= '5') rating = typed - '0'; // typed on the cell
        context.Value = rating;

        var stars = new Icon[5];
        var row = new StackPanel().Orientation(Orientation.Horizontal).Spacing(2).VerticalAlignment(VerticalAlignment.Center);
        var editor = new Border()
            .Padding(6, 3)
            .CornerRadius(4)
            .BorderThickness(2)
            .HorizontalAlignment(HorizontalAlignment.Left)
            .Themed(Border.BorderBrushProperty, c => c.Primary) // the editor draws its own outline, like a text box
            .ToolTip("Click a star, or use ← → and 0–5; Enter stores, Esc cancels")
            .Child(row);
        editor.IsFocusable = true; // to get the keys

        void Show(int count)
        {
            for (int i = 0; i < stars.Length; i++)
            {
                bool filled = i < count; // a copy: the theme callback runs later
                stars[i].IsFilled(filled).Themed(Control.ForegroundProperty, c => filled ? c.Tertiary : c.OutlineVariant);
            }
        }

        void Set(int count)
        {
            rating = Math.Clamp(count, 0, 5);
            context.Value = rating;
            Show(rating);
        }

        for (int i = 0; i < stars.Length; i++)
        {
            int starValue = i + 1;
            stars[i] = new Icon(MaterialIconKind.Star, 22)
                .OnPointerEntered((_, _) => Show(starValue))
                .OnPointerExited((_, _) => Show(rating))
                .OnPointerPressed((_, e) =>
                {
                    // Clicking the current rating clears it, like many rating controls.
                    Set(starValue == rating ? 0 : starValue);
                    context.Commit();
                    e.Handled = true;
                });
            row.Add(stars[i]);
        }

        editor.OnKeyDown((_, e) =>
        {
            int? next = e.Key switch
            {
                Key.Left or Key.Minus or Key.NumPadSubtract => rating - 1,
                Key.Right or Key.Equal or Key.NumPadAdd => rating + 1,
                >= Key.D0 and <= Key.D5 => e.Key - Key.D0,
                >= Key.NumPad0 and <= Key.NumPad5 => e.Key - Key.NumPad0,
                _ => null,
            };
            if (next is { } n)
            {
                Set(n);
                e.Handled = true;
            }
        });

        Show(rating);
        return editor;
    }

    private UIElement RatingSection() => Ui.Section("Custom editor: star rating",
        "A template column shows each rating as stars, and its EditorTemplate builds the editor, as the property grid's " +
        "rating does. Select a rating and press F2, then click a star (hover previews it) or use ← → and 0–5 and Enter. " +
        "With cell selection (this grid) you can also just type 0–5 on a rating cell: the editor starts with the typed digit. " +
        "The same files are in the grid above (Rating is an optional column there), so ratings show in both.",
        _ratingGrid = new DataGrid()
            .Height(380)
            .SelectionUnit(DataGridSelectionUnit.Cell)
            .ItemsSource(_vm.RatedFiles)
            .OnCellEditEnding((_, e) => _vm.Status = $"Rated {((FileEntry)e.Item).Name}: {e.Value} of 5")
            .Columns(
                new DataGridTextColumn<FileEntry>("Name", f => f.Name) { Width = GridLength.Stars(1) },
                new DataGridTextColumn<FileEntry>("Type", f => f.Type) { Width = GridLength.Pixels(160) },
                RatingColumn()),
        Ui.Readout(_vm, v => v.Status),
        Ui.Code(
            "new DataGridTemplateColumn<FileEntry>(\"Rating\", f => Stars(f.Rating))\n" +
            "{\n" +
            "    SortKey = f => ((FileEntry)f).Rating,\n" +
            "    ValueSetter = (f, v) => ((FileEntry)f).Rating = (int)v!,\n" +
            "    EditorTemplate = context =>\n" +
            "    {\n" +
            "        // Read context.OriginalValue (and InitialText when typing started the edit),\n" +
            "        // write context.Value as the user changes it, and call context.Commit() to store it.\n" +
            "        var editor = new RatingStars((int)context.OriginalValue!);\n" +
            "        editor.Picked += rating => { context.Value = rating; context.Commit(); };\n" +
            "        return editor;\n" +
            "    },\n" +
            "}"));

    #endregion

    private static string CellStatus(DataGrid grid) => grid.SelectedCells.Count switch
    {
        0 => "Nothing selected",
        1 => $"Selected \"{grid.SelectedCells[0].Text}\" ({grid.SelectedCells[0].Column.Header})",
        int n => $"{n:N0} cells in {grid.SelectedItems.Count:N0} rows selected (Ctrl+C copies them)",
    };

    // A custom editor for dates: a docked date picker. Picking a date stores it (keeping the time) and ends the edit.
    private static UIElement DateEditor(DataGridEditContext context)
    {
        var original = (DateTime)context.OriginalValue!;
        var picker = new DatePicker { SelectedDate = DateOnly.FromDateTime(original), IsCompact = true };
        picker.SelectedDateChanged += (_, date) =>
        {
            if (date is not { } d) return;
            context.Value = d.ToDateTime(TimeOnly.FromDateTime(original));
            context.Commit();
        };
        return picker;
    }

    // A custom editor for the owner: a combo box that opens right away; choosing an owner ends the edit.
    private static UIElement OwnerEditor(DataGridEditContext context)
    {
        var combo = new ComboBox().IsCompact().ItemsSource(DataGridViewModel.Owners);
        combo.SelectedItem = context.OriginalValue;
        combo.SelectionChanged += (_, owner) => context.Value = owner;
        bool opened = false;
        combo.GotFocus += (_, _) =>
        {
            if (opened) return;
            opened = true;
            combo.IsDropDownOpen = true;
        };
        combo.DropDownClosed += (_, _) => context.Commit();
        return combo;
    }

    private ContextMenu FileMenu()
    {
        var menu = new ContextMenu();
        menu.Items(
            new MenuItem("Open").Icon(MaterialIconKind.OpenInNew).OnClick(() => _vm.Status = $"Opened {Target(menu)?.Name}"),
            new MenuItem("Rename").Icon(MaterialIconKind.DriveFileRenameOutline).InputGestureText("F2").OnClick(() =>
            {
                if (Target(menu) is { } file && _files.FindColumn("name") is { } name) _files.BeginEdit(file, name);
            }),
            new MenuItem("Copy path").Icon(MaterialIconKind.ContentCopy).OnClick(() => _vm.Status = $@"Copied {Target(menu)?.Folder}\{Target(menu)?.Name}"),
            new Separator(),
            new MenuItem("Delete").Icon(MaterialIconKind.Delete).OnClick(() =>
            {
                if (Target(menu) is { } file) _vm.Delete(file);
            }));
        return menu;
    }

    private static FileEntry? Target(ContextMenu menu) => menu.DataContext as FileEntry;

    private UIElement FilesSection() => Ui.Section("10,000 files",
        "Click a header to sort, Shift+click to add a second sort column. Drag a header to move the column, drag its right " +
        "edge to resize it, double-click the edge to fit. Right-click the headers for the column chooser: Created, Folder, " +
        "Attributes and Rating are hidden to start with. Right-click a row (or press the menu key) for its menu; double-click " +
        "or Enter opens it. Set Select to Cells above to select cells like a spreadsheet: click, Ctrl+click, Shift+click or " +
        "Shift+arrows for a rectangle, Home/End and Ctrl+Home/End, and type to start editing. Ctrl+C copies the selected rows " +
        "or cells as tab-separated text that pastes into a spreadsheet.",
        _files,
        Ui.Readout(_vm, v => v.Status),
        Ui.Code("new DataGrid().ItemsSource(vm.Files).Columns(\n" +
                "    new DataGridTextColumn<FileEntry>(\"Type\", f => f.Type) { Width = GridLength.Pixels(160) },\n" +
                "    new DataGridTextColumn<FileEntry>(\"Size\", f => f.Size) { TextConverter = ..., CellAlignment = Right },\n" +
                "    new DataGridTextColumn<FileEntry>(\"Created\", f => f.Created) { Format = \"yyyy-MM-dd\", IsVisible = false })"));

    private UIElement FilterSection() => Ui.Section("Filter menus",
        "Hover a header and click its filter button (or turn on the filter row above): sort, clear the column's filter, or " +
        "pick the values to show. The list holds the values the other filters leave, with their counts; search it, then OK " +
        "keeps the checked matches. A filtered column's button stays visible in the primary color.",
        Ui.Row(
            Ui.IconButton(MaterialIconKind.FilterAltOff, "Clear all filters", ButtonVariant.Tonal).OnClick(() => _files.ClearFilters()),
            Ui.IconButton(MaterialIconKind.FilterList, "Filter by type…", ButtonVariant.Outlined).OnClick(() =>
            {
                if (_files.FindColumn("type") is { } type) _files.OpenFilterMenu(type);
            })),
        Ui.Code("column.FilterValues = [\"PNG image\", \"JPEG image\"];   // what OK in the menu sets\n" +
                "grid.OnFilterChanged(...)   // e.g. \"Showing 1,331 of 10,000 files\""));

    private UIElement EditingSection() => Ui.Section("Editing",
        "Select a row and press F2 to edit the current cell (Left and Right move it; click a cell to choose it). Enter or a " +
        "click elsewhere stores the value, Escape cancels. Name uses the text box editor with validation in CellEditEnding " +
        "(try an empty name or a \"/\"), Modified a date picker and Owner a combo box, both custom editors from EditorTemplate. " +
        "Rename in the row menu starts an edit too.",
        Ui.Code("new DataGridTextColumn<FileEntry>(\"Owner\", f => f.Owner, (f, v) => f.Owner = (string)v!)\n" +
                "{\n" +
                "    EditorTemplate = context => new ComboBox().ItemsSource(owners)\n" +
                "        // the editor writes context.Value, and may end the edit with context.Commit()\n" +
                "}"));

    private UIElement LayoutSection() => Ui.Section("Column layout",
        "The column order, widths, visibility and sort are the layout. Save it (here as JSON, e.g. for app settings) and " +
        "restore it later; LayoutChanged tells when the user changed it. The chooser can also open from a button.",
        Ui.Row(
            Ui.IconButton(MaterialIconKind.ViewColumn, "Columns…", ButtonVariant.Outlined).OnClick((s, _) =>
                _files.CreateColumnChooserMenu().Open((UIElement)s!, byKeyboard: true)),
            Ui.IconButton(MaterialIconKind.Save, "Save layout", ButtonVariant.Tonal).OnClick(() =>
                _vm.SavedLayout = _files.SaveLayout().ToJson()),
            Ui.IconButton(MaterialIconKind.Restore, "Restore layout", ButtonVariant.Tonal).OnClick(() =>
            {
                if (_vm.SavedLayout.Length > 0) _files.RestoreLayout(DataGridLayout.FromJson(_vm.SavedLayout));
            }),
            Ui.IconButton(MaterialIconKind.RestartAlt, "Default layout", ButtonVariant.Text).OnClick(() =>
            {
                if (_defaultLayout != null) _files.RestoreLayout(_defaultLayout);
            })),
        Ui.Readout(_vm, v => v.SavedLayout.Length > 0 ? v.SavedLayout : "(no saved layout)"));

    private UIElement SmallSection() => Ui.Section("Check box selection and empty state",
        "In Multiple mode each row has a check box and the header box selects all. When there are no items the grid shows " +
        "EmptyContent; when the filters hide everything it shows NoMatchesContent.",
        new DataGrid()
            .Height(300)
            .SelectionMode(DataGridSelectionMode.Multiple)
            .ItemsSource(_vm.SmallList)
            .EmptyContent(new StackPanel().Spacing(8).Children(
                new Icon(MaterialIconKind.FolderOpen, 40).Themed(Control.ForegroundProperty, c => c.OnSurfaceVariant).HorizontalAlignment(HorizontalAlignment.Center),
                new TextBlock("This folder is empty").Muted().HorizontalAlignment(HorizontalAlignment.Center)))
            .Columns(
                new DataGridTextColumn<FileEntry>("Name", f => f.Name) { Width = GridLength.Stars(1) },
                new DataGridTextColumn<FileEntry>("Type", f => f.Type) { Width = GridLength.Pixels(160) },
                new DataGridTextColumn<FileEntry>("Size", f => f.Size)
                {
                    Width = GridLength.Pixels(100),
                    CellAlignment = HorizontalAlignment.Right,
                    TextConverter = v => v is long bytes ? FileEntry.FormatSize(bytes) : "",
                }),
        Ui.Row(
            new Button().Variant(ButtonVariant.Tonal).Command(_vm.AddFileCommand),
            new Button().Variant(ButtonVariant.Outlined).Command(_vm.ClearFilesCommand)));
}
