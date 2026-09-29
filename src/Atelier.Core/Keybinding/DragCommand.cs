using System.Windows.Input;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;

namespace Atelier.Core.Keybinding;

/// <summary>
/// A command run by dragging with the pointer, bound to a drag gesture such as <c>"MiddleDrag"</c> or
/// <c>"Ctrl+RightDrag"</c> (see <see cref="PointerGesture"/>). When the pointer moves far enough with the button held, a
/// keybinding handler calls <see cref="BeginDrag"/>, then updates the returned <see cref="IDragOperation"/> until the
/// button is released (or Escape cancels it).
/// </summary>
/// <remarks>
/// Several drag commands of a group can share a gesture: the first one whose <see cref="BeginDrag"/> returns an operation
/// runs, so each can decide by where the drag starts (<see cref="DragStart.Source"/>), like Blender's tools. Drag
/// commands can't run from a menu or the command palette.
/// </remarks>
public interface IDragCommand : ICommand
{
    /// <summary>Starts a drag, or returns <c>null</c> if the command doesn't apply to it (for example not over its kind of element).</summary>
    IDragOperation? BeginDrag(DragStart start);
}

/// <summary>A drag in progress, started by <see cref="IDragCommand.BeginDrag"/>.</summary>
public interface IDragOperation
{
    /// <summary>Called as the pointer moves, with its position in window coordinates and the modifier keys held.</summary>
    void Update(Point screenPosition, ModifierKeys modifiers);

    /// <summary>Called when the button is released: the drag is done.</summary>
    void Complete(Point screenPosition, ModifierKeys modifiers);

    /// <summary>Called when the drag is canceled (Escape, or the pointer capture is lost): undo what it did.</summary>
    void Cancel();
}

/// <summary>Where and how a drag started; see <see cref="IDragCommand.BeginDrag"/>.</summary>
public sealed class DragStart
{
    /// <summary>Initializes a drag start.</summary>
    public DragStart(object? target, UIElement handler, UIElement? source, Point screenPosition, PointerButtons button, ModifierKeys modifiers)
    {
        Target = target;
        Handler = handler;
        Source = source;
        ScreenPosition = screenPosition;
        Button = button;
        Modifiers = modifiers;
    }

    /// <summary>Gets the object the command runs on, as for other keybindings (such as the editor or a view model).</summary>
    public object? Target { get; }

    /// <summary>Gets the element that handles the gesture (the keybinding handler); it has the pointer captured during the drag.</summary>
    public UIElement Handler { get; }

    /// <summary>Gets the element the button was pressed on, or <c>null</c>.</summary>
    public UIElement? Source { get; }

    /// <summary>Gets where the button was pressed, in window coordinates.</summary>
    public Point ScreenPosition { get; }

    /// <summary>Gets where the button was pressed, in the coordinates of <see cref="Handler"/>.</summary>
    public Point Position => Handler.PointToClient(ScreenPosition);

    /// <summary>Gets the button held.</summary>
    public PointerButtons Button { get; }

    /// <summary>Gets the modifier keys held when the button was pressed.</summary>
    public ModifierKeys Modifiers { get; }
}

/// <summary>
/// A base for drag commands: <see cref="ICommand.Execute"/> does nothing (a drag can only start from the pointer), and
/// <see cref="ICommand.CanExecute"/> says whether the command applies to a target.
/// </summary>
public abstract class DragCommand : AtelierCommand, IDragCommand
{
    /// <inheritdoc/>
    public abstract IDragOperation? BeginDrag(DragStart start);

    /// <summary>Does nothing: drag commands run from the pointer.</summary>
    public sealed override void Execute(object? parameter)
    {
    }
}
