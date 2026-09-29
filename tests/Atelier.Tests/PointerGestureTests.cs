using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Tests;

public class PointerGestureParsingTests
{
    [Theory]
    [InlineData("WheelUp", PointerGesture.WheelUp, ModifierKeys.None)]
    [InlineData("ctrl+wheeldown", PointerGesture.WheelDown, ModifierKeys.Control)]
    [InlineData("Shift+RightDrag", PointerGesture.RightDrag, ModifierKeys.Shift)]
    [InlineData("MiddleDrag", PointerGesture.MiddleDrag, ModifierKeys.None)]
    [InlineData("Click", PointerGesture.LeftClick, ModifierKeys.None)]
    [InlineData("Alt+Drag", PointerGesture.LeftDrag, ModifierKeys.Alt)]
    [InlineData("DoubleClick", PointerGesture.DoubleClick, ModifierKeys.None)]
    [InlineData("RightClick", PointerGesture.RightClick, ModifierKeys.None)]
    public void PointerGestures_Parse(string text, PointerGesture pointer, ModifierKeys modifiers)
    {
        Assert.True(KeybindingGesture.TryParse(text, out var gesture));
        Assert.True(gesture.IsPointer);
        Assert.Equal(pointer, gesture.Pointer);
        Assert.Equal(Key.None, gesture.Key);
        Assert.Equal(modifiers, gesture.Modifiers);
    }

    [Theory]
    [InlineData("A+WheelUp")] // a key and a pointer action
    [InlineData("WheelUp+RightClick")]
    [InlineData("Ctrl+K, WheelUp")] // pointer actions can't be part of a chord
    [InlineData("WheelUp, WheelUp")]
    public void InvalidCombinations_DontParse(string text)
    {
        Assert.False(KeybindingGesture.TryParseSequence(text, out _));
    }

    [Fact]
    public void PointerGestures_FormatAndCompare()
    {
        Assert.Equal("Ctrl+Shift+WheelDown", KeybindingGesture.Normalize("shift+ctrl+wheeldown"));
        Assert.Equal("LeftDrag", KeybindingGesture.Normalize("Drag"));
        Assert.Equal("Ctrl+Wheel up", KeybindingGesture.FormatForDisplay("Ctrl+WheelUp"));
        Assert.Equal(["Shift", "Right drag"], KeybindingGesture.Parse("Shift+RightDrag").GetDisplayParts());
        Assert.True(KeybindingGesture.Matches("Click", "LeftClick"));
        Assert.False(KeybindingGesture.Matches("RightClick", "RightDrag"));
        Assert.False(KeybindingGesture.Parse("WheelUp").Matches(Key.None, ModifierKeys.None));
        Assert.NotEqual(KeybindingGesture.Parse("WheelUp").GetHashCode(), KeybindingGesture.Parse("WheelDown").GetHashCode());
    }
}

[Collection("KeybindingTests")]
public class PointerKeybindingTests : IDisposable
{
    private const string Group = "PointerTests";
    private readonly KeybindingHandler _handler;
    private readonly Border _inner;

    public PointerKeybindingTests()
    {
        KeybindingManager.Clear();
        _inner = new Border { Width = 200, Height = 200 };
        _handler = new KeybindingHandler(Group, _inner);
        _handler.Measure(new Size(200, 200));
        _handler.Arrange(new Rect(0, 0, 200, 200));
        _handler.AttachToHost();
    }

    public void Dispose()
    {
        _handler.DetachFromHost();
        KeybindingManager.Clear();
    }

    private static void Register(string name, string gesture, System.Windows.Input.ICommand command) =>
        KeybindingManager.RegisterKeybinding(new KeybindingDescriptor(name, Group, gesture, command));

