using Atelier.Core.Primitives;
using Atelier.Core.Threading;
using Atelier.Nodes;

namespace Atelier.Tests;

public class NodeGroupTests
{
    private static NodeGraphViewModel NewGraph()
    {
        var graph = new NodeGraphViewModel();
        graph.Catalog.Register("add", "Add", "Math", () => new AddNode());
        return graph;
    }

    // source -> [first -> second] -> sink, with the middle two grouped.
    private static (NodeGraphViewModel Graph, AddNode Source, AddNode First, AddNode Second, AddNode Sink, GroupNodeViewModel Group) Grouped()
    {
        var graph = NewGraph();
        var source = (AddNode)graph.AddNode("add", new Point(0, 0));
        var first = (AddNode)graph.AddNode("add", new Point(200, 0));
        var second = (AddNode)graph.AddNode("add", new Point(400, 0));
        var sink = (AddNode)graph.AddNode("add", new Point(600, 0));
        graph.Connect(source.Outputs[0], first.Inputs[0]);
        graph.Connect(first.Outputs[0], second.Inputs[0]);
        graph.Connect(second.Outputs[0], sink.Inputs[0]);
        graph.Undo.Clear();
        var group = graph.Groups.Group(graph, [first, second], "Chain")!;
        return (graph, source, first, second, sink, group);
    }

    [Fact]
    public void Grouping_MovesTheNodesInside_AndConnectsTheGroupNodeLikeThem()
    {
        var (graph, source, first, second, sink, group) = Grouped();
        var definition = group.Definition;

        Assert.Equal([source, sink, group], graph.Nodes);
        Assert.Equal("Chain", group.Title);
        Assert.Equal(["A"], definition.Inputs.Select(s => s.Name));
        Assert.Equal(["Sum"], definition.Outputs.Select(s => s.Name));
        Assert.Same(source.Outputs[0], group.Inputs[0].Link?.From);
        Assert.Same(group.Outputs[0], sink.Inputs[0].Link?.From);

        Assert.Same(definition.Graph, first.Graph);
        Assert.Same(definition.InputNode.Outputs[0], first.Inputs[0].Link?.From);
        Assert.Same(first.Outputs[0], second.Inputs[0].Link?.From);
        Assert.Same(second.Outputs[0], definition.OutputNode.Inputs[0].Link?.From);
        Assert.Same(graph.Undo, definition.Graph.Undo); // one history
        Assert.Equal("Make group Chain", graph.Undo.UndoName);

        graph.Undo.Undo();
        Assert.Equal([source, first, second, sink], graph.Nodes);
        Assert.Same(first.Outputs[0], second.Inputs[0].Link?.From);
        Assert.Same(second.Outputs[0], sink.Inputs[0].Link?.From);
        Assert.Empty(graph.Groups.Definitions);
        Assert.Equal(new Point(200, 0), first.Position);
    }

    [Fact]
    public void GroupNodes_ComputeTheGraphInside_WithTheirOwnInputs()
    {
        var (graph, source, first, second, sink, group) = Grouped();
        first.Inputs[1].Value = 1.0; // inside: (x + 1) + 2
        second.Inputs[1].Value = 2.0;
        source.Inputs[0].Value = 10.0;
        var copy = graph.AddNode(new GroupNodeViewModel(group.Definition));
        copy.Inputs[0].Value = 100.0;
        using var evaluator = new GraphEvaluator(graph, Dispatcher.ImmediateDispatcher.Instance);
        evaluator.EvaluateNow();

        Assert.Equal(13.0, group.Outputs[0].Value);
        Assert.Equal(103.0, copy.Outputs[0].Value); // same group, other input
        Assert.Equal(13.0, sink.Outputs[0].Value);

        second.Inputs[1].Value = 5.0; // editing inside updates every group node
        Assert.Equal(16.0, group.Outputs[0].Value);
        Assert.Equal(106.0, copy.Outputs[0].Value);
    }

