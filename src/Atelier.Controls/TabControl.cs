using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Numerics;
using Atelier.Core.Animation;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>The look of a <see cref="TabControl"/>'s tabs.</summary>
public enum TabStyle
{
    /// <summary>MD3 primary tabs: the selected label in primary, over a 3 px indicator as wide as the label.</summary>
    Primary,

    /// <summary>MD3 secondary tabs: the selected label in on-surface, over a 2 px indicator as wide as the tab.</summary>
    Secondary,

    /// <summary>
    /// Browser/editor tabs: separate tab shapes on a tinted strip, the selected one in the content's color, typically
    /// with close buttons.
    /// </summary>
    Browser,
}

/// <summary>Provides data for <see cref="TabControl.TabClosing"/>; set <see cref="System.ComponentModel.CancelEventArgs.Cancel"/> to keep the tab.</summary>
/// <param name="item">The item of the tab.</param>
/// <param name="index">The index of the tab.</param>
public class TabClosingEventArgs(object item, int index) : System.ComponentModel.CancelEventArgs
{
    /// <summary>Gets the item of the tab (the tab item itself for tabs added as <see cref="TabItem"/>s).</summary>
    public object Item { get; } = item;

    /// <summary>Gets the index of the tab.</summary>
    public int Index { get; } = index;
}

/// <summary>Provides data for <see cref="TabControl.AddTabRequested"/>.</summary>
public class AddTabRequestedEventArgs : EventArgs
{
    /// <summary>
    /// Gets or sets the item to add as a new tab (a <see cref="TabItem"/> or a data item). The tab control adds it to the
    /// end and selects it. Leave it <c>null</c> when the handler adds the item to the bound collection itself; the new tab
    /// is selected either way.
    /// </summary>
    public object? NewItem { get; set; }
}

/// <summary>Provides data for <see cref="TabControl.TabMoved"/>.</summary>
/// <param name="item">The moved item.</param>
/// <param name="oldIndex">The index before the move.</param>
/// <param name="newIndex">The index after the move.</param>
public class TabMovedEventArgs(object item, int oldIndex, int newIndex) : EventArgs
{
    /// <summary>Gets the moved item.</summary>
    public object Item { get; } = item;

    /// <summary>Gets the index before the move.</summary>
    public int OldIndex { get; } = oldIndex;

    /// <summary>Gets the index after the move.</summary>
    public int NewIndex { get; } = newIndex;

    /// <summary>
    /// Gets or sets whether the handler moved the item in the bound collection itself; otherwise the tab control moves it
    /// (removing and inserting it in an <see cref="IList"/> source).
    /// </summary>
    public bool Handled { get; set; }
}

/// <summary>
/// Shows one of several pages under a strip of tabs, in the Material Design 3 primary or secondary style or as
/// browser-style tabs.
/// </summary>
/// <remarks>
/// <para>
/// Tabs are <see cref="TabItem"/>s in <see cref="ItemsControl.Items"/>, or data items from
/// <see cref="ItemsControl.ItemsSource"/>: then <see cref="HeaderTemplate"/> builds each header (for example an icon and
/// a title) and <see cref="ContentTemplate"/> the page, or the view locator finds a view for the item.
/// </para>
/// <para>
/// Closeable tabs (<see cref="AreTabsCloseable"/> or <see cref="TabItem.IsCloseable"/>) have a close button and close
/// with a middle click or Delete; <see cref="TabClosing"/> can keep them. Closing removes the item from
/// <see cref="ItemsControl.Items"/>, or from the bound collection when it is an <see cref="IList"/>. With
/// <see cref="ShowAddButton"/>, a "+" button after the tabs raises <see cref="AddTabRequested"/>.
/// </para>
/// <para>
/// With <see cref="CanReorderTabs"/>, dragging a tab along the strip moves it; the other tabs slide aside, and
/// <see cref="TabMoved"/> reports the move when the tab is dropped.
/// </para>
/// <para>
/// The strip is one tab stop: Left/Right and Home/End select the neighboring, first and last tab, and Ctrl+Page Up/Page
/// Down switch tabs from anywhere in the control. The strip scrolls when the tabs don't fit.
/// </para>
/// </remarks>
public class TabControl : ItemsControl
{
    /// <summary>Identifies the <see cref="SelectedIndex"/> property.</summary>
    public static readonly BindableProperty<int> SelectedIndexProperty =
        BindableProperty.Register<TabControl, int>(nameof(SelectedIndex), -1, (s, o, n) => ((TabControl)s).OnSelectedIndexChanged(o, n),
            coerceValue: (s, v) => ((TabControl)s).CoerceIndex(v));

