using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Nodes;
using static Atelier.Tests.NodeEditorTests;

namespace Atelier.Tests;

public class NodeLinkToolTests : IDisposable
{
    private readonly NodeGraphViewModel _graph = new();
    private readonly NodeViewModel _a;
    private readonly NodeViewModel _b;
    private readonly NodeViewModel _c;
    private readonly LinkViewModel _link;
    private readonly NodeEditor _editor;

    public NodeLinkToolTests()
    {
        _graph.Catalog.Register(new NodeType("math", "Math", "Converter", () => TestSockets.Math()));
        _graph.Catalog.Register("text", "Text", "Input", () =>
        {
            var node = new NodeViewModel("Text");
            node.AddOutput("Text", TestSockets.Text);
            return node;
        });
        _a = _graph.AddNode("math", new Point(20, 20));
        _a.Title = "A";
        _b = _graph.AddNode("math", new Point(330, 20));
        _b.Title = "B";
        _c = _graph.AddNode("math", new Point(100, 260));
        _c.Title = "C";
        _link = _graph.Connect(_a.Outputs[0], _b.Inputs[0])!;
        _graph.Undo.Clear();
        _editor = new NodeEditor { Graph = _graph };
        _editor.AttachToHost();
        LayoutEditor();
        FocusManager.SetFocus(_editor);
    }

    public void Dispose() => _editor.DetachFromHost();

    private void LayoutEditor()
    {
        _editor.Measure(new Size(800, 500));
        _editor.Arrange(new Rect(0, 0, 800, 500));
    }

    private Point At(SocketViewModel socket) => _editor.GraphToView(socket.Anchor);

    private Point OnTitle(NodeViewModel node) => _editor.GraphToView(node.Position + new Point(node.Width / 2, 8));

    private Point Middle(LinkViewModel link) => LinkGeometry.GetPoint(At(link.From), At(link.To), 0.5f, _editor.Zoom);

