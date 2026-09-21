using System;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Theming;
using Atelier.Theming.Material;
using Atelier.Gallery.ViewModels;

namespace Atelier.Gallery.Views;

public class TreeViewView : Grid
{
    private readonly TreeViewViewModel _viewModel;
    private readonly ScrollViewer _scrollViewer;
    private readonly TreeView _treeView;

    public TreeViewView() : this(new TreeViewViewModel())
    {
    }

    public TreeViewView(TreeViewViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;

        this.Rows(GridLength.Auto, GridLength.Star);
        this.RowSpacing(16);

        // 1. Master Controls & Quick Actions Banner
        this.Add(CreateMasterBanner().Row(0));

        // 2. TreeView control setup
        _treeView = new TreeView()
            .IndentSize(_viewModel.IndentSize)
            .ChildrenSelector<TreeItemNode>(node => node.Children)
            .WithItemTemplate<TreeItemNode>(CreateNodeTemplate);

        _treeView.ItemsSource = _viewModel.RootNodes;
        _treeView.BindSelectedItem(_viewModel, vm => vm.SelectedNode, (vm, v) => vm.SelectedNode = v as TreeItemNode);

        _treeView.SelectionChanged += (s, item) =>
        {
            if (item is TreeItemNode node && _viewModel.SelectedNode != node)
            {
                _viewModel.SelectedNode = node;
            }
        };

        _viewModel.RequestExpandAll = () => _treeView.ExpandAll();
        _viewModel.RequestCollapseAll = () => _treeView.CollapseAll();

        // 3. Scrollable Showcase Layout
        var showcaseGrid = new Grid()
            .Columns(GridLength.Pixels(450), GridLength.Star)
            .ColumnSpacing(16);

        showcaseGrid.Add(CreateTreeCard().Column(0));
        showcaseGrid.Add(CreateDetailsStack().Column(1));

        showcaseGrid.Margin = new Thickness(0, 0, 10, 20);

        _scrollViewer = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = showcaseGrid
        }.Row(1);

