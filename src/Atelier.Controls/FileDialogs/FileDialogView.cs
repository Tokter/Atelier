using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// The dialog shown by <see cref="OpenFileDialog"/>, <see cref="SaveFileDialog"/> and <see cref="FolderBrowserDialog"/>:
/// a path bar, a folder tree, the folder's items in a <see cref="DataGrid"/>, a name box, the file types and the
/// buttons. Create it through those classes.
/// </summary>
/// <remarks>
/// Keys: Enter in the name box accepts; in the list, Enter or a double-click opens a folder or accepts a file.
/// Backspace (in the list) or Alt+Up go up, Alt+Left and Alt+Right back and forward, F5 refreshes, Ctrl+L (or Alt+D)
/// edits the path, Ctrl+Shift+N makes a new folder (where offered), F2 renames, Escape cancels.
/// </remarks>
public class FileDialogView : Dialog
{
    /// <summary>The size the dialog takes when the window is large enough.</summary>
    public static readonly Size PreferredSize = new(1000, 680);

    private readonly CommonItemDialog _owner;
    private readonly IFileSystemProvider _fs;
    private readonly Grid _layout = new();
    private readonly DataGridColumn _nameColumn;
    private readonly List<string> _back = [];
    private readonly List<string> _forward = [];
    private readonly List<FolderNode> _treeRoots = [];
    private List<FileSystemEntry> _entries = [];
    private string? _namePattern;
    private bool _isSyncingTree;
    private bool _isAccepting;
    private bool _isUpdatingName;
    private string? _renamedPath;

    internal FileDialogView(CommonItemDialog owner, string startDirectory)
    {
        _owner = owner;
        _fs = owner.FileSystem;
        Padding = new Thickness(20);
        MaxWidth = float.PositiveInfinity;
        CornerRadius = new CornerRadius(16);

        // Title (and the folder dialog's description).
        var title = new TextBlock { Text = owner.EffectiveTitle, FontSize = 22, Margin = new Thickness(4, 0, 4, 12) };
        var header = new StackPanel();
        header.Add(title);
        if (owner is FolderBrowserDialog { Description: { Length: > 0 } description })
        {
            header.Add(new TextBlock { Text = description, Muted = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, -6, 4, 12) });
        }

        // Toolbar: navigation, path, search, new folder.
        BackButton = ToolButton(MaterialIconKind.ArrowBack, "Back (Alt+Left)", GoBack);
        ForwardButton = ToolButton(MaterialIconKind.ArrowForward, "Forward (Alt+Right)", GoForward);
        UpButton = ToolButton(MaterialIconKind.ArrowUpward, "Up (Alt+Up)", GoUp);
        RefreshButton = ToolButton(MaterialIconKind.Refresh, "Refresh (F5)", Refresh);
        NewFolderButton = ToolButton(MaterialIconKind.CreateNewFolder, "New folder (Ctrl+Shift+N)", () => CreateNewFolder());
        NewFolderButton.Visibility = owner.ShowsNewFolderButton ? Visibility.Visible : Visibility.Collapsed;
        PathBar = new FileDialogPathBar(_fs, owner.RootDirectory == null ? null : _fs.GetFullPath(owner.RootDirectory));
        PathBar.PathRequested += (_, path) => NavigateTyped(path);
        SearchBox = new TextBox { Placeholder = "Search", LeadingIconKind = MaterialIconKind.Search, FieldHeight = 40, Width = 220, VerticalAlignment = VerticalAlignment.Center };
        SearchBox.TextChanged += (_, _) => ApplySearch();

        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        toolbar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        toolbar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        toolbar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var navigation = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        navigation.WithChildren(BackButton, ForwardButton, UpButton, RefreshButton);
        Place(toolbar, navigation, 0, 0);
        Place(toolbar, new Border { Child = PathBar, CornerRadius = new CornerRadius(20), Margin = new Thickness(8, 0), StyleKey = PathBarStyleKey, VerticalAlignment = VerticalAlignment.Center }, 0, 1);
        Place(toolbar, SearchBox, 0, 2);
        NewFolderButton.Margin = new Thickness(4, 0, 0, 0);
        Place(toolbar, NewFolderButton, 0, 3);

        // Folder tree.
        FolderTree = new TreeView
        {
            ChildrenSelector = item => ((FolderNode)item).Children,
            ItemTemplate = item => FolderRow((FolderNode)item),
            Margin = new Thickness(0, 0, 4, 0),
        };
        FolderTree.SelectionChanged += (_, item) =>
        {
            if (!_isSyncingTree && item is FolderNode node) Navigate(node.Path);
        };

