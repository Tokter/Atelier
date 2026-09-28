using System;
using System.Collections.Generic;
using Atelier.Core.Events;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// Window-wide keyboard access to menus: Alt or F10 activate the window's main <see cref="Menu"/>, Alt+letter opens a
/// menu by its access key, and the menu key or Shift+F10 open the focused element's <see cref="ContextMenu"/>. Also
/// tracks whether access keys are underlined.
/// </summary>
/// <remarks>
/// The platform layer reports key presses that no element handled with <see cref="HandleKeyDown"/>, key releases with
/// <see cref="HandleKeyUp"/>, and pointer presses with <see cref="OnPointerPressed"/> (Alt with a click isn't a menu
/// key).
/// </remarks>
public static class MenuManager
{
    private static readonly List<WeakReference<Menu>> s_mainMenus = [];
    private static bool s_altDown;
    private static bool s_altAlone;

    /// <summary>Gets whether menu items underline their access keys: while Alt is held or a menu has the keyboard.</summary>
    public static bool AccessKeysVisible { get; private set; }

    /// <summary>Occurs when <see cref="AccessKeysVisible"/> changes.</summary>
    public static event EventHandler? AccessKeysVisibleChanged;

    internal static void SetAccessKeysVisible(bool visible)
    {
        if (AccessKeysVisible == visible) return;
        AccessKeysVisible = visible;
        AccessKeysVisibleChanged?.Invoke(null, EventArgs.Empty);
    }

    internal static void RegisterMainMenu(Menu menu) => s_mainMenus.Add(new WeakReference<Menu>(menu));

    internal static void UnregisterMainMenu(Menu menu) =>
        s_mainMenus.RemoveAll(r => !r.TryGetTarget(out var m) || m == menu);

    /// <summary>Gets the main menu shown in the window of <paramref name="root"/>, or <c>null</c>.</summary>
    public static Menu? GetMainMenu(VisualNode? root)
    {
        if (root == null) return null;
        foreach (var reference in s_mainMenus)
        {
            if (reference.TryGetTarget(out var menu) && menu.IsVisibleInTree() && RootOf(menu) == root) return menu;
        }
        return null;
    }

    private static VisualNode RootOf(VisualNode node)
    {
        while (node.Parent != null) node = node.Parent;
        return node;
    }

    private static bool IsVisibleInTree(this UIElement element)
    {
        for (VisualNode? node = element; node != null; node = node.Parent)
        {
            if (node is UIElement { Visibility: not Core.Primitives.Visibility.Visible }) return false;
        }
        return element.IsAttachedToVisualTree;
    }

    /// <summary>
    /// Handles a key press in the window of <paramref name="root"/> that no element handled: F10 toggles the main menu's
    /// keyboard mode, Alt+letter opens a main menu header by its access key, and the menu key or Shift+F10 open a
    /// context menu. Alt itself is only noted (see <see cref="HandleKeyUp"/>).
    /// </summary>
    /// <returns><c>true</c> if the key was used.</returns>
    public static bool HandleKeyDown(KeyEventArgs e, UIElement? root)
    {
        if (e.Key is Key.LeftAlt or Key.RightAlt)
        {
            if (!s_altDown && !e.IsRepeat)
            {
                s_altDown = true;
                s_altAlone = true;
                if (GetMainMenu(root) != null) SetAccessKeysVisible(true);
            }
            return false;
        }

        s_altAlone = false;
        if (e.Handled || root == null) return false;

        var modifiers = e.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows);
        if (e.Key == Key.Menu || (e.Key == Key.F10 && modifiers == ModifierKeys.Shift))
        {
            e.Handled = ContextMenuService.OpenForFocusedElement(root);
            return e.Handled;
        }

        var menu = GetMainMenu(root);
        if (menu == null) return false;

        if (e.Key == Key.F10 && modifiers == ModifierKeys.None)
        {
            menu.ToggleKeyboardMode();
            e.Handled = true;
            return true;
        }

        if (modifiers == ModifierKeys.Alt && MenuItemsPresenter.KeyToChar(e.Key) is { } letter && menu.OpenByAccessKey(letter))
        {
            e.Handled = true;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Handles a key release in the window of <paramref name="root"/>: releasing Alt without pressing another key in
    /// between toggles the main menu's keyboard mode.
    /// </summary>
    public static void HandleKeyUp(KeyEventArgs e, UIElement? root)
    {
        if (e.Key is not (Key.LeftAlt or Key.RightAlt)) return;

        bool alone = s_altDown && s_altAlone;
        s_altDown = false;
        s_altAlone = false;

        var menu = GetMainMenu(root);
        if (alone && menu != null)
        {
            menu.ToggleKeyboardMode();
        }
        else if (menu is not { IsKeyboardActive: true })
        {
            SetAccessKeysVisible(false);
        }
    }

    /// <summary>Reports a pointer press: Alt pressed with a click doesn't activate the menu when released.</summary>
    public static void OnPointerPressed() => s_altAlone = false;

    /// <summary>Forgets a held Alt key, e.g. when the window loses the focus (Alt+Tab).</summary>
    public static void Reset()
    {
        s_altDown = false;
        s_altAlone = false;
        SetAccessKeysVisible(false);
    }
}
