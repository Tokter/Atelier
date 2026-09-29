using System.Collections.ObjectModel;
using Atelier.Core.Primitives;

namespace Atelier.Nodes;

/// <summary>
/// The reusable sub-graphs (node groups) of a root graph: each <see cref="NodeGroupDefinition"/> is a graph of its own
/// that <see cref="GroupNodeViewModel"/>s use, in the root graph or inside other groups. Changes are recorded on the
/// root graph's undo history.
/// </summary>
public sealed class NodeGroupLibrary
{
    private readonly ObservableCollection<NodeGroupDefinition> _definitions = [];

    internal NodeGroupLibrary(NodeGraphViewModel root)
    {
        Root = root;
        Definitions = new ReadOnlyObservableCollection<NodeGroupDefinition>(_definitions);
    }

    /// <summary>Gets the root graph the groups belong to.</summary>
    public NodeGraphViewModel Root { get; }

    /// <summary>Gets the group definitions, in the order they were added.</summary>
    public ReadOnlyObservableCollection<NodeGroupDefinition> Definitions { get; }

    /// <summary>Gets the root graph and the graphs inside all groups.</summary>
    public IEnumerable<NodeGraphViewModel> AllGraphs => _definitions.Select(d => d.Graph).Prepend(Root);

    /// <summary>Gets the definition with <paramref name="id"/>, or <c>null</c>.</summary>
    public NodeGroupDefinition? Find(string id) => _definitions.FirstOrDefault(d => d.Id == id);

    /// <summary>
    /// Adds an empty group (with its Group Input and Group Output nodes) named <paramref name="name"/>, made unique
    /// ("Group", "Group.001", ...). Undoable.
    /// </summary>
    public NodeGroupDefinition Add(string name, string? id = null)
    {
        var definition = new NodeGroupDefinition(this, id ?? Guid.NewGuid().ToString("N"), UniqueName(name));
        int index = _definitions.Count;
        _definitions.Add(definition);
        Root.Undo.Push(new DelegateUndoAction($"Add group {definition.Name}", () => _definitions.Remove(definition),
            () => _definitions.Insert(Math.Min(index, _definitions.Count), definition)));
        return definition;
    }

    /// <summary>Removes <paramref name="definition"/> if no group node uses it. Undoable.</summary>
    /// <returns><c>false</c> if it's in use or not in the library.</returns>
    public bool Remove(NodeGroupDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        int index = _definitions.IndexOf(definition);
        if (index < 0 || definition.Instances.Any()) return false;
        _definitions.RemoveAt(index);
        Root.Undo.Push(new DelegateUndoAction($"Delete group {definition.Name}",
            () => _definitions.Insert(Math.Min(index, _definitions.Count), definition), () => _definitions.Remove(definition)));
        return true;
    }

    /// <summary>
    /// Turns <paramref name="nodes"/> of <paramref name="graph"/> into a new group, like Blender's Ctrl+G: they move into
    /// the group, the links that crossed into or out of them become the group's inputs and outputs, and a group node
    /// connected like they were takes their place. One undo step.
    /// </summary>
    /// <returns>The new group node, or <c>null</c> if no node could be grouped.</returns>
    public GroupNodeViewModel? Group(NodeGraphViewModel graph, IEnumerable<NodeViewModel> nodes, string name = "Group")
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(nodes);
        if (graph.Groups != this) throw new ArgumentException("The graph doesn't use this library.", nameof(graph));
        var selected = nodes.Where(n => n.Graph == graph && n.CanRemove).Distinct().ToList();
        if (selected.Count == 0) return null;

        var set = selected.ToHashSet();
        var incoming = graph.Links.Where(l => set.Contains(l.To.Node!) && !set.Contains(l.From.Node!)).ToList();
        var outgoing = graph.Links.Where(l => set.Contains(l.From.Node!) && !set.Contains(l.To.Node!)).ToList();
        var inside = graph.Links.Where(l => set.Contains(l.From.Node!) && set.Contains(l.To.Node!)).ToList();
        float left = selected.Min(n => n.Position.X), top = selected.Min(n => n.Position.Y);
        float right = selected.Max(n => n.Position.X + n.Width), bottom = selected.Max(n => n.Position.Y + 120);

