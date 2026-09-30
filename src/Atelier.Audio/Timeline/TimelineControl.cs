using System.ComponentModel;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;

namespace Atelier.Audio;

/// <summary>
/// The base of controls that show part of a song's timeline and scroll and zoom with it, such as
/// <see cref="TimelineRuler"/> and <see cref="TimelineLane"/>: they share a <see cref="Timeline"/> context, draw the
/// ticks of a <see cref="Grid"/>, and run the <see cref="TimelineCommands"/>.
/// </summary>
/// <remarks>
/// <para>
/// Controls given the same <see cref="TimelineContext"/> are connected: zooming or scrolling one of them moves all of them.
/// A control without one gets a context of its own (see <see cref="CurrentTimeline"/>). The left edge of a control shows
/// the context's <see cref="TimelineContext.Start"/>, wherever the control is; controls line up in time when their left
/// edges line up, such as in one column of a grid.
/// </para>
/// <para>
/// The control is a <see cref="KeybindingHandler"/> that runs the <see cref="TimelineCommands.Group"/> commands on itself
/// (zooming with Ctrl+Alt+wheel, scrolling with Shift+wheel and middle drag, and more; users can rebind them), plus the
/// group in <see cref="KeybindingHandler.Group"/> if one is set. A horizontal wheel or trackpad swipe scrolls.
/// </para>
/// </remarks>
public abstract class TimelineControl : KeybindingHandler
{
    /// <summary>Identifies the <see cref="Timeline"/> property.</summary>
    public static readonly BindableProperty<TimelineContext?> TimelineProperty =
        BindableProperty.Register<TimelineControl, TimelineContext?>(nameof(Timeline), null, (s, o, n) => ((TimelineControl)s).OnTimelineChanged(o, n));

