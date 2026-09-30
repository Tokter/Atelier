using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;

namespace Atelier.Audio;

/// <summary>
/// The commands of timeline controls, in two keybinding groups users can rebind in the keybinding editor:
/// <see cref="Group"/> for every <see cref="TimelineControl"/> and <see cref="RulerGroup"/> for the
/// <see cref="TimelineRuler"/> only. The defaults follow Bitwig.
/// </summary>
/// <remarks>
/// <para>
/// Everywhere: Ctrl+Alt+wheel zooms around the pointer, Shift+wheel and middle drag scroll, <c>=</c> and <c>-</c> zoom
/// in and out, and Home shows the whole song.
/// </para>
/// <para>
/// On the ruler: dragging zooms (up to zoom in, down to zoom out) around the point where the drag started and scrolls
/// with the pointer, the wheel zooms, double-clicking shows the whole song, and right-clicking opens the ruler's menu.
/// </para>
/// </remarks>
public static class TimelineCommands
{
    /// <summary>The keybinding group of the commands of every timeline control.</summary>
    public const string Group = "Timeline";

    /// <summary>The keybinding group of the commands of the ruler only.</summary>
    public const string RulerGroup = "TimelineRuler";

    /// <summary>How much one step of <see cref="ZoomIn"/> or <see cref="ZoomOut"/> zooms.</summary>
    public const double ZoomStep = 1.25;

    /// <summary>How far one wheel step scrolls, in pixels.</summary>
    public const float WheelScrollPixels = 60f;

    /// <summary>How much dragging the ruler 100 pixels up zooms in (the factor is <c>e</c>, about 2.7).</summary>
    public const double DragZoomPerPixel = 0.01;

    /// <summary>Gets the command that zooms in around the pointer (Ctrl+Alt+wheel up; wheel up on the ruler).</summary>
    public static TimelineCommand ZoomIn { get; } = new(_ => true, c => c.ZoomAroundPointer(ZoomStep));

    /// <summary>Gets the command that zooms out around the pointer (Ctrl+Alt+wheel down; wheel down on the ruler).</summary>
    public static TimelineCommand ZoomOut { get; } = new(_ => true, c => c.ZoomAroundPointer(1 / ZoomStep));

    /// <summary>Gets the command that scrolls to earlier times (Shift+wheel up).</summary>
    public static TimelineCommand ScrollBack { get; } = new(_ => true, c => c.CurrentTimeline.ScrollBy(-WheelScrollPixels));

    /// <summary>Gets the command that scrolls to later times (Shift+wheel down).</summary>
    public static TimelineCommand ScrollForward { get; } = new(_ => true, c => c.CurrentTimeline.ScrollBy(WheelScrollPixels));

    /// <summary>Gets the command that shows the whole song (Home; double-click on the ruler).</summary>
    public static TimelineCommand ZoomToFit { get; } = new(c => double.IsFinite(c.CurrentTimeline.Duration), c => c.ZoomToFit());

    /// <summary>Gets the command that scrolls by dragging: the timeline follows the pointer; Escape moves it back (middle drag).</summary>
    public static TimelineScrollDragCommand ScrollDrag { get; } = new(zoom: false);

    /// <summary>
    /// Gets the command that zooms and scrolls by dragging, like Bitwig's ruler: up zooms in and down zooms out around the
    /// time where the drag started, which stays under the pointer as it moves sideways (drag on the ruler).
    /// </summary>
    public static TimelineScrollDragCommand ZoomDrag { get; } = new(zoom: true);

    /// <summary>Gets the command that opens the ruler's menu at the pointer (right-click on the ruler).</summary>
    public static TimelineCommand RulerMenu { get; } = new(c => c is TimelineRuler, c => ((TimelineRuler)c).ShowContextMenu());

