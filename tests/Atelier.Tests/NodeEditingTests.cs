using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Nodes;
using static Atelier.Tests.NodeEditorTests;

namespace Atelier.Tests;

public class NodeEditingTests : IDisposable
{
    private readonly NodeGraphViewModel _graph = new();
    private readonly NodeViewModel _a;
    private readonly NodeViewModel _b;
    private readonly NodeEditor _editor;

    public NodeEditingTests()
    {
        _graph.Catalog.Register(new NodeType("math", "Math", "Converter", () => TestSockets.Math()) { Keywords = ["add"] });
        _graph.Catalog.Register("value", "Value", "Input", () =>
        {
            var node = new NodeViewModel("Value");
            node.AddOutput("Value", TestSockets.Float);
            return node;
        });
        _a = _graph.AddNode("math", new Point(20, 20));
        _a.Title = "A";
        _b = _graph.AddNode("math", new Point(300, 40));
        _b.Title = "B";
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

    // A point on a node's title bar.
    private Point OnTitle(NodeViewModel node) => _editor.GraphToView(node.Position + new Point(node.Width / 2, 8));

    private void Click(Point at, ModifierKeys modifiers = ModifierKeys.None)
    {
        Press(_editor, at, PointerButtons.Left, modifiers);
        Release(_editor, at, PointerButtons.Left);
    }

    private void Drag(Point from, Point to, ModifierKeys modifiers = ModifierKeys.None, bool release = true)
    {
        Press(_editor, from, PointerButtons.Left, modifiers);
        Move(_editor, new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2), modifiers);
        Move(_editor, to, modifiers);
        if (release) Release(_editor, to, PointerButtons.Left);
    }

    private void Key(Key key, ModifierKeys modifiers = ModifierKeys.None) =>
        FocusManager.DispatchKeyDown(new KeyEventArgs(key, modifiers: modifiers), _editor);

    [Fact]
    public void Clicking_SelectsANodeOrLink_AndTheBackgroundClears()
    {
        Click(OnTitle(_a));
        Assert.True(_a.IsSelected);
        Assert.Same(_a, _graph.Nodes[^1]); // brought to front

        Click(OnTitle(_b));
        Assert.Equal([_b], _graph.SelectedNodes);

        Click(OnTitle(_a), ModifierKeys.Shift);
        Assert.Equal(2, _graph.SelectedNodes.Count());
        Click(OnTitle(_a), ModifierKeys.Shift);
        Assert.Equal([_b], _graph.SelectedNodes);

        var link = _graph.Connect(_a.Outputs[0], _b.Inputs[0])!;
        _graph.Undo.Clear();
        LayoutEditor();
        var onLink = LinkGeometry.GetPoint(At(link.From), At(link.To), 0.5f, _editor.Zoom);
        Click(onLink);
        Assert.True(link.IsSelected);
        Assert.Empty(_graph.SelectedNodes);

        Click(new Point(700, 450));
        Assert.False(link.IsSelected);
        Assert.False(_graph.Undo.CanUndo); // selection isn't undoable
    }

    [Fact]
    public void DraggingANode_MovesTheSelection_AsOneUndoStep()
    {
        _a.IsSelected = _b.IsSelected = true;
        Drag(OnTitle(_a), OnTitle(_a) + new Point(30, 10));
        Assert.Equal(new Point(50, 30), _a.Position);
        Assert.Equal(new Point(330, 50), _b.Position);
        Assert.Equal("Move", _graph.Undo.UndoName);

        _graph.Undo.Undo();
        Assert.Equal(new Point(20, 20), _a.Position);
        Assert.Equal(new Point(300, 40), _b.Position);
        Assert.False(_graph.Undo.CanUndo);

        // An unselected node is selected alone and moved.
        _graph.ClearSelection();
        Drag(OnTitle(_b), OnTitle(_b) + new Point(-10, 0));
        Assert.Equal([_b], _graph.SelectedNodes);
        Assert.Equal(new Point(290, 40), _b.Position);
        Assert.Equal("Move B", _graph.Undo.UndoName);
    }

    [Fact]
    public void EscapePutsMovedNodesBack_AndCtrlSnaps()
    {
        Drag(OnTitle(_a), OnTitle(_a) + new Point(55, 5), release: false);
        Assert.Equal(new Point(75, 25), _a.Position);
        Key(Atelier.Core.Events.Key.Escape);
        Assert.Equal(new Point(20, 20), _a.Position);
        Assert.False(_graph.Undo.CanUndo);

        // Ctrl held while moving snaps.
        var start = OnTitle(_a);
        Press(_editor, start, PointerButtons.Left);
        Move(_editor, start + new Point(10, 0), ModifierKeys.Control);
        Move(_editor, start + new Point(33, 8), ModifierKeys.Control);
        Release(_editor, start + new Point(33, 8), PointerButtons.Left, ModifierKeys.Control);
        Assert.Equal(new Point(60, 20), _a.Position); // (53, 28) snapped to 20
    }

