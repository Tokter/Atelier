using Atelier.Core.Primitives;

namespace Atelier.Nodes;

/// <summary>
/// A reroute point: a dot that a link passes through, to route it around other nodes. It has one input and one output
/// of the same type (the type of the link it was put into), and passes its value through when the graph is evaluated.
/// </summary>
/// <remarks>The editor draws it as a dot centered on <see cref="Center"/>; both its sockets are anchored there.</remarks>
public sealed class RerouteNodeViewModel : ComputingNodeViewModel
{
    /// <summary>The size of the dot's area, in graph units.</summary>
    public const float Size = 16f;

    /// <summary>Initializes a reroute point for links of <paramref name="type"/>.</summary>
    public RerouteNodeViewModel(SocketType type) : base("Reroute")
    {
        ArgumentNullException.ThrowIfNull(type);
        Type = type;
        Width = Size;
        AddInput("Input", type);
        AddOutput("Output", type);
    }

    /// <summary>Gets the type of the links through the point.</summary>
    public SocketType Type { get; }

    /// <summary>Gets or sets the point's center in graph coordinates (its <see cref="NodeViewModel.Position"/> is the top-left corner of its area).</summary>
    public Point Center
    {
        get => Position + new Point(Size / 2, Size / 2);
        set => Position = value - new Point(Size / 2, Size / 2);
    }

    /// <inheritdoc/>
    protected override float MinWidth => Size;

    /// <inheritdoc/>
    protected internal override void Compute(ComputeContext context) => context.Set(Outputs[0], context.Get(Inputs[0]));

    /// <inheritdoc/>
    protected override NodeViewModel CreateCopy() => new RerouteNodeViewModel(Type);
}
