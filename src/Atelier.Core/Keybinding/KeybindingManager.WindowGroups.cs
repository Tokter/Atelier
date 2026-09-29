using System;
using System.Collections.Generic;
using Atelier.Core.Events;

namespace Atelier.Core.Keybinding;

public static partial class KeybindingManager
{
    private static readonly List<string> s_windowGroups = [];

    /// <summary>
    /// Gets the window groups: keybinding groups whose shortcuts work anywhere in a window, with or without a
    /// <c>KeybindingHandler</c> around the focus, such as the developer tools' F12. Windows run them for keys that
    /// nothing else handled (see <see cref="TryExecuteWindowKeybinding"/>).
    /// </summary>
    public static IReadOnlyList<string> WindowGroups => s_windowGroups;

    /// <summary>Makes <paramref name="group"/> a window group (see <see cref="WindowGroups"/>); does nothing if it already is one.</summary>
    public static void AddWindowGroup(string group)
    {
        ArgumentException.ThrowIfNullOrEmpty(group);
        if (!s_windowGroups.Contains(group))
        {
            s_windowGroups.Add(group);
            OnKeybindingsChanged();
        }
    }

    /// <summary>Stops <paramref name="group"/> being a window group.</summary>
    /// <returns><c>true</c> if it was one.</returns>
    public static bool RemoveWindowGroup(string group)
    {
        if (!s_windowGroups.Remove(group)) return false;
        OnKeybindingsChanged();
        return true;
    }

    /// <summary>
    /// Runs the command of a window group (see <see cref="WindowGroups"/>) whose shortcut is <paramref name="e"/>'s key,
    /// for a key that nothing in the window handled. Window groups' commands run without a target, so they are command
    /// classes or commands that find what they act on themselves (such as the active window).
    /// </summary>
    /// <returns><c>true</c> if a command ran; <paramref name="e"/> is then marked handled.</returns>
    public static bool TryExecuteWindowKeybinding(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Handled) return false;
        foreach (string group in s_windowGroups)
        {
            if (TryExecuteGesture(group, e.Key, e.Modifiers, null))
            {
                e.Handled = true;
                return true;
            }
        }
        return false;
    }
}