        using var step = graph.Undo.Group($"Make group {name}");
        var definition = Add(name);
        step.Name = $"Make group {definition.Name}";

        // One group input per outside output feeding the nodes, one group output per node output leaving them.
        var inputFor = new Dictionary<OutputSocketViewModel, GroupSocket>();
        foreach (var link in incoming)
        {
            if (inputFor.ContainsKey(link.From)) continue;
            inputFor[link.From] = definition.AddInput(link.To.Name, link.To.Type, defaultValue: link.To.Value, minimum: link.To.Minimum, maximum: link.To.Maximum, editor: link.To.Editor);
        }
        var outputFor = new Dictionary<OutputSocketViewModel, GroupSocket>();
        foreach (var link in outgoing)
        {
            if (!outputFor.ContainsKey(link.From)) outputFor[link.From] = definition.AddOutput(link.From.Name, link.From.Type);
        }

        foreach (var link in incoming.Concat(outgoing).Concat(inside)) graph.Disconnect(link);
        foreach (var node in selected) graph.RemoveNode(node);

        // Inside: the nodes keep their layout, between the group's input and output nodes.
        const float margin = 60;
        var shift = new Point(NodeGroupDefinition.InterfaceNodeWidth + 2 * margin - left, -top);
        foreach (var node in selected)
        {
            definition.Graph.AddNode(node);
            node.Position += shift;
        }
        definition.InputNode.Position = new Point(margin, (bottom - top) / 2 - 40);
        definition.OutputNode.Position = new Point(right - left + NodeGroupDefinition.InterfaceNodeWidth + 3 * margin, (bottom - top) / 2 - 40);
        foreach (var link in inside) definition.Graph.Connect(link.From, link.To);
        foreach (var link in incoming) definition.Graph.Connect(definition.InputNode.OutputFor(inputFor[link.From])!, link.To);
        foreach (var (output, socket) in outputFor) definition.Graph.Connect(output, definition.OutputNode.InputFor(socket)!);

