using System.Collections.ObjectModel;
using System.Windows.Input;
using Atelier.Core.Primitives;

namespace Atelier.Nodes;

/// <summary>
/// A node of a graph: a title bar (with an optional color and command buttons), inputs on the left, outputs on the
/// right, and optional content for settings in between. Derive from it for each kind of node, adding its sockets
/// in the constructor.
/// </summary>
/// <remarks>
/// Changes users make (title, position, width, collapsing, muting) are recorded on the graph's
/// <see cref="NodeGraphViewModel.Undo"/> stack; selection isn't.
/// </remarks>
public class NodeViewModel : NodeGraphObject
{
    private readonly ObservableCollection<InputSocketViewModel> _inputs = [];
    private readonly ObservableCollection<OutputSocketViewModel> _outputs = [];
    private string _title;
    private Color? _headerColor;
    private Point _position;
    private float _width = 140;
    private bool _isCollapsed;
    private bool _isMuted;
    private bool _isSelected;
    private object? _content;
    private string? _error;
    private NodeGraphViewModel? _graph;

    /// <summary>Initializes a node.</summary>
    public NodeViewModel(string title = "Node")
    {
        _title = title ?? "";
        Inputs = new ReadOnlyObservableCollection<InputSocketViewModel>(_inputs);
        Outputs = new ReadOnlyObservableCollection<OutputSocketViewModel>(_outputs);
    }

    /// <summary>Gets or sets the identifier that saved graphs refer to the node by; a new GUID by default.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets or sets the <see cref="NodeType.Id"/> of the catalog entry that created the node, or <c>null</c>.</summary>
    public string? TypeId { get; set; }

    /// <summary>Gets the graph the node is in, or <c>null</c>.</summary>
    public NodeGraphViewModel? Graph
    {
        get => _graph;
        internal set
        {
            if (_graph == value) return;
            var old = _graph;
            _graph = value;
            OnGraphChanged(old, value);
        }
    }

    /// <summary>
    /// Gets whether the node can be deleted, duplicated and copied; <c>false</c> for the Group Input and Group Output
    /// nodes inside groups.
    /// </summary>
    protected internal virtual bool CanRemove => true;

    /// <summary>Called when the node is added to a graph or removed from one.</summary>
    protected virtual void OnGraphChanged(NodeGraphViewModel? oldGraph, NodeGraphViewModel? newGraph)
    {
    }

    /// <summary>Gets or sets the text in the title bar.</summary>
    public string Title
    {
        get => _title;
        set => SetUndoableProperty(ref _title, value ?? "", Graph?.Undo, "Rename", v => Title = v);
    }

    /// <summary>Gets or sets the title bar's color, or <c>null</c> for the theme's.</summary>
    public Color? HeaderColor
    {
        get => _headerColor;
        set => SetProperty(ref _headerColor, value);
    }

    /// <summary>Gets or sets the top-left corner in graph coordinates.</summary>
    public Point Position
    {
        get => _position;
        set => SetUndoableProperty(ref _position, value, Graph?.Undo, "Move", v => Position = v);
    }

    /// <summary>Gets or sets the width in graph units; users can resize it.</summary>
    public float Width
    {
        get => _width;
        set => SetUndoableProperty(ref _width, Math.Max(MinWidth, value), Graph?.Undo, "Resize", v => Width = v);
    }

    /// <summary>Gets the smallest <see cref="Width"/>; 40 unless overridden.</summary>
    protected virtual float MinWidth => 40;

    /// <summary>Gets or sets whether only the title bar is shown, with the connected sockets on its edges.</summary>
    public bool IsCollapsed
    {
        get => _isCollapsed;
        set => SetUndoableProperty(ref _isCollapsed, value, Graph?.Undo, value ? "Collapse" : "Expand", v => IsCollapsed = v);
    }

