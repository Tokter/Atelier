using System.Collections.Generic;
using System.Collections.ObjectModel;
using Atelier.Controls;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

/// <summary>A node of the data-bound menu: a title with an optional icon and children, or a separator.</summary>
public sealed record MenuNode(string Title, MaterialIconKind Icon = MaterialIconKind.None, IReadOnlyList<object>? Children = null);

/// <summary>A highlight color, shown with a swatch by a menu item template.</summary>
public sealed record HighlightColor(string Name, Color Color);

/// <summary>
/// The menus demo. The commands carry [property: Keybinding] in the "Menus" group with their labels (and access keys) and
/// icons, so the menu items only name their command; the menus show their shortcuts
/// and the keys work while the focus is inside the demo.
/// </summary>
public partial class MenusViewModel : PageViewModel
{
    /// <summary>The keybinding group of this page's commands.</summary>
    public const string Group = "Menus";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CutCommand), nameof(CopyCommand), nameof(DeleteCommand))]
    private bool _hasSelection = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PasteCommand))]
    private bool _hasClipboard;

    [ObservableProperty]
    private bool _wordWrap = true;

    [ObservableProperty]
    private bool _showMinimap;

    [ObservableProperty]
    private string _alignment = "Left";

    [ObservableProperty]
    private string _lastAction = "Pick something from a menu";

    public ObservableCollection<string> RecentFiles { get; } = ["README.md", "Program.cs", "theme.json"];

    /// <summary>The items of the data-bound menu bar: nodes with children, separators and plain leaves.</summary>
    public IReadOnlyList<object> Catalog { get; } =
    [
        new MenuNode("Products", MaterialIconKind.Inventory2, [
            new MenuNode("Laptops", MaterialIconKind.Laptop),
            new MenuNode("Phones", MaterialIconKind.Smartphone),
            MenuSeparator.Instance,
            new MenuNode("Accessories", MaterialIconKind.Headphones, [
                new MenuNode("Headphones"),
                new MenuNode("Chargers"),
                new MenuNode("Cases"),
            ]),
        ]),
        new MenuNode("Services", MaterialIconKind.HomeRepairService, [
            new MenuNode("Repairs", MaterialIconKind.Build),
            new MenuNode("Trade-in", MaterialIconKind.Sync),
        ]),
        new MenuNode("Support", MaterialIconKind.Help),
    ];

    public IReadOnlyList<HighlightColor> HighlightColors { get; } =
    [
        new("Yellow", Color.FromHex("#FFF59D")),
        new("Green", Color.FromHex("#C5E1A5")),
        new("Blue", Color.FromHex("#90CAF9")),
        new("Pink", Color.FromHex("#F48FB1")),
    ];

    public MenusViewModel()
    {
        PageIcon = MaterialIconKind.Menu;
        PageTitle = "Menus";
        CommandGroup = Group;
        Keywords = "menu menubar menuitem context menu separator shortcut gesture access key checkable radio submenu";
    }

    private void Did(string action) => LastAction = action;

    [RelayCommand]
    [property: Keybinding("MenusNew", Group, "Ctrl+N", Label = "_New", Icon = MaterialIcons.NoteAdd, Description = "Start a new document")]
    private void New() => Did("New");

    [RelayCommand]
    [property: Keybinding("MenusOpen", Group, "Ctrl+O", Label = "_Open…", Icon = MaterialIcons.FolderOpen, Description = "Open a document")]
    private void Open() => Did("Open…");

    [RelayCommand]
    [property: Keybinding("MenusSave", Group, "Ctrl+S", Label = "_Save", Icon = MaterialIcons.Save, Description = "Save the document")]
    private void Save() => Did("Save");

    [RelayCommand]
    [property: Command("OpenRecent", Group, Label = "Open recent", Description = "Open the chosen recent file")]
    private void OpenRecent(string file) => Did($"Open recent: {file}");

    [RelayCommand]
    [property: Keybinding("MenusUndo", Group, "Ctrl+Z", Label = "_Undo", Icon = MaterialIcons.Undo, Description = "Undo the last change")]
    private void Undo() => Did("Undo");

    [RelayCommand]
    [property: Keybinding("MenusRedo", Group, "Ctrl+Y", Label = "_Redo", Icon = MaterialIcons.Redo, Description = "Redo the change")]
    private void Redo() => Did("Redo");

    [RelayCommand(CanExecute = nameof(HasSelection))]
    [property: Keybinding("MenusCut", Group, "Ctrl+X", Label = "Cu_t", Icon = MaterialIcons.ContentCut, Description = "Cut the selection")]
    private void Cut()
    {
        HasClipboard = true;
        Did("Cut");
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    [property: Keybinding("MenusCopy", Group, "Ctrl+C", Label = "_Copy", Icon = MaterialIcons.ContentCopy, Description = "Copy the selection")]
    private void Copy()
    {
        HasClipboard = true;
        Did("Copy");
    }

    [RelayCommand(CanExecute = nameof(HasClipboard))]
    [property: Keybinding("MenusPaste", Group, "Ctrl+V", Label = "_Paste", Icon = MaterialIcons.ContentPaste, Description = "Paste the clipboard")]
    private void Paste() => Did("Paste");

    [RelayCommand(CanExecute = nameof(HasSelection))]
    [property: Keybinding("MenusDelete", Group, "Delete", Label = "_Delete", Icon = MaterialIcons.Delete, Description = "Delete the selection")]
    private void Delete() => Did("Delete (hidden while nothing is selected)");

    [RelayCommand]
    [property: Keybinding("MenusSelectAll", Group, "Ctrl+A", Label = "Select _all", Icon = MaterialIcons.SelectAll, Description = "Select everything")]
    private void SelectAll()
    {
        HasSelection = true;
        Did("Select all");
    }

    [RelayCommand]
    [property: Keybinding("MenusComment", Group, "Ctrl+K, Ctrl+C", Label = "Toggle co_mment", Icon = MaterialIcons.Comment, Description = "Comment the line, or uncomment it")]
    private void CommentLine() => Did("Comment line (a chord: Ctrl+K, Ctrl+C)");

    [RelayCommand]
    private void Pick(string what) => Did(what);

    [RelayCommand]
    [property: Command("Reset", Group, Icon = MaterialIcons.RestartAlt, Description = "Put the demos of this page back as they were")]
    private void Reset()
    {
        HasSelection = true;
        HasClipboard = false;
        WordWrap = true;
        ShowMinimap = false;
        Alignment = "Left";
        LastAction = "Pick something from a menu";
    }
}