        var groupNode = graph.AddNode(new GroupNodeViewModel(definition)
        {
            Position = new Point((left + right) / 2 - 80, (top + bottom) / 2 - 60),
        });
        foreach (var link in incoming) graph.Connect(link.From, groupNode.InputFor(inputFor[link.From])!);
        foreach (var link in outgoing) graph.Connect(groupNode.OutputFor(outputFor[link.From])!, link.To);
        return groupNode;
    }

    /// <summary>
    /// Replaces <paramref name="groupNode"/> with copies of the nodes inside its group, connected like the group node was,
    /// like Blender's Ctrl+Alt+G. Unconnected group inputs pass the group node's own values on. One undo step; the group
    /// definition stays in the library.
    /// </summary>
    /// <returns>The copies.</returns>
    public IReadOnlyList<NodeViewModel> Ungroup(GroupNodeViewModel groupNode)
    {
        ArgumentNullException.ThrowIfNull(groupNode);
        if (groupNode.Graph is not { } graph) return [];
        var definition = groupNode.Definition;
        var inner = definition.Graph.Nodes.Where(n => n.CanRemove).ToList();

        // What fed the group node's inputs and what its outputs fed, by group socket.
        var sources = definition.Inputs.ToDictionary(s => s, s => groupNode.InputFor(s)!.Link?.From);
        var values = definition.Inputs.ToDictionary(s => s, s => groupNode.InputFor(s)!.Value);
        var targets = definition.Outputs.ToDictionary(s => s, s => groupNode.OutputFor(s)!.Links.Select(l => l.To).ToList());
        var innerCenter = inner.Count > 0 ? new Point(inner.Average(n => n.Position.X), inner.Average(n => n.Position.Y)) : default;

        using var step = graph.Undo.Group($"Ungroup {definition.Name}");
        graph.RemoveNode(groupNode);
        var copies = new Dictionary<NodeViewModel, NodeViewModel>();
        foreach (var node in inner)
        {
            var copy = node.Copy();
            copy.Position = node.Position - innerCenter + groupNode.Position;
            copies[node] = copy;
            graph.AddNode(copy);
        }

        OutputSocketViewModel? Outside(OutputSocketViewModel output) =>
            output.Node is GroupInputNodeViewModel input ? sources[input.GroupSocketOf(output)!] : Copied(output);
        OutputSocketViewModel? Copied(OutputSocketViewModel output) =>
            copies.TryGetValue(output.Node!, out var copy) ? copy.Outputs[output.Node!.Outputs.IndexOf(output)] : null;

        foreach (var link in definition.Graph.Links)
        {
            if (link.To.Node is GroupOutputNodeViewModel outputNode)
            {
                var from = Outside(link.From);
                if (from == null) continue;
                foreach (var target in targets[outputNode.GroupSocketOf(link.To)!]) graph.Connect(from, target);
                continue;
            }
            if (!copies.TryGetValue(link.To.Node!, out var toCopy)) continue;
            var to = toCopy.Inputs[link.To.Node!.Inputs.IndexOf(link.To)];
            if (Outside(link.From) is { } source) graph.Connect(source, to);
            else if (link.From.Node is GroupInputNodeViewModel inputNode && values[inputNode.GroupSocketOf(link.From)!] is { } value
                && to.Type.CanConnectFrom(link.From.Type))
            {
                to.Value = to.Type.ConvertFrom(value, link.From.Type);
            }
        }
        graph.ClearSelection();
        foreach (var copy in copies.Values) copy.IsSelected = true;
        return copies.Values.ToList();
    }

    internal void ClearAll() => _definitions.Clear();

    // Adds a definition while loading (not recorded; loading clears the history).
    internal NodeGroupDefinition AddLoaded(string id, string name)
    {
        var definition = new NodeGroupDefinition(this, id, name);
        _definitions.Add(definition);
        return definition;
    }

    private string UniqueName(string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Group" : name.Trim();
        if (_definitions.All(d => d.Name != name)) return name;
        for (int i = 1; ; i++)
        {
            string candidate = $"{name}.{i:000}";
            if (_definitions.All(d => d.Name != candidate)) return candidate;
        }
    }
}

/// <summary>
/// A socket of a group's interface: an input of the group (an output of its Group Input node, an input of its group
/// nodes) or an output (an input of its Group Output node, an output of its group nodes).
/// </summary>
public sealed class GroupSocket : NodeGraphObject
{
    private string _name;

    internal GroupSocket(NodeGroupDefinition definition, string id, string name, SocketType type, bool isInput)
    {
        Definition = definition;
        Id = id;
        _name = name;
        Type = type;
        IsInput = isInput;
    }

    /// <summary>Gets the group the socket belongs to.</summary>
    public NodeGroupDefinition Definition { get; }

    /// <summary>Gets the socket's identifier, which saved graphs refer to it by.</summary>
    public string Id { get; }

    /// <summary>Gets or sets the socket's name, shown on the group's nodes; undoable.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetUndoableProperty(ref _name, value ?? string.Empty, Definition.Library.Root.Undo, "Rename socket", v => Name = v)) Definition.OnInterfaceChanged();
        }
    }

    /// <summary>Gets the socket's type.</summary>
    public SocketType Type { get; }

    /// <summary>Gets whether the socket is an input of the group (rather than an output).</summary>
    public bool IsInput { get; }

    /// <summary>Gets or sets the value a new group node's input starts with.</summary>
    public object? DefaultValue { get; set; }

    /// <summary>Gets or sets the smallest value the input's control offers on group nodes, or <c>null</c>.</summary>
    public double? Minimum { get; set; }

    /// <summary>Gets or sets the largest value the input's control offers on group nodes, or <c>null</c>.</summary>
    public double? Maximum { get; set; }

    /// <summary>Gets or sets the control that edits the input's value on group nodes.</summary>
    public InputEditor Editor { get; set; }

    /// <inheritdoc/>
    public override string ToString() => $"{Definition.Name}.{Name}";
}

