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
        // 1. DataContext of the initially focused element that originated the key event
        // 2. Fall back to this KeybindingHandler's own DataContext
        var initialElement = e.OriginalSource as UIElement;
        object? target = initialElement?.DataContext ?? DataContext;

        if (KeybindingManager.TryExecuteGesture(Group, e.Key, e.Modifiers, target))
        {
            e.Handled = true;
        }
    }
}
