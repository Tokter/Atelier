using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// Displays hierarchical data as expandable <see cref="TreeViewItem"/> nodes with single selection and keyboard navigation.
/// </summary>
/// <remarks>
/// <para>
/// Nodes are generated from <see cref="ItemsSource"/> (children via <see cref="ChildrenSelector"/>) or added manually with
/// <see cref="AddRootItem"/> and <see cref="TreeViewItem.AddChildItem"/>. Child nodes are created lazily, when a node is
/// first expanded (or its <see cref="TreeViewItem.ChildrenItems"/> are read); <see cref="TreeViewItem.HasChildren"/> is
/// known before that. Collections implementing <see cref="INotifyCollectionChanged"/> are observed weakly and updated
/// incrementally, so expansion and selection of unaffected nodes survive changes; a reset reuses the nodes of items that
/// are still present.
/// </para>
/// <para>
/// Selection: removing the selected node clears the selection, and collapsing an ancestor of the selected node selects that
/// ancestor. With duplicate items only one node is selected.
/// </para>
/// <para>
/// Keys: Up/Down move through visible nodes, Home/End go to the first/last one, Right expands or moves to the first child,
/// Left collapses or moves to the parent, Enter/Space toggle, numpad + and - expand and collapse, numpad * expands the
/// whole subtree of the selected node.
/// </para>
/// </remarks>
public class TreeView : Control
{
    /// <summary>Identifies the <see cref="ItemsSource"/> bindable property.</summary>
    public static readonly BindableProperty<IEnumerable?> ItemsSourceProperty =
        BindableProperty.Register<TreeView, IEnumerable?>(
            nameof(ItemsSource),
            null,
            (s, o, n) => ((TreeView)s).OnItemsSourceChanged(o, n)
        );

    /// <summary>Identifies the <see cref="SelectedItem"/> bindable property.</summary>
    public static readonly BindableProperty<object?> SelectedItemProperty =
        BindableProperty.Register<TreeView, object?>(
            nameof(SelectedItem),
            null,
            (s, o, n) => ((TreeView)s).OnSelectedItemChanged(o, n)
        );

    /// <summary>Identifies the <see cref="IndentSize"/> bindable property.</summary>
    public static readonly BindableProperty<float> IndentSizeProperty =
        BindableProperty.Register<TreeView, float>(
            nameof(IndentSize),
            20f,
            (s, o, n) => ((TreeView)s).RefreshNodeVisuals()
        );

    /// <summary>Identifies the <see cref="ExpandIcon"/> bindable property.</summary>
    public static readonly BindableProperty<MaterialIconKind> ExpandIconProperty =
        BindableProperty.Register<TreeView, MaterialIconKind>(
            nameof(ExpandIcon),
            MaterialIconKind.ExpandMore,
            (s, o, n) => ((TreeView)s).RefreshNodeVisuals()
        );

    /// <summary>Identifies the <see cref="CollapseIcon"/> bindable property.</summary>
    public static readonly BindableProperty<MaterialIconKind> CollapseIconProperty =
        BindableProperty.Register<TreeView, MaterialIconKind>(
            nameof(CollapseIcon),
            MaterialIconKind.ChevronRight,
            (s, o, n) => ((TreeView)s).RefreshNodeVisuals()
        );

    /// <summary>Identifies the <see cref="IconSize"/> bindable property.</summary>
    public static readonly BindableProperty<float> IconSizeProperty =
        BindableProperty.Register<TreeView, float>(
            nameof(IconSize),
            18f,
            (s, o, n) => ((TreeView)s).RefreshNodeVisuals()
        );

    /// <summary>Gets or sets the root items. Default <c>null</c>. <see cref="TreeViewItem"/> instances are used as nodes directly.</summary>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// Gets or sets the selected item: the selected node's <see cref="TreeViewItem.ItemValue"/> (or the node itself when
    /// it has none), or <c>null</c>. Default <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Setting it selects the first generated node for an equal item. An item whose node hasn't been generated yet (inside a
    /// never-expanded branch) stays pending and is selected when its node is created.
    /// </remarks>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    private Func<object, IEnumerable?>? _childrenSelector;

