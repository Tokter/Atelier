using Atelier.Core.Primitives;
using Atelier.Rendering;
using Atelier.Theming;

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

/// <summary>
/// Draws a <see cref="TimelineRuler"/>: its background, ticks from the bottom of the main row (labeled ticks across the
/// whole row with the label to their right, medium ticks 40% and minor ticks 22% of it), and the second row's labels.
/// </summary>
/// <remarks>
/// Labeled and emphasized ticks use <see cref="TimelineControl.MajorLineColor"/>, other ticks
/// <see cref="TimelineControl.LineColor"/>. Labels on emphasized ticks (bar lines while beats are labeled) are medium
/// weight, the others normal. A line in <see cref="TimelineControl.LineColor"/> closes the ruler at the bottom.
/// </remarks>
public sealed class TimelineRulerRenderer : ControlRenderer<TimelineRuler>
{
    private const float LabelInset = 4f;
    private const float SecondaryFontScale = 0.9f;

    private readonly CharWidthCache _charWidth = new();
    private readonly CharWidthCache _secondaryCharWidth = new();

    /// <inheritdoc/>
    public override void Render(TimelineRuler ruler, ref DrawingContext context)
    {
        float width = ruler.Bounds.Width;
        float height = ruler.Bounds.Height;
        if (width <= 0 || height <= 0) return;

        context.DrawRect(new Rect(0, 0, width, height), ruler.Background);
        float fontSize = ruler.FontSize;
        float secondaryFontSize = fontSize * SecondaryFontScale;
        bool hasSecondary = ruler.SecondaryMode != null;
        float row = hasSecondary ? Math.Max(0, height - TimelineRuler.SecondaryRowHeight) : height;
        var line = ruler.LineColor;
        var major = ruler.MajorLineColor;

        var grid = ruler.UpdateGrid(_charWidth.Get(ref context, fontSize));
        foreach (var tick in grid.Ticks)
        {
            float x = MathF.Round(tick.X) + 0.5f;
            bool strong = tick.Level == TimelineGrid.LabelLevel || tick.IsEmphasized;
            float top = tick.Level switch
            {
                TimelineGrid.LabelLevel => 0,
                TimelineGrid.MediumLevel => row * 0.6f,
                _ => row * 0.78f,
            };
            context.DrawLine(new Point(x, top), new Point(x, row), strong ? major : line, 1f);
            if (tick.Label is { } label)
            {
                context.DrawText(label, new Point(x + LabelInset, fontSize + 3), ruler.Foreground, fontSize, ruler.FontFamily,
                    tick.IsEmphasized ? FontWeight.Medium : FontWeight.Normal);
            }
        }

        if (ruler.UpdateSecondaryGrid(_secondaryCharWidth.Get(ref context, secondaryFontSize)) is { } secondary)
        {
            context.DrawLine(new Point(0, row + 0.5f), new Point(width, row + 0.5f), line, 1f);
            foreach (var tick in secondary.Ticks)
            {
                if (tick.Label is not { } label) continue;
                float x = MathF.Round(tick.X) + 0.5f;
                context.DrawLine(new Point(x, row), new Point(x, row + 4), line, 1f);
                context.DrawText(label, new Point(x + LabelInset - 1, row + secondaryFontSize + 2), ruler.SecondaryForeground,
                    secondaryFontSize, ruler.FontFamily, FontWeight.Normal);
            }
        }

        context.DrawLine(new Point(0, height - 0.5f), new Point(width, height - 0.5f), line, 1f);
    }
}

/// <summary>
/// Draws a <see cref="TimelineLane"/>: its background and a vertical line per tick of its grid, labeled and emphasized
/// ticks in <see cref="TimelineControl.MajorLineColor"/>, medium ticks in <see cref="TimelineControl.LineColor"/> and the
/// finest at half its opacity.
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

        context.DrawRect(new Rect(0, 0, width, height), lane.Background);
        var line = lane.LineColor;
        var minor = line.WithAlpha(line.A / 255f * 0.5f);
        var major = lane.MajorLineColor;
        var grid = lane.UpdateGrid(_charWidth.Get(ref context, lane.FontSize));
        foreach (var tick in grid.Ticks)
        {
            float x = MathF.Round(tick.X) + 0.5f;
            var color = tick.Level == TimelineGrid.LabelLevel || tick.IsEmphasized ? major
                : tick.Level == TimelineGrid.MediumLevel ? line
                : minor;
            context.DrawLine(new Point(x, 0), new Point(x, height), color, 1f);
        }
    }
}
