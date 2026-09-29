using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Nodes;
using static Atelier.Tests.NodeEditorTests;

namespace Atelier.Tests;

public class NodeGroupEditingTests : IDisposable
{
    private readonly NodeGraphViewModel _graph = new();
    private readonly AddNode _source;
    private readonly AddNode _first;
    private readonly AddNode _second;
    private readonly AddNode _sink;
    private readonly NodeEditor _editor;

    public NodeGroupEditingTests()
    {
        _graph.Catalog.Register("add", "Add", "Math", () => new AddNode());
        _source = (AddNode)_graph.AddNode("add", new Point(20, 20));
        _first = (AddNode)_graph.AddNode("add", new Point(220, 20));
        _second = (AddNode)_graph.AddNode("add", new Point(420, 20));
        _sink = (AddNode)_graph.AddNode("add", new Point(620, 20));
        _graph.Connect(_source.Outputs[0], _first.Inputs[0]);
        _graph.Connect(_first.Outputs[0], _second.Inputs[0]);
        _graph.Connect(_second.Outputs[0], _sink.Inputs[0]);
        _graph.Undo.Clear();
        _editor = new NodeEditor { Graph = _graph };
        _editor.AttachToHost();
        LayoutEditor();
        FocusManager.SetFocus(_editor);
    }

    public void Dispose() => _editor.DetachFromHost();

    private void LayoutEditor()
    {
        _editor.Measure(new Size(1000, 600));
        _editor.Arrange(new Rect(0, 0, 1000, 600));
    }

    private void Key(Key key, ModifierKeys modifiers = ModifierKeys.None) =>
        FocusManager.DispatchKeyDown(new KeyEventArgs(key, modifiers: modifiers), _editor);

    private GroupNodeViewModel MakeGroup()
    {
        _first.IsSelected = _second.IsSelected = true;
        Key(Atelier.Core.Events.Key.G, ModifierKeys.Control);
        return Assert.IsType<GroupNodeViewModel>(Assert.Single(_graph.SelectedNodes));
    }

    [Fact]
    public void CtrlG_MakesAGroupOfTheSelection_AndCtrlAltGUngroupsIt()
    {
        var group = MakeGroup();
        Assert.Equal([_source, _sink, group], _graph.Nodes);
        Assert.Equal("Group", group.Definition.Name);

        Key(Atelier.Core.Events.Key.G, ModifierKeys.Control | ModifierKeys.Alt);
        Assert.DoesNotContain(group, _graph.Nodes);
        Assert.Equal(2, _graph.SelectedNodes.Count());
        Assert.Equal(4, _graph.Nodes.Count);
    }

    [Fact]
    public void TabEntersTheSelectedGroup_AndLeavesItAgain()
    {
        var group = MakeGroup();
        _editor.Zoom = 1.5f;
        Key(Atelier.Core.Events.Key.Tab);

        Assert.Same(group.Definition.Graph, _editor.CurrentGraph);
        Assert.Equal([group], _editor.GroupPath);
        Assert.Same(group, group.Definition.InspectedInstance);
        Assert.Contains(_first, _editor.NodeViews.Select(v => v.Node));
        LayoutEditor();

        Key(Atelier.Core.Events.Key.Tab); // nothing selected inside: leave
        Assert.Same(_graph, _editor.CurrentGraph);
        Assert.Empty(_editor.GroupPath);
        Assert.Equal(1.5f, _editor.Zoom);
        Assert.Equal([group], _graph.SelectedNodes);
        Assert.Null(group.Definition.InspectedInstance);

        _editor.EnterGroup(group);
        Key(Atelier.Core.Events.Key.Tab, ModifierKeys.Control);
        Assert.Same(_graph, _editor.CurrentGraph);
    }

    [Fact]
    public void EditingInsideAGroup_IsPartOfTheOneHistory_AndUndoingItsCreationLeavesIt()
    {
        var group = MakeGroup();
        _editor.EnterGroup(group);
        _first.IsSelected = true;
        Key(Atelier.Core.Events.Key.M);
        Assert.True(_first.IsMuted);

        Key(Atelier.Core.Events.Key.Z, ModifierKeys.Control); // unmute
        Assert.False(_first.IsMuted);
        Key(Atelier.Core.Events.Key.Z, ModifierKeys.Control); // un-make the group: the editor leaves it
        Assert.Same(_graph, _editor.CurrentGraph);
        Assert.Contains(_first, _graph.Nodes);
        Assert.Empty(_graph.Groups.Definitions);
    }

