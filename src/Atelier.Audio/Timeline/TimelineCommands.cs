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
/// in and out, Home shows the whole song, L turns the loop on or off and <c>/</c> turns snapping on or off.
/// </para>
/// <para>
/// On the ruler: clicking sets the play start (or, on a flag, selects the marker and moves the play start to it);
/// dragging a flag moves its marker; dragging on the loop bar draws, resizes or moves the loop; dragging elsewhere zooms
/// (up to zoom in, down to zoom out) around the point where the drag started and scrolls with the pointer; the wheel
/// zooms; double-clicking adds a marker (marker lane), renames one (flag), turns the loop on or off (loop bar) or shows
/// the whole song (elsewhere); right-clicking opens a menu; Delete removes the selected marker and F2 renames it.
/// </para>
/// </remarks>
public static class TimelineCommands
{
    /// <summary>The keybinding group of the commands of every timeline control.</summary>
    public const string Group = "Timeline";

    /// <summary>The keybinding group of the commands of the ruler only.</summary>
    public const string RulerGroup = "TimelineRuler";

    /// <summary>The keybinding group of the commands of the <see cref="AudioWaveformEditor"/> only.</summary>
    public const string EditorGroup = "AudioEditor";

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

    /// <summary>Gets the command that shows the whole song (Home).</summary>
    public static TimelineCommand ZoomToFit { get; } = new(c => double.IsFinite(c.CurrentTimeline.Duration), c => c.ZoomToFit());

    /// <summary>Gets the command that turns the loop on or off (L); turning on an empty loop loops four bars from the play start.</summary>
    public static TimelineCommand ToggleLoop { get; } = new(_ => true, ToggleLoopOf);

    /// <summary>Gets the command that turns snapping to the grid on or off (/).</summary>
    public static TimelineCommand ToggleSnap { get; } = new(_ => true, c => c.SnapToGrid = !c.SnapToGrid);

    /// <summary>Gets the command that scrolls by dragging: the timeline follows the pointer; Escape moves it back (middle drag).</summary>
    public static TimelineScrollDragCommand ScrollDrag { get; } = new(zoom: false);

    /// <summary>
    /// Gets the command that zooms and scrolls by dragging, like Bitwig's ruler: up zooms in and down zooms out around the
    /// time where the drag started, which stays under the pointer as it moves sideways (drag on the ruler).
    /// </summary>
    public static TimelineScrollDragCommand ZoomDrag { get; } = new(zoom: true);

    /// <summary>Gets the command that moves a marker by dragging its flag, keeping its unit and snapping (drag a flag).</summary>
    public static TimelineMarkerDragCommand MoveMarker { get; } = new();

    /// <summary>Gets the command that draws a new loop, moves an edge of the loop or the whole loop by dragging (drag on the loop bar).</summary>
    public static TimelineLoopDragCommand EditLoop { get; } = new();

    /// <summary>
    /// Gets the command for a click on the ruler: on a flag it selects the marker and moves the play start to it, on the
    /// loop bar it does nothing, and elsewhere it moves the play start to the pointer (click).
    /// </summary>
    public static TimelineCommand RulerClick { get; } = new(c => c is TimelineRuler, c => ClickAt((TimelineRuler)c));

    /// <summary>
    /// Gets the command for a double click on the ruler: it renames the marker of a flag, adds a marker on the rest of
    /// the marker lane, turns the loop on or off on the loop bar, and shows the whole song elsewhere (double-click).
    /// </summary>
    public static TimelineCommand RulerDoubleClick { get; } = new(c => c is TimelineRuler, c => DoubleClickAt((TimelineRuler)c));

    /// <summary>Gets the command that opens the menu of the flag under the pointer, or the ruler's menu (right-click on the ruler).</summary>
    public static TimelineCommand RulerMenu { get; } = new(c => c is TimelineRuler, c => ((TimelineRuler)c).ShowContextMenu(c.PointerPosition));

    /// <summary>Gets the command that deletes the ruler's selected marker (Delete).</summary>
    public static TimelineCommand DeleteMarker { get; } = new(c => c is TimelineRuler { SelectedMarker: not null },
        c => ((TimelineRuler)c).DeleteMarker(((TimelineRuler)c).SelectedMarker!));

    /// <summary>Gets the command that renames the ruler's selected marker (F2).</summary>
    public static TimelineCommand RenameMarker { get; } = new(c => c is TimelineRuler { SelectedMarker: not null },
        c => ((TimelineRuler)c).BeginRename(((TimelineRuler)c).SelectedMarker!));

