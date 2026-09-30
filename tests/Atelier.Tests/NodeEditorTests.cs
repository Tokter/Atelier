using Atelier.Audio;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Nodes;
using Atelier.Theming;
using Atelier.Theming.Material;

namespace Atelier.Tests;

public class NodeEditorTests
{
    // An editor showing `graph`, attached and laid out at 600 × 400.
    private static NodeEditor Show(NodeGraphViewModel graph, float zoom = 1, Point offset = default)
    {
        var editor = new NodeEditor { Graph = graph, Zoom = zoom, Offset = offset };
        editor.AttachToHost();
        Layout(editor);
        return editor;
    }

    private static void Layout(NodeEditor editor)
    {
        editor.Measure(new Size(600, 400));
        editor.Arrange(new Rect(0, 0, 600, 400));
    }

    private static Point Center(UIElement element, UIElement relativeTo) =>
        element.PointToNode(new Point(element.Bounds.Width * 0.5f, element.Bounds.Height * 0.5f), relativeTo);

    private static void AssertClose(Point expected, Point actual, float tolerance = 0.75f)
    {
        Assert.True(Math.Abs(expected.X - actual.X) <= tolerance && Math.Abs(expected.Y - actual.Y) <= tolerance, $"expected {expected}, got {actual}");
    }

    [Fact]
    public void ShowsAViewPerNode_InTheGraphsOrder_AndFollowsChanges()
    {
        var graph = new NodeGraphViewModel();
        var a = graph.AddNode(TestSockets.Math("A"));
        var b = graph.AddNode(TestSockets.Math("B"));
        var editor = Show(graph);
        Assert.Equal([a, b], editor.NodeViews.Select(v => v.Node));

        var c = graph.AddNode(TestSockets.Math("C"));
        graph.RemoveNode(a);
        Assert.Equal([b, c], editor.NodeViews.Select(v => v.Node));
        Assert.Null(editor.GetNodeView(a));

        graph.Undo.Undo(); // a comes back in its place
        Assert.Equal([a, b, c], editor.NodeViews.Select(v => v.Node));

        editor.Graph = null;
        Assert.Empty(editor.NodeViews);
        editor.DetachFromHost();
    }

    [Fact]
    public void ViewportConversions_AndZoomAtKeepsThePointUnderThePointer()
    {
        var editor = Show(new NodeGraphViewModel(), zoom: 2, offset: new Point(100, 50));
        Assert.Equal(new Point(120, 70), editor.GraphToView(new Point(10, 10)));
        Assert.Equal(new Point(10, 10), editor.ViewToGraph(new Point(120, 70)));

        var pointer = new Point(300, 200);
        var under = editor.ViewToGraph(pointer);
        editor.ZoomAt(pointer, 0.5f);
        Assert.Equal(0.5f, editor.Zoom);
        AssertClose(pointer, editor.GraphToView(under), 0.01f);

        editor.Zoom = 100;
        Assert.Equal(editor.MaxZoom, editor.Zoom);
        editor.Zoom = 0.001f;
        Assert.Equal(editor.MinZoom, editor.Zoom);

        editor.PanBy(10, -5);
        Assert.Equal(editor.Offset, editor.GraphToView(Point.Zero));
        editor.DetachFromHost();
    }

    [Fact]
    public void NodeViews_ArePlacedAndScaled_AndSocketsCanBeHit()
    {
        var graph = new NodeGraphViewModel();
        var node = graph.AddNode(TestSockets.Math());
        node.Position = new Point(40, 30);
        var editor = Show(graph, zoom: 2, offset: new Point(10, 20));
        var view = editor.GetNodeView(node)!;

        // The body's top-left corner is at the node's position.
        AssertClose(editor.GraphToView(node.Position), view.PointToNode(view.BodyBounds.Location, editor));
        var socketView = view.GetSocketView(node.Outputs[0])!;
        Assert.Same(socketView, editor.HitTest(Center(socketView, editor)));

        editor.DetachFromHost();
    }

