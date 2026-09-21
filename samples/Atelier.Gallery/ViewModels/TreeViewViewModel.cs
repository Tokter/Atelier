using System;
using System.Collections.ObjectModel;
using Atelier.Controls;
using Atelier.Core.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class TreeItemNode : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _details;

    [ObservableProperty]
    private string? _badge;

    [ObservableProperty]
    private bool _isDirectory;

    [ObservableProperty]
    private MaterialIconKind _icon;

    [ObservableProperty]
    private Color? _iconColor;

    public ObservableCollection<TreeItemNode> Children { get; } = [];

    public TreeItemNode? Parent { get; set; }

    public string FullPath
    {
        get
        {
            var segments = new System.Collections.Generic.List<string>();
            var current = this;
            while (current != null)
            {
                segments.Insert(0, current.Name);
                current = current.Parent;
            }
            return "/" + string.Join("/", segments);
        }
    }

    public int TotalDescendantsCount
    {
        get
        {
            int count = Children.Count;
            for (int i = 0; i < Children.Count; i++)
            {
                count += Children[i].TotalDescendantsCount;
            }
            return count;
        }
    }

    public TreeItemNode(string name, bool isDirectory, MaterialIconKind icon, Color? iconColor = null, string? badge = null, string details = "")
    {
        _name = name;
        _isDirectory = isDirectory;
        _icon = icon;
        _iconColor = iconColor;
        _badge = badge;
        _details = details;
    }

    public TreeItemNode AddChild(TreeItemNode child)
    {
        child.Parent = this;
        Children.Add(child);
        return this;
    }

    public TreeItemNode AddChildren(params TreeItemNode[] children)
    {
        foreach (var child in children)
        {
            AddChild(child);
        }
        return this;
    }
}

public partial class TreeViewViewModel : PageViewModel
{
    public ObservableCollection<TreeItemNode> RootNodes { get; } = [];

    [ObservableProperty]
    private TreeItemNode? _selectedNode;

    [ObservableProperty]
    private string _selectedNodeName = string.Empty;

    [ObservableProperty]
    private string _newItemName = "NewComponent.cs";

    [ObservableProperty]
    private bool _newItemIsFolder = false;

    [ObservableProperty]
    private float _indentSize = 20f;

    [ObservableProperty]
    private string _statusMessage = "Ready. Select a node or use keyboard arrow keys.";

    [ObservableProperty]
    private int _totalNodeCount = 0;

    [ObservableProperty]
    private int _rootNodeCount = 0;

    public Action? RequestExpandAll { get; set; }
    public Action? RequestCollapseAll { get; set; }

    public TreeViewViewModel()
    {
        PageTitle = "TreeView";
        PageIcon = MaterialIconKind.AccountTree;

        ResetTree();
    }

    partial void OnSelectedNodeChanged(TreeItemNode? value)
    {
        if (value != null)
        {
            SelectedNodeName = value.Name;
            StatusMessage = $"Selected: {value.FullPath} ({(value.IsDirectory ? $"Directory with {value.Children.Count} items" : $"File, {value.Details}")})";
        }
        else
        {
            SelectedNodeName = string.Empty;
            StatusMessage = "No node selected. Select an item or use keyboard arrows.";
        }
    }

    partial void OnSelectedNodeNameChanged(string value)
    {
        if (SelectedNode != null && !string.IsNullOrWhiteSpace(value) && SelectedNode.Name != value)
        {
            SelectedNode.Name = value;
            StatusMessage = $"Renamed to: {value}";
        }
    }

    [RelayCommand]
    private void ExpandAll()
    {
        RequestExpandAll?.Invoke();
        StatusMessage = "Expanded all tree nodes.";
    }

    [RelayCommand]
    private void CollapseAll()
    {
        RequestCollapseAll?.Invoke();
        StatusMessage = "Collapsed all tree nodes.";
    }

    [RelayCommand]
    private void AddChild()
    {
        string name = string.IsNullOrWhiteSpace(NewItemName) ? (NewItemIsFolder ? "NewFolder" : "NewFile.cs") : NewItemName;
        var node = CreateNodeFromInput(name, NewItemIsFolder);

        if (SelectedNode != null)
        {
            SelectedNode.IsDirectory = true;
            SelectedNode.AddChild(node);
            StatusMessage = $"Added '{node.Name}' under '{SelectedNode.Name}'.";
        }
        else
        {
            RootNodes.Add(node);
            StatusMessage = $"Added '{node.Name}' to root.";
        }

        UpdateTreeStatistics();
    }