    /// <summary>Identifies the <see cref="Mode"/> property.</summary>
    public static readonly BindableProperty<TimelineRulerMode> ModeProperty =
        BindableProperty.Register<TimelineControl, TimelineRulerMode>(nameof(Mode), TimelineRulerMode.Beats, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="LineColor"/> property.</summary>
    public static readonly BindableProperty<Color> LineColorProperty =
        BindableProperty.Register<TimelineControl, Color>(nameof(LineColor), Color.FromRgb(0x80, 0x80, 0x80).WithAlpha(0.3f), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="MajorLineColor"/> property.</summary>
    public static readonly BindableProperty<Color> MajorLineColorProperty =
        BindableProperty.Register<TimelineControl, Color>(nameof(MajorLineColor), Color.FromRgb(0x80, 0x80, 0x80), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="ShowSharedMarkers"/> property.</summary>
    public static readonly BindableProperty<bool> ShowSharedMarkersProperty =
        BindableProperty.Register<TimelineControl, bool>(nameof(ShowSharedMarkers), true, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="SnapToGrid"/> property.</summary>
    public static readonly BindableProperty<bool> SnapToGridProperty =
        BindableProperty.Register<TimelineControl, bool>(nameof(SnapToGrid), true);

    /// <summary>Identifies the <see cref="MarkerColor"/> property.</summary>
    public static readonly BindableProperty<Color> MarkerColorProperty =
        BindableProperty.Register<TimelineControl, Color>(nameof(MarkerColor), Color.FromRgb(0xFB, 0x8C, 0x00), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="LoopColor"/> property.</summary>
    public static readonly BindableProperty<Color> LoopColorProperty =
        BindableProperty.Register<TimelineControl, Color>(nameof(LoopColor), Color.FromRgb(0x42, 0x85, 0xF4), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="PlayheadColor"/> property.</summary>
    public static readonly BindableProperty<Color> PlayheadColorProperty =
        BindableProperty.Register<TimelineControl, Color>(nameof(PlayheadColor), Color.FromRgb(0xE5, 0x39, 0x35), options: PropertyOptions.AffectsRender);

    private const float DefaultCharWidth = 6.5f;

    private TimelineContext? _ownTimeline;
    private TimelineContext? _subscribed;
    private readonly TimelineGrid _grid = new();
    private float _charWidth = DefaultCharWidth;

    static TimelineControl()
    {
        AudioTheme.Register();
        IsFocusableProperty.OverrideDefaultValue<TimelineControl>(true);
        ClipToBoundsProperty.OverrideDefaultValue<TimelineControl>(true);
    }

    /// <summary>Initializes the control, running the <see cref="TimelineCommands.Group"/> commands on itself.</summary>
    protected TimelineControl()
    {
        TimelineCommands.Register();
        AdditionalScopes.Add(new KeybindingScope(TimelineCommands.Group, this));
        Markers.Changed += (_, _) => InvalidateVisual();
    }

    /// <summary>
    /// Gets or sets the context shared with connected controls: the view (zoom and scroll), tempo map and sample rate.
    /// The default is <c>null</c>: the control has a context of its own.
    /// </summary>
    public TimelineContext? Timeline { get => GetValue(TimelineProperty); set => SetValue(TimelineProperty, value); }

    /// <summary>Gets the context in use: <see cref="Timeline"/>, or the control's own when that isn't set.</summary>
    public TimelineContext CurrentTimeline => Timeline ?? (_ownTimeline ??= new TimelineContext());

    /// <summary>Gets or sets what the ticks count in: bars and beats, time or samples. The default is <see cref="TimelineRulerMode.Beats"/>.</summary>
    public TimelineRulerMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    /// <summary>Gets or sets the color of the minor ticks or grid lines.</summary>
    public Color LineColor { get => GetValue(LineColorProperty); set => SetValue(LineColorProperty, value); }

    /// <summary>Gets or sets the color of the labeled and emphasized ticks or grid lines (such as bar lines).</summary>
    public Color MajorLineColor { get => GetValue(MajorLineColorProperty); set => SetValue(MajorLineColorProperty, value); }

    /// <summary>
    /// Gets or sets whether the control shows the shared markers of its context (<see cref="TimelineContext.Markers"/>)
    /// besides its own <see cref="Markers"/>. The default is <c>true</c>.
    /// </summary>
    public bool ShowSharedMarkers { get => GetValue(ShowSharedMarkersProperty); set => SetValue(ShowSharedMarkersProperty, value); }

    /// <summary>
    /// Gets or sets whether dragging and clicking snap to the finest ticks in view (the adaptive grid); holding Shift
    /// does the opposite, like in Bitwig. The default is <c>true</c>.
    /// </summary>
    public bool SnapToGrid { get => GetValue(SnapToGridProperty); set => SetValue(SnapToGridProperty, value); }

    /// <summary>Gets or sets the color of markers without a color of their own.</summary>
    public Color MarkerColor { get => GetValue(MarkerColorProperty); set => SetValue(MarkerColorProperty, value); }

    /// <summary>Gets or sets the color of the loop.</summary>
    public Color LoopColor { get => GetValue(LoopColorProperty); set => SetValue(LoopColorProperty, value); }

    /// <summary>Gets or sets the color of the playhead and the play start marker.</summary>
    public Color PlayheadColor { get => GetValue(PlayheadColorProperty); set => SetValue(PlayheadColorProperty, value); }

    /// <summary>Gets the markers only this control shows, besides the shared ones (see <see cref="ShowSharedMarkers"/>).</summary>
    public TimelineMarkerCollection Markers { get; } = [];

    /// <summary>
    /// Gets the unit new positions get (markers, the play start, a new loop): beats when the control counts in
    /// <see cref="TimelineRulerMode.Beats"/>, seconds otherwise. Moving a position keeps its unit.
    /// </summary>
    public TimelineUnit PositionUnit => Mode == TimelineRulerMode.Beats ? TimelineUnit.Beats : TimelineUnit.Seconds;

    /// <summary>Gets the grid of <see cref="Mode"/> as last computed by <see cref="UpdateGrid"/> (when the control was drawn).</summary>
    public TimelineGrid Grid => _grid;

    /// <summary>
    /// Snaps a song time (seconds) to the finest ticks in view when <see cref="SnapToGrid"/> is on, or off while Shift is
    /// held (Shift inverts snapping).
    /// </summary>
    public double Snap(double time, ModifierKeys modifiers = ModifierKeys.None)
    {
        bool snap = SnapToGrid != ((modifiers & ModifierKeys.Shift) != 0);
        if (!snap) return time;
        _grid.Mode = Mode;
        _grid.CharWidth = _charWidth;
        _grid.Update(CurrentTimeline, Bounds.Width);
        return Math.Max(0, _grid.Snap(time));
    }

    /// <summary>Removes a marker from the control's own markers or, failing that, the shared ones; returns whether it was found.</summary>
    public bool RemoveMarker(TimelineMarker marker) => Markers.Remove(marker) || CurrentTimeline.Markers.Remove(marker);

    /// <summary>Gets where the pointer is over the control, in its coordinates, or <c>null</c> when it isn't over it. Zoom commands zoom around it.</summary>
    public Point? PointerPosition { get; private set; }

    /// <summary>
    /// Computes <see cref="Grid"/> for the control's width and the current view, with labels spaced for characters of
    /// <paramref name="charWidth"/> pixels. Renderers call it before drawing the ticks.
    /// </summary>
    public TimelineGrid UpdateGrid(float charWidth)
    {
        _charWidth = charWidth;
        _grid.Mode = Mode;
        _grid.CharWidth = charWidth;
        _grid.Update(CurrentTimeline, Bounds.Width);
        return _grid;
    }

    /// <summary>Gets the song time (seconds) at <paramref name="x"/> in the control.</summary>
    public double XToTime(float x) => CurrentTimeline.XToTime(x);

    /// <summary>Gets the x in the control of song time <paramref name="time"/> (seconds).</summary>
    public float TimeToX(double time) => (float)CurrentTimeline.TimeToX(time);

    /// <summary>Zooms by <paramref name="factor"/> around the pointer, or the middle of the control when the pointer isn't over it.</summary>
    public void ZoomAroundPointer(double factor) =>
        CurrentTimeline.ZoomAt(factor, PointerPosition?.X ?? Bounds.Width * 0.5f);

    /// <summary>Shows the whole song (up to <see cref="TimelineContext.Duration"/>) across the control's width.</summary>
    public void ZoomToFit() => CurrentTimeline.ZoomToFit(Bounds.Width);

    /// <inheritdoc/>
    /// <remarks>Takes the focus (for the timeline shortcuts) unless an element inside takes it.</remarks>
    public override void OnPreviewPointerPressed(PointerEventArgs e)
    {
        PointerPosition = e.Position;
        base.OnPreviewPointerPressed(e);
        if (!IsFocused) Focus();
    }

    /// <inheritdoc/>
    public override void OnPreviewPointerMoved(PointerEventArgs e)
    {
        PointerPosition = e.Position;
        base.OnPreviewPointerMoved(e);
    }

    /// <inheritdoc/>
    public override void OnPreviewPointerWheel(PointerWheelEventArgs e)
    {
        PointerPosition = e.Position;
        base.OnPreviewPointerWheel(e);
    }

    /// <inheritdoc/>
    public override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!IsPointerCaptured) PointerPosition = null;
    }

    /// <inheritdoc/>
    /// <remarks>After the keybindings (which handle vertical wheel turns), a horizontal wheel or swipe scrolls the timeline.</remarks>
    public override void OnPointerWheel(PointerWheelEventArgs e)
    {
        base.OnPointerWheel(e);
        if (e.Handled || e.DeltaX == 0) return;
        CurrentTimeline.ScrollBy(-e.DeltaX * TimelineCommands.WheelScrollPixels);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        Subscribe(CurrentTimeline);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree()
    {
        base.OnDetachedFromVisualTree();
        Subscribe(null);
    }

    private void OnTimelineChanged(TimelineContext? oldValue, TimelineContext? newValue)
    {
        if (_subscribed != null) Subscribe(CurrentTimeline);
        InvalidateVisual();
    }

    // Listens to the context while the control is in a window, so a context outliving it doesn't keep it alive.
    private void Subscribe(TimelineContext? context)
    {
        if (ReferenceEquals(_subscribed, context)) return;
        if (_subscribed != null)
        {
            _subscribed.PropertyChanged -= OnContextChanged;
            _subscribed.Markers.Changed -= OnContextChanged;
        }
        _subscribed = context;
        if (context != null)
        {
            context.PropertyChanged += OnContextChanged;
            context.Markers.Changed += OnContextChanged;
        }
    }

    // The view, the song and the shared markers are all drawn.
    private void OnContextChanged(object? sender, EventArgs e) => InvalidateVisual();
}