    [Fact]
    public void SocketAnchors_MatchTheirViews_AndFollowTheNode()
    {
        var graph = new NodeGraphViewModel();
        var node = graph.AddNode(TestSockets.Math());
        node.Position = new Point(40, 30);
        var editor = Show(graph, zoom: 1.5f, offset: new Point(-20, 10));
        var view = editor.GetNodeView(node)!;

        foreach (var socket in node.Inputs.Cast<SocketViewModel>().Concat(node.Outputs))
        {
            AssertClose(Center(view.GetSocketView(socket)!, editor), editor.GraphToView(socket.Anchor));
        }
        Assert.Equal(node.Position.X, node.Inputs[0].Anchor.X);
        Assert.Equal(node.Position.X + node.Width, node.Outputs[0].Anchor.X);

        var before = node.Inputs[1].Anchor;
        node.Position = new Point(100, 80); // updates right away, before the next layout
        Assert.Equal(before + new Point(60, 50), node.Inputs[1].Anchor);

        node.IsCollapsed = true;
        Layout(editor);
        float middle = node.Position.Y + view.HeaderHeight / 2;
        Assert.Equal(new Point(node.Position.X, middle), node.Inputs[0].Anchor);
        Assert.Equal(new Point(node.Position.X + node.Width, middle), node.Outputs[0].Anchor);
        Assert.Equal(view.HeaderHeight, view.DesiredSize.Height);
        editor.DetachFromHost();
    }

    [Fact]
    public void FrameAll_FitsAllNodesIntoView()
    {
        var graph = new NodeGraphViewModel();
        var a = graph.AddNode(TestSockets.Math("A"));
        var b = graph.AddNode(TestSockets.Math("B"));
        a.Position = new Point(-500, -200);
        b.Position = new Point(900, 600);
        var editor = Show(graph);

        editor.FrameAll(padding: 20);
        Layout(editor);
        foreach (var node in graph.Nodes)
        {
            var area = editor.GetNodeArea(node);
            var topLeft = editor.GraphToView(area.Location);
            var bottomRight = editor.GraphToView(new Point(area.Right, area.Bottom));
            Assert.True(topLeft.X >= 19 && topLeft.Y >= 19 && bottomRight.X <= 581 && bottomRight.Y <= 381, $"{node} at {topLeft}-{bottomRight}");
        }
        Assert.True(editor.Zoom < 1);
        editor.DetachFromHost();
    }

    // Pointer events as a window dispatches them: preview from the root down, then bubbling up from the element hit.
    internal static void Press(UIElement root, Point at, PointerButtons button, ModifierKeys modifiers = ModifierKeys.None, int clicks = 1)
    {
        var target = root.HitTest(at) ?? root;
        target.DispatchPointerEvent(new PointerEventArgs(at, at, button, modifiers: modifiers, clickCount: clicks),
            (e, a) => e.OnPreviewPointerPressed(a), (e, a) => e.OnPointerPressed(a));
    }

    internal static void Move(UIElement root, Point to, ModifierKeys modifiers = ModifierKeys.None)
    {
        var target = UIElement.CapturedElement ?? root.HitTest(to) ?? root;
        target.DispatchPointerEvent(new PointerEventArgs(to, to, modifiers: modifiers),
            (e, a) => e.OnPreviewPointerMoved(a), (e, a) => e.OnPointerMoved(a));
    }

    internal static void Release(UIElement root, Point at, PointerButtons button, ModifierKeys modifiers = ModifierKeys.None)
    {
        var target = UIElement.CapturedElement ?? root.HitTest(at) ?? root;
        target.DispatchPointerEvent(new PointerEventArgs(at, at, button, modifiers: modifiers),
            (e, a) => e.OnPreviewPointerReleased(a), (e, a) => e.OnPointerReleased(a));
    }

    internal static void Wheel(UIElement root, Point at, float delta, ModifierKeys modifiers = ModifierKeys.None)
    {
        var target = root.HitTest(at) ?? root;
        target.DispatchPointerEvent(new PointerWheelEventArgs(at, at, 0, delta, modifiers: modifiers),
            (e, a) => e.OnPreviewPointerWheel(a), (e, a) => e.OnPointerWheel(a));
    }

