using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Rendering;

namespace Atelier.Audio;

/// <summary>What part of a <see cref="TimelineRuler"/>'s loop bar is at a point.</summary>
public enum LoopBarPart
{
    /// <summary>Not the loop bar.</summary>
    None,

    /// <summary>The loop bar outside the loop: dragging draws a new loop.</summary>
    Empty,

    /// <summary>The loop's start edge: dragging moves the start.</summary>
    Start,

    /// <summary>The loop's end edge: dragging moves the end.</summary>
    End,

    /// <summary>The loop between its edges: dragging moves the whole loop.</summary>
    Body,
}

/// <summary>The data of <see cref="TimelineRuler.MarkerAdding"/>: the new marker, which handlers can change, and whether to add it.</summary>
public sealed class TimelineMarkerAddingEventArgs(TimelineMarker marker) : EventArgs
{
    /// <summary>Gets the marker about to be added to the shared markers; handlers can change its label, color and position.</summary>
    public TimelineMarker Marker { get; } = marker;

    /// <summary>Gets or sets whether to leave the marker out.</summary>
    public bool Cancel { get; set; }
}

/// <summary>
/// A beat and time ruler, like the one above the tracks of a DAW: ticks and labels for bars and beats
/// (<c>5</c>, <c>5.2</c>, <c>5.2.3</c>), time (<c>01:05.250</c>) or samples that adapt to the zoom, with an optional
/// second row in another unit, colored marker flags above them and a loop bar below.
/// </summary>
/// <remarks>
/// <para>
/// The ruler shows its <see cref="TimelineControl.Timeline"/> context; give the same context to the tracks below it and
/// they zoom and scroll together. Labels are as frequent as their width allows: dragging the ruler up zooms in from bars
/// to beats to sixteenths (or from minutes to seconds to milliseconds), with finer unlabeled ticks between labels.
/// Bar lines are drawn stronger while beats are labeled.
/// </para>
/// <para>
/// From top to bottom the ruler has: the marker lane (<see cref="MarkerLaneHeight"/>, with <see cref="ShowMarkers"/>),
/// showing the shared <see cref="TimelineContext.Markers"/> and the ruler's own <see cref="TimelineControl.Markers"/> as
/// flags; the main row of ticks and labels (<see cref="RowHeight"/>) with the play start marker; the second row
/// (<see cref="SecondaryRowHeight"/>, with a <see cref="SecondaryMode"/>); and the loop bar (<see cref="LoopBarHeight"/>,
/// with <see cref="ShowLoopBar"/>) showing <see cref="TimelineContext.Loop"/>. The <see cref="TimelineContext.Playhead"/>
/// is a line through all of them.
/// </para>
/// <para>
/// Like Bitwig's ruler: clicking sets the play start marker; dragging zooms (up to zoom in, down to zoom out) around the
/// time where the drag started and scrolls sideways; the wheel zooms; double-clicking shows the whole song; and
/// right-clicking opens a menu to choose the units, insert a marker or turn the loop on. Drag a flag to move its marker
/// and click it to move the play start there; double-click the marker lane to add a marker, or a flag to rename it;
/// right-click a flag to recolor, rename or delete it; Delete removes the selected marker. On the loop bar, drag outside
/// the loop to draw a new one, drag its edges or body to change it, and double-click it to turn it on or off. Moves snap
/// to the grid in view (see <see cref="TimelineControl.SnapToGrid"/>; Shift inverts it). These are commands of
/// <see cref="TimelineCommands.RulerGroup"/>, and the ones of every timeline control work too
/// (<see cref="TimelineCommands.Group"/>).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var song = new TimelineContext { TempoMap = TempoMap.Constant(128) };
/// song.Markers.Add(new TimelineMarker(TimelinePosition.Bar(song.TempoMap, 9), "Verse", TimelineMarker.Palette[3].Color));
/// new StackPanel().Children(
///     new TimelineRuler().Timeline(song).SecondaryMode(TimelineRulerMode.Time),
///     new TimelineLane().Timeline(song).Height(64));
/// </code>
/// </example>
public class TimelineRuler : TimelineControl
{
    /// <summary>Identifies the <see cref="SecondaryMode"/> property.</summary>
    public static readonly BindableProperty<TimelineRulerMode?> SecondaryModeProperty =
        BindableProperty.Register<TimelineRuler, TimelineRulerMode?>(nameof(SecondaryMode), null, options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="SecondaryForeground"/> property.</summary>
    public static readonly BindableProperty<Color> SecondaryForegroundProperty =
        BindableProperty.Register<TimelineRuler, Color>(nameof(SecondaryForeground), Color.FromRgb(0x80, 0x80, 0x80), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="ShowMarkers"/> property.</summary>
    public static readonly BindableProperty<bool> ShowMarkersProperty =
        BindableProperty.Register<TimelineRuler, bool>(nameof(ShowMarkers), true, options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="ShowLoopBar"/> property.</summary>
    public static readonly BindableProperty<bool> ShowLoopBarProperty =
        BindableProperty.Register<TimelineRuler, bool>(nameof(ShowLoopBar), true, options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender);

    /// <summary>The height of the main row of ticks and labels, in pixels.</summary>
    public const float RowHeight = 26f;

    /// <summary>The height of the second row (with a <see cref="SecondaryMode"/>), in pixels.</summary>
    public const float SecondaryRowHeight = 16f;

    /// <summary>The height of the marker lane (with <see cref="ShowMarkers"/>), in pixels.</summary>
    public const float MarkerLaneHeight = 18f;

    /// <summary>The height of the loop bar (with <see cref="ShowLoopBar"/>), in pixels.</summary>
    public const float LoopBarHeight = 12f;

    /// <summary>How far from a loop edge (in pixels) the pointer grabs the edge.</summary>
    public const float LoopEdgeGrip = 5f;

    /// <summary>The padding around a flag's label, in pixels.</summary>
    public const float FlagPadding = 4f;

    private readonly TimelineGrid _secondaryGrid = new();
    private TimelineMarker? _selectedMarker;
    private Popup? _renamePopup;

    /// <summary>Initializes a ruler, running the <see cref="TimelineCommands.RulerGroup"/> commands on itself as well.</summary>
    public TimelineRuler()
    {
        AdditionalScopes.Add(new KeybindingScope(TimelineCommands.RulerGroup, this));
    }

    /// <summary>Occurs when a marker is about to be added from the ruler (double-click or menu); handlers can change it or cancel.</summary>
    public event EventHandler<TimelineMarkerAddingEventArgs>? MarkerAdding;

    /// <summary>Gets or sets what the second row of labels counts in, or <c>null</c> (the default) for no second row.</summary>
    public TimelineRulerMode? SecondaryMode { get => GetValue(SecondaryModeProperty); set => SetValue(SecondaryModeProperty, value); }

    /// <summary>Gets or sets the color of the second row's labels.</summary>
    public Color SecondaryForeground { get => GetValue(SecondaryForegroundProperty); set => SetValue(SecondaryForegroundProperty, value); }

    /// <summary>Gets or sets whether the ruler has the marker lane at its top. The default is <c>true</c>.</summary>
    public bool ShowMarkers { get => GetValue(ShowMarkersProperty); set => SetValue(ShowMarkersProperty, value); }

    /// <summary>Gets or sets whether the ruler has the loop bar at its bottom. The default is <c>true</c>.</summary>
    public bool ShowLoopBar { get => GetValue(ShowLoopBarProperty); set => SetValue(ShowLoopBarProperty, value); }

    /// <summary>Gets or sets the marker last clicked or moved (outlined; Delete removes it), or <c>null</c>.</summary>
    public TimelineMarker? SelectedMarker
    {
        get => _selectedMarker;
        set
        {
            if (_selectedMarker == value) return;
            _selectedMarker = value;
            InvalidateVisual();
        }
    }

    /// <summary>Gets the second row's grid as last computed by <see cref="UpdateSecondaryGrid"/>.</summary>
    public TimelineGrid SecondaryGrid => _secondaryGrid;

    /// <summary>Gets the marker lane's bounds (empty without <see cref="ShowMarkers"/>).</summary>
    public Rect MarkerLaneBounds => new(0, 0, Bounds.Width, ShowMarkers ? MarkerLaneHeight : 0);

    /// <summary>Gets the main row's bounds.</summary>
    public Rect RowBounds => new(0, MarkerLaneBounds.Bottom, Bounds.Width, RowHeight);

    /// <summary>Gets the second row's bounds (empty without a <see cref="SecondaryMode"/>).</summary>
    public Rect SecondaryRowBounds => new(0, RowBounds.Bottom, Bounds.Width, SecondaryMode != null ? SecondaryRowHeight : 0);

    /// <summary>Gets the loop bar's bounds (empty without <see cref="ShowLoopBar"/>).</summary>
    public Rect LoopBarBounds => new(0, SecondaryRowBounds.Bottom, Bounds.Width, ShowLoopBar ? LoopBarHeight : 0);

    /// <summary>Computes <see cref="SecondaryGrid"/> like <see cref="TimelineControl.UpdateGrid"/>; returns <c>null</c> without a <see cref="SecondaryMode"/>.</summary>
    public TimelineGrid? UpdateSecondaryGrid(float charWidth)
    {
        if (SecondaryMode is not { } mode) return null;
        _secondaryGrid.Mode = mode;
        _secondaryGrid.CharWidth = charWidth;
        _secondaryGrid.Update(CurrentTimeline, Bounds.Width);
        return _secondaryGrid;
    }

    /// <summary>Gets the bounds of a marker's flag in the marker lane: from its position to the end of its label.</summary>
    public Rect GetFlagBounds(TimelineMarker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        float x = TimeToX(marker.Position.ToSeconds(CurrentTimeline.TempoMap));
        float labelWidth = marker.Label.Length > 0 ? TextMeasurer.MeasureWidth(marker.Label, FontSize, FontFamily) : 0;
        var lane = MarkerLaneBounds;
        return new Rect(x, lane.Y + 2, labelWidth + 2 * FlagPadding + 2, Math.Max(0, lane.Height - 4));
    }

    /// <summary>Gets the marker whose flag is at <paramref name="point"/> (the topmost), or <c>null</c>.</summary>
    public TimelineMarker? MarkerAt(Point point)
    {
        if (!ShowMarkers || !MarkerLaneBounds.Contains(point)) return null;
        // The ruler's own markers are drawn over the shared ones, and later ones over earlier ones.
        for (int i = Markers.Count - 1; i >= 0; i--)
        {
            if (HitsFlag(Markers[i], point)) return Markers[i];
        }
        if (!ShowSharedMarkers) return null;
        var shared = CurrentTimeline.Markers;
        for (int i = shared.Count - 1; i >= 0; i--)
        {
            if (HitsFlag(shared[i], point)) return shared[i];
        }
        return null;
    }

    /// <summary>Gets the part of the loop bar at <paramref name="point"/>.</summary>
    public LoopBarPart LoopBarPartAt(Point point)
    {
        if (!ShowLoopBar || !LoopBarBounds.Contains(point)) return LoopBarPart.None;
        var timeline = CurrentTimeline;
        var loop = timeline.Loop;
        if (loop.IsEmpty(timeline.TempoMap)) return LoopBarPart.Empty;
        float start = TimeToX(loop.GetStartSeconds(timeline.TempoMap));
        float end = TimeToX(loop.GetEndSeconds(timeline.TempoMap));
        // Short loops: the nearer edge wins.
        float toStart = Math.Abs(point.X - start);
        float toEnd = Math.Abs(point.X - end);
        if (toStart <= LoopEdgeGrip || toEnd <= LoopEdgeGrip) return toStart < toEnd ? LoopBarPart.Start : LoopBarPart.End;
        return point.X > start && point.X < end ? LoopBarPart.Body : LoopBarPart.Empty;
    }

    /// <summary>Moves the play start marker to the (snapped) time at <paramref name="x"/>, in <see cref="TimelineControl.PositionUnit"/>.</summary>
    public void SetPlayStartAt(float x, ModifierKeys modifiers = ModifierKeys.None)
    {
        var timeline = CurrentTimeline;
        timeline.PlayStart = timeline.CreatePosition(Snap(XToTime(x), modifiers), PositionUnit);
    }

    /// <summary>
    /// Adds a shared marker at the (snapped) time at <paramref name="x"/>, named "Marker n" in the next
    /// <see cref="TimelineMarker.Palette"/> color, after <see cref="MarkerAdding"/>; returns it, or <c>null</c> when canceled.
    /// </summary>
    public TimelineMarker? AddMarkerAt(float x, ModifierKeys modifiers = ModifierKeys.None)
    {
        var timeline = CurrentTimeline;
        int count = timeline.Markers.Count;
        var marker = new TimelineMarker(timeline.CreatePosition(Snap(XToTime(x), modifiers), PositionUnit),
            $"Marker {count + 1}", TimelineMarker.Palette[count % TimelineMarker.Palette.Count].Color);
        var args = new TimelineMarkerAddingEventArgs(marker);
        MarkerAdding?.Invoke(this, args);
        if (args.Cancel) return null;
        timeline.Markers.Add(marker);
        SelectedMarker = marker;
        return marker;
    }

    /// <summary>Turns the loop on or off; turning on an empty loop makes it the bar (or visible grid step) at the play start.</summary>
    public void ToggleLoop()
    {
        var timeline = CurrentTimeline;
        if (!timeline.IsLoopEnabled && timeline.Loop.IsEmpty(timeline.TempoMap))
        {
            var map = timeline.TempoMap;
            double start = timeline.PlayStart.ToSeconds(map);
            var bar = map.GetBarPosition(map.SecondsToBeats(start));
            double from = map.BeatsToSeconds(map.BarToBeats(bar.Bar));
            double to = map.BeatsToSeconds(map.BarToBeats(bar.Bar + 4));
            timeline.Loop = new TimelineRange(timeline.CreatePosition(from, PositionUnit), timeline.CreatePosition(to, PositionUnit));
        }
        timeline.IsLoopEnabled = !timeline.IsLoopEnabled;
    }

    /// <summary>
    /// Shows a text box over the marker's flag to rename it: Enter or leaving it keeps the new name, Escape keeps the old.
    /// </summary>
    public void BeginRename(TimelineMarker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        _renamePopup?.SetValue(Popup.IsOpenProperty, false);
        var box = new TextBox { Text = marker.Label, MinWidth = 160, FieldHeight = 36, FontSize = 13 };
        var popup = new Popup { Child = box, PlacementTarget = this, Placement = PlacementMode.Pointer };
        bool done = false;
        void Finish(bool keep)
        {
            if (done) return;
            done = true;
            if (keep) marker.Label = box.Text.Trim();
            popup.IsOpen = false;
            if (_renamePopup == popup) _renamePopup = null;
            Focus();
        }
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { Finish(true); e.Handled = true; }
            else if (e.Key == Key.Escape) { Finish(false); e.Handled = true; }
        };
        box.LostFocus += (_, _) => Finish(true);
        popup.Closed += (_, _) => Finish(true);
        _renamePopup = popup;
        popup.IsOpen = true;
        box.Focus();
        box.SelectAll();
    }

    /// <summary>Opens the menu for what's at <paramref name="point"/>: a marker's menu over its flag, else the ruler's menu. Returns it, or <c>null</c> when it couldn't open.</summary>
    public ContextMenu? ShowContextMenu(Point? point = null)
    {
        var marker = point is { } p ? MarkerAt(p) : null;
        if (marker != null) SelectedMarker = marker;
        var menu = marker != null ? CreateMarkerMenu(marker) : CreateContextMenu(point?.X);
        return menu.Open(this) ? menu : null;
    }

    /// <summary>
    /// Creates the ruler's menu: the unit of the main row (Beats, Time, Samples), a "Second row" submenu (None or a unit),
    /// "Insert marker" (at <paramref name="x"/>, or the play start), "Loop", "Snap to grid" and "Zoom to fit". Override it
    /// to change the menu.
    /// </summary>
    public virtual ContextMenu CreateContextMenu(float? x = null)
    {
        var menu = new ContextMenu();
        foreach (var mode in Enum.GetValues<TimelineRulerMode>())
        {
            var item = new MenuItem(mode.ToString()) { IsCheckable = true, GroupName = "TimelineRulerMode", IsChecked = Mode == mode };
            item.Click += (_, _) => Mode = mode;
            menu.Items.Add(item);
        }
        var secondary = new MenuItem("Second row");
        var none = new MenuItem("None") { IsCheckable = true, GroupName = "TimelineRulerSecondaryMode", IsChecked = SecondaryMode == null };
        none.Click += (_, _) => SecondaryMode = null;
        secondary.Items.Add(none);
        foreach (var mode in Enum.GetValues<TimelineRulerMode>())
        {
            var item = new MenuItem(mode.ToString()) { IsCheckable = true, GroupName = "TimelineRulerSecondaryMode", IsChecked = SecondaryMode == mode };
            item.Click += (_, _) => SecondaryMode = mode;
            secondary.Items.Add(item);
        }
        menu.Items.Add(secondary);
        menu.Items.Add(new Separator());

        var insert = new MenuItem("Insert marker") { Icon = new Icon(MaterialIconKind.Flag, 18) };
        float at = x ?? TimeToX(CurrentTimeline.PlayStart.ToSeconds(CurrentTimeline.TempoMap));
        insert.Click += (_, _) => AddMarkerAt(at);
        menu.Items.Add(insert);
        menu.Items.Add(new MenuItem(null, TimelineCommands.ToggleLoop) { CommandParameter = this, IsCheckable = true, IsChecked = CurrentTimeline.IsLoopEnabled });
        menu.Items.Add(new MenuItem(null, TimelineCommands.ToggleSnap) { CommandParameter = this, IsCheckable = true, IsChecked = SnapToGrid });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem(null, TimelineCommands.ZoomToFit) { CommandParameter = this });
        return menu;
    }

    /// <summary>Creates a marker's menu: "Rename…", a "Color" submenu of <see cref="TimelineMarker.Palette"/> and "Delete". Override it to change the menu.</summary>
    public virtual ContextMenu CreateMarkerMenu(TimelineMarker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        var menu = new ContextMenu();
        var rename = new MenuItem("Rename…") { Icon = new Icon(MaterialIconKind.Edit, 18) };
        rename.Click += (_, _) => BeginRename(marker);
        menu.Items.Add(rename);
        var color = new MenuItem("Color") { Icon = new Icon(MaterialIconKind.Palette, 18) };
        foreach (var (name, value) in TimelineMarker.Palette)
        {
            var item = new MenuItem(name) { IsCheckable = true, GroupName = "TimelineMarkerColor", IsChecked = marker.Color == value };
            item.Click += (_, _) => marker.Color = value;
            color.Items.Add(item);
        }
        menu.Items.Add(color);
        menu.Items.Add(new Separator());
        var delete = new MenuItem("Delete") { Icon = new Icon(MaterialIconKind.Delete, 18) };
        delete.Click += (_, _) => DeleteMarker(marker);
        menu.Items.Add(delete);
        return menu;
    }

    /// <summary>Removes a marker (see <see cref="TimelineControl.RemoveMarker"/>), clearing the selection if it was selected.</summary>
    public void DeleteMarker(TimelineMarker marker)
    {
        ArgumentNullException.ThrowIfNull(marker);
        RemoveMarker(marker);
        if (SelectedMarker == marker) SelectedMarker = null;
    }

    /// <inheritdoc/>
    /// <remarks>The heights of the parts shown: marker lane, main row, second row and loop bar; as wide as it's given.</remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(availableSize);
        return new Size(0, (ShowMarkers ? MarkerLaneHeight : 0) + RowHeight + (SecondaryMode != null ? SecondaryRowHeight : 0) + (ShowLoopBar ? LoopBarHeight : 0));
    }

    private bool HitsFlag(TimelineMarker marker, Point point)
    {
        var flag = GetFlagBounds(marker);
        return point.X >= flag.X - 3 && point.X <= flag.Right;
    }
}