    [Fact]
    public void LinksOntoTheEmptySockets_AddGroupInputsAndOutputs()
    {
        var group = MakeGroup();
        var definition = group.Definition;
        _editor.EnterGroup(group);
        LayoutEditor();
        Point At(SocketViewModel socket) => _editor.GraphToView(socket.Anchor);

        // From an input inside to the Group Input's empty socket.
        Press(_editor, At(_first.Inputs[1]), PointerButtons.Left);
        Move(_editor, At(_first.Inputs[1]) + new Point(-40, 10));
        Move(_editor, At(definition.InputNode.NewSocket!));
        Release(_editor, At(definition.InputNode.NewSocket!), PointerButtons.Left);
        Assert.Equal(["A", "B"], definition.Inputs.Select(s => s.Name));
        Assert.Same(definition.InputNode.OutputFor(definition.Inputs[1]), _first.Inputs[1].Link?.From);
        Assert.Equal(2, group.Inputs.Count);
        Assert.Equal("Add group input", _graph.Undo.UndoName);
        LayoutEditor();

        // From an output inside to the Group Output's empty socket.
        Press(_editor, At(_first.Outputs[0]), PointerButtons.Left);
        Move(_editor, At(_first.Outputs[0]) + new Point(40, 10));
        Move(_editor, At(definition.OutputNode.NewSocket!));
        Release(_editor, At(definition.OutputNode.NewSocket!), PointerButtons.Left);
        Assert.Equal(2, definition.Outputs.Count);
        Assert.Same(_first.Outputs[0], definition.OutputNode.InputFor(definition.Outputs[1])?.Link?.From);
        Assert.Equal(2, group.Outputs.Count);

        _graph.Undo.Undo();
        Assert.Single(definition.Outputs);
    }

    [Fact]
    public void TheAddMenuOffersGroups_ButNotInsideThemselves()
    {
        var group = MakeGroup();
        Assert.Contains(_editor.CreateMenuCatalog().Types, t => t.Category == "Groups" && t.Title == "Group");
        var added = _editor.AddNode(_editor.CreateMenuCatalog().Types.Single(t => t.Category == "Groups"), new Point(100, 300));
        Assert.Same(group.Definition, Assert.IsType<GroupNodeViewModel>(added).Definition);

        _editor.EnterGroup(group);
        Assert.DoesNotContain(_editor.CreateMenuCatalog().Types, t => t.Category == "Groups");
        Assert.NotNull(_editor.CreateContextMenu(default).Items.OfType<MenuItem>().SingleOrDefault(i => i.Command == NodeEditorCommands.ExitGroup));
    }

    [Fact]
    public void TheInterfacePanel_RenamesAndRemovesSockets()
    {
        var group = MakeGroup();
        _editor.EnterGroup(group);
        var panel = FindAll<GroupInterfacePanel>(_editor).Single();
        Assert.Same(group.Definition, panel.Definition);

        var boxes = FindAll<TextBox>(panel).ToList(); // the group's name, then the sockets' names
        boxes[1].Text = "Value";
        boxes[1].OnLostFocus();
        Assert.Equal("Value", group.Definition.Inputs[0].Name);
        Assert.Equal("Value", group.Inputs[0].Name);

        boxes[0].Text = "Chain";
        boxes[0].OnLostFocus();
        Assert.Equal("Chain", group.Title);

        _editor.ShowGroupInterface = false;
        Assert.Equal(Visibility.Collapsed, ((UIElement)panel.Parent!).Visibility);
    }

    private static IEnumerable<T> FindAll<T>(UIElement root) where T : UIElement
    {
        foreach (var child in root.Children.OfType<UIElement>())
        {
            if (child is T match) yield return match;
            foreach (var inner in FindAll<T>(child)) yield return inner;
        }
    }
}
