using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Platform;
using Atelier.Core.Primitives;

namespace Atelier.Nodes;

/// <summary>
/// The node editor's commands, registered in the <see cref="NodeEditor.CommandGroup"/> keybinding group with the
/// editor as their target: users can rebind them in the keybinding editor, and the command palette lists them while the
/// focus is in an editor. The defaults follow Blender, except that middle-drag pans and Delete deletes.
/// </summary>
public static class NodeEditorCommands
{
    /// <summary>How much one step of <see cref="ZoomIn"/> or <see cref="ZoomOut"/> zooms.</summary>
    public const float ZoomStep = 1.1f;

    /// <summary>How far <see cref="Duplicate"/> moves the copies, in graph units.</summary>
    public static readonly Point DuplicateOffset = new(20, 20);

    /// <summary>Gets the command that pans the view by dragging (middle drag).</summary>
    public static PanCommand Pan { get; } = new();

    /// <summary>Gets the command that zooms in around the pointer (wheel up).</summary>
    public static ZoomCommand ZoomIn { get; } = new(ZoomStep);

    /// <summary>Gets the command that zooms out around the pointer (wheel down).</summary>
    public static ZoomCommand ZoomOut { get; } = new(1 / ZoomStep);

    /// <summary>Gets the command that zooms and pans to show all nodes (Home).</summary>
    public static NodeEditorCommand FrameAll { get; } = new(e => e.CurrentGraph?.Nodes.Count > 0, e => e.FrameAll());

    /// <summary>Gets the command that zooms and pans to show the selected nodes (Num .).</summary>
    public static NodeEditorCommand FrameSelected { get; } = new(e => e.CurrentGraph?.SelectedNodes.Any() == true, e => e.FrameNodes(e.CurrentGraph!.SelectedNodes));

    /// <summary>Gets the command that connects sockets by dragging, or moves a link picked up from an input (drag from a socket).</summary>
    public static ConnectCommand Connect { get; } = new();

    /// <summary>Gets the command that moves nodes by dragging one (drag a node).</summary>
    public static MoveNodesCommand MoveNodes { get; } = new();

    /// <summary>Gets the command that takes the dragged nodes out of their links and moves them (Alt+drag a node).</summary>
    public static MoveNodesCommand MoveDetached { get; } = new(detach: true);

    /// <summary>Gets the command that removes the links a stroke crosses (Ctrl+right drag).</summary>
    public static CutLinksCommand CutLinks { get; } = new();

    /// <summary>Gets the command that puts reroute points on the links a stroke crosses (Shift+right drag).</summary>
    public static AddRerouteCommand AddReroute { get; } = new();

    /// <summary>Gets the command that selects the nodes in a box (drag over the background).</summary>
    public static BoxSelectCommand BoxSelect { get; } = new(BoxSelectMode.Replace);

    /// <summary>Gets the command that adds the nodes in a box to the selection (Shift+drag).</summary>
    public static BoxSelectCommand BoxSelectExtend { get; } = new(BoxSelectMode.Extend);

    /// <summary>Gets the command that removes the nodes in a box from the selection (Ctrl+drag).</summary>
    public static BoxSelectCommand BoxSelectSubtract { get; } = new(BoxSelectMode.Subtract);

    /// <summary>Gets the command that selects the node or link under the pointer, or clears the selection over the background (click).</summary>
    public static NodeEditorCommand Select { get; } = new(e => e.CurrentGraph != null, e => SelectAtPointer(e, extend: false));

    /// <summary>Gets the command that adds the node or link under the pointer to the selection, or removes it (Shift+click).</summary>
    public static NodeEditorCommand SelectExtend { get; } = new(e => e.CurrentGraph != null, e => SelectAtPointer(e, extend: true));

    /// <summary>Gets the command that selects all nodes (A).</summary>
    public static NodeEditorCommand SelectAll { get; } = new(e => e.CurrentGraph?.Nodes.Count > 0, e => e.CurrentGraph!.SelectAll());

    /// <summary>Gets the command that deselects everything (Alt+A).</summary>
    public static NodeEditorCommand DeselectAll { get; } = new(HasSelection, e => e.CurrentGraph!.ClearSelection());

    /// <summary>Gets the command that deletes the selected nodes and links (Delete).</summary>
    public static NodeEditorCommand Delete { get; } = new(HasSelection, e => e.CurrentGraph!.DeleteSelection());

    /// <summary>Gets the command that deletes the selected nodes, connecting their neighbors directly (Ctrl+X).</summary>
    public static NodeEditorCommand DeleteReconnect { get; } = new(e => e.CurrentGraph?.SelectedNodes.Any() == true, DeleteWithReconnect);

