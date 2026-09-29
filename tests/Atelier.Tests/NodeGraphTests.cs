using Atelier.Core.Primitives;
using Atelier.Nodes;

namespace Atelier.Tests;

/// <summary>Socket types and nodes shared by the node graph tests.</summary>
internal static class TestSockets
{
    public static readonly SocketType Float = new("float", "Float", Color.FromRgb(160, 160, 160), typeof(double), 0.0);
    public static readonly SocketType Int = new("int", "Integer", Color.FromRgb(70, 130, 70), typeof(int), 0);
    public static readonly SocketType Text = new("string", "String", Color.FromRgb(100, 150, 200), typeof(string), "");

    static TestSockets() => Float.AddConversionFrom(Int, v => (double)(int)v!);

    /// <summary>A node with float inputs A and B and a float output Result.</summary>
    public static NodeViewModel Math(string title = "Math")
    {
        var node = new NodeViewModel(title);
        node.AddInput("A", Float);
        node.AddInput("B", Float);
        node.AddOutput("Result", Float);
        return node;
    }
}

/// <summary>A clock that only moves when told to.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _ticks;
    public void Advance(TimeSpan by) => _ticks += by.Ticks;
}

public class SocketTypeTests
{
    [Fact]
    public void AnInput_AcceptsItsOwnType_AndTypesItOptsInto()
    {
        Assert.True(TestSockets.Float.CanConnectFrom(TestSockets.Float));
        Assert.True(TestSockets.Float.CanConnectFrom(TestSockets.Int));
        Assert.False(TestSockets.Int.CanConnectFrom(TestSockets.Float)); // conversions are one-way
        Assert.False(TestSockets.Float.CanConnectFrom(TestSockets.Text));

        Assert.Equal(3.0, TestSockets.Float.ConvertFrom(3, TestSockets.Int));
        Assert.Throws<InvalidOperationException>(() => TestSockets.Float.ConvertFrom("x", TestSockets.Text));
    }

    [Fact]
    public void Inputs_StartWithTheTypesDefault_UnlessGivenAValue()
    {
        var node = new NodeViewModel();
        Assert.Equal(0.0, node.AddInput("A", TestSockets.Float).Value);
        Assert.Equal(2.5, node.AddInput("B", TestSockets.Float, 2.5).Value);
        Assert.Same(node, node.Inputs[0].Node);
        Assert.Same(node.Inputs[1], node.FindInput("B"));
    }
}

public class NodeGraphTests
{
    private static (NodeGraphViewModel Graph, NodeViewModel A, NodeViewModel B) TwoNodes()
    {
        var graph = new NodeGraphViewModel();
        return (graph, graph.AddNode(TestSockets.Math("A")), graph.AddNode(TestSockets.Math("B")));
    }

    [Fact]
    public void Connect_LinksTheSockets()
    {
        var (graph, a, b) = TwoNodes();
        var link = graph.Connect(a.Outputs[0], b.Inputs[0]);

        Assert.NotNull(link);
        Assert.Same(link, Assert.Single(graph.Links));
        Assert.Same(link, b.Inputs[0].Link);
        Assert.Same(link, Assert.Single(a.Outputs[0].Links));
        Assert.True(b.Inputs[0].IsConnected);
        Assert.True(a.Outputs[0].IsConnected);
        Assert.Equal(TestSockets.Float.Color, link!.Color);
    }

    [Fact]
    public void Connect_ReplacesTheInputsLink_AndOutputsFeedManyInputs()
    {
        var (graph, a, b) = TwoNodes();
        var c = graph.AddNode(TestSockets.Math("C"));
        graph.Connect(a.Outputs[0], c.Inputs[0]);
        graph.Connect(a.Outputs[0], c.Inputs[1]);
        Assert.Equal(2, a.Outputs[0].Links.Count);

        var replacing = graph.Connect(b.Outputs[0], c.Inputs[0]);

        Assert.Equal(2, graph.Links.Count);
        Assert.Same(replacing, c.Inputs[0].Link);
        Assert.Single(a.Outputs[0].Links);

        graph.Undo.Undo(); // one step: the old link is back
        Assert.Same(a.Outputs[0], c.Inputs[0].Link!.From);
        Assert.False(b.Outputs[0].IsConnected);
    }

