using System;
using System.Collections.Generic;
using System.Linq;
using Atelier.Core.Events;

namespace Atelier.Core.Keybinding;

public static partial class KeybindingManager
{
    public static Dictionary<string, Dictionary<string, IKeybindingDescriptor>> RegisteredKeybindings { get; } = new Dictionary<string, Dictionary<string, IKeybindingDescriptor>>();
    private static readonly Dictionary<string, KeybindingGesture?> _gestureCache = new(StringComparer.OrdinalIgnoreCase);

    public static void RegisterKeybinding(IKeybindingDescriptor keybindingDescriptor)
    {
        if (keybindingDescriptor == null)
            throw new ArgumentNullException(nameof(keybindingDescriptor));

        if (RegisteredKeybindings.ContainsKey(keybindingDescriptor.Group) && RegisteredKeybindings[keybindingDescriptor.Group].ContainsKey(keybindingDescriptor.Name))
            throw new InvalidOperationException($"A keybinding with the name '{keybindingDescriptor.Name}' is already registered.");

        if (!RegisteredKeybindings.ContainsKey(keybindingDescriptor.Group))
        {
            RegisteredKeybindings[keybindingDescriptor.Group] = new Dictionary<string, IKeybindingDescriptor>();
        }

        RegisteredKeybindings[keybindingDescriptor.Group][keybindingDescriptor.Name] = keybindingDescriptor;
        WarmGestureCache(keybindingDescriptor.Keybinding);
    }

    /// <summary>
    /// Registers or updates a keybinding descriptor in <see cref="RegisteredKeybindings"/>.
    /// If a keybinding with the same group and name already exists, it is overwritten.
    /// </summary>
    public static void RegisterOrUpdateKeybinding(IKeybindingDescriptor keybindingDescriptor)
    {
        if (keybindingDescriptor == null)
            throw new ArgumentNullException(nameof(keybindingDescriptor));

        if (!RegisteredKeybindings.TryGetValue(keybindingDescriptor.Group, out var groupKeybindings))
        {
            groupKeybindings = new Dictionary<string, IKeybindingDescriptor>();
            RegisteredKeybindings[keybindingDescriptor.Group] = groupKeybindings;
        }

        groupKeybindings[keybindingDescriptor.Name] = keybindingDescriptor;
        WarmGestureCache(keybindingDescriptor.Keybinding);
    }

    public static void Clear()
    {
        RegisteredKeybindings.Clear();
        _gestureCache.Clear();
    }

    private static void WarmGestureCache(string? keybindingStr)
    {
        if (!string.IsNullOrWhiteSpace(keybindingStr))
        {
            GetParsedGesture(keybindingStr);
        }
    }

    private static KeybindingGesture? GetParsedGesture(string keybindingStr)
    {
        if (string.IsNullOrWhiteSpace(keybindingStr))
            return null;

        if (_gestureCache.TryGetValue(keybindingStr, out var cached))
            return cached;

        if (KeybindingGesture.TryParse(keybindingStr, out var gesture))
        {
            _gestureCache[keybindingStr] = gesture;
            var normalized = gesture.ToString();
            if (!string.Equals(keybindingStr, normalized, StringComparison.OrdinalIgnoreCase))
            {
                _gestureCache[normalized] = gesture;
            }
            return gesture;
        }

        _gestureCache[keybindingStr] = null;
        return null;
    }

