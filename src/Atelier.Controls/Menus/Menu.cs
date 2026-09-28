using System;
using System.Collections;
using System.Collections.Generic;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// A menu bar: a row of headers (such as File, Edit, View) that open menus of <see cref="MenuItem"/>s.
/// </summary>
/// <remarks>
/// <para>
/// Items are <see cref="MenuItem"/>s and <see cref="Separator"/>s in <see cref="ItemsControl.Items"/>, or data items
/// from <see cref="ItemsControl.ItemsSource"/>: <see cref="ChildrenSelector"/> gives each item its submenu items,
/// <see cref="ItemSetup"/> sets up the generated menu items (header, icon, command...), <see cref="ItemsControl.ItemTemplate"/>
/// builds custom header content, and <see cref="MenuSeparator.Instance"/> (or <see cref="IsSeparatorSelector"/>) marks
/// separators.
/// </para>
/// <para>
/// A click opens a header's menu; while one is open, hovering another header opens that one. From the keyboard, Alt or
/// F10 (for the window's main menu, see <see cref="IsMainMenu"/>) moves the focus to the first header; Alt+letter opens
/// the header with that access key. In the bar, Left/Right move between headers and Down, Enter or Space open the menu;
/// in a menu, Left/Right at the ends move to the neighboring menu. Escape closes one level at a time.
/// </para>
/// <para>A menu bar can be hosted in a custom window's <see cref="TitleBar"/> with <see cref="TitleBar.Menu"/>.</para>
/// </remarks>
public class Menu : ItemsControl, IMenuRoot
{
    /// <summary>Identifies the <see cref="KeybindingGroup"/> property.</summary>
    public static readonly BindableProperty<string?> KeybindingGroupProperty =
        BindableProperty.Register<Menu, string?>(nameof(KeybindingGroup), null);

    /// <summary>Identifies the <see cref="IsMainMenu"/> property.</summary>
    public static readonly BindableProperty<bool> IsMainMenuProperty =
        BindableProperty.Register<Menu, bool>(nameof(IsMainMenu), true, (s, o, n) => ((Menu)s).UpdateRegistration());

    private Func<object, IEnumerable?>? _childrenSelector;
    private Action<MenuItem, object>? _itemSetup;
    private Func<object, bool>? _isSeparatorSelector;
    private UIElement? _focusBeforeMenu;
    private bool _isRegistered;