/// <summary>
/// A reusable sub-graph: a <see cref="Graph"/> of its own with a Group Input node (whose outputs are the group's
/// inputs) and a Group Output node (whose inputs are its outputs). <see cref="GroupNodeViewModel"/>s use it; changing
/// it changes all of them.
/// </summary>
public sealed class NodeGroupDefinition : NodeGraphObject
{
    /// <summary>The width of the Group Input and Group Output nodes.</summary>
    public const float InterfaceNodeWidth = 140;

    private readonly ObservableCollection<GroupSocket> _inputs = [];
    private readonly ObservableCollection<GroupSocket> _outputs = [];
    private string _name;
    private bool _isEvaluating;

    internal NodeGroupDefinition(NodeGroupLibrary library, string id, string name)
    {
        Library = library;
        Id = id;
        _name = name;
        Inputs = new ReadOnlyObservableCollection<GroupSocket>(_inputs);
        Outputs = new ReadOnlyObservableCollection<GroupSocket>(_outputs);
        Graph = new NodeGraphViewModel(this);
        InputNode = new GroupInputNodeViewModel(this) { Position = new Point(0, 0) };
        OutputNode = new GroupOutputNodeViewModel(this) { Position = new Point(400, 0) };
        using (library.Root.Undo.Suspend())
        {
            Graph.AddNode(InputNode);
            Graph.AddNode(OutputNode);
        }
        Graph.ContentChanged += (_, _) => ContentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Gets the library the group is in.</summary>
    public NodeGroupLibrary Library { get; }

    /// <summary>Gets the group's identifier, which saved graphs refer to it by.</summary>
    public string Id { get; }

    /// <summary>Gets or sets the group's name, the title of its group nodes; undoable.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetUndoableProperty(ref _name, value ?? string.Empty, Library.Root.Undo, "Rename group", v => Name = v)) OnInterfaceChanged();
        }
    }

    /// <summary>Gets the graph inside the group.</summary>
    public NodeGraphViewModel Graph { get; }

    /// <summary>Gets the node inside the group whose outputs are the group's inputs.</summary>
    public GroupInputNodeViewModel InputNode { get; }

    /// <summary>Gets the node inside the group whose inputs are the group's outputs.</summary>
    public GroupOutputNodeViewModel OutputNode { get; }

    /// <summary>Gets the group's inputs.</summary>
    public ReadOnlyObservableCollection<GroupSocket> Inputs { get; }

    /// <summary>Gets the group's outputs.</summary>
    public ReadOnlyObservableCollection<GroupSocket> Outputs { get; }

    /// <summary>Gets the group nodes that use the group, in all graphs of the library.</summary>
    public IEnumerable<GroupNodeViewModel> Instances =>
        Library.AllGraphs.SelectMany(g => g.Nodes).OfType<GroupNodeViewModel>().Where(n => n.Definition == this);

    /// <summary>
    /// Gets or sets the group node whose values the nodes inside show (the one the editor entered the group through);
    /// <c>null</c> shows those of the group node computed last.
    /// </summary>
    public GroupNodeViewModel? InspectedInstance { get; set; }

    /// <summary>Occurs when the graph inside the group changes; its group nodes compute again.</summary>
    public event EventHandler? ContentChanged;

    /// <summary>Occurs when the group's name or its inputs and outputs change; its nodes update their sockets.</summary>
    public event EventHandler? InterfaceChanged;

    /// <summary>Gets whether the group is <paramref name="other"/> or uses it inside, directly or through other groups.</summary>
    public bool Contains(NodeGroupDefinition other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return other == this || Graph.Nodes.OfType<GroupNodeViewModel>().Any(n => n.Definition.Contains(other));
    }

    /// <summary>Adds an input to the group (a unique name is made from <paramref name="name"/>). Undoable.</summary>
    public GroupSocket AddInput(string name, SocketType type, int? index = null, object? defaultValue = null, double? minimum = null, double? maximum = null, InputEditor editor = InputEditor.Auto) =>
        AddSocket(_inputs, isInput: true, name, type, index, defaultValue, minimum, maximum, editor);

    /// <summary>Adds an output to the group (a unique name is made from <paramref name="name"/>). Undoable.</summary>
    public GroupSocket AddOutput(string name, SocketType type, int? index = null) =>
        AddSocket(_outputs, isInput: false, name, type, index, null, null, null, InputEditor.Auto);

    /// <summary>Removes <paramref name="socket"/> from the group; the links of its sockets on the group's nodes are removed. One undo step.</summary>
    public void RemoveSocket(GroupSocket socket)
    {
        ArgumentNullException.ThrowIfNull(socket);
        var list = socket.IsInput ? _inputs : _outputs;
        int index = list.IndexOf(socket);
        if (index < 0) return;
        using (Library.Root.Undo.Group($"Remove {(socket.IsInput ? "input" : "output")} {socket.Name}"))
        {
            foreach (var mirrored in MirroredSockets(socket).ToList())
            {
                if (mirrored.IsConnected) mirrored.Node!.Graph!.Disconnect(mirrored);
            }
            list.RemoveAt(index);
            OnInterfaceChanged();
            Library.Root.Undo.Push(new DelegateUndoAction("Remove socket",
                () => { list.Insert(Math.Min(index, list.Count), socket); OnInterfaceChanged(); },
                () => { list.Remove(socket); OnInterfaceChanged(); }));
        }
    }

    /// <summary>Moves <paramref name="socket"/> to position <paramref name="index"/> among the group's inputs or outputs. Undoable.</summary>
    public void MoveSocket(GroupSocket socket, int index)
    {
        ArgumentNullException.ThrowIfNull(socket);
        var list = socket.IsInput ? _inputs : _outputs;
        int from = list.IndexOf(socket);
        index = Math.Clamp(index, 0, list.Count - 1);
        if (from < 0 || from == index) return;
        list.Move(from, index);
        OnInterfaceChanged();
        Library.Root.Undo.Push(new DelegateUndoAction($"Move {socket.Name}",
            () => { list.Move(list.IndexOf(socket), from); OnInterfaceChanged(); },
            () => { list.Move(list.IndexOf(socket), index); OnInterfaceChanged(); }));
    }

    /// <summary>
    /// Computes the graph inside the group for <paramref name="inputs"/> (one value per input) and returns its outputs'
    /// values (one per output).
    /// </summary>
    /// <exception cref="InvalidOperationException">A node inside failed (the message names it).</exception>
    public object?[] Evaluate(IReadOnlyList<object?> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        if (_isEvaluating) throw new InvalidOperationException($"The group '{Name}' is being computed already.");
        _isEvaluating = true;
        try
        {
            for (int i = 0; i < _inputs.Count; i++)
            {
                if (InputNode.OutputFor(_inputs[i]) is { } output) output.Value = i < inputs.Count ? inputs[i] : _inputs[i].DefaultValue;
            }
            GraphEvaluator.EvaluateAll(Graph);
            if (Graph.Nodes.FirstOrDefault(n => n.Error != null) is { } failed)
            {
                throw new InvalidOperationException($"{failed.Title}: {failed.Error}");
            }
            return _outputs.Select(s => OutputNode.InputFor(s)?.EffectiveValue).ToArray();
        }
        finally
        {
            _isEvaluating = false;
        }
    }

    /// <inheritdoc/>
    public override string ToString() => Name;

    internal void OnInterfaceChanged() => InterfaceChanged?.Invoke(this, EventArgs.Empty);

    // Adds a socket while loading (not recorded).
    internal GroupSocket AddLoadedSocket(string id, string name, SocketType type, bool isInput)
    {
        var socket = new GroupSocket(this, id, name, type, isInput);
        (isInput ? _inputs : _outputs).Add(socket);
        OnInterfaceChanged();
        return socket;
    }

    private GroupSocket AddSocket(ObservableCollection<GroupSocket> list, bool isInput, string name, SocketType type, int? index,
        object? defaultValue, double? minimum, double? maximum, InputEditor editor)
    {
        ArgumentNullException.ThrowIfNull(type);
        var socket = new GroupSocket(this, Guid.NewGuid().ToString("N"), UniqueSocketName(list, name), type, isInput)
        {
            DefaultValue = defaultValue ?? type.DefaultValue,
            Minimum = minimum,
            Maximum = maximum,
            Editor = editor,
        };
        int at = Math.Clamp(index ?? list.Count, 0, list.Count);
        list.Insert(at, socket);
        OnInterfaceChanged();
        Library.Root.Undo.Push(new DelegateUndoAction($"Add {(isInput ? "input" : "output")} {socket.Name}",
            () => { list.Remove(socket); OnInterfaceChanged(); },
            () => { list.Insert(Math.Min(at, list.Count), socket); OnInterfaceChanged(); }));
        return socket;
    }

    private IEnumerable<SocketViewModel> MirroredSockets(GroupSocket socket)
    {
        var nodes = Instances.Cast<GroupInterfaceNodeViewModel>().Append(InputNode).Append(OutputNode);
        foreach (var node in nodes)
        {
            if (node.SocketFor(socket) is { } mirrored) yield return mirrored;
        }
    }

    private static string UniqueSocketName(IEnumerable<GroupSocket> sockets, string name)
    {
        name = string.IsNullOrWhiteSpace(name) ? "Value" : name.Trim();
        var names = sockets.Select(s => s.Name).ToHashSet();
        if (!names.Contains(name)) return name;
        for (int i = 1; ; i++)
        {
            if (!names.Contains($"{name} {i}")) return $"{name} {i}";
        }
    }
}

