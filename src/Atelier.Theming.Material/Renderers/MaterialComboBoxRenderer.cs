using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a <see cref="ComboBox"/> as a Material Design 3 outlined select: a 1 px outline (on-surface when hovered, 2 px
/// primary when focused or open), the selected item's text (or the placeholder in on-surface-variant), and the
/// drop-down arrow, which points up while open.
/// </summary>
public class MaterialComboBoxRenderer(MaterialColorScheme colors) : ControlRenderer<ComboBox>
{
    // Space kept free for the arrow on the right; matches the ComboBox layout.
    private const float ArrowAreaWidth = 28f;

    /// <inheritdoc/>
    public override void Render(ComboBox comboBox, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, comboBox.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        bool enabled = comboBox.IsEnabled;
        bool active = comboBox.IsFocused || comboBox.IsDropDownOpen;
        var corner = comboBox.CornerRadius;

        if (MaterialDrawing.IsSet(comboBox, Control.BackgroundProperty))
        {
            context.DrawRoundedRect(bounds, corner, comboBox.Background);
        }

        Color outline = !enabled ? MaterialDrawing.DisabledContainer(colors)
            : active ? colors.Primary
            : comboBox.IsHovered ? colors.OnSurface
            : colors.Outline;
        context.DrawRoundedRectOutline(bounds, corner, outline, enabled && active ? 2f : 1f);

        // Text of the selection (unless a template element shows it), clipped before the arrow.
        if (comboBox.SelectionDisplayElement == null)
        {
            var padding = comboBox.Padding;
            var textPos = new Point(padding.Left, (bounds.Height + comboBox.FontSize) * 0.5f - 2f);
            var clipRect = new Rect(padding.Left, 0, Math.Max(0, bounds.Width - padding.Left - ArrowAreaWidth), bounds.Height);

            // Cached by the ComboBox when the selection changes, so rendering doesn't call ToString() per frame.
            string? text = comboBox.SelectedItem != null ? comboBox.SelectionBoxText : comboBox.Placeholder;
            if (!string.IsNullOrEmpty(text))
            {
                Color textColor = comboBox.SelectedItem != null ? colors.OnSurface : colors.OnSurfaceVariant;
                if (!enabled) textColor = MaterialDrawing.DisabledContent(colors);

                using var clip = context.PushClip(clipRect);
                context.DrawText(text, textPos, textColor, comboBox.FontSize, comboBox.FontFamily);
            }
        }

        // MD3 drop-down arrow: a 10×5 triangle in the 24 px trailing icon area, flipped while open.
        float cx = bounds.Right - 18f;
        float cy = bounds.Height * 0.5f;
        Color arrow = !enabled ? MaterialDrawing.DisabledContent(colors) : active ? colors.Primary : colors.OnSurfaceVariant;
        MaterialDrawing.FillDropDownArrow(ref context, cx, cy, comboBox.IsDropDownOpen, arrow);
    }
}