    [Fact]
    public void CanConnect_RejectsInvalidLinks()
    {
        var (graph, a, b) = TwoNodes();
        var text = new NodeViewModel("Text");
        text.AddInput("In", TestSockets.Text);
        text.AddOutput("Out", TestSockets.Text);
        var counter = new NodeViewModel("Count");
        counter.AddOutput("Count", TestSockets.Int);
        graph.AddNode(text);
        graph.AddNode(counter);

        Assert.Equal(ConnectResult.SameNode, graph.CanConnect(a.Outputs[0], a.Inputs[0]));
        Assert.Equal(ConnectResult.IncompatibleTypes, graph.CanConnect(text.Outputs[0], a.Inputs[0]));
        Assert.Equal(ConnectResult.IncompatibleTypes, graph.CanConnect(a.Outputs[0], text.Inputs[0]));
        Assert.Equal(ConnectResult.Ok, graph.CanConnect(counter.Outputs[0], a.Inputs[0])); // opted-in conversion

        graph.Connect(a.Outputs[0], b.Inputs[0]);
        Assert.Equal(ConnectResult.AlreadyConnected, graph.CanConnect(a.Outputs[0], b.Inputs[0]));
        Assert.Equal(ConnectResult.WouldCreateCycle, graph.CanConnect(b.Outputs[0], a.Inputs[1]));
        Assert.Null(graph.Connect(b.Outputs[0], a.Inputs[1]));
        Assert.Single(graph.Links);

        Assert.Throws<ArgumentException>(() => graph.CanConnect(TestSockets.Math().Outputs[0], a.Inputs[0]));
    }

    [Fact]
    public void EffectiveValue_IsTheLinkedOutputsConvertedValue_OtherwiseTheInputsOwn()
    {
        var graph = new NodeGraphViewModel();
        var counter = new NodeViewModel("Count");
        var count = counter.AddOutput("Count", TestSockets.Int);
        var math = graph.AddNode(TestSockets.Math());
        graph.AddNode(counter);
        var input = math.Inputs[0];
        input.Value = 1.5;
        var changes = new List<string?>();
        input.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        count.Value = 4;
        Assert.Equal(1.5, input.EffectiveValue);

        graph.Connect(count, input);
        Assert.Equal(4.0, input.EffectiveValue);
        Assert.Contains(nameof(InputSocketViewModel.EffectiveValue), changes);

        changes.Clear();
        count.Value = 7;
        Assert.Equal(7.0, input.EffectiveValue);
        Assert.Equal([nameof(InputSocketViewModel.EffectiveValue)], changes);

        graph.Disconnect(input.Link!);
        Assert.Equal(1.5, input.EffectiveValue); // its own value was kept
    }

    [Fact]
    public void RemoveNode_RemovesItsLinks_AndUndoRestoresBoth()
    {
        var (graph, a, b) = TwoNodes();
        var c = graph.AddNode(TestSockets.Math("C"));
        graph.Connect(a.Outputs[0], b.Inputs[0]);
        graph.Connect(b.Outputs[0], c.Inputs[0]);
        graph.Connect(a.Outputs[0], c.Inputs[1]);

        Assert.True(graph.RemoveNode(b));

        Assert.Equal([a, c], graph.Nodes);
        Assert.Single(graph.Links);
        Assert.Null(b.Graph);
        Assert.False(b.HasLinks);
        Assert.Equal("Delete B", graph.Undo.UndoName);

        graph.Undo.Undo();
        Assert.Equal([a, b, c], graph.Nodes); // back in its place
        Assert.Same(graph, b.Graph);
        Assert.Equal(3, graph.Links.Count);
        Assert.Same(a.Outputs[0], b.Inputs[0].Link!.From);
        Assert.Same(b.Outputs[0], c.Inputs[0].Link!.From);

        graph.Undo.Redo();
        Assert.Equal([a, c], graph.Nodes);
        Assert.Single(graph.Links);
        Assert.False(graph.RemoveNode(b));
    }

    [Fact]
    public void RemoveNodes_IsOneUndoStep()
    {
        var (graph, a, b) = TwoNodes();
        graph.Connect(a.Outputs[0], b.Inputs[0]);
        a.IsSelected = b.IsSelected = true;

        graph.RemoveNodes(graph.SelectedNodes);
        Assert.Empty(graph.Nodes);
        Assert.Empty(graph.Links);

        graph.Undo.Undo();
        Assert.Equal([a, b], graph.Nodes);
        Assert.Single(graph.Links);
    }

