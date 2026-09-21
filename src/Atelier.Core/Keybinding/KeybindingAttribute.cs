using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atelier.Core.Keybinding
{
    /// <summary>
    /// Marks an <see cref="System.Windows.Input.ICommand"/> implementation, property, or command-generating method for compile-time registration with <see cref="KeybindingManager"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = true)]
    public class KeybindingAttribute : Attribute
    {
        /// <summary>
        /// Gets the command name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the logical category group of the command.
        /// </summary>
        public string Group { get; }

        /// <summary>
        /// Gets the default keyboard shortcut associated with the command, if any.
        /// </summary>
        public string DefaultKeybinding { get; }

        public KeybindingAttribute(string name, string group, string defaultKeybinding = "")
        {
            Name = name;
            Group = group;
            DefaultKeybinding = defaultKeybinding ?? string.Empty;
        }
    }
}
