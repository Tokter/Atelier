using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class TreeViewView : GalleryPage
{
    private readonly TreeViewViewModel _vm;
    private readonly TreeView _explorer;

    public TreeViewView(TreeViewViewModel viewModel)
        : base(MaterialIconKind.AccountTree, "TreeView",
            "TreeView shows hierarchical data as expandable nodes, with single selection and keyboard navigation. Nodes " +
            "are generated from a data source and a template, or built from TreeViewItems.")
    {
        _vm = viewModel;

        _explorer = new TreeView()
            .Height(400)
            .BindItemsSource(_vm, v => v.RootNodes)
            .WithChildrenSelector((TreeItemNode node) => node.Children)
            .WithItemTemplate((TreeItemNode node) => NodeRow(node))
            .BindSelectedItem(_vm, v => v.SelectedNode, (v, node) => v.SelectedNode = node)
            .Bind(TreeView.IndentSizeProperty, _vm, v => v.IndentSize)
            .Bind(TreeView.IconSizeProperty, _vm, v => v.IconSize)
            .Bind(TreeView.ExpandIconProperty, _vm, v => v.UseArrowIcons ? MaterialIconKind.ArrowDropDown : MaterialIconKind.ExpandMore)
            .Bind(TreeView.CollapseIconProperty, _vm, v => v.UseArrowIcons ? MaterialIconKind.ArrowRight : MaterialIconKind.ChevronRight)
            .OnItemExpanded(item => _vm.LastEvent = $"ItemExpanded → {item.ItemValue}")
            .OnItemCollapsed(item => _vm.LastEvent = $"ItemCollapsed → {item.ItemValue}")
            .OnSelectionChanged(item => _vm.LastEvent = $"SelectionChanged → {item?.ToString() ?? "null"}");

        // Child nodes are created when a node is first expanded, so reveal the initially selected node's path.
        RevealSelection(_explorer.RootItems);
        _vm.LastEvent = "Expand, collapse or select a node";

        Settings(
            new Switch("Controls enabled").ShowThumbIcon().BindIsChecked(_vm, v => v.ControlsEnabled, (v, on) => v.ControlsEnabled = on),
            new Button("Reset").Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));

        SectionsPanel.BindIsEnabled(_vm, v => v.ControlsEnabled);

        Sections(ExplorerSection(), ManualSection());
    }

    private UIElement ExplorerSection() => Ui.Section("Data-bound tree",
        "The roots come from an ObservableCollection, the children from a selector, and each header from a typed " +
        "template. Adding and removing items updates the tree in place; expansion and selection are kept.",
        Ui.Columns(360,
            _explorer,
            Ui.Stack(
                Ui.Demo("Selected node",
                    Ui.Readout(_vm, v => $"Path = {v.SelectedNode?.FullPath ?? "(none)"}"),
                    Ui.Readout(_vm, v => v.SelectedNode is { } n ? $"{n.Kind} · {n.Details} · {n.Children.Count} children" : "Nothing selected"),
                    Ui.Readout(_vm, v => v.LastEvent)),
                Ui.Demo("Change the tree",
                    Ui.Row(
                        new TextBox().Placeholder("Name").Width(200).BindText(_vm, v => v.NewItemName, (v, text) => v.NewItemName = text),
                        Ui.IconButton(MaterialIconKind.Add, "Add", ButtonVariant.Tonal).Command(_vm.AddItemCommand),
                        Ui.IconButton(MaterialIconKind.Delete, "Remove", ButtonVariant.Outlined).Command(_vm.RemoveSelectedCommand)),
                    Ui.Note("Names without an extension become folders. New items go into the selected folder."),
                    Ui.Row(
                        Ui.IconButton(MaterialIconKind.UnfoldMore, "Expand all", ButtonVariant.Text).OnClick(() => _explorer.ExpandAll()),
                        Ui.IconButton(MaterialIconKind.UnfoldLess, "Collapse all", ButtonVariant.Text).OnClick(() => _explorer.CollapseAll()),
                        new Button("Restore sample").Variant(ButtonVariant.Text).Command(_vm.ResetTreeCommand))),
                Ui.Demo("Appearance",
                    Ui.Columns(200,
                        Ui.SliderSetting("Indent", _vm, v => v.IndentSize, (v, value) => v.IndentSize = value, 8, 40, "0 px", 1),
                        Ui.SliderSetting("Expander size", _vm, v => v.IconSize, (v, value) => v.IconSize = value, 12, 28, "0 px", 1)),
                    new Switch("Arrow expander icons").BindIsChecked(_vm, v => v.UseArrowIcons, (v, on) => v.UseArrowIcons = on)))),

        Ui.Note("Keyboard: Up/Down move through visible nodes, Right expands or moves to the first child, Left collapses " +
                "or moves to the parent, Enter/Space toggle, numpad * expands the whole subtree."),

        Ui.Code("new TreeView()\n" +
                "    .BindItemsSource(vm, v => v.RootNodes)\n" +
                "    .WithChildrenSelector((TreeItemNode node) => node.Children)\n" +
                "    .WithItemTemplate((TreeItemNode node) => NodeRow(node))\n" +
                "    .BindSelectedItem(vm, v => v.SelectedNode, (v, node) => v.SelectedNode = node)"));

    // Expands the nodes on the path to the view model's selected node.
    private bool RevealSelection(System.Collections.Generic.IReadOnlyList<TreeViewItem> items)
    {
        foreach (var item in items)
        {
            if (ReferenceEquals(item.ItemValue, _vm.SelectedNode))
            {
                return true;
            }

            if (item.HasChildren && item.ItemValue is TreeItemNode { IsFolder: true } && RevealSelection(item.ChildrenItems))
            {
                item.IsExpanded = true;
                return true;
            }
        }
        return false;
    }

    private static StackPanel NodeRow(TreeItemNode node) =>
        new StackPanel()
            .Orientation(Orientation.Horizontal)
            .Spacing(8)
            .Children(
                new Icon(node.Icon, 20)
                    .VerticalAlignment(VerticalAlignment.Center)
                    .Themed(Control.ForegroundProperty, c => node.Kind switch
                    {
                        FileKind.Folder => c.Tertiary,
                        FileKind.Code => c.Primary,
                        FileKind.Image => c.Secondary,
                        _ => c.OnSurfaceVariant
                    }),
                new TextBlock().VerticalAlignment(VerticalAlignment.Center).BindText(node, n => n.Name),
                new TextBlock(node.IsFolder ? node.Details : string.Empty)
                    .BodySmall()
                    .Muted()
                    .VerticalAlignment(VerticalAlignment.Center));

    private UIElement ManualSection() => Ui.Section("Built from TreeViewItems",
        "Without an ItemsSource, nodes are TreeViewItems added with RootItems and ChildrenItems. Each item can start " +
        "expanded and reports its own Expanded and Collapsed events.",
        Ui.Columns(300,
            Ui.Demo("Settings tree",
                new TreeView()
                    .RootItems(
                        Node("Appearance", MaterialIconKind.Palette).IsExpanded().ChildrenItems(
                            Node("Theme", MaterialIconKind.DarkMode),
                            Node("Fonts", MaterialIconKind.TextFields)),
                        Node("Notifications", MaterialIconKind.Notifications).ChildrenItems(
                            Node("Email", MaterialIconKind.Mail),
                            Node("Sounds", MaterialIconKind.VolumeUp)),
                        Node("Privacy", MaterialIconKind.Lock)
                            .OnExpanded(() => _vm.LastEvent = "Privacy.Expanded")
                            .OnCollapsed(() => _vm.LastEvent = "Privacy.Collapsed")
                            .ChildrenItems(Node("Permissions", MaterialIconKind.Security)))
                    .BindSelectedItem(_vm, v => v.ManualSelection, (v, item) => v.ManualSelection = item),
                Ui.Readout(_vm, v => $"SelectedItem = {v.ManualSelection ?? "null"}")),

            Ui.Demo("Plain text nodes",
                new TreeView()
                    .IndentSize(28)
                    .ExpandIcon(MaterialIconKind.Remove)
                    .CollapseIcon(MaterialIconKind.Add)
                    .IconSize(16)
                    .RootItems(
                        new TreeViewItem("Fruit").IsExpanded().ChildrenItems(
                            new TreeViewItem("Apple"),
                            new TreeViewItem("Banana")),
                        new TreeViewItem("Vegetables").ChildrenItems(
                            new TreeViewItem("Carrot"),
                            new TreeViewItem("Leek"))),
                Ui.Note("A 28 px indent and +/− expander icons at 16 px.")),

            Ui.Demo("Disabled",
                new TreeView()
                    .IsEnabled(false)
                    .RootItems(new TreeViewItem("Archive").IsExpanded().ChildrenItems(
                        new TreeViewItem("2024"),
                        new TreeViewItem("2025"))))));

    private static TreeViewItem Node(string text, MaterialIconKind icon) =>
        new TreeViewItem().Configure(item =>
        {
            item.ItemValue = text;
            item.SetContentVisual(new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Children(
                new Icon(icon, 20).VerticalAlignment(VerticalAlignment.Center).Themed(Control.ForegroundProperty, c => c.OnSurfaceVariant),
                new TextBlock(text).VerticalAlignment(VerticalAlignment.Center)));
        });
}