    /// <summary>Gets the command that trims, fades or changes the gain of an editor's sound by dragging its handles (drag a handle).</summary>
    public static AudioEditorHandleDragCommand EditHandle { get; } = new();

    /// <summary>Gets the command that selects a stretch of an editor's sound by dragging across it (drag the sound).</summary>
    public static AudioEditorSelectDragCommand SelectRange { get; } = new();

    /// <summary>Gets the command for a click on an editor: it clears the selection and moves the play start to the pointer (click).</summary>
    public static TimelineCommand EditorClick { get; } = new(c => c is AudioWaveformEditor, c => ClickEditor((AudioWaveformEditor)c));

    /// <summary>
    /// Gets the command for a double click on an editor: it resets the gain on the gain chip or a fade on its handle, and
    /// selects the audible region elsewhere (double-click).
    /// </summary>
    public static TimelineCommand EditorDoubleClick { get; } = new(c => c is AudioWaveformEditor, c => DoubleClickEditor((AudioWaveformEditor)c));

    /// <summary>Gets the command that opens an editor's menu (right-click on the editor).</summary>
    public static TimelineCommand EditorMenu { get; } = new(c => c is AudioWaveformEditor, c => ((AudioWaveformEditor)c).ShowContextMenu());

    /// <summary>Gets the command that selects an editor's audible region (Ctrl+A).</summary>
    public static TimelineCommand SelectAllSound { get; } = new(c => c is AudioWaveformEditor { Source: not null }, c => ((AudioWaveformEditor)c).SelectAll());

    /// <summary>Gets the command that clears an editor's selection (Escape).</summary>
    public static TimelineCommand ClearSelection { get; } = new(c => c is AudioWaveformEditor { Selection: not null }, c => ((AudioWaveformEditor)c).ClearSelection());

    /// <summary>Gets the command that trims an editor's sound to its selection (Ctrl+T).</summary>
    public static TimelineCommand TrimToSelection { get; } = new(c => c is AudioWaveformEditor editor && editor.GetSelectionInSource() != null,
        c => ((AudioWaveformEditor)c).TrimToSelection());

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
        Add(Group, "ToggleLoop", "L", ToggleLoop, "Loop", "Turn the loop on or off", MaterialIcons.Repeat);
        Add(Group, "ToggleSnap", "Slash", ToggleSnap, "Snap to grid", "Turn snapping to the grid in view on or off (Shift inverts it while dragging)", MaterialIcons.Grid4x4);

        // Drag commands sharing a gesture start where they apply: flags, then the loop bar, then anywhere.
        Add(RulerGroup, "MoveMarker", "LeftDrag", MoveMarker, "Move marker", "Drag a flag to move its marker", MaterialIcons.Flag);
        Add(RulerGroup, "EditLoop", "LeftDrag", EditLoop, "Edit loop", "Drag on the loop bar to draw, resize or move the loop", MaterialIcons.Repeat);
        Add(RulerGroup, "ZoomDrag", "LeftDrag", ZoomDrag, "Zoom and scroll", "Drag up or down to zoom, sideways to scroll", MaterialIcons.ZoomIn);
        Add(RulerGroup, "RulerClick", "LeftClick", RulerClick, "Set play start", "Move the play start to the pointer, or to the marker clicked", MaterialIcons.PlayArrow);
        Add(RulerGroup, "RulerDoubleClick", "DoubleClick", RulerDoubleClick, "Add marker, loop or fit", "Add or rename a marker, turn the loop on or off, or show the whole song", MaterialIcons.Flag);
        Add(RulerGroup, "WheelZoomIn", "WheelUp", ZoomIn, "Zoom in (ruler)", "Zoom in around the pointer", MaterialIcons.ZoomIn);
        Add(RulerGroup, "WheelZoomOut", "WheelDown", ZoomOut, "Zoom out (ruler)", "Zoom out around the pointer", MaterialIcons.ZoomOut);
        Add(RulerGroup, "RulerMenu", "RightClick", RulerMenu, "Ruler menu", "Open the menu of the flag under the pointer, or the ruler's menu", MaterialIcons.Menu);
        Add(RulerGroup, "DeleteMarker", "Delete", DeleteMarker, "Delete marker", "Delete the selected marker", MaterialIcons.Delete);
        Add(RulerGroup, "RenameMarker", "F2", RenameMarker, "Rename marker", "Rename the selected marker", MaterialIcons.Edit);