    /// <summary>
    /// Gets or sets the function returning an item's children (or <c>null</c> for none). Default <c>null</c>: no children.
    /// Changing it regenerates all nodes.
    /// </summary>
    public Func<object, IEnumerable?>? ChildrenSelector
    {
        get => _childrenSelector;
        set
        {
            _childrenSelector = value;
            RegenerateTree();
        }
    }

    private Func<object, UIElement>? _itemTemplate;

    /// <summary>
    /// Gets or sets the factory for a node's header content. Default <c>null</c>: the item's text. Changing it regenerates
    /// all nodes.
    /// </summary>
    public Func<object, UIElement>? ItemTemplate
    {
        get => _itemTemplate;
        set
        {
            _itemTemplate = value;
            RegenerateTree();
        }
    }

    /// <summary>Gets or sets the horizontal indentation per level, in pixels. Default 20. Applies to existing nodes.</summary>
    public float IndentSize
    {
        get => GetValue(IndentSizeProperty);
        set => SetValue(IndentSizeProperty, value);
    }

    /// <summary>Gets or sets the expander icon of expanded nodes. Default <see cref="MaterialIconKind.ExpandMore"/>.</summary>
    public MaterialIconKind ExpandIcon
    {
        get => GetValue(ExpandIconProperty);
        set => SetValue(ExpandIconProperty, value);
    }

    /// <summary>Gets or sets the expander icon of collapsed nodes. Default <see cref="MaterialIconKind.ChevronRight"/>.</summary>
    public MaterialIconKind CollapseIcon
    {
        get => GetValue(CollapseIconProperty);
        set => SetValue(CollapseIconProperty, value);
    }

    /// <summary>Gets or sets the expander icon size. Default 18.</summary>
    public float IconSize
    {
        get => GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>Gets the scroll viewer hosting the nodes.</summary>
    public ScrollViewer ScrollViewer { get; } = new();

    /// <summary>Gets the root nodes, in display order.</summary>
    public IReadOnlyList<TreeViewItem> RootItems => _rootItems;

    /// <summary>Gets the selected node, or <c>null</c>.</summary>
    public TreeViewItem? SelectedNode => _selectedNode;

    private readonly StackPanel _rootItemsPanel = new() { Orientation = Orientation.Vertical };
    private readonly List<TreeViewItem> _rootItems = [];
    private WeakCollectionChangedSubscription<TreeView>? _sourceSubscription;
    private TreeViewItem? _selectedNode;
    private TreeViewItem? _pendingScrollTarget;
    private bool _isSyncingSelection;
    private bool _isFocusingFromPointer;

    /// <summary>Occurs when the selection changes; the argument is the new <see cref="SelectedItem"/>.</summary>
    public event EventHandler<object?>? SelectionChanged;

    /// <summary>Occurs when a node is expanded.</summary>
    public event EventHandler<TreeViewItem>? ItemExpanded;

    /// <summary>Occurs when a node is collapsed.</summary>
    public event EventHandler<TreeViewItem>? ItemCollapsed;

    /// <summary>Initializes a new, empty <see cref="TreeView"/>.</summary>
    public TreeView()
    {
        IsFocusable = true;
        ScrollViewer.Content = _rootItemsPanel;
        ScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        ScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        AddChild(ScrollViewer);
    }

    internal bool IsSyncingSelection => _isSyncingSelection;

    #region Node generation

    private void OnItemsSourceChanged(IEnumerable? oldSource, IEnumerable? newSource)
    {
        _sourceSubscription?.Dispose();
        _sourceSubscription = null;

        ResetNodes(newSource, _rootItems, _rootItemsPanel, null, 0, reuse: true);
        InvalidateMeasure();

        if (newSource is INotifyCollectionChanged incc)
        {
            _sourceSubscription = new WeakCollectionChangedSubscription<TreeView>(
                incc, this, static (tree, e) => tree.OnItemsSourceCollectionChanged(e));
        }
    }

    private void OnItemsSourceCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        Atelier.Core.Threading.Dispatcher.VerifyAccess("TreeView.ItemsSource collection change");
        ApplyCollectionChange(ItemsSource, e, _rootItems, _rootItemsPanel, null, 0);
    }

