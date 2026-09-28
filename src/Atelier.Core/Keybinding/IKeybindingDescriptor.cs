using System.Windows.Input;

namespace Atelier.Core.Keybinding
{
    /// <summary>
    /// Describes a registered command: its identity (<see cref="Group"/> and <see cref="Name"/>), how it is presented
    /// (<see cref="Label"/>, <see cref="Description"/>, <see cref="Icon"/>) and its keyboard shortcut, if any.
    /// Compatible with Native AOT without requiring runtime reflection.
    /// </summary>
    public interface IKeybindingDescriptor
    {
        /// <summary>
        /// Gets the command name, unique within its group.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the logical category group of the command.
        /// </summary>
        string Group { get; }

        /// <summary>
        /// Gets the keyboard shortcut associated with the command, or an empty string if it has none.
        /// </summary>
        string Keybinding { get; }

        /// <summary>
        /// Gets the command instance associated with this descriptor.
        /// </summary>
        ICommand Command { get; }

        /// <summary>
        /// Gets the text shown for the command, e.g. on a button or menu item. Defaults to <see cref="Name"/>.
        /// </summary>
        string Label => Name;

        /// <summary>
        /// Gets what the command does, for tooltips; empty if not described.
        /// </summary>
        string Description => string.Empty;

        /// <summary>
        /// Gets the name of the command's icon (such as a <c>MaterialIconKind</c> value); empty for none.
        /// </summary>
        string Icon => string.Empty;
    }
}
