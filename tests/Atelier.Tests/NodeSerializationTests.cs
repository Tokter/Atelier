using System.Text.Json.Nodes;
using Atelier.Core.Events;
using Atelier.Core.Platform;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Nodes;

namespace Atelier.Tests;

/// <summary>A node with a setting of its own, saved with WriteState.</summary>
internal sealed class ModeNode : NodeViewModel
{
    public ModeNode() : base("Mode")
    {
        AddInput("In", TestSockets.Float);
        AddOutput("Out", TestSockets.Float);
    }

    public string Mode { get; set; } = "add";

    public override void WriteState(JsonObject state) => state["mode"] = Mode;

    public override void ReadState(JsonObject state) => Mode = (string?)state["mode"] ?? "add";
}

public class NodeSerializationTests
{
    private static readonly SocketType Paint = new SocketType("paint", "Paint", Color.White, typeof(Color), Color.Black)
        .SetSerialization(v => ((Color)v!).ToString(), json => Color.FromHex((string)json!));

    private static NodeGraphViewModel NewGraph()
    {
        var graph = new NodeGraphViewModel();
        graph.Catalog.Register("math", "Math", "Converter", () => TestSockets.Math());
        graph.Catalog.Register("mode", "Mode", "Converter", () => new ModeNode());
        graph.Catalog.Register("paint", "Paint", "Color", () =>
        {
            var node = new NodeViewModel("Paint");
            node.AddInput("Color", Paint);
            node.AddOutput("Color", Paint);
            return node;
        });
        return graph;
    }

    [Fact]
    public void SavingAndLoading_KeepsNodesValuesSettingsAndLinks()
    {
        var graph = NewGraph();
        var a = graph.AddNode("math", new Point(10, 20));
        a.Title = "First";
        a.Inputs[1].Value = 2.5;
        a.HeaderColor = Color.FromRgb(1, 2, 3);
        a.IsMuted = true;
        var mode = (ModeNode)graph.AddNode("mode", new Point(200, 20));
        mode.Mode = "multiply";
        mode.IsCollapsed = true;
        mode.Width = 180;
        var paint = graph.AddNode("paint", new Point(0, 300));
        paint.Inputs[0].Value = Color.FromRgb(0x12, 0x34, 0x56);
        graph.Connect(a.Outputs[0], mode.Inputs[0]);
        var reroute = graph.InsertReroute(mode.Inputs[0].Link!, new Point(150, 100))!;

        string json = NodeGraphSerializer.Save(graph, out var saveProblems);
        Assert.Empty(saveProblems);

        var loaded = NewGraph();
        loaded.AddNode("math", default); // replaced
        Assert.Empty(NodeGraphSerializer.Load(loaded, json));
        Assert.False(loaded.Undo.CanUndo);
        Assert.Equal(4, loaded.Nodes.Count);
        Assert.Equal(2, loaded.Links.Count);

        var la = loaded.Nodes.Single(n => n.Id == a.Id);
        Assert.Equal(("First", new Point(10, 20), 2.5, true), (la.Title, la.Position, la.Inputs[1].Value, la.IsMuted));
        Assert.Equal(Color.FromRgb(1, 2, 3), la.HeaderColor);
        var lm = (ModeNode)loaded.Nodes.Single(n => n.Id == mode.Id);
        Assert.Equal(("multiply", true, 180f), (lm.Mode, lm.IsCollapsed, lm.Width));
        Assert.Equal(Color.FromRgb(0x12, 0x34, 0x56), loaded.Nodes.Single(n => n.Id == paint.Id).Inputs[0].Value);
        var lr = Assert.IsType<RerouteNodeViewModel>(loaded.Nodes.Single(n => n.Id == reroute.Id));
        Assert.Equal(reroute.Center, lr.Center);
        Assert.Same(la.Outputs[0], lr.Inputs[0].Link?.From);
        Assert.Same(lr.Outputs[0], lm.Inputs[0].Link?.From);
    }

