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

/// <summary>
/// Displays a vertical, scrollable list of items, creating one container element per item.
/// </summary>
/// <remarks>
/// <para>
/// Items come from <see cref="ItemsSource"/> (copied into <see cref="Items"/> and kept in sync when the source implements
/// <see cref="INotifyCollectionChanged"/>) or are added to <see cref="Items"/> directly. Containers are updated
/// incrementally: adding, removing, moving or replacing an item only creates or removes the affected containers, and a
/// reset (or assigning a new source) rebuilds them in a single pass.
/// </para>
/// <para>
/// The source collection is observed through a weak subscription, so a long-lived view-model collection does not keep a
/// discarded control alive. Collection changes must be raised on the UI thread.
/// </para>
/// <para>There is no UI virtualization: every item gets a container.</para>
/// </remarks>
public class ItemsControl : Control
{
    /// <summary>Identifies the <see cref="ItemsSource"/> bindable property.</summary>
    public static readonly BindableProperty<IEnumerable?> ItemsSourceProperty =
        BindableProperty.Register<ItemsControl, IEnumerable?>(
            nameof(ItemsSource),
            null,
            (s, o, n) => ((ItemsControl)s).OnItemsSourceChanged(o, n)
        );

    /// <summary>
    /// Gets or sets the collection the items are generated from. Default <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Setting it replaces the contents of <see cref="Items"/> with the source's items. If the source implements
    /// <see cref="INotifyCollectionChanged"/>, later additions, removals, moves, replacements and resets are mirrored
    /// into <see cref="Items"/>; notifications without valid indices are handled as a reset.
    /// </remarks>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>
    /// Gets the items displayed by the control. Filled from <see cref="ItemsSource"/> when one is set; items can also be
    /// added or removed directly.
    /// </summary>
    public ObservableCollection<object> Items { get; } = [];

    private Func<object, UIElement>? _itemTemplate;

    /// <summary>
    /// Gets or sets the factory that creates the visual for an item. Default <c>null</c>: <see cref="UIElement"/> items
    /// are shown as they are and other items as a <see cref="TextBlock"/> with their <see cref="object.ToString"/> text.
    /// </summary>
    /// <remarks>Changing the template regenerates all containers. The created element's DataContext is set to the item.</remarks>
    public Func<object, UIElement>? ItemTemplate
    {
        get => _itemTemplate;
        set
        {
            _itemTemplate = value;
            RebuildContainers();
            OnItemsChanged(ResetArgs);
            InvalidateMeasure();
        }
    }

    /// <summary>Gets the scroll viewer hosting the item panel.</summary>
    public ScrollViewer ScrollViewer { get; } = new();

    /// <summary>The vertical panel that holds the item containers, in item order.</summary>
    protected readonly StackPanel ItemPanel = new() { Orientation = Orientation.Vertical };

    internal static readonly NotifyCollectionChangedEventArgs ResetArgs = new(NotifyCollectionChangedAction.Reset);

    private readonly List<UIElement> _containers = [];
    private WeakCollectionChangedSubscription<ItemsControl>? _sourceSubscription;
    private bool _isSyncingFromSource;

    /// <summary>Initializes a new, empty <see cref="ItemsControl"/>.</summary>
    public ItemsControl()
    {
        ScrollViewer.Content = ItemPanel;
        AddChild(ScrollViewer);
        Items.CollectionChanged += OnItemsCollectionChanged;
    }

    /// <summary>Gets the number of generated containers (equal to <c>Items.Count</c>).</summary>
    protected int ContainerCount => _containers.Count;

    /// <summary>
    /// Returns the container generated for the item at <paramref name="index"/>, or <c>null</c> if the index is out of range.
    /// </summary>
    /// <param name="index">The item index.</param>
    public UIElement? ContainerFromIndex(int index) =>
        (uint)index < (uint)_containers.Count ? _containers[index] : null;

    /// <summary>
    /// Returns the index of the item whose container is <paramref name="container"/>, or -1 if it isn't one of this
    /// control's containers.
    /// </summary>
    /// <param name="container">The container element.</param>
    public int IndexFromContainer(UIElement container) => _containers.IndexOf(container);