    [RelayCommand]
    private void AddRoot()
    {
        string name = string.IsNullOrWhiteSpace(NewItemName) ? (NewItemIsFolder ? "NewFolder" : "NewFile.cs") : NewItemName;
        var node = CreateNodeFromInput(name, NewItemIsFolder);
        RootNodes.Add(node);
        StatusMessage = $"Added root item '{node.Name}'.";
        UpdateTreeStatistics();
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedNode == null)
        {
            StatusMessage = "Select a node first to remove it.";
            return;
        }

        var nodeToRemove = SelectedNode;
        var parent = nodeToRemove.Parent;

        if (parent != null)
        {
            parent.Children.Remove(nodeToRemove);
            SelectedNode = parent;
            StatusMessage = $"Removed '{nodeToRemove.Name}' from '{parent.Name}'.";
        }
        else
        {
            RootNodes.Remove(nodeToRemove);
            SelectedNode = RootNodes.Count > 0 ? RootNodes[0] : null;
            StatusMessage = $"Removed root item '{nodeToRemove.Name}'.";
        }

        UpdateTreeStatistics();
    }

    [RelayCommand]
    private void ResetTree()
    {
        RootNodes.Clear();

        // 1. Solution Root: src
        var srcFolder = new TreeItemNode("src", true, MaterialIconKind.Folder, Color.FromHex("#FBBF24"), "dir", "4 projects");

        var coreProject = new TreeItemNode("Atelier.Core", true, MaterialIconKind.Folder, Color.FromHex("#FBBF24"), "proj", "Core abstractions");
        coreProject.AddChildren(
            new TreeItemNode("BindableProperty.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "14 KB"),
            new TreeItemNode("DependencyObject.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "8 KB"),
            new TreeItemNode("Primitives.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "6 KB"),
            new TreeItemNode("StyleManager.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "11 KB")
        );

        var controlsProject = new TreeItemNode("Atelier.Controls", true, MaterialIconKind.Folder, Color.FromHex("#FBBF24"), "proj", "UI controls");
        controlsProject.AddChildren(
            new TreeItemNode("Button.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "12 KB"),
            new TreeItemNode("CheckBox.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "9 KB"),
            new TreeItemNode("TextBox.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "21 KB"),
            new TreeItemNode("TreeView.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "13 KB"),
            new TreeItemNode("TreeViewItem.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "10 KB"),
            new TreeItemNode("Card.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "7 KB"),
            new TreeItemNode("Slider.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "15 KB")
        );

        var themingProject = new TreeItemNode("Atelier.Theming.Material", true, MaterialIconKind.Folder, Color.FromHex("#FBBF24"), "proj", "MD3 styling");
        themingProject.AddChildren(
            new TreeItemNode("MaterialTheme.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "16 KB"),
            new TreeItemNode("MaterialColorScheme.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "9 KB"),
            new TreeItemNode("MaterialTypography.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "6 KB"),
            new TreeItemNode("MaterialRenderers.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "28 KB")
        );

        var layoutProject = new TreeItemNode("Atelier.Layout", true, MaterialIconKind.Folder, Color.FromHex("#FBBF24"), "proj", "Panels & layout");
        layoutProject.AddChildren(
            new TreeItemNode("Grid.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "18 KB"),
            new TreeItemNode("StackPanel.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "8 KB"),
            new TreeItemNode("Border.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "5 KB"),
            new TreeItemNode("ScrollViewer.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "12 KB")
        );

        srcFolder.AddChildren(coreProject, controlsProject, themingProject, layoutProject);

        // 2. Samples Root: samples
        var samplesFolder = new TreeItemNode("samples", true, MaterialIconKind.Folder, Color.FromHex("#FBBF24"), "dir", "Demo apps");
        var galleryProject = new TreeItemNode("Atelier.Gallery", true, MaterialIconKind.Folder, Color.FromHex("#FBBF24"), "proj", "Gallery app");

        var viewsFolder = new TreeItemNode("Views", true, MaterialIconKind.Folder, Color.FromHex("#FBBF24"), "dir", "Gallery views");
        viewsFolder.AddChildren(
            new TreeItemNode("MainView.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "5 KB"),
            new TreeItemNode("CardsView.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "21 KB"),
            new TreeItemNode("CheckboxesView.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "21 KB"),
            new TreeItemNode("TextBoxesView.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "17 KB"),
            new TreeItemNode("TreeViewView.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "16 KB"),
            new TreeItemNode("TypographyView.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "30 KB")
        );

        var assetsFolder = new TreeItemNode("Assets", true, MaterialIconKind.Folder, Color.FromHex("#FBBF24"), "dir", "Icons & artwork");
        assetsFolder.AddChildren(
            new TreeItemNode("Atelier.png", false, MaterialIconKind.Image, Color.FromHex("#EC4899"), "png", "128 KB"),
            new TreeItemNode("Banner.png", false, MaterialIconKind.Image, Color.FromHex("#EC4899"), "png", "340 KB")
        );

        galleryProject.AddChildren(
            new TreeItemNode("Program.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "4 KB"),
            viewsFolder,
            assetsFolder
        );
        samplesFolder.AddChild(galleryProject);

        // 3. Tests Root: tests
        var testsFolder = new TreeItemNode("tests", true, MaterialIconKind.Folder, Color.FromHex("#FBBF24"), "dir", "Unit tests");
        var testsProject = new TreeItemNode("Atelier.Tests", true, MaterialIconKind.Folder, Color.FromHex("#FBBF24"), "proj", "308 tests");
        testsProject.AddChildren(
            new TreeItemNode("TreeViewTests.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "12 KB"),
            new TreeItemNode("LayoutTests.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "18 KB"),
            new TreeItemNode("TextBoxTests.cs", false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "15 KB")
        );
        testsFolder.AddChild(testsProject);

        // 4. Root documentation and solution files
        var solutionFile = new TreeItemNode("Atelier.slnx", false, MaterialIconKind.Settings, Color.FromHex("#8B5CF6"), "sln", "Solution manifest");
        var readmeFile = new TreeItemNode("README.md", false, MaterialIconKind.Description, Color.FromHex("#F59E0B"), "md", "5 KB");
        var licenseFile = new TreeItemNode("LICENSE", false, MaterialIconKind.Description, Color.FromHex("#6B7280"), "txt", "1 KB");

        RootNodes.Add(srcFolder);
        RootNodes.Add(samplesFolder);
        RootNodes.Add(testsFolder);
        RootNodes.Add(solutionFile);
        RootNodes.Add(readmeFile);
        RootNodes.Add(licenseFile);

        SelectedNode = controlsProject.Children.Count > 3 ? controlsProject.Children[3] : srcFolder;
        UpdateTreeStatistics();
        StatusMessage = "Sample tree reset to Atelier Project Explorer structure.";
    }

    private void UpdateTreeStatistics()
    {
        RootNodeCount = RootNodes.Count;
        int total = RootNodes.Count;
        for (int i = 0; i < RootNodes.Count; i++)
        {
            total += RootNodes[i].TotalDescendantsCount;
        }
        TotalNodeCount = total;
    }

    private static TreeItemNode CreateNodeFromInput(string name, bool isFolder)
    {
        if (isFolder)
        {
            return new TreeItemNode(name, true, MaterialIconKind.Folder, Color.FromHex("#FBBF24"), "dir", "Folder");
        }

        string ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
        return ext switch
        {
            ".cs" => new TreeItemNode(name, false, MaterialIconKind.Code, Color.FromHex("#3B82F6"), "cs", "C# Source"),
            ".md" => new TreeItemNode(name, false, MaterialIconKind.Description, Color.FromHex("#F59E0B"), "md", "Markdown"),
            ".png" or ".jpg" or ".svg" => new TreeItemNode(name, false, MaterialIconKind.Image, Color.FromHex("#EC4899"), "asset", "Image file"),
            ".slnx" or ".sln" or ".csproj" or ".json" => new TreeItemNode(name, false, MaterialIconKind.Settings, Color.FromHex("#8B5CF6"), "conf", "Configuration"),
            _ => new TreeItemNode(name, false, MaterialIconKind.Description, Color.FromHex("#6B7280"), "file", "Document")
        };
    }
}