    [Fact]
    public void AddNode_IsUndoable_AndANodeIsInOneGraphOnly()
    {
        var graph = new NodeGraphViewModel();
        var node = graph.AddNode(TestSockets.Math());
        Assert.Same(graph, node.Graph);
        Assert.Equal("Add Math", graph.Undo.UndoName);
        Assert.Throws<InvalidOperationException>(() => new NodeGraphViewModel().AddNode(node));

        graph.Undo.Undo();
        Assert.Empty(graph.Nodes);
        Assert.Null(node.Graph);
        graph.Undo.Redo();
        Assert.Same(node, Assert.Single(graph.Nodes));
    }

    [Fact]
    public void InsertIntoLink_UsesTheFirstFittingInputAndOutput()
    {
        var (graph, a, b) = TwoNodes();
        var link = graph.Connect(a.Outputs[0], b.Inputs[1])!;
        var middle = new NodeViewModel("Middle");
        middle.AddInput("Label", TestSockets.Text);
        var value = middle.AddInput("Value", TestSockets.Float);
        middle.AddOutput("Name", TestSockets.Text);
        var result = middle.AddOutput("Result", TestSockets.Float);
        graph.AddNode(middle);

        Assert.True(graph.InsertIntoLink(middle, link));

        Assert.Equal(2, graph.Links.Count);
        Assert.DoesNotContain(link, graph.Links);
        Assert.Same(a.Outputs[0], value.Link!.From);
        Assert.Same(result, b.Inputs[1].Link!.From);
        Assert.Equal("Insert Middle", graph.Undo.UndoName);

        graph.Undo.Undo();
        Assert.Same(link, Assert.Single(graph.Links));
        Assert.Same(link, b.Inputs[1].Link);
        Assert.False(middle.HasLinks);
    }

    [Fact]
    public void InsertIntoLink_DoesNothing_WithoutFittingSockets_OrWhenTheNodeIsLinked()
    {
        var (graph, a, b) = TwoNodes();
        var link = graph.Connect(a.Outputs[0], b.Inputs[0])!;
        var textOnly = new NodeViewModel("Text");
        textOnly.AddInput("In", TestSockets.Text);
        textOnly.AddOutput("Out", TestSockets.Text);
        var sink = new NodeViewModel("Sink");
        sink.AddInput("In", TestSockets.Float);
        graph.AddNode(textOnly);
        graph.AddNode(sink);
        var linked = graph.AddNode(TestSockets.Math("Linked"));
        graph.Connect(linked.Outputs[0], b.Inputs[1]);
        string? undoName = graph.Undo.UndoName;

        Assert.False(graph.InsertIntoLink(textOnly, link));
        Assert.False(graph.InsertIntoLink(sink, link)); // no output
        Assert.False(graph.InsertIntoLink(linked, link));
        Assert.Same(link, b.Inputs[0].Link);
        Assert.Equal(undoName, graph.Undo.UndoName);
    }

    [Fact]
    public void TopologyQueries()
    {
        var graph = new NodeGraphViewModel();
        // Added out of order: d <- c <- a, c <- b; e stands alone.
        var d = graph.AddNode(TestSockets.Math("D"));
        var c = graph.AddNode(TestSockets.Math("C"));
        var e = graph.AddNode(TestSockets.Math("E"));
        var a = graph.AddNode(TestSockets.Math("A"));
        var b = graph.AddNode(TestSockets.Math("B"));
        graph.Connect(a.Outputs[0], c.Inputs[0]);
        graph.Connect(b.Outputs[0], c.Inputs[1]);
        graph.Connect(c.Outputs[0], d.Inputs[0]);

        Assert.Equal([e, a, b, c, d], graph.GetTopologicalOrder());
        Assert.Equal(new HashSet<NodeViewModel> { a, b, c }, graph.GetUpstream(d).ToHashSet());
        Assert.Equal(new HashSet<NodeViewModel> { c, d }, graph.GetDownstream(a).ToHashSet());
        Assert.Empty(graph.GetUpstream(e));
        Assert.Equal([d, e], graph.GetSinks());
    }

