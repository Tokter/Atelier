using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;

namespace Atelier.Nodes;

/// <summary>
/// Connects sockets by dragging, like Blender: from an output to an input, or from an unconnected input back to an
/// output. Dragging from a connected input picks its link up: drop it on another input to move it, or anywhere else to
/// remove it. The link snaps to the nearest socket it can connect to, which is outlined.
/// </summary>
public sealed class ConnectCommand : DragCommand
{
    /// <summary>How close to a socket (in pixels) a drag must start, or a link be dropped, to use it.</summary>
    public const float SocketRadius = 12f;

    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is NodeEditor { Graph: not null };

    /// <inheritdoc/>
    public override IDragOperation? BeginDrag(DragStart start)
    {
        if (start.Target is not NodeEditor { Graph: { } graph } editor) return null;
        var position = editor.PointToClient(start.ScreenPosition);
        if (editor.SocketAt(position, SocketRadius) is not { } socket) return null;

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

            using (graph.Undo.Group(pickedUp != null ? "Reconnect" : "Connect"))
            {
                if (pickedUp != null) graph.Disconnect(pickedUp);
                switch (_target)
                {
                    case InputSocketViewModel input when from is OutputSocketViewModel output:
                        graph.Connect(output, input);
                        break;
                    case OutputSocketViewModel output when from is InputSocketViewModel input:
                        graph.Connect(output, input);
                        break;
                }
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
public sealed class MoveNodesCommand : DragCommand
{
    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is NodeEditor { Graph: not null };

    /// <inheritdoc/>
    public override IDragOperation? BeginDrag(DragStart start)
    {
        if (start.Target is not NodeEditor { Graph: { } graph } editor) return null;
        if (editor.NodeAt(editor.PointToClient(start.ScreenPosition)) is not { } node) return null;

        if (!node.IsSelected) editor.SelectOnly(node);
        var nodes = graph.SelectedNodes.ToList();
        return new Operation(editor, graph, nodes, start.ScreenPosition);
    }

    private sealed class Operation : IDragOperation
    {
        private readonly NodeEditor _editor;
        private readonly NodeGraphViewModel _graph;
        private readonly List<(NodeViewModel Node, Point From)> _nodes;
        private readonly Point _start;
        private readonly IDisposable _suspended;

        public Operation(NodeEditor editor, NodeGraphViewModel graph, List<NodeViewModel> nodes, Point start)
        {
            _editor = editor;
            _graph = graph;
            _nodes = nodes.Select(n => (n, n.Position)).ToList();
            _start = start;
            _suspended = graph.Undo.Suspend(); // the whole move becomes one step at the end
        }

        public void Update(Point screenPosition, ModifierKeys modifiers)
        {
            var delta = (screenPosition - _start) / _editor.Zoom;
            bool snap = _editor.SnapToGrid ^ modifiers.HasFlag(ModifierKeys.Control);
            foreach (var (node, from) in _nodes) node.Position = _editor.Snap(from + delta, snap);
        }

        public void Complete(Point screenPosition, ModifierKeys modifiers)
        {
            Update(screenPosition, modifiers);
            _suspended.Dispose();
            var moves = _nodes.Select(n => (n.Node, n.From, To: n.Node.Position)).Where(m => m.From != m.To).ToList();
            if (moves.Count == 0) return;
            _graph.Undo.Push(new DelegateUndoAction(moves.Count == 1 ? $"Move {moves[0].Node.Title}" : "Move",
                () => { foreach (var m in moves) m.Node.Position = m.From; },
                () => { foreach (var m in moves) m.Node.Position = m.To; }));
        }

        public void Cancel()
        {
            foreach (var (node, from) in _nodes) node.Position = from;
            _suspended.Dispose();
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
    /// <summary>Gets how the box changes the selection.</summary>
    public BoxSelectMode Mode { get; } = mode;

    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is NodeEditor { Graph: not null };

    /// <inheritdoc/>
    public override IDragOperation? BeginDrag(DragStart start)
    {
        if (start.Target is not NodeEditor { Graph: { } graph } editor) return null;
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