    /// <summary>Identifies the <see cref="TabStyle"/> property.</summary>
    public static readonly BindableProperty<TabStyle> TabStyleProperty =
        BindableProperty.Register<TabControl, TabStyle>(nameof(TabStyle), TabStyle.Primary, (s, o, n) => ((TabControl)s).OnTabStyleChanged(),
            options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="AreTabsCloseable"/> property.</summary>
    public static readonly BindableProperty<bool> AreTabsCloseableProperty =
        BindableProperty.Register<TabControl, bool>(nameof(AreTabsCloseable), false, (s, o, n) => ((TabControl)s).ForEachTab(t => t.UpdateCloseButton()));

    /// <summary>Identifies the <see cref="ShowAddButton"/> property.</summary>
    public static readonly BindableProperty<bool> ShowAddButtonProperty =
        BindableProperty.Register<TabControl, bool>(nameof(ShowAddButton), false, (s, o, n) => ((TabControl)s).OnShowAddButtonChanged(n));

    /// <summary>Identifies the <see cref="CanReorderTabs"/> property.</summary>
    public static readonly BindableProperty<bool> CanReorderTabsProperty =
        BindableProperty.Register<TabControl, bool>(nameof(CanReorderTabs), true);

    /// <summary>The style key of the "+" button.</summary>
    public const string AddButtonStyleKey = "TabAddButton";

    /// <summary>How far the pointer must move before a pressed tab starts to drag.</summary>
    public const float DragThreshold = 6f;

    private static AnimationClock? s_clock;

    private readonly Button _addButton;
    private readonly ContentControl _content = new();
    private Func<object, UIElement>? _headerTemplate;
    private Func<object?, UIElement?>? _contentTemplate;
    private object? _selectedItem;
    private bool _isUpdatingSelection;
    private bool _selectNextAddedItem;
    private object? _reselectAfterChange;
    private bool _bringSelectedIntoView;

    // The indicator slides from the previous tab: from this rect (in item panel coordinates) to the selected tab's.
    private Rect _indicatorFrom;
    private float _indicatorProgress = 1f;
    private FloatAnimation? _indicatorAnimation;

    // Dragging a tab.
    private TabItem? _pressedTab;
    private Point _pressPosition;
    private bool _isDragging;
    private int _dragStartIndex;
    private float _dragGrabOffset;
    private readonly Dictionary<TabItem, FloatAnimation> _slides = [];

    /// <summary>
    /// Sets the clock that animates the indicator and the tabs sliding while dragging for all tab controls; the platform
    /// layer calls this at startup. Without a clock they move at once.
    /// </summary>
    public static void SetGlobalAnimationClock(AnimationClock? clock) => s_clock = clock;

    /// <summary>Initializes an empty tab control with MD3 primary tabs.</summary>
    public TabControl()
    {
        ItemPanel.Orientation = Orientation.Horizontal;
        ScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
        ScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        ScrollViewer.IsScrollAnimationEnabled = true;

        _addButton = new Button
        {
            Variant = ButtonVariant.Text,
            Content = new Icon(MaterialIconKind.Add, 20),
            Width = 36,
            Height = 36,
            MinWidth = 36,
            MinHeight = 36,
            Padding = new Thickness(8),
            CornerRadius = new CornerRadius(18),
            Margin = new Thickness(4, 0),
            Visibility = Visibility.Collapsed,
            StyleKey = AddButtonStyleKey,
        };
        ToolTipService.SetToolTip(_addButton, "New tab");
        _addButton.Click += (_, _) => RequestAddTab();

        AddChild(_addButton);
        AddChild(_content);
    }

    #region Properties

    /// <summary>Gets or sets the index of the selected tab; -1 only while there are no tabs.</summary>
    public int SelectedIndex { get => GetValue(SelectedIndexProperty); set => SetValue(SelectedIndexProperty, value); }

    /// <summary>Gets or sets the selected item (a tab item or a data item), or <c>null</c> without tabs.</summary>
    public object? SelectedItem
    {
        get => _selectedItem;
        set
        {
            int index = value == null ? -1 : Items.IndexOf(value);
            if (index >= 0) SelectedIndex = index;
        }
    }

    /// <summary>Gets the tab of the selected item, or <c>null</c>.</summary>
    public TabItem? SelectedTab => ContainerFromIndex(SelectedIndex) as TabItem;

    /// <summary>Gets or sets the look of the tabs. The default is <see cref="TabStyle.Primary"/>.</summary>
    public TabStyle TabStyle { get => GetValue(TabStyleProperty); set => SetValue(TabStyleProperty, value); }

    /// <summary>
    /// Gets or sets whether tabs have a close button (a tab's <see cref="TabItem.IsCloseable"/> overrides it). The default
    /// is <c>false</c>.
    /// </summary>
    public bool AreTabsCloseable { get => GetValue(AreTabsCloseableProperty); set => SetValue(AreTabsCloseableProperty, value); }

    /// <summary>Gets or sets whether a "+" button after the tabs raises <see cref="AddTabRequested"/>. The default is <c>false</c>.</summary>
    public bool ShowAddButton { get => GetValue(ShowAddButtonProperty); set => SetValue(ShowAddButtonProperty, value); }

    /// <summary>Gets or sets whether tabs can be dragged to a new place. The default is <c>true</c>.</summary>
    public bool CanReorderTabs { get => GetValue(CanReorderTabsProperty); set => SetValue(CanReorderTabsProperty, value); }

    /// <summary>
    /// Gets or sets the factory of the header content for an item, such as an icon and a title; its DataContext is the
    /// item. <c>null</c> (the default) shows <see cref="TabItem.Header"/>, or the item's text for data items.
    /// </summary>
    public Func<object, UIElement>? HeaderTemplate
    {
        get => _headerTemplate;
        set
        {
            _headerTemplate = value;
            ForEachTab(t => t.RebuildHeader());
        }
    }

    /// <summary>
    /// Gets or sets the factory of the page for a data item; <c>null</c> (the default) finds a view through the view
    /// locator. Not used for <see cref="TabItem"/>s, which have their own <see cref="TabItem.Content"/>.
    /// </summary>
    public Func<object?, UIElement?>? ContentTemplate
    {
        get => _contentTemplate;
        set
        {
            _contentTemplate = value;
            UpdateContent();
        }
    }

    /// <summary>Gets the "+" button.</summary>
    public Button AddButton => _addButton;

    /// <summary>Gets the presenter of the selected page.</summary>
    public ContentControl ContentPresenter => _content;

    /// <summary>Gets the tab being dragged, or <c>null</c>.</summary>
    public TabItem? DraggedTab => _isDragging ? _pressedTab : null;

    /// <summary>Occurs when the selected tab changes, with the new selected item.</summary>
    public event EventHandler<object?>? SelectionChanged;

    /// <summary>Occurs before a tab closes; set <see cref="System.ComponentModel.CancelEventArgs.Cancel"/> to keep it.</summary>
    public event EventHandler<TabClosingEventArgs>? TabClosing;

    /// <summary>Occurs after a tab closed, with its item.</summary>
    public event EventHandler<object>? TabClosed;

    /// <summary>Occurs when the "+" button is clicked; set <see cref="AddTabRequestedEventArgs.NewItem"/> or add an item to the bound collection.</summary>
    public event EventHandler<AddTabRequestedEventArgs>? AddTabRequested;

    /// <summary>Occurs when a dragged tab was dropped at a new place, before the item is moved.</summary>
    public event EventHandler<TabMovedEventArgs>? TabMoved;

    /// <summary>Gets the tab at <paramref name="index"/>, or <c>null</c>.</summary>
    public TabItem? GetTab(int index) => ContainerFromIndex(index) as TabItem;

    private void ForEachTab(Action<TabItem> action)
    {
        for (int i = 0; i < ContainerCount; i++)
        {
            if (ContainerFromIndex(i) is TabItem tab) action(tab);
        }
    }

    #endregion

    #region Items and selection

    /// <inheritdoc/>
    /// <remarks>A <see cref="TabItem"/> is its own container; other items get a new tab item with the item as DataContext.</remarks>
    protected override UIElement CreateContainerForItem(object item)
    {
        var tab = item as TabItem ?? new TabItem { Header = item, DataContext = item };
        tab.Attach(this, item);
        tab.IsFocusable = false;
        return tab;
    }

    /// <inheritdoc/>
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);