    [Fact]
    public void MiddleDragPans_AndTheWheelZoomsAroundThePointer()
    {
        var editor = Show(new NodeGraphViewModel());
        Press(editor, new Point(100, 100), PointerButtons.Middle);
        Move(editor, new Point(101, 101)); // not a drag yet
        Assert.Equal(Point.Zero, editor.Offset);
        Move(editor, new Point(130, 90));
        Assert.True(editor.IsDragging);
        Release(editor, new Point(130, 90), PointerButtons.Middle);
        Assert.Equal(new Point(30, -10), editor.Offset);
        Assert.False(editor.IsPointerCaptured);

        var pointer = new Point(200, 150);
        var under = editor.ViewToGraph(pointer);
        Wheel(editor, pointer, 1);
        Assert.Equal(1.1f, editor.Zoom, 3);
        AssertClose(pointer, editor.GraphToView(under), 0.01f);
        Wheel(editor, pointer, -1);
        Assert.Equal(1f, editor.Zoom, 3);
        editor.DetachFromHost();
    }

    [Fact]
    public void EscapeCancelsAPan()
    {
        var editor = Show(new NodeGraphViewModel());
        FocusManager.SetFocus(editor);
        Press(editor, new Point(100, 100), PointerButtons.Middle);
        Move(editor, new Point(160, 100));
        Assert.Equal(new Point(60, 0), editor.Offset);

        FocusManager.DispatchKeyDown(new KeyEventArgs(Key.Escape), editor);
        Assert.Equal(Point.Zero, editor.Offset);
        Assert.False(editor.IsDragging);
        Assert.False(editor.IsPointerCaptured);
        editor.DetachFromHost();
    }

    [Fact]
    public void TheEditorsGestures_CanBeRebound()
    {
        var editor = Show(new NodeGraphViewModel());
        try
        {
            KeybindingManager.SetCustomization(NodeEditor.CommandGroup, "Pan", new CommandCustomization(Keybinding: "Alt+RightDrag"));
            KeybindingManager.SetCustomization(NodeEditor.CommandGroup, "ZoomIn", new CommandCustomization(Keybinding: "Ctrl+WheelUp"));

            Press(editor, new Point(100, 100), PointerButtons.Middle);
            Move(editor, new Point(150, 100));
            Release(editor, new Point(150, 100), PointerButtons.Middle);
            Assert.Equal(Point.Zero, editor.Offset); // no longer bound

            Press(editor, new Point(100, 100), PointerButtons.Right, ModifierKeys.Alt);
            Move(editor, new Point(150, 100), ModifierKeys.Alt);
            Release(editor, new Point(150, 100), PointerButtons.Right);
            Assert.Equal(new Point(50, 0), editor.Offset);

            Wheel(editor, new Point(10, 10), 1);
            Assert.Equal(1f, editor.Zoom);
            Wheel(editor, new Point(10, 10), 1, ModifierKeys.Control);
            Assert.Equal(1.1f, editor.Zoom, 3);
        }
        finally
        {
            KeybindingManager.ClearCustomizations();
            editor.DetachFromHost();
        }
    }

    [Fact]
    public void TheEditorsCommands_AreListedWhereTheFocusIs_ExceptDrags()
    {
        var graph = new NodeGraphViewModel();
        var node = graph.AddNode(TestSockets.Math());
        var editor = Show(graph);
        var inNode = editor.GetNodeView(node)!.GetInputEditor(node.Inputs[0])!;

        var commands = KeybindingHandler.GetActiveCommands(inNode);
        var frame = Assert.Single(commands, c => c.Descriptor.Name == "FrameAll");
        Assert.Same(editor, frame.Target);
        Assert.True(frame.CanExecute);
        Assert.Contains(commands, c => c.Descriptor.Command is IDragCommand);
        editor.DetachFromHost();
    }

    private static UIElement Surface(NodeEditor editor) => (UIElement)editor.Content!;

    [Fact]
    public void BackgroundLayers_AreDrawnBehindEverything_AndCanBeReplaced()
    {
        var editor = new NodeEditor();
        var grid = Assert.IsType<GridLayer>(Assert.Single(editor.BackgroundLayers));
        Assert.Same(grid, Surface(editor).Children[0]);
        Assert.Same(editor, grid.Editor);

        var dots = new DotGridLayer();
        editor.BackgroundLayers[0] = dots;
        editor.BackgroundLayers.Add(new GridLayer { Spacing = 100 });
        Assert.Same(dots, Surface(editor).Children[0]);
        Assert.IsType<GridLayer>(Surface(editor).Children[1]);
        Assert.Null(grid.Editor);
        Assert.DoesNotContain(grid, Surface(editor).Children);
        Assert.False(dots.IsHitTestVisible);
    }

