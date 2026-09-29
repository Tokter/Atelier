using System;
using System.Collections.Generic;
using System.Windows.Input;
using Atelier.Core.Keybinding;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// A further keybinding group a <see cref="KeybindingHandler"/> runs besides its own <see cref="KeybindingHandler.Group"/>
/// (see <see cref="KeybindingHandler.AdditionalScopes"/>), optionally for a fixed target object.
/// </summary>
/// <remarks>
/// For example, a window's handler can run the commands of the page it shows, on the page's view model, wherever the
/// focus is. Change <see cref="Group"/> and <see cref="Target"/> when the page changes.
/// </remarks>
public sealed class KeybindingScope
{
    /// <summary>Initializes a scope for <paramref name="group"/>.</summary>
    /// <param name="group">The keybinding group; empty for none.</param>
    /// <param name="target">The object the group's commands run on, or <c>null</c> to find it as for the handler's own group.</param>
    public KeybindingScope(string group = "", object? target = null)
    {
        Group = group;
        Target = target;
    }

    /// <summary>Gets or sets the keybinding group; empty for none.</summary>
    public string Group { get; set; }

    /// <summary>
    /// Gets or sets the object the group's commands run on, such as a view model; <c>null</c> finds it as for the
    /// handler's own group (the DataContexts from the focused element up to the handler, then the handler's).
    /// </summary>
    public object? Target { get; set; }
}

/// <summary>
/// A command that would run from the current focus: its registration, the <see cref="KeybindingHandler"/> whose group
/// provides it, and the object it runs on. See <see cref="KeybindingHandler.GetActiveCommands"/>.
/// </summary>
public sealed class ActiveCommand
{
    internal ActiveCommand(IKeybindingDescriptor descriptor, KeybindingHandler? handler, object? target)
    {
        Descriptor = descriptor;
        Handler = handler;
        Target = target;
    }

    /// <summary>Gets the command's registration: label, description, icon, shortcut and the command.</summary>
    public IKeybindingDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the handler whose group (or <see cref="KeybindingScope"/>) provides the command; <c>null</c> for a command of a
    /// window group (see <see cref="KeybindingManager.WindowGroups"/>).
    /// </summary>
    public KeybindingHandler? Handler { get; }

    /// <summary>Gets the object the command runs on (passed to <see cref="ICommand.Execute"/>), or <c>null</c>.</summary>
    public object? Target { get; }

    /// <summary>
    /// Gets the command closer to the focus that has the same shortcut, so that pressing it runs that one instead;
    /// <c>null</c> if this command's shortcut runs this command.
    /// </summary>
    public ActiveCommand? ShadowedBy { get; internal set; }

    /// <summary>Gets whether the command can run now.</summary>
    public bool CanExecute => Descriptor.Command.CanExecute(Target);

    /// <summary>Runs the command if it can run; <c>false</c> if it couldn't.</summary>
    public bool TryExecute()
    {
        if (!CanExecute) return false;
        Descriptor.Command.Execute(Target);
        return true;
    }
}

