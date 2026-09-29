using System.Collections.ObjectModel;
using Atelier.Core.Primitives;

namespace Atelier.Nodes;

/// <summary>Whether an output can be linked to an input, or why not.</summary>
public enum ConnectResult
{
    /// <summary>The link can be made.</summary>
    Ok,

    /// <summary>Both sockets are on the same node.</summary>
    SameNode,

    /// <summary>The input's type doesn't accept the output's.</summary>
    IncompatibleTypes,

    /// <summary>The input's node feeds the output's node, so the link would make a loop.</summary>
    WouldCreateCycle,

    /// <summary>The link already exists.</summary>
    AlreadyConnected,
}

/// <summary>
/// A node graph: nodes, the links between their sockets, and the undo history of its edits. Change the graph
/// through its methods; each records itself for undo.
/// </summary>
public class NodeGraphViewModel : NodeGraphObject
{
    private readonly ObservableCollection<NodeViewModel> _nodes = [];
    private readonly ObservableCollection<LinkViewModel> _links = [];

    /// <summary>Initializes an empty graph.</summary>
    public NodeGraphViewModel()
    {
        Nodes = new ReadOnlyObservableCollection<NodeViewModel>(_nodes);
        Links = new ReadOnlyObservableCollection<LinkViewModel>(_links);
    }

    /// <summary>Gets the nodes, back to front.</summary>
    public ReadOnlyObservableCollection<NodeViewModel> Nodes { get; }

    /// <summary>Gets the links.</summary>
    public ReadOnlyObservableCollection<LinkViewModel> Links { get; }

    /// <summary>Gets the undo history.</summary>
    public UndoStack Undo { get; } = new();

    /// <summary>Gets or sets the kinds of nodes users can add.</summary>
    public NodeCatalog Catalog { get; set; } = new();

    /// <summary>Gets the selected nodes.</summary>
    public IEnumerable<NodeViewModel> SelectedNodes => _nodes.Where(n => n.IsSelected);

    /// <summary>Removes all nodes and links and forgets the undo history (for example before loading another graph).</summary>
    public void Clear()
    {
        foreach (var link in _links.ToList()) RemoveLink(link);
        foreach (var node in _nodes.ToList()) RemoveNodeFromList(node);
        Undo.Clear();
    }

    /// <summary>Gets the selected links.</summary>
    public IEnumerable<LinkViewModel> SelectedLinks => _links.Where(l => l.IsSelected);

    /// <summary>Deselects all nodes and links.</summary>
    public void ClearSelection()
    {
        foreach (var node in _nodes) node.IsSelected = false;
        foreach (var link in _links) link.IsSelected = false;
    }

    /// <summary>Selects all nodes (and no links).</summary>
    public void SelectAll()
    {
        foreach (var node in _nodes) node.IsSelected = true;
        foreach (var link in _links) link.IsSelected = false;
    }

    /// <summary>Moves <paramref name="node"/> in front of the others (to the end of <see cref="Nodes"/>); not recorded for undo.</summary>
    public void BringToFront(NodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        int index = _nodes.IndexOf(node);
        if (index >= 0 && index < _nodes.Count - 1) _nodes.Move(index, _nodes.Count - 1);
    }

    /// <summary>Removes the selected nodes (with their links) and the selected links, as one undo step.</summary>
    /// <returns><c>false</c> if nothing was selected.</returns>
    public bool DeleteSelection()
    {
        var nodes = SelectedNodes.ToList();
        var links = SelectedLinks.Where(l => !l.From.Node!.IsSelected && !l.To.Node!.IsSelected).ToList();
        if (nodes.Count == 0 && links.Count == 0) return false;
        using (Undo.Group(nodes.Count == 1 && links.Count == 0 ? $"Delete {nodes[0].Title}" : "Delete"))
        {
            foreach (var link in links) Disconnect(link);
            foreach (var node in nodes) RemoveNodeRecorded(node);
        }
        return true;
    }

    /// <summary>
    /// Adds copies of <paramref name="nodes"/> (see <see cref="NodeViewModel.Copy"/>) moved by <paramref name="offset"/>,
    /// as one undo step: links between the copied nodes are copied between the copies, and links into them from other
    /// nodes feed the copies too, like Blender's Shift+D.
    /// </summary>
    /// <returns>The copies, in the order of <paramref name="nodes"/>.</returns>
    public IReadOnlyList<NodeViewModel> Duplicate(IEnumerable<NodeViewModel> nodes, Point offset)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var originals = nodes.Where(n => n.Graph == this).Distinct().ToList();
        var copies = new Dictionary<NodeViewModel, NodeViewModel>();
        if (originals.Count == 0) return [];

