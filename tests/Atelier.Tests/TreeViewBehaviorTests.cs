using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Xunit;

namespace Atelier.Tests;

public class TreeViewBehaviorTests
{
    private class Node
    {
        public string Name { get; }
        public CountingObservableCollection<Node> Children { get; } = [];

        public Node(string name, params Node[] children)
        {
            Name = name;
            foreach (var child in children) Children.Add(child);
        }

        public override string ToString() => Name;
    }

    private static TreeView CreateTree(IEnumerable<Node> roots, Func<object, IEnumerable<Node>?>? selector = null)
    {
        var tree = new TreeView
        {
            ChildrenSelector = selector ?? (item => ((Node)item).Children),
            ItemsSource = roots
        };
        Layout(tree);
        return tree;
    }

    private static void Layout(UIElement element, float width = 400, float height = 600)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
    }

    private static TreeViewItem NodeFor(TreeView tree, string name)
    {
        foreach (var node in tree.GetAllNodes())
        {
            if (node.ItemValue is Node n && n.Name == name) return node;
        }
        throw new InvalidOperationException($"No node for {name}");
    }

    // TreeViewItem -> root StackPanel -> header Border -> header StackPanel -> [indent spacer, expander button, content]
    private static UIElement HeaderPart(TreeViewItem item, int index) =>
        (UIElement)item.Children[0].Children[0].Children[0].Children[index];

    [Fact]
    public void ExpansionState_SurvivesAddRemoveAndMoveOfSiblings()
    {
        var folder = new Node("Folder", new Node("File"));
        var roots = new ObservableCollection<Node> { new("First"), folder };
        var tree = CreateTree(roots);
        var folderItem = NodeFor(tree, "Folder");
        folderItem.IsExpanded = true;

        roots.Insert(0, new Node("Inserted"));
        roots.RemoveAt(1);
        roots.Move(1, 0);

        Assert.Same(folderItem, tree.RootItems[0]);
        Assert.True(folderItem.IsExpanded);
        Assert.Equal(3, tree.GetVisibleItems().Count); // Folder, File, Inserted
    }

    [Fact]
    public void ExpansionState_SurvivesReset_ForItemsStillPresent()
    {
        var folder = new Node("Folder", new Node("File"));
        var other = new Node("Other");
        var roots = new CountingObservableCollection<Node>([folder, other]);
        var tree = CreateTree(roots);
        var folderItem = NodeFor(tree, "Folder");
        folderItem.IsExpanded = true;
        var otherItem = NodeFor(tree, "Other");

        roots.ResetTo([new Node("New"), folder]);

        Assert.Same(folderItem, tree.RootItems[1]);
        Assert.True(folderItem.IsExpanded);
        Assert.Null(otherItem.ParentTreeView); // discarded
    }

    [Fact]
    public void ChildCollectionChanges_AreIncremental()
    {
        var sub = new Node("Sub", new Node("Leaf"));
        var root = new Node("Root", sub);
        var tree = CreateTree([root]);
        var rootItem = NodeFor(tree, "Root");
        rootItem.IsExpanded = true;
        var subItem = NodeFor(tree, "Sub");
        subItem.IsExpanded = true;

        root.Children.Insert(0, new Node("Before"));
        root.Children[0] = new Node("Replaced");

        Assert.Same(subItem, rootItem.ChildrenItems[1]);
        Assert.True(subItem.IsExpanded);
        Assert.Equal("Replaced", ((Node)rootItem.ChildrenItems[0].ItemValue!).Name);
        Assert.Equal(1, rootItem.ChildrenItems[0].Level);
    }

    [Fact]
    public void CollapsingAncestorOfSelection_SelectsTheAncestor()
    {
        var leaf = new Node("Leaf");
        var root = new Node("Root", new Node("Folder", leaf));
        var tree = CreateTree([root]);
        tree.ExpandAll();
        tree.SelectedItem = leaf;
        var raised = new List<object?>();
        tree.SelectionChanged += (s, item) => raised.Add(item);

        NodeFor(tree, "Root").IsExpanded = false;

        Assert.Same(root, tree.SelectedItem);
        Assert.True(NodeFor(tree, "Root").IsSelected);
        Assert.False(NodeFor(tree, "Leaf").IsSelected);
        Assert.Equal(new object?[] { root }, raised);
    }

    [Fact]
    public void RemovingSelectedItem_ClearsSelection()
    {
        var child = new Node("Child");
        var root = new Node("Root", child, new Node("Other"));
        var tree = CreateTree([root]);
        tree.ExpandAll();
        tree.SelectedItem = child;
        var raised = new List<object?>();
        tree.SelectionChanged += (s, item) => raised.Add(item);

        root.Children.Remove(child);

        Assert.Null(tree.SelectedItem);
        Assert.Null(tree.SelectedNode);
        Assert.Equal(new object?[] { null }, raised);
    }

    [Fact]
    public void RemovingAncestorOfSelection_ClearsSelection()
    {
        var leaf = new Node("Leaf");
        var folder = new Node("Folder", leaf);
        var roots = new ObservableCollection<Node> { folder };
        var tree = CreateTree(roots);
        tree.ExpandAll();
        tree.SelectedItem = leaf;

        roots.Clear();

        Assert.Null(tree.SelectedItem);
    }

    [Fact]
    public void ManualTree_ChildAddedBeforeRoot_GetsTreeRecursively()
    {
        var tree = new TreeView();
        var root = new TreeViewItem("Root");
        var child = new TreeViewItem("Child");
        var grandChild = new TreeViewItem("GrandChild");
        child.AddChildItem(grandChild);
        root.AddChildItem(child);
        tree.AddRootItem(root);
        root.IsExpanded = true;
        Layout(tree);

        Assert.Same(tree, child.ParentTreeView);
        Assert.Same(tree, grandChild.ParentTreeView);
        Assert.Equal(2, grandChild.Level);

        // Clicking the child's header selects it.
        var header = (UIElement)child.Children[0].Children[0];
        header.OnPointerPressed(new PointerEventArgs(Point.Zero, PointerButtons.Left));
        header.OnPointerReleased(new PointerEventArgs(Point.Zero, PointerButtons.Left));
        Assert.Equal("Child", tree.SelectedItem);
    }

    [Fact]
    public void ManualNode_MarkedSelected_BecomesTreeSelection()
    {
        var tree = new TreeView();
        var root = new TreeViewItem("Root") { IsSelected = true };

        tree.AddRootItem(root);

        Assert.Same(root, tree.SelectedNode);
        Assert.Equal("Root", tree.SelectedItem);
    }

    [Fact]
    public void IndentSizeChange_AfterLayout_ReachesExistingItems()
    {
        var tree = CreateTree([new Node("Root", new Node("Child"))]);
        tree.ExpandAll();
        Layout(tree);
        var child = NodeFor(tree, "Child");
        Assert.Equal(20f, HeaderPart(child, 0).Width);

        tree.IndentSize = 32f;
        Layout(tree);

        Assert.Equal(32f, HeaderPart(child, 0).Width);
        Assert.Equal(0f, HeaderPart(NodeFor(tree, "Root"), 0).Width);
    }

    [Fact]
    public void IconChanges_AfterLayout_RefreshExistingItems()
    {
        var tree = CreateTree([new Node("Root", new Node("Child"))]);
        var root = NodeFor(tree, "Root");
        var icon = (Icon)HeaderPart(root, 1).Children[0];
        Assert.Equal(MaterialIconKind.ChevronRight, icon.Kind);

        tree.CollapseIcon = MaterialIconKind.Add;
        tree.IconSize = 24f;

        Assert.Equal(MaterialIconKind.Add, icon.Kind);
        Assert.Equal(24f, icon.Size);
    }

    [Fact]
    public void Children_AreCreatedLazily_OnFirstExpand()
    {
        var calls = new List<string>();
        var root = new Node("Root", new Node("Folder", new Node("Deep")), new Node("Empty"));
        var tree = CreateTree([root], item =>
        {
            calls.Add(((Node)item).Name);
            return ((Node)item).Children;
        });

        var rootItem = tree.RootItems[0];
        Assert.Equal(new[] { "Root" }, calls);
        Assert.True(rootItem.HasChildren);
        Assert.Single(tree.GetAllNodes());

        rootItem.IsExpanded = true;

        Assert.Equal(new[] { "Root", "Folder", "Empty" }, calls);
        Assert.True(NodeFor(tree, "Folder").HasChildren);
        Assert.False(NodeFor(tree, "Empty").HasChildren);
        Assert.Equal(3, tree.GetAllNodes().Count);
    }

    [Fact]
    public void LazyNode_HasChildren_FollowsCollectionChanges()
    {
        var root = new Node("Root");
        var tree = CreateTree([root]);
        var rootItem = tree.RootItems[0];
        Assert.False(rootItem.HasChildren);

        root.Children.Add(new Node("Late"));

        Assert.True(rootItem.HasChildren);
        Assert.Single(tree.GetAllNodes()); // still not generated
        rootItem.IsExpanded = true;
        Assert.Equal(2, tree.GetAllNodes().Count);
    }

    [Fact]
    public void PendingSelection_IsAppliedWhenNodeIsGenerated()
    {
        var deep = new Node("Deep");
        var tree = CreateTree([new Node("Root", deep)]);

        tree.SelectedItem = deep;
        Assert.Null(tree.SelectedNode);

        tree.RootItems[0].IsExpanded = true;

        Assert.NotNull(tree.SelectedNode);
        Assert.Same(deep, tree.SelectedNode!.ItemValue);
        Assert.True(tree.SelectedNode.IsSelected);
    }

    [Fact]
    public void SettingIsSelectedFalse_ClearsSelectedItem()
    {
        var tree = CreateTree([new Node("A"), new Node("B")]);
        var a = tree.RootItems[0];
        a.IsSelected = true;
        Assert.NotNull(tree.SelectedItem);

        a.IsSelected = false;

        Assert.Null(tree.SelectedItem);
        Assert.Null(tree.SelectedNode);
    }

    [Fact]
    public void DuplicateItems_OnlyOneNodeIsSelected()
    {
        var tree = new TreeView { ItemsSource = new[] { "same", "other", "same" } };

        tree.SelectedItem = "same";

        Assert.True(tree.RootItems[0].IsSelected);
        Assert.False(tree.RootItems[2].IsSelected);

        tree.RootItems[2].IsSelected = true;
        Assert.False(tree.RootItems[0].IsSelected);
        Assert.Same(tree.RootItems[2], tree.SelectedNode);
    }

    [Fact]
    public void RightAndMiddleClicks_DoNotSelectOrToggle()
    {
        var tree = CreateTree([new Node("Root", new Node("Child"))]);
        var root = tree.RootItems[0];
        var header = (UIElement)root.Children[0].Children[0];
        var expander = HeaderPart(root, 1);

        foreach (var button in new[] { PointerButtons.Right, PointerButtons.Middle })
        {
            header.OnPointerPressed(new PointerEventArgs(Point.Zero, button));
            header.OnPointerReleased(new PointerEventArgs(Point.Zero, button));
            expander.OnPointerPressed(new PointerEventArgs(Point.Zero, button));
            expander.OnPointerReleased(new PointerEventArgs(Point.Zero, button));
        }

        Assert.Null(tree.SelectedItem);
        Assert.False(root.IsExpanded);
    }

    [Fact]
    public void ScrollIntoView_AfterExpandingAncestors_WaitsForLayout()
    {
        var roots = new List<Node>();
        for (int i = 0; i < 30; i++) roots.Add(new Node($"Root {i}", new Node($"Child {i}")));
        var tree = CreateTree(roots);
        Layout(tree, 400, 150);

        var last = tree.RootItems[29].ChildrenItems[0];
        last.EnsureVisible();
        tree.ScrollIntoView(last); // layout is pending here: the node has no position yet
        Assert.Equal(0, tree.ScrollViewer.ScrollOffsetY);

        Layout(tree, 400, 150);

        float offset = tree.ScrollViewer.ScrollOffsetY;
        Assert.True(offset > 0);
        float nodeTop = last.PointToScreen(Point.Zero).Y - tree.ScrollViewer.PointToScreen(Point.Zero).Y;
        Assert.True(nodeTop >= -0.01f);
        Assert.True(nodeTop + last.HeaderHeight <= 150.01f);
    }

    [Fact]
    public void NumPadKeys_ExpandCollapseAndExpandAll()
    {
        var root = new Node("Root", new Node("Folder", new Node("Leaf")));
        var tree = CreateTree([root]);
        tree.SelectedItem = root;
        var rootItem = tree.RootItems[0];

        tree.OnKeyDown(new KeyEventArgs(Key.NumPadAdd));
        Assert.True(rootItem.IsExpanded);

        tree.OnKeyDown(new KeyEventArgs(Key.NumPadSubtract));
        Assert.False(rootItem.IsExpanded);

        tree.OnKeyDown(new KeyEventArgs(Key.NumPadMultiply));
        Assert.True(rootItem.IsExpanded);
        Assert.True(NodeFor(tree, "Folder").IsExpanded);
        Assert.Equal(3, tree.GetVisibleItems().Count);
    }

    [Fact]
    public void ArrowKeys_NavigateAcrossNestedLevels()
    {
        var root1 = new Node("R1", new Node("A", new Node("A1")));
        var root2 = new Node("R2");
        var tree = CreateTree([root1, root2]);
        tree.ExpandAll();
        tree.SelectedItem = root2;

        tree.OnKeyDown(new KeyEventArgs(Key.Up));
        Assert.Equal("A1", tree.SelectedItem!.ToString());

        tree.OnKeyDown(new KeyEventArgs(Key.Down));
        Assert.Same(root2, tree.SelectedItem);

        tree.OnKeyDown(new KeyEventArgs(Key.Home));
        Assert.Same(root1, tree.SelectedItem);

        tree.OnKeyDown(new KeyEventArgs(Key.End));
        Assert.Same(root2, tree.SelectedItem);
    }

    [Fact]
    public void DiscardedNodes_UnsubscribeFromModelCollections()
    {
        var folder = new Node("Folder", new Node("File"));
        var roots = new ObservableCollection<Node> { folder };
        var tree = CreateTree(roots);
        tree.ExpandAll();
        Assert.Equal(1, folder.Children.SubscriberCount);

        for (int i = 0; i < 5; i++)
        {
            tree.RebuildTree();
            tree.ItemsSource = null;
            tree.ItemsSource = roots;
        }
        Assert.Equal(1, folder.Children.SubscriberCount);

        roots.Remove(folder);
        Assert.Equal(0, folder.Children.SubscriberCount);
        Assert.Equal(0, folder.Children[0].Children.SubscriberCount);
    }

    [Fact]
    public void DetachedTreeView_IsCollectable_WhileModelLivesOn()
    {
        var roots = new CountingObservableCollection<Node>([new Node("Root", new Node("Child"))]);

        var weak = CreateAttachAndDetach(roots);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(weak.TryGetTarget(out _));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<TreeView> CreateAttachAndDetach(CountingObservableCollection<Node> roots)
    {
        var host = new StackPanel();
        var tree = new TreeView { ChildrenSelector = item => ((Node)item).Children, ItemsSource = roots };
        host.Add(tree);
        host.AttachToHost();
        tree.ExpandAll();
        Layout(host);
        host.DetachFromHost();
        host.Remove(tree);
        return new WeakReference<TreeView>(tree);
    }
}