    [Fact]
    public void GridSteps_ThinOutWhenZoomedOut()
    {
        Assert.Equal(20f, NodeEditorLayer.VisibleStep(20, 5, 1, 8, out float fade));
        Assert.Equal(1f, fade);
        Assert.Equal(20f, NodeEditorLayer.VisibleStep(20, 5, 0.2f, 8, out fade)); // 4 px lines: every fifth stays
        Assert.Equal(1f, fade);
        Assert.Equal(10f, NodeEditorLayer.VisibleStep(20, 5, 0.5f, 8, out fade));
        Assert.Equal(0.25f, fade, 3); // the finest lines are fading out
    }
}

public class NodeInputEditorTests
{
    private static InputSocketViewModel Input(SocketType type, object? value = null, double? min = null, double? max = null)
    {
        var node = new NodeViewModel();
        var input = node.AddInput("Value", type, value);
        input.Minimum = min;
        input.Maximum = max;
        return input;
    }

    private static readonly SocketType Bool = new("bool", "Boolean", Color.White, typeof(bool), false);
    private static readonly SocketType Other = new("object", "Object", Color.White, typeof(Uri));

    [Fact]
    public void Auto_PicksAControlByTheValueType()
    {
        Assert.Equal(InputEditor.CheckBox, InputEditors.Resolve(Input(Bool)));
        Assert.Equal(InputEditor.Slider, InputEditors.Resolve(Input(TestSockets.Float, min: 0, max: 1)));
        Assert.Equal(InputEditor.TextBox, InputEditors.Resolve(Input(TestSockets.Float)));
        Assert.Equal(InputEditor.TextBox, InputEditors.Resolve(Input(TestSockets.Text)));
        Assert.Equal(InputEditor.None, InputEditors.Resolve(Input(Other)));

        Assert.IsType<CheckBox>(InputEditors.Create(Input(Bool)));
        Assert.IsType<Slider>(InputEditors.Create(Input(TestSockets.Int, min: 0, max: 10)));
        Assert.Null(InputEditors.Create(Input(Other)));
        var knob = Input(TestSockets.Float);
        knob.Editor = InputEditor.Knob;
        Assert.IsType<Slider>(InputEditors.Create(knob)); // until a knob factory is registered
    }

    [Fact]
    public void ASlider_EditsTheValue_KeepingItsType()
    {
        var input = Input(TestSockets.Int, 3, 0, 10);
        var slider = InputEditors.CreateSlider(input);
        slider.AttachToHost();
        Assert.Equal(3f, slider.Value);

        slider.Value = 6.6f;
        Assert.Equal(7, input.Value); // rounded to an int

        input.Value = 2;
        Assert.Equal(2f, slider.Value);
        slider.DetachFromHost();

        input.Value = 9; // no longer followed once detached
        Assert.Equal(2f, slider.Value);
    }

    [Fact]
    public void ATextBox_ParsesNumbersAsTheyAreTyped_AndIgnoresTheRest()
    {
        var input = Input(TestSockets.Float, 1.5);
        var textBox = InputEditors.CreateTextBox(input);
        textBox.AttachToHost();
        Assert.Equal("1.5", textBox.Text);
        Assert.Equal("Value", textBox.Label);

        textBox.Text = "2.25";
        Assert.Equal(2.25, input.Value);
        textBox.Text = "abc";
        Assert.Equal(2.25, input.Value);

        input.Value = 4.0;
        Assert.Equal("4", textBox.Text);
        textBox.DetachFromHost();

        var text = Input(TestSockets.Text, "a");
        var textEditor = InputEditors.CreateTextBox(text);
        textEditor.Text = "hello";
        Assert.Equal("hello", text.Value);
    }

    [Fact]
    public void ACheckBox_EditsItsValue()
    {
        var flag = Input(Bool, true);
        var checkBox = InputEditors.CreateCheckBox(flag);
        Assert.True(checkBox.IsChecked);
        checkBox.IsChecked = false;
        Assert.Equal(false, flag.Value);
    }

