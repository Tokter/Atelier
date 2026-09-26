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

public class ItemsControl : Control
{
    public static readonly BindableProperty<IEnumerable?> ItemsSourceProperty =
        BindableProperty.Register<ItemsControl, IEnumerable?>(
            nameof(ItemsSource),
            null,
            (s, o, n) => ((ItemsControl)s).OnItemsSourceChanged(o, n)
        );

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ObservableCollection<object> Items { get; } = [];

    private Func<object, UIElement>? _itemTemplate;
    public Func<object, UIElement>? ItemTemplate
    {
        get => _itemTemplate;
        set
        {
            _itemTemplate = value;
            RebuildItemViews();
        }
    }

    public ScrollViewer ScrollViewer { get; } = new();
    protected readonly StackPanel ItemPanel = new() { Orientation = Orientation.Vertical };

    public ItemsControl()
    {
        ScrollViewer.Content = ItemPanel;
        AddChild(ScrollViewer);
        Items.CollectionChanged += OnItemsCollectionChanged;
    }

    private void OnItemsSourceChanged(IEnumerable? oldSource, IEnumerable? newSource)
    {
        if (oldSource is INotifyCollectionChanged oldIncc)
        {
            oldIncc.CollectionChanged -= OnItemsSourceCollectionChanged;
        }

        Items.Clear();

        if (newSource != null)
        {
            foreach (var item in newSource)
            {
                Items.Add(item);
            }

            if (newSource is INotifyCollectionChanged newIncc)
            {
                newIncc.CollectionChanged += OnItemsSourceCollectionChanged;
            }
        }
    }

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Atelier.Core.Threading.Dispatcher.VerifyAccess("ItemsControl.ItemsSource collection change");
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                {
                    for (int i = 0; i < e.NewItems.Count; i++)
                    {
                        Items.Insert(e.NewStartingIndex + i, e.NewItems[i]!);
                    }
                }
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                {
                    for (int i = 0; i < e.OldItems.Count; i++)
                    {
                        Items.RemoveAt(e.OldStartingIndex);
                    }
                }
                break;
            case NotifyCollectionChangedAction.Reset:
                Items.Clear();
                if (ItemsSource != null)
                {
                    foreach (var item in ItemsSource) Items.Add(item);
                }
                break;
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RebuildItemViews();
    }

    protected virtual void RebuildItemViews()
    {
        ItemPanel.Clear();
        foreach (var item in Items)
        {
            UIElement container = CreateContainerForItem(item);
            ItemPanel.Add(container);
        }
        InvalidateMeasure();
    }

    protected virtual UIElement CreateContainerForItem(object item)
    {
        if (ItemTemplate != null)
        {
            var el = ItemTemplate(item);
            el.DataContext = item;
            return el;
        }

        if (item is UIElement ui) return ui;

        return new TextBlock(item?.ToString() ?? string.Empty)
        {
            Margin = new Thickness(8, 4),
            DataContext = item
        };
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

public class ListBoxItem : ContentControl
{
    public static readonly BindableProperty<bool> IsSelectedProperty =
        BindableProperty.Register<ListBoxItem, bool>(
            nameof(IsSelected),
            false,
            options: PropertyOptions.AffectsRender
        );

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public ListBox? ParentListBox { get; internal set; }
    public object? ItemValue { get; internal set; }
    public event EventHandler? Clicked;

    static ListBoxItem()
    {
        PaddingProperty.OverrideDefaultValue<ListBoxItem>(new Thickness(12, 8));
        CornerRadiusProperty.OverrideDefaultValue<ListBoxItem>(new CornerRadius(4));
        ClipToBoundsProperty.OverrideDefaultValue<ListBoxItem>(true);
    }

    public ListBoxItem()
    {
        IsFocusable = false; // ListBox itself is the single focusable tab stop
    }

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
        bool wasPressed = IsPressed;
        base.OnPointerReleased(e);
        e.Handled = true;

        if (wasPressed && IsHovered && IsEnabled)
        {
            ParentListBox?.Focus();
            Clicked?.Invoke(this, EventArgs.Empty);
        }
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsEnabled && (e.Key is Key.Enter or Key.Space))
        {
            Clicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }
}

public class ListBox : ItemsControl
{
    public static readonly BindableProperty<object?> SelectedItemProperty =
        BindableProperty.Register<ListBox, object?>(
            nameof(SelectedItem),
            null,
            (s, o, n) => ((ListBox)s).OnSelectedItemChanged(o, n)
        );

    public static readonly BindableProperty<int> SelectedIndexProperty =
        BindableProperty.Register<ListBox, int>(
            nameof(SelectedIndex),
            -1,
            (s, o, n) => ((ListBox)s).OnSelectedIndexChanged(o, n)
        );

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public event EventHandler<object?>? SelectionChanged;

    private readonly List<ListBoxItem> _containers = [];

    public ListBox()
    {
        IsFocusable = true;
    }

