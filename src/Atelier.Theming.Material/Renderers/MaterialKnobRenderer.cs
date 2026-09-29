using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a <see cref="Knob"/> in the style of the Material slider: a round-ended track arc (primary up to the value,
/// surface-container-highest after it) around a surface-container-high dial with an indicator line pointing at the
/// value; knobs 56 px and larger show the value text in the dial.
/// </summary>
/// <remarks>
/// Hover and press show the on-surface state layer over the dial, and the indicator turns primary while dragging;
/// keyboard focus shows the focus ring. A disabled knob is drawn at 38% opacity.
/// </remarks>
/// <param name="colors">The color scheme.</param>
public class MaterialKnobRenderer(MaterialColorScheme colors) : ControlRenderer<Knob>
{
    private const float MinValueTextSize = 56f;

    /// <inheritdoc/>
    public override void Render(Knob knob, ref DrawingContext context)
    {
        float size = Math.Min(knob.Bounds.Width, knob.Bounds.Height);
        if (size <= 0) return;

        int layer = knob.IsEnabled ? -1 : context.SaveOpacityLayer(MaterialState.DisabledContentOpacity);

        var center = new Point(knob.Bounds.Width * 0.5f, knob.Bounds.Height * 0.5f);
        float track = Math.Max(3f, size * 0.1f);
        float arcRadius = size * 0.5f - track * 0.5f;
        context.DrawArc(center, arcRadius, Knob.StartAngle, Knob.SweepAngle, colors.OutlineVariant, track);
        float sweep = knob.NormalizedValue * Knob.SweepAngle;
        if (sweep > 0.5f)
        {
            context.DrawArc(center, arcRadius, Knob.StartAngle, sweep, colors.Primary, track);
        }

        float dial = arcRadius - track * 0.5f - Math.Max(2f, size * 0.05f);
        context.DrawCircle(center, dial, colors.SurfaceContainerHigh);
        context.DrawCircleOutline(center, dial, colors.OutlineVariant, 1f);
        MaterialDrawing.DrawStateLayerCircle(ref context, center, colors.OnSurface, MaterialDrawing.StateLayerOpacity(knob), dial);

        float angle = knob.Angle * MathF.PI / 180f;
        var direction = new Point(MathF.Cos(angle), MathF.Sin(angle));
        float inner = size >= MinValueTextSize ? dial * 0.6f : dial * 0.25f;
        float outer = dial - Math.Max(2f, size * 0.06f);
        context.DrawLine(
            new Point(center.X + direction.X * inner, center.Y + direction.Y * inner),
            new Point(center.X + direction.X * outer, center.Y + direction.Y * outer),
            knob.IsDragging ? colors.Primary : colors.OnSurface,
            Math.Max(2f, size * 0.06f));

        if (size >= MinValueTextSize)
        {
            float fontSize = MaterialTypescale.LabelMedium.Size;
            string text = knob.ValueText;
            var textSize = context.MeasureText(text, fontSize, null, FontWeight.Medium);
            context.DrawText(text, new Point(center.X - textSize.Width * 0.5f, center.Y + fontSize * 0.36f), colors.OnSurface, fontSize, null, FontWeight.Medium);
        }

        if (knob.IsFocusVisible)
        {
            MaterialDrawing.DrawFocusRingCircle(ref context, center, size * 0.5f, colors.Secondary);
        }

        if (layer >= 0) context.Canvas.RestoreToCount(layer);
    }
}