    private void OnItemsSourceChanged(IEnumerable? oldSource, IEnumerable? newSource)
    {
        _sourceSubscription?.Dispose();
        _sourceSubscription = null;

        ResetItemsFromSource();

        if (newSource is INotifyCollectionChanged incc)
        {
            _sourceSubscription = new WeakCollectionChangedSubscription<ItemsControl>(
                incc, this, static (control, e) => control.OnItemsSourceCollectionChanged(e));
        }
    }

    // Refills Items from the source and regenerates the containers once, instead of once per item.
    private void ResetItemsFromSource()
    {
        _isSyncingFromSource = true;
        try
        {
            Items.Clear();
            if (ItemsSource != null)
            {
                foreach (var item in ItemsSource)
                {
                    Items.Add(item!);
                }
            }
        }
        finally
        {
            _isSyncingFromSource = false;
        }

        ApplyItemsChange(ResetArgs);
    }

    private void OnItemsSourceCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        Atelier.Core.Threading.Dispatcher.VerifyAccess("ItemsControl.ItemsSource collection change");

        // Notifications may omit indices (-1 is allowed by INotifyCollectionChanged); those fall back to a reset.
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add
                when e.NewItems != null && e.NewStartingIndex >= 0 && e.NewStartingIndex <= Items.Count:
                for (int i = 0; i < e.NewItems.Count; i++)
                {
                    Items.Insert(e.NewStartingIndex + i, e.NewItems[i]!);
                }
                return;

            case NotifyCollectionChangedAction.Remove
                when e.OldItems != null && e.OldStartingIndex >= 0 && e.OldStartingIndex + e.OldItems.Count <= Items.Count:
                for (int i = 0; i < e.OldItems.Count; i++)
                {
                    Items.RemoveAt(e.OldStartingIndex);
                }
                return;

            case NotifyCollectionChangedAction.Replace
                when e.NewItems != null && e.OldItems != null && e.NewItems.Count == e.OldItems.Count
                     && e.NewStartingIndex >= 0 && e.NewStartingIndex + e.NewItems.Count <= Items.Count:
                for (int i = 0; i < e.NewItems.Count; i++)
                {
                    Items[e.NewStartingIndex + i] = e.NewItems[i]!;
                }
                return;

            case NotifyCollectionChangedAction.Move
                when e.OldItems is { Count: 1 } && e.OldStartingIndex >= 0 && e.OldStartingIndex < Items.Count
                     && e.NewStartingIndex >= 0 && e.NewStartingIndex < Items.Count:
                Items.Move(e.OldStartingIndex, e.NewStartingIndex);
                return;

            default:
                ResetItemsFromSource();
                return;
        }
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_isSyncingFromSource)
        {
            return;
        }

        ApplyItemsChange(e);
    }

    private void ApplyItemsChange(NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                for (int i = 0; i < e.NewItems!.Count; i++)
                {
                    InsertContainer(e.NewStartingIndex + i, e.NewItems[i]!);
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                for (int i = 0; i < e.OldItems!.Count; i++)
                {
                    RemoveContainerAt(e.OldStartingIndex);
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                for (int i = 0; i < e.NewItems!.Count; i++)
                {
                    RemoveContainerAt(e.NewStartingIndex + i);
                    InsertContainer(e.NewStartingIndex + i, e.NewItems[i]!);
                }
                break;

            case NotifyCollectionChangedAction.Move:
            {
                var container = _containers[e.OldStartingIndex];
                _containers.RemoveAt(e.OldStartingIndex);
                _containers.Insert(e.NewStartingIndex, container);
                ItemPanel.InsertChild(e.NewStartingIndex, container);
                break;
            }

            default:
                RebuildContainers();
                break;
        }

        OnItemsChanged(e);
        InvalidateMeasure();
    }

    private void InsertContainer(int index, object item)
    {
        var container = CreateContainerForItem(item);
        _containers.Insert(index, container);
        ItemPanel.InsertChild(index, container);
    }

    private void RemoveContainerAt(int index)
    {
        var container = _containers[index];
        _containers.RemoveAt(index);
        ItemPanel.Remove(container);
    }

    private void RebuildContainers()
    {
        ItemPanel.Clear();
        _containers.Clear();
        for (int i = 0; i < Items.Count; i++)
        {
            var container = CreateContainerForItem(Items[i]);
            _containers.Add(container);
            ItemPanel.Add(container);
        }
    }

    /// <summary>
    /// Called after <see cref="Items"/> changed and the containers were updated. <paramref name="e"/> describes the change
    /// in <see cref="Items"/> coordinates; a reset (also used after a new <see cref="ItemsSource"/> or
    /// <see cref="ItemTemplate"/>) means all containers were regenerated. The base implementation does nothing.
    /// </summary>
    /// <param name="e">The change.</param>
    protected virtual void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
    }

    /// <summary>
    /// Creates the element displayed for <paramref name="item"/>: the <see cref="ItemTemplate"/> result, the item itself if
    /// it is a <see cref="UIElement"/>, or a <see cref="TextBlock"/> showing its text.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The container element.</returns>
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
        return finalSize;
    }
}

