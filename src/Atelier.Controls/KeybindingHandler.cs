using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// A control that intercepts bubbling keyboard events in the visual tree and executes
/// matching keybindings from a specified <see cref="KeybindingManager"/> group.
/// </summary>
public class KeybindingHandler : ContentControl
{
    public static readonly BindableProperty<string> GroupProperty =
        BindableProperty.Register<KeybindingHandler, string>(
            nameof(Group),
            string.Empty);

    /// <summary>
    /// Gets or sets the keybinding group name to match against (e.g. "Global", "Detail").
    /// </summary>
    public string Group
    {
        get => GetValue(GroupProperty);
        set => SetValue(GroupProperty, value);
    }

    public KeybindingHandler()
    {
    }

    public KeybindingHandler(string group, object? content = null)
    {
        Group = group;
        Content = content;
    }

    public static KeybindingHandler Create(string group, object? content = null) => new(group, content);

    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || string.IsNullOrEmpty(Group))
            return;

        // Resolve target parameter:
        // 1. Try DataContext of the initially focused element that originated the key event
        var initialElement = e.OriginalSource as UIElement;
        object? initialTarget = initialElement?.DataContext;

        if (initialTarget != null && KeybindingManager.TryExecuteGesture(Group, e.Key, e.Modifiers, initialTarget))
        {
            e.Handled = true;
            return;
        }

        // 2. Walk up the visual ancestor chain from initialElement up to this KeybindingHandler,
        // trying each ancestor's distinct DataContext (e.g. subviews, pages, cards)
        var current = initialElement?.Parent as UIElement;
        while (current != null && current != this)
        {
            if (current.DataContext != null && !ReferenceEquals(current.DataContext, initialTarget))
            {
                if (KeybindingManager.TryExecuteGesture(Group, e.Key, e.Modifiers, current.DataContext))
                {
                    e.Handled = true;
                    return;
                }
            }
            current = current.Parent as UIElement;
        }

        // 3. Fall back to this KeybindingHandler's own DataContext
        if (DataContext != null && !ReferenceEquals(DataContext, initialTarget))
        {
            if (KeybindingManager.TryExecuteGesture(Group, e.Key, e.Modifiers, DataContext))
            {
                e.Handled = true;
                return;
            }
        }
        else if (initialTarget == null && DataContext == null)
        {
            // 4. Try null target (for parameterless or class-level [Keybinding] commands)
            if (KeybindingManager.TryExecuteGesture(Group, e.Key, e.Modifiers, null))
            {
                e.Handled = true;
            }
        }
    }
}