        // The folder's items.
        _nameColumn = new DataGridTemplateColumn<FileSystemEntry>("Name", NameCell)
        {
            Key = "name",
            Width = GridLength.Stars(1),
            MinWidth = 200,
            CanHide = false,
            SortKey = e => new NameSortKey((FileSystemEntry)e),
            Text = e => ((FileSystemEntry)e).Name,
            ValueSetter = (e, v) => Rename((FileSystemEntry)e, v as string ?? string.Empty),
            EditorTemplate = DataGridTextColumn.CreateTextBoxEditor,
        };
        FileList = new DataGrid
        {
            SelectionMode = owner.AllowsMultipleSelection ? DataGridSelectionMode.Extended : DataGridSelectionMode.Single,
            EmptyContent = "This folder is empty.",
            NoMatchesContent = "No items match your search.",
            ShowFilterMenus = true,
        };
        FileList.Columns.Add(_nameColumn);
        foreach (var column in CreateColumns()) FileList.Columns.Add(column);
        FileList.SortBy(_nameColumn, DataGridSortDirection.Ascending);
        if ((owner.ColumnLayout ?? CommonItemDialog.SharedColumnLayout) is { } layout) FileList.RestoreLayout(layout);
        FileList.SelectionChanged += (_, _) => OnListSelectionChanged();
        FileList.RowActivated += (_, item) => OnRowActivated((FileSystemEntry)item);
        FileList.CellEditEnded += (_, committed) => OnRenameEnded(committed);

