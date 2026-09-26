using System;
using System.Windows.Input;

namespace Atelier.Core.Keybinding;

/// <summary>
/// A statically registered descriptor of a command with an associated keybinding, wrapping an <see cref="ICommand"/> instance.
/// Compatible with Native AOT without runtime reflection.
/// Normalizes keybinding gestures to canonical modifier order upon creation.
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

    /// <summary>
    /// Initializes a new instance of the <see cref="KeybindingDescriptor"/> class.
    /// </summary>
    /// <param name="name">The command name.</param>
    /// <param name="group">The logical category group of the command.</param>
    /// <param name="keybinding">The keyboard shortcut; normalized with <see cref="KeybindingGesture.Normalize"/>.</param>
    /// <param name="command">The command to execute.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/>, <paramref name="group"/> or <paramref name="command"/> is <see langword="null"/>.</exception>
    public KeybindingDescriptor(string name, string group, string keybinding, ICommand command)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Group = group ?? throw new ArgumentNullException(nameof(group));
        Keybinding = KeybindingGesture.Normalize(keybinding);
        Command = command ?? throw new ArgumentNullException(nameof(command));
    }
}
