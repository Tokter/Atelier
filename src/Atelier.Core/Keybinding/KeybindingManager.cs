using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Atelier.Core.Events;

namespace Atelier.Core.Keybinding;

/// <summary>
/// Global registry of keybindings, organized by group and name, with lookup and execution by gesture.
/// </summary>
/// <remarks>
/// Registered descriptors (and the commands they hold) live until they are removed with
/// <see cref="UnregisterKeybinding(string, string)"/> or <see cref="Clear"/>. Unregister keybindings whose commands
/// reference short-lived objects such as views or view models.
/// </remarks>
public static partial class KeybindingManager
{
    /// <summary>
    /// Gets the registered keybindings, keyed by group and then by name.
    /// </summary>
    public static Dictionary<string, Dictionary<string, IKeybindingDescriptor>> RegisteredKeybindings { get; } = new Dictionary<string, Dictionary<string, IKeybindingDescriptor>>();
    private static readonly Dictionary<string, KeybindingGesture?> _gestureCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, KeybindingGesture[]?> _sequenceCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a keybinding descriptor.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="keybindingDescriptor"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">A keybinding with the same group and name is already registered.</exception>
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
        ReportConflicts(keybindingDescriptor);
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
        ReportConflicts(keybindingDescriptor);
    }

    /// <summary>
    /// Removes a registered keybinding, releasing its command.
    /// </summary>
    /// <param name="group">The keybinding group.</param>
    /// <param name="name">The keybinding name within the group.</param>
    /// <returns><c>true</c> if a keybinding was removed.</returns>
    public static bool UnregisterKeybinding(string group, string name)
    {
        if (!RegisteredKeybindings.TryGetValue(group, out var groupKeybindings) || !groupKeybindings.Remove(name))
        {
            return false;
        }

        if (groupKeybindings.Count == 0)
        {
            RegisteredKeybindings.Remove(group);
        }
        return true;
    }

    /// <summary>
    /// Removes a registered keybinding, releasing its command.
    /// </summary>
    /// <param name="keybindingDescriptor">The descriptor to remove; matched by group and name, and only if it is the registered instance.</param>
    /// <returns><c>true</c> if the descriptor was registered and has been removed.</returns>
    public static bool UnregisterKeybinding(IKeybindingDescriptor keybindingDescriptor)
    {
        ArgumentNullException.ThrowIfNull(keybindingDescriptor);

        return RegisteredKeybindings.TryGetValue(keybindingDescriptor.Group, out var groupKeybindings)
            && groupKeybindings.TryGetValue(keybindingDescriptor.Name, out var registered)
            && ReferenceEquals(registered, keybindingDescriptor)
            && UnregisterKeybinding(keybindingDescriptor.Group, keybindingDescriptor.Name);
    }

    /// <summary>
    /// Removes all registered keybindings and cached parsed gestures.
    /// </summary>
    public static void Clear()
    {
        RegisteredKeybindings.Clear();
        _gestureCache.Clear();
        _sequenceCache.Clear();
    }

    private static void WarmGestureCache(string? keybindingStr)
    {
        if (!string.IsNullOrWhiteSpace(keybindingStr))
        {
            GetParsedGesture(keybindingStr);
            GetParsedSequence(keybindingStr);
        }
    }

    private static KeybindingGesture[]? GetParsedSequence(string? keybindingStr)
    {
        if (string.IsNullOrWhiteSpace(keybindingStr))
            return null;

        if (!_sequenceCache.TryGetValue(keybindingStr, out var strokes))
        {
            strokes = KeybindingGesture.TryParseSequence(keybindingStr, out var parsed) ? parsed : null;
            _sequenceCache[keybindingStr] = strokes;
        }
        return strokes;
    }

    #region Chords (multi-stroke keybindings)

    /// <summary>
    /// Finds a keybinding in <paramref name="group"/> whose full stroke sequence equals <paramref name="strokes"/>
    /// (a single stroke matches single-gesture keybindings; several strokes match chords such as <c>"Ctrl+K, Ctrl+C"</c>).
    /// </summary>
    public static IKeybindingDescriptor? FindKeybinding(string group, IReadOnlyList<KeybindingGesture> strokes)
    {
        if (string.IsNullOrEmpty(group) || strokes.Count == 0 || !RegisteredKeybindings.TryGetValue(group, out var groupKeybindings))
            return null;

        foreach (var descriptor in groupKeybindings.Values)
        {
            var sequence = GetParsedSequence(descriptor.Keybinding);
            if (sequence != null && sequence.Length == strokes.Count && StartsWith(sequence, strokes))
            {
                return descriptor;
            }
        }
        return null;
    }

    /// <summary>
    /// Determines whether <paramref name="strokes"/> is the beginning of a longer chord registered in <paramref name="group"/>,
    /// i.e. whether more keys are needed to complete a keybinding.
    /// </summary>
    public static bool IsKeybindingPrefix(string group, IReadOnlyList<KeybindingGesture> strokes)
    {
        if (string.IsNullOrEmpty(group) || strokes.Count == 0 || !RegisteredKeybindings.TryGetValue(group, out var groupKeybindings))
            return false;

        foreach (var descriptor in groupKeybindings.Values)
        {
            var sequence = GetParsedSequence(descriptor.Keybinding);
            if (sequence != null && sequence.Length > strokes.Count && StartsWith(sequence, strokes))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Finds the keybinding in <paramref name="group"/> matching <paramref name="strokes"/> exactly and executes it on
    /// <paramref name="target"/> if its command can execute.
    /// </summary>
    /// <returns><c>true</c> if a keybinding was executed.</returns>
    public static bool TryExecuteSequence(string group, IReadOnlyList<KeybindingGesture> strokes, object? target = null)
    {
        var descriptor = FindKeybinding(group, strokes);
        if (descriptor != null && descriptor.Command.CanExecute(target))
        {
            descriptor.Command.Execute(target);
            return true;
        }
        return false;
    }

    private static bool StartsWith(KeybindingGesture[] sequence, IReadOnlyList<KeybindingGesture> prefix)
    {
        for (int i = 0; i < prefix.Count; i++)
        {
            if (!sequence[i].Equals(prefix[i]))
            {
                return false;
            }
        }
        return true;
    }

    #endregion

    #region Conflicts

    /// <summary>
    /// Describes two keybindings in the same group that cannot both be triggered as intended.
    /// </summary>
    /// <param name="Group">The group both keybindings belong to.</param>
    /// <param name="First">The keybinding that wins (its sequence is equal to, or a prefix of, the other's).</param>
    /// <param name="Second">The keybinding that is shadowed.</param>
    /// <param name="IsPrefix">
    /// <c>true</c> if <paramref name="First"/> is a shorter keybinding that fires before the chord <paramref name="Second"/>
    /// can be completed; <c>false</c> if both use the same keys.
    /// </param>
    public sealed record KeybindingConflict(string Group, IKeybindingDescriptor First, IKeybindingDescriptor Second, bool IsPrefix);

    /// <summary>
    /// Finds keybindings that shadow each other within a group: identical key sequences (only the first registered one
    /// fires) and single keybindings that are the first stroke of a chord (the chord can never be completed).
    /// </summary>
    /// <returns>The conflicts, in registration order.</returns>
    public static IReadOnlyList<KeybindingConflict> GetConflicts()
    {
        var conflicts = new List<KeybindingConflict>();
        foreach (var (group, groupKeybindings) in RegisteredKeybindings)
        {
            var descriptors = groupKeybindings.Values.ToArray();
            for (int i = 0; i < descriptors.Length; i++)
            {
                for (int j = i + 1; j < descriptors.Length; j++)
                {
                    if (TryGetConflict(group, descriptors[i], descriptors[j], out var conflict))
                    {
                        conflicts.Add(conflict);
                    }
                }
            }
        }
        return conflicts;
    }

    private static bool TryGetConflict(string group, IKeybindingDescriptor a, IKeybindingDescriptor b, out KeybindingConflict conflict)
    {
        conflict = null!;
        var sa = GetParsedSequence(a.Keybinding);
        var sb = GetParsedSequence(b.Keybinding);
        if (sa == null || sb == null)
        {
            return false;
        }

        if (sa.Length <= sb.Length && StartsWith(sb, sa))
        {
            conflict = new KeybindingConflict(group, a, b, IsPrefix: sa.Length < sb.Length);
            return true;
        }

        if (sb.Length < sa.Length && StartsWith(sa, sb))
        {
            conflict = new KeybindingConflict(group, b, a, IsPrefix: true);
            return true;
        }

        return false;
    }

    private static void ReportConflicts(IKeybindingDescriptor added)
    {
        if (!RegisteredKeybindings.TryGetValue(added.Group, out var groupKeybindings))
            return;

        foreach (var other in groupKeybindings.Values)
        {
            if (!ReferenceEquals(other, added) && TryGetConflict(added.Group, other, added, out var conflict))
            {
                Debug.WriteLine(
                    $"[Keybinding] Conflict in group '{conflict.Group}': '{conflict.First.Name}' ({conflict.First.Keybinding}) " +
                    (conflict.IsPrefix ? "fires before chord" : "shadows") +
                    $" '{conflict.Second.Name}' ({conflict.Second.Keybinding}).");
            }
        }
    }

    #endregion

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

    /// <summary>
    /// Determines whether the keybinding <paramref name="name"/> in <paramref name="group"/> exists and can run for <paramref name="target"/>.
    /// </summary>
    public static bool CanExecuteKeybinding(string group, string name, object? target = null)
    {
        if (RegisteredKeybindings.TryGetValue(group, out var groupKeybindings) && groupKeybindings.TryGetValue(name, out var keybindingDescriptor))
        {
            return keybindingDescriptor.Command.CanExecute(target);
        }
        return false;
    }

    /// <summary>
    /// Determines whether the keybinding identified by <paramref name="key"/> (<c>"Group_Name"</c>, as in the generated
    /// constants) exists and can run for <paramref name="target"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is not in <c>"Group_Name"</c> form.</exception>
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

    /// <summary>
    /// Runs the keybinding <paramref name="name"/> in <paramref name="group"/> for <paramref name="target"/>.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No such keybinding is registered.</exception>
    /// <exception cref="InvalidOperationException">The command cannot run for <paramref name="target"/>.</exception>
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

    /// <summary>
    /// Runs the keybinding identified by <paramref name="key"/> (<c>"Group_Name"</c>) for <paramref name="target"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is not in <c>"Group_Name"</c> form.</exception>
    /// <exception cref="KeyNotFoundException">No such keybinding is registered.</exception>
    /// <exception cref="InvalidOperationException">The command cannot run for <paramref name="target"/>.</exception>
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

    /// <summary>Gets the names of all groups that have registered keybindings.</summary>
    public static IEnumerable<string> GetKeybindingGroups()
    {
        return RegisteredKeybindings.Keys;
    }

    /// <summary>Gets all registered keybindings across all groups.</summary>
    public static IEnumerable<IKeybindingDescriptor> GetKeybindings()
    {
        return RegisteredKeybindings.Values.SelectMany(group => group.Values);
    }

    /// <summary>Gets the keybindings registered in <paramref name="group"/>, or an empty sequence.</summary>
    public static IEnumerable<IKeybindingDescriptor> GetKeybindings(string group)
    {
        if (RegisteredKeybindings.TryGetValue(group, out var groupKeybindings))
        {
            return groupKeybindings.Values;
        }
        return Enumerable.Empty<IKeybindingDescriptor>();
    }
}