    /// <summary>Gets or sets whether the node is bypassed: evaluation passes its inputs through to matching outputs.</summary>
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (SetUndoableProperty(ref _isMuted, value, Graph?.Undo, value ? "Mute" : "Unmute", v => IsMuted = v)) Graph?.OnContentChanged();
        }
    }

    /// <summary>Gets or sets whether the node is selected (not recorded for undo).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Gets or sets what went wrong with the node, shown on it; <c>null</c> when nothing did. Evaluation sets it for computing nodes.</summary>
    public string? Error
    {
        get => _error;
        set => SetProperty(ref _error, value);
    }

    /// <summary>Gets or sets what is shown between the title bar and the sockets: an element, or a view model the editor finds a template for.</summary>
    public object? Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    /// <summary>Gets the commands shown as buttons in the title bar; their [Command] attributes give their icons and tooltips.</summary>
    public ObservableCollection<ICommand> HeaderCommands { get; } = [];

    /// <summary>Gets the inputs, top to bottom.</summary>
    public ReadOnlyObservableCollection<InputSocketViewModel> Inputs { get; }

    /// <summary>Gets the outputs, top to bottom.</summary>
    public ReadOnlyObservableCollection<OutputSocketViewModel> Outputs { get; }

    /// <summary>Adds an input.</summary>
    public InputSocketViewModel AddInput(InputSocketViewModel input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Node != null) throw new InvalidOperationException($"The input '{input}' already belongs to a node.");
        input.Node = this;
        _inputs.Add(input);
        return input;
    }

    /// <summary>Adds an input named <paramref name="name"/> of <paramref name="type"/>, starting at <paramref name="value"/> or the type's default.</summary>
    public InputSocketViewModel AddInput(string name, SocketType type, object? value = null) => AddInput(new InputSocketViewModel(name, type, value));

    /// <summary>Adds an output.</summary>
    public OutputSocketViewModel AddOutput(OutputSocketViewModel output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (output.Node != null) throw new InvalidOperationException($"The output '{output}' already belongs to a node.");
        output.Node = this;
        _outputs.Add(output);
        return output;
    }

    /// <summary>Inserts <paramref name="input"/> at <paramref name="index"/>, for nodes whose sockets change (like group nodes).</summary>
    protected void InsertInput(int index, InputSocketViewModel input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Node != null) throw new InvalidOperationException($"The input '{input}' already belongs to a node.");
        input.Node = this;
        _inputs.Insert(Math.Clamp(index, 0, _inputs.Count), input);
    }

    /// <summary>Inserts <paramref name="output"/> at <paramref name="index"/>, for nodes whose sockets change.</summary>
    protected void InsertOutput(int index, OutputSocketViewModel output)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (output.Node != null) throw new InvalidOperationException($"The output '{output}' already belongs to a node.");
        output.Node = this;
        _outputs.Insert(Math.Clamp(index, 0, _outputs.Count), output);
    }

    /// <summary>Removes <paramref name="socket"/>, which must not be connected (disconnect it through the graph first, for undo).</summary>
    /// <exception cref="InvalidOperationException">The socket is connected.</exception>
    protected void RemoveSocket(SocketViewModel socket)
    {
        ArgumentNullException.ThrowIfNull(socket);
        if (socket.Node != this) return;
        if (socket.IsConnected) throw new InvalidOperationException($"The socket '{socket}' is connected.");
        if (socket is InputSocketViewModel input) _inputs.Remove(input);
        else _outputs.Remove((OutputSocketViewModel)socket);
        socket.Node = null;
    }

    /// <summary>Moves <paramref name="socket"/> to position <paramref name="index"/> among the node's inputs or outputs.</summary>
    protected void MoveSocket(SocketViewModel socket, int index)
    {
        ArgumentNullException.ThrowIfNull(socket);
        if (socket is InputSocketViewModel input && _inputs.IndexOf(input) is var i and >= 0) _inputs.Move(i, Math.Clamp(index, 0, _inputs.Count - 1));
        else if (socket is OutputSocketViewModel output && _outputs.IndexOf(output) is var o and >= 0) _outputs.Move(o, Math.Clamp(index, 0, _outputs.Count - 1));
    }

    /// <summary>Adds an output named <paramref name="name"/> of <paramref name="type"/>.</summary>
    public OutputSocketViewModel AddOutput(string name, SocketType type) => AddOutput(new OutputSocketViewModel(name, type));

    /// <summary>Gets the input named <paramref name="name"/>, or <c>null</c>.</summary>
    public InputSocketViewModel? FindInput(string name) => _inputs.FirstOrDefault(s => s.Name == name);

    /// <summary>Gets the output named <paramref name="name"/>, or <c>null</c>.</summary>
    public OutputSocketViewModel? FindOutput(string name) => _outputs.FirstOrDefault(s => s.Name == name);

    /// <summary>
    /// Creates an unlinked copy of the node, not in a graph: a new node of the same kind (see <see cref="CreateCopy"/>)
    /// with this node's title, colors, size, flags, position and input values (see <see cref="CopyStateFrom"/>).
    /// </summary>
    public NodeViewModel Copy()
    {
        var copy = CreateCopy();
        copy.CopyStateFrom(this);
        return copy;
    }

    /// <summary>
    /// Creates a new node of the same kind for <see cref="Copy"/>: from the graph's catalog when the node has a
    /// <see cref="TypeId"/>, a plain node with the same sockets for a plain <see cref="NodeViewModel"/>, otherwise with
    /// the type's parameterless constructor. Override it for node types that need arguments.
    /// </summary>
    /// <exception cref="NotSupportedException">The node's type can't be created.</exception>
    protected virtual NodeViewModel CreateCopy()
    {
        if (TypeId != null && Graph?.Catalog.Find(TypeId) is { } type) return type.CreateNode();
        if (GetType() == typeof(NodeViewModel))
        {
            var copy = new NodeViewModel(Title);
            foreach (var input in _inputs)
            {
                copy.AddInput(new InputSocketViewModel(input.Name, input.Type, input.Value) { Editor = input.Editor, Minimum = input.Minimum, Maximum = input.Maximum });
            }
            foreach (var output in _outputs) copy.AddOutput(output.Name, output.Type);
            return copy;
        }
        if (GetType().GetConstructor(Type.EmptyTypes) != null) return (NodeViewModel)Activator.CreateInstance(GetType())!;
        throw new NotSupportedException($"Nodes of type {GetType().Name} can't be copied: register it in the catalog or override CreateCopy.");
    }

    /// <summary>
    /// Copies <paramref name="source"/>'s state onto this new node for <see cref="Copy"/>: title, header color, width,
    /// collapsed and muted flags, position, type id, the values of the inputs that match by position and type, and the
    /// settings <see cref="WriteState"/> saves.
    /// Override it to copy the node's own settings too.
    /// </summary>
    protected virtual void CopyStateFrom(NodeViewModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var state = new System.Text.Json.Nodes.JsonObject();
        source.WriteState(state);
        if (state.Count > 0) ReadState(state);
        TypeId = source.TypeId;
        Title = source.Title;
        HeaderColor = source.HeaderColor;
        Width = source.Width;
        IsCollapsed = source.IsCollapsed;
        IsMuted = source.IsMuted;
        Position = source.Position;
        for (int i = 0; i < _inputs.Count && i < source._inputs.Count; i++)
        {
            if (_inputs[i].Type == source._inputs[i].Type) _inputs[i].Value = source._inputs[i].Value;
        }
    }

    /// <summary>
    /// Saves the node's own settings (beyond its sockets' values), such as a math node's operation, into
    /// <paramref name="state"/> for <see cref="NodeGraphSerializer"/>. Does nothing unless overridden.
    /// </summary>
    public virtual void WriteState(System.Text.Json.Nodes.JsonObject state)
    {
    }

    /// <summary>Restores the settings <see cref="WriteState"/> saved. Does nothing unless overridden.</summary>
    public virtual void ReadState(System.Text.Json.Nodes.JsonObject state)
    {
    }

    /// <summary>Gets whether any of the node's sockets is connected.</summary>
    public bool HasLinks => _inputs.Any(s => s.IsConnected) || _outputs.Any(s => s.IsConnected);

    /// <inheritdoc/>
    public override string ToString() => Title;
}