    /// <summary>Initializes an empty menu bar.</summary>
    public Menu()
    {
        ItemPanel.Orientation = Orientation.Horizontal;
        ItemPanel.Spacing = 2;
        ScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        ScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>Gets or sets the keybinding group whose shortcuts the items show first; <c>null</c> (the default) prefers "Global".</summary>
    public string? KeybindingGroup { get => GetValue(KeybindingGroupProperty); set => SetValue(KeybindingGroupProperty, value); }

    /// <summary>
    /// Gets or sets whether this is its window's main menu, which Alt, F10 and Alt+letter reach from anywhere in the
    /// window. The default is <c>true</c>.
    /// </summary>
    public bool IsMainMenu { get => GetValue(IsMainMenuProperty); set => SetValue(IsMainMenuProperty, value); }

    /// <summary>Gets or sets the function that returns the submenu items of a data item.</summary>
    public Func<object, IEnumerable?>? ChildrenSelector
    {
        get => _childrenSelector;
        set
        {
            _childrenSelector = value;
            RefreshContainers();
        }
    }

    /// <summary>
    /// Gets or sets the callback that sets up the menu item generated for a data item, e.g. its header, icon, command and
    /// check state (bindings to the data item work too).
    /// </summary>
    public Action<MenuItem, object>? ItemSetup
    {
        get => _itemSetup;
        set
        {
            _itemSetup = value;
            RefreshContainers();
        }
    }

    /// <summary>Gets or sets the function that decides which data items are separators.</summary>
    public Func<object, bool>? IsSeparatorSelector
    {
        get => _isSeparatorSelector;
        set
        {
            _isSeparatorSelector = value;
            RefreshContainers();
        }
    }

    /// <summary>Gets the header whose menu is open, or <c>null</c>.</summary>
    public MenuItem? OpenItem { get; private set; }

    /// <summary>Gets whether the menu bar has the keyboard (after Alt or F10), with its headers focusable.</summary>
    public bool IsKeyboardActive { get; private set; }

    /// <summary>Gets the top-level menu items.</summary>
    public IEnumerable<MenuItem> TopLevelItems
    {
        get
        {
            for (int i = 0; i < ContainerCount; i++)
            {
                if (ContainerFromIndex(i) is MenuItem item) yield return item;
            }
        }
    }

    UIElement? IMenuRoot.MenuTarget => this;

    Func<object, UIElement>? IMenuRoot.ItemTemplate => ItemTemplate;

    /// <inheritdoc/>
    protected override UIElement CreateContainerForItem(object item)
    {
        var container = MenuItemFactory.CreateContainer(this, null, item);
        container.IsFocusable = IsKeyboardActive;
        return container;
    }

    /// <summary>Closes the open menu (and its submenus), and leaves the keyboard mode.</summary>
    public void CloseMenu()
    {
        OpenItem?.CloseSubmenu();
        ExitKeyboardMode(restoreFocus: true);
    }

    #region Opening and closing

    internal void OnTopLevelOpened(MenuItem item)
    {
        if (OpenItem == null && !IsKeyboardActive)
        {
            var focused = FocusManager.GetFocusedElement(this);
            _focusBeforeMenu = focused != null && !focused.IsDescendantOf(this) ? focused : null;
        }
        OpenItem = item;
    }

    internal void OnTopLevelClosed(MenuItem item, bool returnFocus)
    {
        if (OpenItem == item) OpenItem = null;
        if (!returnFocus || OpenItem != null) return;

        if (IsKeyboardActive)
        {
            item.Focus(); // Escape from a menu goes back to its header
        }
        else
        {
            RestoreFocus();
        }
    }

    private void RestoreFocus()
    {
        var previous = _focusBeforeMenu;
        _focusBeforeMenu = null;
        if (previous is { IsAttachedToVisualTree: true }) previous.Focus();
    }

    internal void MoveToAdjacentMenu(MenuItem? current, int direction)
    {
        var items = new List<MenuItem>();
        foreach (var item in TopLevelItems)
        {
            if (item.Visibility == Visibility.Visible && item.IsEnabled) items.Add(item);
        }
        if (items.Count == 0) return;

        int index = current != null ? items.IndexOf(current) : -1;
        var next = items[((index + direction) % items.Count + items.Count) % items.Count];
        bool wasOpen = OpenItem != null;

        if (!IsKeyboardActive) EnterKeyboardMode(next);
        if (wasOpen && next.HasItems)
        {
            next.OpenSubmenu(focusFirstItem: true);
        }
        else
        {
            OpenItem?.CloseSubmenu();
            next.Focus();
        }
    }

    #endregion

    #region Keyboard mode

    /// <summary>
    /// Gives the menu bar the keyboard: the headers become focusable and <paramref name="item"/> (or the first header) gets
    /// the focus; access keys are shown. Escape, a click elsewhere or moving the focus away ends it.
    /// </summary>
    public void EnterKeyboardMode(MenuItem? item = null)
    {
        if (!IsKeyboardActive)
        {
            var focused = FocusManager.GetFocusedElement(this);
            if (OpenItem == null) _focusBeforeMenu = focused != null && !focused.IsDescendantOf(this) ? focused : null;
            IsKeyboardActive = true;
            foreach (var header in TopLevelItems) header.IsFocusable = true;
            FocusManager.FocusChanged += OnFocusChanged;
            MenuManager.SetAccessKeysVisible(true);
        }

        FocusManager.NotifyKeyboardInteraction();
        (item ?? FirstHeader())?.Focus();
    }

    /// <summary>Ends the keyboard mode; with <paramref name="restoreFocus"/> the focus returns to where it was before.</summary>
    public void ExitKeyboardMode(bool restoreFocus)
    {
        MenuManager.SetAccessKeysVisible(false);
        if (!IsKeyboardActive)
        {
            if (restoreFocus && OpenItem == null) RestoreFocus();
            return;
        }

        IsKeyboardActive = false;
        FocusManager.FocusChanged -= OnFocusChanged;
        foreach (var header in TopLevelItems) header.IsFocusable = false;
        if (restoreFocus) RestoreFocus();
        else _focusBeforeMenu = null;
    }

    /// <summary>Toggles the keyboard mode, as the Alt key and F10 do.</summary>
    public void ToggleKeyboardMode()
    {
        if (IsKeyboardActive)
        {
            OpenItem?.CloseSubmenu();
            ExitKeyboardMode(restoreFocus: true);
        }
        else
        {
            EnterKeyboardMode();
        }
    }

    /// <summary>Opens the menu of the header with the access key <paramref name="letter"/>, as Alt+letter does.</summary>
    /// <returns><c>true</c> if a header has that access key.</returns>
    public bool OpenByAccessKey(char letter)
    {
        letter = char.ToUpperInvariant(letter);
        foreach (var item in TopLevelItems)
        {
            if (item.Visibility == Visibility.Visible && item.IsEnabled && item.AccessKey == letter)
            {
                EnterKeyboardMode(item);
                item.Activate(byKeyboard: true);
                return true;
            }
        }
        return false;
    }

    private MenuItem? FirstHeader()
    {
        foreach (var item in TopLevelItems)
        {
            if (item.Visibility == Visibility.Visible && item.IsEnabled) return item;
        }
        return null;
    }

    private void OnFocusChanged(UIElement? oldFocus, UIElement? newFocus)
    {
        if (newFocus != null && newFocus != this && !newFocus.IsDescendantOf(this))
        {
            OpenItem?.CloseSubmenu();
            ExitKeyboardMode(restoreFocus: false);
        }
    }

    /// <inheritdoc/>
    /// <remarks>On a header: Left/Right move between headers, Down/Up open the menu, a letter opens the header with that access key, Escape leaves the keyboard mode.</remarks>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Source is not MenuItem { IsTopLevel: true } header || header.Root != this) return;

        switch (e.Key)
        {
            case Key.Left:
                MoveToAdjacentMenu(header, -1);
                break;
            case Key.Right:
                MoveToAdjacentMenu(header, 1);
                break;
            case Key.Down or Key.Up:
                header.Activate(byKeyboard: true);
                break;
            case Key.Escape:
                ExitKeyboardMode(restoreFocus: true);
                break;
            default:
                if (MenuItemsPresenter.KeyToChar(e.Key) is not { } letter || (e.Modifiers & ModifierKeys.Control) != 0 || !OpenByAccessKey(letter)) return;
                break;
        }
        e.Handled = true;
    }

    #endregion

    #region Registration

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        UpdateRegistration();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree()
    {
        OpenItem?.CloseSubmenu();
        ExitKeyboardMode(restoreFocus: false);
        UpdateRegistration(forceOff: true);
        base.OnDetachedFromVisualTree();
    }

    private void UpdateRegistration(bool forceOff = false)
    {
        bool register = !forceOff && IsMainMenu && IsAttachedToVisualTree;
        if (register == _isRegistered) return;
        _isRegistered = register;
        if (register) MenuManager.RegisterMainMenu(this);
        else MenuManager.UnregisterMainMenu(this);
    }

    #endregion
}
