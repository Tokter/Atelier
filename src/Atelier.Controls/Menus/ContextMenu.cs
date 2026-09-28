using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>Provides data for <see cref="ContextMenu.Opening"/>; set <see cref="CancelEventArgs.Cancel"/> to keep the menu closed.</summary>
/// <param name="target">The element the menu opens for.</param>
/// <param name="byKeyboard">Whether the menu key or Shift+F10 opened it (instead of a right-click).</param>
public class ContextMenuOpeningEventArgs(UIElement target, bool byKeyboard) : CancelEventArgs
{
    /// <summary>Gets the element the menu opens for.</summary>
    public UIElement Target { get; } = target;

    /// <summary>Gets whether the keyboard opened the menu.</summary>
    public bool ByKeyboard { get; } = byKeyboard;
}

/// <summary>
/// A popup menu for an element: set it with <see cref="ContextMenuService.ContextMenuProperty"/> (<c>.ContextMenu(...)</c>
/// in markup) and it opens on a right-click, the menu key or Shift+F10.
/// </summary>
/// <remarks>
/// <para>
/// Items work as in a <see cref="Menu"/>: <see cref="MenuItem"/>s and <see cref="Separator"/>s in <see cref="Items"/>,
/// or data items from <see cref="ItemsSource"/> with <see cref="ChildrenSelector"/>, <see cref="ItemSetup"/>,
/// <see cref="ItemTemplate"/> and <see cref="IsSeparatorSelector"/>.
/// </para>
/// <para>
/// A right-click opens it at the pointer; the keyboard opens it below the element with the first item focused. Unless
/// its DataContext is set, it takes the element's, so items can bind to the element's view model.
/// <see cref="Opening"/> lets you fill or change the items first, or cancel. A click outside, Escape or picking an item
/// closes it, and the focus returns to where it was.
/// </para>
/// </remarks>
public class ContextMenu : Popup, IMenuRoot
{
    /// <summary>Identifies the <see cref="ItemsSource"/> property.</summary>
    public static readonly BindableProperty<IEnumerable?> ItemsSourceProperty =
        BindableProperty.Register<ContextMenu, IEnumerable?>(nameof(ItemsSource), null, (s, o, n) => ((ContextMenu)s)._presenter.ItemsSource = n);

    /// <summary>Identifies the <see cref="KeybindingGroup"/> property.</summary>
    public static readonly BindableProperty<string?> KeybindingGroupProperty =
        BindableProperty.Register<ContextMenu, string?>(nameof(KeybindingGroup), null);

    private readonly MenuItemsPresenter _presenter = new();
    private Func<object, IEnumerable?>? _childrenSelector;
    private Action<MenuItem, object>? _itemSetup;
    private Func<object, UIElement>? _itemTemplate;
    private Func<object, bool>? _isSeparatorSelector;
    private bool _attachedToHost;
    private bool _dataContextFromTarget;

    /// <summary>Initializes an empty context menu.</summary>
    public ContextMenu()
    {
        StaysOpen = false;
        Placement = PlacementMode.Pointer;
        Child = _presenter;
        _presenter.SetRoot(this);
        Closed += (_, _) => OnMenuClosed();
    }

    /// <summary>Gets the items: menu items, separators or data items.</summary>
    public ObservableCollection<object> Items => _presenter.Items;

    /// <summary>Gets or sets the collection the items are generated from.</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>Gets or sets the keybinding group whose shortcuts the items show first; <c>null</c> (the default) prefers "Global".</summary>
    public string? KeybindingGroup { get => GetValue(KeybindingGroupProperty); set => SetValue(KeybindingGroupProperty, value); }

    /// <summary>Gets the item list.</summary>
    public MenuItemsPresenter Presenter => _presenter;

    /// <summary>Gets or sets the function that returns the submenu items of a data item.</summary>
    public Func<object, IEnumerable?>? ChildrenSelector { get => _childrenSelector; set { _childrenSelector = value; Regenerate(); } }

    /// <summary>Gets or sets the callback that sets up the menu item generated for a data item.</summary>
    public Action<MenuItem, object>? ItemSetup { get => _itemSetup; set { _itemSetup = value; Regenerate(); } }

    /// <summary>Gets or sets the factory of the header content of generated menu items.</summary>
    public Func<object, UIElement>? ItemTemplate { get => _itemTemplate; set { _itemTemplate = value; Regenerate(); } }

    /// <summary>Gets or sets the function that decides which data items are separators.</summary>
    public Func<object, bool>? IsSeparatorSelector { get => _isSeparatorSelector; set { _isSeparatorSelector = value; Regenerate(); } }

    /// <summary>Occurs before the menu opens for an element; fill or change the items, or cancel.</summary>
    public event EventHandler<ContextMenuOpeningEventArgs>? Opening;

    UIElement? IMenuRoot.MenuTarget => PlacementTarget;

