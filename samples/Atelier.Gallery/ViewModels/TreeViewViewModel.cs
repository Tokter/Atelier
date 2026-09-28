using System;
using System.Collections.ObjectModel;
using System.IO;
using Atelier.Controls;
using Atelier.Core.Keybinding;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

/// <summary>What a node of the file tree is; decides its icon and color.</summary>
public enum FileKind
{
    Folder,
    Code,
    Image,
    Document,
    Config
}

/// <summary>A folder or file in the explorer demo.</summary>
public partial class TreeItemNode : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private FileKind _kind;

    [ObservableProperty]
    private string _details;

    public ObservableCollection<TreeItemNode> Children { get; } = [];

    public TreeItemNode? Parent { get; private set; }

    public bool IsFolder => Kind == FileKind.Folder;

    public MaterialIconKind Icon => Kind switch
    {
        FileKind.Folder => MaterialIconKind.Folder,
        FileKind.Code => MaterialIconKind.Code,
        FileKind.Image => MaterialIconKind.Image,
        FileKind.Config => MaterialIconKind.Settings,
        _ => MaterialIconKind.Description
    };

    public string FullPath => Parent == null ? "/" + Name : Parent.FullPath + "/" + Name;

    public TreeItemNode(string name, FileKind kind, string details = "")
    {
        _name = name;
        _kind = kind;
        _details = details;
    }

    public TreeItemNode Add(params TreeItemNode[] children)
    {
        foreach (var child in children)
        {
            child.Parent = this;
            Children.Add(child);
        }
        return this;
    }

    public void Remove(TreeItemNode child)
    {
        Children.Remove(child);
        child.Parent = null;
    }

    public static TreeItemNode FromName(string name) =>
        Path.GetExtension(name).ToLowerInvariant() switch
        {
            "" => new TreeItemNode(name, FileKind.Folder, "Folder"),
            ".cs" => new TreeItemNode(name, FileKind.Code, "C# source"),
            ".png" or ".jpg" or ".svg" => new TreeItemNode(name, FileKind.Image, "Image"),
            ".json" or ".csproj" or ".slnx" => new TreeItemNode(name, FileKind.Config, "Configuration"),
            _ => new TreeItemNode(name, FileKind.Document, "Document")
        };

    public override string ToString() => Name;
}

public partial class TreeViewViewModel : PageViewModel
{
    public ObservableCollection<TreeItemNode> RootNodes { get; } = [];

    [ObservableProperty]
    private bool _controlsEnabled = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedCommand))]
    private TreeItemNode? _selectedNode;

    [ObservableProperty]
    private string _newItemName = "Settings.json";

    [ObservableProperty]
    private float _indentSize = 20;

    [ObservableProperty]
    private float _iconSize = 18;

    [ObservableProperty]
    private bool _useArrowIcons;

    [ObservableProperty]
    private string _lastEvent = "Expand, collapse or select a node";

    [ObservableProperty]
    private object? _manualSelection;

    public TreeViewViewModel()
    {
        PageTitle = "TreeView";
        PageIcon = MaterialIconKind.AccountTree;
        Keywords = "treeview treeviewitem hierarchy tree explorer expand collapse";
        ResetTree();
    }

    [RelayCommand]
    [property: Command("AddItem", "TreeView", Label = "Add", Icon = MaterialIcons.Add, Description = "Add the named file or folder to the selected folder")]
    private void AddItem()
    {
        var node = TreeItemNode.FromName(string.IsNullOrWhiteSpace(NewItemName) ? "NewFolder" : NewItemName.Trim());

        // Add inside the selected folder, next to the selected file, or at the root.
        var parent = SelectedNode is { IsFolder: true } folder ? folder : SelectedNode?.Parent;
        if (parent != null)
        {
            parent.Add(node);
        }
        else
        {
            RootNodes.Add(node);
        }

        SelectedNode = node;
        LastEvent = $"Added {node.FullPath}";
    }

    [RelayCommand(CanExecute = nameof(CanRemove))]
    [property: Command("RemoveSelected", "TreeView", Label = "Remove", Icon = MaterialIcons.Delete, Description = "Remove the selected item")]
    private void RemoveSelected()
    {
        if (SelectedNode is not { } node)
        {
            return;
        }

        var parent = node.Parent;
        if (parent != null)
        {
            parent.Remove(node);
        }
        else
        {
            RootNodes.Remove(node);
        }

        SelectedNode = parent;
        LastEvent = $"Removed {node.Name}";
    }

    private bool CanRemove() => SelectedNode != null;

    [RelayCommand]
    [property: Command("ResetTree", "TreeView", Label = "Restore sample", Description = "Bring back the sample tree")]
    private void ResetTree()
    {
        RootNodes.Clear();

        var core = new TreeItemNode("Atelier.Core", FileKind.Folder, "Property system, tree, input").Add(
            new TreeItemNode("BindableObject.cs", FileKind.Code, "38 KB"),
            new TreeItemNode("BindableProperty.cs", FileKind.Code, "14 KB"),
            new TreeItemNode("UIElement.cs", FileKind.Code, "52 KB"),
            new TreeItemNode("Atelier.Core.csproj", FileKind.Config, "1 KB"));

        var controls = new TreeItemNode("Atelier.Controls", FileKind.Folder, "Controls").Add(
            new TreeItemNode("Button.cs", FileKind.Code, "6 KB"),
            new TreeItemNode("Slider.cs", FileKind.Code, "15 KB"),
            new TreeItemNode("TreeView.cs", FileKind.Code, "30 KB"),
            new TreeItemNode("TreeViewItem.cs", FileKind.Code, "18 KB"));

        var src = new TreeItemNode("src", FileKind.Folder, "Libraries").Add(core, controls);

        var gallery = new TreeItemNode("samples", FileKind.Folder, "Sample apps").Add(
            new TreeItemNode("Atelier.Gallery", FileKind.Folder, "This app").Add(
                new TreeItemNode("Program.cs", FileKind.Code, "3 KB"),
                new TreeItemNode("Assets", FileKind.Folder, "Images").Add(
                    new TreeItemNode("Atelier.png", FileKind.Image, "128 KB"))));

        RootNodes.Add(src);
        RootNodes.Add(gallery);
        RootNodes.Add(new TreeItemNode("Atelier.slnx", FileKind.Config, "Solution"));
        RootNodes.Add(new TreeItemNode("README.md", FileKind.Document, "5 KB"));

        SelectedNode = controls.Children[2];
    }

    [RelayCommand]
    [property: Command("Reset", "TreeView", Icon = MaterialIcons.RestartAlt, Description = "Put the demos of this page back as they were")]
    private void Reset()
    {
        ControlsEnabled = true;
        IndentSize = 20;
        IconSize = 18;
        UseArrowIcons = false;
        LastEvent = "Expand, collapse or select a node";
        ResetTree();
    }
}