    /// <summary>Gets the command that copies the selected nodes (and the links between them) to the clipboard (Ctrl+C).</summary>
    public static NodeEditorCommand Copy { get; } = new(e => e.CurrentGraph?.SelectedNodes.Any() == true,
        e => Clipboard.SetText(NodeGraphSerializer.Copy(e.CurrentGraph!.SelectedNodes)));

    /// <summary>Gets the command that pastes copied nodes at the pointer and selects them (Ctrl+V).</summary>
    public static NodeEditorCommand Paste { get; } = new(e => e.CurrentGraph != null && NodeGraphSerializer.IsGraph(Clipboard.GetText()),
        e => NodeGraphSerializer.Paste(e.CurrentGraph!, Clipboard.GetText()!, e.PointerPosition is { } p ? e.ViewToGraph(p) : null));

    /// <summary>Gets the command that duplicates the selected nodes and selects the copies (Shift+D).</summary>
    public static NodeEditorCommand Duplicate { get; } = new(e => e.CurrentGraph?.SelectedNodes.Any() == true, DuplicateSelection);

    /// <summary>Gets the command that collapses the selected nodes, or expands them when all are collapsed (H).</summary>
    public static NodeEditorCommand ToggleCollapse { get; } = new(e => e.CurrentGraph?.SelectedNodes.Any() == true,
        e => Toggle(e, "Collapse", n => n.IsCollapsed, (n, v) => n.IsCollapsed = v));

    /// <summary>Gets the command that mutes the selected nodes, or unmutes them when all are muted (M).</summary>
    public static NodeEditorCommand ToggleMute { get; } = new(e => e.CurrentGraph?.SelectedNodes.Any() == true,
        e => Toggle(e, "Mute", n => n.IsMuted, (n, v) => n.IsMuted = v));

    /// <summary>Gets the command that turns the selected nodes into a group, replacing them with a group node (Ctrl+G).</summary>
    public static NodeEditorCommand MakeGroup { get; } = new(e => e.CurrentGraph?.SelectedNodes.Any(n => n.CanRemove) == true, MakeGroupOfSelection);

    /// <summary>Gets the command that replaces the selected group nodes with the nodes inside their groups (Ctrl+Alt+G).</summary>
    public static NodeEditorCommand Ungroup { get; } = new(e => SelectedGroupNode(e) != null, UngroupSelection);

    /// <summary>Gets the command that enters the selected group node's group, or leaves the group the editor is in (Tab).</summary>
    public static NodeEditorCommand EnterGroup { get; } = new(e => SelectedGroupNode(e) != null || e.GroupPath.Count > 0,
        e =>
        {
            if (SelectedGroupNode(e) is { } node) e.EnterGroup(node);
            else e.ExitGroup();
        });

    /// <summary>Gets the command that leaves the group the editor is in (Ctrl+Tab).</summary>
    public static NodeEditorCommand ExitGroup { get; } = new(e => e.GroupPath.Count > 0, e => e.ExitGroup());

    /// <summary>Gets the command that shows or hides the panel with the group's inputs and outputs (N).</summary>
    public static NodeEditorCommand ToggleGroupInterface { get; } = new(e => e.GroupPath.Count > 0, e => e.ShowGroupInterface = !e.ShowGroupInterface);

    /// <summary>Gets the command that undoes the last change to the graph (Ctrl+Z).</summary>
    public static NodeEditorCommand Undo { get; } = new(e => e.CurrentGraph?.Undo.CanUndo == true, e => e.CurrentGraph!.Undo.Undo());

    /// <summary>Gets the command that redoes the last undone change (Ctrl+Shift+Z).</summary>
    public static NodeEditorCommand Redo { get; } = new(e => e.CurrentGraph?.Undo.CanRedo == true, e => e.CurrentGraph!.Undo.Redo());

    /// <summary>Gets the command that opens the searchable menu of node types to add at the pointer (Shift+A).</summary>
    public static NodeEditorCommand AddNode { get; } = new(e => e.CurrentGraph?.Catalog.Types.Count > 0, e => e.ShowAddNodeMenu(e.PointerPosition));

    /// <summary>Gets the command that opens the editor's context menu at the pointer (right click).</summary>
    public static NodeEditorCommand ContextMenu { get; } = new(e => e.CurrentGraph != null, e => e.ShowContextMenu(e.PointerPosition));

