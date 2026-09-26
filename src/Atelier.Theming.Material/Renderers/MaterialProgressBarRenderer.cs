using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a Material Design 3 linear <see cref="ProgressBar"/>: a surface-container-highest track filled with primary up
/// to the value, or a moving primary segment when indeterminate.
/// </summary>
public class MaterialProgressBarRenderer(MaterialColorScheme colors) : ControlRenderer<ProgressBar>
{
    /// <inheritdoc/>
    public override void Render(ProgressBar progressBar, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, progressBar.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var corner = progressBar.CornerRadius;
        bool enabled = progressBar.IsEnabled;
        Color indicator = enabled ? colors.Primary : colors.OnSurface.WithOpacity(0.38f);
        context.DrawRoundedRect(bounds, corner, enabled ? colors.SurfaceContainerHighest : MaterialDrawing.DisabledContainer(colors));

        if (progressBar.IsIndeterminate)
        {
            float barWidth = bounds.Width * 0.35f;
            float x = (bounds.Width + barWidth) * progressBar.IndeterminateOffset - barWidth;
            float left = Math.Max(0, x);
            float right = Math.Min(bounds.Width, x + barWidth);
            if (right > left)
            {
                context.DrawRoundedRect(new Rect(left, 0, right - left, bounds.Height), corner, indicator);
            }
        }
        else
        {
            float width = bounds.Width * progressBar.NormalizedValue;
            if (width > 0)
            {
                context.DrawRoundedRect(new Rect(0, 0, width, bounds.Height), corner, indicator);
            }
        }
    }
}