        var main = new Grid();
        main.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Pixels(240)));
        main.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        main.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Place(main, FolderTree, 0, 0);
        Place(main, new GridSplitter(), 0, 1);
        Place(main, FileList, 0, 2);

        // Name and type.
        NameBox = new TextBox { Label = owner.NameLabel, FieldHeight = 48, VerticalAlignment = VerticalAlignment.Center };
        NameBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                _ = AcceptAsync();
                e.Handled = true;
            }
        };
        TypeBox = new ComboBox { MinWidth = 240, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        if (owner is FileDialog { Filters.Count: > 0 } fileDialog)
        {
            foreach (var filter in fileDialog.Filters) TypeBox.Items.Add(filter.Description);
            TypeBox.SelectedIndex = Math.Clamp(fileDialog.FilterIndex - 1, 0, fileDialog.Filters.Count - 1);
            fileDialog.FilterIndex = TypeBox.SelectedIndex + 1;
            TypeBox.SelectionChanged += (_, _) => OnTypeChanged();
        }
        else
        {
            TypeBox.Visibility = Visibility.Collapsed;
        }
        var nameRow = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        nameRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        nameRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        Place(nameRow, NameBox, 0, 0);
        Place(nameRow, TypeBox, 0, 1);

        // Buttons.
        ReadOnlyBox = new CheckBox("Open as read-only")
        {
            IsChecked = owner is OpenFileDialog { ReadOnlyChecked: true },
            Visibility = owner is OpenFileDialog { ShowReadOnly: true } ? Visibility.Visible : Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
        };
        CancelButton = new Button("Cancel") { Variant = ButtonVariant.Text };
        CancelButton.Click += (_, _) => Close(DialogResult.Cancel);
        AcceptButton = new Button(owner.AcceptButtonText) { Variant = ButtonVariant.Filled };
        AcceptButton.Click += (_, _) => _ = AcceptAsync();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.WithChildren(CancelButton, AcceptButton);
        var actions = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        actions.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        actions.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        Place(actions, ReadOnlyBox, 0, 0);
        Place(actions, buttons, 0, 1);

        _layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _layout.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        _layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        _layout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Place(_layout, header, 0, 0);
        Place(_layout, toolbar, 1, 0);
        Place(_layout, main, 2, 0);
        Place(_layout, nameRow, 3, 0);
        Place(_layout, actions, 4, 0);
        Content = _layout;

        BuildTree();
        CurrentDirectory = startDirectory;
        LoadDirectory(startDirectory);
        if (owner is FileDialog { InitialName.Length: > 0 } start) SetName(start.InitialName);
        Opened += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    #region Parts

    /// <summary>Gets the dialog object that shows this view.</summary>
    public CommonItemDialog Owner => _owner;

    /// <summary>Gets the folder shown.</summary>
    public string CurrentDirectory { get; private set; }

    /// <summary>Gets the items shown (after the file type, hidden-items and name-pattern filters; before the search).</summary>
    public IReadOnlyList<FileSystemEntry> Entries => _entries;

    /// <summary>Gets the path bar.</summary>
    public FileDialogPathBar PathBar { get; }

    /// <summary>Gets the folder tree.</summary>
    public TreeView FolderTree { get; }

    /// <summary>Gets the list of the folder's items.</summary>
    public DataGrid FileList { get; }

    /// <summary>Gets the search box.</summary>
    public TextBox SearchBox { get; }

    /// <summary>Gets the name box.</summary>
    public TextBox NameBox { get; }

    /// <summary>Gets the file type box.</summary>
    public ComboBox TypeBox { get; }

    /// <summary>Gets the "Open as read-only" check box.</summary>
    public CheckBox ReadOnlyBox { get; }

    /// <summary>Gets the back button.</summary>
    public Button BackButton { get; }

    /// <summary>Gets the forward button.</summary>
    public Button ForwardButton { get; }

    /// <summary>Gets the up button.</summary>
    public Button UpButton { get; }

    /// <summary>Gets the refresh button.</summary>
    public Button RefreshButton { get; }

    /// <summary>Gets the new folder button.</summary>
    public Button NewFolderButton { get; }

    /// <summary>Gets the accept button ("Open", "Save" or "Select Folder").</summary>
    public Button AcceptButton { get; }

    /// <summary>Gets the cancel button.</summary>
    public Button CancelButton { get; }

    private static void Place(Grid grid, UIElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
        grid.Add(element);
    }

    private static Button ToolButton(MaterialIconKind icon, string toolTip, Action action)
    {
        var button = new Button
        {
            Variant = ButtonVariant.Text,
            Content = new Icon(icon, 22),
            Width = 40,
            Height = 40,
            MinWidth = 0,
            MinHeight = 0,
            Padding = Thickness.Zero,
            CornerRadius = new CornerRadius(20),
            VerticalAlignment = VerticalAlignment.Center,
            StyleKey = ToolButtonStyleKey,
        };
        ToolTipService.SetToolTip(button, toolTip);
        button.Click += (_, _) => action();
        return button;
    }

    /// <summary>Identifies the style of the toolbar's icon buttons.</summary>
    public const string ToolButtonStyleKey = "FileDialogToolButton";

    /// <summary>Identifies the style of the <see cref="Border"/> around the path bar.</summary>
    public const string PathBarStyleKey = "FileDialogPathBar";

    #endregion

    #region Columns

    // The columns besides Name; those after Size are in the column chooser.
    private static IEnumerable<DataGridColumn> CreateColumns()
    {
        yield return new DataGridTextColumn<FileSystemEntry>("Date modified", e => e.LastWriteTime) { Key = "modified", Format = "g", Width = GridLength.Pixels(160) };
        yield return new DataGridTextColumn<FileSystemEntry>("Type", e => e.TypeName) { Key = "type", Width = GridLength.Pixels(150) };
        yield return new DataGridTextColumn<FileSystemEntry>("Size", e => e.Length)
        {
            Key = "size",
            Width = GridLength.Pixels(100),
            CellAlignment = HorizontalAlignment.Right,
            TextConverter = v => v is long bytes ? FileTypes.FormatSize(bytes) : string.Empty,
            SortKey = e => ((FileSystemEntry)e).Length ?? -1,
        };
        yield return new DataGridTextColumn<FileSystemEntry>("Date created", e => e.CreationTime) { Key = "created", Format = "g", Width = GridLength.Pixels(160), IsVisible = false };
        yield return new DataGridTextColumn<FileSystemEntry>("Date accessed", e => e.LastAccessTime) { Key = "accessed", Format = "g", Width = GridLength.Pixels(160), IsVisible = false };
        yield return new DataGridTextColumn<FileSystemEntry>("Extension", e => e.Extension) { Key = "extension", Width = GridLength.Pixels(90), IsVisible = false };
        yield return new DataGridTextColumn<FileSystemEntry>("Attributes", e => e.AttributeLetters) { Key = "attributes", Width = GridLength.Pixels(100), IsVisible = false };
        yield return new DataGridCheckBoxColumn("Read-only", e => ((FileSystemEntry)e).IsReadOnly) { Key = "readonly", IsVisible = false, Width = GridLength.Pixels(100) };
        yield return new DataGridCheckBoxColumn("Hidden", e => ((FileSystemEntry)e).IsHidden) { Key = "hidden", IsVisible = false, Width = GridLength.Pixels(90) };
        yield return new DataGridTextColumn<FileSystemEntry>("Link target", e => e.LinkTarget) { Key = "link", Width = GridLength.Pixels(200), IsVisible = false };
        yield return new DataGridTextColumn<FileSystemEntry>("Full path", e => e.FullPath) { Key = "path", Width = GridLength.Pixels(320), IsVisible = false };
    }

    private static UIElement NameCell(FileSystemEntry entry)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var icon = new Icon(FileTypes.GetIcon(entry), 20) { VerticalAlignment = VerticalAlignment.Center, StyleKey = entry.IsDirectory ? FolderIconStyleKey : FileIconStyleKey };
        var text = new TextBlock { Text = entry.Name, VerticalAlignment = VerticalAlignment.Center, Muted = entry.IsHidden };
        row.Add(icon);
        row.Add(text);
        return row;
    }

    /// <summary>Identifies the style of folder icons (in the list and the tree).</summary>
    public const string FolderIconStyleKey = "FileDialogFolderIcon";

    /// <summary>Identifies the style of file icons.</summary>
    public const string FileIconStyleKey = "FileDialogFileIcon";

    // Folders before files, then by name (natural order is left to the comparer of names).
    private readonly record struct NameSortKey(bool IsDirectory, string Name) : IComparable<NameSortKey>, IComparable
    {
        public NameSortKey(FileSystemEntry entry) : this(entry.IsDirectory, entry.Name)
        {
        }

        public int CompareTo(NameSortKey other) =>
            IsDirectory != other.IsDirectory ? (IsDirectory ? -1 : 1) : string.Compare(Name, other.Name, StringComparison.CurrentCultureIgnoreCase);

        public int CompareTo(object? obj) => obj is NameSortKey other ? CompareTo(other) : 1;
    }

    #endregion

    #region Navigation

    /// <summary>Shows <paramref name="directory"/>; a folder that doesn't exist (or is outside the root folder) is ignored.</summary>
    /// <returns><c>true</c> if the folder is shown.</returns>
    public bool Navigate(string directory) => Navigate(directory, addToHistory: true);

    private bool Navigate(string directory, bool addToHistory)
    {
        string full;
        try
        {
            full = _fs.GetFullPath(directory);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return false;
        }
        if (!_fs.DirectoryExists(full) || !_owner.IsInsideRoot(full)) return false;

        if (addToHistory && !string.Equals(full, CurrentDirectory, _fs.PathComparison))
        {
            _back.Add(CurrentDirectory);
            _forward.Clear();
        }
        SearchBox.Text = string.Empty;
        LoadDirectory(full);
        return true;
    }

    // Navigates to a typed path (path bar), with a message if it doesn't exist.
    private void NavigateTyped(string path)
    {
        string trimmed = path.Trim().Trim('"');
        if (trimmed.Length == 0) return;
        string target = _fs.IsPathRooted(trimmed) ? trimmed : _fs.Combine(CurrentDirectory, trimmed);
        if (!Navigate(target))
        {
            _ = ShowMessageAsync($"Can't find \"{trimmed}\". Check the spelling and try again.", DialogButtons.Ok);
        }
    }

    /// <summary>Goes back to the previous folder.</summary>
    public void GoBack()
    {
        if (_back.Count == 0) return;
        string target = _back[^1];
        _back.RemoveAt(_back.Count - 1);
        _forward.Add(CurrentDirectory);
        if (!Navigate(target, addToHistory: false)) UpdateButtons();
    }

    /// <summary>Goes forward again.</summary>
    public void GoForward()
    {
        if (_forward.Count == 0) return;
        string target = _forward[^1];
        _forward.RemoveAt(_forward.Count - 1);
        _back.Add(CurrentDirectory);
        if (!Navigate(target, addToHistory: false)) UpdateButtons();
    }

    /// <summary>Goes to the parent folder (not above the root folder).</summary>
    public void GoUp()
    {
        if (CanGoUp && _fs.GetParent(CurrentDirectory) is { } parent) Navigate(parent);
    }

    private bool CanGoUp => _fs.GetParent(CurrentDirectory) is { } parent && _owner.IsInsideRoot(parent);

    /// <summary>Reads the folder again (and the tree).</summary>
    public void Refresh()
    {
        foreach (var root in _treeRoots) root.Reset();
        FolderTree.RebuildTree();
        LoadDirectory(CurrentDirectory);
    }

    private void LoadDirectory(string directory)
    {
        CurrentDirectory = directory;
        IReadOnlyList<FileSystemEntry> all;
        string? error = null;
        try
        {
            all = _fs.GetEntries(directory);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            all = [];
            error = ex is UnauthorizedAccessException ? "You don't have permission to open this folder." : ex.Message;
        }

        var filter = (_owner as FileDialog)?.CurrentFilter;
        var shown = new List<FileSystemEntry>();
        foreach (var entry in all)
        {
            if (entry.IsHidden && !_owner.ShowHiddenItems) continue;
            if (!entry.IsDirectory)
            {
                if (!_owner.ShowsFiles) continue;
                if (_namePattern != null && !new FileDialogFilter(_namePattern, [_namePattern]).Matches(entry.Name)) continue;
                if (_namePattern == null && filter != null && !filter.Matches(entry.Name)) continue;
            }
            shown.Add(entry);
        }
        _entries = shown;
        FileList.EmptyContent = error ?? (_owner.ShowsFiles ? "This folder is empty." : "This folder has no subfolders.");
        FileList.ItemsSource = shown;
        PathBar.SetPath(directory);
        UpdateButtons();
        RevealInTree(directory);
    }

    private void UpdateButtons()
    {
        BackButton.IsEnabled = _back.Count > 0;
        ForwardButton.IsEnabled = _forward.Count > 0;
        UpButton.IsEnabled = CanGoUp;
    }

    /// <summary>Shows only the files matching a pattern typed into the name box (e.g. "*.log") until the file type changes.</summary>
    public void ApplyNamePattern(string pattern)
    {
        _namePattern = pattern;
        LoadDirectory(CurrentDirectory);
    }

    private void ApplySearch()
    {
        string search = SearchBox.Text.Trim();
        FileList.Filter = search.Length == 0 ? null : item => ((FileSystemEntry)item).Name.Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    private void OnTypeChanged()
    {
        if (_owner is not FileDialog dialog) return;
        var previous = dialog.CurrentFilter;
        dialog.FilterIndex = TypeBox.SelectedIndex + 1;
        _namePattern = null;

        // Save: a typed name follows the type ("notes.txt" becomes "notes.md").
        if (dialog is SaveFileDialog && previous?.DefaultExtension is { } oldExtension && dialog.CurrentFilter?.DefaultExtension is { } newExtension
            && NameBox.Text.EndsWith(oldExtension, StringComparison.OrdinalIgnoreCase))
        {
            SetName(NameBox.Text[..^oldExtension.Length] + newExtension);
        }
        LoadDirectory(CurrentDirectory);
    }

    #endregion

    #region Tree

    private void BuildTree()
    {
        _treeRoots.Clear();
        if (_owner.RootDirectory != null)
        {
            string root = _fs.GetFullPath(_owner.RootDirectory);
            _treeRoots.Add(new FolderNode(this, _fs.GetName(root), root, MaterialIconKind.Folder));
        }
        else
        {
            foreach (var place in _fs.GetPlaces()) _treeRoots.Add(new FolderNode(this, place.Name, place.Path, place.Icon));
            foreach (var custom in _owner.CustomPlaces)
            {
                string path = _fs.GetFullPath(custom);
                if (_fs.DirectoryExists(path)) _treeRoots.Add(new FolderNode(this, _fs.GetName(path), path, MaterialIconKind.FolderSpecial));
            }
            foreach (var root in _fs.GetRoots()) _treeRoots.Add(new FolderNode(this, root.Name, root.Path, root.Icon));
        }
        FolderTree.ItemsSource = _treeRoots;
    }

    private static UIElement FolderRow(FolderNode node)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        row.Add(new Icon(node.Icon, 20) { VerticalAlignment = VerticalAlignment.Center, StyleKey = FolderIconStyleKey });
        row.Add(new TextBlock { Text = node.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
        return row;
    }

    // Selects the tree node of the folder, expanding the way there from the root that contains it most closely.
    private void RevealInTree(string directory)
    {
        FolderNode? best = null;
        foreach (var root in _treeRoots)
        {
            if (_owner.IsSameOrInside(directory, root.Path) && (best == null || root.Path.Length > best.Path.Length)) best = root;
        }

        _isSyncingTree = true;
        try
        {
            if (best == null)
            {
                FolderTree.SelectedItem = null;
                return;
            }

            // The chain of folders from the root down to the directory.
            var chain = new List<string>();
            for (string? p = directory; p != null && !string.Equals(p, best.Path, _fs.PathComparison); p = _fs.GetParent(p)) chain.Add(p);
            chain.Reverse();

            var node = best;
            foreach (string path in chain)
            {
                var item = FindTreeItem(node);
                if (item == null) break;
                item.IsExpanded = true;
                var child = node.Children.FirstOrDefault(c => string.Equals(c.Path, path, _fs.PathComparison));
                if (child == null) break;
                node = child;
            }
            FolderTree.SelectedItem = node;
            if (FolderTree.SelectedNode is { } selected) FolderTree.ScrollIntoView(selected);
        }
        finally
        {
            _isSyncingTree = false;
        }
    }

    private TreeViewItem? FindTreeItem(FolderNode node) => FolderTree.GetAllNodes().FirstOrDefault(n => ReferenceEquals(n.ItemValue, node));

    // A folder of the tree; its subfolders are read when the tree first asks for them.
    private sealed class FolderNode(FileDialogView view, string name, string path, MaterialIconKind icon)
    {
        private List<FolderNode>? _children;

        public string Name { get; } = name;

        public string Path { get; } = path;

        public MaterialIconKind Icon { get; } = icon;

        public List<FolderNode> Children => _children ??= ReadChildren();

        public void Reset() => _children = null;

        private List<FolderNode> ReadChildren()
        {
            try
            {
                return view._fs.GetEntries(Path)
                    .Where(e => e.IsDirectory && (!e.IsHidden || view._owner.ShowHiddenItems))
                    .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(e => new FolderNode(view, e.Name, e.FullPath, MaterialIconKind.Folder))
                    .ToList();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                return [];
            }
        }

        public override string ToString() => Name;
    }

    #endregion

    #region Selection and accepting

    private void SetName(string text)
    {
        _isUpdatingName = true;
        try
        {
            NameBox.Text = text;
        }
        finally
        {
            _isUpdatingName = false;
        }
    }

    // The name box follows the selection: files for the file dialogs, folders for the folder dialog.
    private void OnListSelectionChanged()
    {
        if (_isUpdatingName) return;
        bool folders = !_owner.ShowsFiles;
        var names = FileList.SelectedItems.Cast<FileSystemEntry>().Where(e => e.IsDirectory == folders).Select(e => e.Name).ToList();
        if (names.Count > 0) SetName(CommonItemDialog.FormatNames(names));
        else if (folders && FileList.SelectedItems.Count == 0) SetName(string.Empty);
    }

    private void OnRowActivated(FileSystemEntry entry)
    {
        if (entry.IsDirectory)
        {
            Navigate(entry.FullPath);
            if (_owner.ShowsFiles) SetName(string.Empty);
            return;
        }
        SetName(entry.Name);
        _ = AcceptAsync();
    }

    /// <summary>
    /// Accepts the name box (or, when it is empty, the selected folder: opened in the file dialogs, chosen in the
    /// folder dialog) and closes the dialog if the choice is valid; otherwise navigates, filters or shows why.
    /// </summary>
    /// <returns><c>true</c> if the dialog closes.</returns>
    public async Task<bool> AcceptAsync()
    {
        if (_isAccepting) return false;
        _isAccepting = true;
        try
        {
            if (FileList.IsEditing && !FileList.CommitEdit()) return false;

            var names = CommonItemDialog.ParseNames(NameBox.Text);
            if (names.Count == 0 && _owner.ShowsFiles && FileList.SelectedItem is FileSystemEntry { IsDirectory: true } folder && FileList.SelectedItems.Count == 1)
            {
                Navigate(folder.FullPath);
                return false;
            }
            if (!await _owner.AcceptAsync(this, CurrentDirectory, names))
            {
                if (IsOpen) NameBox.Focus();
                return false;
            }
            Close(DialogResult.Ok);
            return true;
        }
        finally
        {
            _isAccepting = false;
        }
    }

    /// <summary>Shows a message over the dialog and returns the button chosen.</summary>
    public async Task<DialogResult> ShowMessageAsync(string message, DialogButtons buttons, string? title = null)
    {
        var host = DialogHost.FindNearestHost(this);
        if (host == null) return DialogResult.None;
        var response = await new Dialog(title ?? _owner.EffectiveTitle, message, buttons).ShowAsync(host);
        return response.Result;
    }

    #endregion

    #region New folder and rename

    /// <summary>Creates a folder named "New folder" (or "New folder (2)", ...) and starts renaming it; returns its path, or <c>null</c> on failure.</summary>
    public string? CreateNewFolder()
    {
        if (!_owner.ShowsNewFolderButton) return null;
        string name = "New folder";
        for (int i = 2; _fs.DirectoryExists(_fs.Combine(CurrentDirectory, name)) || _fs.FileExists(_fs.Combine(CurrentDirectory, name)); i++) name = $"New folder ({i})";
        string path = _fs.Combine(CurrentDirectory, name);
        try
        {
            _fs.CreateDirectory(path);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            _ = ShowMessageAsync($"Can't create a folder here.\n{ex.Message}", DialogButtons.Ok);
            return null;
        }

        SearchBox.Text = string.Empty;
        LoadDirectory(CurrentDirectory);
        FolderTree.RebuildTree();
        if (_entries.FirstOrDefault(e => string.Equals(e.FullPath, path, _fs.PathComparison)) is { } entry)
        {
            FileList.SelectedItem = entry;
            FileList.Focus();
            FileList.BeginEdit(entry, _nameColumn);
        }
        return path;
    }

    // Renames an item (F2 in the list, or after "New folder"); throws to reject the name.
    private void Rename(FileSystemEntry entry, string newName)
    {
        newName = newName.Trim();
        if (newName == entry.Name) return;
        if (newName.Length == 0) throw new ArgumentException("Enter a name.");
        if (newName.Any(_fs.InvalidFileNameChars.Contains)) throw new ArgumentException("A name can't contain " + string.Join(" ", _fs.InvalidFileNameChars.Where(c => !char.IsControl(c))) + ".");
        string? parent = _fs.GetParent(entry.FullPath);
        if (parent == null) return;
        string target = _fs.Combine(parent, newName);
        if (!string.Equals(target, entry.FullPath, _fs.PathComparison) && (_fs.FileExists(target) || _fs.DirectoryExists(target)))
        {
            throw new ArgumentException($"There is already an item named \"{newName}\".");
        }
        try
        {
            _fs.Move(entry.FullPath, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
        _renamedPath = target; // the list is read again when the edit has ended
    }

    private void OnRenameEnded(bool committed)
    {
        if (!committed || _renamedPath is not { } path) return;
        _renamedPath = null;
        LoadDirectory(CurrentDirectory);
        foreach (var root in _treeRoots) root.Reset();
        FolderTree.RebuildTree();
        RevealInTree(CurrentDirectory);
        if (_entries.FirstOrDefault(e => string.Equals(e.FullPath, path, _fs.PathComparison)) is { } entry)
        {
            FileList.SelectedItem = entry;
            if (entry.IsDirectory == !_owner.ShowsFiles) SetName(entry.Name);
        }
    }

    #endregion

    #region Keys and layout

    /// <inheritdoc/>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;
        bool alt = (e.Modifiers & ModifierKeys.Alt) != 0;
        bool ctrl = (e.Modifiers & ModifierKeys.Control) != 0;
        bool shift = (e.Modifiers & ModifierKeys.Shift) != 0;
        switch (e.Key)
        {
            case Key.Left when alt:
                GoBack();
                break;
            case Key.Right when alt:
                GoForward();
                break;
            case Key.Up when alt:
            case Key.Backspace when !alt && !ctrl:
                GoUp();
                break;
            case Key.F5:
                Refresh();
                break;
            case Key.L when ctrl:
            case Key.D when alt:
                PathBar.BeginEdit();
                break;
            case Key.N when ctrl && shift:
                CreateNewFolder();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    /// <inheritdoc/>
    /// <remarks>Takes <see cref="PreferredSize"/>, or less in a smaller window.</remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        float width = float.IsFinite(availableSize.Width) ? Math.Min(PreferredSize.Width, availableSize.Width - 32) : PreferredSize.Width;
        float height = float.IsFinite(availableSize.Height) ? Math.Min(PreferredSize.Height, availableSize.Height - 32) : PreferredSize.Height;
        var padding = Padding;
        _layout.Width = Math.Max(420, width) - padding.Horizontal;
        _layout.Height = Math.Max(360, height) - padding.Vertical;
        return base.MeasureOverride(availableSize);
    }

    #endregion
}

/// <summary>
/// The path bar of a <see cref="FileDialogView"/>: the folders of the path as buttons (click one to go there), with the
/// first ones left out behind "…" when they don't fit. Clicking beside them, or <see cref="BeginEdit"/>, turns it into a
/// text box for typing a path (Enter goes there, Escape keeps the folder).
/// </summary>
public class FileDialogPathBar : Control
{
    private readonly IFileSystemProvider _fs;
    private readonly string? _root;
    private readonly List<string> _paths = [];
    private readonly List<Button> _buttons = [];
    private readonly List<Icon> _chevrons = [];
    private readonly Button _overflow;
    private readonly TextBox _editBox;
    private string _path = string.Empty;
    private int _firstShown;

    internal FileDialogPathBar(IFileSystemProvider fs, string? root)
    {
        _fs = fs;
        _root = root;
        Height = 40;
        CornerRadius = new CornerRadius(20);
        ClipToBounds = true;
        _overflow = SegmentButton("…");
        _overflow.Click += (_, _) =>
        {
            if (_firstShown > 0) PathRequested?.Invoke(this, _paths[_firstShown - 1]);
        };
        AddChild(_overflow);

        _editBox = new TextBox { FieldHeight = 40, Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };
        _editBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                string text = _editBox.Text;
                EndEdit();
                PathRequested?.Invoke(this, text);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                EndEdit();
                e.Handled = true;
            }
        };
        _editBox.LostFocus += (_, _) => EndEdit();
        AddChild(_editBox);
    }

    /// <summary>Occurs when the user asks for a folder: a clicked folder's path, or the typed text.</summary>
    public event EventHandler<string>? PathRequested;

    /// <summary>Gets the path shown.</summary>
    public string Path => _path;

    /// <summary>Gets the paths of the folders shown as buttons, from the root down.</summary>
    public IReadOnlyList<string> SegmentPaths => _paths;

    /// <summary>Gets the folder buttons, in the order of <see cref="SegmentPaths"/>.</summary>
    public IReadOnlyList<Button> SegmentButtons => _buttons;

    /// <summary>Gets whether the path is being typed.</summary>
    public bool IsEditing => _editBox.Visibility == Visibility.Visible;

    /// <summary>Gets the text box for typing a path.</summary>
    public TextBox EditBox => _editBox;

    /// <summary>Gets the index of the first folder shown; those before it are behind "…".</summary>
    public int FirstShownSegment => _firstShown;

    internal void SetPath(string path)
    {
        _path = path;
        foreach (var button in _buttons) RemoveChild(button);
        foreach (var chevron in _chevrons) RemoveChild(chevron);
        _buttons.Clear();
        _chevrons.Clear();
        _paths.Clear();

        for (string? p = path; p != null; p = _fs.GetParent(p))
        {
            _paths.Insert(0, p);
            if (_root != null && string.Equals(p, _root, _fs.PathComparison)) break;
        }
        for (int i = 0; i < _paths.Count; i++)
        {
            string target = _paths[i];
            var button = SegmentButton(_fs.GetName(target));
            button.Click += (_, _) => PathRequested?.Invoke(this, target);
            _buttons.Add(button);
            AddChild(button);
            if (i < _paths.Count - 1)
            {
                var chevron = new Icon(MaterialIconKind.ChevronRight, 18) { VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
                _chevrons.Add(chevron);
                AddChild(chevron);
            }
        }
        ToolTipService.SetToolTip(this, path);
        InvalidateMeasure();
    }

    private static Button SegmentButton(string text) => new(text)
    {
        Variant = ButtonVariant.Text,
        MinWidth = 0,
        MinHeight = 0,
        Height = 32,
        Padding = new Thickness(8, 0),
        CornerRadius = new CornerRadius(16),
        VerticalAlignment = VerticalAlignment.Center,
        StyleKey = SegmentStyleKey,
    };

    /// <summary>Identifies the style of the folder buttons.</summary>
    public const string SegmentStyleKey = "FileDialogPathSegment";

    /// <summary>Shows the text box with the path, all selected.</summary>
    public void BeginEdit()
    {
        _editBox.Text = _path;
        _editBox.Visibility = Visibility.Visible;
        _editBox.Focus();
        _editBox.SelectAll();
        InvalidateMeasure();
    }

    /// <summary>Shows the folders again.</summary>
    public void EndEdit()
    {
        if (!IsEditing) return;
        _editBox.Visibility = Visibility.Collapsed;
        InvalidateMeasure();
    }

    /// <inheritdoc/>
    /// <remarks>A press beside the folders starts typing a path.</remarks>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || e.Button != PointerButtons.Left || IsEditing) return;
        BeginEdit();
        e.Handled = true;
    }

    private const float ChevronSpace = 18f;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        float width = float.IsFinite(availableSize.Width) ? availableSize.Width : 600;
        _editBox.Measure(new Size(width, 40));
        _overflow.Measure(new Size(float.PositiveInfinity, 40));
        foreach (var chevron in _chevrons) chevron.Measure(new Size(ChevronSpace, 40));
        float total = 0;
        foreach (var button in _buttons)
        {
            button.Measure(new Size(float.PositiveInfinity, 40));
            total += button.DesiredSize.Width + ChevronSpace;
        }

        // Leave out the first folders until the rest (and "…") fit.
        _firstShown = 0;
        float available = width - 16;
        while (_firstShown < _buttons.Count - 1 && total + (_firstShown > 0 ? _overflow.DesiredSize.Width + ChevronSpace : 0) > available)
        {
            total -= _buttons[_firstShown].DesiredSize.Width + ChevronSpace;
            _firstShown++;
        }
        return new Size(width, 40);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var hidden = new Rect(-10000, 0, 0, 0);
        if (IsEditing)
        {
            _editBox.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            _overflow.Arrange(hidden);
            foreach (var button in _buttons) button.Arrange(hidden);
            foreach (var chevron in _chevrons) chevron.Arrange(hidden);
            return finalSize;
        }

        float x = 8;
        float center = finalSize.Height / 2;
        if (_firstShown > 0)
        {
            var size = _overflow.DesiredSize;
            _overflow.Arrange(new Rect(x, center - size.Height / 2, size.Width, size.Height));
            x += size.Width;
            if (_chevrons.Count > 0)
            {
                // The chevron before the first shown folder follows "…".
                var chevron = _chevrons[_firstShown - 1];
                chevron.Arrange(new Rect(x, center - chevron.DesiredSize.Height / 2, ChevronSpace, chevron.DesiredSize.Height));
                x += ChevronSpace;
            }
        }
        else
        {
            _overflow.Arrange(hidden);
        }

        for (int i = 0; i < _buttons.Count; i++)
        {
            var button = _buttons[i];
            if (i < _firstShown)
            {
                button.Arrange(hidden);
                if (i < _chevrons.Count && i != _firstShown - 1) _chevrons[i].Arrange(hidden);
                continue;
            }
            var size = button.DesiredSize;
            button.Arrange(new Rect(x, center - size.Height / 2, Math.Min(size.Width, Math.Max(0, finalSize.Width - x)), size.Height));
            x += size.Width;
            if (i < _chevrons.Count)
            {
                var chevron = _chevrons[i];
                chevron.Arrange(new Rect(x, center - chevron.DesiredSize.Height / 2, ChevronSpace, chevron.DesiredSize.Height));
                x += ChevronSpace;
            }
        }
        return finalSize;
    }
}