/// <summary>
/// The container a <see cref="ListBox"/> creates for each item; shows the item and its selection state.
/// </summary>
/// <remarks>
/// Items are not focusable: the <see cref="ListBox"/> is the single tab stop and handles the keyboard. A left click selects
/// the item.
/// </remarks>
public class ListBoxItem : ContentControl
{
    /// <summary>Identifies the <see cref="IsSelected"/> bindable property.</summary>
    public static readonly BindableProperty<bool> IsSelectedProperty =
        BindableProperty.Register<ListBoxItem, bool>(
            nameof(IsSelected),
            false,
            options: PropertyOptions.AffectsRender
        );

    /// <summary>
    /// Gets or sets whether the item is shown as selected. Maintained by the owning <see cref="ListBox"/>; setting it
    /// directly only changes the visual state.
    /// </summary>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>Gets the list box that created this container.</summary>
    public ListBox? ParentListBox { get; internal set; }

    /// <summary>Gets the item this container displays.</summary>
    public object? ItemValue { get; internal set; }

    /// <summary>Gets the index of <see cref="ItemValue"/> in the owner's <see cref="ItemsControl.Items"/>.</summary>
    public int Index { get; internal set; } = -1;

    /// <summary>Occurs when the item is clicked with the left button (or activated with Enter/Space).</summary>
    public event EventHandler? Clicked;

    static ListBoxItem()
    {
        PaddingProperty.OverrideDefaultValue<ListBoxItem>(new Thickness(12, 8));
        CornerRadiusProperty.OverrideDefaultValue<ListBoxItem>(new CornerRadius(4));
        ClipToBoundsProperty.OverrideDefaultValue<ListBoxItem>(true);
    }

    /// <summary>Initializes a new <see cref="ListBoxItem"/>.</summary>
    public ListBoxItem()
    {
        IsFocusable = false; // ListBox itself is the single focusable tab stop
    }

    /// <inheritdoc/>
    /// <remarks>The whole item is one hit target; its content is not hit-tested.</remarks>
    public override UIElement? HitTest(Point point)
    {
        if (Visibility != Visibility.Visible || !IsHitTestVisible || !Bounds.Contains(point))
        {
            return null;
        }

        return this;
    }

    /// <inheritdoc/>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;
    }

    /// <inheritdoc/>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        bool wasPressed = IsPressed;
        base.OnPointerReleased(e);
        e.Handled = true;

        if (wasPressed && IsHovered && IsEnabled && e.Button == PointerButtons.Left)
        {
            Activate();
        }
    }

    /// <inheritdoc/>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsEnabled && (e.Key is Key.Enter or Key.Space))
        {
            Activate();
            e.Handled = true;
        }
    }

    private void Activate()
    {
        Clicked?.Invoke(this, EventArgs.Empty);
        ParentListBox?.OnContainerClicked(this);
    }
}