    [Fact]
    public void NumberRanges_StepByOne_ForWholeNumbers_AndByAHundredth_OtherwiseOfTheRange()
    {
        Assert.Equal(new NumberRange(0, 20, 1, 2, true, "{0:0}"), InputEditors.GetNumberRange(Input(TestSockets.Int, 5, 0, 20)));
        Assert.Equal(new NumberRange(0, 2, 0.02f, 0.2f, false, "{0:0.00}"), InputEditors.GetNumberRange(Input(TestSockets.Float, 0.5, 0, 2)));
        var unbounded = InputEditors.GetNumberRange(Input(TestSockets.Float));
        Assert.Equal((0f, 1f), (unbounded.Minimum, unbounded.Maximum));
    }

    [Fact]
    public void ARegisteredFactory_ReplacesTheBuiltInControl_UntilItIsRemoved()
    {
        var gain = Input(TestSockets.Float, 0.5, 0, 2);
        gain.Editor = InputEditor.Knob;
        InputEditors.Register(InputEditor.Knob, input =>
        {
            var range = InputEditors.GetNumberRange(input);
            var knob = new Knob { Minimum = range.Minimum, Maximum = range.Maximum };
            InputEditors.BindNumber(knob, input, v => knob.Value = v, h => knob.ValueChanged += h);
            return knob;
        });
        try
        {
            var knob = Assert.IsType<Knob>(InputEditors.Create(gain));
            Assert.Equal((0f, 2f, 0.5f), (knob.Minimum, knob.Maximum, knob.Value));
            knob.Value = 1.5f;
            Assert.Equal(1.5, gain.Value);
        }
        finally
        {
            InputEditors.Register(InputEditor.Knob, null);
        }
        Assert.IsType<Slider>(InputEditors.Create(gain));
        Assert.Throws<ArgumentException>(() => InputEditors.Register(InputEditor.Auto, _ => null));
    }

    [Fact]
    public void AnInputsControl_IsHiddenWhileItIsConnected()
    {
        var graph = new NodeGraphViewModel();
        var a = graph.AddNode(TestSockets.Math("A"));
        var b = graph.AddNode(TestSockets.Math("B"));
        var editor = new NodeEditor { Graph = graph };
        editor.AttachToHost();
        var control = editor.GetNodeView(b)!.GetInputEditor(b.Inputs[0])!;
        Assert.Equal(Visibility.Visible, control.Visibility);

        var link = graph.Connect(a.Outputs[0], b.Inputs[0])!;
        Assert.Equal(Visibility.Collapsed, control.Visibility);
        graph.Disconnect(link);
        Assert.Equal(Visibility.Visible, control.Visibility);
        editor.DetachFromHost();
    }

    [Fact]
    public void TheEditorsFactory_ReplacesTheDefaultControls()
    {
        var graph = new NodeGraphViewModel();
        var node = graph.AddNode(TestSockets.Math());
        var editor = new NodeEditor { Graph = graph };
        Assert.IsType<TextBox>(editor.GetNodeView(node)!.GetInputEditor(node.Inputs[0]));

        editor.InputEditorFactory = input => input.Name == "A" ? new Knob() : null;
        var view = editor.GetNodeView(node)!;
        Assert.IsType<Knob>(view.GetInputEditor(node.Inputs[0]));
        Assert.Null(view.GetInputEditor(node.Inputs[1]));
    }
}

public class LinkGeometryTests
{
    [Fact]
    public void Links_LeaveOutputsRightwards_AndBackwardLinksLoopRound()
    {
        var (c1, c2) = LinkGeometry.GetControlPoints(new Point(0, 0), new Point(200, 100));
        Assert.Equal(new Point(100, 0), c1);
        Assert.Equal(new Point(100, 100), c2);

        (c1, c2) = LinkGeometry.GetControlPoints(new Point(100, 0), new Point(90, 50));
        Assert.Equal(new Point(100 + LinkGeometry.MinHandle, 0), c1);
        Assert.Equal(new Point(90 - LinkGeometry.MinHandle, 50), c2);

        (c1, _) = LinkGeometry.GetControlPoints(new Point(100, 0), new Point(90, 50), scale: 2);
        Assert.Equal(new Point(100 + 2 * LinkGeometry.MinHandle, 0), c1);
    }

