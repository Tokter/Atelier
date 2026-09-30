using Atelier.Core.Primitives;
using Atelier.Rendering;
using Atelier.Theming;

namespace Atelier.Audio.Renderers;

/// <summary>
/// Draws an <see cref="AudioWaveformEditor"/>: its background, grid and loop shade like a <see cref="TimelineLane"/>; the
/// parts of the sound trimmed away dimmed; the audible region tinted, with the waveform scaled by the gain and the fades
/// in <see cref="Atelier.Controls.Control.Foreground"/>; the fades as curves over a darker shade; the
/// <see cref="AudioWaveformEditor.Selection"/>; the trim edges, the fade handles and the gain chip in
/// <see cref="AudioWaveformEditor.HandleColor"/> (the one under the pointer brighter); and the markers and the playhead
/// on top.
/// </summary>
public sealed class AudioWaveformEditorRenderer : ControlRenderer<AudioWaveformEditor>
{
    private const float TrimmedOpacity = 0.3f;
    private const float AudibleTint = 0.06f;
    private const float FadeShade = 0.22f;

    private readonly CharWidthCache _charWidth = new();

    /// <inheritdoc/>
    public override void Render(AudioWaveformEditor editor, ref DrawingContext context)
    {
        float width = editor.Bounds.Width;
        float height = editor.Bounds.Height;
        if (width <= 0 || height <= 0) return;

        using var clip = context.PushClip(new Rect(0, 0, width, height));
        context.DrawRect(new Rect(0, 0, width, height), editor.Background);
        TimelineDrawing.GridLines(ref context, editor, editor.UpdateGrid(_charWidth.Get(ref context, editor.FontSize)), height);
        TimelineDrawing.LoopShade(ref context, editor, width, 0, height);

        if (editor.Source is { } source)
        {
            var visible = context.Canvas.LocalClipBounds;
            double from = Math.Max(0, visible.Left), to = Math.Min(width, visible.Right);
            DrawSound(editor, source, ref context, width, height, from, to);
        }

        DrawSelection(editor, ref context, width, height);
        if (editor.Source != null) DrawHandles(editor, ref context, width);
        TimelineDrawing.MarkerLines(ref context, editor, width, height);
        TimelineDrawing.Playhead(ref context, editor, width, 0, height);
    }

    private static void DrawSound(AudioWaveformEditor editor, WaveformData source, ref DrawingContext context, float width, float height, double from, double to)
    {
        var timeline = editor.CurrentTimeline;
        double soundStart = editor.SoundStartSeconds;
        double timeAtZero = timeline.Start - soundStart;
        double pixelsPerSecond = timeline.PixelsPerSecond;
        float top = AudioWaveformEditor.HandleStripHeight;
        float waveHeight = Math.Max(0, height - top - 2);
        var color = editor.Foreground;
        var style = new WaveformStyle(color, editor.ChannelLayout, 1, editor.ShowRms, 0.5f, Color.Transparent);
        var gain = editor.GetGain();
        double trimStart = editor.TrimStart, trimEnd = editor.EffectiveTrimEnd;

        // The audible region's tint, and the parts trimmed away dimmed (at the gain, without the fades).
        float left = editor.TimeToX(soundStart + trimStart), right = editor.TimeToX(soundStart + trimEnd);
        context.DrawRect(new Rect(left, 0, Math.Max(0, right - left), height), color.WithAlpha(AudibleTint));
        var dimmed = style with { Color = color.WithAlpha(color.A / 255f * TrimmedOpacity) };
        var level = WaveformGain.Identity with { Gain = gain.Gain };
        WaveformDrawing.Draw(ref context, source, timeAtZero, pixelsPerSecond, 0, trimStart, from, to, top, waveHeight, dimmed, level);
        WaveformDrawing.Draw(ref context, source, timeAtZero, pixelsPerSecond, trimEnd, source.Duration, from, to, top, waveHeight, dimmed, level);
        WaveformDrawing.Draw(ref context, source, timeAtZero, pixelsPerSecond, trimStart, trimEnd, from, to, top, waveHeight, style, gain);

        // The fades: a shade above each curve, and the curve from silence at the bottom to full level at the top.
        var fades = gain with { Gain = 1 };
        var handle = editor.HandleColor;
        if (editor.FadeIn > 0) DrawFade(ref context, editor, fades, soundStart + trimStart, soundStart + gain.FadeInEnd, top, waveHeight, from, to, handle);
        if (editor.FadeOut > 0) DrawFade(ref context, editor, fades, soundStart + gain.FadeOutStart, soundStart + trimEnd, top, waveHeight, from, to, handle);
    }