    [Fact]
    public void AClick_RunsOnRelease_UnlessThePointerMovedAway()
    {
        var command = new CountingCommand();
        Register("Menu", "RightClick", command);

        NodeEditorTests.Press(_handler, new Point(50, 50), PointerButtons.Right);
        Assert.True(_handler.IsPointerCaptured);
        Assert.Equal(0, command.Count);
        NodeEditorTests.Release(_handler, new Point(51, 50), PointerButtons.Right);
        Assert.Equal(1, command.Count);
        Assert.False(_handler.IsPointerCaptured);

        NodeEditorTests.Press(_handler, new Point(50, 50), PointerButtons.Right);
        NodeEditorTests.Move(_handler, new Point(80, 50));
        NodeEditorTests.Release(_handler, new Point(80, 50), PointerButtons.Right);
        Assert.Equal(1, command.Count); // a drag, not a click

        NodeEditorTests.Press(_handler, new Point(50, 50), PointerButtons.Right, ModifierKeys.Control);
        NodeEditorTests.Release(_handler, new Point(50, 50), PointerButtons.Right);
        Assert.Equal(1, command.Count); // other modifiers
    }

    [Fact]
    public void UnboundButtons_AreLeftAlone()
    {
        Register("Menu", "RightClick", new CountingCommand());
        var args = new PointerEventArgs(new Point(10, 10), new Point(10, 10), PointerButtons.Left, clickCount: 1);
        _handler.OnPointerPressed(args);
        Assert.False(args.Handled);
        Assert.False(_handler.IsPointerCaptured);
    }

    [Fact]
    public void DoubleClicksAndWheelTurns_RunTheirCommands()
    {
        var doubleClick = new CountingCommand();
        var zoom = new CountingCommand();
        Register("Open", "DoubleClick", doubleClick);
        Register("Zoom", "Ctrl+WheelUp", zoom);

        NodeEditorTests.Press(_handler, new Point(10, 10), PointerButtons.Left, clicks: 2);
        Assert.Equal(1, doubleClick.Count);

        NodeEditorTests.Wheel(_handler, new Point(10, 10), 1);
        NodeEditorTests.Wheel(_handler, new Point(10, 10), -1, ModifierKeys.Control);
        Assert.Equal(0, zoom.Count);
        NodeEditorTests.Wheel(_handler, new Point(10, 10), 1, ModifierKeys.Control);
        Assert.Equal(1, zoom.Count);
    }

    [Fact]
    public void DragCommands_SharingAGesture_PickByWhereTheDragStarts()
    {
        var onInner = new RecordingDragCommand(start => start.Source == _inner, contextual: true);
        var anywhere = new RecordingDragCommand(_ => true);
        Register("MoveInner", "LeftDrag", onInner);
        Register("Other", "LeftDrag", anywhere);
        Assert.Empty(KeybindingManager.GetConflicts()); // the first only takes drags on the inner border

        _handler.Content = new Canvas();
        ((Canvas)_handler.Content).Add(_inner);
        _handler.Measure(new Size(200, 200));
        _handler.Arrange(new Rect(0, 0, 200, 200));
        _inner.Width = 50;
        _inner.Height = 50;
        _handler.Measure(new Size(200, 200));
        _handler.Arrange(new Rect(0, 0, 200, 200));

        NodeEditorTests.Press(_handler, new Point(20, 20), PointerButtons.Left); // on the inner border
        NodeEditorTests.Move(_handler, new Point(40, 20));
        NodeEditorTests.Move(_handler, new Point(60, 25));
        NodeEditorTests.Release(_handler, new Point(60, 25), PointerButtons.Left);
        Assert.Equal(["begin (20, 20)", "update (40, 20)", "update (60, 25)", "complete (60, 25)"], onInner.Log);
        Assert.Empty(anywhere.Log);

        NodeEditorTests.Press(_handler, new Point(150, 150), PointerButtons.Left); // beside it
        NodeEditorTests.Move(_handler, new Point(150, 170));
        Assert.True(_handler.IsDragging);
        _handler.CancelDrag();
        Assert.Equal(["begin (150, 150)", "update (150, 170)", "cancel"], anywhere.Log);
        Assert.False(_handler.IsPointerCaptured);
    }

