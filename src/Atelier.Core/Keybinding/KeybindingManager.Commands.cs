using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Input;

namespace Atelier.Core.Keybinding;

/// <summary>
/// A keybinding command that stands for a command of some target object (such as a view model), for example
/// <see cref="PropertyKeybindingCommand{TTarget}"/>; lets <see cref="KeybindingManager.FindKeybinding(ICommand, string?, IEnumerable{object?}?)"/>
/// match it to the target's command.
/// </summary>
public interface IKeybindingCommandResolver
{
    /// <summary>Gets the command this keybinding runs for <paramref name="target"/>, or <c>null</c> if it doesn't apply.</summary>
    ICommand? ResolveCommand(object? target);
}

public static partial class KeybindingManager
{
    /// <summary>Occurs after keybindings were registered, changed or removed, e.g. so menus can update their shortcut text.</summary>
    public static event EventHandler? KeybindingsChanged;

    private static void OnKeybindingsChanged() => KeybindingsChanged?.Invoke(null, EventArgs.Empty);

    /// <summary>
    /// Finds the keybinding that runs <paramref name="command"/>, for example to show its shortcut in a menu.
    /// </summary>
    /// <remarks>
    /// A descriptor matches when it holds the same command instance; when it resolves to the command for one of
    /// <paramref name="targets"/> (keybindings on command properties, see <see cref="IKeybindingCommandResolver"/>); or,
    /// for command classes marked with <see cref="CommandAttribute"/>, when it holds an instance of the same class.
    /// Among several matches, <paramref name="preferredGroup"/> wins, then the "Global" group, then the first found.
    /// </remarks>
    /// <param name="command">The command, such as a menu item's.</param>
    /// <param name="preferredGroup">The keybinding group to prefer, or <c>null</c>.</param>
    /// <param name="targets">Objects the command may belong to (such as the DataContexts around a menu item), used to
    /// match keybindings registered on command properties.</param>
    /// <returns>The descriptor, or <c>null</c> if the command has no keybinding.</returns>
    public static IKeybindingDescriptor? FindKeybinding(ICommand command, string? preferredGroup = null, IEnumerable<object?>? targets = null)
    {
        ArgumentNullException.ThrowIfNull(command);

        IKeybindingDescriptor? best = null;
        int bestRank = int.MaxValue;
        bool classMatches = IsKeybindingCommandClass(command.GetType());

        foreach (var (group, byName) in RegisteredKeybindings)
        {
            foreach (var descriptor in byName.Values)
            {
                if (!Matches(descriptor.Command, command, classMatches, targets)) continue;

                int rank = group == preferredGroup ? 0 : group == "Global" ? 1 : 2;
                if (rank < bestRank)
                {
                    best = descriptor;
                    bestRank = rank;
                    if (rank == 0) return best;
                }
            }
        }
        return best;
    }

    /// <summary>
    /// Gets the registered command <paramref name="name"/> in <paramref name="group"/> (with its label, description,
    /// icon and shortcut), or <c>null</c> if there is none.
    /// </summary>
    public static IKeybindingDescriptor? FindCommand(string group, string name)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(name);
        return RegisteredKeybindings.TryGetValue(group, out var byName) && byName.TryGetValue(name, out var descriptor) ? descriptor : null;
    }

    /// <summary>
    /// Gets the registered command that <paramref name="command"/> is (see
    /// <see cref="FindKeybinding(ICommand, string?, IEnumerable{object?}?)"/>), for its label, description, icon and
    /// shortcut; <c>null</c> if it isn't registered. Commands without a shortcut are found too.
    /// </summary>
    public static IKeybindingDescriptor? FindCommand(ICommand command, string? preferredGroup = null, IEnumerable<object?>? targets = null) =>
        FindKeybinding(command, preferredGroup, targets);

    /// <summary>
    /// Gets the display text of the shortcut that runs <paramref name="command"/> (see
    /// <see cref="FindKeybinding(ICommand, string?, IEnumerable{object?}?)"/>), such as <c>"Ctrl+S"</c> or
    /// <c>"Ctrl+K, Ctrl+C"</c>, or <c>null</c> if it has none.
    /// </summary>
    public static string? GetGestureText(ICommand command, string? preferredGroup = null, IEnumerable<object?>? targets = null) =>
        FindKeybinding(command, preferredGroup, targets) is { Keybinding: { Length: > 0 } keybinding }
            ? KeybindingGesture.FormatForDisplay(keybinding)
            : null;

    private static bool Matches(ICommand registered, ICommand command, bool classMatches, IEnumerable<object?>? targets)
    {
        if (ReferenceEquals(registered, command)) return true;

        if (registered is IKeybindingCommandResolver resolver && targets != null)
        {
            foreach (var target in targets)
            {
                if (target != null && ReferenceEquals(resolver.ResolveCommand(target), command)) return true;
            }
        }

        return classMatches && registered.GetType() == command.GetType();
    }

    private static readonly Dictionary<Type, bool> s_keybindingClasses = [];

    // Command classes registered by type ([Command] or [Keybinding] on the class) match any instance of that class; general command
    // types (like RelayCommand) never match by type.
    private static bool IsKeybindingCommandClass(Type type)
    {
        lock (s_keybindingClasses)
        {
            if (!s_keybindingClasses.TryGetValue(type, out bool isClass))
            {
                isClass = type.IsDefined(typeof(CommandAttribute), inherit: false);
                s_keybindingClasses[type] = isClass;
            }
            return isClass;
        }
    }
}
