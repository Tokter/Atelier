using Atelier.Core.Primitives;
using Atelier.Rendering;
using Atelier.Theming;
using SkiaSharp;

namespace Atelier.Audio.Renderers;

/// <summary>Measures the width of a digit per font size once, for spacing timeline labels.</summary>
internal sealed class CharWidthCache
{
    private float _fontSize = float.NaN;
    private float _width;

    public float Get(ref DrawingContext context, float fontSize)
    {
        if (fontSize != _fontSize)
        {
            _fontSize = fontSize;
            _width = context.MeasureText("0", fontSize).Width;
        }
        return _width;
    }
}

/// <summary>Drawing shared by the timeline renderers.</summary>
internal static class TimelineDrawing
{
    /// <summary>The opacity of the loop's shade over lanes and the ruler's rows while it's on.</summary>
    public const float LoopShadeOpacity = 0.12f;

    /// <summary>Gets a marker's color: its own, or the control's <see cref="TimelineControl.MarkerColor"/>.</summary>
    public static Color MarkerColor(TimelineControl control, TimelineMarker marker) => marker.Color.A == 0 ? control.MarkerColor : marker.Color;

    /// <summary>Gets dark or light text for a background color, whichever reads better.</summary>
    public static Color TextOn(Color background) =>
        0.299f * background.R + 0.587f * background.G + 0.114f * background.B > 150 ? Color.FromRgb(0x1C, 0x1B, 0x1F) : Color.White;

    /// <summary>Draws a 1 px vertical line on the pixel grid.</summary>
    public static void VerticalLine(ref DrawingContext context, float x, float top, float bottom, Color color) =>
        context.DrawLine(new Point(MathF.Round(x) + 0.5f, top), new Point(MathF.Round(x) + 0.5f, bottom), color, 1f);

    /// <summary>Shades the loop across <paramref name="top"/> to <paramref name="bottom"/> while it's on.</summary>
    public static void LoopShade(ref DrawingContext context, TimelineControl control, float width, float top, float bottom)
    {
        var timeline = control.CurrentTimeline;
        if (!timeline.IsLoopEnabled || timeline.Loop.IsEmpty(timeline.TempoMap)) return;
        float start = Math.Max(0, control.TimeToX(timeline.Loop.GetStartSeconds(timeline.TempoMap)));
        float end = Math.Min(width, control.TimeToX(timeline.Loop.GetEndSeconds(timeline.TempoMap)));
        if (end > start) context.DrawRect(new Rect(start, top, end - start, bottom - top), control.LoopColor.WithAlpha(LoopShadeOpacity));
    }

    /// <summary>Draws the playhead across <paramref name="top"/> to <paramref name="bottom"/> when there is one.</summary>
    public static void Playhead(ref DrawingContext context, TimelineControl control, float width, float top, float bottom)
    {
        if (control.CurrentTimeline.Playhead is not { } time) return;
        float x = control.TimeToX(time);
        if (x >= -1 && x <= width + 1) VerticalLine(ref context, x, top, bottom, control.PlayheadColor);
    }
}

/// <summary>
/// Draws a <see cref="TimelineRuler"/>: its background; the marker lane with a colored flag per marker (the label on a
/// flag in the marker's color, the selected one outlined, and a line down through the main row); the main row's ticks
/// from its bottom (labeled ticks across the whole row with the label to their right, medium ticks 40% and minor ticks
/// 22% of it) and the play start marker, a triangle at its top; the second row's labels; the loop bar with the loop
/// (in full color while it's on, faded while it's off); and the playhead through everything.
/// </summary>
/// <remarks>
/// Labeled and emphasized ticks use <see cref="TimelineControl.MajorLineColor"/>, other ticks
/// <see cref="TimelineControl.LineColor"/>. Labels on emphasized ticks (bar lines while beats are labeled) are medium
/// weight, the others normal. While the loop is on it also shades the rows. Lines in
/// <see cref="TimelineControl.LineColor"/> separate the parts.
/// </remarks>
public sealed class TimelineRulerRenderer : ControlRenderer<TimelineRuler>
{
    private const float LabelInset = 4f;
    private const float SecondaryFontScale = 0.9f;
    private const float PlayStartSize = 6f;

    private readonly CharWidthCache _charWidth = new();
    private readonly CharWidthCache _secondaryCharWidth = new();
    private static readonly SKPath PlayStartTriangle = CreatePlayStartTriangle();