    [Fact]
    public void BoxSelection_ReplacesExtendsOrSubtracts()
    {
        Drag(new Point(5, 5), OnTitle(_a) + new Point(0, 20));
        Assert.Equal([_a], _graph.SelectedNodes);
        Assert.Null(_editor.SelectionBox);

        Drag(new Point(600, 5), OnTitle(_b), ModifierKeys.Shift);
        Assert.Equal(2, _graph.SelectedNodes.Count());

        Drag(new Point(5, 5), OnTitle(_a) + new Point(0, 20), ModifierKeys.Control);
        Assert.Equal([_b], _graph.SelectedNodes);

        Drag(new Point(5, 400), new Point(700, 5), release: false); // live preview selects both
        Assert.Equal(2, _graph.SelectedNodes.Count());
        Assert.NotNull(_editor.SelectionBox);
        Key(Atelier.Core.Events.Key.Escape);
        Assert.Equal([_b], _graph.SelectedNodes);
    }

    [Fact]
    public void DraggingBetweenSockets_ConnectsThem_InEitherDirection()
    {
        Drag(At(_a.Outputs[0]), At(_b.Inputs[1]) + new Point(6, 3)); // near enough snaps to it
        Assert.Same(_a.Outputs[0], _b.Inputs[1].Link?.From);
        Assert.Equal("Connect", _graph.Undo.UndoName);

        Drag(At(_b.Inputs[0]), At(_a.Outputs[0]));
        Assert.Same(_a.Outputs[0], _b.Inputs[0].Link?.From);

        // A cycle or an empty spot connects nothing.
        int before = _graph.Links.Count;
        Drag(At(_b.Outputs[0]), At(_a.Inputs[0]));
        Drag(At(_a.Outputs[0]), new Point(700, 450));
        Assert.Equal(before, _graph.Links.Count);
        Assert.Null(_editor.DraggedFrom);
    }

    [Fact]
    public void DraggingFromAConnectedInput_MovesOrRemovesItsLink()
    {
        var link = _graph.Connect(_a.Outputs[0], _b.Inputs[0])!;
        _graph.Undo.Clear();
        LayoutEditor();

        Drag(At(_b.Inputs[0]), At(_b.Inputs[0]) + new Point(1, 5)); // dropped back: nothing changes
        Assert.Same(link, _b.Inputs[0].Link);
        Assert.False(_graph.Undo.CanUndo);

        Drag(At(_b.Inputs[0]), new Point(200, 200), release: false);
        Assert.Same(link, _editor.HiddenLink);
        Release(_editor, new Point(200, 200), PointerButtons.Left);
        Assert.Empty(_graph.Links);
        Assert.Null(_editor.HiddenLink);

        _graph.Undo.Undo();
        Drag(At(_b.Inputs[0]), At(_b.Inputs[1]));
        Assert.Null(_b.Inputs[0].Link);
        Assert.Same(_a.Outputs[0], _b.Inputs[1].Link?.From);
        Assert.Equal("Reconnect", _graph.Undo.UndoName);
        _graph.Undo.Undo();
        Assert.Same(link, _b.Inputs[0].Link);
    }

    [Fact]
    public void KeyCommands_DeleteDuplicateCollapseMuteAndUndo()
    {
        var link = _graph.Connect(_a.Outputs[0], _b.Inputs[0])!;
        _b.IsSelected = true;

        Key(Atelier.Core.Events.Key.D, ModifierKeys.Shift);
        var copy = Assert.Single(_graph.SelectedNodes);
        Assert.NotSame(_b, copy);
        Assert.Equal("B", copy.Title);
        Assert.Equal(_b.Position + NodeEditorCommands.DuplicateOffset, copy.Position);
        Assert.Same(_a.Outputs[0], copy.Inputs[0].Link?.From); // the link into it is copied

        Key(Atelier.Core.Events.Key.H);
        Key(Atelier.Core.Events.Key.M);
        Assert.True(copy.IsCollapsed && copy.IsMuted);
        Key(Atelier.Core.Events.Key.M);
        Assert.False(copy.IsMuted);

        Key(Atelier.Core.Events.Key.Delete);
        Assert.DoesNotContain(copy, _graph.Nodes);
        Key(Atelier.Core.Events.Key.Z, ModifierKeys.Control);
        Assert.Contains(copy, _graph.Nodes);
        Key(Atelier.Core.Events.Key.Z, ModifierKeys.Control | ModifierKeys.Shift);
        Assert.DoesNotContain(copy, _graph.Nodes);

        Key(Atelier.Core.Events.Key.A);
        Assert.Equal(2, _graph.SelectedNodes.Count());
        Key(Atelier.Core.Events.Key.A, ModifierKeys.Alt);
        Assert.Empty(_graph.SelectedNodes);

        link.IsSelected = true;
        Key(Atelier.Core.Events.Key.Delete);
        Assert.Empty(_graph.Links);
        Assert.Equal(2, _graph.Nodes.Count);
    }

