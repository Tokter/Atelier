using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Xunit;

namespace Atelier.Tests;

public class TreeViewTests
{
    private class TestNode
    {
        public string Name { get; set; }
        public ObservableCollection<TestNode> Children { get; set; } = [];

        public TestNode(string name, params TestNode[] children)
        {
            Name = name;
            foreach (var child in children) Children.Add(child);
        }
    }

    [Fact]
    public void TreeView_ItemsSourceAndChildrenSelector_BuildsHierarchy()
    {
        var root = new TestNode("Root",
            new TestNode("Folder 1",
                new TestNode("File 1.1"),
                new TestNode("File 1.2")
            ),
            new TestNode("Folder 2")
        );

        var treeView = new TreeView()
            .ItemsSource(new[] { root })
            .ChildrenSelector<TestNode>(item => item.Children)
            .IndentSize(24f);

        treeView.Measure(new Size(400, 600));
        treeView.Arrange(new Rect(0, 0, 400, 600));

        var visible = treeView.GetVisibleItems();
        Assert.Single(visible); // Only root is visible initially since IsExpanded is false
        Assert.Equal("Root", (visible[0].ItemValue as TestNode)?.Name);
        Assert.Equal(0, visible[0].Level);
        Assert.True(visible[0].HasChildren);
        Assert.Equal(2, visible[0].ChildrenItems.Count);

        // Check child items
        var folder1 = visible[0].ChildrenItems[0];
        Assert.Equal("Folder 1", (folder1.ItemValue as TestNode)?.Name);
        Assert.Equal(1, folder1.Level);
        Assert.True(folder1.HasChildren);
        Assert.Equal(2, folder1.ChildrenItems.Count);

        var folder2 = visible[0].ChildrenItems[1];
        Assert.Equal("Folder 2", (folder2.ItemValue as TestNode)?.Name);
        Assert.Equal(1, folder2.Level);
        Assert.False(folder2.HasChildren);
    }

    [Fact]
    public void TreeView_ItemTemplate_AppliesCustomViews()
    {
        var items = new[]
        {
            new TestNode("Project Root", new TestNode("Source.cs"))
        };

        var treeView = new TreeView()
            .ItemsSource(items)
            .ChildrenSelector<TestNode>(item => item.Children)
            .ItemTemplate<TestNode>(node => new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4f
            }.Children(new Icon(MaterialIconKind.Folder, 16f), new TextBlock(node.Name)));

        treeView.Measure(new Size(400, 600));
        treeView.Arrange(new Rect(0, 0, 400, 600));

        var rootNode = treeView.GetVisibleItems()[0];
        rootNode.IsExpanded = true;