public partial class KeybindingHandler
{
    /// <summary>
    /// Gets the commands whose shortcuts work with the focus on <paramref name="focusedElement"/>: those of the groups of
    /// the <see cref="KeybindingHandler"/>s from the element up to the window's root (their <see cref="Group"/> and
    /// <see cref="AdditionalScopes"/>), innermost first, each with the object it runs on, like a key press would find it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The walk continues from an open popup to the element that owns it (see <see cref="FocusManager.GetTreeOwner"/>).
    /// A command a handler finds no target for (a view model's command whose view model isn't around) isn't listed; one
    /// found by several handlers is listed once, for the innermost. A command whose shortcut an inner command also uses
    /// has <see cref="ActiveCommand.ShadowedBy"/> set. The commands of the window groups
    /// (<see cref="KeybindingManager.WindowGroups"/>), which work anywhere in a window, come last.
    /// </para>
    /// <para>Used by <see cref="CommandPalette"/>; also useful for a "shortcuts here" help.</para>
    /// </remarks>
    /// <param name="focusedElement">The focused element, or the window's root when nothing has the focus.</param>
    public static IReadOnlyList<ActiveCommand> GetActiveCommands(UIElement focusedElement)
    {
        ArgumentNullException.ThrowIfNull(focusedElement);
        var result = new List<ActiveCommand>();
        var listed = new HashSet<(string, string)>();
        var byShortcut = new Dictionary<string, ActiveCommand>(StringComparer.OrdinalIgnoreCase);

        for (VisualNode? node = focusedElement; node != null; node = Up(node))
        {
            if (node is not KeybindingHandler handler) continue;
            foreach (var scope in handler.Scopes())
            {
                foreach (var descriptor in KeybindingManager.GetKeybindings(scope.Group))
                {
                    if (listed.Contains((descriptor.Group, descriptor.Name))) continue;
                    if (!handler.TryFindTarget(descriptor.Command, scope, focusedElement, out var target)) continue;
                    Add(descriptor, handler, target);
                }
            }
        }

        // Window groups run for keys nothing else handled, without a target.
        foreach (string group in KeybindingManager.WindowGroups)
        {
            foreach (var descriptor in KeybindingManager.GetKeybindings(group))
            {
                if (listed.Contains((descriptor.Group, descriptor.Name)) || descriptor.Command is IKeybindingCommandResolver) continue;
                Add(descriptor, null, null);
            }
        }
        return result;

        void Add(IKeybindingDescriptor descriptor, KeybindingHandler? handler, object? target)
        {
            listed.Add((descriptor.Group, descriptor.Name));
            var command = new ActiveCommand(descriptor, handler, target);
            if (descriptor.Keybinding.Length > 0)
            {
                if (byShortcut.TryGetValue(descriptor.Keybinding, out var first)) command.ShadowedBy = first;
                else byShortcut[descriptor.Keybinding] = command;
            }
            result.Add(command);
        }
    }

    // One step up: the parent, or from the root of an owned tree (an open popup) to its owner.
    private static VisualNode? Up(VisualNode node) => node.Parent ?? FocusManager.GetTreeOwner(node);

    // The handler's own group, then its additional scopes (those with a group).
    private IEnumerable<KeybindingScope> Scopes()
    {
        if (!string.IsNullOrEmpty(Group)) yield return new KeybindingScope(Group);
        foreach (var scope in AdditionalScopes)
        {
            if (!string.IsNullOrEmpty(scope.Group)) yield return scope;
        }
    }

    // The objects a command of the handler's group may run on, in the order key handling tries them: the DataContext of
    // the element the key came from, the distinct DataContexts of its ancestors up to this handler, this handler's
    // DataContext; null only when neither the element nor the handler has one.
    private IEnumerable<object?> TargetCandidates(UIElement? origin)
    {
        var seen = new List<object>();
        object? initial = origin?.DataContext;
        if (initial != null)
        {
            seen.Add(initial);
            yield return initial;
        }
        for (VisualNode? node = origin == null ? null : Up(origin); node != null && node != this; node = Up(node))
        {
            if (node is UIElement { DataContext: { } dc } && !seen.Contains(dc))
            {
                seen.Add(dc);
                yield return dc;
            }
        }
        if (DataContext != null && !seen.Contains(DataContext)) yield return DataContext;
        if (initial == null && DataContext == null) yield return null;
    }

    // The object a command of scope would run on: the scope's own target, or the first candidate the command applies to
    // (a view model's command applies to that view model only; other commands to the first candidate).
    private bool TryFindTarget(ICommand command, KeybindingScope scope, UIElement? origin, out object? target)
    {
        IEnumerable<object?> candidates = scope.Target != null ? new[] { scope.Target } : TargetCandidates(origin);
        foreach (var candidate in candidates)
        {
            if (command is not IKeybindingCommandResolver resolver || (candidate != null && resolver.ResolveCommand(candidate) != null))
            {
                target = candidate;
                return true;
            }
        }
        target = null;
        return false;
    }
}
