using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Atelier.Core.Keybinding
{
    /// <summary>
    /// Represents a statically registered descriptor of a command with an associated keybinding.
    /// Compatible with Native AOT without requiring runtime reflection.
    /// </summary>
    public interface IKeybindingDescriptor
    {  
        /// <summary>
        /// Gets the command name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the logical category group of the command.
        /// </summary>
        string Group { get; }

        /// <summary>
        /// Gets the keyboard shortcut associated with the command, if any.
        /// </summary>
        string Keybinding { get; }

        /// <summary>
        /// Gets the command instance associated with this descriptor.
        /// </summary>
        ICommand Command { get; }
    }
}
