using System;
using System.Collections;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// The root of a menu tree, a <see cref="Menu"/> bar or a <see cref="ContextMenu"/>: holds the settings its
/// <see cref="MenuItem"/>s (at every level) are generated with, and closes the whole menu.
/// </summary>
public interface IMenuRoot
{
    /// <summary>Gets the function that returns the child items of a data item (its submenu), or <c>null</c>.</summary>
    Func<object, IEnumerable?>? ChildrenSelector { get; }

    /// <summary>
    /// Gets the callback that sets up the menu item generated for a data item (header, icon, command, check state...),
    /// or <c>null</c>.
    /// </summary>
    Action<MenuItem, object>? ItemSetup { get; }

    /// <summary>Gets the factory of the header content of generated menu items, or <c>null</c>.</summary>
    Func<object, UIElement>? ItemTemplate { get; }

    /// <summary>Gets the function that decides which data items are separators, or <c>null</c>.</summary>
    Func<object, bool>? IsSeparatorSelector { get; }

    /// <summary>Gets the keybinding group whose shortcuts the items show first, or <c>null</c>.</summary>
    string? KeybindingGroup { get; }

    /// <summary>Gets the element the menu belongs to, whose DataContext the items' commands may come from.</summary>
    UIElement? MenuTarget { get; }

    /// <summary>Closes all open menus of this root, e.g. after an item was clicked.</summary>
    void CloseMenu();
}

/// <summary>
/// Put <see cref="Instance"/> into a bound menu collection to get a separator line between items.
/// </summary>
public sealed class MenuSeparator
{
    /// <summary>The separator item.</summary>
    public static readonly MenuSeparator Instance = new();

    private MenuSeparator()
    {
    }
}

/// <summary>A horizontal line that separates groups of items, such as in a menu (MD3 divider).</summary>
/// <remarks>1 px high with 8 px above and below; themes draw the line (MD3: outline-variant).</remarks>
public class Separator : Control
{
    /// <summary>Initializes a separator.</summary>
    public Separator()
    {
        Margin = new Thickness(0, 8);
        Height = 1;
        HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize) => new(0, 1);
}

/// <summary>Parses headers with access keys, such as <c>"_File"</c> (an underscore before the letter; <c>"__"</c> is an underscore).</summary>
public static class AccessText
{
    /// <summary>Removes the access key marker from <paramref name="text"/>.</summary>
    /// <param name="text">The header, e.g. <c>"Save _As..."</c>.</param>
    /// <param name="accessIndex">The index of the access key in the returned text, or -1.</param>
    /// <returns>The display text, e.g. <c>"Save As..."</c>.</returns>
    public static string Parse(string text, out int accessIndex)
    {
        accessIndex = -1;
        if (text.IndexOf('_') < 0) return text;

        var result = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '_' && i + 1 < text.Length)
            {
                if (text[i + 1] == '_')
                {
                    result.Append('_');
                    i++;
                    continue;
                }
                if (accessIndex < 0) accessIndex = result.Length;
                continue;
            }
            result.Append(text[i]);
        }
        return result.ToString();
    }
}

internal static class MenuItemFactory
{
    // The container for an item of a menu level: menu items and separators as they are, other elements as they are,
    // separators for MenuSeparator (or the root's selector), and a generated menu item for data items.
    public static UIElement CreateContainer(IMenuRoot? root, MenuItem? parent, object item)
    {
        switch (item)
        {
            case MenuItem menuItem:
                menuItem.AttachToMenu(root, parent, null);
                return menuItem;
            case UIElement element:
                return element;
            case MenuSeparator:
                return new Separator();
        }

        if (root?.IsSeparatorSelector?.Invoke(item) == true)
        {
            return new Separator();
        }

        var generated = new MenuItem { DataContext = item, Header = item };
        generated.AttachToMenu(root, parent, item);
        if (root?.ChildrenSelector?.Invoke(item) is { } children)
        {
            generated.ItemsSource = children;
        }
        root?.ItemSetup?.Invoke(generated, item);
        return generated;
    }
}