/// <summary>
/// The base of the nodes whose sockets follow a group's interface: group nodes and the Group Input and Group Output
/// nodes inside the group. Their sockets are kept (with their links) while the interface changes around them.
/// </summary>
public abstract class GroupInterfaceNodeViewModel : ComputingNodeViewModel
{
    private readonly Dictionary<GroupSocket, SocketViewModel> _sockets = [];
    private readonly bool _inputsFollowGroupInputs;
    private readonly bool _outputsFollowGroupOutputs;
    private readonly bool _outputsFollowGroupInputs;
    private readonly bool _inputsFollowGroupOutputs;

    private protected GroupInterfaceNodeViewModel(NodeGroupDefinition definition, string title, bool inputsFollowGroupInputs,
        bool outputsFollowGroupOutputs, bool outputsFollowGroupInputs, bool inputsFollowGroupOutputs) : base(title)
    {
        Definition = definition;
        _inputsFollowGroupInputs = inputsFollowGroupInputs;
        _outputsFollowGroupOutputs = outputsFollowGroupOutputs;
        _outputsFollowGroupInputs = outputsFollowGroupInputs;
        _inputsFollowGroupOutputs = inputsFollowGroupOutputs;
        SyncSockets();
    }

    /// <summary>Gets the group whose interface the node follows.</summary>
    public NodeGroupDefinition Definition { get; }

