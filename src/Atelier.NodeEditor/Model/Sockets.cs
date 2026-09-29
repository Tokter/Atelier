using Atelier.Core.Primitives;

namespace Atelier.Nodes;

/// <summary>Which control edits the value of an unconnected input, next to its socket.</summary>
public enum InputEditor
{
    /// <summary>Chosen from the socket type's value type: a check box for <see cref="bool"/>, a slider for numbers with a range, otherwise a text box.</summary>
    Auto,

    /// <summary>No control; only the input's name is shown.</summary>
    None,

    /// <summary>A text box.</summary>
    TextBox,

    /// <summary>A slider between <see cref="InputSocketViewModel.Minimum"/> and <see cref="InputSocketViewModel.Maximum"/>.</summary>
    Slider,

    /// <summary>A knob between <see cref="InputSocketViewModel.Minimum"/> and <see cref="InputSocketViewModel.Maximum"/>.</summary>
    Knob,

    /// <summary>A check box.</summary>
    CheckBox,
}

/// <summary>An input or output of a node: a typed connection point.</summary>
public abstract class SocketViewModel : NodeGraphObject
{
    private string _name;
    private Point _anchor;

    private protected SocketViewModel(string name, SocketType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        _name = name ?? "";
        Type = type;
    }

    /// <summary>Gets or sets the name shown next to the socket.</summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value ?? "");
    }

    /// <summary>Gets the data type, which decides what the socket can connect to.</summary>
    public SocketType Type { get; }

    /// <summary>Gets the node the socket belongs to, once added to one.</summary>
    public NodeViewModel? Node { get; internal set; }

    /// <summary>Gets whether one or more links are attached.</summary>
    public abstract bool IsConnected { get; }

    /// <summary>
    /// Gets or sets the socket's center in graph coordinates. The editor sets it when it lays the node out; links
    /// are drawn between anchors.
    /// </summary>
    public Point Anchor
    {
        get => _anchor;
        set => SetProperty(ref _anchor, value);
    }

    /// <inheritdoc/>
    public override string ToString() => Node is null ? Name : $"{Node.Title}.{Name}";
}

/// <summary>
/// An input of a node, on its left. It has at most one link; without one, its <see cref="Value"/> is used, which a
/// control next to the socket edits (hidden while connected).
/// </summary>
public sealed class InputSocketViewModel : SocketViewModel
{
    private object? _value;
    private LinkViewModel? _link;
    private InputEditor _editor;

    /// <summary>Initializes an input whose value starts as <paramref name="value"/>, or the type's default.</summary>
    public InputSocketViewModel(string name, SocketType type, object? value = null) : base(name, type)
    {
        _value = value ?? type.DefaultValue;
    }

    /// <summary>
    /// Gets or sets the value used while the input isn't connected. Setting it is recorded for undo; quick successive
    /// changes (dragging a slider) merge into one step.
    /// </summary>
    public object? Value
    {
        get => _value;
        set
        {
            if (SetUndoableProperty(ref _value, value, Node?.Graph?.Undo, $"Change {Name}", v => Value = v))
            {
                OnPropertyChanged(nameof(EffectiveValue));
                Node?.Graph?.OnContentChanged();
            }
        }
    }

    /// <summary>Gets the link that feeds the input, or <c>null</c>.</summary>
    public LinkViewModel? Link
    {
        get => _link;
        internal set
        {
            if (!SetProperty(ref _link, value)) return;
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(EffectiveValue));
        }
    }

    /// <inheritdoc/>
    public override bool IsConnected => _link != null;

    /// <summary>Gets the value the node works with: the linked output's value (converted to this type), otherwise <see cref="Value"/>.</summary>
    public object? EffectiveValue => _link is { } link ? Type.ConvertFrom(link.From.Value, link.From.Type) : _value;

    /// <summary>Gets or sets which control edits <see cref="Value"/>.</summary>
    public InputEditor Editor
    {
        get => _editor;
        set => SetProperty(ref _editor, value);
    }

    /// <summary>Gets or sets the smallest value a slider or knob offers, or <c>null</c> for no range.</summary>
    public double? Minimum { get; set; }

    /// <summary>Gets or sets the largest value a slider or knob offers, or <c>null</c> for no range.</summary>
    public double? Maximum { get; set; }

    internal void OnLinkedValueChanged() => OnPropertyChanged(nameof(EffectiveValue));
}

/// <summary>An output of a node, on its right. It can feed any number of inputs.</summary>
public sealed class OutputSocketViewModel : SocketViewModel
{
    private readonly List<LinkViewModel> _links = [];
    private object? _value;

    /// <summary>Initializes an output.</summary>
    public OutputSocketViewModel(string name, SocketType type) : base(name, type)
    {
        _value = type.DefaultValue;
    }

    /// <summary>Gets or sets the value the node produces; linked inputs' <see cref="InputSocketViewModel.EffectiveValue"/> follow it.</summary>
    public object? Value
    {
        get => _value;
        set
        {
            if (!SetProperty(ref _value, value)) return;
            foreach (var link in _links) link.To.OnLinkedValueChanged();
        }
    }

    /// <summary>Gets the links from this output.</summary>
    public IReadOnlyList<LinkViewModel> Links => _links;

    /// <inheritdoc/>
    public override bool IsConnected => _links.Count > 0;

    internal void AddLink(LinkViewModel link)
    {
        _links.Add(link);
        if (_links.Count == 1) OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(Links));
    }

    internal void RemoveLink(LinkViewModel link)
    {
        if (!_links.Remove(link)) return;
        if (_links.Count == 0) OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(Links));
    }
}