        // Detach tabs that left the strip (tab items can be reused elsewhere).
        if (e.OldItems != null)
        {
            foreach (var old in e.OldItems)
            {
                if (old is TabItem { Owner: not null } tab && !Items.Contains(tab)) tab.Detach();
            }
        }

        int index;
        if (_reselectAfterChange != null)
        {
            // Moving an item in the bound collection (remove, then insert): keep the selection on the same item, and
            // leave it alone while the item is out of the collection.
            index = Items.IndexOf(_reselectAfterChange);
            if (index < 0) return;
        }
        else if (_selectNextAddedItem && e.Action == NotifyCollectionChangedAction.Add)
        {
            index = e.NewStartingIndex;
            _selectNextAddedItem = false;
        }
        else if (_selectedItem != null && Items.IndexOf(_selectedItem) is var kept && kept >= 0)
        {
            index = kept;
        }
        else
        {
            // The selected tab went away: select the one that took its place (or the new last one).
            index = Items.Count == 0 ? -1 : Math.Clamp(SelectedIndex < 0 ? 0 : SelectedIndex, 0, Items.Count - 1);
        }

        ApplySelection(index, force: true);
    }

    private int CoerceIndex(int index) => Items.Count == 0 ? -1 : Math.Clamp(index, 0, Items.Count - 1);

    private void OnSelectedIndexChanged(int oldIndex, int newIndex)
    {
        if (!_isUpdatingSelection) ApplySelection(newIndex, force: false);
    }

    private void ApplySelection(int index, bool force)
    {
        index = CoerceIndex(index);
        var item = index >= 0 ? Items[index] : null;
        bool changed = !ReferenceEquals(item, _selectedItem);
        var previousTab = _selectedItem != null ? GetTab(Items.IndexOf(_selectedItem)) : null;

        _isUpdatingSelection = true;
        try
        {
            SelectedIndex = index;
        }
        finally
        {
            _isUpdatingSelection = false;
        }

        if (!changed && !force) return;
        _selectedItem = item;

        bool hadFocus = false;
        for (int i = 0; i < ContainerCount; i++)
        {
            if (GetTab(i) is not { } tab) continue;
            hadFocus |= tab.IsFocused;
            tab.IsSelected = i == index;
            tab.IsFocusable = i == index; // the strip is one tab stop
        }

        var selectedTab = GetTab(index);
        if (changed)
        {
            StartIndicatorSlide(previousTab);
            UpdateContent();
            if (hadFocus) selectedTab?.Focus();
            // Scroll the strip to the tab once it is laid out (a new tab has no bounds yet).
            _bringSelectedIntoView = true;
            InvalidateArrange();
            SelectionChanged?.Invoke(this, item);
        }
        InvalidateVisual();
    }

    private void UpdateContent()
    {
        var item = _selectedItem;
        if (item is TabItem tab)
        {
            _content.ContentTemplate = null;
            _content.Content = tab.Content;
        }
        else
        {
            _content.ContentTemplate = _contentTemplate;
            _content.Content = item;
        }
    }

    internal void OnTabContentChanged(TabItem tab)
    {
        if (ReferenceEquals(tab, _selectedItem)) UpdateContent();
    }

    private void OnTabStyleChanged()
    {
        ForEachTab(t => t.RebuildHeader());
        InvalidateMeasure();
    }

    private void OnShowAddButtonChanged(bool show)
    {
        _addButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        InvalidateMeasure();
    }

    #endregion

    #region Closing and adding

    /// <summary>Closes the tab of <paramref name="tab"/> (see <see cref="CloseTab(int)"/>).</summary>
    public bool CloseTab(TabItem tab) => IndexFromContainer(tab) is var index && index >= 0 && CloseTab(index);

    /// <summary>
    /// Closes the tab at <paramref name="index"/>: raises <see cref="TabClosing"/>, then removes the item from
    /// <see cref="ItemsControl.Items"/>, or from the bound collection when it is an <see cref="IList"/> that can change,
    /// and raises <see cref="TabClosed"/>. The next tab (or the previous, for the last) is selected when the selected tab
    /// closes.
    /// </summary>
    /// <returns><c>true</c> if the tab was closed.</returns>
    public bool CloseTab(int index)
    {
        if (index < 0 || index >= Items.Count) return false;
        var item = Items[index];

        var closing = new TabClosingEventArgs(item, index);
        TabClosing?.Invoke(this, closing);
        if (closing.Cancel) return false;

        if (!TryRemoveItem(index)) return false;
        TabClosed?.Invoke(this, item);
        return true;
    }

    private bool TryRemoveItem(int index)
    {
        if (ItemsSource == null)
        {
            Items.RemoveAt(index);
            return true;
        }
        if (ItemsSource is IList { IsReadOnly: false, IsFixedSize: false } list)
        {
            list.RemoveAt(index);
            return true;
        }
        return false;
    }

    private bool TryAddItem(object item)
    {
        if (ItemsSource == null)
        {
            Items.Add(item);
            return true;
        }
        if (ItemsSource is IList { IsReadOnly: false, IsFixedSize: false } list)
        {
            list.Add(item);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Asks for a new tab like the "+" button: raises <see cref="AddTabRequested"/>, adds the returned item, and selects
    /// the new tab.
    /// </summary>
    public void RequestAddTab()
    {
        var args = new AddTabRequestedEventArgs();
        _selectNextAddedItem = true;
        try
        {
            AddTabRequested?.Invoke(this, args);
            if (args.NewItem != null) TryAddItem(args.NewItem);
        }
        finally
        {
            _selectNextAddedItem = false;
        }
    }

    #endregion

    #region Indicator

    // The indicator's place under the selected tab (in item panel coordinates), following a dragged tab.
    private Rect GetIndicatorSlot(TabItem? tab)
    {
        if (tab == null) return Rect.Zero;
        var bounds = tab.Bounds;
        float dx = tab.RenderTransform.M31;
        if (TabStyle == TabStyle.Primary)
        {
            var label = tab.LabelBounds;
            float width = Math.Max(24f, label.Width);
            float x = bounds.X + dx + label.X + (label.Width - width) * 0.5f;
            return new Rect(x, bounds.Bottom - 3f, width, 3f);
        }
        return new Rect(bounds.X + dx, bounds.Bottom - 2f, bounds.Width, 2f);
    }

    private void StartIndicatorSlide(TabItem? previousTab)
    {
        _indicatorAnimation?.Stop();
        _indicatorAnimation = null;
        if (previousTab == null || s_clock == null || TabStyle == TabStyle.Browser || previousTab.Bounds.Width <= 0)
        {
            _indicatorProgress = 1f;
            return;
        }

        _indicatorFrom = GetIndicatorSlot(previousTab);
        _indicatorProgress = 0f;
        _indicatorAnimation = new FloatAnimation(0f, 1f, TimeSpan.FromMilliseconds(250), p =>
        {
            _indicatorProgress = p;
            InvalidateVisual();
        }, Easing.EaseOutCubic);
        s_clock.Add(_indicatorAnimation);
    }

    /// <summary>
    /// Gets the MD3 indicator under the selected tab, in this control's coordinates, while it slides from the previous
    /// tab; empty for browser tabs or without a selection.
    /// </summary>
    public Rect IndicatorBounds
    {
        get
        {
            if (TabStyle == TabStyle.Browser || SelectedTab is not { } tab) return Rect.Zero;
            var target = GetIndicatorSlot(tab);
            var rect = _indicatorProgress >= 1f ? target : new Rect(
                _indicatorFrom.X + (target.X - _indicatorFrom.X) * _indicatorProgress,
                target.Y,
                _indicatorFrom.Width + (target.Width - _indicatorFrom.Width) * _indicatorProgress,
                target.Height);
            var strip = ScrollViewer.Bounds;
            return new Rect(strip.X + rect.X - ScrollViewer.ScrollOffsetX, strip.Y + rect.Y, rect.Width, rect.Height);
        }
    }

    /// <summary>Gets the bounds of the tab strip (the tabs and the "+" button) in this control's coordinates.</summary>
    public Rect StripBounds => new(0, 0, Bounds.Width, ScrollViewer.Bounds.Height);

    /// <summary>Gets the bounds of the visible tabs area in this control's coordinates.</summary>
    public Rect TabsViewportBounds => ScrollViewer.Bounds;

    #endregion

    #region Dragging

    internal void OnTabPressed(TabItem tab, PointerEventArgs e)
    {
        int index = IndexFromContainer(tab);
        if (index < 0) return;

        SelectedIndex = index;
        tab.Focus();
        tab.CapturePointer();
        _pressedTab = tab;
        _pressPosition = e.ScreenPosition;
        _isDragging = false;
    }

    internal void OnTabDragMoved(TabItem tab, PointerEventArgs e)
    {
        if (tab != _pressedTab) return;
        if (!_isDragging)
        {
            if (!CanReorderTabs || Items.Count < 2 || MathF.Abs(e.ScreenPosition.X - _pressPosition.X) < DragThreshold) return;
            _isDragging = true;
            tab.IsDragging = true;
            tab.ZIndex = 1; // drawn above the tabs it passes
            _dragStartIndex = IndexFromContainer(tab);
            _dragGrabOffset = _pressPosition.X - ItemPanelToScreenX(tab.Bounds.X);
        }

        // Follow the pointer along the strip, kept within it.
        float panelX = e.ScreenPosition.X - ItemPanelToScreenX(0) - _dragGrabOffset;
        float maxX = Math.Max(0, ItemPanel.Bounds.Width - tab.Bounds.Width);
        panelX = Math.Clamp(panelX, 0, maxX);

        // Swap with a neighbor once the dragged tab's leading edge passes the neighbor's middle (reachable at both ends
        // of the strip, whatever the widths).
        int visual = VisualIndexOf(tab);
        float left = panelX, right = panelX + tab.Bounds.Width;
        while (visual + 1 < ItemPanel.Children.Count && ItemPanel.Children[visual + 1] is TabItem next && right > SlotX(visual + 1, tab) + next.Bounds.Width * 0.5f)
        {
            MoveVisual(tab, next, visual + 1, -1);
            visual++;
        }
        while (visual > 0 && ItemPanel.Children[visual - 1] is TabItem previous && left < SlotX(visual - 1, tab) + previous.Bounds.Width * 0.5f)
        {
            MoveVisual(tab, previous, visual - 1, 1);
            visual--;
        }

        tab.RenderTransform = Matrix3x2.CreateTranslation(panelX - SlotX(visual, tab), 0);
        InvalidateVisual();
    }

    // Moves the dragged tab to visual index `to`, sliding the neighbor it passed by the dragged tab's width.
    private void MoveVisual(TabItem dragged, TabItem neighbor, int to, int neighborDirection)
    {
        ItemPanel.InsertChild(to, dragged);
        float distance = dragged.Bounds.Width * neighborDirection;
        // The neighbor jumps a slot at the next layout; start it where it was and let it slide.
        Slide(neighbor, neighbor.RenderTransform.M31 - distance);
    }

    private void Slide(TabItem tab, float fromOffset)
    {
        if (_slides.Remove(tab, out var running)) running.Stop();
        if (s_clock == null || MathF.Abs(fromOffset) < 0.5f)
        {
            tab.RenderTransform = Matrix3x2.Identity;
            tab.ZIndex = 0;
            return;
        }

        tab.RenderTransform = Matrix3x2.CreateTranslation(fromOffset, 0);
        var animation = new FloatAnimation(fromOffset, 0f, TimeSpan.FromMilliseconds(180), x =>
        {
            tab.RenderTransform = MathF.Abs(x) < 0.01f ? Matrix3x2.Identity : Matrix3x2.CreateTranslation(x, 0);
            InvalidateVisual();
        }, Easing.EaseOutCubic, () =>
        {
            _slides.Remove(tab);
            tab.ZIndex = 0;
        });
        _slides[tab] = animation;
        s_clock.Add(animation);
    }

    private int VisualIndexOf(TabItem tab)
    {
        var children = ItemPanel.Children;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] == tab) return i;
        }
        return -1;
    }

    // The x where the tab at visual index `index` is laid out (in item panel coordinates), from the widths before it.
    private float SlotX(int index, TabItem dragged)
    {
        float x = 0;
        var children = ItemPanel.Children;
        for (int i = 0; i < index && i < children.Count; i++)
        {
            if (children[i] is UIElement child) x += child.Bounds.Width;
        }
        return x;
    }

    private float ItemPanelToScreenX(float x) => ItemPanel.PointToScreen(new Point(x, 0)).X;

    internal void OnTabReleased(TabItem tab)
    {
        if (tab != _pressedTab) return;
        _pressedTab = null;
        if (!_isDragging) return;

        _isDragging = false;
        tab.IsDragging = false;
        int to = VisualIndexOf(tab);
        int from = _dragStartIndex;

        // Settle into the slot.
        float offset = tab.RenderTransform.M31;
        Slide(tab, offset);

        if (to != from && to >= 0)
        {
            CommitMove(from, to);
        }
        InvalidateVisual();
    }

    internal void OnTabDragCanceled(TabItem tab)
    {
        if (tab != _pressedTab || !_isDragging) return;

        // Put the tabs back in their item order.
        _pressedTab = null;
        _isDragging = false;
        tab.IsDragging = false;
        for (int i = 0; i < ContainerCount; i++)
        {
            if (ContainerFromIndex(i) is { } container) ItemPanel.InsertChild(i, container);
        }
        Slide(tab, 0);
        InvalidateMeasure();
    }

    private void CommitMove(int from, int to)
    {
        var item = Items[from];
        var args = new TabMovedEventArgs(item, from, to);
        TabMoved?.Invoke(this, args);
        if (args.Handled)
        {
            return;
        }

        if (ItemsSource == null)
        {
            Items.Move(from, to);
        }
        else if (ItemsSource is IList { IsReadOnly: false, IsFixedSize: false } list)
        {
            // Keep the selection on the same item across the remove and insert.
            _reselectAfterChange = _selectedItem ?? item;
            try
            {
                list.RemoveAt(from);
                list.Insert(to, item);
            }
            finally
            {
                _reselectAfterChange = null;
            }
        }
        else
        {
            // A read-only source can't change: put the tab back.
            for (int i = 0; i < ContainerCount; i++)
            {
                if (ContainerFromIndex(i) is { } container) ItemPanel.InsertChild(i, container);
            }
        }
    }

    #endregion

    #region Keyboard and wheel

    /// <inheritdoc/>
    /// <remarks>See the class remarks for the keys.</remarks>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || Items.Count == 0) return;

        bool ctrl = (e.Modifiers & ModifierKeys.Control) != 0;
        if (ctrl && e.Key is Key.PageUp or Key.PageDown)
        {
            int count = Items.Count;
            SelectAndFocus((SelectedIndex + (e.Key == Key.PageDown ? 1 : count - 1)) % count, focus: e.Source is TabItem);
            e.Handled = true;
            return;
        }

        if (e.Source is not TabItem tab || tab.Owner != this) return;
        switch (e.Key)
        {
            case Key.Left: SelectAndFocus(SelectedIndex - 1, focus: true); break;
            case Key.Right: SelectAndFocus(SelectedIndex + 1, focus: true); break;
            case Key.Home: SelectAndFocus(0, focus: true); break;
            case Key.End: SelectAndFocus(Items.Count - 1, focus: true); break;
            case Key.Delete when tab.IsEffectivelyCloseable:
                CloseTab(tab);
                SelectedTab?.Focus();
                break;
            default: return;
        }
        e.Handled = true;
    }

    private void SelectAndFocus(int index, bool focus)
    {
        SelectedIndex = index;
        if (focus) SelectedTab?.Focus();
    }

    #endregion

    #region Layout

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        _addButton.Measure(availableSize);
        float addWidth = _addButton.Visibility == Visibility.Collapsed ? 0 : _addButton.DesiredSize.Width;
        ScrollViewer.Measure(new Size(Math.Max(0, availableSize.Width - addWidth), availableSize.Height));
        float stripHeight = Math.Max(ScrollViewer.DesiredSize.Height, _addButton.Visibility == Visibility.Collapsed ? 0 : _addButton.DesiredSize.Height);

        _content.Measure(new Size(availableSize.Width, Math.Max(0, availableSize.Height - stripHeight)));
        float width = Math.Max(ItemPanel.DesiredSize.Width + addWidth, _content.DesiredSize.Width);
        if (!float.IsInfinity(availableSize.Width)) width = Math.Min(width, availableSize.Width);
        return new Size(width, stripHeight + _content.DesiredSize.Height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        bool hasAdd = _addButton.Visibility != Visibility.Collapsed;
        float addWidth = hasAdd ? _addButton.DesiredSize.Width : 0;
        float stripHeight = Math.Max(ScrollViewer.DesiredSize.Height, hasAdd ? _addButton.DesiredSize.Height : 0);
        float tabsWidth = Math.Min(ItemPanel.DesiredSize.Width, Math.Max(0, finalSize.Width - addWidth));

        ScrollViewer.Arrange(new Rect(0, 0, tabsWidth, stripHeight));
        if (_bringSelectedIntoView && SelectedTab is { Bounds.Width: > 0 } selected)
        {
            _bringSelectedIntoView = false;
            selected.BringIntoView();
        }
        if (hasAdd)
        {
            var add = _addButton.DesiredSize;
            _addButton.Arrange(new Rect(tabsWidth, (stripHeight - add.Height) * 0.5f, add.Width, add.Height));
        }
        _content.Arrange(new Rect(0, stripHeight, finalSize.Width, Math.Max(0, finalSize.Height - stripHeight)));
        return finalSize;
    }

    #endregion
}