    /// <summary>Gets the node's socket for <paramref name="socket"/> of the group's interface, or <c>null</c>.</summary>
    public SocketViewModel? SocketFor(GroupSocket socket) => _sockets.TryGetValue(socket, out var s) && s.Node == this ? s : null;

    /// <summary>Gets the node's input for <paramref name="socket"/>, or <c>null</c>.</summary>
    public InputSocketViewModel? InputFor(GroupSocket socket) => SocketFor(socket) as InputSocketViewModel;

    /// <summary>Gets the node's output for <paramref name="socket"/>, or <c>null</c>.</summary>
    public OutputSocketViewModel? OutputFor(GroupSocket socket) => SocketFor(socket) as OutputSocketViewModel;

    /// <summary>Gets the interface socket <paramref name="socket"/> of this node stands for, or <c>null</c>.</summary>
    public GroupSocket? GroupSocketOf(SocketViewModel socket) => _sockets.FirstOrDefault(p => p.Value == socket && socket.Node == this).Key;

    /// <inheritdoc/>
    protected override void OnGraphChanged(NodeGraphViewModel? oldGraph, NodeGraphViewModel? newGraph)
    {
        base.OnGraphChanged(oldGraph, newGraph);
        if (oldGraph != null) Definition.InterfaceChanged -= OnInterfaceChanged;
        if (newGraph != null)
        {
            Definition.InterfaceChanged += OnInterfaceChanged;
            SyncSockets();
        }
    }