/// <summary>
/// A list of items with single selection, keyboard navigation and type-ahead search.
/// </summary>
/// <remarks>
/// <para>
/// Selection is index based: <see cref="SelectedIndex"/> identifies the selected position, so lists with duplicate items
/// select and highlight exactly one entry. Selection follows changes to <see cref="ItemsControl.Items"/>: inserting or
/// moving items updates <see cref="SelectedIndex"/>, removing or replacing the selected item clears the selection.
/// </para>
/// <para>
/// While <see cref="ItemsControl.Items"/> is empty, <see cref="SelectedIndex"/> and <see cref="SelectedItem"/> keep the
/// requested value as a pending selection, which is applied once items arrive (so they can be set or bound before
/// <see cref="ItemsControl.ItemsSource"/>). Otherwise an out-of-range index is coerced to -1 and an item that is not in
/// <see cref="ItemsControl.Items"/> is coerced to <c>null</c>.
/// </para>
/// <para>
/// Keys: Up/Down, Home/End and PageUp/PageDown (by the viewport height, using the real container heights) move the
/// selection; typing selects the next item whose text starts with the typed prefix (see <see cref="IsTextSearchEnabled"/>).
/// </para>
/// </remarks>
public class ListBox : ItemsControl
{
    /// <summary>Identifies the <see cref="SelectedItem"/> bindable property.</summary>
    public static readonly BindableProperty<object?> SelectedItemProperty =
        BindableProperty.Register<ListBox, object?>(
            nameof(SelectedItem),
            null,
            (s, o, n) => ((ListBox)s).OnSelectedItemChanged(o, n),
            coerceValue: (s, v) => ((ListBox)s).CoerceSelectedItem(v)
        );

    /// <summary>Identifies the <see cref="SelectedIndex"/> bindable property.</summary>
    public static readonly BindableProperty<int> SelectedIndexProperty =
        BindableProperty.Register<ListBox, int>(
            nameof(SelectedIndex),
            -1,
            (s, o, n) => ((ListBox)s).OnSelectedIndexChanged(o, n),
            coerceValue: (s, v) => ((ListBox)s).CoerceSelectedIndex(v)
        );

    /// <summary>Identifies the <see cref="IsTextSearchEnabled"/> bindable property.</summary>
    public static readonly BindableProperty<bool> IsTextSearchEnabledProperty =
        BindableProperty.Register<ListBox, bool>(nameof(IsTextSearchEnabled), true);

    /// <summary>
    /// Gets or sets the selected item, or <c>null</c> for no selection. Default <c>null</c>.
    /// </summary>
    /// <remarks>
    /// With duplicate items, setting an item that equals the current selection keeps the selected position; otherwise the
    /// first equal item is selected.
    /// </remarks>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <summary>
    /// Gets or sets the index of the selected item in <see cref="ItemsControl.Items"/>, or -1 for no selection. Default -1.
    /// </summary>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <summary>
    /// Gets or sets whether typing selects the next item whose text (<see cref="object.ToString"/>) starts with the typed
    /// characters. Characters typed within one second extend the prefix; repeating one character cycles through the
    /// items starting with it. Default <c>true</c>.
    /// </summary>
    public bool IsTextSearchEnabled
    {
        get => GetValue(IsTextSearchEnabledProperty);
        set => SetValue(IsTextSearchEnabledProperty, value);
    }

    /// <summary>
    /// Occurs when the selection changes; the argument is the new <see cref="SelectedItem"/>.
    /// </summary>
    /// <remarks>
    /// Raised once per selection change, after both <see cref="SelectedIndex"/> and <see cref="SelectedItem"/> are
    /// updated. Not raised when only the index shifts because items were inserted or removed before the selection.
    /// </remarks>
    public event EventHandler<object?>? SelectionChanged;

    // The ComboBox that uses this list as its drop-down: clicks commit there instead of selecting here.
    internal ComboBox? OwnerComboBox { get; set; }

    private ListBoxItem? _selectedContainer;
    private bool _isSyncingSelection;
    private bool _isFocusingFromPointer;
    private int _pendingScrollIndex = -1;
    private TextSearchState? _textSearch;

