using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a standalone <see cref="ToggleButton"/> like an MD3 segmented button: outlined when unchecked, a
/// secondary-container fill when checked, half of that fill when indeterminate, and supplies its content color.
/// <see cref="CheckBox"/>, <see cref="RadioButton"/> and <see cref="Switch"/> have their own renderers.
/// </summary>
public class MaterialToggleButtonRenderer(MaterialColorScheme colors) : ControlRenderer<ToggleButton>, IContentColorProvider
{
    /// <inheritdoc/>
    public override void Render(ToggleButton button, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, button.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var corner = button.CornerRadius;
        float progress = SelectionProgress(button);

        if (!button.IsEnabled)
        {
            if (progress > 0f)
            {
                context.DrawRoundedRect(bounds, corner, MaterialDrawing.DisabledContainer(colors).WithOpacity(progress));
            }
            context.DrawRoundedRectOutline(bounds, corner, MaterialDrawing.DisabledContainer(colors), 1f);
            return;
        }

        if (progress > 0f)
        {
            context.DrawRoundedRect(bounds, corner, colors.SecondaryContainer.WithOpacity(progress));
        }

        MaterialDrawing.DrawStateLayer(ref context, bounds, corner, ContentColor(button), MaterialDrawing.StateLayerOpacity(button));
        context.DrawRoundedRectOutline(bounds, corner, colors.Outline, 1f);

        if (button.IsFocusVisible)
        {
            MaterialDrawing.DrawFocusRing(ref context, bounds, corner, colors.Secondary);
        }
    }

    /// <inheritdoc/>
    public bool TryGetContentColor(UIElement element, out Color color)
    {
        if (element is not ToggleButton button)
        {
            color = default;
            return false;
        }

        color = button.IsEnabled ? ContentColor(button) : colors.OnSurface;
        return true;
    }

    // 0 unchecked, 1 checked, 0.5 indeterminate, animated between them.
    private static float SelectionProgress(ToggleButton button) =>
        button.CheckAnimationProgress * (button.IsChecked == null ? 0.5f : 1f);

    private Color ContentColor(ToggleButton button) =>
        MaterialDrawing.IsSet(button, Control.ForegroundProperty)
            ? button.Foreground
            : Color.Lerp(colors.OnSurface, colors.OnSecondaryContainer, SelectionProgress(button));
}
