using System;
using System.Windows.Input;

namespace Atelier.Core.Keybinding;

/// <summary>
/// A user's changes to a command: its label, icon or keyboard shortcut. <c>null</c> keeps the command's own value
/// (from its <see cref="CommandAttribute"/>); an empty <see cref="Keybinding"/> removes the shortcut.
/// </summary>
/// <param name="Label">The label shown for the command, or <c>null</c> for the default.</param>
/// <param name="Icon">The icon (a Material icon name, SVG path data or an SVG document), or <c>null</c> for the default.</param>
/// <param name="Keybinding">The shortcut, such as <c>"Ctrl+Shift+T"</c> or <c>"Ctrl+K, Ctrl+T"</c>; empty for none; <c>null</c> for the default.</param>
public sealed record CommandCustomization(string? Label = null, string? Icon = null, string? Keybinding = null)
{
    /// <summary>Gets whether the customization changes nothing.</summary>
    public bool IsEmpty => Label == null && Icon == null && Keybinding == null;
}

/// <summary>
/// A registered command with a user's <see cref="CommandCustomization"/> applied: the customized label, icon and shortcut,
/// with the command's own values in <see cref="Original"/>.
/// </summary>
/// <remarks>Created by <see cref="KeybindingManager"/> for registered commands that have a customization.</remarks>
public sealed class CustomizedKeybindingDescriptor : IKeybindingDescriptor
{
    internal CustomizedKeybindingDescriptor(IKeybindingDescriptor original, CommandCustomization customization)
    {
        Original = original;
        Customization = customization;
        Keybinding = customization.Keybinding != null ? KeybindingGesture.Normalize(customization.Keybinding) : original.Keybinding;
    }

    /// <summary>Gets the command as registered, with its default label, icon and shortcut.</summary>
    public IKeybindingDescriptor Original { get; }

    /// <summary>Gets the user's changes.</summary>
    public CommandCustomization Customization { get; }

    /// <inheritdoc/>
    public string Name => Original.Name;

    /// <inheritdoc/>
    public string Group => Original.Group;

    /// <inheritdoc/>
    public string Keybinding { get; }

    /// <inheritdoc/>
    public ICommand Command => Original.Command;

    /// <inheritdoc/>
    public string Label => Customization.Label ?? Original.Label;

    /// <inheritdoc/>
    public string Description => Original.Description;

    /// <inheritdoc/>
    public string Icon => Customization.Icon ?? Original.Icon;
}