    /// <summary>Initializes a new, empty <see cref="ListBox"/>.</summary>
    public ListBox()
    {
        IsFocusable = true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Gaining focus from the keyboard selects the first item if nothing is selected; focus gained by clicking an item
    /// only selects the clicked item.
    /// </remarks>
    public override void OnGotFocus()
    {
        base.OnGotFocus();
        if (!_isFocusingFromPointer && SelectedIndex < 0 && Items.Count > 0)
        {
            SelectedIndex = 0;
        }
        _selectedContainer?.InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void OnLostFocus()
    {
        base.OnLostFocus();
        _selectedContainer?.InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!IsEnabled || Items.Count == 0) return;

        int current = SelectedIndex;
        int target;
        switch (e.Key)
        {
            case Key.Down:
                target = current < 0 ? 0 : Math.Min(current + 1, Items.Count - 1);
                break;
            case Key.Up:
                target = current <= 0 ? 0 : current - 1;
                break;
            case Key.Home:
                target = 0;
                break;
            case Key.End:
                target = Items.Count - 1;
                break;
            case Key.PageDown:
                target = GetPageTarget(current, 1);
                break;
            case Key.PageUp:
                target = GetPageTarget(current, -1);
                break;
            default:
                return;
        }

        if (target != current)
        {
            SelectedIndex = target;
        }
        ScrollIntoView(target);
        e.Handled = true;
    }

    /// <inheritdoc/>
    public override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || !IsEnabled || !IsTextSearchEnabled)
        {
            return;
        }

