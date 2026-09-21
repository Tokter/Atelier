using System;
using System.Windows.Input;

namespace Atelier.Core.Keybinding;

/// <summary>
/// A statically registered descriptor of a command with an associated keybinding, wrapping an <see cref="ICommand"/> instance.
/// Compatible with Native AOT without runtime reflection.
/// </summary>
public sealed class KeybindingDescriptor : IKeybindingDescriptor
{
    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Group { get; }

    /// <inheritdoc />
    public string Keybinding { get; }

    /// <summary>
    /// Gets the underlying <see cref="ICommand"/> instance.
    /// </summary>
    public ICommand Command { get; }

    public KeybindingDescriptor(string name, string group, string keybinding, ICommand command)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Group = group ?? throw new ArgumentNullException(nameof(group));
        Keybinding = keybinding ?? string.Empty;
        Command = command ?? throw new ArgumentNullException(nameof(command));
    }
}