    private void OnInterfaceChanged(object? sender, EventArgs e) => SyncSockets();

    // Makes the sockets match the interface: the same socket objects for the same interface sockets, so links survive.
    private protected virtual void SyncSockets()
    {
        if (_inputsFollowGroupInputs) Sync(Definition.Inputs, input: true);
        if (_inputsFollowGroupOutputs) Sync(Definition.Outputs, input: true);
        if (_outputsFollowGroupOutputs) Sync(Definition.Outputs, input: false);
        if (_outputsFollowGroupInputs) Sync(Definition.Inputs, input: false);
    }

    private void Sync(IReadOnlyList<GroupSocket> interfaceSockets, bool input)
    {
        var wanted = interfaceSockets.ToHashSet();
        foreach (var (groupSocket, socket) in _sockets.ToList())
        {
            if (wanted.Contains(groupSocket) || socket.Node != this || (socket is InputSocketViewModel) != input) continue;
            if (socket.IsConnected) Graph?.Disconnect(socket);
            if (!socket.IsConnected) RemoveSocket(socket);
        }
        for (int i = 0; i < interfaceSockets.Count; i++)
        {
            var groupSocket = interfaceSockets[i];
            if (!_sockets.TryGetValue(groupSocket, out var socket))
            {
                socket = input
                    ? new InputSocketViewModel(groupSocket.Name, groupSocket.Type, groupSocket.DefaultValue)
                    {
                        Minimum = groupSocket.Minimum,
                        Maximum = groupSocket.Maximum,
                        Editor = this is GroupOutputNodeViewModel ? InputEditor.None : groupSocket.Editor,
                    }
                    : new OutputSocketViewModel(groupSocket.Name, groupSocket.Type);
                _sockets[groupSocket] = socket;
            }
            socket.Name = groupSocket.Name;
            if (socket.Node == null)
            {
                if (socket is InputSocketViewModel inputSocket) InsertInput(i, inputSocket);
                else InsertOutput(i, (OutputSocketViewModel)socket);
            }
            else if ((input ? Inputs.IndexOf((InputSocketViewModel)socket) : Outputs.IndexOf((OutputSocketViewModel)socket)) != i)
            {
                MoveSocket(socket, i);
            }
        }
    }
}

/// <summary>
/// A node that uses a <see cref="NodeGroupDefinition"/>: its inputs and outputs are the group's, and it computes the
/// graph inside the group with its own input values. Its title is the group's name.
/// </summary>
public sealed class GroupNodeViewModel : GroupInterfaceNodeViewModel
{
    private object?[]? _lastInputs;