    private static void DrawFade(ref DrawingContext context, AudioWaveformEditor editor, in WaveformGain fades, double start, double end,
        float top, float height, double from, double to, Color color)
    {
        double sound = editor.SoundStartSeconds;
        int first = (int)Math.Max(Math.Floor(from), Math.Floor(editor.TimeToX(start)));
        int last = (int)Math.Min(Math.Ceiling(to), Math.Ceiling(editor.TimeToX(end)));
        var shade = Color.Black.WithAlpha(FadeShade);
        Point? previous = null;
        for (int x = first; x <= last; x += 2)
        {
            double time = Math.Clamp(editor.XToTime(x), start, end);
            float curve = top + (1 - fades.At(time - sound)) * height;
            if (x < last) context.DrawRect(new Rect(x, top, 2, curve - top), shade);
            var point = new Point(x, curve);
            if (previous is { } p) context.DrawLine(p, point, color, 1.5f);
            previous = point;
        }
    }

    private static void DrawSelection(AudioWaveformEditor editor, ref DrawingContext context, float width, float height)
    {
        if (editor.Selection is not { } selection) return;
        var map = editor.CurrentTimeline.TempoMap;
        float start = editor.TimeToX(selection.GetStartSeconds(map));
        float end = editor.TimeToX(selection.GetEndSeconds(map));
        if (end < 0 || start > width) return;
        var color = editor.SelectionColor;
        context.DrawRect(new Rect(start, 0, end - start, height), color.WithAlpha(0.22f));
        TimelineDrawing.VerticalLine(ref context, start, 0, height, color);
        TimelineDrawing.VerticalLine(ref context, end - 1, 0, height, color);
    }

    private static void DrawHandles(AudioWaveformEditor editor, ref DrawingContext context, float width)
    {
        var color = editor.HandleColor;
        var part = editor.HighlightedPart;
        float height = editor.Bounds.Height;

        // The trim edges.
        float start = editor.TimeToX(editor.AudibleStart), end = editor.TimeToX(editor.AudibleEnd);
        DrawEdge(ref context, start, height, color, part == AudioEditorPart.TrimStart, 1);
        DrawEdge(ref context, end, height, color, part == AudioEditorPart.TrimEnd, -1);

        // The fade handles.
        foreach (var fade in (ReadOnlySpan<AudioEditorPart>)[AudioEditorPart.FadeIn, AudioEditorPart.FadeOut])
        {
            var bounds = editor.GetFadeHandleBounds(fade);
            if (bounds.Right < 0 || bounds.X > width) continue;
            float grow = part == fade ? 1.5f : 0;
            context.DrawRect(new Rect(bounds.X - grow, bounds.Y - grow, bounds.Width + 2 * grow, bounds.Height + 2 * grow), color.WithAlpha(part == fade ? 1f : 0.8f));
        }

        // The gain chip, where there's room for it.
        if (end - start > 60)
        {
            var chip = editor.GetGainChipBounds();
            var background = color.WithAlpha(part == AudioEditorPart.Gain ? 0.95f : 0.7f);
            context.DrawRoundedRect(chip, new CornerRadius(chip.Height / 2), background);
            string text = editor.GainText;
            float fontSize = editor.FontSize;
            var size = context.MeasureText(text, fontSize);
            context.DrawText(text, new Point(chip.X + (chip.Width - size.Width) / 2, chip.Y + chip.Height / 2 + fontSize * 0.36f),
                TimelineDrawing.TextOn(color), fontSize, editor.FontFamily, FontWeight.Medium);
        }
    }

    // An edge of the audible region: a line with a short tab into the region at the top and bottom, like a bracket.
    private static void DrawEdge(ref DrawingContext context, float x, float height, Color color, bool highlighted, int direction)
    {
        float thickness = highlighted ? 3f : 2f;
        var edge = color.WithAlpha(highlighted ? 1f : 0.7f);
        float left = direction > 0 ? x : x - thickness;
        context.DrawRect(new Rect(left, 0, thickness, height), edge);
        float tab = 5;
        float tabLeft = direction > 0 ? x : x - tab;
        context.DrawRect(new Rect(tabLeft, 0, tab, 2), edge);
        context.DrawRect(new Rect(tabLeft, height - 2, tab, 2), edge);
    }
}
