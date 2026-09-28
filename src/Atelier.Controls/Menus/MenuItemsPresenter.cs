using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// The list of items of a submenu or context menu: the menu items and separators, in a column that scrolls when it is
/// taller than the window.
/// </summary>
/// <remarks>
/// The items share their columns: when any item has an icon (or a check mark) all of them leave room for it, and
/// likewise for submenu arrows, so labels and shortcuts line up. The keyboard moves between the items: Up/Down (wrapping),
/// Home/End, a letter jumps to the item with that access key (or first letter), Right opens a submenu and Left closes
/// this one.
/// </remarks>
public class MenuItemsPresenter : ItemsControl
{
    private IMenuRoot? _root;

    /// <summary>Initializes an empty item list.</summary>
    public MenuItemsPresenter()
    {
        ItemPanel.Margin = new Thickness(0, 8);
        ScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        ScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        MinWidth = 112; // MD3 menu minimum width
        IsFocusable = true;
    }

    /// <summary>Gets the menu item whose submenu this is, or <c>null</c> for a context menu.</summary>
    public MenuItem? OwnerItem { get; internal set; }

    /// <summary>Gets the root of the menu.</summary>
    public IMenuRoot? Root => _root;

    /// <summary>Gets whether the items leave room for an icon or check mark.</summary>
    public bool HasIconColumn { get; private set; }

    /// <summary>Gets whether the items leave room for a submenu arrow.</summary>
    public bool HasSubmenuColumn { get; private set; }

    /// <summary>Gets the menu items (not separators) in order.</summary>
    public IEnumerable<MenuItem> MenuItems
    {
        get
        {
            for (int i = 0; i < ContainerCount; i++)
            {
                if (ContainerFromIndex(i) is MenuItem item) yield return item;
            }
        }
    }

    internal void SetRoot(IMenuRoot? root)
    {
        if (ReferenceEquals(_root, root)) return;
        _root = root;
        if (ItemsSource != null)
        {
            RefreshContainers(); // generated items depend on the root's settings
        }
        else
        {
            foreach (var item in MenuItems) item.AttachToMenu(root, OwnerItem, item.DataItem);
        }
    }

    /// <inheritdoc/>
    protected override UIElement CreateContainerForItem(object item) => MenuItemFactory.CreateContainer(_root, OwnerItem, item);

    /// <inheritdoc/>
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        UpdateColumns();
        OwnerItem?.OnSubmenuItemsChanged();
    }

    /// <summary>Recomputes whether the items need the icon and arrow columns (after an item's icon or items changed).</summary>
    public void UpdateColumns()
    {
        bool icons = false, arrows = false;
        foreach (var item in MenuItems)
        {
            icons |= item.HasIcon || item.IsCheckable;
            arrows |= item.HasItems;
        }
        if (icons == HasIconColumn && arrows == HasSubmenuColumn) return;

        HasIconColumn = icons;
        HasSubmenuColumn = arrows;
        foreach (var item in MenuItems) item.InvalidateMeasure();
    }

    /// <summary>Gets the menu items that can take the keyboard focus: visible and enabled.</summary>
    private List<MenuItem> NavigableItems()
    {
        var items = new List<MenuItem>();
        foreach (var item in MenuItems)
        {
            if (item.Visibility == Visibility.Visible && item.IsEnabled) items.Add(item);
        }
        return items;
    }

    /// <summary>Moves the keyboard focus to the first enabled item.</summary>
    public bool FocusFirstItem()
    {
        var items = NavigableItems();
        if (items.Count == 0)
        {
            Focus();
            return false;
        }
        items[0].Focus();
        return true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// See the class remarks for the keys. Handled while tunneling, so the list's scroll viewer doesn't take the arrow
    /// keys; keys from a nested submenu are left to its own list.
    /// </remarks>
    public override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled) return;
        if (!ReferenceEquals(e.Source, this) && !(e.Source is MenuItem { Parent: var panel } && panel == ItemPanel)) return;

        var items = NavigableItems();
        var current = e.Source as MenuItem;
        int index = current != null ? items.IndexOf(current) : -1;

        switch (e.Key)
        {
            case Key.Down:
                if (items.Count > 0) FocusItem(items[(index + 1) % items.Count]);
                break;
            case Key.Up:
                if (items.Count > 0) FocusItem(items[index <= 0 ? items.Count - 1 : index - 1]);
                break;
            case Key.Home:
                if (items.Count > 0) FocusItem(items[0]);
                break;
            case Key.End:
                if (items.Count > 0) FocusItem(items[^1]);
                break;
            case Key.Right when current is { HasItems: true }:
                current.OpenSubmenu(focusFirstItem: true);
                break;
            case Key.Right when _root is Menu bar:
                bar.MoveToAdjacentMenu(OwnerTopLevel(), 1);
                break;
            case Key.Left when OwnerItem is { IsTopLevel: false } owner:
                owner.CloseSubmenu();
                owner.Focus();
                break;
            case Key.Left when _root is Menu bar:
                bar.MoveToAdjacentMenu(OwnerTopLevel(), -1);
                break;
            default:
                if (!TryAccessKey(e, items, index)) return;
                break;
        }
        e.Handled = true;
    }

    // Focuses an item and scrolls a long menu to it.
    private static void FocusItem(MenuItem item)
    {
        item.Focus();
        item.BringIntoView();
    }

    private MenuItem? OwnerTopLevel()
    {
        var item = OwnerItem;
        while (item?.ParentItem != null) item = item.ParentItem;
        return item;
    }

    // A letter or digit activates the item with that access key (or first letter); with several matches it moves the
    // focus to the next one instead.
    private static bool TryAccessKey(KeyEventArgs e, List<MenuItem> items, int index)
    {
        if ((e.Modifiers & (ModifierKeys.Control | ModifierKeys.Windows)) != 0) return false;
        char? letter = KeyToChar(e.Key);
        if (letter == null) return false;

        var matches = new List<MenuItem>();
        foreach (var item in items)
        {
            if (item.AccessKey == letter) matches.Add(item);
        }
        if (matches.Count == 0) return false;

        if (matches.Count == 1)
        {
            matches[0].Focus();
            matches[0].Activate(byKeyboard: true);
            return true;
        }

        // Several: cycle through them from the focused item.
        var next = matches.Find(m => items.IndexOf(m) > index) ?? matches[0];
        next.Focus();
        return true;
    }

    internal static char? KeyToChar(Key key) => key switch
    {
        >= Key.A and <= Key.Z => (char)('A' + (key - Key.A)),
        >= Key.D0 and <= Key.D9 => (char)('0' + (key - Key.D0)),
        _ => null,
    };
}