    /// <summary>Initializes a node that uses <paramref name="definition"/>.</summary>
    public GroupNodeViewModel(NodeGroupDefinition definition)
        : base(definition ?? throw new ArgumentNullException(nameof(definition)), definition.Name,
            inputsFollowGroupInputs: true, outputsFollowGroupOutputs: true, outputsFollowGroupInputs: false, inputsFollowGroupOutputs: false)
    {
        HeaderColor = Color.FromRgb(0x2B, 0x65, 0x2B);
        Width = 160;
    }

    /// <inheritdoc/>
    protected internal override void Compute(ComputeContext context)
    {
        var inputs = Inputs.Select(i => context.Get(i)).ToArray();
        _lastInputs = inputs;
        var outputs = Definition.Evaluate(inputs);
        for (int i = 0; i < outputs.Length && i < Outputs.Count; i++) context.Set(Outputs[i], outputs[i]);

        // Leave the values of the entered group node showing inside the group.
        if (Definition.InspectedInstance is { Graph: not null } inspected && inspected != this && inspected._lastInputs is { } shown)
        {
            try
            {
                Definition.Evaluate(shown);
            }
            catch (InvalidOperationException)
            {
                // Its own error is shown on it.
            }
        }
    }

    /// <inheritdoc/>
    protected override NodeViewModel CreateCopy() => new GroupNodeViewModel(Definition);

    /// <inheritdoc/>
    protected override void OnGraphChanged(NodeGraphViewModel? oldGraph, NodeGraphViewModel? newGraph)
    {
        if (oldGraph != null)
        {
            Definition.ContentChanged -= OnDefinitionChanged;
            Definition.InterfaceChanged -= OnDefinitionRenamed;
        }
        base.OnGraphChanged(oldGraph, newGraph);
        if (newGraph != null)
        {
            if (newGraph.Groups != Definition.Library) throw new InvalidOperationException($"The group '{Definition.Name}' belongs to another graph.");
            Definition.ContentChanged += OnDefinitionChanged;
            Definition.InterfaceChanged += OnDefinitionRenamed;
            using (Definition.Library.Root.Undo.Suspend()) Title = Definition.Name;
        }
    }

    private void OnDefinitionChanged(object? sender, EventArgs e) => Invalidate();

    private void OnDefinitionRenamed(object? sender, EventArgs e)
    {
        using (Definition.Library.Root.Undo.Suspend()) Title = Definition.Name;
        Invalidate();
    }
}

/// <summary>The node inside a group whose outputs are the group's inputs; it can't be deleted or copied.</summary>
public sealed class GroupInputNodeViewModel : GroupInterfaceNodeViewModel
{
    internal GroupInputNodeViewModel(NodeGroupDefinition definition)
        : base(definition, "Group Input", inputsFollowGroupInputs: false, outputsFollowGroupOutputs: false, outputsFollowGroupInputs: true, inputsFollowGroupOutputs: false)
    {
        HeaderColor = Color.FromRgb(0x3C, 0x3C, 0x3C);
        Width = NodeGroupDefinition.InterfaceNodeWidth;
    }

    /// <inheritdoc/>
    protected internal override bool CanRemove => false;

    /// <inheritdoc/>
    protected internal override void Compute(ComputeContext context)
    {
        // Its outputs are set by the group node being computed.
    }
}

/// <summary>The node inside a group whose inputs are the group's outputs; it can't be deleted or copied.</summary>
public sealed class GroupOutputNodeViewModel : GroupInterfaceNodeViewModel
{
    internal GroupOutputNodeViewModel(NodeGroupDefinition definition)
        : base(definition, "Group Output", inputsFollowGroupInputs: false, outputsFollowGroupOutputs: false, outputsFollowGroupInputs: false, inputsFollowGroupOutputs: true)
    {
        HeaderColor = Color.FromRgb(0x3C, 0x3C, 0x3C);
        Width = NodeGroupDefinition.InterfaceNodeWidth;
    }

    /// <inheritdoc/>
    protected internal override bool CanRemove => false;

    /// <inheritdoc/>
    protected internal override void Compute(ComputeContext context)
    {
        // The group node reads its inputs.
    }
}
