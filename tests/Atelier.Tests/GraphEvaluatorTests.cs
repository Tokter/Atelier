using Atelier.Core.Threading;
using Atelier.Nodes;

namespace Atelier.Tests;

/// <summary>A dispatcher that runs posted work only when asked, like frames.</summary>
internal sealed class QueueDispatcher : IDispatcher
{
    private readonly Queue<Action> _queue = new();

    public int Pending => _queue.Count;
    public bool CheckAccess() => true;
    public void Post(Action action) => _queue.Enqueue(action);
    public void Send(Action action) => action();

    public void RunFrame()
    {
        for (int n = _queue.Count; n > 0; n--) _queue.Dequeue()();
    }
}

internal sealed class AddNode : ComputingNodeViewModel
{
    public int Computed { get; private set; }

    public AddNode(string title = "Add") : base(title)
    {
        AddInput("A", TestSockets.Float);
        AddInput("B", TestSockets.Float);
        AddOutput("Sum", TestSockets.Float);
    }

    protected override void Compute(ComputeContext context)
    {
        Computed++;
        context.Set("Sum", context.Get<double>("A") + context.Get<double>("B"));
    }
}

/// <summary>Outputs its input, but no less than 0; throws for NaN.</summary>
internal sealed class ClampNode : ComputingNodeViewModel
{
    private double _minimum;

    public int Computed { get; private set; }

    public ClampNode() : base("Clamp")
    {
        AddInput("Value", TestSockets.Float);
        AddOutput("Result", TestSockets.Float);
    }

    // A setting outside the sockets, as a node's content would have.
    public double Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            Invalidate();
        }
    }

    protected override void Compute(ComputeContext context)
    {
        Computed++;
        double value = context.Get<double>("Value");
        if (double.IsNaN(value)) throw new ArgumentException("The value isn't a number.");
        context.Set("Result", Math.Max(Minimum, value));
    }
}

public class GraphEvaluatorTests
{
    private readonly QueueDispatcher _frames = new();

    [Fact]
    public void ComputesEveryNodeInTheNextFrame_InDependencyOrder()
    {
        var graph = new NodeGraphViewModel();
        var second = graph.AddNode(new AddNode("Second"));
        var first = graph.AddNode(new AddNode("First"));
        first.Inputs[0].Value = 1.0;
        first.Inputs[1].Value = 2.0;
        second.Inputs[1].Value = 10.0;
        graph.Connect(first.Outputs[0], second.Inputs[0]);
        using var evaluator = new GraphEvaluator(graph, _frames);
        IReadOnlyList<NodeViewModel>? computed = null;
        evaluator.Evaluated += (_, e) => computed = e.ComputedNodes;

        Assert.True(evaluator.IsPending);
        Assert.Equal(0, first.Computed);

        _frames.RunFrame();
        Assert.Equal([first, second], computed);
        Assert.Equal(13.0, second.Outputs[0].Value);
        Assert.False(evaluator.IsPending);
    }

    [Fact]
    public void ManyChangesInOneFrame_ComputeEachNodeOnce()
    {
        var graph = new NodeGraphViewModel();
        var add = graph.AddNode(new AddNode());
        using var evaluator = new GraphEvaluator(graph, _frames);
        _frames.RunFrame();

        for (int i = 1; i <= 5; i++) add.Inputs[0].Value = (double)i;
        add.Inputs[1].Value = 0.5;
        Assert.Equal(1, _frames.Pending);

        _frames.RunFrame();
        Assert.Equal(2, add.Computed);
        Assert.Equal(5.5, add.Outputs[0].Value);
    }

    [Fact]
    public void OnlyAffectedNodesRecompute_AndUnchangedOutputsStopThePropagation()
    {
        var graph = new NodeGraphViewModel();
        var clamp = graph.AddNode(new ClampNode());
        var after = graph.AddNode(new AddNode("After"));
        var unrelated = graph.AddNode(new AddNode("Unrelated"));
        graph.Connect(clamp.Outputs[0], after.Inputs[0]);
        clamp.Inputs[0].Value = -1.0;
        using var evaluator = new GraphEvaluator(graph, _frames);
        _frames.RunFrame();

        clamp.Inputs[0].Value = -2.0; // still clamped to 0
        Assert.Equal([clamp], evaluator.EvaluateNow());
        Assert.Equal(1, after.Computed);

        clamp.Inputs[0].Value = 4.0;
        Assert.Equal([clamp, after], evaluator.EvaluateNow());
        Assert.Equal(4.0, after.Outputs[0].Value);
        Assert.Equal(1, unrelated.Computed);
    }

    [Fact]
    public void AConnectedInputsOwnValue_DoesntTriggerAPass()
    {
        var graph = new NodeGraphViewModel();
        var first = graph.AddNode(new AddNode("First"));
        var second = graph.AddNode(new AddNode("Second"));
        graph.Connect(first.Outputs[0], second.Inputs[0]);
        using var evaluator = new GraphEvaluator(graph, _frames);
        _frames.RunFrame();

        second.Inputs[0].Value = 99.0;
        Assert.False(evaluator.IsPending);
    }