    public override void OnGotFocus()
    {
        base.OnGotFocus();
        if (SelectedIndex < 0 && Items.Count > 0)
        {
            SelectedIndex = 0;
            ScrollIntoView(0);
        }
        InvalidateContainersVisual();
    }

    public override void OnLostFocus()
    {
        base.OnLostFocus();
        InvalidateContainersVisual();
    }

    private void InvalidateContainersVisual()
    {
        for (int i = 0; i < _containers.Count; i++)
        {
            _containers[i].InvalidateVisual();
        }
        InvalidateVisual();
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!IsEnabled || Items.Count == 0) return;

        switch (e.Key)
        {
            case Key.Down:
            {
                int next = SelectedIndex < Items.Count - 1 ? SelectedIndex + 1 : (SelectedIndex < 0 ? 0 : SelectedIndex);
                if (next != SelectedIndex)
                {
                    SelectedIndex = next;
                    ScrollIntoView(next);
                }
                e.Handled = true;
                break;
            }
            case Key.Up:
            {
                int prev = SelectedIndex > 0 ? SelectedIndex - 1 : 0;
                if (prev != SelectedIndex)
                {
                    SelectedIndex = prev;
                    ScrollIntoView(prev);
                }
                e.Handled = true;
                break;
            }
            case Key.Home:
            {
                if (SelectedIndex != 0)
                {
                    SelectedIndex = 0;
                    ScrollIntoView(0);
                }
                e.Handled = true;
                break;
            }
            case Key.End:
            {
                int last = Items.Count - 1;
                if (SelectedIndex != last)
                {
                    SelectedIndex = last;
                    ScrollIntoView(last);
                }
                e.Handled = true;
                break;
            }
            case Key.PageDown:
            {
                int pageStep = Math.Max(1, (int)(ScrollViewer.Viewport.Height / 36f));
                int target = Math.Min(Items.Count - 1, (SelectedIndex < 0 ? 0 : SelectedIndex) + pageStep);
                if (target != SelectedIndex)
                {
                    SelectedIndex = target;
                    ScrollIntoView(target);
                }
                e.Handled = true;
                break;
            }
            case Key.PageUp:
            {
                int pageStep = Math.Max(1, (int)(ScrollViewer.Viewport.Height / 36f));
                int target = Math.Max(0, SelectedIndex - pageStep);
                if (target != SelectedIndex)
                {
                    SelectedIndex = target;
                    ScrollIntoView(target);
                }
                e.Handled = true;
                break;
            }
        }
    }

    public void ScrollIntoView(int index)
    {
        if (index < 0 || index >= _containers.Count) return;

        var container = _containers[index];
        float itemTop = container.Bounds.Y;
        float itemHeight = container.Bounds.Height > 0 ? container.Bounds.Height : 36f;
        float itemBottom = itemTop + itemHeight;

        float viewTop = ScrollViewer.ScrollOffsetY;
        float viewHeight = ScrollViewer.Viewport.Height > 0 ? ScrollViewer.Viewport.Height : Bounds.Height;
        float viewBottom = viewTop + viewHeight;

        if (itemTop < viewTop)
        {
            ScrollViewer.ScrollOffsetY = Math.Max(0, itemTop);
        }
        else if (itemBottom > viewBottom && viewHeight > 0)
        {
            ScrollViewer.ScrollOffsetY = Math.Min(ScrollViewer.MaxScrollY, itemBottom - viewHeight);
        }
    }

    protected override UIElement CreateContainerForItem(object item)
    {
        var container = new ListBoxItem
        {
            ParentListBox = this,
            ItemValue = item,
            Content = base.CreateContainerForItem(item),
            IsSelected = Equals(item, SelectedItem)
        };

        container.Clicked += (s, e) =>
        {
            Focus();
            SelectedItem = container.ItemValue;
            int idx = Items.IndexOf(container.ItemValue!);
            if (idx >= 0) ScrollIntoView(idx);
        };

        _containers.Add(container);
        return container;
    }

    protected override void RebuildItemViews()
    {
        _containers.Clear();
        base.RebuildItemViews();
    }

    private void OnSelectedItemChanged(object? oldItem, object? newItem)
    {
        SelectedIndex = newItem != null ? Items.IndexOf(newItem) : -1;
        UpdateContainerSelections();
        SelectionChanged?.Invoke(this, newItem);
    }

    private void OnSelectedIndexChanged(int oldIndex, int newIndex)
    {
        if (newIndex >= 0 && newIndex < Items.Count)
        {
            SelectedItem = Items[newIndex];
            ScrollIntoView(newIndex);
        }
        else
        {
            SelectedItem = null;
        }
    }

    private void UpdateContainerSelections()
    {
        for (int i = 0; i < _containers.Count; i++)
        {
            _containers[i].IsSelected = Equals(_containers[i].ItemValue, SelectedItem);
        }
        InvalidateContainersVisual();
    }
}
