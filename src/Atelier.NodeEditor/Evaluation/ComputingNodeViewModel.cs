using System.Globalization;

namespace Atelier.Nodes;

/// <summary>
/// A node that computes its outputs from its inputs. A <see cref="GraphEvaluator"/> calls <see cref="Compute"/>
/// whenever an input it depends on changed, in dependency order.
/// </summary>
/// <example>
/// <code>
/// class AddNode : ComputingNodeViewModel
/// {
///     public AddNode() : base("Add")
///     {
///         AddInput("A", Sockets.Float);
///         AddInput("B", Sockets.Float);
///         AddOutput("Sum", Sockets.Float);
///     }
///
///     protected override void Compute(ComputeContext context) =>
///         context.Set("Sum", context.Get&lt;double&gt;("A") + context.Get&lt;double&gt;("B"));
/// }
/// </code>
/// </example>
public abstract class ComputingNodeViewModel : NodeViewModel
{
    /// <summary>Initializes a computing node.</summary>
    protected ComputingNodeViewModel(string title = "Node") : base(title)
    {
    }

    internal event EventHandler? Invalidated;

    /// <summary>
    /// Sets the outputs from the inputs. It should only read inputs and set outputs through
    /// <paramref name="context"/>, not change the graph. An exception becomes the node's
    /// <see cref="NodeViewModel.Error"/>, and its outputs fall back to their types' defaults.
    /// </summary>
    protected internal abstract void Compute(ComputeContext context);

    /// <summary>Asks for <see cref="Compute"/> to run again, for changes the evaluator doesn't see, such as a setting in the node's content.</summary>
    protected void Invalidate() => Invalidated?.Invoke(this, EventArgs.Empty);
}

/// <summary>What <see cref="ComputingNodeViewModel.Compute"/> reads its inputs from and writes its outputs to.</summary>
public sealed class ComputeContext
{
    /// <summary>Initializes a context for computing <paramref name="node"/>; the evaluator creates these, but tests of nodes can too.</summary>
    public ComputeContext(NodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        Node = node;
    }

    /// <summary>Gets the node being computed.</summary>
    public NodeViewModel Node { get; }

    /// <summary>Gets the value of <paramref name="input"/>: its link's value (converted to its type), otherwise its own.</summary>
    public object? Get(InputSocketViewModel input) => Own(input).EffectiveValue;

    /// <summary>Gets the value of <paramref name="input"/> as <typeparamref name="T"/>; numbers are converted between types.</summary>
    /// <exception cref="InvalidCastException">The value can't be converted.</exception>
    public T Get<T>(InputSocketViewModel input) => Convert<T>(Get(input), input);

    /// <summary>Gets the value of the input named <paramref name="inputName"/> as <typeparamref name="T"/>.</summary>
    /// <exception cref="ArgumentException">The node has no such input.</exception>
    public T Get<T>(string inputName) => Get<T>(Input(inputName));

    /// <summary>Gets whether the input named <paramref name="inputName"/> is connected.</summary>
    public bool IsConnected(string inputName) => Input(inputName).IsConnected;

    /// <summary>Sets the value of <paramref name="output"/>.</summary>
    public void Set(OutputSocketViewModel output, object? value)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (output.Node != Node) throw new ArgumentException($"The output '{output}' isn't on the node '{Node}'.", nameof(output));
        output.Value = value;
    }

    /// <summary>Sets the value of the output named <paramref name="outputName"/>.</summary>
    /// <exception cref="ArgumentException">The node has no such output.</exception>
    public void Set(string outputName, object? value) =>
        Set(Node.FindOutput(outputName) ?? throw new ArgumentException($"The node '{Node}' has no output '{outputName}'.", nameof(outputName)), value);

    private InputSocketViewModel Own(InputSocketViewModel input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.Node == Node ? input : throw new ArgumentException($"The input '{input}' isn't on the node '{Node}'.", nameof(input));
    }

    private InputSocketViewModel Input(string name) =>
        Node.FindInput(name) ?? throw new ArgumentException($"The node '{Node}' has no input '{name}'.", nameof(name));

    private static T Convert<T>(object? value, InputSocketViewModel input)
    {
        if (value is T typed) return typed;
        if (value is null) return default!;
        var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(target))
        {
            try
            {
                return (T)System.Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException or InvalidCastException)
            {
                throw new InvalidCastException($"The value '{value}' of the input '{input.Name}' can't be converted to {typeof(T).Name}.", ex);
            }
        }
        throw new InvalidCastException($"The input '{input.Name}' holds a {value.GetType().Name}, not a {typeof(T).Name}.");
    }
}