    [Fact]
    public void NodeEdits_AreUndoable_ButSelectionIsNot()
    {
        var graph = new NodeGraphViewModel();
        var node = graph.AddNode(TestSockets.Math());
        graph.Undo.Clear();

        node.IsSelected = true;
        Assert.False(graph.Undo.CanUndo);

        node.Title = "Add";
        node.IsMuted = true;
        node.IsCollapsed = true;
        Assert.Equal("Collapse", graph.Undo.UndoName);

        graph.Undo.Undo();
        graph.Undo.Undo();
        graph.Undo.Undo();
        Assert.Equal("Math", node.Title);
        Assert.False(node.IsMuted);
        Assert.False(node.IsCollapsed);
        Assert.True(node.IsSelected);
        Assert.False(graph.Undo.CanUndo);
    }

    [Fact]
    public void MovingANode_MergesIntoOneStep()
    {
        var graph = new NodeGraphViewModel();
        var node = graph.AddNode(TestSockets.Math());
        for (int x = 1; x <= 10; x++) node.Position = new Point(x * 10, 0);

        Assert.Equal("Move", graph.Undo.UndoName);
        graph.Undo.Undo();
        Assert.Equal(default, node.Position);
        Assert.Equal("Add Math", graph.Undo.UndoName);

        graph.Undo.Redo();
        Assert.Equal(new Point(100, 0), node.Position);
    }
}

public class NodeUndoTests
{
    [Fact]
    public void InputValueChanges_MergeWithinTheWindow_AndNotAfterIt()
    {
        var clock = new ManualTimeProvider();
        var graph = new NodeGraphViewModel();
        graph.Undo.TimeProvider = clock;
        var input = graph.AddNode(TestSockets.Math()).Inputs[0];

        input.Value = 1.0;
        clock.Advance(TimeSpan.FromMilliseconds(500));
        input.Value = 2.0;
        clock.Advance(TimeSpan.FromMilliseconds(500));
        input.Value = 3.0; // still within a second of the last change
        clock.Advance(TimeSpan.FromSeconds(2));
        input.Value = 4.0;

        Assert.Equal("Change A", graph.Undo.UndoName);
        graph.Undo.Undo();
        Assert.Equal(3.0, input.Value);
        graph.Undo.Undo();
        Assert.Equal(0.0, input.Value);
    }

    [Fact]
    public void BreakMerge_AndOtherProperties_StartNewSteps()
    {
        var graph = new NodeGraphViewModel();
        var node = graph.AddNode(TestSockets.Math());
        graph.Undo.Clear();

        node.Inputs[0].Value = 1.0;
        node.Inputs[1].Value = 1.0;
        node.Inputs[0].Value = 2.0; // a different key came between
        graph.Undo.BreakMerge();
        node.Inputs[0].Value = 3.0;

        int steps = 0;
        while (graph.Undo.Undo()) steps++;
        Assert.Equal(4, steps);
    }

    [Fact]
    public void Groups_AreOneStep_AndMergeTheirPropertyChanges()
    {
        var clock = new ManualTimeProvider();
        var undo = new UndoStack { TimeProvider = clock };
        int value = 0, other = 0;

        using (undo.Group("Drag"))
        {
            for (int i = 1; i <= 5; i++)
            {
                undo.RecordChange("Set", "value", value, i, v => value = v);
                value = i;
                clock.Advance(TimeSpan.FromSeconds(5)); // slow, but in one group
            }
            undo.RecordChange("Set other", "other", other, 9, v => other = v);
            other = 9;
            Assert.False(undo.CanUndo); // not recorded until the group ends
        }

        Assert.Equal("Drag", undo.UndoName);
        Assert.True(undo.Undo());
        Assert.Equal((0, 0), (value, other));
        Assert.False(undo.CanUndo);
        Assert.True(undo.Redo());
        Assert.Equal((5, 9), (value, other));
    }

    [Fact]
    public void AnEmptyGroup_RecordsNothing_AndGroupsMustNest()
    {
        var undo = new UndoStack();
        using (undo.Group("Nothing")) { }
        Assert.False(undo.CanUndo);

        var outer = undo.Group("Outer");
        undo.Group("Inner");
        Assert.Throws<InvalidOperationException>(() => outer.Dispose());
    }