    [Fact]
    public void LinkChanges_AndSourceOutputs_UpdateTheNodesTheyFeed()
    {
        var graph = new NodeGraphViewModel();
        var source = new NodeViewModel("Count"); // doesn't compute: its output is set from outside
        var count = source.AddOutput("Count", TestSockets.Int);
        graph.AddNode(source);
        var add = graph.AddNode(new AddNode());
        add.Inputs[0].Value = 0.5;
        using var evaluator = new GraphEvaluator(graph, _frames);
        _frames.RunFrame();

        count.Value = 3;
        Assert.False(evaluator.IsPending); // nothing linked yet

        var link = graph.Connect(count, add.Inputs[0])!;
        _frames.RunFrame();
        Assert.Equal(3.0, add.Outputs[0].Value); // converted from int

        count.Value = 5;
        _frames.RunFrame();
        Assert.Equal(5.0, add.Outputs[0].Value);

        graph.Disconnect(link);
        _frames.RunFrame();
        Assert.Equal(0.5, add.Outputs[0].Value);

        graph.Undo.Undo(); // the link is back
        _frames.RunFrame();
        Assert.Equal(5.0, add.Outputs[0].Value);
    }

    [Fact]
    public void AddedNodesAreComputed_AndRemovingANodeUpdatesWhatItFed()
    {
        var graph = new NodeGraphViewModel();
        using var evaluator = new GraphEvaluator(graph, _frames);
        var first = graph.AddNode(new AddNode("First"));
        var second = graph.AddNode(new AddNode("Second"));
        first.Inputs[0].Value = 2.0;
        graph.Connect(first.Outputs[0], second.Inputs[0]);
        _frames.RunFrame();
        Assert.Equal(2.0, second.Outputs[0].Value);

        graph.RemoveNode(first);
        _frames.RunFrame();
        Assert.Equal(0.0, second.Outputs[0].Value);

        first.Inputs[0].Value = 7.0; // no longer in the graph
        Assert.False(evaluator.IsPending);
    }

    [Fact]
    public void AMutedNode_PassesItsFirstFittingInputThrough()
    {
        var graph = new NodeGraphViewModel();
        var clamp = graph.AddNode(new ClampNode());
        clamp.Inputs[0].Value = -3.0;
        using var evaluator = new GraphEvaluator(graph, _frames);
        _frames.RunFrame();
        Assert.Equal(0.0, clamp.Outputs[0].Value);

        clamp.IsMuted = true;
        _frames.RunFrame();
        Assert.Equal(-3.0, clamp.Outputs[0].Value);
        Assert.Equal(1, clamp.Computed);

        clamp.IsMuted = false;
        _frames.RunFrame();
        Assert.Equal(0.0, clamp.Outputs[0].Value);
    }

    [Fact]
    public void ANodeThatThrows_ShowsTheError_AndOutputsDefaults()
    {
        var graph = new NodeGraphViewModel();
        var clamp = graph.AddNode(new ClampNode());
        var after = graph.AddNode(new AddNode());
        graph.Connect(clamp.Outputs[0], after.Inputs[0]);
        clamp.Inputs[0].Value = 5.0;
        using var evaluator = new GraphEvaluator(graph, _frames);
        _frames.RunFrame();

        clamp.Inputs[0].Value = double.NaN;
        _frames.RunFrame();
        Assert.Equal("The value isn't a number.", clamp.Error);
        Assert.Equal(0.0, clamp.Outputs[0].Value);
        Assert.Equal(0.0, after.Outputs[0].Value);
        Assert.Null(after.Error);

        clamp.Inputs[0].Value = 1.0;
        _frames.RunFrame();
        Assert.Null(clamp.Error);
        Assert.Equal(1.0, after.Outputs[0].Value);
    }

    [Fact]
    public void ANodeCanAskToBeComputedAgain_UntilTheEvaluatorIsDisposed()
    {
        var graph = new NodeGraphViewModel();
        var clamp = graph.AddNode(new ClampNode());
        clamp.Inputs[0].Value = 1.0;
        var evaluator = new GraphEvaluator(graph, _frames);
        _frames.RunFrame();

        clamp.Minimum = 3;
        _frames.RunFrame();
        Assert.Equal(3.0, clamp.Outputs[0].Value);

        evaluator.Dispose();
        clamp.Minimum = 5;
        clamp.Inputs[0].Value = 2.0;
        _frames.RunFrame();
        Assert.Equal(3.0, clamp.Outputs[0].Value);
        Assert.False(evaluator.IsPending);
    }

    [Fact]
    public void WithoutAFrameLoop_ChangesAreComputedRightAway()
    {
        // The default dispatcher in tests and headless use runs posted work at once.
        var graph = new NodeGraphViewModel();
        var add = graph.AddNode(new AddNode());
        using var evaluator = new GraphEvaluator(graph, Dispatcher.ImmediateDispatcher.Instance);

        add.Inputs[0].Value = 4.0;
        Assert.Equal(4.0, add.Outputs[0].Value);
    }

    [Fact]
    public void Context_ConvertsNumbers_AndRejectsUnknownSockets()
    {
        var node = new AddNode();
        var context = new ComputeContext(node);
        node.Inputs[0].Value = 2.0;
        node.Inputs[1].Value = null;

        Assert.Equal(2, context.Get<int>("A"));
        Assert.Equal(0.0, context.Get<double>("B"));
        Assert.Equal(2.0, context.Get(node.Inputs[0]));
        Assert.False(context.IsConnected("A"));
        Assert.Throws<ArgumentException>(() => context.Get<double>("C"));
        Assert.Throws<ArgumentException>(() => context.Set("Nope", 1.0));
        Assert.Throws<ArgumentException>(() => context.Get(new AddNode().Inputs[0]));

        node.Inputs[0].Value = "text";
        Assert.Throws<InvalidCastException>(() => context.Get<double>("A"));
    }
}
