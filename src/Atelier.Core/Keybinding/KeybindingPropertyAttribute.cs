using System;

namespace Atelier.Core.Keybinding;

/// <summary>
/// Marks a property or a command-generating method (such as via <c>[RelayCommand]</c>) for compile-time keybinding registration.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
public sealed class KeybindingPropertyAttribute : KeybindingAttribute
{
    public KeybindingPropertyAttribute(string name, string group, string defaultKeybinding = "")
        : base(name, group, defaultKeybinding)
    {
    }
}