    [Fact]
    public void Duplicating_KeepsANodesOwnSettings()
    {
        var graph = NewGraph();
        var mode = (ModeNode)graph.AddNode("mode", default);
        mode.Mode = "divide";
        var copy = (ModeNode)Assert.Single(graph.Duplicate([mode], new Point(10, 10)));
        Assert.Equal("divide", copy.Mode);
    }

    [Fact]
    public void UnknownTypesAndUntypedNodes_AreReported()
    {
        var graph = NewGraph();
        graph.AddNode("math", default);
        graph.AddNode(new NodeViewModel("Plain")); // no type id
        string json = NodeGraphSerializer.Save(graph, out var problems);
        Assert.Contains(problems, p => p.Contains("Plain"));

        var other = new NodeGraphViewModel(); // knows no types
        var loadProblems = NodeGraphSerializer.Load(other, json);
        Assert.Empty(other.Nodes);
        Assert.Contains(loadProblems, p => p.Contains("math"));

        Assert.Throws<FormatException>(() => NodeGraphSerializer.Load(other, "{\"nodes\":[]}"));
        Assert.False(NodeGraphSerializer.IsGraph("hello"));
        Assert.True(NodeGraphSerializer.IsGraph(json));
    }

    [Fact]
    public void Paste_AddsCopiesWithTheirLinks_AtAPoint_AsOneStep()
    {
        var graph = NewGraph();
        var a = graph.AddNode("math", new Point(100, 100));
        var b = graph.AddNode("math", new Point(300, 150));
        var outside = graph.AddNode("math", new Point(0, 0));
        graph.Connect(a.Outputs[0], b.Inputs[0]);
        graph.Connect(outside.Outputs[0], a.Inputs[0]); // not copied: it comes from outside
        string json = NodeGraphSerializer.Copy([a, b]);

        var pasted = NodeGraphSerializer.Paste(graph, json, new Point(500, 500));
        Assert.Equal(2, pasted.Count);
        Assert.Equal(new Point(500, 500), pasted[0].Position);
        Assert.Equal(new Point(700, 550), pasted[1].Position);
        Assert.Same(pasted[0].Outputs[0], pasted[1].Inputs[0].Link?.From);
        Assert.Null(pasted[0].Inputs[0].Link);
        Assert.NotEqual(a.Id, pasted[0].Id);
        Assert.Equal(pasted, graph.SelectedNodes);
        Assert.Equal("Paste", graph.Undo.UndoName);

        graph.Undo.Undo();
        Assert.Equal(3, graph.Nodes.Count);
    }

    [Fact]
    public void CtrlCAndCtrlV_CopyAndPasteTheSelection()
    {
        var saved = Clipboard.Current;
        Clipboard.Current = new Clipboard.NullClipboard();
        var graph = NewGraph();
        var a = graph.AddNode("math", new Point(10, 10));
        var editor = new NodeEditor { Graph = graph };
        editor.AttachToHost();
        try
        {
            FocusManager.SetFocus(editor);
            Assert.False(NodeEditorCommands.Paste.CanExecute(editor));
            a.IsSelected = true;
            FocusManager.DispatchKeyDown(new KeyEventArgs(Key.C, modifiers: ModifierKeys.Control), editor);
            Assert.True(NodeGraphSerializer.IsGraph(Clipboard.GetText()));

            FocusManager.DispatchKeyDown(new KeyEventArgs(Key.V, modifiers: ModifierKeys.Control), editor);
            Assert.Equal(2, graph.Nodes.Count);
            Assert.Equal(new Point(30, 30), graph.Nodes[1].Position); // no pointer: 20 units down and right
            Assert.Equal([graph.Nodes[1]], graph.SelectedNodes);
        }
        finally
        {
            editor.DetachFromHost();
            Clipboard.Current = saved;
        }
    }
}
