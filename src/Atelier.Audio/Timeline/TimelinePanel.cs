using System.ComponentModel;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Audio;

/// <summary>
/// A panel that places its children on the timeline, like clips on a track: each child sits at its attached
/// <see cref="StartProperty"/> and is <see cref="LengthProperty"/> long, both in seconds or beats, at the zoom and scroll
/// position of the <see cref="Timeline"/> context. Zooming resizes the children, scrolling moves them, and tempo changes
/// move beat-anchored ones.
/// </summary>
/// <remarks>
/// <para>
/// Put it in a <see cref="TimelineLane"/> (whose context it uses when it has none of its own) so the lane draws the grid
/// behind the clips and handles zooming and scrolling. Its left edge shows the context's <see cref="TimelineContext.Start"/>
/// like every scrolling timeline control. Children fill its height.
/// </para>
/// <para>
/// A child without a length (<see cref="TimelinePosition"/> with a NaN value, the default) is as wide as it wants to be.
/// Children entirely outside the panel are arranged empty and not drawn. Children far larger than the panel (a long clip
/// zoomed in to single samples) are cut to a margin around the panel, so coordinates stay precise; a
/// <see cref="WaveformView"/> in such a child still maps its pixels to the right samples (see <see cref="XToTime"/> and
/// <see cref="GetStartSeconds"/>).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// new TimelineLane().Timeline(song).Content(new TimelinePanel().Children(
///     new Border().TimelineStart(TimelinePosition.Bar(song.TempoMap, 5)).TimelineLength(TimelinePosition.Seconds(2))
///         .Child(new WaveformView().Source(kick))));
/// </code>
/// </example>
public class TimelinePanel : Panel
{
    /// <summary>Identifies the <see cref="Timeline"/> property.</summary>
    public static readonly BindableProperty<TimelineContext?> TimelineProperty =
        BindableProperty.Register<TimelinePanel, TimelineContext?>(nameof(Timeline), null, (s, o, n) => ((TimelinePanel)s).Resubscribe(), options: PropertyOptions.AffectsMeasure);

    /// <summary>Identifies the attached start of a child: where on the timeline it begins. The default is the start of the song.</summary>
    public static readonly BindableProperty<TimelinePosition> StartProperty =
        BindableProperty.RegisterAttached<TimelinePanel, UIElement, TimelinePosition>("Start", TimelinePosition.Zero, options: PropertyOptions.AffectsMeasure);

    /// <summary>Identifies the attached length of a child, in seconds or beats; a NaN value (the default) leaves the child as wide as it wants to be.</summary>
    public static readonly BindableProperty<TimelinePosition> LengthProperty =
        BindableProperty.RegisterAttached<TimelinePanel, UIElement, TimelinePosition>("Length", TimelinePosition.Seconds(double.NaN), options: PropertyOptions.AffectsMeasure);

    private TimelineContext? _subscribed;
    private TimelineContext? _ownTimeline;

    static TimelinePanel()
    {
        ClipToBoundsProperty.OverrideDefaultValue<TimelinePanel>(true);
    }

    /// <summary>
    /// Gets or sets the context whose view places the children; <c>null</c> (the default) uses the context of the
    /// timeline control the panel is in (such as its lane).
    /// </summary>
    public TimelineContext? Timeline { get => GetValue(TimelineProperty); set => SetValue(TimelineProperty, value); }

    /// <summary>Gets the context in use: <see cref="Timeline"/>, else the enclosing timeline control's, else one of the panel's own.</summary>
    public TimelineContext CurrentTimeline
    {
        get
        {
            if (Timeline is { } timeline) return timeline;
            for (var node = Parent; node != null; node = node.Parent)
            {
                if (node is TimelineControl control) return control.CurrentTimeline;
            }
            return _ownTimeline ??= new TimelineContext();
        }
    }

    /// <summary>Sets where a child begins on the timeline.</summary>
    public static void SetStart(UIElement element, TimelinePosition value) => element.SetValue(StartProperty, value);

    /// <summary>Gets where a child begins on the timeline.</summary>
    public static TimelinePosition GetStart(UIElement element) => element.GetValue(StartProperty);