        int match = (_textSearch ??= new TextSearchState()).Find(Items, SelectedIndex, e.Text);
        if (match >= 0)
        {
            SelectedIndex = match;
            ScrollIntoView(match);
            e.Handled = true;
        }
    }

    // Moves by roughly one viewport of items, measured with the real container heights.
    private int GetPageTarget(int current, int direction)
    {
        int count = Items.Count;
        int index = current < 0 ? 0 : current;
        float viewport = ScrollViewer.Viewport.Height > 0 && !float.IsInfinity(ScrollViewer.Viewport.Height)
            ? ScrollViewer.Viewport.Height
            : Bounds.Height;

        float travelled = 0f;
        while (true)
        {
            int next = index + direction;
            if (next < 0 || next >= count)
            {
                break;
            }

            float height = ContainerFromIndex(next)!.Bounds.Height;
            if (height <= 0 || viewport <= 0)
            {
                // Not laid out yet: move a single item.
                return index == current ? next : index;
            }

            travelled += height;
            if (travelled > viewport && index != current)
            {
                break;
            }
            index = next;
        }

        return index;
    }

    /// <summary>
    /// Scrolls so the item at <paramref name="index"/> is fully visible. If layout is pending, scrolling happens after the
    /// next arrange, when the item's position is known.
    /// </summary>
    /// <param name="index">The item index; out-of-range values are ignored.</param>
    public void ScrollIntoView(int index)
    {
        if (index < 0 || index >= ContainerCount) return;

        if (!IsArrangeValid || !ContainerFromIndex(index)!.IsArrangeValid)
        {
            _pendingScrollIndex = index;
            return;
        }

        ScrollIntoViewCore(index);
    }

    private void ScrollIntoViewCore(int index)
    {
        var container = ContainerFromIndex(index)!;
        float itemTop = container.Bounds.Y;
        float itemBottom = itemTop + container.Bounds.Height;

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

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);

        if (_pendingScrollIndex >= 0)
        {
            int index = _pendingScrollIndex;
            _pendingScrollIndex = -1;
            if (index < ContainerCount)
            {
                ScrollIntoViewCore(index);
            }
        }

        return result;
    }

    /// <summary>Creates a <see cref="ListBoxItem"/> wrapping the base visual for <paramref name="item"/>.</summary>
    /// <param name="item">The item.</param>
    /// <returns>The new container.</returns>
    protected override UIElement CreateContainerForItem(object item)
    {
        return new ListBoxItem
        {
            ParentListBox = this,
            ItemValue = item,
            Content = base.CreateContainerForItem(item)
        };
    }

    internal void OnContainerClicked(ListBoxItem container)
    {
        int index = container.Index;
        if (OwnerComboBox != null)
        {
            OwnerComboBox.OnDropDownItemClicked(index);
            return;
        }

        _isFocusingFromPointer = true;
        try
        {
            Focus();
        }
        finally
        {
            _isFocusingFromPointer = false;
        }

        SelectedIndex = index;
        ScrollIntoView(index);
    }

    /// <inheritdoc/>
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);

        // Keep each container's index current, starting at the first position the change affects.
        int start = e.Action switch
        {
            NotifyCollectionChangedAction.Add => e.NewStartingIndex,
            NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace => e.OldStartingIndex,
            NotifyCollectionChangedAction.Move => Math.Min(e.OldStartingIndex, e.NewStartingIndex),
            _ => 0
        };
        for (int i = Math.Max(0, start); i < ContainerCount; i++)
        {
            ((ListBoxItem)ContainerFromIndex(i)!).Index = i;
        }

        if (_pendingScrollIndex >= ContainerCount)
        {
            _pendingScrollIndex = -1;
        }

        bool wasResolved = _selectedContainer != null;
        if (e.Action != NotifyCollectionChangedAction.Add && e.Action != NotifyCollectionChangedAction.Move
            && _selectedContainer != null && _selectedContainer.Parent == null)
        {
            // The selected container was removed or regenerated.
            _selectedContainer = null;
        }

        ResyncSelection(e, wasResolved);
        OwnerComboBox?.OnDropDownItemsChanged(e);
    }

    private void ResyncSelection(NotifyCollectionChangedEventArgs e, bool wasResolved)
    {
        object? oldItem = SelectedItem;
        int newIndex = SelectionSync.ResyncIndex(Items, SelectedIndex, oldItem, wasResolved, e);

        _isSyncingSelection = true;
        try
        {
            if (newIndex >= 0)
            {
                SelectedIndex = newIndex;
                SelectedItem = Items[newIndex];
            }
            else if (newIndex == -1)
            {
                SelectedIndex = -1;
                SelectedItem = null;
            }
            // SelectionSync.Pending: nothing to resolve yet; keep the requested values.
        }
        finally
        {
            _isSyncingSelection = false;
        }

        UpdateSelectedContainer();
        if (!Equals(oldItem, SelectedItem))
        {
            SelectionChanged?.Invoke(this, SelectedItem);
        }
    }

    private int CoerceSelectedIndex(int value)
    {
        if (value < -1) return -1;
        if (Items.Count == 0) return value; // pending until items arrive
        return value < Items.Count ? value : -1;
    }

    private object? CoerceSelectedItem(object? value)
    {
        if (value == null || Items.Count == 0) return value;
        return SelectionSync.IndexOf(Items, value, SelectedIndex) >= 0 ? value : null;
    }

    private void OnSelectedItemChanged(object? oldItem, object? newItem)
    {
        if (_isSyncingSelection) return;

        int index;
        _isSyncingSelection = true;
        try
        {
            index = newItem == null ? -1 : SelectionSync.IndexOf(Items, newItem, SelectedIndex);
            SelectedIndex = index;
        }
        finally
        {
            _isSyncingSelection = false;
        }

        UpdateSelectedContainer();
        if (index >= 0) ScrollIntoView(index);
        SelectionChanged?.Invoke(this, newItem);
    }

    private void OnSelectedIndexChanged(int oldIndex, int newIndex)
    {
        if (_isSyncingSelection) return;

        object? oldItem = SelectedItem;
        bool inRange = newIndex >= 0 && newIndex < Items.Count;
        _isSyncingSelection = true;
        try
        {
            if (inRange)
            {
                SelectedItem = Items[newIndex];
            }
            else if (newIndex < 0 || Items.Count > 0)
            {
                SelectedItem = null;
            }
        }
        finally
        {
            _isSyncingSelection = false;
        }

        UpdateSelectedContainer();
        if (inRange) ScrollIntoView(newIndex);
        if (inRange || !Equals(oldItem, SelectedItem))
        {
            SelectionChanged?.Invoke(this, SelectedItem);
        }
    }

    private void UpdateSelectedContainer()
    {
        var container = ContainerFromIndex(SelectedIndex) as ListBoxItem;
        if (container == _selectedContainer) return;

        if (_selectedContainer != null) _selectedContainer.IsSelected = false;
        _selectedContainer = container;
        if (container != null) container.IsSelected = true;
    }
}

/// <summary>Selection bookkeeping shared by <see cref="ListBox"/> and <see cref="ComboBox"/>.</summary>
internal static class SelectionSync
{
    /// <summary>Returned by <see cref="ResyncIndex"/> when a pending selection still can't be resolved.</summary>
    public const int Pending = -2;

