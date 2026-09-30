using Atelier.Core.Primitives;
using Atelier.Core.Tree;

namespace Atelier.Audio;

/// <summary>
/// A track lane under a <see cref="TimelineRuler"/>: draws the timeline's grid lines (bar lines stronger, then beats and
/// their subdivisions, or seconds) and the loop's shade behind its <see cref="Atelier.Controls.ContentControl.Content"/>,
/// and the markers and the playhead over it; it zooms and scrolls the shared timeline like the other timeline controls.
/// </summary>
/// <remarks>
/// <para>
/// Give it the ruler's <see cref="TimelineControl.Timeline"/> context and put it in the ruler's column, and its lines
/// continue the ruler's ticks. For clips, make a <see cref="TimelinePanel"/> its content: the panel uses the lane's
/// context and places its children at their times.
/// </para>
/// <para>
/// Labeled and emphasized ticks are drawn in <see cref="TimelineControl.MajorLineColor"/>, the others in
/// <see cref="TimelineControl.LineColor"/> (the finest at half its opacity), over
/// <see cref="Atelier.Controls.Control.Background"/>. Keep <see cref="Atelier.Controls.Control.FontSize"/> the same as the
/// ruler's so both space their ticks alike.
/// </para>
/// </remarks>
public class TimelineLane : TimelineControl
{
    private readonly TimelineLaneOverlay _overlay;

    /// <summary>Initializes a lane.</summary>
    public TimelineLane()
    {
        _overlay = new TimelineLaneOverlay(this) { ZIndex = 1, IsHitTestVisible = false };
        AddChild(_overlay);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        _overlay.Measure(availableSize);
        return base.MeasureOverride(availableSize);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _overlay.Arrange(new Rect(Point.Zero, finalSize));
        return base.ArrangeOverride(finalSize);
    }
}

/// <summary>The layer over a <see cref="TimelineLane"/>'s content that shows the markers and the playhead.</summary>
internal sealed class TimelineLaneOverlay(TimelineLane lane) : UIElement
{
    public TimelineLane Lane { get; } = lane;
}