    /// <summary>Sets how long a child is on the timeline; a NaN value leaves it as wide as it wants to be.</summary>
    public static void SetLength(UIElement element, TimelinePosition value) => element.SetValue(LengthProperty, value);

    /// <summary>Gets how long a child is on the timeline.</summary>
    public static TimelinePosition GetLength(UIElement element) => element.GetValue(LengthProperty);

    /// <summary>Gets where a child begins, in seconds.</summary>
    public double GetStartSeconds(UIElement child)
    {
        ArgumentNullException.ThrowIfNull(child);
        return GetStart(child).ToSeconds(CurrentTimeline.TempoMap);
    }

    /// <summary>Gets where a child ends in seconds, or NaN when it has no length.</summary>
    public double GetEndSeconds(UIElement child)
    {
        ArgumentNullException.ThrowIfNull(child);
        var length = GetLength(child);
        return double.IsNaN(length.Value) ? double.NaN : GetStart(child).GetEndSeconds(length, CurrentTimeline.TempoMap);
    }

    /// <summary>Gets the song time (seconds) at <paramref name="x"/> in the panel.</summary>
    public double XToTime(double x) => CurrentTimeline.XToTime(x);

    /// <summary>Gets the x in the panel of song time <paramref name="time"/> (seconds).</summary>
    public double TimeToX(double time) => CurrentTimeline.TimeToX(time);

    /// <inheritdoc/>
    /// <remarks>Each child is measured at its length in pixels (or unlimited without one) and the panel's height; the panel wants no width and its tallest child's height.</remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        var timeline = CurrentTimeline;
        float height = 0;
        foreach (var node in Children)
        {
            if (node is not UIElement child) continue;
            double end = GetEndSeconds(child);
            float width = double.IsNaN(end) ? float.PositiveInfinity
                : (float)Math.Clamp((end - GetStartSeconds(child)) * timeline.PixelsPerSecond, 0, MaxChildWidth(availableSize.Width));
            child.Measure(new Size(width, availableSize.Height));
            height = Math.Max(height, child.DesiredSize.Height);
        }
        return new Size(0, float.IsFinite(availableSize.Height) ? 0 : height);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Places each child at its start's x with its length's width and the panel's height. Children outside the panel get
    /// an empty rectangle; children reaching far beyond it are cut to one panel width (at least 256 px) on either side.
    /// </remarks>
    protected override Size ArrangeOverride(Size finalSize)
    {
        double margin = Math.Max(256, finalSize.Width);
        foreach (var node in Children)
        {
            if (node is not UIElement child) continue;
            double left = TimeToX(GetStartSeconds(child));
            double end = GetEndSeconds(child);
            double right = double.IsNaN(end) ? left + child.DesiredSize.Width : TimeToX(end);
            if (right < 0 || left > finalSize.Width || child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(Rect.Zero);
                continue;
            }
            left = Math.Max(left, -margin);
            right = Math.Min(right, finalSize.Width + margin);
            child.Arrange(new Rect((float)left, 0, (float)Math.Max(0, right - left), finalSize.Height));
        }
        return finalSize;
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        Resubscribe();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree()
    {
        base.OnDetachedFromVisualTree();
        Subscribe(null);
    }

    private static float MaxChildWidth(float available) => float.IsFinite(available) ? 3 * Math.Max(256, available) : float.MaxValue;

    private void Resubscribe()
    {
        if (IsAttachedToVisualTree) Subscribe(CurrentTimeline);
        InvalidateMeasure();
    }

    private void Subscribe(TimelineContext? context)
    {
        if (ReferenceEquals(_subscribed, context)) return;
        if (_subscribed != null) _subscribed.PropertyChanged -= OnContextChanged;
        _subscribed = context;
        if (context != null) context.PropertyChanged += OnContextChanged;
    }

    // Scrolling moves the children, zooming resizes them and tempo changes move beat-anchored ones.
    private void OnContextChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TimelineContext.Start)) InvalidateArrange();
        else if (e.PropertyName is nameof(TimelineContext.PixelsPerSecond) or nameof(TimelineContext.TempoMap)) InvalidateMeasure();
    }
}
