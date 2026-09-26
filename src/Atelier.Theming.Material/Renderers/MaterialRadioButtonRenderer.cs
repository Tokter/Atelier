using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a Material Design 3 <see cref="RadioButton"/>: a 20 px ring (2 px, on-surface-variant) that turns primary with
/// a 10 px dot when selected.
/// </summary>
/// <remarks>
/// Hover and press show the circular state layer (40 px, smaller in dense sizing); keyboard focus shows the focus ring
/// hugging the circle.
/// Disabled: on-surface at 38%.
/// </remarks>
/// <param name="colors">The color scheme.</param>
/// <param name="sizing">The theme sizing (the state layer shrinks with the density); <c>null</c> for desktop sizing.</param>
public class MaterialRadioButtonRenderer(MaterialColorScheme colors, MaterialSizing? sizing = null) : ControlRenderer<RadioButton>
{
    private const float RingWidth = 2f;
    private const float DotRadius = 5f;
    private readonly float _stateLayerRadius = (sizing ?? MaterialSizing.Desktop).StateLayerSize * 0.5f;

    /// <inheritdoc/>
    public override void Render(RadioButton radioButton, ref DrawingContext context)
    {
        var indicator = radioButton.GetIndicatorBounds();
        var center = new Point(indicator.X + indicator.Width * 0.5f, indicator.Y + indicator.Height * 0.5f);
        float radius = indicator.Width * 0.5f;
        bool enabled = radioButton.IsEnabled;
        bool selected = radioButton.IsChecked == true;

        MaterialDrawing.DrawStateLayerCircle(ref context, center, selected ? colors.Primary : colors.OnSurface,
            MaterialDrawing.HaloOpacity(radioButton), _stateLayerRadius);

        Color color = !enabled ? MaterialDrawing.DisabledContent(colors)
            : selected ? colors.Primary
            : radioButton.IsHovered || radioButton.IsPressed ? colors.OnSurface
            : colors.OnSurfaceVariant;

        context.DrawCircleOutline(center, radius, color, RingWidth);

        float dot = DotRadius * radioButton.CheckAnimationProgress;
        if (dot > 0.1f)
        {
            context.DrawCircle(center, dot, color);
        }

        if (radioButton.IsFocusVisible)
        {
            MaterialDrawing.DrawFocusRingCircle(ref context, center, radius, colors.Secondary);
        }
    }
}
