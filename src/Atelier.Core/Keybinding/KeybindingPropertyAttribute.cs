using System;

namespace Atelier.Core.Keybinding;

/// <summary>
/// Marks a property or a command-generating method (such as via <c>[RelayCommand]</c>) for compile-time keybinding registration.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
public sealed class KeybindingPropertyAttribute : KeybindingAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KeybindingPropertyAttribute"/> class.
    /// </summary>
    /// <param name="name">The command name.</param>
    /// <param name="group">The logical category group of the command.</param>
    /// <param name="defaultKeybinding">The default keyboard shortcut, or an empty string for none.</param>
    public KeybindingPropertyAttribute(string name, string group, string defaultKeybinding = "")
        : base(name, group, defaultKeybinding)
    {
    }
}