    /// <summary>
    /// Returns the index of <paramref name="item"/>, preferring <paramref name="preferredIndex"/> if the item is there
    /// (keeps the selected position among duplicates), or -1.
    /// </summary>
    public static int IndexOf(IList<object> items, object item, int preferredIndex)
    {
        if ((uint)preferredIndex < (uint)items.Count && Equals(items[preferredIndex], item))
        {
            return preferredIndex;
        }
        return items.IndexOf(item);
    }

    /// <summary>
    /// Computes the selected index after <paramref name="e"/> was applied to <paramref name="items"/>: -1 if the selected
    /// item was removed or replaced, the shifted index otherwise, or <see cref="Pending"/> to keep a pending selection.
    /// </summary>
    /// <param name="items">The items, after the change.</param>
    /// <param name="index">The selected index before the change.</param>
    /// <param name="item">The selected item.</param>
    /// <param name="resolved">Whether the selection referred to an actual item before the change.</param>
    /// <param name="e">The change.</param>
    public static int ResyncIndex(IList<object> items, int index, object? item, bool resolved, NotifyCollectionChangedEventArgs e)
    {
        int count = items.Count;
        if (!resolved)
        {
            // Pending selection (requested while there were no items) or none.
            if (item != null)
            {
                return count == 0 ? Pending : items.IndexOf(item);
            }
            if (index >= 0)
            {
                return count == 0 ? Pending : (index < count ? index : -1);
            }
            return -1;
        }

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                return index >= e.NewStartingIndex ? index + e.NewItems!.Count : index;

            case NotifyCollectionChangedAction.Remove:
            {
                int removed = e.OldItems!.Count;
                if (index < e.OldStartingIndex) return index;
                return index < e.OldStartingIndex + removed ? -1 : index - removed;
            }

            case NotifyCollectionChangedAction.Replace:
                return index >= e.OldStartingIndex && index < e.OldStartingIndex + e.OldItems!.Count ? -1 : index;

            case NotifyCollectionChangedAction.Move:
            {
                int moved = e.OldItems!.Count;
                if (index >= e.OldStartingIndex && index < e.OldStartingIndex + moved)
                {
                    return e.NewStartingIndex + (index - e.OldStartingIndex);
                }
                if (index > e.OldStartingIndex) index -= moved;
                if (index >= e.NewStartingIndex) index += moved;
                return index;
            }

            default:
                if (item == null) return -1;
                return IndexOf(items, item, index);
        }
    }
}

/// <summary>Prefix-matching type-ahead search over item texts.</summary>
internal sealed class TextSearchState
{
    /// <summary>Typing pauses longer than this start a new prefix.</summary>
    public const int TimeoutMilliseconds = 1000;

    private string _prefix = string.Empty;
    private long _lastInputTime;

    /// <summary>
    /// Adds <paramref name="text"/> to the search prefix and returns the index of the first matching item at or after the
    /// current one (after it for a single character, so repeated presses cycle), or -1.
    /// </summary>
    public int Find(IList<object> items, int currentIndex, string text)
    {
        if (items.Count == 0 || string.IsNullOrEmpty(text) || char.IsControl(text[0]))
        {
            return -1;
        }

        long now = Environment.TickCount64;
        if (now - _lastInputTime > TimeoutMilliseconds)
        {
            _prefix = string.Empty;
        }
        _lastInputTime = now;
        _prefix += text;

        if (_prefix.Length > 1)
        {
            int match = FindFrom(items, Math.Max(currentIndex, 0), _prefix);
            if (match >= 0 || !IsSingleRepeatedCharacter(_prefix))
            {
                return match;
            }
        }

        // One character, or the same character repeated: cycle through the items starting with it.
        return FindFrom(items, currentIndex + 1, _prefix.Substring(0, 1));
    }

    private static int FindFrom(IList<object> items, int start, string prefix)
    {
        int count = items.Count;
        for (int i = 0; i < count; i++)
        {
            int index = (start + i) % count;
            string? itemText = items[index]?.ToString();
            if (itemText != null && itemText.StartsWith(prefix, StringComparison.CurrentCultureIgnoreCase))
            {
                return index;
            }
        }
        return -1;
    }

    private static bool IsSingleRepeatedCharacter(string text)
    {
        for (int i = 1; i < text.Length; i++)
        {
            if (char.ToUpperInvariant(text[i]) != char.ToUpperInvariant(text[0])) return false;
        }
        return true;
    }
}