    private void Drag(Point from, Point to, PointerButtons button = PointerButtons.Left, ModifierKeys modifiers = ModifierKeys.None, bool release = true)
    {
        Press(_editor, from, button, modifiers);
        Move(_editor, new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2 + 1), modifiers);
        Move(_editor, to, modifiers);
        if (release) Release(_editor, to, button, modifiers);
    }

    [Fact]
    public void DroppingAnUnlinkedNodeOnALink_InsertsIt_AndMakesRoom()
    {
        var middle = Middle(_link);
        Drag(OnTitle(_c), middle, release: false);
        Assert.Same(_link, _editor.InsertTarget);
        Release(_editor, middle, PointerButtons.Left);

        Assert.Null(_editor.InsertTarget);
        Assert.Same(_a.Outputs[0], _c.Inputs[0].Link?.From);
        Assert.Same(_c.Outputs[0], _b.Inputs[0].Link?.From);
        Assert.Equal(_c.Position.X + _c.Width + MoveNodesCommand.InsertSpacing, _b.Position.X, 3);
        Assert.Equal("Insert C", _graph.Undo.UndoName);

        _graph.Undo.Undo(); // move, insert and making room together
        Assert.Equal(new Point(100, 260), _c.Position);
        Assert.Equal(new Point(330, 20), _b.Position);
        Assert.Same(_link, _b.Inputs[0].Link);
        Assert.False(_graph.Undo.CanUndo);
    }

    [Fact]
    public void NodesWithLinks_OrWithoutFittingSockets_AreNotInserted()
    {
        _editor.AutoInsert = false;
        Drag(OnTitle(_c), Middle(_link), release: false);
        Assert.Null(_editor.InsertTarget);
        FocusManager.DispatchKeyDown(new KeyEventArgs(Key.Escape), _editor);
        _editor.AutoInsert = true;

        var text = _graph.AddNode("text", new Point(100, 380)); // an output, but no input
        LayoutEditor();
        Drag(OnTitle(text), Middle(_link));
        Assert.Same(_link, _b.Inputs[0].Link);
    }

    [Fact]
    public void AltDrag_DetachesTheNode_AndEscapePutsItBack()
    {
        // A -> C -> B
        Assert.True(_graph.InsertIntoLink(_c, _link));
        _graph.Undo.Clear();
        LayoutEditor();

        Drag(OnTitle(_c), OnTitle(_c) + new Point(0, 80), modifiers: ModifierKeys.Alt, release: false);
        Assert.False(_c.HasLinks);
        Assert.Same(_a.Outputs[0], _b.Inputs[0].Link?.From);
        FocusManager.DispatchKeyDown(new KeyEventArgs(Key.Escape), _editor);
        Assert.Same(_c.Outputs[0], _b.Inputs[0].Link?.From);
        Assert.Same(_a.Outputs[0], _c.Inputs[0].Link?.From);
        Assert.Equal(new Point(100, 260), _c.Position);
        Assert.False(_graph.Undo.CanUndo);

        Drag(OnTitle(_c), OnTitle(_c) + new Point(0, 80), modifiers: ModifierKeys.Alt);
        Assert.False(_c.HasLinks);
        Assert.Equal("Detach C", _graph.Undo.UndoName);
        _graph.Undo.Undo();
        Assert.Same(_c.Outputs[0], _b.Inputs[0].Link?.From);
        Assert.Equal(new Point(100, 260), _c.Position);
    }

    [Fact]
    public void DroppingANewLinkOnEmptySpace_OffersTheNodesItCanConnectTo()
    {
        Press(_editor, At(_b.Inputs[1]), PointerButtons.Left);
        Move(_editor, new Point(600, 400));
        Release(_editor, new Point(700, 420), PointerButtons.Left);
        // The menu is open with the node types that have an output for a float input.
        var menu = FocusManager.GetFocusedElement(_editor) is { } focused ? FindMenu(focused) : null;
        Assert.NotNull(menu);
        Assert.Equal(["math"], menu!.Results.Select(t => t.Id));

        menu.Pick(menu.Results[0]);
        var added = _graph.Nodes[^1];
        Assert.Same(added.Outputs[0], _b.Inputs[1].Link?.From);
        Assert.Equal(_editor.ViewToGraph(new Point(700, 420)).X - added.Width, added.Position.X, 3); // left of the drop
        Assert.Equal("Add Math", _graph.Undo.UndoName);
        _graph.Undo.Undo();
        Assert.DoesNotContain(added, _graph.Nodes);
        Assert.Null(_b.Inputs[1].Link);
    }

    private static AddNodeMenu? FindMenu(UIElement element)
    {
        for (VisualNode? node = element; node != null; node = node.Parent)
        {
            if (node is AddNodeMenu menu) return menu;
        }
        return null;
    }

    [Fact]
    public void CtrlRightDragging_CutsTheLinksItCrosses()
    {
        var other = _graph.Connect(_a.Outputs[0], _c.Inputs[1])!;
        _graph.Undo.Clear();
        LayoutEditor();
        var middle = Middle(_link);
        Drag(middle + new Point(0, -30), middle + new Point(0, 30), PointerButtons.Right, ModifierKeys.Control, release: false);
        Assert.NotNull(_editor.Stroke);
        Release(_editor, middle + new Point(0, 30), PointerButtons.Right, ModifierKeys.Control);

        Assert.Null(_editor.Stroke);
        Assert.Equal([other], _graph.Links);
        Assert.Equal("Cut link", _graph.Undo.UndoName);
    }

    [Fact]
    public void ShiftRightDragging_AddsRerouteDots_ThatPassValuesThrough()
    {
        var middle = Middle(_link);
        Drag(middle + new Point(0, -30), middle + new Point(0, 30), PointerButtons.Right, ModifierKeys.Shift);

        var reroute = Assert.IsType<RerouteNodeViewModel>(_graph.Nodes[^1]);
        Assert.Same(reroute.Outputs[0], _b.Inputs[0].Link?.From);
        Assert.Same(_a.Outputs[0], reroute.Inputs[0].Link?.From);
        Assert.Equal(middle.X, _editor.GraphToView(reroute.Center).X, 1);
        Assert.Equal("Add reroute", _graph.Undo.UndoName);
        LayoutEditor();
        Assert.Equal(reroute.Center, reroute.Inputs[0].Anchor);
        Assert.Equal(reroute.Center, reroute.Outputs[0].Anchor);
        Assert.True(_editor.GetNodeView(reroute)!.IsReroute);

        // Dragging it moves it rather than picking up its link.
        var center = _editor.GraphToView(reroute.Center);
        Drag(center, center + new Point(0, 40));
        Assert.Same(reroute.Outputs[0], _b.Inputs[0].Link?.From);
        Assert.Equal(40, _editor.GraphToView(reroute.Center).Y - center.Y, 1);

        // Values pass through, and Ctrl+X removes it, reconnecting A to B.
        var evaluator = new GraphEvaluator(_graph, Atelier.Core.Threading.Dispatcher.ImmediateDispatcher.Instance);
        _a.Outputs[0].Value = 3.0;
        evaluator.EvaluateNow();
        Assert.Equal(3.0, _b.Inputs[0].EffectiveValue);
        evaluator.Dispose();

        _editor.SelectOnly(reroute);
        FocusManager.DispatchKeyDown(new KeyEventArgs(Key.X, modifiers: ModifierKeys.Control), _editor);
        Assert.DoesNotContain(reroute, _graph.Nodes);
        Assert.Same(_a.Outputs[0], _b.Inputs[0].Link?.From);
    }
}