    [Fact]
    public void TypingInANodesTextBox_DoesntRunTheEditorsShortcuts()
    {
        _a.IsSelected = true;
        var textBox = (TextBox)_editor.GetNodeView(_a)!.GetInputEditor(_a.Inputs[0])!;
        FocusManager.SetFocus(textBox);
        FocusManager.DispatchKeyDown(new KeyEventArgs(Atelier.Core.Events.Key.M), _editor);
        FocusManager.DispatchKeyDown(new KeyEventArgs(Atelier.Core.Events.Key.Delete), _editor);
        Assert.False(_a.IsMuted);
        Assert.Contains(_a, _graph.Nodes);
    }

    [Fact]
    public void TheAddMenu_SearchesTheCatalog_AndAddsThePickedNodeWhereItOpened()
    {
        var menu = _editor.ShowAddNodeMenu(new Point(400, 300))!;
        Assert.Equal(["math", "value"], menu.Results.Select(t => t.Id));
        menu.SearchText = "val";
        Assert.Equal("value", menu.HighlightedType?.Id);

        menu.OnPreviewKeyDown(new KeyEventArgs(Atelier.Core.Events.Key.Enter));
        var added = _graph.Nodes[^1];
        Assert.Equal("value", added.TypeId);
        Assert.Equal(_editor.ViewToGraph(new Point(400, 300)), added.Position);
        Assert.Equal([added], _graph.SelectedNodes);
        Assert.Equal("Add Value", _graph.Undo.UndoName);
    }

    [Fact]
    public void TheContextMenu_OffersTheCatalogAndTheCommands()
    {
        var menu = _editor.CreateContextMenu(new Point(100, 100));
        var add = Assert.IsType<MenuItem>(menu.Items[0]);
        Assert.Equal(["Converter", "Input"], add.Items.OfType<MenuItem>().Select(i => i.Header));

        var delete = menu.Items.OfType<MenuItem>().Single(i => i.Command == NodeEditorCommands.Delete);
        Assert.Same(_editor, delete.CommandParameter);
        Assert.False(NodeEditorCommands.Delete.CanExecute(_editor));
        _a.IsSelected = true;
        Assert.True(NodeEditorCommands.Delete.CanExecute(_editor));

        ((MenuItem)((MenuItem)add.Items[1]).Items[0]).Activate();
        Assert.Equal("value", _graph.Nodes[^1].TypeId);
    }
}

public class NodeGraphEditingTests
{
    [Fact]
    public void Duplicate_CopiesLinksBetweenAndIntoTheCopies()
    {
        var graph = new NodeGraphViewModel();
        var source = graph.AddNode(TestSockets.Math("Source"));
        var a = graph.AddNode(TestSockets.Math("A"));
        var b = graph.AddNode(TestSockets.Math("B"));
        graph.Connect(source.Outputs[0], a.Inputs[0]);
        graph.Connect(a.Outputs[0], b.Inputs[1]);
        a.Inputs[1].Value = 7.0;

        var copies = graph.Duplicate([a, b], new Point(10, 10));
        var (ca, cb) = (copies[0], copies[1]);
        Assert.Equal(7.0, ca.Inputs[1].Value);
        Assert.Same(source.Outputs[0], ca.Inputs[0].Link?.From);
        Assert.Same(ca.Outputs[0], cb.Inputs[1].Link?.From);
        Assert.Equal(3, source.Outputs[0].Links.Count + a.Outputs[0].Links.Count);
        Assert.Equal("Duplicate", graph.Undo.UndoName);

        graph.Undo.Undo();
        Assert.Equal(3, graph.Nodes.Count);
        Assert.Equal(2, graph.Links.Count);
    }

    [Fact]
    public void UndoingAnAdd_RemovesThatNode_EvenAfterReordering()
    {
        var graph = new NodeGraphViewModel();
        var a = graph.AddNode(TestSockets.Math("A"));
        var b = graph.AddNode(TestSockets.Math("B"));
        graph.BringToFront(a);
        Assert.Equal([b, a], graph.Nodes);

        graph.Undo.Undo(); // "Add B"
        Assert.Equal([a], graph.Nodes);
        graph.Undo.Redo();
        Assert.Equal([a, b], graph.Nodes);
    }

    [Fact]
    public void Copy_UsesTheCatalog_OrTheParameterlessConstructor()
    {
        var graph = new NodeGraphViewModel();
        graph.Catalog.Register("math", "Math", "", () => TestSockets.Math());
        var fromCatalog = graph.AddNode("math", new Point(5, 5));
        fromCatalog.IsCollapsed = true;
        var copy = fromCatalog.Copy();
        Assert.Equal("math", copy.TypeId);
        Assert.True(copy.IsCollapsed);
        Assert.Null(copy.Graph);

        var computing = new ClampNode { Minimum = 2 };
        Assert.IsType<ClampNode>(computing.Copy());
    }

    [Fact]
    public void Suspend_KeepsChangesOutOfTheHistory()
    {
        var undo = new UndoStack();
        using (undo.Suspend())
        {
            undo.Push(new DelegateUndoAction("Ignored", () => { }, () => { }));
            undo.RecordChange("Ignored", "key", 1, 2, _ => { });
            Assert.True(undo.IsSuspended);
        }
        Assert.False(undo.IsSuspended);
        Assert.False(undo.CanUndo);
    }
}