    [Fact]
    public void AnErrorInside_ShowsOnTheGroupNode()
    {
        var graph = NewGraph();
        var definition = graph.Groups.Add("Divide");
        var clamp = definition.Graph.AddNode(new ClampNode());
        definition.Graph.Connect(clamp.Outputs[0], definition.OutputNode.InputFor(definition.AddOutput("Result", TestSockets.Float))!);
        var input = definition.AddInput("Value", TestSockets.Float);
        definition.Graph.Connect(definition.InputNode.OutputFor(input)!, clamp.Inputs[0]);
        var node = graph.AddNode(new GroupNodeViewModel(definition));
        node.Inputs[0].Value = double.NaN;
        using var evaluator = new GraphEvaluator(graph, Dispatcher.ImmediateDispatcher.Instance);
        evaluator.EvaluateNow();
        Assert.Equal("Clamp: The value isn't a number.", node.Error);
    }

    [Fact]
    public void ChangingTheInterface_UpdatesEveryNode_AndUndoRestoresLinks()
    {
        var (graph, source, _, _, _, group) = Grouped();
        var definition = group.Definition;
        var copy = graph.AddNode(new GroupNodeViewModel(definition));

        var extra = definition.AddInput("Offset", TestSockets.Float, defaultValue: 3.0);
        Assert.Equal(["A", "Offset"], group.Inputs.Select(i => i.Name));
        Assert.Equal(3.0, copy.Inputs[1].Value);
        Assert.Equal(3, definition.InputNode.Outputs.Count); // with the empty socket at the end
        Assert.True(GroupSockets.IsNewSocket(definition.InputNode.Outputs[^1]));

        definition.Inputs[0].Name = "Value";
        Assert.Equal("Value", group.Inputs[0].Name);
        Assert.Equal("Value", definition.InputNode.Outputs[0].Name);
        definition.Name = "Renamed";
        Assert.Equal("Renamed", copy.Title);

        var link = group.Inputs[0].Link!;
        definition.RemoveSocket(definition.Inputs[0]);
        Assert.Equal(["Offset"], group.Inputs.Select(i => i.Name));
        Assert.DoesNotContain(link, graph.Links);
        Assert.Empty(definition.InputNode.Outputs[0].Links);

        graph.Undo.Undo();
        Assert.Equal(["Value", "Offset"], group.Inputs.Select(i => i.Name));
        Assert.Same(source.Outputs[0], group.Inputs[0].Link?.From);
        Assert.NotEmpty(definition.InputNode.Outputs[0].Links);

        definition.MoveSocket(extra, 0);
        Assert.Equal(["Offset", "Value"], copy.Inputs.Select(i => i.Name));
    }

    [Fact]
    public void AGroupCantEndUpInsideItself()
    {
        var graph = NewGraph();
        var outer = graph.Groups.Add("Outer");
        var inner = graph.Groups.Add("Inner");
        outer.Graph.AddNode(new GroupNodeViewModel(inner));

        Assert.Throws<InvalidOperationException>(() => outer.Graph.AddNode(new GroupNodeViewModel(outer)));
        Assert.Throws<InvalidOperationException>(() => inner.Graph.AddNode(new GroupNodeViewModel(outer)));
        Assert.True(outer.Contains(inner));
        Assert.False(inner.Contains(outer));
        Assert.Throws<InvalidOperationException>(() => new NodeGraphViewModel().AddNode(new GroupNodeViewModel(inner))); // another library
    }

    [Fact]
    public void TheGroupsOwnInputAndOutputNodes_CantBeRemoved()
    {
        var graph = NewGraph();
        var definition = graph.Groups.Add("Group");
        Assert.False(definition.Graph.RemoveNode(definition.InputNode));
        definition.Graph.SelectAll();
        Assert.False(definition.Graph.DeleteSelection());
        Assert.Empty(definition.Graph.Duplicate(definition.Graph.Nodes, default));
        Assert.Equal(2, definition.Graph.Nodes.Count);
        Assert.False(graph.Groups.Remove(definition) && definition.Instances.Any());
    }

