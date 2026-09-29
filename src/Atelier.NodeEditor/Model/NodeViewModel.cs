using System.Collections.ObjectModel;
using System.Windows.Input;
using Atelier.Core.Primitives;

namespace Atelier.NodeEditor;

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
    public NodeGraphViewModel? Graph { get; internal set; }

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
        set => SetUndoableProperty(ref _width, Math.Max(40, value), Graph?.Undo, "Resize", v => Width = v);
    }

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
        set => SetUndoableProperty(ref _isMuted, value, Graph?.Undo, value ? "Mute" : "Unmute", v => IsMuted = v);
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

    /// <summary>Adds an output named <paramref name="name"/> of <paramref name="type"/>.</summary>
    public OutputSocketViewModel AddOutput(string name, SocketType type) => AddOutput(new OutputSocketViewModel(name, type));

    /// <summary>Gets the input named <paramref name="name"/>, or <c>null</c>.</summary>
    public InputSocketViewModel? FindInput(string name) => _inputs.FirstOrDefault(s => s.Name == name);

    /// <summary>Gets the output named <paramref name="name"/>, or <c>null</c>.</summary>
    public OutputSocketViewModel? FindOutput(string name) => _outputs.FirstOrDefault(s => s.Name == name);

    /// <summary>Gets whether any of the node's sockets is connected.</summary>
    public bool HasLinks => _inputs.Any(s => s.IsConnected) || _outputs.Any(s => s.IsConnected);

    /// <inheritdoc/>
    public override string ToString() => Title;
}
