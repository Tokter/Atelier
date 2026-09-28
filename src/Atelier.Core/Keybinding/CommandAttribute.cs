using System;

namespace Atelier.Core.Keybinding;

/// <summary>
/// Declares a command for compile-time registration with <see cref="KeybindingManager"/>: its identity (group and
/// name), how it is presented (label, description, icon) and its default keyboard shortcut, if any.
/// </summary>
/// <remarks>
/// <para>
/// Put it on an <see cref="System.Windows.Input.ICommand"/> class with a parameterless constructor, on a command
/// property, or on a command-generating method such as a <c>[RelayCommand]</c> method (with <c>[property: Command(...)]</c>).
/// The source generator registers a descriptor for it (see <see cref="IKeybindingDescriptor"/>); controls bound to the
/// command can then show its label, icon, description and shortcut.
/// </para>
/// <para>
/// <see cref="KeybindingAttribute"/> is the same declaration with the shortcut as the third constructor argument.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [RelayCommand]
/// [property: Command("ToggleTheme", "Global", Label = "Toggle theme", Icon = "DarkMode",
///     Description = "Switch between the light and dark theme", DefaultKeybinding = "Ctrl+T")]
/// private void ToggleTheme() { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
public class CommandAttribute : Attribute
{
    /// <summary>Initializes a command declaration.</summary>
    /// <param name="name">The command's name, unique within its group, such as <c>"ToggleTheme"</c>; it identifies the command and never changes.</param>
    /// <param name="group">The group the command belongs to, such as <c>"Global"</c>; shortcuts only fire in their group.</param>
    public CommandAttribute(string name, string group)
    {
        Name = name;
        Group = group;
    }

    /// <summary>Gets the command's name, unique within its group.</summary>
    public string Name { get; }

    /// <summary>Gets the group the command belongs to.</summary>
    public string Group { get; }

    /// <summary>
    /// Gets or sets the text shown for the command, e.g. on a button or menu item. When empty, it is made from
    /// <see cref="Name"/> ("ToggleTheme" becomes "Toggle theme").
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets what the command does, for tooltips and the keybinding editor.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the command's icon, such as a <c>MaterialIconKind</c> value (<c>"DarkMode"</c>). It is a
    /// name rather than an enum value because the icon sets live in the controls library.
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>Gets or sets the default keyboard shortcut, such as <c>"Ctrl+T"</c> or <c>"Ctrl+K, Ctrl+C"</c>; empty for none.</summary>
    public string DefaultKeybinding { get; set; } = string.Empty;
}
