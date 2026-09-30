namespace Atelier.Audio;

/// <summary>
/// A track lane under a <see cref="TimelineRuler"/>: draws the timeline's grid lines (bar lines stronger, then beats and
/// their subdivisions, or seconds) behind its <see cref="Atelier.Controls.ContentControl.Content"/>, and zooms and
/// scrolls the shared timeline like the other timeline controls.
/// </summary>
/// <remarks>
/// Give it the ruler's <see cref="TimelineControl.Timeline"/> context and put it in the ruler's column, and its lines
/// continue the ruler's ticks. Labeled and emphasized ticks are drawn in <see cref="TimelineControl.MajorLineColor"/>,
/// the others in <see cref="TimelineControl.LineColor"/> (the finest at half its opacity), over
/// <see cref="Atelier.Controls.Control.Background"/>. Keep <see cref="Atelier.Controls.Control.FontSize"/> the same as the
/// ruler's so both space their ticks alike.
/// </remarks>
public class TimelineLane : TimelineControl
{
}
