using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;

namespace Atelier.Nodes;

/// <summary>
/// Connects sockets by dragging, like Blender: from an output to an input, or from an unconnected input back to an
/// output. Dragging from a connected input picks its link up: drop it on another input to move it, or anywhere else to
/// remove it. The link snaps to the nearest socket it can connect to, which is outlined. A new link dropped on empty
/// space opens the add-node menu with the node types it can connect to (see <see cref="NodeEditor.SearchOnLinkDrop"/>).
/// </summary>
public sealed class ConnectCommand : DragCommand
{
    /// <summary>How close to a socket (in pixels) a drag must start, or a link be dropped, to use it.</summary>
    public const float SocketRadius = 12f;

    /// <inheritdoc/>
    /// <remarks>Only drags that start at a socket.</remarks>
    public override bool IsContextual => true;

    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is NodeEditor { CurrentGraph: not null };

    /// <inheritdoc/>
    public override IDragOperation? BeginDrag(DragStart start)
    {
        if (start.Target is not NodeEditor { CurrentGraph: { } graph } editor) return null;
        var position = editor.PointToClient(start.ScreenPosition);
        // Reroute points are moved by dragging them; links go into them from other sockets.
        if (editor.SocketAt(position, SocketRadius, s => s.Node is not RerouteNodeViewModel) is not { } socket) return null;

        return socket switch
        {
            InputSocketViewModel { Link: { } link } => new Operation(editor, graph, link.From, link),
            _ => new Operation(editor, graph, socket, null),
        };
    }

    private sealed class Operation(NodeEditor editor, NodeGraphViewModel graph, SocketViewModel from, LinkViewModel? pickedUp) : IDragOperation
    {
        private SocketViewModel? _target;

        public void Update(Point screenPosition, ModifierKeys modifiers)
        {
            editor.HiddenLink = pickedUp;
            var position = editor.PointToClient(screenPosition);
            _target = editor.SocketAt(position, SocketRadius, Accepts);
            editor.ShowDraggedLink(from, position, _target);
        }

        public void Complete(Point screenPosition, ModifierKeys modifiers)
        {
            Update(screenPosition, modifiers);
            Clear();
            if (pickedUp != null && _target == pickedUp.To) return; // dropped back where it was
            if (pickedUp == null && _target == null)
            {
                // A new link dropped on empty space: offer the nodes it can connect to.
                if (editor.SearchOnLinkDrop && !GroupSockets.IsNewSocket(from)) editor.ShowAddNodeMenu(editor.PointToClient(screenPosition), from);
                return;
            }

            var (output, input) = (from, _target) switch
            {
                (OutputSocketViewModel o, InputSocketViewModel i) => (o, i),
                (InputSocketViewModel i, OutputSocketViewModel o) => (o, i),
                _ => ((OutputSocketViewModel?)null, (InputSocketViewModel?)null),
            };
            string name = GroupSockets.IsNewSocket(output) ? "Add group input" : GroupSockets.IsNewSocket(input) ? "Add group output" : pickedUp != null ? "Reconnect" : "Connect";
            using (graph.Undo.Group(name))
            {
                if (pickedUp != null) graph.Disconnect(pickedUp);
                if (output == null || input == null) return;

                // The empty socket of a group's own input or output node: add the socket to the group first.
                if (GroupSockets.IsNewSocket(output) && graph.Owner is { } inputsOf)
                {
                    var socket = inputsOf.AddInput(input.Name, input.Type, defaultValue: input.Value, minimum: input.Minimum, maximum: input.Maximum, editor: input.Editor);
                    output = inputsOf.InputNode.OutputFor(socket)!;
                }
                else if (GroupSockets.IsNewSocket(input) && graph.Owner is { } outputsOf)
                {
                    input = outputsOf.OutputNode.InputFor(outputsOf.AddOutput(output.Name, output.Type))!;
                }
                graph.Connect(output, input);
            }
        }

        public void Cancel() => Clear();

        private void Clear()
        {
            editor.HiddenLink = null;
            editor.ShowDraggedLink(null, default, null);
        }