    /// <summary>
    /// Re-reads the root items from <see cref="ItemsSource"/>, keeping the nodes (with their expansion and selection) of
    /// items that are still present.
    /// </summary>
    public void RebuildTree()
    {
        ResetNodes(ItemsSource, _rootItems, _rootItemsPanel, null, 0, reuse: true);
        InvalidateMeasure();
    }

    // Template or selector changed: every node's visuals and children must be recreated.
    private void RegenerateTree()
    {
        if (ItemsSource == null) return;
        ResetNodes(ItemsSource, _rootItems, _rootItemsPanel, null, 0, reuse: false);
        InvalidateMeasure();
    }

    /// <summary>Adds a manually created root node (and its existing children) to the tree.</summary>
    /// <param name="item">The node to add.</param>
    public void AddRootItem(TreeViewItem item)
    {
        _rootItems.Add(item);
        _rootItemsPanel.Add(item);
        item.AttachToTree(this, null, 0);
        InvalidateMeasure();
    }

    /// <summary>Removes a root node added with <see cref="AddRootItem"/>; clears the selection if it was inside it.</summary>
    /// <param name="item">The node to remove.</param>
    public void RemoveRootItem(TreeViewItem item)
    {
        if (_rootItems.Remove(item))
        {
            _rootItemsPanel.Remove(item);
            OnNodeRemoved(item);
            item.AttachToTree(null, null, 0);
            InvalidateMeasure();
        }
    }

    // Applies one collection notification to a node list and its panel (roots or a node's children).
    internal void ApplyCollectionChange(
        IEnumerable? source,
        NotifyCollectionChangedEventArgs e,
        List<TreeViewItem> nodes,
        Panel panel,
        TreeViewItem? parent,
        int level)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add
                when e.NewItems != null && e.NewStartingIndex >= 0 && e.NewStartingIndex <= nodes.Count:
                for (int i = 0; i < e.NewItems.Count; i++)
                {
                    var node = CreateTreeViewItem(e.NewItems[i]!, level, parent);
                    nodes.Insert(e.NewStartingIndex + i, node);
                    panel.InsertChild(e.NewStartingIndex + i, node);
                }
                break;

            case NotifyCollectionChangedAction.Remove
                when e.OldItems != null && e.OldStartingIndex >= 0 && e.OldStartingIndex + e.OldItems.Count <= nodes.Count:
                for (int i = 0; i < e.OldItems.Count; i++)
                {
                    RemoveNodeAt(nodes, panel, e.OldStartingIndex);
                }
                break;

            case NotifyCollectionChangedAction.Replace
                when e.NewItems != null && e.OldItems != null && e.NewItems.Count == e.OldItems.Count
                     && e.NewStartingIndex >= 0 && e.NewStartingIndex + e.NewItems.Count <= nodes.Count:
                for (int i = 0; i < e.NewItems.Count; i++)
                {
                    int index = e.NewStartingIndex + i;
                    RemoveNodeAt(nodes, panel, index);
                    var node = CreateTreeViewItem(e.NewItems[i]!, level, parent);
                    nodes.Insert(index, node);
                    panel.InsertChild(index, node);
                }
                break;

            case NotifyCollectionChangedAction.Move
                when e.OldItems is { Count: 1 } && e.OldStartingIndex >= 0 && e.OldStartingIndex < nodes.Count
                     && e.NewStartingIndex >= 0 && e.NewStartingIndex < nodes.Count:
            {
                var node = nodes[e.OldStartingIndex];
                nodes.RemoveAt(e.OldStartingIndex);
                nodes.Insert(e.NewStartingIndex, node);
                panel.InsertChild(e.NewStartingIndex, node);
                break;
            }

