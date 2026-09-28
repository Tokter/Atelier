using System;
using System.Collections;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Markup;

/// <summary>Fluent methods for <see cref="Menu"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class MenuMarkup
{
    /// <summary>Sets the keybinding group whose shortcuts the items show first.</summary>
    public static T KeybindingGroup<T>(this T menu, string? group) where T : Menu => menu.Set(Menu.KeybindingGroupProperty, group);

    /// <summary>Sets whether this is the window's main menu, reached with Alt, F10 and Alt+letter (the default).</summary>
    public static T IsMainMenu<T>(this T menu, bool isMainMenu = true) where T : Menu => menu.Set(Menu.IsMainMenuProperty, isMainMenu);

    /// <summary>Sets the function that returns the submenu items of each data item.</summary>
    public static T WithChildrenSelector<T, TItem>(this T menu, Func<TItem, IEnumerable?> selector) where T : Menu
    {
        menu.ChildrenSelector = MenuMarkupHelpers.Typed(selector);
        return menu;
    }

    /// <summary>Sets the callback that sets up the menu item generated for each data item (header, icon, command...).</summary>
    public static T WithItemSetup<T, TItem>(this T menu, Action<MenuItem, TItem> setup) where T : Menu
    {
        menu.ItemSetup = MenuMarkupHelpers.Typed(setup);
        return menu;
    }

    /// <summary>Sets the function that decides which data items are separators.</summary>
    public static T WithIsSeparatorSelector<T, TItem>(this T menu, Func<TItem, bool> selector) where T : Menu
    {
        menu.IsSeparatorSelector = MenuMarkupHelpers.Typed(selector);
        return menu;
    }
}