        // Sockets the dragged end can connect to (and the input a picked-up link came from).
        private bool Accepts(SocketViewModel socket) => (from, socket) switch
        {
            // The empty socket of a group's own input or output node takes a link from any other socket.
            (OutputSocketViewModel output, InputSocketViewModel input) when GroupSockets.IsNewSocket(output) || GroupSockets.IsNewSocket(input) =>
                !(GroupSockets.IsNewSocket(output) && GroupSockets.IsNewSocket(input)) && output.Node != input.Node,
            (InputSocketViewModel input, OutputSocketViewModel output) when GroupSockets.IsNewSocket(output) || GroupSockets.IsNewSocket(input) =>
                !(GroupSockets.IsNewSocket(output) && GroupSockets.IsNewSocket(input)) && output.Node != input.Node,
            (OutputSocketViewModel output, InputSocketViewModel input) =>
                input == pickedUp?.To || graph.CanConnect(output, input) is ConnectResult.Ok or ConnectResult.AlreadyConnected,
            (InputSocketViewModel input, OutputSocketViewModel output) => graph.CanConnect(output, input) == ConnectResult.Ok,
            _ => false,
        };
    }
}

/// <summary>
/// Moves nodes by dragging one: an unselected node is selected (alone) first; all selected nodes move. Snaps to the grid
/// when <see cref="NodeEditor.SnapToGrid"/> is on, or off while Ctrl is held. The move is one undo step; Escape puts the
/// nodes back.
/// </summary>
/// <remarks>
/// A single node without links dropped onto a link (under the pointer) is inserted into it (see
/// <see cref="NodeEditor.AutoInsert"/>), and the nodes after it move right to make room. With <see cref="Detach"/>, the
/// nodes are first taken out of their links, their neighbors connected directly (see
/// <see cref="NodeGraphViewModel.Detach"/>), like Blender's Alt+drag.
/// </remarks>
public sealed class MoveNodesCommand(bool detach = false) : DragCommand
{
    /// <inheritdoc/>
    /// <remarks>Only drags that start on a node.</remarks>
    public override bool IsContextual => true;

    /// <summary>How far (in pixels) from the pointer a link can be to insert the dropped node into it.</summary>
    public const float InsertDistance = 12f;

    /// <summary>The room left between an inserted node and the node after it, in graph units.</summary>
    public const float InsertSpacing = 40f;

    /// <summary>Gets whether the nodes are taken out of their links before they move.</summary>
    public bool Detach { get; } = detach;

    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is NodeEditor { CurrentGraph: not null };

    /// <inheritdoc/>
    public override IDragOperation? BeginDrag(DragStart start)
    {
        if (start.Target is not NodeEditor { CurrentGraph: { } graph } editor) return null;
        if (editor.NodeAt(editor.PointToClient(start.ScreenPosition)) is not { } node) return null;

        if (!node.IsSelected) editor.SelectOnly(node);
        var nodes = graph.SelectedNodes.ToList();
        return new Operation(editor, graph, nodes, start.ScreenPosition, Detach);
    }

    private sealed class Operation : IDragOperation
    {
        private readonly NodeEditor _editor;
        private readonly NodeGraphViewModel _graph;
        private readonly List<(NodeViewModel Node, Point From)> _nodes;
        private readonly Point _start;
        private readonly UndoStack.UndoGroup _group;
        private readonly IDisposable _suspended;

        public Operation(NodeEditor editor, NodeGraphViewModel graph, List<NodeViewModel> nodes, Point start, bool detach)
        {
            _editor = editor;
            _graph = graph;
            _nodes = nodes.Select(n => (n, n.Position)).ToList();
            _start = start;
            _group = graph.Undo.Group(nodes.Count == 1 ? $"Move {nodes[0].Title}" : "Move");
            if (detach)
            {
                bool detached = false;
                foreach (var node in nodes) detached |= graph.Detach(node);
                if (detached) _group.Name = nodes.Count == 1 ? $"Detach {nodes[0].Title}" : "Detach";
            }
            _suspended = graph.Undo.Suspend(); // the positions are recorded once, at the end
        }

        public void Update(Point screenPosition, ModifierKeys modifiers)
        {
            var delta = (screenPosition - _start) / _editor.Zoom;
            bool snap = _editor.SnapToGrid ^ modifiers.HasFlag(ModifierKeys.Control);
            foreach (var (node, from) in _nodes) node.Position = _editor.Snap(from + delta, snap);
            _editor.InsertTarget = FindInsertTarget(_editor.PointToClient(screenPosition));
        }

