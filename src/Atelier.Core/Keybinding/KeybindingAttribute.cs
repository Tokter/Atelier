using System;

namespace Atelier.Core.Keybinding
{
    /// <summary>
    /// Marks an <see cref="System.Windows.Input.ICommand"/> implementation, property, or command-generating method for compile-time registration with <see cref="KeybindingManager"/>.
    /// </summary>
    /// <remarks>
    /// A <see cref="CommandAttribute"/> with the default shortcut as the third constructor argument; the label,
    /// description and icon can be set as named arguments too.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public class KeybindingAttribute : CommandAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="KeybindingAttribute"/> class.
        /// </summary>
        /// <param name="name">The command name.</param>
        /// <param name="group">The logical category group of the command.</param>
        /// <param name="defaultKeybinding">The default keyboard shortcut, or an empty string for none; <see langword="null"/> is treated as empty.</param>
        public KeybindingAttribute(string name, string group, string defaultKeybinding = "")
            : base(name, group)
        {
            DefaultKeybinding = defaultKeybinding ?? string.Empty;
        }
    }
}