    /// <inheritdoc/>
    public override void Render(TimelineRuler ruler, ref DrawingContext context)
    {
        float width = ruler.Bounds.Width;
        float height = ruler.Bounds.Height;
        if (width <= 0 || height <= 0) return;

        // Labels and flags near the edges would spill out: ClipToBounds clips children, not the control's own drawing.
        using var clip = context.PushClip(new Rect(0, 0, width, height));
        context.DrawRect(new Rect(0, 0, width, height), ruler.Background);
        var line = ruler.LineColor;
        var lane = ruler.MarkerLaneBounds;
        var row = ruler.RowBounds;
        var secondaryRow = ruler.SecondaryRowBounds;
        var loopBar = ruler.LoopBarBounds;
        TimelineDrawing.LoopShade(ref context, ruler, width, row.Y, secondaryRow.Bottom);

        DrawTicks(ruler, ref context, row);
        DrawSecondaryRow(ruler, ref context, secondaryRow, width);
        if (loopBar.Height > 0) DrawLoopBar(ruler, ref context, loopBar, width);
        if (lane.Height > 0)
        {
            context.DrawLine(new Point(0, lane.Bottom - 0.5f), new Point(width, lane.Bottom - 0.5f), line, 1f);
            DrawMarkers(ruler, ref context, row.Bottom, width);
        }
        DrawPlayStart(ruler, ref context, row, width);
        TimelineDrawing.Playhead(ref context, ruler, width, 0, height);
        context.DrawLine(new Point(0, height - 0.5f), new Point(width, height - 0.5f), line, 1f);
    }

    private void DrawTicks(TimelineRuler ruler, ref DrawingContext context, Rect row)
    {
        float fontSize = ruler.FontSize;
        var line = ruler.LineColor;
        var major = ruler.MajorLineColor;
        var grid = ruler.UpdateGrid(_charWidth.Get(ref context, fontSize));
        foreach (var tick in grid.Ticks)
        {
            bool strong = tick.Level == TimelineGrid.LabelLevel || tick.IsEmphasized;
            float top = tick.Level switch
            {
                TimelineGrid.LabelLevel => row.Y,
                TimelineGrid.MediumLevel => row.Y + row.Height * 0.6f,
                _ => row.Y + row.Height * 0.78f,
            };
            TimelineDrawing.VerticalLine(ref context, tick.X, top, row.Bottom, strong ? major : line);
            if (tick.Label is { } label)
            {
                context.DrawText(label, new Point(MathF.Round(tick.X) + 0.5f + LabelInset, row.Y + fontSize + 3), ruler.Foreground, fontSize,
                    ruler.FontFamily, tick.IsEmphasized ? FontWeight.Medium : FontWeight.Normal);
            }
        }
    }

    private void DrawSecondaryRow(TimelineRuler ruler, ref DrawingContext context, Rect row, float width)
    {
        float fontSize = ruler.FontSize * SecondaryFontScale;
        if (ruler.UpdateSecondaryGrid(_secondaryCharWidth.Get(ref context, fontSize)) is not { } grid) return;
        var line = ruler.LineColor;
        context.DrawLine(new Point(0, row.Y + 0.5f), new Point(width, row.Y + 0.5f), line, 1f);
        foreach (var tick in grid.Ticks)
        {
            if (tick.Label is not { } label) continue;
            TimelineDrawing.VerticalLine(ref context, tick.X, row.Y, row.Y + 4, line);
            context.DrawText(label, new Point(MathF.Round(tick.X) + LabelInset, row.Y + fontSize + 2), ruler.SecondaryForeground,
                fontSize, ruler.FontFamily, FontWeight.Normal);
        }
    }

    private static void DrawLoopBar(TimelineRuler ruler, ref DrawingContext context, Rect bar, float width)
    {
        context.DrawLine(new Point(0, bar.Y + 0.5f), new Point(width, bar.Y + 0.5f), ruler.LineColor, 1f);
        var timeline = ruler.CurrentTimeline;
        if (timeline.Loop.IsEmpty(timeline.TempoMap)) return;
        float start = ruler.TimeToX(timeline.Loop.GetStartSeconds(timeline.TempoMap));
        float end = ruler.TimeToX(timeline.Loop.GetEndSeconds(timeline.TempoMap));
        if (end < 0 || start > width) return;
        var color = timeline.IsLoopEnabled ? ruler.LoopColor : ruler.LoopColor.WithAlpha(0.35f);
        context.DrawRoundedRect(new Rect(start, bar.Y + 3, Math.Max(2, end - start), bar.Height - 5), new CornerRadius(3), color);
    }

    private static void DrawMarkers(TimelineRuler ruler, ref DrawingContext context, float lineBottom, float width)
    {
        if (ruler.ShowSharedMarkers)
        {
            foreach (var marker in ruler.CurrentTimeline.Markers) DrawFlag(ruler, ref context, marker, lineBottom, width);
        }
        foreach (var marker in ruler.Markers) DrawFlag(ruler, ref context, marker, lineBottom, width);
    }

