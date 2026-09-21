using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

public class TreeViewItem : Control
{
    public static readonly BindableProperty<bool> IsExpandedProperty =
        BindableProperty.Register<TreeViewItem, bool>(
            nameof(IsExpanded),
            false,
            (s, o, n) => ((TreeViewItem)s).OnIsExpandedChanged(o, n)
        );

    public static readonly BindableProperty<bool> IsSelectedProperty =
        BindableProperty.Register<TreeViewItem, bool>(
            nameof(IsSelected),
            false,
            (s, o, n) => ((TreeViewItem)s).OnIsSelectedChanged(o, n)
        );

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public object? ItemValue { get; set; }
    public TreeView? ParentTreeView { get; internal set; }
    public TreeViewItem? ParentTreeViewItem { get; internal set; }
    public int Level { get; internal set; } = 0;
    public bool HasChildren { get; internal set; } = false;

    public bool IsHeaderHovered { get; internal set; } = false;

    public ObservableCollection<TreeViewItem> ChildrenItems { get; } = [];

    public event EventHandler? Expanded;
    public event EventHandler? Collapsed;

    // Internal layout composition
    private readonly StackPanel _rootLayout;
    private readonly TreeViewItemHeader _headerRow;
    private readonly Border _indentSpacer;
    private readonly TreeViewExpanderButton _expanderButton;
    private readonly Icon _expanderIcon;
    private readonly Border _contentWrapper;
    private readonly StackPanel _childrenPanel;

    private IEnumerable? _childrenSource;

    public Rect HeaderBounds => new(0, 0, Bounds.Width, _headerRow.Bounds.Height > 0 ? _headerRow.Bounds.Height : 32f);
    public float HeaderHeight => HeaderBounds.Height;

    public TreeViewItem()
    {
        IsFocusable = false; // TreeView itself manages focus and keyboard traversal
        CornerRadius = new CornerRadius(6);

        _rootLayout = new StackPanel { Orientation = Orientation.Vertical };

        // 1. Header row
        _headerRow = new TreeViewItemHeader(this)
        {
            CornerRadius = CornerRadius,
            Padding = new Thickness(4, 2)
        };

        var headerStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4f,
            VerticalAlignment = VerticalAlignment.Center
        };

        _indentSpacer = new Border { Width = 0f, Height = 1f };

        _expanderIcon = new Icon(MaterialIconKind.ChevronRight, 18f)
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        _expanderButton = new TreeViewExpanderButton(this)
        {
            Width = 24f,
            Height = 24f,
            VerticalAlignment = VerticalAlignment.Center,
            Child = _expanderIcon
        };

        _contentWrapper = new Border
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        headerStack.Add(_indentSpacer);
        headerStack.Add(_expanderButton);
        headerStack.Add(_contentWrapper);

        _headerRow.Child = headerStack;