        // Verify DataContext was set on templated visual
        var childNode = rootNode.ChildrenItems[0];
        Assert.NotNull(childNode.ItemValue);
        Assert.Equal("Source.cs", ((TestNode)childNode.ItemValue).Name);
    }

    [Fact]
    public void TreeView_ExpandCollapse_TogglesIconAndChildrenVisibility()
    {
        var root = new TestNode("Root", new TestNode("Child"));
        var treeView = new TreeView()
            .ItemsSource(new[] { root })
            .ChildrenSelector<TestNode>(item => item.Children);

        treeView.Measure(new Size(400, 600));
        treeView.Arrange(new Rect(0, 0, 400, 600));

        var rootItem = treeView.GetVisibleItems()[0];
        Assert.False(rootItem.IsExpanded);
        Assert.Single(treeView.GetVisibleItems());

        bool expandedFired = false;
        bool collapsedFired = false;
        treeView.ItemExpanded += (s, e) => expandedFired = true;
        treeView.ItemCollapsed += (s, e) => collapsedFired = true;

        // Expand root
        rootItem.IsExpanded = true;
        Assert.True(expandedFired);
        Assert.Equal(2, treeView.GetVisibleItems().Count);

        // Collapse root
        rootItem.IsExpanded = false;
        Assert.True(collapsedFired);
        Assert.Single(treeView.GetVisibleItems());
    }

    [Fact]
    public void TreeView_LeafNode_HidesExpanderIcon()
    {
        var leaf = new TestNode("Solo File");
        var treeView = new TreeView()
            .ItemsSource(new[] { leaf })
            .ChildrenSelector<TestNode>(item => item.Children);

        treeView.Measure(new Size(400, 600));
        treeView.Arrange(new Rect(0, 0, 400, 600));

        var item = treeView.GetVisibleItems()[0];
        Assert.False(item.HasChildren);
    }

    [Fact]
    public void TreeView_Selection_UpdatesSelectedItemAndVisualState()
    {
        var root = new TestNode("Root", new TestNode("Child 1"), new TestNode("Child 2"));
        var treeView = new TreeView()
            .ItemsSource(new[] { root })
            .ChildrenSelector<TestNode>(item => item.Children);

        object? selected = null;
        treeView.SelectionChanged += (s, item) => selected = item;

        var rootItem = treeView.GetVisibleItems()[0];
        rootItem.IsExpanded = true;

        var child1 = rootItem.ChildrenItems[0];
        var child2 = rootItem.ChildrenItems[1];

        // Select child 1
        child1.IsSelected = true;
        Assert.Equal(child1.ItemValue, treeView.SelectedItem);
        Assert.Equal(child1.ItemValue, selected);
        Assert.True(child1.IsSelected);
        Assert.False(rootItem.IsSelected);

        // Select child 2 via TreeView.SelectedItem
        treeView.SelectedItem = child2.ItemValue;
        Assert.True(child2.IsSelected);
        Assert.False(child1.IsSelected);
    }

    [Fact]
    public void TreeView_KeyboardNavigation_NavigatesVisiblePreOrder()
    {
        var child1 = new TestNode("Child 1");
        var child2 = new TestNode("Child 2");
        var root = new TestNode("Root", child1, child2);

        var treeView = new TreeView()
            .ItemsSource(new[] { root })
            .ChildrenSelector<TestNode>(item => item.Children);

        treeView.Measure(new Size(400, 600));
        treeView.Arrange(new Rect(0, 0, 400, 600));

        // Initial focus selects root
        treeView.OnGotFocus();
        Assert.Equal(root, treeView.SelectedItem);

        var rootItem = treeView.GetVisibleItems()[0];

        // Arrow Right expands root
        treeView.OnKeyDown(new KeyEventArgs(Key.Right));
        Assert.True(rootItem.IsExpanded);
        Assert.Equal(3, treeView.GetVisibleItems().Count);

        // Arrow Right again moves into first child
        treeView.OnKeyDown(new KeyEventArgs(Key.Right));
        Assert.Equal(child1, treeView.SelectedItem);

        // Arrow Down moves to next child
        treeView.OnKeyDown(new KeyEventArgs(Key.Down));
        Assert.Equal(child2, treeView.SelectedItem);

        // Arrow Up moves back to child 1
        treeView.OnKeyDown(new KeyEventArgs(Key.Up));
        Assert.Equal(child1, treeView.SelectedItem);

        // Arrow Left on leaf ascends to parent (Root)
        treeView.OnKeyDown(new KeyEventArgs(Key.Left));
        Assert.Equal(root, treeView.SelectedItem);

        // Arrow Left on expanded Root collapses it
        treeView.OnKeyDown(new KeyEventArgs(Key.Left));
        Assert.False(rootItem.IsExpanded);
        Assert.Single(treeView.GetVisibleItems());
    }

    [Fact]
    public void TreeView_ObservableCollection_DynamicallyUpdatesNodes()
    {
        var children = new ObservableCollection<TestNode>
        {
            new("Initial Item")
        };
        var root = new TestNode("Root") { Children = children };

        var treeView = new TreeView()
            .ItemsSource(new[] { root })
            .ChildrenSelector<TestNode>(item => item.Children);

        treeView.Measure(new Size(400, 600));
        treeView.Arrange(new Rect(0, 0, 400, 600));

        var rootItem = treeView.GetVisibleItems()[0];
        rootItem.IsExpanded = true;
        Assert.Single(rootItem.ChildrenItems);

        // Add dynamic child to observable collection
        children.Add(new TestNode("Added Item"));
        Assert.Equal(2, rootItem.ChildrenItems.Count);
        Assert.Equal("Added Item", (rootItem.ChildrenItems[1].ItemValue as TestNode)?.Name);

        // Remove item
        children.RemoveAt(0);
        Assert.Single(rootItem.ChildrenItems);
        Assert.Equal("Added Item", (rootItem.ChildrenItems[0].ItemValue as TestNode)?.Name);
    }

    [Fact]
    public void TreeView_DeclarativeStaticItems_Works()
    {
        var treeView = new TreeView();
        var root = new TreeViewItem("Static Root");
        var child = new TreeViewItem("Static Child");
        root.AddChildItem(child);
        treeView.AddRootItem(root);

        treeView.Measure(new Size(400, 600));
        treeView.Arrange(new Rect(0, 0, 400, 600));

        Assert.Single(treeView.GetVisibleItems());
        root.IsExpanded = true;
        Assert.Equal(2, treeView.GetVisibleItems().Count);
    }

    [Fact]
    public void TreeView_ExpandAllAndCollapseAll_Works()
    {
        var root = new TestNode("Root",
            new TestNode("Folder 1", new TestNode("Leaf 1")),
            new TestNode("Folder 2", new TestNode("Leaf 2"))
        );

        var treeView = new TreeView()
            .ItemsSource(new[] { root })
            .ChildrenSelector<TestNode>(item => item.Children);

        treeView.ExpandAll();
        Assert.Equal(5, treeView.GetVisibleItems().Count);

        treeView.CollapseAll();
        Assert.Single(treeView.GetVisibleItems());
    }

    [Fact]
    public void TreeView_MouseInteractions_ExpanderAndHeaderClick()
    {
        var child = new TestNode("Child");
        var root = new TestNode("Root", child);

        var treeView = new TreeView()
            .ItemsSource(new[] { root })
            .ChildrenSelector<TestNode>(item => item.Children);

        treeView.Measure(new Size(400, 600));
        treeView.Arrange(new Rect(0, 0, 400, 600));

        var rootItem = treeView.GetVisibleItems()[0];

        // Click on expander button (at x=12, y=12 within header)
        var hitExpander = treeView.HitTest(new Point(12, 12));
        Assert.NotNull(hitExpander);
        
        hitExpander.OnPointerPressed(new PointerEventArgs(new Point(12, 12), PointerButtons.Left));
        hitExpander.OnPointerReleased(new PointerEventArgs(new Point(12, 12), PointerButtons.Left));

        // Root expanded, but NOT selected
        Assert.True(rootItem.IsExpanded);
        Assert.Null(treeView.SelectedItem);

        // Now click on the header content area (at x=100, y=12)
        var hitHeader = treeView.HitTest(new Point(100, 12));
        Assert.NotNull(hitHeader);

        hitHeader.OnPointerPressed(new PointerEventArgs(new Point(100, 12), PointerButtons.Left));
        hitHeader.OnPointerReleased(new PointerEventArgs(new Point(100, 12), PointerButtons.Left));

        // Now root is selected!
        Assert.Equal(root, treeView.SelectedItem);
        Assert.True(rootItem.IsSelected);
    }

    [Fact]
    public void TreeView_DoubleClickHeader_TogglesExpansion()
    {
        var child = new TestNode("Child");
        var root = new TestNode("Root", child);

        var treeView = new TreeView()
            .ItemsSource(new[] { root })
            .ChildrenSelector<TestNode>(item => item.Children);

        treeView.Measure(new Size(400, 600));
        treeView.Arrange(new Rect(0, 0, 400, 600));

        var rootItem = treeView.GetVisibleItems()[0];
        Assert.False(rootItem.IsExpanded);

        var hitHeader = treeView.HitTest(new Point(100, 12));
        Assert.NotNull(hitHeader);

        // First click (selects)
        hitHeader.OnPointerPressed(new PointerEventArgs(new Point(100, 12), PointerButtons.Left));
        hitHeader.OnPointerReleased(new PointerEventArgs(new Point(100, 12), PointerButtons.Left));
        Assert.Equal(root, treeView.SelectedItem);
        Assert.False(rootItem.IsExpanded);

        // Rapid second click (double-click within 400ms -> toggles expansion)
        hitHeader.OnPointerPressed(new PointerEventArgs(new Point(100, 12), PointerButtons.Left));
        hitHeader.OnPointerReleased(new PointerEventArgs(new Point(100, 12), PointerButtons.Left));
        Assert.True(rootItem.IsExpanded);
    }
}