    /// <summary>
    /// Registers the commands that aren't registered yet (with the users' changes applied). Timeline controls call it
    /// when they're created.
    /// </summary>
    public static void Register()
    {
        Add(Group, "ZoomIn", "Ctrl+Alt+WheelUp", ZoomIn, "Zoom in", "Zoom in around the pointer", MaterialIcons.ZoomIn);
        Add(Group, "ZoomOut", "Ctrl+Alt+WheelDown", ZoomOut, "Zoom out", "Zoom out around the pointer", MaterialIcons.ZoomOut);
        Add(Group, "ZoomInKey", "Equal", ZoomIn, "Zoom in (key)", "Zoom in around the pointer", MaterialIcons.ZoomIn);
        Add(Group, "ZoomOutKey", "Minus", ZoomOut, "Zoom out (key)", "Zoom out around the pointer", MaterialIcons.ZoomOut);
        Add(Group, "ScrollBack", "Shift+WheelUp", ScrollBack, "Scroll back", "Scroll to earlier times", MaterialIcons.ChevronLeft);
        Add(Group, "ScrollForward", "Shift+WheelDown", ScrollForward, "Scroll forward", "Scroll to later times", MaterialIcons.ChevronRight);
        Add(Group, "ScrollDrag", "MiddleDrag", ScrollDrag, "Scroll", "Move the timeline by dragging", MaterialIcons.PanTool);
        Add(Group, "ZoomToFit", "Home", ZoomToFit, "Zoom to fit", "Show the whole song", MaterialIcons.FitScreen);

        Add(RulerGroup, "ZoomDrag", "LeftDrag", ZoomDrag, "Zoom and scroll", "Drag up or down to zoom, sideways to scroll", MaterialIcons.ZoomIn);
        Add(RulerGroup, "WheelZoomIn", "WheelUp", ZoomIn, "Zoom in (ruler)", "Zoom in around the pointer", MaterialIcons.ZoomIn);
        Add(RulerGroup, "WheelZoomOut", "WheelDown", ZoomOut, "Zoom out (ruler)", "Zoom out around the pointer", MaterialIcons.ZoomOut);
        Add(RulerGroup, "FitOnDoubleClick", "DoubleClick", ZoomToFit, "Zoom to fit (ruler)", "Show the whole song", MaterialIcons.FitScreen);
        Add(RulerGroup, "RulerMenu", "RightClick", RulerMenu, "Ruler menu", "Choose what the ruler shows", MaterialIcons.Menu);
    }

    private static void Add(string group, string name, string keybinding, AtelierCommand command, string label, string description, string icon)
    {
        foreach (var registered in KeybindingManager.GetKeybindings(group))
        {
            if (registered.Name == name) return;
        }
        KeybindingManager.RegisterKeybinding(new KeybindingDescriptor(name, group, keybinding, command,
            label: label, description: description, icon: icon));
    }
}

/// <summary>A command on a <see cref="TimelineControl"/> (its target), made of two functions.</summary>
/// <param name="canExecute">Whether the command can run on a control.</param>
/// <param name="execute">Runs the command on a control.</param>
public sealed class TimelineCommand(Func<TimelineControl, bool> canExecute, Action<TimelineControl> execute) : AtelierCommand
{
    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is TimelineControl control && canExecute(control);

    /// <inheritdoc/>
    public override void Execute(object? parameter)
    {
        if (parameter is TimelineControl control && canExecute(control)) execute(control);
    }
}

/// <summary>
/// Scrolls a <see cref="TimelineControl"/>'s timeline by dragging, the time where the drag started following the pointer;
/// with zooming, dragging up zooms in and down zooms out around it. Escape puts the view back.
/// </summary>
/// <param name="zoom">Whether vertical movement zooms.</param>
public sealed class TimelineScrollDragCommand(bool zoom) : DragCommand
{
    /// <summary>Gets whether vertical movement zooms.</summary>
    public bool Zooms { get; } = zoom;

    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is TimelineControl;

    /// <inheritdoc/>
    public override IDragOperation? BeginDrag(DragStart start) =>
        start.Target is TimelineControl control ? new Operation(control, start, Zooms) : null;

    private sealed class Operation : IDragOperation
    {
        private readonly TimelineControl _control;
        private readonly TimelineContext _timeline;
        private readonly bool _zoom;
        private readonly Point _startScreen;
        private readonly double _anchorTime;
        private readonly double _startZoom;
        private readonly double _startTime;

        public Operation(TimelineControl control, DragStart start, bool zoom)
        {
            _control = control;
            _timeline = control.CurrentTimeline;
            _zoom = zoom;
            _startScreen = start.ScreenPosition;

            _anchorTime = _timeline.XToTime(control.PointToClient(start.ScreenPosition).X);
            _startZoom = _timeline.PixelsPerSecond;
            _startTime = _timeline.Start;
        }

        public void Update(Point screenPosition, ModifierKeys modifiers)
        {
            var moved = screenPosition - _startScreen;
            double pixelsPerSecond = _zoom ? _startZoom * Math.Exp(-moved.Y * TimelineCommands.DragZoomPerPixel) : _startZoom;
            // Keep the time grabbed under the pointer as it zooms and moves sideways.
            _timeline.PixelsPerSecond = pixelsPerSecond;
            double x = _control.PointToClient(screenPosition).X;
            _timeline.Start = _anchorTime - x / _timeline.PixelsPerSecond;
        }

        public void Complete(Point screenPosition, ModifierKeys modifiers) => Update(screenPosition, modifiers);

        public void Cancel()
        {
            _timeline.PixelsPerSecond = _startZoom;
            _timeline.Start = _startTime;
        }
    }
}