    [Fact]
    public void ANewChange_ClearsRedo_AndChangesDuringUndoArentRecorded()
    {
        var undo = new UndoStack();
        int value = 0, changed = 0;
        undo.Changed += (_, _) => changed++;
        void Set(int v)
        {
            undo.RecordChange("Set", "value", value, v, Set);
            value = v;
        }

        Set(1);
        undo.BreakMerge();
        Set(2);
        Assert.True(undo.Undo()); // Set(1) runs, but isn't recorded
        Assert.Equal(1, value);
        Assert.True(undo.CanRedo);
        Assert.Equal("Set", undo.UndoName);

        undo.Push(new DelegateUndoAction("Other", () => { }, () => { }));
        Assert.False(undo.CanRedo);
        Assert.Equal(4, changed);
        Assert.False(undo.Redo());
    }

    [Fact]
    public void MaxDepth_DropsTheOldestSteps()
    {
        var undo = new UndoStack { MaxDepth = 3 };
        var log = new List<int>();
        for (int i = 0; i < 5; i++)
        {
            int n = i;
            undo.Push(new DelegateUndoAction($"#{n}", () => log.Add(n), () => { }));
        }
        while (undo.Undo()) { }
        Assert.Equal([4, 3, 2], log);
    }
}

public class NodeCatalogTests
{
    private static NodeCatalog Catalog()
    {
        var catalog = new NodeCatalog();
        catalog.Register(new NodeType("math", "Math", "Converter", () => TestSockets.Math())
        {
            HeaderColor = Color.FromRgb(40, 80, 120),
            Keywords = ["add", "multiply"],
        });
        catalog.Register("value", "Value", "Input", () =>
        {
            var node = new NodeViewModel("Value");
            node.AddOutput("Value", TestSockets.Float);
            return node;
        });
        catalog.Register("text", "Join Text", "Text", () =>
        {
            var node = new NodeViewModel("Join Text");
            node.AddInput("A", TestSockets.Text);
            node.AddOutput("Text", TestSockets.Text);
            return node;
        });
        catalog.Register("viewer", "Viewer", "Output", () =>
        {
            var node = new NodeViewModel("Viewer");
            node.AddInput("Value", TestSockets.Float);
            return node;
        });
        return catalog;
    }

    [Fact]
    public void CreateNode_SetsTheTypeIdPositionAndColor()
    {
        var catalog = Catalog();
        var node = catalog.CreateNode("math", new Point(10, 20));

        Assert.Equal("math", node.TypeId);
        Assert.Equal(new Point(10, 20), node.Position);
        Assert.Equal(Color.FromRgb(40, 80, 120), node.HeaderColor);
        Assert.Throws<KeyNotFoundException>(() => catalog.CreateNode("nope", default));
        Assert.Throws<ArgumentException>(() => catalog.Register("math", "Again", "", () => new NodeViewModel()));
        Assert.Equal(["Converter", "Input", "Text", "Output"], catalog.Categories);

        var graph = new NodeGraphViewModel { Catalog = catalog };
        var added = graph.AddNode("value", new Point(5, 5));
        Assert.Same(added, Assert.Single(graph.Nodes));
    }

    [Fact]
    public void Search_RanksTitleMatchesFirst_AndFindsKeywordsAndCategories()
    {
        var catalog = Catalog();

        Assert.Equal(4, catalog.Search("").Count);
        Assert.Equal(["value", "viewer", "math"], catalog.Search("v").Select(t => t.Id)); // Math by its category, Converter
        Assert.Equal(["text"], catalog.Search("text").Select(t => t.Id)); // title word beats category
        Assert.Equal(["math"], catalog.Search("MULT").Select(t => t.Id));
        Assert.Equal(["viewer"], catalog.Search("output").Select(t => t.Id));
        Assert.Equal(["text"], catalog.Search("join tex").Select(t => t.Id)); // every word must match
        Assert.Empty(catalog.Search("join math"));
    }

    [Fact]
    public void Search_CanLimitToTypesThatConnectToADraggedSocket()
    {
        var catalog = Catalog();

        // Dragging from a float output: types with an input accepting floats.
        Assert.Equal(["math", "viewer"], catalog.Search("", acceptingOutput: TestSockets.Float).Select(t => t.Id));
        // From an int output: the float inputs accept ints too.
        Assert.Equal(["math", "viewer"], catalog.Search("", acceptingOutput: TestSockets.Int).Select(t => t.Id));
        // Dragging from a float input: types with an output it accepts.
        Assert.Equal(["math", "value"], catalog.Search("", feedingInput: TestSockets.Float).Select(t => t.Id));
        Assert.Equal(["text"], catalog.Search("", feedingInput: TestSockets.Text).Select(t => t.Id));
    }
}