        public void Complete(Point screenPosition, ModifierKeys modifiers)
        {
            Update(screenPosition, modifiers);
            var target = _editor.InsertTarget;
            _editor.InsertTarget = null;
            _suspended.Dispose();

            var moves = _nodes.Select(n => (n.Node, n.From, To: n.Node.Position)).Where(m => m.From != m.To).ToList();
            if (moves.Count > 0)
            {
                _graph.Undo.Push(new DelegateUndoAction(_group.Name,
                    () => { foreach (var m in moves) m.Node.Position = m.From; },
                    () => { foreach (var m in moves) m.Node.Position = m.To; }));
            }
            if (target != null && _graph.InsertIntoLink(_nodes[0].Node, target))
            {
                _group.Name = $"Insert {_nodes[0].Node.Title}";
                MakeRoom(_nodes[0].Node, target.To.Node!);
            }
            _group.Dispose();
        }

        public void Cancel()
        {
            _editor.InsertTarget = null;
            foreach (var (node, from) in _nodes) node.Position = from;
            _suspended.Dispose();
            _group.Discard(); // also undoes a detach
        }

        // The link under the pointer that the single, unlinked node being moved can go into.
        private LinkViewModel? FindInsertTarget(Point pointer)
        {
            if (!_editor.AutoInsert || _nodes.Count != 1) return null;
            var node = _nodes[0].Node;
            if (node.HasLinks) return null;
            var link = _editor.LinkAt(pointer, InsertDistance);
            return link != null && _graph.CanInsertIntoLink(node, link) ? link : null;
        }

        // Moves the node after the inserted one, and the nodes after that, right until there's room.
        private void MakeRoom(NodeViewModel inserted, NodeViewModel next)
        {
            float shift = inserted.Position.X + inserted.Width + InsertSpacing - next.Position.X;
            if (shift <= 0) return;
            var after = _graph.GetDownstream(next).Append(next).Where(n => n.Position.X >= inserted.Position.X).ToList();
            foreach (var node in after) node.Position = new Point(node.Position.X + shift, node.Position.Y);
        }
    }
}

/// <summary>How a box selection changes the selection.</summary>
public enum BoxSelectMode
{
    /// <summary>The nodes in the box become the selection.</summary>
    Replace,

    /// <summary>The nodes in the box are added to the selection.</summary>
    Extend,

    /// <summary>The nodes in the box are removed from the selection.</summary>
    Subtract,
}

/// <summary>Selects the nodes a box dragged over the background touches; the selection follows the box as it's drawn.</summary>
public sealed class BoxSelectCommand(BoxSelectMode mode) : DragCommand
{
    /// <inheritdoc/>
    /// <remarks>Only drags that start on the background.</remarks>
    public override bool IsContextual => true;

    /// <summary>Gets how the box changes the selection.</summary>
    public BoxSelectMode Mode { get; } = mode;

    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is NodeEditor { CurrentGraph: not null };

    /// <inheritdoc/>
    public override IDragOperation? BeginDrag(DragStart start)
    {
        if (start.Target is not NodeEditor { CurrentGraph: { } graph } editor) return null;
        var position = editor.PointToClient(start.ScreenPosition);
        if (editor.NodeAt(position) != null) return null;
        return new Operation(editor, graph, Mode, position);
    }

    private sealed class Operation(NodeEditor editor, NodeGraphViewModel graph, BoxSelectMode mode, Point start) : IDragOperation
    {
        private readonly HashSet<NodeViewModel> _before = graph.SelectedNodes.ToHashSet();
        private readonly List<LinkViewModel> _linksBefore = graph.SelectedLinks.ToList();