    private static void DrawFlag(TimelineRuler ruler, ref DrawingContext context, TimelineMarker marker, float lineBottom, float width)
    {
        var flag = ruler.GetFlagBounds(marker);
        if (flag.Right < 0 || flag.X > width) return;
        var color = TimelineDrawing.MarkerColor(ruler, marker);
        TimelineDrawing.VerticalLine(ref context, flag.X, flag.Y, lineBottom, color);
        var shape = new Rect(MathF.Round(flag.X), flag.Y, flag.Width, flag.Height);
        context.DrawRoundedRect(shape, new CornerRadius(0, 3, 3, 0), color);
        if (marker == ruler.SelectedMarker) context.DrawRoundedRectOutline(shape, new CornerRadius(0, 3, 3, 0), ruler.Foreground, 1.5f);
        if (marker.Label.Length > 0)
        {
            float fontSize = ruler.FontSize;
            context.DrawText(marker.Label, new Point(shape.X + TimelineRuler.FlagPadding + 1, flag.Y + flag.Height * 0.5f + fontSize * 0.36f),
                TimelineDrawing.TextOn(color), fontSize, ruler.FontFamily, FontWeight.Medium);
        }
    }

    private static void DrawPlayStart(TimelineRuler ruler, ref DrawingContext context, Rect row, float width)
    {
        var timeline = ruler.CurrentTimeline;
        float x = MathF.Round(ruler.TimeToX(timeline.PlayStart.ToSeconds(timeline.TempoMap))) + 0.5f;
        if (x < -PlayStartSize || x > width + PlayStartSize) return;
        int save = context.Canvas.Save();
        context.Canvas.Translate(x, row.Y);
        context.DrawPath(PlayStartTriangle, ruler.PlayheadColor);
        context.Canvas.RestoreToCount(save);
    }

    // A triangle pointing down from the top of the row, its tip at x = 0.
    private static SKPath CreatePlayStartTriangle()
    {
        using var builder = new SKPathBuilder();
        builder.MoveTo(-PlayStartSize, 0);
        builder.LineTo(PlayStartSize, 0);
        builder.LineTo(0, PlayStartSize * 1.2f);
        builder.Close();
        return builder.Detach();
    }
}

/// <summary>
/// Draws a <see cref="TimelineLane"/>: its background; a vertical line per tick of its grid (labeled and emphasized ticks
/// in <see cref="TimelineControl.MajorLineColor"/>, medium ticks in <see cref="TimelineControl.LineColor"/> and the finest
/// at half its opacity); the loop's shade while it's on; a line per marker (shared and its own) in the marker's color;
/// and the playhead.
/// </summary>
public sealed class TimelineLaneRenderer : ControlRenderer<TimelineLane>
{
    private readonly CharWidthCache _charWidth = new();

    /// <inheritdoc/>
    public override void Render(TimelineLane lane, ref DrawingContext context)
    {
        float width = lane.Bounds.Width;
        float height = lane.Bounds.Height;
        if (width <= 0 || height <= 0) return;

        using var clip = context.PushClip(new Rect(0, 0, width, height));
        context.DrawRect(new Rect(0, 0, width, height), lane.Background);
        var line = lane.LineColor;
        var minor = line.WithAlpha(line.A / 255f * 0.5f);
        var major = lane.MajorLineColor;
        var grid = lane.UpdateGrid(_charWidth.Get(ref context, lane.FontSize));
        foreach (var tick in grid.Ticks)
        {
            var color = tick.Level == TimelineGrid.LabelLevel || tick.IsEmphasized ? major
                : tick.Level == TimelineGrid.MediumLevel ? line
                : minor;
            TimelineDrawing.VerticalLine(ref context, tick.X, 0, height, color);
        }

        TimelineDrawing.LoopShade(ref context, lane, width, 0, height);
        var map = lane.CurrentTimeline.TempoMap;
        if (lane.ShowSharedMarkers)
        {
            foreach (var marker in lane.CurrentTimeline.Markers) DrawMarkerLine(lane, ref context, marker, map, width, height);
        }
        foreach (var marker in lane.Markers) DrawMarkerLine(lane, ref context, marker, map, width, height);
        TimelineDrawing.Playhead(ref context, lane, width, 0, height);
    }

    private static void DrawMarkerLine(TimelineLane lane, ref DrawingContext context, TimelineMarker marker, TempoMap map, float width, float height)
    {
        float x = lane.TimeToX(marker.Position.ToSeconds(map));
        if (x < -1 || x > width + 1) return;
        TimelineDrawing.VerticalLine(ref context, x, 0, height, TimelineDrawing.MarkerColor(lane, marker).WithAlpha(0.8f));
    }
}