            default:
                // Reset, or a notification without usable indices.
                ResetNodes(source, nodes, panel, parent, level, reuse: true);
                break;
        }

        InvalidateMeasure();
    }

    private void RemoveNodeAt(List<TreeViewItem> nodes, Panel panel, int index)
    {
        var node = nodes[index];
        nodes.RemoveAt(index);
        panel.Remove(node);
        DiscardNode(node);
    }

    // Rebuilds a node list from its source. With reuse, nodes of items that are still present are kept as they are.
    internal void ResetNodes(
        IEnumerable? source,
        List<TreeViewItem> nodes,
        Panel panel,
        TreeViewItem? parent,
        int level,
        bool reuse)
    {
        TreeViewItem[] oldNodes = nodes.Count > 0 ? nodes.ToArray() : [];
        Dictionary<object, TreeViewItem>? reusable = null;
        foreach (var node in oldNodes)
        {
            node.ResetMark = false;
            if (reuse && node.ItemValue != null)
            {
                reusable ??= new Dictionary<object, TreeViewItem>();
                reusable.TryAdd(node.ItemValue, node);
            }
        }

        nodes.Clear();
        panel.Clear();

        if (source != null)
        {
            foreach (var item in source)
            {
                TreeViewItem node;
                if (item is not TreeViewItem && item != null && reusable != null && reusable.Remove(item, out var existing))
                {
                    node = existing;
                    node.AttachToTree(this, parent, level);
                }
                else
                {
                    node = CreateTreeViewItem(item!, level, parent);
                }

                node.ResetMark = true;
                nodes.Add(node);
                panel.Add(node);
            }
        }

        foreach (var node in oldNodes)
        {
            if (!node.ResetMark)
            {
                DiscardNode(node);
            }
        }
    }

    /// <summary>Creates the node for <paramref name="item"/> (or attaches it, if it is a <see cref="TreeViewItem"/>).</summary>
    internal TreeViewItem CreateTreeViewItem(object item, int level, TreeViewItem? parentItem)
    {
        if (item is TreeViewItem directItem)
        {
            directItem.AttachToTree(this, parentItem, level);
            return directItem;
        }

        var node = new TreeViewItem { ItemValue = item };

        // Render content using ItemTemplate or default TextBlock
        UIElement contentVisual;
        if (ItemTemplate != null)
        {
            contentVisual = ItemTemplate(item);
            contentVisual.DataContext = item;
        }
        else if (item is UIElement ui)
        {
            contentVisual = ui;
        }
        else
        {
            contentVisual = new TextBlock(item?.ToString() ?? string.Empty)
            {
                DataContext = item,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        node.SetContentVisual(contentVisual);
        node.AttachToTree(this, parentItem, level);

        if (ChildrenSelector != null && item != null)
        {
            node.SetChildrenSource(ChildrenSelector(item));
        }

        // A selection requested before this node existed (inside an unexpanded branch) applies now.
        if (_selectedNode == null && SelectedItem != null && Matches(node, SelectedItem))
        {
            SetSelectedNode(node, raiseEvent: false);
        }

        return node;
    }

    // A node left the tree for good (its item was removed): drop selection references and model subscriptions.
    internal void DiscardNode(TreeViewItem node)
    {
        OnNodeRemoved(node);
        node.Release();
    }

    // A node (and its subtree) is leaving the tree.
    internal void OnNodeRemoved(TreeViewItem node)
    {
        if (_pendingScrollTarget != null && node.IsSelfOrAncestorOf(_pendingScrollTarget))
        {
            _pendingScrollTarget = null;
        }

        if (_selectedNode != null && node.IsSelfOrAncestorOf(_selectedNode))
        {
            SetSelectedNode(null, raiseEvent: true);
        }
    }

    private void RefreshNodeVisuals()
    {
        RefreshNodeVisuals(_rootItems);
        InvalidateMeasure();
    }

    private static void RefreshNodeVisuals(List<TreeViewItem> nodes)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            node.UpdateIndentation();
            node.UpdateExpanderVisual();
            RefreshNodeVisuals(node.RealizedChildren);
        }
    }

    #endregion

    #region Selection

    private static bool Matches(TreeViewItem node, object value) =>
        ReferenceEquals(node, value) || Equals(node.ItemValue, value);

    private void OnSelectedItemChanged(object? oldItem, object? newItem)
    {
        if (_isSyncingSelection) return;

        TreeViewItem? node = null;
        if (newItem != null)
        {
            node = _selectedNode != null && Matches(_selectedNode, newItem) ? _selectedNode : FindNode(_rootItems, newItem);
        }

        ApplySelectedNode(node);
        SelectionChanged?.Invoke(this, newItem);
    }

    // Selects a node (or none) and updates SelectedItem to match.
    internal void SetSelectedNode(TreeViewItem? node, bool raiseEvent)
    {
        object? oldItem = SelectedItem;
        bool nodeChanged = node != _selectedNode;
        ApplySelectedNode(node);

        _isSyncingSelection = true;
        try
        {
            SelectedItem = node == null ? null : node.ItemValue ?? node;
        }
        finally
        {
            _isSyncingSelection = false;
        }

        if (raiseEvent && (nodeChanged || !Equals(oldItem, SelectedItem)))
        {
            SelectionChanged?.Invoke(this, SelectedItem);
        }
    }

    private void ApplySelectedNode(TreeViewItem? node)
    {
        var old = _selectedNode;
        _selectedNode = node;
        if (old == node) return;

        _isSyncingSelection = true;
        try
        {
            if (old != null) old.IsSelected = false;
            if (node != null) node.IsSelected = true;
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    // Called when a node's IsSelected is set directly.
    internal void OnNodeIsSelectedChanged(TreeViewItem node, bool isSelected)
    {
        if (isSelected)
        {
            if (_selectedNode != node) SetSelectedNode(node, raiseEvent: true);
        }
        else if (_selectedNode == node)
        {
            SetSelectedNode(null, raiseEvent: true);
        }
    }

    // A node that was already marked selected joined the tree.
    internal void AdoptSelectedNode(TreeViewItem node)
    {
        if (_selectedNode == null)
        {
            SetSelectedNode(node, raiseEvent: true);
        }
        else if (_selectedNode != node)
        {
            _isSyncingSelection = true;
            try
            {
                node.IsSelected = false;
            }
            finally
            {
                _isSyncingSelection = false;
            }
        }
    }

    private static TreeViewItem? FindNode(List<TreeViewItem> nodes, object value)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (Matches(node, value)) return node;
            var found = FindNode(node.RealizedChildren, value);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>Selects <paramref name="node"/>, expands its ancestors and scrolls it into view.</summary>
    internal void SelectNode(TreeViewItem node)
    {
        SetSelectedNode(node, raiseEvent: true);
        node.EnsureVisible();
        ScrollIntoView(node);
    }

    internal void SelectFromPointer(TreeViewItem node)
    {
        _isFocusingFromPointer = true;
        try
        {
            Focus();
        }
        finally
        {
            _isFocusingFromPointer = false;
        }

        SelectNode(node);
    }

    #endregion

    #region Traversal

    /// <summary>
    /// Returns a new list of all generated nodes in pre-order. Nodes of never-expanded branches are not generated and not
    /// included. Allocates; intended for inspection and tests.
    /// </summary>
    public List<TreeViewItem> GetAllNodes()
    {
        var list = new List<TreeViewItem>();
        CollectAllNodes(_rootItems, list);
        return list;
    }

    private static void CollectAllNodes(List<TreeViewItem> nodes, List<TreeViewItem> list)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            list.Add(nodes[i]);
            CollectAllNodes(nodes[i].RealizedChildren, list);
        }
    }

    /// <summary>
    /// Returns a new list of the nodes that are currently shown (visible and not inside a collapsed node), in display
    /// order. Allocates; intended for inspection and tests.
    /// </summary>
    public List<TreeViewItem> GetVisibleItems()
    {
        var list = new List<TreeViewItem>();
        CollectVisibleItems(_rootItems, list);
        return list;
    }

    private static void CollectVisibleItems(List<TreeViewItem> nodes, List<TreeViewItem> list)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node.Visibility != Visibility.Visible) continue;
            list.Add(node);
            if (node.IsExpanded)
            {
                CollectVisibleItems(node.RealizedChildren, list);
            }
        }
    }

    private List<TreeViewItem> SiblingsOf(TreeViewItem node) => node.ParentTreeViewItem?.RealizedChildren ?? _rootItems;

    private static TreeViewItem? FirstVisibleFrom(List<TreeViewItem> nodes, int start)
    {
        for (int i = Math.Max(0, start); i < nodes.Count; i++)
        {
            if (nodes[i].Visibility == Visibility.Visible) return nodes[i];
        }
        return null;
    }

    private static TreeViewItem? LastVisibleBefore(List<TreeViewItem> nodes, int end)
    {
        for (int i = Math.Min(end, nodes.Count) - 1; i >= 0; i--)
        {
            if (nodes[i].Visibility == Visibility.Visible) return nodes[i];
        }
        return null;
    }

    private static TreeViewItem DeepestVisible(TreeViewItem node)
    {
        while (node.IsExpanded && LastVisibleBefore(node.RealizedChildren, node.RealizedChildren.Count) is { } child)
        {
            node = child;
        }
        return node;
    }

    private TreeViewItem? FirstVisibleNode() => FirstVisibleFrom(_rootItems, 0);

    private TreeViewItem? LastVisibleNode()
    {
        var last = LastVisibleBefore(_rootItems, _rootItems.Count);
        return last == null ? null : DeepestVisible(last);
    }

    private TreeViewItem? NextVisibleNode(TreeViewItem node)
    {
        if (node.IsExpanded && FirstVisibleFrom(node.RealizedChildren, 0) is { } child)
        {
            return child;
        }

        for (var current = node; current != null; current = current.ParentTreeViewItem)
        {
            var siblings = SiblingsOf(current);
            var next = FirstVisibleFrom(siblings, siblings.IndexOf(current) + 1);
            if (next != null) return next;
        }
        return null;
    }

    private TreeViewItem? PreviousVisibleNode(TreeViewItem node)
    {
        var siblings = SiblingsOf(node);
        var previous = LastVisibleBefore(siblings, siblings.IndexOf(node));
        return previous != null ? DeepestVisible(previous) : node.ParentTreeViewItem;
    }

    #endregion

    /// <summary>
    /// Scrolls so the header of <paramref name="node"/> is visible. If layout is pending (for example right after its
    /// ancestors were expanded), scrolling happens after the next arrange, when the node's position is known.
    /// </summary>
    /// <param name="node">A node of this tree.</param>
    public void ScrollIntoView(TreeViewItem node)
    {
        if (node.ParentTreeView != this) return;

        if (!IsArrangeValid || !node.IsArrangeValid)
        {
            _pendingScrollTarget = node;
            return;
        }

        ScrollIntoViewCore(node);
    }

    private void ScrollIntoViewCore(TreeViewItem node)
    {
        var screenPt = node.PointToScreen(Point.Zero);
        var svScreenPt = ScrollViewer.PointToScreen(Point.Zero);
        float nodeRelativeY = screenPt.Y - svScreenPt.Y + ScrollViewer.ScrollOffsetY;
        float nodeHeight = node.HeaderHeight;

        float viewTop = ScrollViewer.ScrollOffsetY;
        float viewHeight = ScrollViewer.Viewport.Height > 0 ? ScrollViewer.Viewport.Height : Bounds.Height;
        float viewBottom = viewTop + viewHeight;

        if (nodeRelativeY < viewTop)
        {
            ScrollViewer.ScrollOffsetY = Math.Max(0, nodeRelativeY);
        }
        else if (nodeRelativeY + nodeHeight > viewBottom && viewHeight > 0)
        {
            ScrollViewer.ScrollOffsetY = Math.Min(ScrollViewer.MaxScrollY, nodeRelativeY + nodeHeight - viewHeight);
        }
    }

    /// <summary>Expands every node that has children, generating the whole tree.</summary>
    public void ExpandAll()
    {
        for (int i = 0; i < _rootItems.Count; i++)
        {
            ExpandSubtree(_rootItems[i]);
        }
    }

    /// <summary>Collapses every generated node that has children.</summary>
    public void CollapseAll()
    {
        CollapseAll(_rootItems);
    }

    private static void CollapseAll(List<TreeViewItem> nodes)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            CollapseAll(node.RealizedChildren);
            if (node.HasChildren)
            {
                node.IsExpanded = false;
            }
        }
    }

    /// <summary>Expands <paramref name="node"/> and all its descendants that have children.</summary>
    /// <param name="node">The subtree root.</param>
    public static void ExpandSubtree(TreeViewItem node)
    {
        if (!node.HasChildren) return;

        node.IsExpanded = true;
        var children = node.RealizedChildren;
        for (int i = 0; i < children.Count; i++)
        {
            ExpandSubtree(children[i]);
        }
    }

    internal void NotifyItemExpanded(TreeViewItem item)
    {
        ItemExpanded?.Invoke(this, item);
    }

    internal void NotifyItemCollapsed(TreeViewItem item)
    {
        ItemCollapsed?.Invoke(this, item);

        // Like WPF: a selection hidden by collapsing moves to the collapsed node.
        if (_selectedNode != null && _selectedNode != item && item.IsSelfOrAncestorOf(_selectedNode))
        {
            SetSelectedNode(item, raiseEvent: true);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Gaining focus from the keyboard selects the first node if nothing is selected.</remarks>
    public override void OnGotFocus()
    {
        base.OnGotFocus();
        if (!_isFocusingFromPointer && _selectedNode == null && SelectedItem == null && FirstVisibleNode() is { } first)
        {
            SelectNode(first);
        }
        _selectedNode?.InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void OnLostFocus()
    {
        base.OnLostFocus();
        _selectedNode?.InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsEnabled || e.Handled) return;

        var current = _selectedNode;
        TreeViewItem? target = null;

        switch (e.Key)
        {
            case Key.Down:
                target = current == null ? FirstVisibleNode() : NextVisibleNode(current);
                break;
            case Key.Up:
                target = current == null ? FirstVisibleNode() : PreviousVisibleNode(current);
                break;
            case Key.Home:
                target = FirstVisibleNode();
                break;
            case Key.End:
                target = LastVisibleNode();
                break;
            case Key.Right:
                if (current != null && current.HasChildren)
                {
                    if (!current.IsExpanded)
                    {
                        current.IsExpanded = true;
                    }
                    else
                    {
                        target = FirstVisibleFrom(current.RealizedChildren, 0);
                    }
                }
                break;
            case Key.Left:
                if (current != null)
                {
                    if (current.IsExpanded)
                    {
                        current.IsExpanded = false;
                    }
                    else
                    {
                        target = current.ParentTreeViewItem;
                    }
                }
                break;
            case Key.Enter:
            case Key.Space:
                if (current != null && current.HasChildren)
                {
                    current.IsExpanded = !current.IsExpanded;
                }
                break;
            case Key.NumPadAdd:
                if (current != null && current.HasChildren) current.IsExpanded = true;
                break;
            case Key.NumPadSubtract:
                if (current != null) current.IsExpanded = false;
                break;
            case Key.NumPadMultiply:
                if (current != null) ExpandSubtree(current);
                break;
            default:
                return;
        }

        e.Handled = true;
        if (target != null && target != current)
        {
            SelectNode(target);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        ScrollViewer.Measure(availableSize);
        return ScrollViewer.DesiredSize;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        ScrollViewer.Arrange(new Rect(Point.Zero, finalSize));

        if (_pendingScrollTarget is { } target)
        {
            _pendingScrollTarget = null;
            if (target.ParentTreeView == this)
            {
                ScrollIntoViewCore(target);
            }
        }

        return finalSize;
    }
}
