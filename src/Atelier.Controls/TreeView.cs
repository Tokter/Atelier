using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

public class TreeView : Control
{
    public static readonly BindableProperty<IEnumerable?> ItemsSourceProperty =
        BindableProperty.Register<TreeView, IEnumerable?>(
            nameof(ItemsSource),
            null,
            (s, o, n) => ((TreeView)s).OnItemsSourceChanged(o, n)
        );

    public static readonly BindableProperty<object?> SelectedItemProperty =
        BindableProperty.Register<TreeView, object?>(
            nameof(SelectedItem),
            null,
            (s, o, n) => ((TreeView)s).OnSelectedItemChanged(o, n)
        );

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    private Func<object, IEnumerable?>? _childrenSelector;
    public Func<object, IEnumerable?>? ChildrenSelector
    {
        get => _childrenSelector;
        set
        {
            _childrenSelector = value;
            RebuildTree();
        }
    }

    private Func<object, UIElement>? _itemTemplate;
    public Func<object, UIElement>? ItemTemplate
    {
        get => _itemTemplate;
        set
        {
            _itemTemplate = value;
            RebuildTree();
        }
    }

    private float _indentSize = 20f;
    public float IndentSize
    {
        get => _indentSize;
        set
        {
            _indentSize = value;
            InvalidateMeasure();
        }
    }

    public MaterialIconKind ExpandIcon { get; set; } = MaterialIconKind.ExpandMore;
    public MaterialIconKind CollapseIcon { get; set; } = MaterialIconKind.ChevronRight;
    public float IconSize { get; set; } = 18f;

    public ScrollViewer ScrollViewer { get; } = new();
    private readonly StackPanel _rootItemsPanel = new() { Orientation = Orientation.Vertical };
    private readonly List<TreeViewItem> _rootItems = [];

    public event EventHandler<object?>? SelectionChanged;
    public event EventHandler<TreeViewItem>? ItemExpanded;
    public event EventHandler<TreeViewItem>? ItemCollapsed;

    public TreeView()
    {
        IsFocusable = true;
        ScrollViewer.Content = _rootItemsPanel;
        ScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        ScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        AddChild(ScrollViewer);
    }