        public void Update(Point screenPosition, ModifierKeys modifiers)
        {
            var end = editor.PointToClient(screenPosition);
            var box = new Rect(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
            editor.SelectionBox = box;
            var inside = editor.NodesIn(box).ToHashSet();
            foreach (var node in graph.Nodes)
            {
                node.IsSelected = mode switch
                {
                    BoxSelectMode.Extend => _before.Contains(node) || inside.Contains(node),
                    BoxSelectMode.Subtract => _before.Contains(node) && !inside.Contains(node),
                    _ => inside.Contains(node),
                };
            }
            if (mode == BoxSelectMode.Replace)
            {
                foreach (var link in graph.Links) link.IsSelected = false;
            }
        }

        public void Complete(Point screenPosition, ModifierKeys modifiers)
        {
            Update(screenPosition, modifiers);
            editor.SelectionBox = null;
        }

        public void Cancel()
        {
            editor.SelectionBox = null;
            foreach (var node in graph.Nodes) node.IsSelected = _before.Contains(node);
            foreach (var link in _linksBefore) link.IsSelected = true;
        }
    }
}

/// <summary>
/// A tool that draws a stroke by dragging and then acts on the links it crosses: <see cref="CutLinksCommand"/> removes
/// them, <see cref="AddRerouteCommand"/> puts reroute points where it crosses them. Escape cancels the stroke.
/// </summary>
public abstract class LinkStrokeCommand : DragCommand
{
    private const float MinimumStep = 3f;

    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is NodeEditor { CurrentGraph: not null };

    /// <inheritdoc/>
    public override IDragOperation? BeginDrag(DragStart start) =>
        start.Target is NodeEditor { CurrentGraph: { } graph } editor ? new Operation(this, editor, graph, editor.PointToClient(start.ScreenPosition)) : null;

    /// <summary>Whether the stroke cuts links (drawn in the error color) rather than adding to them.</summary>
    private protected abstract bool Cuts { get; }

    /// <summary>Acts on the links the stroke crossed, each with the first point (in graph coordinates) where it did.</summary>
    private protected abstract void Apply(NodeGraphViewModel graph, IReadOnlyList<(LinkViewModel Link, Point At)> crossed);

    private sealed class Operation(LinkStrokeCommand command, NodeEditor editor, NodeGraphViewModel graph, Point start) : IDragOperation
    {
        private readonly List<Point> _points = [start];

        public void Update(Point screenPosition, ModifierKeys modifiers)
        {
            var point = editor.PointToClient(screenPosition);
            var step = point - _points[^1];
            if (step.X * step.X + step.Y * step.Y < MinimumStep * MinimumStep) return;
            _points.Add(point);
            editor.StrokeCuts = command.Cuts;
            editor.Stroke = _points.ToList();
        }

        public void Complete(Point screenPosition, ModifierKeys modifiers)
        {
            Update(screenPosition, modifiers);
            editor.Stroke = null;
            var crossed = new List<(LinkViewModel, Point)>();
            foreach (var link in graph.Links)
            {
                if (LinkGeometry.TryIntersect(_points, editor.GraphToView(link.From.Anchor), editor.GraphToView(link.To.Anchor), editor.Zoom, out var hit))
                {
                    crossed.Add((link, editor.ViewToGraph(hit)));
                }
            }
            if (crossed.Count > 0) command.Apply(graph, crossed);
        }

        public void Cancel() => editor.Stroke = null;
    }
}

/// <summary>Removes the links a stroke drawn by dragging crosses, as one undo step (Blender's Ctrl+right drag).</summary>
public sealed class CutLinksCommand : LinkStrokeCommand
{
    private protected override bool Cuts => true;

    private protected override void Apply(NodeGraphViewModel graph, IReadOnlyList<(LinkViewModel Link, Point At)> crossed)
    {
        using (graph.Undo.Group(crossed.Count == 1 ? "Cut link" : "Cut links"))
        {
            foreach (var (link, _) in crossed) graph.Disconnect(link);
        }
    }
}

/// <summary>Puts a reroute point on each link a stroke drawn by dragging crosses, where it crosses, as one undo step (Blender's Shift+right drag).</summary>
public sealed class AddRerouteCommand : LinkStrokeCommand
{
    private protected override bool Cuts => false;

    private protected override void Apply(NodeGraphViewModel graph, IReadOnlyList<(LinkViewModel Link, Point At)> crossed)
    {
        using (graph.Undo.Group(crossed.Count == 1 ? "Add reroute" : "Add reroutes"))
        {
            foreach (var (link, at) in crossed) graph.InsertReroute(link, at);
        }
    }
}