        using (Undo.Group(originals.Count == 1 ? $"Duplicate {originals[0].Title}" : "Duplicate"))
        {
            foreach (var node in originals)
            {
                var copy = node.Copy();
                copy.Position = node.Position + offset;
                copies[node] = copy;
                AddNode(copy);
            }
            foreach (var node in originals)
            {
                var copy = copies[node];
                for (int i = 0; i < node.Inputs.Count && i < copy.Inputs.Count; i++)
                {
                    if (node.Inputs[i].Link is not { } link) continue;
                    var from = link.From;
                    if (copies.TryGetValue(from.Node!, out var fromCopy))
                    {
                        int output = from.Node!.Outputs.IndexOf(from);
                        if (output >= fromCopy.Outputs.Count) continue;
                        from = fromCopy.Outputs[output];
                    }
                    Connect(from, copy.Inputs[i]);
                }
            }
        }
        return originals.Select(n => copies[n]).ToList();
    }

    /// <summary>Adds <paramref name="node"/> on top of the others.</summary>
    /// <returns>The node.</returns>
    public T AddNode<T>(T node) where T : NodeViewModel
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Graph != null) throw new InvalidOperationException($"The node '{node}' is already in a graph.");
        if (node.HasLinks) throw new InvalidOperationException($"The node '{node}' has links from another graph.");
        int index = _nodes.Count;
        InsertNode(node, index);
        Undo.Push(new DelegateUndoAction($"Add {node.Title}", () => RemoveNodeFromList(node), () => InsertNode(node, index)));
        return node;
    }

    /// <summary>Creates a node of the catalog's type <paramref name="typeId"/> at <paramref name="position"/> and adds it.</summary>
    /// <exception cref="KeyNotFoundException">The catalog has no such type.</exception>
    public NodeViewModel AddNode(string typeId, Point position) => AddNode(Catalog.CreateNode(typeId, position));

    /// <summary>Removes <paramref name="node"/> and its links.</summary>
    /// <returns><c>false</c> if the node isn't in the graph.</returns>
    public bool RemoveNode(NodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Graph != this) return false;
        using (Undo.Group($"Delete {node.Title}"))
        {
            RemoveNodeRecorded(node);
        }
        return true;
    }

    /// <summary>Removes <paramref name="nodes"/> and their links, as one undo step.</summary>
    public void RemoveNodes(IEnumerable<NodeViewModel> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        using (Undo.Group("Delete"))
        {
            foreach (var node in nodes.ToList())
            {
                if (node.Graph == this) RemoveNodeRecorded(node);
            }
        }
    }

    /// <summary>Checks whether <paramref name="from"/> can be linked to <paramref name="to"/>.</summary>
    /// <exception cref="ArgumentException">A socket isn't on a node of this graph.</exception>
    public ConnectResult CanConnect(OutputSocketViewModel from, InputSocketViewModel to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (from.Node?.Graph != this) throw new ArgumentException($"The output '{from}' isn't on a node of this graph.", nameof(from));
        if (to.Node?.Graph != this) throw new ArgumentException($"The input '{to}' isn't on a node of this graph.", nameof(to));
        if (from.Node == to.Node) return ConnectResult.SameNode;
        if (to.Link?.From == from) return ConnectResult.AlreadyConnected;
        if (!to.Type.CanConnectFrom(from.Type)) return ConnectResult.IncompatibleTypes;
        if (Reaches(to.Node, from.Node)) return ConnectResult.WouldCreateCycle;
        return ConnectResult.Ok;
    }

    /// <summary>Links <paramref name="from"/> to <paramref name="to"/>, replacing the input's link if it has one.</summary>
    /// <returns>The new link, or <c>null</c> if <see cref="CanConnect"/> doesn't allow it.</returns>
    public LinkViewModel? Connect(OutputSocketViewModel from, InputSocketViewModel to)
    {
        if (CanConnect(from, to) != ConnectResult.Ok) return null;
        using (Undo.Group("Connect"))
        {
            if (to.Link is { } old) Disconnect(old);
            var link = new LinkViewModel(from, to);
            int index = _links.Count;
            InsertLink(link, index);
            Undo.Push(new DelegateUndoAction("Connect", () => RemoveLink(link), () => InsertLink(link, index)));
            return link;
        }
    }

    /// <summary>Removes <paramref name="link"/>.</summary>
    /// <returns><c>false</c> if the link isn't in the graph.</returns>
    public bool Disconnect(LinkViewModel link)
    {
        ArgumentNullException.ThrowIfNull(link);
        int index = _links.IndexOf(link);
        if (index < 0) return false;
        RemoveLink(link);
        Undo.Push(new DelegateUndoAction("Disconnect", () => InsertLink(link, index), () => RemoveLink(link)));
        return true;
    }

    /// <summary>Removes all links of <paramref name="socket"/>.</summary>
    public void Disconnect(SocketViewModel socket)
    {
        ArgumentNullException.ThrowIfNull(socket);
        using (Undo.Group("Disconnect"))
        {
            foreach (var link in LinksOf(socket).ToList()) Disconnect(link);
        }
    }

    /// <summary>
    /// Puts <paramref name="node"/> into <paramref name="link"/>, like dropping a node onto a link in Blender: the
    /// link's output feeds the node's first input that accepts it, and the node's first output the link's input
    /// accepts feeds that input.
    /// </summary>
    /// <returns><c>false</c> (and nothing changes) if the node has links already or no fitting input or output.</returns>
    public bool InsertIntoLink(NodeViewModel node, LinkViewModel link)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(link);
        if (node.Graph != this || !_links.Contains(link) || node.HasLinks) return false;
        var input = node.Inputs.FirstOrDefault(i => i.Type.CanConnectFrom(link.From.Type));
        var output = node.Outputs.FirstOrDefault(o => link.To.Type.CanConnectFrom(o.Type));
        if (input is null || output is null) return false;

        using (Undo.Group($"Insert {node.Title}"))
        {
            var (from, to) = (link.From, link.To);
            Disconnect(link);
            Connect(from, input);
            Connect(output, to);
        }
        return true;
    }

    /// <summary>Puts a reroute point centered at <paramref name="center"/> into <paramref name="link"/>, as one undo step.</summary>
    /// <returns>The reroute point, or <c>null</c> if the link isn't in the graph.</returns>
    public RerouteNodeViewModel? InsertReroute(LinkViewModel link, Point center)
    {
        ArgumentNullException.ThrowIfNull(link);
        if (!_links.Contains(link)) return null;
        var reroute = new RerouteNodeViewModel(link.From.Type) { Center = center };
        using (Undo.Group("Add reroute"))
        {
            AddNode(reroute);
            var (from, to) = (link.From, link.To);
            Disconnect(link);
            Connect(from, reroute.Inputs[0]);
            Connect(reroute.Outputs[0], to);
        }
        return reroute;
    }

    /// <summary>Gets whether <see cref="InsertIntoLink"/> would put <paramref name="node"/> into <paramref name="link"/>.</summary>
    public bool CanInsertIntoLink(NodeViewModel node, LinkViewModel link)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(link);
        return node.Graph == this && _links.Contains(link) && !node.HasLinks
            && node.Inputs.Any(i => i.Type.CanConnectFrom(link.From.Type))
            && node.Outputs.Any(o => link.To.Type.CanConnectFrom(o.Type));
    }

    /// <summary>
    /// Takes <paramref name="node"/> out of its links, like Blender's detach: each input its outputs fed is fed by what
    /// fed the node instead (the first of its inputs' links with a type that input accepts), and all of the node's
    /// links are removed. One undo step.
    /// </summary>
    /// <returns><c>false</c> if the node had no links.</returns>
    public bool Detach(NodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (node.Graph != this || !node.HasLinks) return false;

        var feeding = node.Inputs.Where(i => i.Link != null).Select(i => i.Link!.From).ToList();
        var fed = node.Outputs.SelectMany(o => o.Links).Select(l => l.To).ToList();
        using (Undo.Group($"Detach {node.Title}"))
        {
            foreach (var link in LinksOf(node).ToList()) Disconnect(link);
            foreach (var input in fed)
            {
                if (feeding.FirstOrDefault(output => input.Type.CanConnectFrom(output.Type)) is { } output) Connect(output, input);
            }
        }
        return true;
    }

    /// <summary>Gets <paramref name="node"/>'s links: those of its inputs, then those of its outputs.</summary>
    public IEnumerable<LinkViewModel> LinksOf(NodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        foreach (var input in node.Inputs)
        {
            if (input.Link is { } link) yield return link;
        }
        foreach (var output in node.Outputs)
        {
            foreach (var link in output.Links) yield return link;
        }
    }

    /// <summary>Gets <paramref name="socket"/>'s links.</summary>
    public static IEnumerable<LinkViewModel> LinksOf(SocketViewModel socket) => socket switch
    {
        InputSocketViewModel { Link: { } link } => [link],
        OutputSocketViewModel output => output.Links,
        _ => [],
    };

    /// <summary>Gets the nodes in an order where each comes after all nodes that feed it; unrelated nodes keep their order in <see cref="Nodes"/>.</summary>
    public IReadOnlyList<NodeViewModel> GetTopologicalOrder()
    {
        var pending = _nodes.ToDictionary(n => n, n => n.Inputs.Count(i => i.IsConnected));
        var order = new List<NodeViewModel>(_nodes.Count);
        var ready = new SortedSet<int>(_nodes.Select((n, i) => (n, i)).Where(x => pending[x.n] == 0).Select(x => x.i));
        var indices = _nodes.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i);
        while (ready.Count > 0)
        {
            var node = _nodes[ready.Min];
            ready.Remove(ready.Min);
            order.Add(node);
            foreach (var output in node.Outputs)
            {
                foreach (var link in output.Links)
                {
                    var next = link.To.Node!;
                    if (--pending[next] == 0) ready.Add(indices[next]);
                }
            }
        }
        return order;
    }

    /// <summary>Gets the nodes that feed <paramref name="node"/>, directly or through others.</summary>
    public IReadOnlyCollection<NodeViewModel> GetUpstream(NodeViewModel node) =>
        Collect(node, n => n.Inputs.Where(i => i.Link != null).Select(i => i.Link!.From.Node!));

    /// <summary>Gets the nodes <paramref name="node"/> feeds, directly or through others.</summary>
    public IReadOnlyCollection<NodeViewModel> GetDownstream(NodeViewModel node) =>
        Collect(node, n => n.Outputs.SelectMany(o => o.Links).Select(l => l.To.Node!));

    /// <summary>Gets the nodes whose outputs feed nothing (the graph's results, like Blender's output nodes).</summary>
    public IReadOnlyList<NodeViewModel> GetSinks() => _nodes.Where(n => !n.Outputs.Any(o => o.IsConnected)).ToList();

    private static HashSet<NodeViewModel> Collect(NodeViewModel start, Func<NodeViewModel, IEnumerable<NodeViewModel>> next)
    {
        ArgumentNullException.ThrowIfNull(start);
        var found = new HashSet<NodeViewModel>();
        var stack = new Stack<NodeViewModel>(next(start));
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (!found.Add(node)) continue;
            foreach (var n in next(node)) stack.Push(n);
        }
        found.Remove(start);
        return found;
    }

    // Whether a path of links leads from start to target (or they are the same node).
    private static bool Reaches(NodeViewModel start, NodeViewModel target)
    {
        var visited = new HashSet<NodeViewModel>();
        var stack = new Stack<NodeViewModel>();
        stack.Push(start);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node == target) return true;
            if (!visited.Add(node)) continue;
            foreach (var output in node.Outputs)
            {
                foreach (var link in output.Links) stack.Push(link.To.Node!);
            }
        }
        return false;
    }

    private void RemoveNodeRecorded(NodeViewModel node)
    {
        foreach (var link in LinksOf(node).ToList()) Disconnect(link);
        int index = _nodes.IndexOf(node);
        RemoveNodeFromList(node);
        Undo.Push(new DelegateUndoAction($"Delete {node.Title}", () => InsertNode(node, index), () => RemoveNodeFromList(node)));
    }

    // Undo and redo insert at the recorded index where possible (the order can have changed since) and remove by reference.
    private void InsertNode(NodeViewModel node, int index)
    {
        node.Graph = this;
        _nodes.Insert(Math.Min(index, _nodes.Count), node);
    }

    private void RemoveNodeFromList(NodeViewModel node)
    {
        _nodes.Remove(node);
        node.Graph = null;
    }

    private void InsertLink(LinkViewModel link, int index)
    {
        link.To.Link = link;
        link.From.AddLink(link);
        _links.Insert(Math.Min(index, _links.Count), link);
    }

    private void RemoveLink(LinkViewModel link)
    {
        _links.Remove(link);
        link.From.RemoveLink(link);
        if (link.To.Link == link) link.To.Link = null;
    }
}