    private void OnItemsSourceChanged(IEnumerable? oldSource, IEnumerable? newSource)
    {
        if (oldSource is INotifyCollectionChanged oldIncc)
        {
            oldIncc.CollectionChanged -= OnItemsSourceCollectionChanged;
        }

        RebuildTree();

        if (newSource is INotifyCollectionChanged newIncc)
        {
            newIncc.CollectionChanged += OnItemsSourceCollectionChanged;
        }
    }

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Atelier.Core.Threading.Dispatcher.VerifyAccess("TreeView.ItemsSource collection change");
        RebuildTree();
    }

    public void RebuildTree()
    {
        _rootItemsPanel.Clear();
        _rootItems.Clear();

        if (ItemsSource != null)
        {
            foreach (var item in ItemsSource)
            {
                var rootNode = CreateTreeViewItem(item, 0, null);
                _rootItems.Add(rootNode);
                _rootItemsPanel.Add(rootNode);
            }
        }

        UpdateSelectionVisual();
        InvalidateMeasure();
    }

    public void AddRootItem(TreeViewItem item)
    {
        item.ParentTreeView = this;
        item.ParentTreeViewItem = null;
        item.Level = 0;
        item.UpdateIndentation();

        _rootItems.Add(item);
        _rootItemsPanel.Add(item);
        InvalidateMeasure();
    }

    public void RemoveRootItem(TreeViewItem item)
    {
        if (_rootItems.Remove(item))
        {
            _rootItemsPanel.Remove(item);
            item.ParentTreeView = null;
            InvalidateMeasure();
        }
    }

    internal TreeViewItem CreateTreeViewItem(object item, int level, TreeViewItem? parentItem)
    {
        if (item is TreeViewItem directItem)
        {
            directItem.ParentTreeView = this;
            directItem.ParentTreeViewItem = parentItem;
            directItem.Level = level;
            directItem.UpdateIndentation();
            return directItem;
        }

        var node = new TreeViewItem
        {
            ParentTreeView = this,
            ParentTreeViewItem = parentItem,
            Level = level,
            ItemValue = item
        };

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

        // Bind children if selector provided
        if (ChildrenSelector != null && item != null)
        {
            var children = ChildrenSelector(item);
            node.SetChildrenSource(children);
        }

        return node;
    }

    private void OnSelectedItemChanged(object? oldItem, object? newItem)
    {
        UpdateSelectionVisual();
        SelectionChanged?.Invoke(this, newItem);
    }

    internal void SelectNode(TreeViewItem node)
    {
        SelectedItem = node.ItemValue ?? node;
        node.EnsureVisible();
        ScrollIntoView(node);
    }

    private void UpdateSelectionVisual()
    {
        var allNodes = GetAllNodes();
        for (int i = 0; i < allNodes.Count; i++)
        {
            var node = allNodes[i];
            node.IsSelected = Equals(node.ItemValue, SelectedItem) || Equals(node, SelectedItem);
        }
        InvalidateVisual();
    }

    public List<TreeViewItem> GetAllNodes()
    {
        var list = new List<TreeViewItem>();
        for (int i = 0; i < _rootItems.Count; i++)
        {
            CollectAllNodes(_rootItems[i], list);
        }
        return list;
    }

    private static void CollectAllNodes(TreeViewItem node, List<TreeViewItem> list)
    {
        list.Add(node);
        for (int i = 0; i < node.ChildrenItems.Count; i++)
        {
            CollectAllNodes(node.ChildrenItems[i], list);
        }
    }

    public List<TreeViewItem> GetVisibleItems()
    {
        var list = new List<TreeViewItem>();
        for (int i = 0; i < _rootItems.Count; i++)
        {
            CollectVisibleItems(_rootItems[i], list);
        }
        return list;
    }

    private static void CollectVisibleItems(TreeViewItem node, List<TreeViewItem> list)
    {
        if (node.Visibility != Visibility.Visible) return;
        list.Add(node);

        if (node.IsExpanded)
        {
            for (int i = 0; i < node.ChildrenItems.Count; i++)
            {
                CollectVisibleItems(node.ChildrenItems[i], list);
            }
        }
    }

    public void ScrollIntoView(TreeViewItem node)
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

    public void ExpandAll()
    {
        var nodes = GetAllNodes();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].HasChildren)
            {
                nodes[i].IsExpanded = true;
            }
        }
    }

    public void CollapseAll()
    {
        var nodes = GetAllNodes();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].HasChildren)
            {
                nodes[i].IsExpanded = false;
            }
        }
    }

    internal void NotifyItemExpanded(TreeViewItem item)
    {
        ItemExpanded?.Invoke(this, item);
    }

    internal void NotifyItemCollapsed(TreeViewItem item)
    {
        ItemCollapsed?.Invoke(this, item);
    }

    public override void OnGotFocus()
    {
        base.OnGotFocus();
        if (SelectedItem == null && _rootItems.Count > 0)
        {
            SelectNode(_rootItems[0]);
        }
        InvalidateAllVisual();
    }

    public override void OnLostFocus()
    {
        base.OnLostFocus();
        InvalidateAllVisual();
    }

    private void InvalidateAllVisual()
    {
        var nodes = GetAllNodes();
        for (int i = 0; i < nodes.Count; i++)
        {
            nodes[i].InvalidateVisual();
        }
        InvalidateVisual();
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!IsEnabled) return;

        var visible = GetVisibleItems();
        if (visible.Count == 0) return;

        int selectedIdx = -1;
        for (int i = 0; i < visible.Count; i++)
        {
            if (Equals(visible[i].ItemValue, SelectedItem) || Equals(visible[i], SelectedItem))
            {
                selectedIdx = i;
                break;
            }
        }

        switch (e.Key)
        {
            case Key.Down:
            {
                int next = selectedIdx < visible.Count - 1 ? selectedIdx + 1 : (selectedIdx < 0 ? 0 : selectedIdx);
                if (next >= 0 && next < visible.Count && next != selectedIdx)
                {
                    SelectNode(visible[next]);
                }
                e.Handled = true;
                break;
            }
            case Key.Up:
            {
                int prev = selectedIdx > 0 ? selectedIdx - 1 : 0;
                if (prev >= 0 && prev < visible.Count && prev != selectedIdx)
                {
                    SelectNode(visible[prev]);
                }
                e.Handled = true;
                break;
            }
            case Key.Right:
            {
                if (selectedIdx >= 0 && selectedIdx < visible.Count)
                {
                    var node = visible[selectedIdx];
                    if (node.HasChildren)
                    {
                        if (!node.IsExpanded)
                        {
                            node.IsExpanded = true;
                        }
                        else if (node.ChildrenItems.Count > 0)
                        {
                            SelectNode(node.ChildrenItems[0]);
                        }
                    }
                }
                e.Handled = true;
                break;
            }
            case Key.Left:
            {
                if (selectedIdx >= 0 && selectedIdx < visible.Count)
                {
                    var node = visible[selectedIdx];
                    if (node.IsExpanded)
                    {
                        node.IsExpanded = false;
                    }
                    else if (node.ParentTreeViewItem != null)
                    {
                        SelectNode(node.ParentTreeViewItem);
                    }
                }
                e.Handled = true;
                break;
            }
            case Key.Home:
            {
                if (visible.Count > 0)
                {
                    SelectNode(visible[0]);
                }
                e.Handled = true;
                break;
            }
            case Key.End:
            {
                if (visible.Count > 0)
                {
                    SelectNode(visible[^1]);
                }
                e.Handled = true;
                break;
            }
            case Key.Enter:
            case Key.Space:
            {
                if (selectedIdx >= 0 && selectedIdx < visible.Count)
                {
                    var node = visible[selectedIdx];
                    if (node.HasChildren)
                    {
                        node.IsExpanded = !node.IsExpanded;
                    }
                }
                e.Handled = true;
                break;
            }
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        ScrollViewer.Measure(availableSize);
        return ScrollViewer.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        ScrollViewer.Arrange(new Rect(Point.Zero, finalSize));
        return finalSize;
    }
}
