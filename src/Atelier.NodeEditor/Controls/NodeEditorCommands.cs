using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;

namespace Atelier.Nodes;

/// <summary>
/// The node editor's commands, registered in the <see cref="NodeEditor.CommandGroup"/> keybinding group with the
/// editor as their target: users can rebind them in the keybinding editor, and the command palette lists them while the
/// focus is in an editor.
/// </summary>
public static class NodeEditorCommands
{
    /// <summary>How much one step of <see cref="ZoomIn"/> or <see cref="ZoomOut"/> zooms.</summary>
    public const float ZoomStep = 1.1f;

    /// <summary>Gets the command that pans the view by dragging (middle drag).</summary>
    public static PanCommand Pan { get; } = new();

    /// <summary>Gets the command that zooms in around the pointer (wheel up).</summary>
    public static ZoomCommand ZoomIn { get; } = new(ZoomStep);

    /// <summary>Gets the command that zooms out around the pointer (wheel down).</summary>
    public static ZoomCommand ZoomOut { get; } = new(1 / ZoomStep);

    /// <summary>Gets the command that zooms and pans to show all nodes (Home).</summary>
    public static FrameAllCommand FrameAll { get; } = new();

    /// <summary>
    /// Registers the commands that aren't registered yet (with the users' changes applied). Node editors call it when
    /// they're created.
    /// </summary>
    public static void Register()
    {
        Add("Pan", "MiddleDrag", Pan, "Pan", "Move the view by dragging", MaterialIcons.PanTool);
        Add("ZoomIn", "WheelUp", ZoomIn, "Zoom in", "Zoom in around the pointer", MaterialIcons.ZoomIn);
        Add("ZoomOut", "WheelDown", ZoomOut, "Zoom out", "Zoom out around the pointer", MaterialIcons.ZoomOut);
        Add("FrameAll", "Home", FrameAll, "Frame all", "Zoom and pan to show all nodes", MaterialIcons.FitScreen);
    }

    private static void Add(string name, string keybinding, AtelierCommand command, string label, string description, string icon)
    {
        foreach (var registered in KeybindingManager.GetKeybindings(NodeEditor.CommandGroup))
        {
            if (registered.Name == name) return;
        }
        KeybindingManager.RegisterKeybinding(new KeybindingDescriptor(name, NodeEditor.CommandGroup, keybinding, command,
            label: label, description: description, icon: icon));
    }
}

/// <summary>Pans a <see cref="NodeEditor"/>'s view by dragging: the graph follows the pointer; Escape moves it back.</summary>
public sealed class PanCommand : DragCommand
{
    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is NodeEditor;

    /// <inheritdoc/>
    public override IDragOperation? BeginDrag(DragStart start) =>
        start.Target is NodeEditor editor ? new PanOperation(editor, start.ScreenPosition) : null;

    private sealed class PanOperation(NodeEditor editor, Point start) : IDragOperation
    {
        private readonly Point _startOffset = editor.Offset;

        public void Update(Point screenPosition, ModifierKeys modifiers)
        {
            // Screen pixels are view pixels as long as the editor itself isn't scaled.
            editor.Offset = _startOffset + (screenPosition - start);
        }

        public void Complete(Point screenPosition, ModifierKeys modifiers) => Update(screenPosition, modifiers);

        public void Cancel() => editor.Offset = _startOffset;
    }
}

/// <summary>Zooms a <see cref="NodeEditor"/> by a factor, around the pointer when it's over the editor (else the center).</summary>
public sealed class ZoomCommand(float factor) : AtelierCommand
{
    /// <summary>Gets the factor the zoom is multiplied by.</summary>
    public float Factor { get; } = factor;

    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is NodeEditor;

    /// <inheritdoc/>
    public override void Execute(object? parameter)
    {
        if (parameter is not NodeEditor editor) return;
        var around = editor.PointerPosition ?? new Point(editor.Bounds.Width / 2, editor.Bounds.Height / 2);
        editor.ZoomAt(around, editor.Zoom * Factor);
    }
}

/// <summary>Zooms and pans a <see cref="NodeEditor"/> to show all its nodes (see <see cref="NodeEditor.FrameAll"/>).</summary>
public sealed class FrameAllCommand : AtelierCommand
{
    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is NodeEditor { Graph.Nodes.Count: > 0 };

    /// <inheritdoc/>
    public override void Execute(object? parameter) => (parameter as NodeEditor)?.FrameAll();
}