    [Fact]
    public void Ungrouping_PutsCopiesOfTheNodesBack()
    {
        var (graph, source, first, _, sink, group) = Grouped();
        first.Inputs[1].Value = 4.0;
        var copies = graph.Groups.Ungroup(group);

        Assert.Equal(2, copies.Count);
        Assert.DoesNotContain(group, graph.Nodes);
        var (a, b) = ((AddNode)copies[0], (AddNode)copies[1]);
        Assert.Same(source.Outputs[0], a.Inputs[0].Link?.From);
        Assert.Same(a.Outputs[0], b.Inputs[0].Link?.From);
        Assert.Same(b.Outputs[0], sink.Inputs[0].Link?.From);
        Assert.Equal(4.0, a.Inputs[1].Value);
        Assert.Single(graph.Groups.Definitions); // the group stays

        graph.Undo.Undo();
        Assert.Contains(group, graph.Nodes);
        Assert.Same(group.Outputs[0], sink.Inputs[0].Link?.From);
    }

    [Fact]
    public void SavingAndLoading_KeepsGroupsAndNestedGroups()
    {
        var (graph, _, first, _, sink, group) = Grouped();
        first.Inputs[1].Value = 1.0;
        var outer = graph.Groups.Add("Outer");
        var inside = outer.Graph.AddNode(new GroupNodeViewModel(group.Definition));
        var outerInput = outer.AddInput("X", TestSockets.Float);
        outer.Graph.Connect(outer.InputNode.OutputFor(outerInput)!, inside.Inputs[0]);
        graph.AddNode(new GroupNodeViewModel(outer));
        string json = NodeGraphSerializer.Save(graph);

        var loaded = NewGraph();
        Assert.Empty(NodeGraphSerializer.Load(loaded, json));
        Assert.Equal(["Chain", "Outer"], loaded.Groups.Definitions.Select(d => d.Name));
        var chain = loaded.Groups.Definitions[0];
        Assert.Equal(4, chain.Graph.Nodes.Count);
        Assert.Equal(3, chain.Graph.Links.Count);
        var loadedGroup = loaded.Nodes.OfType<GroupNodeViewModel>().First(n => n.Definition == chain);
        Assert.Same(loadedGroup.Outputs[0], loaded.Nodes.Single(n => n.Id == sink.Id).Inputs[0].Link?.From);
        var loadedOuter = loaded.Groups.Definitions[1];
        var nested = Assert.Single(loadedOuter.Graph.Nodes.OfType<GroupNodeViewModel>());
        Assert.Same(chain, nested.Definition);
        Assert.Same(loadedOuter.InputNode.Outputs[0], nested.Inputs[0].Link?.From);
        Assert.Equal(1.0, chain.Graph.Nodes.OfType<AddNode>().First().Inputs[1].Value);
    }

    [Fact]
    public void PastingAGroupNode_BringsItsGroup_OrUsesTheOneThere()
    {
        var (graph, _, _, _, _, group) = Grouped();
        string json = NodeGraphSerializer.Copy([group]);

        var pastedHere = Assert.IsType<GroupNodeViewModel>(Assert.Single(NodeGraphSerializer.Paste(graph, json)));
        Assert.Same(group.Definition, pastedHere.Definition);
        Assert.Single(graph.Groups.Definitions);

        var other = NewGraph();
        var pastedThere = Assert.IsType<GroupNodeViewModel>(Assert.Single(NodeGraphSerializer.Paste(other, json)));
        Assert.Equal("Chain", pastedThere.Definition.Name);
        Assert.Same(other.Groups, pastedThere.Definition.Library);
        Assert.Equal(4, pastedThere.Definition.Graph.Nodes.Count);
        Assert.Equal("Paste Chain", other.Undo.UndoName);
        other.Undo.Undo(); // the group goes too
        Assert.Empty(other.Groups.Definitions);
        Assert.Empty(other.Nodes);
    }
}