        // Handles first, then selecting across the sound.
        Add(EditorGroup, "EditHandle", "LeftDrag", EditHandle, "Trim, fade or gain", "Drag an edge to trim, a fade handle to fade, the dB chip to change the gain", MaterialIcons.Tune);
        Add(EditorGroup, "SelectRange", "LeftDrag", SelectRange, "Select", "Drag across the sound to select a stretch of it", MaterialIcons.SelectAll);
        Add(EditorGroup, "EditorClick", "LeftClick", EditorClick, "Set play start", "Clear the selection and move the play start to the pointer", MaterialIcons.PlayArrow);
        Add(EditorGroup, "EditorDoubleClick", "DoubleClick", EditorDoubleClick, "Select all or reset", "Reset the gain or a fade on its handle, else select the audible region", MaterialIcons.SelectAll);
        Add(EditorGroup, "EditorMenu", "RightClick", EditorMenu, "Editor menu", "Open the editor's menu", MaterialIcons.Menu);
        Add(EditorGroup, "SelectAllSound", "Ctrl+A", SelectAllSound, "Select all", "Select the audible region", MaterialIcons.SelectAll);
        Add(EditorGroup, "ClearSelection", "Escape", ClearSelection, "Clear selection", "Clear the selection", MaterialIcons.Deselect);
        Add(EditorGroup, "TrimToSelection", "Ctrl+T", TrimToSelection, "Trim to selection", "Trim the sound to the selection", MaterialIcons.ContentCut);
    }

    private static void ClickEditor(AudioWaveformEditor editor)
    {
        if (editor.PointerPosition is not { } point) return;
        editor.ClearSelection();
        var timeline = editor.CurrentTimeline;
        timeline.PlayStart = timeline.CreatePosition(editor.Snap(editor.XToTime(point.X)), editor.PositionUnit);
    }

    private static void DoubleClickEditor(AudioWaveformEditor editor)
    {
        switch (editor.PointerPosition is { } point ? editor.PartAt(point) : AudioEditorPart.None)
        {
            case AudioEditorPart.Gain: editor.Gain = 0; break;
            case AudioEditorPart.FadeIn: editor.FadeIn = 0; break;
            case AudioEditorPart.FadeOut: editor.FadeOut = 0; break;
            default: editor.SelectAll(); break;
        }
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

    private static void ToggleLoopOf(TimelineControl control)
    {
        if (control is TimelineRuler ruler)
        {
            ruler.ToggleLoop();
            return;
        }
        var timeline = control.CurrentTimeline;
        if (timeline.IsLoopEnabled || !timeline.Loop.IsEmpty(timeline.TempoMap)) timeline.IsLoopEnabled = !timeline.IsLoopEnabled;
    }

    private static void ClickAt(TimelineRuler ruler)
    {
        if (ruler.PointerPosition is not { } point) return;
        if (ruler.MarkerAt(point) is { } marker)
        {
            ruler.SelectedMarker = marker;
            ruler.CurrentTimeline.PlayStart = marker.Position;
            return;
        }
        if (ruler.LoopBarPartAt(point) != LoopBarPart.None) return;
        ruler.SelectedMarker = null;
        ruler.SetPlayStartAt(point.X);
    }

    private static void DoubleClickAt(TimelineRuler ruler)
    {
        if (ruler.PointerPosition is not { } point) return;
        if (ruler.MarkerAt(point) is { } marker) ruler.BeginRename(marker);
        else if (ruler.MarkerLaneBounds.Contains(point)) ruler.AddMarkerAt(point.X);
        else if (ruler.LoopBarPartAt(point) != LoopBarPart.None) ruler.ToggleLoop();
        else ruler.ZoomToFit();
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

/// <summary>
/// Moves a marker by dragging its flag on a <see cref="TimelineRuler"/>: the marker keeps its unit and snaps to the grid
/// (Shift inverts snapping); it becomes the selected marker. Escape puts it back.
/// </summary>
public sealed class TimelineMarkerDragCommand : DragCommand
{
    /// <inheritdoc/>
    /// <remarks>It applies only over a flag.</remarks>
    public override bool IsContextual => true;

    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is TimelineRuler;

    /// <inheritdoc/>
    public override IDragOperation? BeginDrag(DragStart start)
    {
        if (start.Target is not TimelineRuler ruler) return null;
        var marker = ruler.MarkerAt(ruler.PointToClient(start.ScreenPosition));
        if (marker == null) return null;
        ruler.SelectedMarker = marker;
        return new Operation(ruler, marker, start.ScreenPosition);
    }

    private sealed class Operation(TimelineRuler ruler, TimelineMarker marker, Point start) : IDragOperation
    {
        private readonly TimelinePosition _original = marker.Position;
        private readonly double _originalTime = marker.Position.ToSeconds(ruler.CurrentTimeline.TempoMap);

        public void Update(Point screenPosition, ModifierKeys modifiers)
        {
            var timeline = ruler.CurrentTimeline;
            double time = _originalTime + (screenPosition.X - start.X) / timeline.PixelsPerSecond;
            marker.Position = timeline.MovePosition(_original, ruler.Snap(Math.Max(0, time), modifiers));
        }

        public void Complete(Point screenPosition, ModifierKeys modifiers) => Update(screenPosition, modifiers);

        public void Cancel() => marker.Position = _original;
    }
}

/// <summary>
/// Edits the loop by dragging on a <see cref="TimelineRuler"/>'s loop bar: outside the loop it draws a new loop (and
/// turns the loop on), on an edge it moves that edge (not past the other), and between the edges it moves the whole loop.
/// Ends keep their unit and snap to the grid (Shift inverts snapping). Escape puts the loop back.
/// </summary>
public sealed class TimelineLoopDragCommand : DragCommand
{
    /// <inheritdoc/>
    /// <remarks>It applies only over the loop bar.</remarks>
    public override bool IsContextual => true;

    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is TimelineRuler;

    /// <inheritdoc/>
    public override IDragOperation? BeginDrag(DragStart start)
    {
        if (start.Target is not TimelineRuler ruler) return null;
        var part = ruler.LoopBarPartAt(ruler.PointToClient(start.ScreenPosition));
        return part == LoopBarPart.None ? null : new Operation(ruler, part, start.ScreenPosition);
    }

    private sealed class Operation : IDragOperation
    {
        private readonly TimelineRuler _ruler;
        private readonly TimelineContext _timeline;
        private readonly LoopBarPart _part;
        private readonly Point _start;
        private readonly TimelineRange _original;
        private readonly bool _wasEnabled;
        private readonly double _startTime;
        private readonly double _endTime;
        private readonly double _anchorTime;

        public Operation(TimelineRuler ruler, LoopBarPart part, Point start)
        {
            _ruler = ruler;
            _timeline = ruler.CurrentTimeline;
            _part = part;
            _start = start;
            _original = _timeline.Loop;
            _wasEnabled = _timeline.IsLoopEnabled;
            _startTime = _original.GetStartSeconds(_timeline.TempoMap);
            _endTime = _original.GetEndSeconds(_timeline.TempoMap);
            _anchorTime = ruler.XToTime(ruler.PointToClient(start).X);
        }

        public void Update(Point screenPosition, ModifierKeys modifiers)
        {
            var timeline = _timeline;
            double delta = (screenPosition.X - _start.X) / timeline.PixelsPerSecond;
            switch (_part)
            {
                case LoopBarPart.Start:
                    timeline.Loop = _original with { Start = timeline.MovePosition(_original.Start, Math.Min(_endTime, _ruler.Snap(Math.Max(0, _startTime + delta), modifiers))) };
                    break;
                case LoopBarPart.End:
                    timeline.Loop = _original with { End = timeline.MovePosition(_original.End, Math.Max(_startTime, _ruler.Snap(_endTime + delta, modifiers))) };
                    break;
                case LoopBarPart.Body:
                    double start = _ruler.Snap(Math.Max(0, _startTime + delta), modifiers);
                    double shift = start - _startTime;
                    timeline.Loop = new TimelineRange(timeline.MovePosition(_original.Start, start), timeline.MovePosition(_original.End, _endTime + shift));
                    break;
                default:
                    double from = _ruler.Snap(Math.Max(0, _anchorTime), modifiers);
                    double to = _ruler.Snap(Math.Max(0, _anchorTime + delta), modifiers);
                    var unit = _ruler.PositionUnit;
                    timeline.Loop = new TimelineRange(timeline.CreatePosition(Math.Min(from, to), unit), timeline.CreatePosition(Math.Max(from, to), unit));
                    if (to != from) timeline.IsLoopEnabled = true;
                    break;
            }
        }

        public void Complete(Point screenPosition, ModifierKeys modifiers) => Update(screenPosition, modifiers);

        public void Cancel()
        {
            _timeline.Loop = _original;
            _timeline.IsLoopEnabled = _wasEnabled;
        }
    }
}

/// <summary>
/// Drags a handle of an <see cref="AudioWaveformEditor"/>: an edge of the audible region trims it (the audio stays in
/// place), a fade handle changes the fade's length, and the gain chip changes the gain (up for louder, Shift for fine
/// steps). Trims and fades snap to the grid (Shift inverts it). Escape puts everything back.
/// </summary>
public sealed class AudioEditorHandleDragCommand : DragCommand
{
    /// <inheritdoc/>
    /// <remarks>It applies only over a handle.</remarks>
    public override bool IsContextual => true;

    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is AudioWaveformEditor;

    /// <inheritdoc/>
    public override IDragOperation? BeginDrag(DragStart start)
    {
        if (start.Target is not AudioWaveformEditor editor) return null;
        var part = editor.PartAt(editor.PointToClient(start.ScreenPosition));
        if (part is AudioEditorPart.None or AudioEditorPart.Body) return null;
        editor.HighlightForDrag(part);
        return new Operation(editor, part, start.ScreenPosition);
    }

    private sealed class Operation(AudioWaveformEditor editor, AudioEditorPart part, Point start) : IDragOperation
    {
        private readonly (double TrimStart, double TrimEnd, double FadeIn, double FadeOut, float Gain) _original =
            (editor.TrimStart, editor.TrimEnd, editor.FadeIn, editor.FadeOut, editor.Gain);

        public void Update(Point screenPosition, ModifierKeys modifiers)
        {
            if (part == AudioEditorPart.Gain)
            {
                float step = (modifiers & ModifierKeys.Shift) != 0 ? AudioWaveformEditor.GainPerPixel / 10 : AudioWaveformEditor.GainPerPixel;
                editor.Gain = MathF.Round((_original.Gain - (screenPosition.Y - start.Y) * step) * 10) / 10;
                return;
            }
            double time = editor.Snap(editor.XToTime(editor.PointToClient(screenPosition).X), modifiers);
            double sound = editor.SoundStartSeconds;
            switch (part)
            {
                case AudioEditorPart.TrimStart: editor.SetTrimStart(time - sound); break;
                case AudioEditorPart.TrimEnd: editor.SetTrimEnd(time - sound); break;
                case AudioEditorPart.FadeIn: editor.SetFadeIn(time - editor.AudibleStart); break;
                case AudioEditorPart.FadeOut: editor.SetFadeOut(editor.AudibleEnd - time); break;
            }
        }

        public void Complete(Point screenPosition, ModifierKeys modifiers) => Update(screenPosition, modifiers);

        public void Cancel()
        {
            editor.TrimStart = _original.TrimStart;
            editor.TrimEnd = _original.TrimEnd;
            editor.FadeIn = _original.FadeIn;
            editor.FadeOut = _original.FadeOut;
            editor.Gain = _original.Gain;
        }
    }
}

/// <summary>
/// Selects a stretch of an <see cref="AudioWaveformEditor"/>'s sound by dragging across it, from where the drag started
/// to the pointer, snapped to the grid (Shift inverts it). Escape puts the previous selection back.
/// </summary>
public sealed class AudioEditorSelectDragCommand : DragCommand
{
    /// <inheritdoc/>
    /// <remarks>It applies only over the sound.</remarks>
    public override bool IsContextual => true;

    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is AudioWaveformEditor;

    /// <inheritdoc/>
    public override IDragOperation? BeginDrag(DragStart start)
    {
        if (start.Target is not AudioWaveformEditor editor) return null;
        var point = editor.PointToClient(start.ScreenPosition);
        return editor.PartAt(point) == AudioEditorPart.Body ? new Operation(editor, editor.XToTime(point.X)) : null;
    }

    private sealed class Operation(AudioWaveformEditor editor, double anchor) : IDragOperation
    {
        private readonly TimelineRange? _original = editor.Selection;

        public void Update(Point screenPosition, ModifierKeys modifiers)
        {
            var timeline = editor.CurrentTimeline;
            double from = editor.Snap(anchor, modifiers);
            double to = editor.Snap(editor.XToTime(editor.PointToClient(screenPosition).X), modifiers);
            var unit = editor.PositionUnit;
            editor.Selection = from == to ? null
                : new TimelineRange(timeline.CreatePosition(Math.Min(from, to), unit), timeline.CreatePosition(Math.Max(from, to), unit));
        }

        public void Complete(Point screenPosition, ModifierKeys modifiers) => Update(screenPosition, modifiers);

        public void Cancel() => editor.Selection = _original;
    }
}