    [Fact]
    public void DistanceTo_IsSmallOnTheCurve_AndGrowsAwayFromIt()
    {
        var start = new Point(0, 0);
        var end = new Point(200, 100);
        Assert.Equal(new Point(100, 50), LinkGeometry.GetPoint(start, end, 0.5f));
        Assert.True(LinkGeometry.DistanceTo(new Point(100, 50), start, end) < 0.5f);
        Assert.True(LinkGeometry.DistanceTo(start, start, end) < 0.01f);
        Assert.InRange(LinkGeometry.DistanceTo(new Point(100, 80), start, end), 20, 31);
    }
}

public class ThemeExtensionTests
{
    private static int s_applied;

    private static void CountApplications(Theme theme) => s_applied++;

    [Fact]
    public void Extensions_ApplyOncePerTheme_ToTheCurrentAndLaterThemes()
    {
        var light = MaterialTheme.CreateLight();
        using var active = ActiveTheme.Use(light);
        s_applied = 0;
        ThemeExtensions.Register(CountApplications);
        ThemeExtensions.Register(CountApplications);
        Assert.Equal(1, s_applied);

        var dark = MaterialTheme.CreateDark();
        ThemeManager.Current = dark;
        ThemeManager.Current = light;
        ThemeExtensions.ApplyTo(dark);
        Assert.Equal(2, s_applied);
    }

    [Fact]
    public void TheNodeEditor_AddsItsRenderersAndMaterialColors()
    {
        var theme = MaterialTheme.CreateDark();
        using var active = ActiveTheme.Use(theme);
        var node = new NodeViewModel("Node");
        var view = new NodeView(node);
        view.ApplyStyles();

        Assert.NotNull(theme.Renderers.GetRenderer(typeof(NodeView)));
        Assert.NotNull(theme.Renderers.GetRenderer(typeof(DotGridLayer)));
        Assert.Equal(theme.Colors.SurfaceContainerHigh, view.Background);
        Assert.Equal(theme.Colors.SurfaceContainerHighest, view.HeaderBackground);
    }

    [Fact]
    public void ANodesTitleBar_IsDrawnInItsColor_WithReadableText()
    {
        using var active = ActiveTheme.Use(MaterialTheme.CreateLight());
        var graph = new NodeGraphViewModel();
        var dark = graph.AddNode(TestSockets.Math("Dark"));
        dark.HeaderColor = Color.FromRgb(0x20, 0x30, 0x80);
        var light = graph.AddNode(TestSockets.Math("Light"));
        light.HeaderColor = Color.FromRgb(0xF0, 0xE0, 0x90);
        light.Position = new Point(200, 0);
        var editor = new NodeEditor { Graph = graph, Width = 400, Height = 200 };
        var root = new Atelier.Layout.Canvas { Width = 400, Height = 200 };
        root.Add(editor);
        root.AttachToHost();
        using var bitmap = ThemeRendering.Render(root, 400, 200);
        root.DetachFromHost();

        Assert.True(ThemeRendering.IsClose(bitmap.GetPixel(120, 6), dark.HeaderColor.Value), $"{bitmap.GetPixel(120, 6)}");
        Assert.True(ThemeRendering.IsClose(bitmap.GetPixel(320, 6), light.HeaderColor.Value), $"{bitmap.GetPixel(320, 6)}");
        var darkTitle = FindTextBlock(editor.GetNodeView(dark)!, "Dark");
        var lightTitle = FindTextBlock(editor.GetNodeView(light)!, "Light");
        Assert.True(darkTitle.Foreground.R > 200);
        Assert.True(lightTitle.Foreground.R < 60);
    }

    private static TextBlock FindTextBlock(UIElement root, string text)
    {
        foreach (var child in root.Children.OfType<UIElement>())
        {
            if (child is TextBlock { } block && block.Text == text) return block;
            try
            {
                return FindTextBlock(child, text);
            }
            catch (InvalidOperationException)
            {
            }
        }
        throw new InvalidOperationException($"No text block '{text}'.");
    }
}