    [Fact]
    public void ADragCommandThatTakesEveryDrag_ShadowsLaterOnes()
    {
        var everywhere = new RecordingDragCommand(_ => true);
        var later = new RecordingDragCommand(_ => true, contextual: true);
        Register("Pan", "MiddleDrag", everywhere);
        Register("Tool", "MiddleDrag", later);
        var conflict = Assert.Single(KeybindingManager.GetConflicts());
        Assert.Equal("Pan", conflict.First.Name);
        Assert.Equal("Tool", conflict.Second.Name);
    }

    [Fact]
    public void SameClickGestures_StillConflict()
    {
        Register("A", "RightClick", new CountingCommand());
        Register("B", "RightClick", new CountingCommand());
        Assert.Single(KeybindingManager.GetConflicts());
    }

    private sealed class CountingCommand : AtelierCommand
    {
        public int Count { get; private set; }
        public override void Execute(object? parameter) => Count++;
    }

    private sealed class RecordingDragCommand(Func<DragStart, bool> applies, bool contextual = false) : DragCommand
    {
        public override bool IsContextual => contextual;

        public List<string> Log { get; } = [];

        public override IDragOperation? BeginDrag(DragStart start)
        {
            if (!applies(start)) return null;
            Log.Add($"begin {start.ScreenPosition}");
            return new Operation(Log);
        }

        private sealed class Operation(List<string> log) : IDragOperation
        {
            public void Update(Point screenPosition, ModifierKeys modifiers) => log.Add($"update {screenPosition}");
            public void Complete(Point screenPosition, ModifierKeys modifiers) => log.Add($"complete {screenPosition}");
            public void Cancel() => log.Add("cancel");
        }
    }
}

public class ShortcutRecorderPointerTests
{
    private static ShortcutRecorder Recording()
    {
        var recorder = new ShortcutRecorder();
        recorder.Measure(new Size(300, 100));
        recorder.Arrange(new Rect(0, 0, 300, 100));
        recorder.AttachToHost();
        recorder.StartRecording();
        return recorder;
    }

    [Fact]
    public void RecordsClicksDragsAndWheelTurns()
    {
        var recorder = Recording();
        NodeEditorTests.Press(recorder, new Point(10, 10), PointerButtons.Right, ModifierKeys.Control);
        NodeEditorTests.Release(recorder, new Point(10, 10), PointerButtons.Right);
        Assert.Equal("Ctrl+RightClick", recorder.Shortcut);
        Assert.False(recorder.IsRecording);

        recorder.StartRecording();
        NodeEditorTests.Press(recorder, new Point(10, 10), PointerButtons.Middle);
        NodeEditorTests.Move(recorder, new Point(60, 10));
        NodeEditorTests.Release(recorder, new Point(60, 10), PointerButtons.Middle);
        Assert.Equal("MiddleDrag", recorder.Shortcut);

        recorder.StartRecording();
        NodeEditorTests.Wheel(recorder, new Point(10, 10), -1, ModifierKeys.Shift);
        Assert.Equal("Shift+WheelDown", recorder.Shortcut);
        recorder.DetachFromHost();
    }

    [Fact]
    public void ADoubleClick_ReplacesTheClickItStartedWith()
    {
        var recorder = Recording();
        string? recorded = null;
        recorder.ShortcutRecorded += (_, s) => recorded = s;
        NodeEditorTests.Press(recorder, new Point(10, 10), PointerButtons.Left, clicks: 1);
        NodeEditorTests.Release(recorder, new Point(10, 10), PointerButtons.Left);
        Assert.Equal("LeftClick", recorded);
        NodeEditorTests.Press(recorder, new Point(10, 10), PointerButtons.Left, clicks: 2);
        Assert.Equal("DoubleClick", recorder.Shortcut);
        Assert.Equal("DoubleClick", recorded);
        Assert.False(recorder.IsRecording);
        recorder.DetachFromHost();
    }
}