/// <summary>Fluent methods for <see cref="MenuItem"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class MenuItemMarkup
{
    /// <summary>Sets the header; an underscore marks the access key, e.g. <c>"_Open"</c>.</summary>
    public static T Header<T>(this T item, object? header) where T : MenuItem => item.Set(MenuItem.HeaderProperty, header);

    /// <summary>
    /// Sets the icon before the header: a <see cref="MaterialIconKind"/>, an icon name or SVG (see <see cref="IconSource"/>), or
    /// an element. Without one, the item shows its command's icon.
    /// </summary>
    public static T Icon<T>(this T item, object? icon) where T : MenuItem => item.Set(MenuItem.IconProperty, icon);

    /// <summary>Sets the shortcut text shown on the right, instead of the command's keybinding.</summary>
    public static T InputGestureText<T>(this T item, string? text) where T : MenuItem => item.Set(MenuItem.InputGestureTextProperty, text);

    /// <summary>Makes a click toggle the check mark.</summary>
    public static T IsCheckable<T>(this T item, bool isCheckable = true) where T : MenuItem => item.Set(MenuItem.IsCheckableProperty, isCheckable);

    /// <summary>Checks (or unchecks) the item.</summary>
    public static T IsChecked<T>(this T item, bool isChecked = true) where T : MenuItem => item.Set(MenuItem.IsCheckedProperty, isChecked);

    /// <summary>Puts a checkable item in a radio group: checking it unchecks the others of the group in the same menu.</summary>
    public static T GroupName<T>(this T item, string? groupName) where T : MenuItem => item.Set(MenuItem.GroupNameProperty, groupName);

    /// <summary>Keeps the menu open after a click, e.g. for check items.</summary>
    public static T StaysOpenOnClick<T>(this T item, bool staysOpen = true) where T : MenuItem => item.Set(MenuItem.StaysOpenOnClickProperty, staysOpen);

    /// <summary>Hides the item (instead of graying it out) while its command can't execute.</summary>
    public static T HideWhenDisabled<T>(this T item, bool hide = true) where T : MenuItem => item.Set(MenuItem.HideWhenDisabledProperty, hide);

    /// <summary>Sets the collection the submenu's items are generated from.</summary>
    public static T ItemsSource<T>(this T item, IEnumerable? source) where T : MenuItem => item.Set(MenuItem.ItemsSourceProperty, source);

    /// <summary>Opens (or closes) the submenu.</summary>
    public static T IsSubmenuOpen<T>(this T item, bool isOpen = true) where T : MenuItem => item.Set(MenuItem.IsSubmenuOpenProperty, isOpen);

    /// <summary>Adds submenu items: menu items, separators or data items.</summary>
    public static T Items<T>(this T item, params object[] items) where T : MenuItem
    {
        foreach (var child in items) item.Items.Add(child);
        return item;
    }

    /// <summary>Handles <see cref="MenuItem.CheckedChanged"/>, raised with the new check state.</summary>
    public static T OnCheckedChanged<T>(this T item, Action<bool> action) where T : MenuItem
    {
        item.CheckedChanged += MarkupExtensions.ToHandler(action);
        return item;
    }

    /// <summary>Binds the check state to <paramref name="source"/>. With a <paramref name="setter"/>, clicking writes it back.</summary>
    public static T BindIsChecked<T, TSource>(this T item, TSource source, Func<TSource, bool> getter, Action<TSource, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : MenuItem where TSource : class =>
        item.BindToSource(MenuItem.IsCheckedProperty, source, getter, setter, updateSourceTrigger, getterExpression);
}

/// <summary>Fluent methods for <see cref="ContextMenu"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class ContextMenuMarkup
{
    /// <summary>Adds items: menu items, separators or data items.</summary>
    public static T Items<T>(this T menu, params object[] items) where T : ContextMenu
    {
        foreach (var item in items) menu.Items.Add(item);
        return menu;
    }

    /// <summary>Sets the collection the items are generated from.</summary>
    public static T ItemsSource<T>(this T menu, IEnumerable? source) where T : ContextMenu => menu.Set(ContextMenu.ItemsSourceProperty, source);

    /// <summary>Sets the keybinding group whose shortcuts the items show first.</summary>
    public static T KeybindingGroup<T>(this T menu, string? group) where T : ContextMenu => menu.Set(ContextMenu.KeybindingGroupProperty, group);

    /// <summary>Sets the function that returns the submenu items of each data item.</summary>
    public static T WithChildrenSelector<T, TItem>(this T menu, Func<TItem, IEnumerable?> selector) where T : ContextMenu
    {
        menu.ChildrenSelector = MenuMarkupHelpers.Typed(selector);
        return menu;
    }

    /// <summary>Sets the callback that sets up the menu item generated for each data item.</summary>
    public static T WithItemSetup<T, TItem>(this T menu, Action<MenuItem, TItem> setup) where T : ContextMenu
    {
        menu.ItemSetup = MenuMarkupHelpers.Typed(setup);
        return menu;
    }

    /// <summary>Sets the factory of the header content of generated menu items.</summary>
    public static T WithItemTemplate<T, TItem>(this T menu, Func<TItem, UIElement> template) where T : ContextMenu
    {
        menu.ItemTemplate = ItemsMarkupHelpers.TypedTemplate(template);
        return menu;
    }

    /// <summary>Sets the function that decides which data items are separators.</summary>
    public static T WithIsSeparatorSelector<T, TItem>(this T menu, Func<TItem, bool> selector) where T : ContextMenu
    {
        menu.IsSeparatorSelector = MenuMarkupHelpers.Typed(selector);
        return menu;
    }

    /// <summary>Handles <see cref="ContextMenu.Opening"/>, raised before the menu opens; fill the items or cancel.</summary>
    public static T OnOpening<T>(this T menu, EventHandler<ContextMenuOpeningEventArgs> handler) where T : ContextMenu
    {
        menu.Opening += handler;
        return menu;
    }
}

/// <summary>Fluent methods for giving any element a <see cref="Controls.ContextMenu"/>.</summary>
public static class ContextMenuServiceMarkup
{
    /// <summary>Sets the menu the element shows on a right-click, the menu key or Shift+F10.</summary>
    public static T ContextMenu<T>(this T element, ContextMenu? menu) where T : UIElement => element.Set(ContextMenuService.ContextMenuProperty, menu);
}

/// <summary>Fluent methods for hosting a menu bar in a <see cref="TitleBar"/>.</summary>
public static class TitleBarMenuMarkup
{
    /// <summary>Shows a menu bar after the title bar's icon, before the title.</summary>
    public static T Menu<T>(this T titleBar, Menu? menu) where T : TitleBar => titleBar.Set(TitleBar.MenuProperty, menu);
}

internal static class MenuMarkupHelpers
{
    public static Func<object, IEnumerable?> Typed<TItem>(Func<TItem, IEnumerable?> selector) =>
        item => item is TItem typed ? selector(typed) : null;

    public static Action<MenuItem, object> Typed<TItem>(Action<MenuItem, TItem> setup) =>
        (menuItem, item) =>
        {
            if (item is TItem typed) setup(menuItem, typed);
        };

    public static Func<object, bool> Typed<TItem>(Func<TItem, bool> selector) =>
        item => item is TItem typed && selector(typed);
}