public class NodeLinkModelTests
{
    [Fact]
    public void Detach_ConnectsWhatFedTheNode_ToWhatItFed()
    {
        var graph = new NodeGraphViewModel();
        var a = graph.AddNode(TestSockets.Math("A"));
        var b = graph.AddNode(TestSockets.Math("B"));
        var c = graph.AddNode(TestSockets.Math("C"));
        var d = graph.AddNode(TestSockets.Math("D"));
        graph.Connect(a.Outputs[0], b.Inputs[0]);
        graph.Connect(b.Outputs[0], c.Inputs[1]);
        graph.Connect(b.Outputs[0], d.Inputs[0]);

        Assert.True(graph.Detach(b));
        Assert.False(b.HasLinks);
        Assert.Same(a.Outputs[0], c.Inputs[1].Link?.From);
        Assert.Same(a.Outputs[0], d.Inputs[0].Link?.From);
        Assert.Equal("Detach B", graph.Undo.UndoName);
        Assert.False(graph.Detach(b));

        graph.Undo.Undo();
        Assert.Same(b.Outputs[0], c.Inputs[1].Link?.From);
        Assert.Equal(3, graph.Links.Count);
    }

    [Fact]
    public void UndoGroups_CanBeRenamed_OrDiscarded()
    {
        var undo = new UndoStack();
        int value = 0;
        var group = undo.Group("Move");
        undo.RecordChange("Set", "v", value, 5, v => value = v);
        value = 5;
        group.Name = "Insert";
        group.Dispose();
        Assert.Equal("Insert", undo.UndoName);

        var discarded = undo.Group("Drag");
        undo.RecordChange("Set", "v", value, 9, v => value = v);
        value = 9;
        discarded.Discard();
        Assert.Equal(5, value); // reverted
        Assert.Equal("Insert", undo.UndoName);
        Assert.False(undo.CanRedo);
    }

    [Fact]
    public void TryIntersect_FindsWhereAStrokeCrossesALink()
    {
        var start = new Point(0, 0);
        var end = new Point(200, 0);
        Assert.True(LinkGeometry.TryIntersect([new Point(100, -10), new Point(100, 10)], start, end, 1, out var hit));
        Assert.Equal(100, hit.X, 1);
        Assert.Equal(0, hit.Y, 1);
        Assert.False(LinkGeometry.TryIntersect([new Point(100, 10), new Point(100, 30)], start, end, 1, out _));
    }
}

public class NodeEditorGestureConflictTests
{
    [Fact]
    public void TheDefaultGestures_DontConflict_ButRebindingPanOntoTheRerouteToolDoes()
    {
        _ = new NodeEditor(); // registers the commands
        try
        {
            Assert.DoesNotContain(Atelier.Core.Keybinding.KeybindingManager.GetConflicts(), c => c.Group == NodeEditor.CommandGroup);

            Atelier.Core.Keybinding.KeybindingManager.SetCustomization(NodeEditor.CommandGroup, "Pan",
                new Atelier.Core.Keybinding.CommandCustomization(Keybinding: "Shift+RightDrag"));
            var conflict = Assert.Single(Atelier.Core.Keybinding.KeybindingManager.GetConflicts(), c => c.Group == NodeEditor.CommandGroup);
            Assert.Equal("AddReroute", conflict.First.Name);
            Assert.Equal("Pan", conflict.Second.Name);
        }
        finally
        {
            Atelier.Core.Keybinding.KeybindingManager.ClearCustomizations();
        }
    }
}