        this.Add(_scrollViewer);
    }

    public override void OnPointerWheel(PointerWheelEventArgs e)
    {
        base.OnPointerWheel(e);
        if (!e.Handled && _scrollViewer != null)
        {
            _scrollViewer.OnPointerWheel(e);
        }
    }

    private UIElement CreateMasterBanner()
    {
        var card = new Card(CardVariant.Filled)
            .Padding(20)
            .CornerRadius(14);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 14 };

        // Header text
        stack.Add(new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 }
            .Children(
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
                    .Children(
                        new Icon(MaterialIconKind.AccountTree, 28) { Foreground = Color.FromHex("#1E88E5"), VerticalAlignment = VerticalAlignment.Center },
                        new TextBlock("TreeView (Material Design 3)").TitleLarge()
                    ),
                new TextBlock("Hierarchical data presentation featuring expandable nodes, custom item templates, dynamic node insertion/deletion, selection tracking, and full keyboard navigation.")
                    .Subtext()
            )
        );

        // Quick action buttons
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Button("Expand All")
                    .Variant(ButtonVariant.Filled)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ExpandAllCommand),

                new Button("Collapse All")
                    .Variant(ButtonVariant.Tonal)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.CollapseAllCommand),

                new Button("Reset Sample Tree")
                    .Variant(ButtonVariant.Outlined)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.ResetTreeCommand)
            );

        stack.Add(actionRow);
        card.Child = stack;
        return card;
    }

    private UIElement CreateTreeCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(18)
            .CornerRadius(12);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 };

        // Card Title with node count badge
        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.FolderOpen, 22) { Foreground = Color.FromHex("#FBBF24"), VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Project Explorer").TitleMedium().VerticalAlign(VerticalAlignment.Center),
                new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 2),
                    Background = Color.FromHex("#1E88E5").WithAlpha(0.12f),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock()
                        .Caption()
                        .Foreground(Color.FromHex("#1E88E5"))
                        .Bold()
                        .BindText(_viewModel, vm => $"{vm.TotalNodeCount} items")
                }
            );
        stack.Add(headerRow);

        // Action Toolbar
        var toolbar = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 };

        var inputRow = new Grid()
            .Columns(GridLength.Star, GridLength.Auto)
            .ColumnSpacing(8);

        var nameBox = new TextBox()
            .Placeholder("Item name (e.g. Component.cs)")
            .BindText(_viewModel, vm => vm.NewItemName, (vm, v) => vm.NewItemName = v)
            .Column(0);

        var folderSwitch = new Switch("Folder")
            .BindIsChecked(_viewModel, vm => vm.NewItemIsFolder, (vm, v) => vm.NewItemIsFolder = v)
            .Column(1);

        inputRow.Add(nameBox);
        inputRow.Add(folderSwitch);
        toolbar.Add(inputRow);

        var buttonsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Button("Add Child")
                    .Variant(ButtonVariant.Filled)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.AddChildCommand),

                new Button("Add Root")
                    .Variant(ButtonVariant.Tonal)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.AddRootCommand),

                new Button("Delete")
                    .Variant(ButtonVariant.Outlined)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.RemoveSelectedCommand)
            );
        toolbar.Add(buttonsRow);
        stack.Add(toolbar);

        // TreeView Display Box
        var treeBox = new Border
        {
            Height = 440,
            CornerRadius = new CornerRadius(8),
            Background = Color.FromRgb(128, 128, 128).WithAlpha(0.05f),
            BorderBrush = Color.FromRgb(128, 128, 128).WithAlpha(0.2f),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(6),
            Child = _treeView
        };
        stack.Add(treeBox);

        // Status banner
        var statusBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6),
            Background = Color.FromRgb(128, 128, 128).WithAlpha(0.08f),
            Child = new TextBlock()
                .Caption()
                .BindText(_viewModel, vm => vm.StatusMessage)
        };
        stack.Add(statusBorder);

        card.Child = stack;
        return card;
    }

    private UIElement CreateDetailsStack()
    {
        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 16 };

        stack.Add(CreateInspectorCard());
        stack.Add(CreateConfigurationCard());
        stack.Add(CreateKeyboardGuideCard());

        return stack;
    }

    private UIElement CreateInspectorCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(18)
            .CornerRadius(12);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 };

        // Header
        stack.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Search, 20) { Foreground = Color.FromHex("#8B5CF6"), VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Node Inspector").TitleMedium()
            )
        );

        // Node Name edit
        var nameLabel = new TextBlock("Node Name (Real-time Rename):").LabelSmall();
        var renameBox = new TextBox()
            .Placeholder("Select a node to edit name...")
            .BindText(_viewModel, vm => vm.SelectedNodeName, (vm, v) => vm.SelectedNodeName = v);

        // Path Display
        var pathLabel = new TextBlock("Full Hierarchical Path:").LabelSmall();
        var pathBorder = new Border
        {
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6),
            Background = Color.FromRgb(128, 128, 128).WithAlpha(0.1f),
            Child = new TextBlock()
                .Caption()
                .Bold()
                .BindText(_viewModel, vm => vm.SelectedNode != null ? vm.SelectedNode.FullPath : "No node selected")
        };

        // Node Details Grid
        var detailsGrid = new Grid()
            .Columns(GridLength.Star, GridLength.Star)
            .ColumnSpacing(12)
            .RowSpacing(8);

        detailsGrid.Rows(GridLength.Auto, GridLength.Auto);

        var typeText = new TextBlock()
            .Caption()
            .BindText(_viewModel, vm => vm.SelectedNode != null
                ? $"Type: {(vm.SelectedNode.IsDirectory ? "Directory / Folder" : "File Component")}"
                : "Type: -")
            .Row(0).Column(0);

        var badgeText = new TextBlock()
            .Caption()
            .BindText(_viewModel, vm => vm.SelectedNode != null
                ? $"Tag / Badge: {vm.SelectedNode.Badge ?? "none"}"
                : "Tag / Badge: -")
            .Row(0).Column(1);

        var childrenCountText = new TextBlock()
            .Caption()
            .BindText(_viewModel, vm => vm.SelectedNode != null
                ? $"Direct Children: {vm.SelectedNode.Children.Count}"
                : "Direct Children: -")
            .Row(1).Column(0);

        var descendantsCountText = new TextBlock()
            .Caption()
            .BindText(_viewModel, vm => vm.SelectedNode != null
                ? $"Total Descendants: {vm.SelectedNode.TotalDescendantsCount}"
                : "Total Descendants: -")
            .Row(1).Column(1);

        detailsGrid.Add(typeText);
        detailsGrid.Add(badgeText);
        detailsGrid.Add(childrenCountText);
        detailsGrid.Add(descendantsCountText);

        // Actions
        var actionsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Button("Add Sub-item")
                    .Variant(ButtonVariant.Tonal)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.AddChildCommand),

                new Button("Delete Node")
                    .Variant(ButtonVariant.Outlined)
                    .VerticalAlign(VerticalAlignment.Center)
                    .Command(_viewModel.RemoveSelectedCommand)
            );

        stack.Add(nameLabel);
        stack.Add(renameBox);
        stack.Add(pathLabel);
        stack.Add(pathBorder);
        stack.Add(detailsGrid);
        stack.Add(actionsRow);

        card.Child = stack;
        return card;
    }

    private UIElement CreateConfigurationCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(18)
            .CornerRadius(12);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 12 };

        stack.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Tune, 20) { Foreground = Color.FromHex("#10B981"), VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Appearance & Layout").TitleMedium()
            )
        );

        // Indent Slider
        var indentLabel = new TextBlock($"Level Indentation: {_viewModel.IndentSize:F0} dp").LabelSmall();
        var indentSlider = new Slider
        {
            Minimum = 10,
            Maximum = 36,
            Value = _viewModel.IndentSize
        };

        indentSlider.ValueChanged += (s, val) =>
        {
            _viewModel.IndentSize = val;
            _treeView.IndentSize = val;
            indentLabel.Text = $"Level Indentation: {val:F0} dp";
        };

        // Stats Row
        var statsRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new TextBlock().Caption().BindText(_viewModel, vm => $"Total Nodes: {vm.TotalNodeCount}"),
                new TextBlock().Caption().BindText(_viewModel, vm => $"Root Items: {vm.RootNodeCount}"),
                new TextBlock().Caption().BindText(_viewModel, vm => $"Default Indent: 20 dp")
            );

        stack.Add(indentLabel);
        stack.Add(indentSlider);
        stack.Add(statsRow);

        card.Child = stack;
        return card;
    }

    private static UIElement CreateKeyboardGuideCard()
    {
        var card = new Card(CardVariant.Outlined)
            .Padding(18)
            .CornerRadius(12);

        var stack = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 };

        stack.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center }
            .Children(
                new Icon(MaterialIconKind.Keyboard, 20) { Foreground = Color.FromHex("#EC4899"), VerticalAlignment = VerticalAlignment.Center },
                new TextBlock("Keyboard Navigation & Gestures").TitleMedium()
            )
        );

        static UIElement CreateShortcutRow(string key, string description)
        {
            var keyBadge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2),
                Background = Color.FromRgb(128, 128, 128).WithAlpha(0.16f),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock(key).Caption().Bold().VerticalAlign(VerticalAlignment.Center)
            };

            var descText = new TextBlock(description).Caption().VerticalAlign(VerticalAlignment.Center);

            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                VerticalAlignment = VerticalAlignment.Center
            }.Children(keyBadge, descText);
        }

        stack.Add(CreateShortcutRow("↑ / ↓", "Navigate up and down across visible tree nodes"));
        stack.Add(CreateShortcutRow("→", "Expand collapsed node, or jump to its first child"));
        stack.Add(CreateShortcutRow("←", "Collapse expanded node, or jump back to its parent"));
        stack.Add(CreateShortcutRow("Home / End", "Jump to the first or last visible node"));
        stack.Add(CreateShortcutRow("Enter / Space", "Toggle expansion of the selected node"));
        stack.Add(CreateShortcutRow("Double-Click", "Quickly expand or collapse any parent node"));

        card.Child = stack;
        return card;
    }

    private static UIElement CreateNodeTemplate(TreeItemNode node)
    {
        var icon = new Icon(node.Icon, 18)
        {
            Foreground = node.IconColor ?? Color.FromHex("#FBBF24"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var nameText = new TextBlock(node.Name)
            .LabelMedium()
            .VerticalAlign(VerticalAlignment.Center);
        nameText.BindText(node, n => n.Name);

        var badge = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 1),
            Background = Color.FromRgb(128, 128, 128).WithAlpha(0.12f),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock(node.Badge ?? string.Empty)
                .Caption()
                .VerticalAlign(VerticalAlignment.Center)
        };

        var detailsText = new TextBlock(node.Details)
            .Caption()
            .Muted()
            .VerticalAlign(VerticalAlignment.Center);
        detailsText.BindText(node, n => n.Details);

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        }.Children(icon, nameText, badge, detailsText);
    }
}