    private void Regenerate()
    {
        if (_presenter.ItemsSource != null)
        {
            _presenter.SetRoot(null);
            _presenter.SetRoot(this);
        }
    }

    /// <summary>
    /// Opens the menu for <paramref name="target"/>: at the pointer, or below the element (with the first item focused)
    /// when <paramref name="byKeyboard"/>. Raises <see cref="Opening"/> first.
    /// </summary>
    /// <returns><c>true</c> if the menu opened.</returns>
    public bool Open(UIElement target, bool byKeyboard = false)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (IsOpen) IsOpen = false;

        PlacementTarget = target;
        Placement = byKeyboard ? PlacementMode.Bottom : PlacementMode.Pointer;
        if (_dataContextFromTarget || GetValueSource(DataContextProperty) == ValueSource.Default)
        {
            DataContext = target.DataContext;
            _dataContextFromTarget = true;
        }

        var opening = new ContextMenuOpeningEventArgs(target, byKeyboard);
        Opening?.Invoke(this, opening);
        if (opening.Cancel || _presenter.Items.Count == 0) return false;

        // A context menu is its own tree, shown in the window of its target; attached, its items follow their commands.
        if (Parent == null && !IsAttachedToVisualTree)
        {
            AttachToHost(target.Host);
            ApplyStylesToTree();
            _attachedToHost = true;
        }

        IsOpen = true;
        if (byKeyboard) _presenter.FocusFirstItem();
        else _presenter.Focus();
        if (byKeyboard) MenuManager.SetAccessKeysVisible(true);
        return true;
    }

    /// <inheritdoc/>
    public void CloseMenu() => IsOpen = false;

    private void OnMenuClosed()
    {
        foreach (var item in _presenter.MenuItems)
        {
            if (item.IsSubmenuOpen) item.CloseSubmenu();
        }
        MenuManager.SetAccessKeysVisible(false);
        if (_attachedToHost)
        {
            _attachedToHost = false;
            DetachFromHost();
        }
    }

    Func<object, IEnumerable?>? IMenuRoot.ChildrenSelector => _childrenSelector;

    Action<MenuItem, object>? IMenuRoot.ItemSetup => _itemSetup;

    Func<object, UIElement>? IMenuRoot.ItemTemplate => _itemTemplate;

    Func<object, bool>? IMenuRoot.IsSeparatorSelector => _isSeparatorSelector;
}

/// <summary>
/// Gives elements a <see cref="ContextMenu"/> (the ContextMenu attached property) and opens it on a right-click, the
/// menu key or Shift+F10. The platform layer reports that input through <see cref="OnPointerReleased"/> and
/// <see cref="OpenForFocusedElement"/>.
/// </summary>
public static class ContextMenuService
{
    /// <summary>Identifies the ContextMenu attached property: the menu an element shows on a right-click.</summary>
    public static readonly BindableProperty<ContextMenu?> ContextMenuProperty =
        BindableProperty.RegisterAttached<ContextMenuOwner, UIElement, ContextMenu?>("ContextMenu", null);

    // Attached properties need an owner type for their registry key; this class is static.
    private sealed class ContextMenuOwner;

    /// <summary>Gets the context menu of <paramref name="element"/>.</summary>
    public static ContextMenu? GetContextMenu(UIElement element) => element.GetValue(ContextMenuProperty);

    /// <summary>Sets the context menu of <paramref name="element"/>; <c>null</c> removes it.</summary>
    public static void SetContextMenu(UIElement element, ContextMenu? menu) => element.SetValue(ContextMenuProperty, menu);

    /// <summary>
    /// Opens the context menu of <paramref name="element"/> or its nearest ancestor that has one (and is enabled).
    /// </summary>
    /// <returns><c>true</c> if a menu opened.</returns>
    public static bool TryOpen(UIElement? element, bool byKeyboard)
    {
        for (var node = element; node != null; node = node.Parent as UIElement)
        {
            if (GetContextMenu(node) is { } menu)
            {
                return node.IsEnabled && menu.Open(node, byKeyboard);
            }
        }
        return false;
    }

    /// <summary>
    /// Reports a released pointer button over <paramref name="element"/>: a right-click that no element handled opens the
    /// context menu. Called by the platform layer.
    /// </summary>
    /// <returns><c>true</c> if a menu opened.</returns>
    public static bool OnPointerReleased(UIElement? element, Core.Events.PointerButtons button, bool handled) =>
        button == Core.Events.PointerButtons.Right && !handled && TryOpen(element, byKeyboard: false);

    /// <summary>
    /// Opens the context menu of the focused element of <paramref name="root"/>'s window (or of the root), as the menu
    /// key and Shift+F10 do. Called by the platform layer.
    /// </summary>
    /// <returns><c>true</c> if a menu opened.</returns>
    public static bool OpenForFocusedElement(UIElement root) =>
        TryOpen(FocusManager.GetFocusedElement(root) ?? root, byKeyboard: true);
}