        // 2. Children panel
        _childrenPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Visibility = Visibility.Collapsed
        };

        _rootLayout.Add(_headerRow);
        _rootLayout.Add(_childrenPanel);

        AddChild(_rootLayout);
        UpdateExpanderVisual();
    }

    public TreeViewItem(string text) : this()
    {
        ItemValue = text;
        SetContentVisual(new TextBlock(text) { VerticalAlignment = VerticalAlignment.Center });
    }

    internal void UpdateIndentation()
    {
        float indent = ParentTreeView?.IndentSize ?? 20f;
        _indentSpacer.Width = Level * indent;
    }

    public void SetContentVisual(UIElement visual)
    {
        _contentWrapper.Child = visual;
        InvalidateMeasure();
    }

    public void SetChildrenSource(IEnumerable? source)
    {
        if (_childrenSource is INotifyCollectionChanged oldIncc)
        {
            oldIncc.CollectionChanged -= OnChildrenCollectionChanged;
        }

        _childrenSource = source;
        RebuildChildren();

        if (_childrenSource is INotifyCollectionChanged newIncc)
        {
            newIncc.CollectionChanged += OnChildrenCollectionChanged;
        }
    }

    private void OnChildrenCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildChildren();
    }

    public void RebuildChildren()
    {
        _childrenPanel.Clear();
        ChildrenItems.Clear();

        if (_childrenSource != null && ParentTreeView != null)
        {
            foreach (var childObj in _childrenSource)
            {
                var childNode = ParentTreeView.CreateTreeViewItem(childObj, Level + 1, this);
                ChildrenItems.Add(childNode);
                _childrenPanel.Add(childNode);
            }
        }

        HasChildren = ChildrenItems.Count > 0;
        UpdateExpanderVisual();
        InvalidateMeasure();
    }

    public void AddChildItem(TreeViewItem child)
    {
        child.ParentTreeView = ParentTreeView;
        child.ParentTreeViewItem = this;
        child.Level = Level + 1;
        child.UpdateIndentation();

        ChildrenItems.Add(child);
        _childrenPanel.Add(child);

        HasChildren = true;
        UpdateExpanderVisual();
        InvalidateMeasure();
    }

    public void RemoveChildItem(TreeViewItem child)
    {
        if (ChildrenItems.Remove(child))
        {
            _childrenPanel.Remove(child);
            child.ParentTreeViewItem = null;
            HasChildren = ChildrenItems.Count > 0;
            UpdateExpanderVisual();
            InvalidateMeasure();
        }
    }

    internal void UpdateExpanderVisual()
    {
        if (HasChildren)
        {
            _expanderIcon.Visibility = Visibility.Visible;
            _expanderIcon.Kind = IsExpanded
                ? (ParentTreeView?.ExpandIcon ?? MaterialIconKind.ExpandMore)
                : (ParentTreeView?.CollapseIcon ?? MaterialIconKind.ChevronRight);
            _expanderIcon.Size = ParentTreeView?.IconSize ?? 18f;
        }
        else
        {
            _expanderIcon.Visibility = Visibility.Collapsed;
        }
    }

    private void OnIsExpandedChanged(bool oldVal, bool newVal)
    {
        _childrenPanel.Visibility = newVal ? Visibility.Visible : Visibility.Collapsed;
        UpdateExpanderVisual();
        InvalidateMeasure();

        if (newVal)
        {
            Expanded?.Invoke(this, EventArgs.Empty);
            ParentTreeView?.NotifyItemExpanded(this);
        }
        else
        {
            Collapsed?.Invoke(this, EventArgs.Empty);
            ParentTreeView?.NotifyItemCollapsed(this);
        }
    }

    private void OnIsSelectedChanged(bool oldVal, bool newVal)
    {
        InvalidateVisual();
        if (newVal && ParentTreeView != null && !Equals(ParentTreeView.SelectedItem, ItemValue))
        {
            ParentTreeView.SelectedItem = ItemValue ?? this;
        }
    }

    internal void SelectThis()
    {
        if (ParentTreeView != null)
        {
            ParentTreeView.Focus();
            ParentTreeView.SelectNode(this);
        }
    }

    public void EnsureVisible()
    {
        var current = ParentTreeViewItem;
        while (current != null)
        {
            current.IsExpanded = true;
            current = current.ParentTreeViewItem;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        UpdateIndentation();
        _rootLayout.Measure(availableSize);
        return _rootLayout.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _rootLayout.Arrange(new Rect(Point.Zero, finalSize));
        return finalSize;
    }
}

internal class TreeViewItemHeader(TreeViewItem owner) : Border
{
    private DateTime _lastClickTime = DateTime.MinValue;

    public override UIElement? HitTest(Point point)
    {
        if (Visibility != Visibility.Visible || !IsHitTestVisible || !Bounds.Contains(point))
        {
            return null;
        }

        Point localPoint = point.Offset(-Bounds.X, -Bounds.Y);
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (Children[i] is UIElement child)
            {
                var hit = child.HitTest(localPoint);
                if (hit is TreeViewExpanderButton)
                {
                    return hit;
                }
            }
        }

        return this;
    }

    public override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        owner.IsHeaderHovered = true;
        owner.InvalidateVisual();
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        owner.IsHeaderHovered = false;
        owner.InvalidateVisual();
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Handled = true;

        var now = DateTime.UtcNow;
        if (now - _lastClickTime < TimeSpan.FromMilliseconds(400))
        {
            if (owner.HasChildren)
            {
                owner.IsExpanded = !owner.IsExpanded;
            }
            _lastClickTime = DateTime.MinValue;
        }
        else
        {
            _lastClickTime = now;
            owner.SelectThis();
        }
    }
}

internal class TreeViewExpanderButton(TreeViewItem owner) : Border
{
    public override UIElement? HitTest(Point point)
    {
        if (Visibility != Visibility.Visible || !IsHitTestVisible || !Bounds.Contains(point))
        {
            return null;
        }
        return this;
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Handled = true;

        if (owner.HasChildren)
        {
            owner.IsExpanded = !owner.IsExpanded;
        }
    }
}