    /// <summary>
    /// Registers the commands that aren't registered yet (with the users' changes applied). Node editors call it when
    /// they're created.
    /// </summary>
    public static void Register()
    {
        // Drag tools sharing a gesture start where they apply: sockets first, then nodes, then the background.
        Add("Connect", "LeftDrag", Connect, "Connect sockets", "Drag from a socket to connect it, or from a connected input to move its link", MaterialIcons.Timeline);
        Add("MoveNodes", "LeftDrag", MoveNodes, "Move nodes", "Drag a node to move the selected nodes; Ctrl toggles snapping", MaterialIcons.OpenWith);
        Add("MoveDetached", "Alt+LeftDrag", MoveDetached, "Detach and move", "Drag a node to take it out of its links, connecting its neighbors directly", MaterialIcons.CallSplit);
        Add("BoxSelect", "LeftDrag", BoxSelect, "Box select", "Drag over the background to select the nodes in a box", MaterialIcons.SelectAll);
        Add("BoxSelectExtend", "Shift+LeftDrag", BoxSelectExtend, "Box select (add)", "Drag to add the nodes in a box to the selection", MaterialIcons.LibraryAdd);
        Add("BoxSelectSubtract", "Ctrl+LeftDrag", BoxSelectSubtract, "Box select (remove)", "Drag to remove the nodes in a box from the selection", MaterialIcons.Deselect);
        Add("CutLinks", "Ctrl+RightDrag", CutLinks, "Cut links", "Drag across links to remove them", MaterialIcons.ContentCut);
        Add("AddReroute", "Shift+RightDrag", AddReroute, "Add reroute", "Drag across links to put reroute points on them", MaterialIcons.Commit);
        Add("Pan", "MiddleDrag", Pan, "Pan", "Move the view by dragging", MaterialIcons.PanTool);
        Add("Select", "LeftClick", Select, "Select", "Select the node or link under the pointer", MaterialIcons.AdsClick);
        Add("SelectExtend", "Shift+LeftClick", SelectExtend, "Add to selection", "Add the node or link under the pointer to the selection, or remove it", MaterialIcons.AddBox);
        Add("SelectAll", "A", SelectAll, "Select all", "Select all nodes", MaterialIcons.SelectAll);
        Add("DeselectAll", "Alt+A", DeselectAll, "Deselect all", "Clear the selection", MaterialIcons.Deselect);
        Add("AddNode", "Shift+A", AddNode, "_Add node…", "Search for a node to add at the pointer", MaterialIcons.AddCircle);
        Add("ContextMenu", "RightClick", ContextMenu, "Context menu", "Open the node editor's menu", MaterialIcons.Menu);
        Add("Delete", "Delete", Delete, "_Delete", "Delete the selected nodes and links", MaterialIcons.Delete);
        Add("DeleteReconnect", "Ctrl+X", DeleteReconnect, "Delete with reconnect", "Delete the selected nodes, connecting what fed them to what they fed", MaterialIcons.LinkOff);
        Add("Copy", "Ctrl+C", Copy, "_Copy", "Copy the selected nodes to the clipboard", MaterialIcons.ContentCopy);
        Add("Paste", "Ctrl+V", Paste, "_Paste", "Paste copied nodes at the pointer", MaterialIcons.ContentPaste);
        Add("Duplicate", "Shift+D", Duplicate, "D_uplicate", "Copy the selected nodes with the links into them", MaterialIcons.CopyAll);
        Add("ToggleCollapse", "H", ToggleCollapse, "_Collapse", "Collapse or expand the selected nodes", MaterialIcons.UnfoldLess);
        Add("ToggleMute", "M", ToggleMute, "_Mute", "Mute or unmute the selected nodes: muted nodes pass their inputs through", MaterialIcons.Block);
        Add("MakeGroup", "Ctrl+G", MakeGroup, "Make _group", "Turn the selected nodes into a reusable group", MaterialIcons.AccountTree);
        Add("Ungroup", "Ctrl+Alt+G", Ungroup, "U_ngroup", "Replace the selected group nodes with the nodes inside their groups", MaterialIcons.CallSplit);
        Add("EnterGroup", "Tab", EnterGroup, "_Enter or leave group", "Edit the selected group node's group, or go back out of the group", MaterialIcons.Login);
        Add("ExitGroup", "Ctrl+Tab", ExitGroup, "Leave group", "Go back out of the group being edited", MaterialIcons.Logout);
        Add("ToggleGroupInterface", "N", ToggleGroupInterface, "Group interface", "Show or hide the panel with the group's inputs and outputs", MaterialIcons.ViewSidebar);
        Add("Undo", "Ctrl+Z", Undo, "_Undo", "Undo the last change to the graph", MaterialIcons.Undo);
        Add("Redo", "Ctrl+Shift+Z", Redo, "_Redo", "Redo the last undone change", MaterialIcons.Redo);
        Add("ZoomIn", "WheelUp", ZoomIn, "Zoom in", "Zoom in around the pointer", MaterialIcons.ZoomIn);
        Add("ZoomOut", "WheelDown", ZoomOut, "Zoom out", "Zoom out around the pointer", MaterialIcons.ZoomOut);
        Add("FrameAll", "Home", FrameAll, "_Frame all", "Zoom and pan to show all nodes", MaterialIcons.FitScreen);
        Add("FrameSelected", "NumPadDecimal", FrameSelected, "Frame _selected", "Zoom and pan to show the selected nodes", MaterialIcons.CenterFocusStrong);
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

    private static bool HasSelection(NodeEditor editor) =>
        editor.CurrentGraph is { } graph && (graph.SelectedNodes.Any() || graph.SelectedLinks.Any());

    private static void SelectAtPointer(NodeEditor editor, bool extend)
    {
        if (editor.CurrentGraph is not { } graph || editor.PointerPosition is not { } position) return;
        if (editor.NodeAt(position) is { } node)
        {
            if (!extend) editor.SelectOnly(node);
            else
            {
                node.IsSelected = !node.IsSelected;
                if (node.IsSelected) graph.BringToFront(node);
            }
            return;
        }
        if (editor.LinkAt(position) is { } link)
        {
            if (!extend) graph.ClearSelection();
            link.IsSelected = !extend || !link.IsSelected;
            return;
        }
        if (!extend) graph.ClearSelection();
    }

    private static void DeleteWithReconnect(NodeEditor editor)
    {
        var graph = editor.CurrentGraph!;
        var nodes = graph.SelectedNodes.ToList();
        using (graph.Undo.Group(nodes.Count == 1 ? $"Delete {nodes[0].Title}" : "Delete"))
        {
            foreach (var node in nodes)
            {
                graph.Detach(node);
                graph.RemoveNode(node);
            }
        }
    }

    // The first selected group node, if any.
    private static GroupNodeViewModel? SelectedGroupNode(NodeEditor editor) =>
        editor.CurrentGraph?.SelectedNodes.OfType<GroupNodeViewModel>().FirstOrDefault();

    private static void MakeGroupOfSelection(NodeEditor editor)
    {
        var graph = editor.CurrentGraph!;
        if (graph.Groups.Group(graph, graph.SelectedNodes.ToList()) is { } node) editor.SelectOnly(node);
    }

    private static void UngroupSelection(NodeEditor editor)
    {
        var graph = editor.CurrentGraph!;
        var nodes = graph.SelectedNodes.OfType<GroupNodeViewModel>().ToList();
        var copies = new List<NodeViewModel>();
        using (graph.Undo.Group(nodes.Count == 1 ? $"Ungroup {nodes[0].Definition.Name}" : "Ungroup"))
        {
            foreach (var node in nodes) copies.AddRange(graph.Groups.Ungroup(node));
        }
        graph.ClearSelection();
        foreach (var copy in copies) copy.IsSelected = true;
    }

    private static void DuplicateSelection(NodeEditor editor)
    {
        var graph = editor.CurrentGraph!;
        var copies = graph.Duplicate(graph.SelectedNodes.ToList(), DuplicateOffset);
        graph.ClearSelection();
        foreach (var copy in copies) copy.IsSelected = true;
    }

    // Sets a flag on all selected nodes: on, unless it's on for all of them already.
    private static void Toggle(NodeEditor editor, string name, Func<NodeViewModel, bool> get, Action<NodeViewModel, bool> set)
    {
        var graph = editor.CurrentGraph!;
        var nodes = graph.SelectedNodes.ToList();
        bool value = !nodes.All(get);
        using (graph.Undo.Group(value ? name : $"Un{name.ToLowerInvariant()}"))
        {
            foreach (var node in nodes) set(node, value);
        }
    }
}

/// <summary>A command on a <see cref="NodeEditor"/> (its target), made of two functions.</summary>
public sealed class NodeEditorCommand(Func<NodeEditor, bool> canExecute, Action<NodeEditor> execute) : AtelierCommand
{
    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is NodeEditor editor && canExecute(editor);

    /// <inheritdoc/>
    public override void Execute(object? parameter)
    {
        if (parameter is NodeEditor editor && canExecute(editor)) execute(editor);
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