    /// <summary>
    /// Finds a registered keybinding descriptor in the specified group matching the key and modifiers,
    /// regardless of the order in which modifiers were defined in the keybinding.
    /// </summary>
    public static IKeybindingDescriptor? FindKeybinding(string group, Key key, ModifierKeys modifiers)
    {
        if (string.IsNullOrEmpty(group))
            return null;

        if (RegisteredKeybindings.TryGetValue(group, out var groupKeybindings))
        {
            foreach (var descriptor in groupKeybindings.Values)
            {
                var gesture = GetParsedGesture(descriptor.Keybinding);
                if (gesture.HasValue && gesture.Value.Matches(key, modifiers))
                {
                    return descriptor;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a registered keybinding descriptor in the specified group matching the given gesture string,
    /// regardless of the order in which modifiers are specified (e.g. "Ctrl+Shift+L" matches "Shift+Ctrl+L").
    /// </summary>
    public static IKeybindingDescriptor? FindKeybinding(string group, string gestureString)
    {
        if (string.IsNullOrEmpty(group) || string.IsNullOrWhiteSpace(gestureString))
            return null;

        if (KeybindingGesture.TryParse(gestureString, out var gesture))
        {
            return FindKeybinding(group, gesture.Key, gesture.Modifiers);
        }

        return null;
    }

    /// <summary>
    /// Finds a registered keybinding descriptor in any group matching the key and modifiers.
    /// </summary>
    public static IKeybindingDescriptor? FindKeybinding(Key key, ModifierKeys modifiers)
    {
        foreach (var group in RegisteredKeybindings.Keys)
        {
            var descriptor = FindKeybinding(group, key, modifiers);
            if (descriptor != null)
                return descriptor;
        }

        return null;
    }

    /// <summary>
    /// Finds a registered keybinding descriptor in any group matching the given gesture string,
    /// regardless of modifier ordering (e.g. "Ctrl+Shift+L" matches "Shift+Ctrl+L").
    /// </summary>
    public static IKeybindingDescriptor? FindKeybinding(string gestureString)
    {
        if (string.IsNullOrWhiteSpace(gestureString))
            return null;

        if (KeybindingGesture.TryParse(gestureString, out var gesture))
        {
            return FindKeybinding(gesture.Key, gesture.Modifiers);
        }

        return null;
    }

    /// <summary>
    /// Attempts to find and execute a registered keybinding in the specified group matching the key and modifiers.
    /// Returns true if a keybinding was found and successfully executed on the target.
    /// </summary>
    public static bool TryExecuteGesture(string group, Key key, ModifierKeys modifiers, object? target = null)
    {
        var descriptor = FindKeybinding(group, key, modifiers);
        if (descriptor != null && descriptor.Command.CanExecute(target))
        {
            descriptor.Command.Execute(target);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to find and execute a registered keybinding in the specified group matching the key event args.
    /// Returns true if a keybinding was found and successfully executed on the target.
    /// </summary>
    public static bool TryExecuteGesture(string group, KeyEventArgs e, object? target = null)
    {
        if (e == null) return false;
        return TryExecuteGesture(group, e.Key, e.Modifiers, target);
    }

    /// <summary>
    /// Attempts to find and execute a registered keybinding matching the gesture string in the specified group,
    /// regardless of modifier order (e.g. "Ctrl+Shift+L" matches "Shift+Ctrl+L").
    /// </summary>
    public static bool TryExecuteGesture(string group, string gestureString, object? target = null)
    {
        if (string.IsNullOrEmpty(group) || string.IsNullOrWhiteSpace(gestureString))
            return false;

        if (KeybindingGesture.TryParse(gestureString, out var gesture))
        {
            return TryExecuteGesture(group, gesture.Key, gesture.Modifiers, target);
        }

        return false;
    }

    /// <summary>
    /// Attempts to find and execute a registered keybinding matching the gesture string across any group,
    /// regardless of modifier order.
    /// </summary>
    public static bool TryExecuteGesture(string gestureString, object? target = null)
    {
        if (string.IsNullOrWhiteSpace(gestureString))
            return false;

        if (KeybindingGesture.TryParse(gestureString, out var gesture))
        {
            foreach (var group in RegisteredKeybindings.Keys)
            {
                if (TryExecuteGesture(group, gesture.Key, gesture.Modifiers, target))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether two gesture strings represent the same keyboard shortcut,
    /// regardless of modifier order, casing, or spacing (e.g. "Ctrl+Shift+L" matches "Shift+Ctrl+L").
    /// </summary>
    public static bool MatchesGesture(string? gestureA, string? gestureB)
    {
        return KeybindingGesture.Matches(gestureA, gestureB);
    }

    /// <summary>
    /// Determines whether two gesture strings represent the same keyboard shortcut,
    /// regardless of modifier order, casing, or spacing (e.g. "Ctrl+Shift+L" matches "Shift+Ctrl+L").
    /// </summary>
    public static bool GesturesMatch(string? gestureA, string? gestureB) => MatchesGesture(gestureA, gestureB);

    /// <summary>
    /// Normalizes a gesture string to canonical form with modifiers in standard order (e.g. "Shift+Ctrl+L" -> "Ctrl+Shift+L").
    /// </summary>
    public static string NormalizeGesture(string? gestureString)
    {
        return KeybindingGesture.Normalize(gestureString);
    }

    public static bool CanExecuteKeybinding(string group, string name, object? target = null)
    {
        if (RegisteredKeybindings.TryGetValue(group, out var groupKeybindings) && groupKeybindings.TryGetValue(name, out var keybindingDescriptor))
        {
            return keybindingDescriptor.Command.CanExecute(target);
        }
        return false;
    }

    public static bool CanExecuteKeybinding(string key, object? target = null)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        int separatorIndex = key.IndexOf('_');
        if (separatorIndex <= 0 || separatorIndex >= key.Length - 1)
            throw new ArgumentException($"Invalid keybinding format '{key}'. Expected 'Group_Name'.", nameof(key));

        string group = key.Substring(0, separatorIndex);
        string name = key.Substring(separatorIndex + 1);
        return CanExecuteKeybinding(group, name, target);
    }

    public static void ExecuteKeybinding(string group, string name, object? target = null)
    {
        if (RegisteredKeybindings.TryGetValue(group, out var groupKeybindings) && groupKeybindings.TryGetValue(name, out var keybindingDescriptor))
        {
            if (keybindingDescriptor.Command.CanExecute(target))
            {
                keybindingDescriptor.Command.Execute(target);
            }
            else
            {
                throw new InvalidOperationException($"The keybinding '{name}' cannot be executed on the provided target.");
            }
        }
        else
        {
            throw new KeyNotFoundException($"No keybinding with the name '{name}' is registered in group '{group}'.");
        }
    }

    public static void ExecuteKeybinding(string key, object? target = null)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentNullException(nameof(key));

        int separatorIndex = key.IndexOf('_');
        if (separatorIndex <= 0 || separatorIndex >= key.Length - 1)
            throw new ArgumentException($"Invalid keybinding format '{key}'. Expected 'Group_Name'.", nameof(key));

        string group = key.Substring(0, separatorIndex);
        string name = key.Substring(separatorIndex + 1);
        ExecuteKeybinding(group, name, target);
    }

    public static IEnumerable<string> GetKeybindingGroups()
    {
        return RegisteredKeybindings.Keys;
    }

    public static IEnumerable<IKeybindingDescriptor> GetKeybindings()
    {
        return RegisteredKeybindings.Values.SelectMany(group => group.Values);
    }

    public static IEnumerable<IKeybindingDescriptor> GetKeybindings(string group)
    {
        if (RegisteredKeybindings.TryGetValue(group, out var groupKeybindings))
        {
            return groupKeybindings.Values;
        }
        return Enumerable.Empty<IKeybindingDescriptor>();
    }
}
