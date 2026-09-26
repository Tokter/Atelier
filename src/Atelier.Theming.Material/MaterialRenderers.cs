using System;
using System.Numerics;
using SkiaSharp;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

public class MaterialButtonRenderer(MaterialColorScheme colors) : ControlRenderer<Button>
{
    public override void Render(Button button, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, button.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // Resolve colors based on variant
        Color bgColor = button.Variant switch
        {
            ButtonVariant.Filled => colors.Primary,
            ButtonVariant.Elevated => colors.SurfaceContainerLow,
            ButtonVariant.Tonal => colors.SecondaryContainer,
            ButtonVariant.Outlined => Color.Transparent,
            ButtonVariant.Text => Color.Transparent,
            _ => colors.Primary
        };

        Color fgColor = button.Variant switch
        {
            ButtonVariant.Filled => colors.OnPrimary,
            ButtonVariant.Elevated => colors.Primary,
            ButtonVariant.Tonal => colors.OnSecondaryContainer,
            ButtonVariant.Outlined => colors.Primary,
            ButtonVariant.Text => colors.Primary,
            _ => colors.OnPrimary
        };

        // State overlay
        if (!button.IsEnabled)
        {
            bgColor = colors.OnSurface.WithAlpha(0.12f);
            fgColor = colors.OnSurface.WithAlpha(0.38f);
        }
        else if (button is TitleBarButton tb)
        {
            if (tb.IsCloseButton)
            {
                if (button.IsPressed)
                {
                    bgColor = Color.FromRgb(241, 112, 122);
                    fgColor = Color.White;
                }
                else if (button.IsHovered)
                {
                    bgColor = Color.FromRgb(232, 17, 35); // Native close red
                    fgColor = Color.White;
                }
                else
                {
                    bgColor = Color.Transparent;
                    fgColor = colors.OnSurface;
                }
            }
            else
            {
                if (button.IsPressed)
                {
                    bgColor = colors.OnSurface.WithAlpha(0.14f);
                }
                else if (button.IsHovered)
                {
                    bgColor = colors.OnSurface.WithAlpha(0.08f);
                }
                else
                {
                    bgColor = Color.Transparent;
                }
                fgColor = colors.OnSurface;
            }

            if (button.Content is TextBlock tbText)
            {
                tbText.Foreground = fgColor;
            }
            else if (button.Content is Icon ic)
            {
                ic.Foreground = fgColor;
            }
        }
        else if (button.IsPressed)
        {
            bgColor = Color.Lerp(bgColor, fgColor, 0.12f);
        }
        else if (button.IsHovered)
        {
            bgColor = Color.Lerp(bgColor, fgColor, 0.08f);
        }

        // Draw elevation shadow for Elevated/Filled
        if (button.Elevation > 0 && button.IsEnabled && button.Variant is ButtonVariant.Filled or ButtonVariant.Elevated)
        {
            float elev = button.Variant == ButtonVariant.Elevated
                ? (button.IsPressed ? button.Elevation + 2 : (button.IsHovered ? button.Elevation + 1 : button.Elevation))
                : (button.IsHovered ? button.Elevation : (button.IsPressed ? button.Elevation + 1 : (button.Elevation > 1f ? button.Elevation : 0f)));

            if (elev > 0)
            {
                context.DrawShadow(bounds, button.CornerRadius, elev, Color.Black);
            }
        }

        // Draw background
        if (bgColor.A > 0)
        {
            context.DrawRoundedRect(bounds, button.CornerRadius, bgColor);
        }

        // Draw outline if Outlined variant
        if (button.Variant == ButtonVariant.Outlined)
        {
            Color outlineColor = button.IsEnabled ? colors.Outline : colors.OnSurface.WithAlpha(0.12f);
            context.DrawRoundedRectOutline(bounds, button.CornerRadius, outlineColor, 1.0f);
        }

        // Draw Material 3 Ink Ripple Effect
        if (button.HasActiveRipple)
        {
            using var clip = context.PushRoundedClip(bounds, button.CornerRadius);
            float maxDim = MathF.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height);
            float radius = maxDim * button.RippleProgress;
            Color rippleColor = fgColor.WithAlpha(button.RippleOpacity);
            context.DrawCircle(button.RippleCenter, radius, rippleColor);
        }

        // Draw focus ring
        if (button.IsFocused)
        {
            var focusBounds = new Rect(bounds.X - 2, bounds.Y - 2, bounds.Width + 4, bounds.Height + 4);
            var focusCorner = new CornerRadius(button.CornerRadius.TopLeft + 2);
            context.DrawRoundedRectOutline(focusBounds, focusCorner, colors.Primary, 2.0f);
        }
    }
}

/// <summary>
/// Draws a standalone <see cref="ToggleButton"/>: outlined when unchecked, a secondary-container fill when checked, and
/// half of that fill when indeterminate. <see cref="CheckBox"/>, <see cref="RadioButton"/> and <see cref="Switch"/> have
/// their own renderers.
/// </summary>
public class MaterialToggleButtonRenderer(MaterialColorScheme colors) : ControlRenderer<ToggleButton>
{
    /// <inheritdoc/>
    public override void Render(ToggleButton button, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, button.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var corner = button.CornerRadius;
        float progress = button.CheckAnimationProgress * (button.IsChecked == null ? 0.5f : 1f);

        if (!button.IsEnabled)
        {
            context.DrawRoundedRect(bounds, corner, colors.OnSurface.WithAlpha(0.12f * Math.Max(progress, 0.5f)));
            return;
        }

        Color fg = Color.Lerp(colors.Primary, colors.OnSecondaryContainer, progress);
        Color bg = colors.SecondaryContainer.WithAlpha(progress);
        if (button.IsPressed)
        {
            bg = Color.Lerp(bg, fg.WithAlpha(0.12f), 0.5f);
        }
        else if (button.IsHovered)
        {
            bg = Color.Lerp(bg, fg.WithAlpha(0.08f), 0.5f);
        }

        if (bg.A > 0)
        {
            context.DrawRoundedRect(bounds, corner, bg);
        }

        if (progress < 1f)
        {
            context.DrawRoundedRectOutline(bounds, corner, colors.Outline.WithAlpha(1f - progress), 1f);
        }

        if (button.IsFocused)
        {
            var focusBounds = new Rect(bounds.X - 2, bounds.Y - 2, bounds.Width + 4, bounds.Height + 4);
            context.DrawRoundedRectOutline(focusBounds, new CornerRadius(corner.TopLeft + 2), colors.Primary, 2.0f);
        }
    }
}

internal static class MaterialRendererHelpers
{
    public static void ApplyDisabledState(UIElement element, Color disabledForeground)
    {
        if (element is TextBlock tb)
        {
            tb.Foreground = disabledForeground;
        }
        else if (element is Icon ic)
        {
            ic.Foreground = disabledForeground;
        }
        else
        {
            foreach (var child in element.Children)
            {
                if (child is UIElement ui)
                {
                    ApplyDisabledState(ui, disabledForeground);
                }
            }
        }
    }
}

public class MaterialCheckBoxRenderer(MaterialColorScheme colors) : ControlRenderer<CheckBox>
{
    public override void Render(CheckBox checkBox, ref DrawingContext context)
    {
        var boxRect = checkBox.GetIndicatorBounds();
        var corner = new CornerRadius(2);

        float progress = checkBox.CheckAnimationProgress;
        bool isEnabled = checkBox.IsEnabled;
        bool isIndeterminate = checkBox.IsChecked == null;

        Color fillColor;
        Color markColor;
        if (!isEnabled)
        {
            fillColor = colors.OnSurface.WithAlpha(0.38f);
            markColor = colors.Surface;
        }
        else
        {
            // Checked / transitioning state: fill primary
            fillColor = Color.Lerp(colors.Outline, colors.Primary, progress);
            markColor = colors.OnPrimary;
        }

        if (progress <= 0.01f)
        {
            // Unchecked state: outline only
            Color outlineColor = !isEnabled ? fillColor : checkBox.IsHovered ? colors.OnSurface : colors.Outline;
            context.DrawRoundedRectOutline(boxRect, corner, outlineColor, 2f);
        }
        else
        {
            context.DrawRoundedRect(boxRect, corner, fillColor);

            if (isIndeterminate)
            {
                // Indeterminate: a horizontal dash growing from the center
                float half = 5f * progress;
                float cx = boxRect.X + boxRect.Width * 0.5f;
                float cy = boxRect.Y + boxRect.Height * 0.5f;
                context.DrawLine(new Point(cx - half, cy), new Point(cx + half, cy), markColor, 2f);
            }
            else if (progress > 0.1f)
            {
                // Vector check mark, its second stroke drawn with the progress
                using var builder = new SKPathBuilder();
                float x1 = boxRect.X + 3.5f;
                float y1 = boxRect.Y + 9f;
                float x2 = boxRect.X + 7.5f;
                float y2 = boxRect.Y + 13f;
                float x3 = boxRect.X + 14.5f;
                float y3 = boxRect.Y + 5.5f;

                builder.MoveTo(x1, y1);
                builder.LineTo(x2, y2);
                float endX = x2 + (x3 - x2) * progress;
                float endY = y2 + (y3 - y2) * progress;
                builder.LineTo(endX, endY);

                using var path = builder.Detach();
                context.DrawPathOutline(path, markColor, 2.0f);
            }
        }

        // Draw focus ring
        if (checkBox.IsFocused && isEnabled)
        {
            var focusRect = new Rect(boxRect.X - 3, boxRect.Y - 3, boxRect.Width + 6, boxRect.Height + 6);
            context.DrawRoundedRectOutline(focusRect, new CornerRadius(4), colors.Primary, 2f);
        }
    }
}

public class MaterialRadioButtonRenderer(MaterialColorScheme colors) : ControlRenderer<RadioButton>
{
    public override void Render(RadioButton radioButton, ref DrawingContext context)
    {
        var indicator = radioButton.GetIndicatorBounds();
        var center = new Point(indicator.X + indicator.Width * 0.5f, indicator.Y + indicator.Height * 0.5f);
        float radius = indicator.Width * 0.5f - 1f;
        bool isEnabled = radioButton.IsEnabled;
        bool isChecked = radioButton.IsChecked == true;

        if (!isEnabled)
        {
            Color disabledColor = colors.OnSurface.WithAlpha(0.38f);
            if (isChecked)
            {
                context.DrawCircleOutline(center, radius, disabledColor, 2f);
                context.DrawCircle(center, 5f, disabledColor);
            }
            else
            {
                context.DrawCircleOutline(center, radius, disabledColor, 2f);
            }
            return;
        }

        if (isChecked)
        {
            context.DrawCircleOutline(center, radius, colors.Primary, 2f);
            context.DrawCircle(center, 5f, colors.Primary);
        }
        else
        {
            Color outlineColor = radioButton.IsHovered ? colors.OnSurface : colors.Outline;
            context.DrawCircleOutline(center, radius, outlineColor, 2f);
        }

        if (radioButton.IsFocused)
        {
            context.DrawCircleOutline(center, radius + 3f, colors.Primary, 2f);
        }
    }
}

public class MaterialSwitchRenderer(MaterialColorScheme colors) : ControlRenderer<Switch>
{
    public override void Render(Switch switchControl, ref DrawingContext context)
    {
        var pad = switchControl.Padding;
        float trackW = Switch.TrackWidth;
        float trackH = Switch.TrackHeight;
        float trackX = pad.Left;
        float y = pad.Top + (switchControl.Bounds.Height - pad.Vertical - trackH) * 0.5f;
        var trackRect = new Rect(trackX, y, trackW, trackH);
        var trackCorner = new CornerRadius(11f);

        float progress = switchControl.ThumbAnimationProgress;
        bool isEnabled = switchControl.IsEnabled;

        // 1. Draw Track
        if (!isEnabled)
        {
            if (progress > 0.5f)
            {
                context.DrawRoundedRect(trackRect, trackCorner, colors.OnSurface.WithAlpha(0.12f));
            }
            else
            {
                context.DrawRoundedRectOutline(trackRect, trackCorner, colors.OnSurface.WithAlpha(0.12f), 2f);
            }
        }
        else
        {
            Color trackFill = Color.Lerp(colors.SurfaceContainerHighest, colors.Primary, progress);
            context.DrawRoundedRect(trackRect, trackCorner, trackFill);

            if (progress < 0.99f)
            {
                float outlineAlpha = 1f - progress;
                Color outlineColor = switchControl.IsHovered ? colors.OnSurface : colors.Outline;
                context.DrawRoundedRectOutline(trackRect, trackCorner, outlineColor.WithAlpha(outlineAlpha), 2f);
            }
        }

        // 2. Thumb Geometry & Position
        float minRadius = switchControl.ShowThumbIcon ? 8f : 6f;
        float maxRadius = 8.5f;
        float thumbRadius = minRadius + (maxRadius - minRadius) * progress;

        float startX = trackX + 11f;
        float endX = trackX + 29f;
        float thumbCenterX = startX + (endX - startX) * progress;
        float thumbCenterY = y + trackH * 0.5f;
        var thumbCenter = new Point(thumbCenterX, thumbCenterY);

        // 3. Thumb Colors
        Color thumbColor;
        if (!isEnabled)
        {
            thumbColor = progress > 0.5f
                ? colors.Surface
                : colors.OnSurface.WithAlpha(0.38f);
        }
        else
        {
            Color uncheckedThumb = switchControl.IsHovered ? colors.OnSurface : colors.Outline;
            thumbColor = Color.Lerp(uncheckedThumb, colors.OnPrimary, progress);
        }

        // 4. Draw Thumb Shadow (when checked and enabled)
        if (isEnabled && progress > 0.1f)
        {
            var thumbRect = new Rect(thumbCenterX - thumbRadius, thumbCenterY - thumbRadius, thumbRadius * 2, thumbRadius * 2);
            context.DrawShadow(thumbRect, new CornerRadius(thumbRadius), 2f * progress, colors.OnSurface);
        }

        // 5. Draw Thumb Circle
        context.DrawCircle(thumbCenter, thumbRadius, thumbColor);

        // 6. Draw Thumb Icon (MD3 optional checkmark inside thumb)
        if (switchControl.ShowThumbIcon)
        {
            if (progress > 0.5f)
            {
                using var builder = new SKPathBuilder();
                float cx = thumbCenter.X;
                float cy = thumbCenter.Y;
                builder.MoveTo(cx - 3.5f, cy);
                builder.LineTo(cx - 1f, cy + 2.5f);
                builder.LineTo(cx + 3.5f, cy - 2f);
                using var path = builder.Detach();
                Color iconColor = isEnabled ? colors.Primary : colors.OnSurface.WithAlpha(0.38f);
                context.DrawPathOutline(path, iconColor, 1.5f);
            }
            else
            {
                using var builder = new SKPathBuilder();
                float cx = thumbCenter.X;
                float cy = thumbCenter.Y;
                builder.MoveTo(cx - 2.5f, cy);
                builder.LineTo(cx + 2.5f, cy);
                using var path = builder.Detach();
                Color iconColor = isEnabled ? (switchControl.IsHovered ? colors.OnSurface : colors.Outline) : colors.OnSurface.WithAlpha(0.38f);
                context.DrawPathOutline(path, iconColor, 1.5f);
            }
        }

        // 7. Focus Ring
        if (switchControl.IsFocused && isEnabled)
        {
            var focusRect = new Rect(trackRect.X - 2.5f, trackRect.Y - 2.5f, trackRect.Width + 5f, trackRect.Height + 5f);
            context.DrawRoundedRectOutline(focusRect, new CornerRadius(13.5f), colors.Primary, 2f);
        }
    }
}

/// <summary>
/// Draws a Material Design 3 <see cref="TextBox"/>: container, leading icon, floating label, text with selection and
/// caret (using the text box's cached character offsets), and supporting text.
/// </summary>
public class MaterialTextBoxRenderer(MaterialColorScheme colors) : ControlRenderer<TextBox>
{
    /// <inheritdoc/>
    public override void Render(TextBox textBox, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, textBox.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        bool isEnabled = textBox.IsEnabled;
        bool isFocused = textBox.IsFocused;
        bool hasError = textBox.HasValidationError;
        float progress = textBox.LabelAnimationProgress;

        float supportingTextHeight = textBox.HasSupportingText ? 20f : 0f;
        var containerRect = new Rect(bounds.Left, bounds.Top, bounds.Width, Math.Max(36f, bounds.Height - supportingTextHeight));

        // 1. Container Background & Border/Underline
        if (textBox.Variant == TextBoxVariant.Filled)
        {
            // Filled style: colored background + rounded top corners + bottom underline
            Color containerBg = isEnabled
                ? colors.SurfaceContainerHighest
                : colors.OnSurface.WithAlpha(0.04f);

            var topCorners = new CornerRadius(textBox.CornerRadius.TopLeft, textBox.CornerRadius.TopRight, 0, 0);
            context.DrawRoundedRect(containerRect, topCorners, containerBg);

            // Bottom underline (active indicator line)
            float lineY = containerRect.Bottom - 1f;
            if (isFocused && isEnabled)
            {
                // Brightens in color when focused (2dp Primary, or Error while invalid)
                context.DrawLine(
                    new Point(containerRect.Left, lineY),
                    new Point(containerRect.Right, lineY),
                    hasError ? colors.Error : colors.Primary,
                    2f
                );
            }
            else if (hasError && isEnabled)
            {
                context.DrawLine(
                    new Point(containerRect.Left, lineY),
                    new Point(containerRect.Right, lineY),
                    colors.Error,
                    1f
                );
            }
            else
            {
                Color underlineColor = !isEnabled
                    ? colors.OnSurface.WithAlpha(0.12f)
                    : (textBox.IsHovered ? colors.OnSurface : colors.Outline);

                context.DrawLine(
                    new Point(containerRect.Left, lineY),
                    new Point(containerRect.Right, lineY),
                    underlineColor,
                    1f
                );
            }
        }
        else // Outlined style
        {
            // Outlined does not change background, has box with rounded corners around whole field
            Color outlineColor;
            float strokeWidth = 1f;

            if (!isEnabled)
            {
                outlineColor = colors.OnSurface.WithAlpha(0.12f);
            }
            else if (hasError)
            {
                outlineColor = colors.Error;
                strokeWidth = isFocused ? 2f : 1f;
            }
            else if (isFocused)
            {
                outlineColor = colors.Primary;
                strokeWidth = 2f;
            }
            else if (textBox.IsHovered)
            {
                outlineColor = colors.OnSurface;
            }
            else
            {
                outlineColor = colors.Outline;
            }

            context.DrawRoundedRectOutline(containerRect, textBox.CornerRadius, outlineColor, strokeWidth);
        }

        // 2. Leading Icon (Optional)
        float textStartX = textBox.GetTextContentStartX();
        if (textBox.HasLeadingIcon)
        {
            float iconSize = 20f;
            float iconLeft = containerRect.Left + 12f;
            float iconTop = containerRect.Top + (containerRect.Height - iconSize) * 0.5f;

            Color iconColor = !isEnabled
                ? colors.OnSurface.WithAlpha(0.38f)
                : (isFocused ? colors.Primary : colors.OnSurfaceVariant);

            string glyph = MaterialIconFontManager.GetGlyph(textBox.LeadingIconKind);
            var tf = MaterialIconFontManager.GetTypeface(0, 400, 0, iconSize);
            var font = context.PaintRegistry.GetFont(iconSize, tf);
            var paint = context.PaintRegistry.GetFillPaint(iconColor);
            font.GetFontMetrics(out var metrics);
            float glyphWidth = font.MeasureText(glyph.AsSpan());
            float ix = iconLeft + (iconSize - glyphWidth) * 0.5f;
            float iy = iconTop + (iconSize - (metrics.Ascent + metrics.Descent)) * 0.5f;
            context.Canvas.DrawText(glyph, ix, iy, SKTextAlign.Left, font, paint);
        }

        // 3. Label Text (with smooth animation between resting and floating)
        if (textBox.HasLabel)
        {
            float floatingFontSize = 11f;
            float restingFontSize = textBox.FontSize;
            float currentFontSize = restingFontSize + (floatingFontSize - restingFontSize) * progress;

            // Resting positions
            float restingX = textStartX;
            float restingY = containerRect.Top + (containerRect.Height + restingFontSize) * 0.5f - 2f;

            // Floating positions
            float floatingX = textBox.Variant == TextBoxVariant.Filled ? textStartX : containerRect.Left + 12f;
            float floatingY = textBox.Variant == TextBoxVariant.Filled
                ? containerRect.Top + 8f + floatingFontSize
                : containerRect.Top + floatingFontSize * 0.5f;

            float currentX = restingX + (floatingX - restingX) * progress;
            float currentY = restingY + (floatingY - restingY) * progress;

            Color labelColor;
            if (!isEnabled)
            {
                labelColor = colors.OnSurface.WithAlpha(0.38f);
            }
            else if (hasError)
            {
                labelColor = colors.Error;
            }
            else
            {
                Color targetColor = isFocused ? colors.Primary : colors.OnSurfaceVariant;
                labelColor = Color.Lerp(colors.OnSurfaceVariant, targetColor, progress);
            }

            // Cutout notch for Outlined when floating
            if (textBox.Variant == TextBoxVariant.Outlined && progress > 0.05f)
            {
                var labelMeasure = context.MeasureText(textBox.Label, currentFontSize, textBox.FontFamily);
                var notchRect = new Rect(currentX - 4f, containerRect.Top - 3f, (labelMeasure.Width + 8f) * progress, 6f);
                context.DrawRect(notchRect, colors.Surface);
            }

            context.DrawText(textBox.Label, new Point(currentX, currentY), labelColor, currentFontSize, textBox.FontFamily);
        }

        // 4. Input Text & Placeholder
        float textY;
        if (textBox.Variant == TextBoxVariant.Filled && textBox.HasLabel)
        {
            // Position text in lower portion of filled container when label is present
            textY = containerRect.Top + containerRect.Height - 14f;
        }
        else
        {
            // Center vertically in container
            textY = containerRect.Top + (containerRect.Height + textBox.FontSize) * 0.5f - 2f;
        }

        // Offsets come from the text box's per-text cache, so nothing is measured or allocated per frame.
        float originX = textBox.GetTextOriginX();
        bool hasText = textBox.Text.Length > 0;

        float viewportWidth = textBox.GetViewportWidth();
        var textClipRect = new Rect(textStartX, containerRect.Top + 1f, viewportWidth, Math.Max(0f, containerRect.Height - 2f));

        using (context.PushClip(textClipRect))
        {
            // Selection highlight
            if (isFocused && textBox.HasSelection && hasText)
            {
                int selStart = textBox.SelectionStart;
                float selStartX = originX + textBox.GetCharacterOffset(selStart);
                float selWidth = textBox.GetCharacterOffset(selStart + textBox.SelectionLength) - textBox.GetCharacterOffset(selStart);

                float selTop = textY - textBox.FontSize - 1f;
                float selHeight = textBox.FontSize + 4f;

                context.DrawRect(new Rect(selStartX, selTop, selWidth, selHeight), colors.Primary.WithAlpha(0.35f));
            }

            if (hasText)
            {
                // An unset foreground means the theme color; anything set (black included) is honored.
                Color baseColor = MaterialTextBlockRenderer.HasExplicitForeground(textBox) ? textBox.Foreground : colors.OnSurface;
                Color textColor = isEnabled ? baseColor : baseColor.WithAlpha(baseColor.Af * 0.38f);
                context.DrawText(textBox.DisplayText, new Point(originX, textY), textColor, textBox.FontSize, textBox.FontFamily);
            }
            else if (!string.IsNullOrEmpty(textBox.Placeholder) && (!textBox.HasLabel || progress > 0.8f))
            {
                float placeholderX = originX;
                if (textBox.TextAlignment != TextAlignment.Left)
                {
                    float free = viewportWidth - context.MeasureText(textBox.Placeholder, textBox.FontSize, textBox.FontFamily).Width;
                    placeholderX = textStartX + Math.Max(0f, textBox.TextAlignment == TextAlignment.Center ? free * 0.5f : free);
                }

                Color placeholderColor = isEnabled ? colors.OnSurfaceVariant.WithAlpha(0.6f) : colors.OnSurface.WithAlpha(0.38f);
                context.DrawText(textBox.Placeholder, new Point(placeholderX, textY), placeholderColor, textBox.FontSize, textBox.FontFamily);
            }

            // Caret
            if (isFocused && isEnabled && textBox.CaretVisible && !textBox.HasSelection)
            {
                float caretX = originX + textBox.GetCharacterOffset(textBox.CaretIndex);

                float caretWidth = Math.Max(1f, MathF.Round(textBox.CaretWidth));
                float caretTop = textY - textBox.FontSize;
                float caretHeight = textBox.FontSize + 2f;

                context.DrawPixelRect(new Rect(caretX, caretTop, caretWidth, caretHeight), colors.Primary);
            }
        }

        // 5. Supporting Text (Optional): the first validation error replaces it, in the error color (MD3 error state)
        if (textBox.HasSupportingText)
        {
            Color supportColor = !isEnabled
                ? colors.OnSurface.WithAlpha(0.38f)
                : (textBox.HasValidationError ? colors.Error : colors.OnSurfaceVariant);
            float supportY = containerRect.Bottom + 15f;
            context.DrawText(textBox.DisplayedSupportingText, new Point(containerRect.Left + 16f, supportY), supportColor, 12f, textBox.FontFamily);
        }
    }
}

public class MaterialSliderRenderer(MaterialColorScheme colors) : ControlRenderer<Slider>
{
    public override void Render(Slider slider, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, slider.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        float trackHeight = 6f;
        float handleRadius = 10f;

        // Track is always vertically centered in the Slider's bounds
        float trackCenterY = bounds.Height * 0.5f;
        float trackY = trackCenterY - trackHeight * 0.5f;

        float progress = slider.NormalizedValue;
        float handleX = handleRadius + progress * (bounds.Width - handleRadius * 2);

        // Inactive track
        var inactiveRect = new Rect(bounds.Left + handleRadius, trackY, bounds.Width - handleRadius * 2, trackHeight);
        context.DrawRoundedRect(inactiveRect, new CornerRadius(trackHeight * 0.5f), colors.SurfaceContainerHighest);

        // Active track
        var activeRect = new Rect(bounds.Left + handleRadius, trackY, handleX - (bounds.Left + handleRadius), trackHeight);
        if (activeRect.Width > 0)
        {
            context.DrawRoundedRect(activeRect, new CornerRadius(trackHeight * 0.5f), colors.Primary);
        }

        // Tick marks of a discrete slider (MD3), skipped when they would be closer than 4 px
        float range = slider.Maximum - slider.Minimum;
        float tick = slider.TickFrequency;
        float usable = bounds.Width - handleRadius * 2;
        if (slider.IsSnapToTickEnabled && tick > 0 && range > 0 && usable > 0 && usable * tick / range >= 4f)
        {
            int count = (int)MathF.Floor(range / tick + 1e-4f);
            for (int i = 0; i <= count; i++)
            {
                float ratio = i * tick / range;
                float x = handleRadius + ratio * usable;
                Color dot = ratio <= progress ? colors.OnPrimary : colors.OnSurfaceVariant;
                context.DrawCircle(new Point(x, trackCenterY), 1f, dot);
            }
        }

        // Handle
        var handleCenter = new Point(handleX, trackCenterY);
        context.DrawShadow(new Rect(handleCenter.X - handleRadius, handleCenter.Y - handleRadius, handleRadius * 2, handleRadius * 2), new CornerRadius(handleRadius), 2f, colors.OnSurface);
        context.DrawCircle(handleCenter, handleRadius, colors.Primary);

        if (slider.IsHovered || slider.IsPressed || slider.IsFocused)
        {
            context.DrawCircle(handleCenter, handleRadius + 6, colors.Primary.WithAlpha(slider.IsFocused ? 0.35f : 0.15f));
        }

        // Floating Value Indicator Bubble (Material Design 3)
        if (slider.ShowValueIndicator && slider.ValueIndicatorOpacity > 0.005f)
        {
            float opacity = slider.ValueIndicatorOpacity;
            string text = slider.ValueText; // cached by the slider; no per-frame formatting
            float fontSize = 11f;

            var textSize = context.MeasureText(text, fontSize, bold: true);
            float bubbleWidth = Math.Max(28f, textSize.Width + 14f);
            float bubbleHeight = 20f;
            float bubbleCorner = bubbleHeight * 0.5f;

            // Center bubble above knob, clamping horizontally within bounds
            float bubbleX = Math.Clamp(handleX - bubbleWidth * 0.5f, bounds.Left, bounds.Right - bubbleWidth);
            float bubbleY = handleCenter.Y - handleRadius - 6f - bubbleHeight;

            var bubbleRect = new Rect(bubbleX, bubbleY, bubbleWidth, bubbleHeight);

            // Subtle drop shadow for floating pill
            context.DrawShadow(bubbleRect, new CornerRadius(bubbleCorner), 2f, Color.Black.WithAlpha(opacity));

            // Bubble container (Primary color matching handle)
            context.DrawRoundedRect(bubbleRect, new CornerRadius(bubbleCorner), colors.Primary.WithAlpha(opacity));

            // Centered text inside bubble
            float textX = bubbleRect.Left + (bubbleRect.Width - textSize.Width) * 0.5f;
            float textY = bubbleRect.Top + (bubbleRect.Height + fontSize * 0.7f) * 0.5f;
            context.DrawText(text, new Point(textX, textY), colors.OnPrimary.WithAlpha(opacity), fontSize, bold: true);
        }
    }
}

public class MaterialProgressBarRenderer(MaterialColorScheme colors) : ControlRenderer<ProgressBar>
{
    public override void Render(ProgressBar progressBar, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, progressBar.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var corner = new CornerRadius(bounds.Height * 0.5f);

        // Background track
        context.DrawRoundedRect(bounds, corner, colors.SurfaceContainerHighest);

        if (progressBar.IsIndeterminate)
        {
            // Indeterminate moving bar
            float barWidth = bounds.Width * 0.35f;
            float x = (bounds.Width + barWidth) * progressBar.IndeterminateOffset - barWidth;
            var indRect = new Rect(Math.Max(0, x), 0, Math.Min(barWidth, bounds.Width - Math.Max(0, x)), bounds.Height);
            context.DrawRoundedRect(indRect, corner, colors.Primary);
        }
        else
        {
            // Determinate progress
            float activeW = bounds.Width * progressBar.NormalizedValue;
            if (activeW > 0)
            {
                context.DrawRoundedRect(new Rect(0, 0, activeW, bounds.Height), corner, colors.Primary);
            }
        }
    }
}

/// <summary>Draws <see cref="TextBlock"/> text using the line layout the text block computed during measure.</summary>
public class MaterialTextBlockRenderer(MaterialColorScheme colors) : ControlRenderer<TextBlock>
{
    /// <summary>
    /// Returns whether <paramref name="element"/>'s foreground was set anywhere (locally, by a style, a binding, an
    /// animation or inherited from an ancestor that set it). An unset foreground (the default) means "use the theme color".
    /// </summary>
    internal static bool HasExplicitForeground(UIElement element) =>
        element.GetValueSource(Control.ForegroundProperty) != Atelier.Core.Properties.ValueSource.Default;

    /// <summary>
    /// Resolves the color <paramref name="textBlock"/> is drawn in: the theme's text color while
    /// <see cref="TextBlock.Foreground"/> is unset, otherwise the foreground (any color, black included); muted text
    /// uses the secondary text color or the foreground at 60% opacity, and disabled text 38% opacity.
    /// </summary>
    public Color GetTextColor(TextBlock textBlock)
    {
        Color textColor;
        bool hasCustomForeground = HasExplicitForeground(textBlock);

        if (textBlock.Muted)
        {
            textColor = hasCustomForeground
                ? textBlock.Foreground.WithAlpha(textBlock.Foreground.Af * 0.6f)
                : colors.OnSurfaceVariant;
        }
        else if (hasCustomForeground)
        {
            textColor = textBlock.Foreground;
        }
        else
        {
            textColor = colors.OnSurface;
        }

        if (!textBlock.IsEnabled)
        {
            textColor = textColor.WithAlpha(textColor.Af * 0.38f);
        }

        return textColor;
    }

    public override void Render(TextBlock textBlock, ref DrawingContext context)
    {
        if (string.IsNullOrEmpty(textBlock.Text)) return;

        Color textColor = GetTextColor(textBlock);

        int clipSave = -1;
        if (textBlock.ClipToBounds && textBlock.Bounds.Width > 0 && textBlock.Bounds.Height > 0)
        {
            clipSave = context.Canvas.Save();
            context.Canvas.ClipRect(new SKRect(0, 0, textBlock.Bounds.Width, textBlock.Bounds.Height), SKClipOperation.Intersect, antialias: true);
        }

        try
        {
            // The lines were laid out during measure/arrange and are reused here: no wrapping or measuring per frame.
            var lines = textBlock.GetLines();
            var alignment = textBlock.TextAlignment;
            float width = textBlock.Bounds.Width;
            float textY = textBlock.FirstBaseline;
            float advance = textBlock.LineAdvance;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.Length > 0 || line.HasEllipsis)
                {
                    float textX = alignment switch
                    {
                        TextAlignment.Center => (width - line.Width) * 0.5f,
                        TextAlignment.Right => width - line.Width,
                        _ => 0f
                    };

                    context.DrawText(textBlock.GetLineText(i), new Point(textX, textY), textColor, textBlock.FontSize, textBlock.FontFamily, textBlock.Bold, textBlock.Italic);
                }
                textY += advance;
            }
        }
        finally
        {
            if (clipSave >= 0)
            {
                context.Canvas.RestoreToCount(clipSave);
            }
        }
    }
}

/// <summary>Draws the <see cref="Panel.Background"/> of every panel type.</summary>
public class MaterialPanelRenderer : ControlRenderer<Panel>
{
    public override void Render(Panel panel, ref DrawingContext context)
    {
        var background = panel.Background;
        var size = panel.Bounds.Size;
        if (background.A > 0 && size.Width > 0 && size.Height > 0)
        {
            context.DrawRect(new Rect(Point.Zero, size), background);
        }
    }
}

public class MaterialBorderRenderer : ControlRenderer<Border>
{
    public override void Render(Border border, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, border.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (border.Elevation > 0)
        {
            context.DrawShadow(bounds, border.CornerRadius, border.Elevation, Color.Black);
        }

        if (border.Background.A > 0)
        {
            context.DrawRoundedRect(bounds, border.CornerRadius, border.Background);
        }

        if (border.BorderBrush.A > 0 && border.BorderThickness.Left > 0)
        {
            context.DrawRoundedRectOutline(bounds, border.CornerRadius, border.BorderBrush, border.BorderThickness.Left);
        }
    }
}

public class MaterialCardRenderer(MaterialColorScheme colors) : ControlRenderer<Card>
{
    public override void Render(Card card, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, card.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var corner = card.CornerRadius;

        Color bg;
        Color border = Color.Transparent;
        float borderWidth = 0f;

        switch (card.Variant)
        {
            case CardVariant.Elevated:
                bg = card.Background.A > 0 ? card.Background : colors.SurfaceContainerLow;
                float elev = card.Elevation > 0 ? card.Elevation : 1f;
                context.DrawShadow(bounds, corner, elev, Color.Black);
                break;

            case CardVariant.Filled:
                bg = card.Background.A > 0 ? card.Background : colors.SurfaceContainerHighest;
                if (card.Elevation > 0)
                {
                    context.DrawShadow(bounds, corner, card.Elevation, Color.Black);
                }
                break;

            case CardVariant.Outlined:
            default:
                bg = card.Background.A > 0 ? card.Background : colors.Surface;
                border = card.BorderBrush.A > 0 ? card.BorderBrush : colors.OutlineVariant;
                borderWidth = card.BorderThickness.Left > 0 ? card.BorderThickness.Left : 1f;
                break;
        }

        context.DrawRoundedRect(bounds, corner, bg);
        if (borderWidth > 0 && border.A > 0)
        {
            context.DrawRoundedRectOutline(bounds, corner, border, borderWidth);
        }
    }
}

public class MaterialToolbarRenderer(MaterialColorScheme colors) : ControlRenderer<Toolbar>
{
    public override void Render(Toolbar toolbar, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, toolbar.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // 1. Material Design Elevation Drop Shadow
        if (toolbar.Elevation > 0)
        {
            context.DrawShadow(bounds, toolbar.CornerRadius, toolbar.Elevation, Color.Black);
        }

        // 2. Toolbar Surface Background
        var bg = toolbar.Background.A > 0 ? toolbar.Background : colors.SurfaceContainer;
        context.DrawRoundedRect(bounds, toolbar.CornerRadius, bg);

        // 3. Optional Divider / Border
        if (toolbar.BorderBrush.A > 0)
        {
            if (toolbar.BorderThickness.Bottom > 0 && toolbar.BorderThickness.Left == 0 && toolbar.BorderThickness.Top == 0 && toolbar.BorderThickness.Right == 0)
            {
                var dividerRect = new Rect(bounds.Left, bounds.Bottom - toolbar.BorderThickness.Bottom, bounds.Width, toolbar.BorderThickness.Bottom);
                context.DrawRect(dividerRect, toolbar.BorderBrush);
            }
            else if (toolbar.BorderThickness.Left > 0)
            {
                context.DrawRoundedRectOutline(bounds, toolbar.CornerRadius, toolbar.BorderBrush, toolbar.BorderThickness.Left);
            }
        }
    }
}

public class MaterialListBoxItemRenderer(MaterialColorScheme colors) : ControlRenderer<ListBoxItem>
{
    public override void Render(ListBoxItem item, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, item.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (item.IsSelected)
        {
            Color selBg = item.IsHovered
                ? colors.SecondaryContainer.WithAlpha(0.85f)
                : colors.SecondaryContainer;
            context.DrawRoundedRect(bounds, item.CornerRadius, selBg);
        }
        else if (item.IsHovered)
        {
            context.DrawRoundedRect(bounds, item.CornerRadius, colors.SurfaceContainerHighest);
        }

        if (item.IsFocused || (item.IsSelected && item.ParentListBox?.IsFocused == true))
        {
            context.DrawRoundedRectOutline(bounds, item.CornerRadius, colors.Primary, 1.5f);
        }
    }
}

public class MaterialTreeViewItemRenderer(MaterialColorScheme colors) : ControlRenderer<TreeViewItem>
{
    public override void Render(TreeViewItem item, ref DrawingContext context)
    {
        var bounds = item.HeaderBounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var cornerRadius = item.CornerRadius.TopLeft > 0 ? item.CornerRadius : new CornerRadius(6);

        if (item.IsSelected)
        {
            Color selBg = item.IsHeaderHovered
                ? colors.SecondaryContainer.WithAlpha(0.85f)
                : colors.SecondaryContainer;
            context.DrawRoundedRect(bounds, cornerRadius, selBg);
        }
        else if (item.IsHeaderHovered)
        {
            context.DrawRoundedRect(bounds, cornerRadius, colors.SurfaceContainerHighest);
        }

        if (item.IsSelected && item.ParentTreeView?.IsFocused == true)
        {
            context.DrawRoundedRectOutline(bounds, cornerRadius, colors.Primary, 1.5f);
        }
    }
}

public class MaterialScrollViewerRenderer(MaterialColorScheme colors) : ControlRenderer<ScrollViewer>
{
    public override void RenderOverlay(ScrollViewer scrollViewer, ref DrawingContext context)
    {
        // 1. Render Vertical ScrollBar
        if (scrollViewer.IsVerticalScrollBarVisible)
        {
            var vTrack = scrollViewer.GetVerticalTrackRect();
            var vThumb = scrollViewer.GetVerticalThumbRect();

            if (vThumb.Height > 0 && vTrack.Height > 0)
            {
                float cornerRadius = vTrack.Width * 0.5f;
                float hoverProgress = Math.Clamp(
                    (vTrack.Width - ScrollViewer.NormalScrollBarWidth) /
                    Math.Max(1f, ScrollViewer.HoveredScrollBarWidth - ScrollViewer.NormalScrollBarWidth),
                    0f, 1f);

                if (hoverProgress > 0.001f)
                {
                    context.DrawRoundedRect(vTrack, new CornerRadius(cornerRadius), colors.SurfaceContainerHighest.WithAlpha(0.6f * hoverProgress));
                }

                Color activeColor = scrollViewer.IsVerticalThumbDragging
                    ? colors.Primary
                    : colors.OnSurfaceVariant;

                Color idleColor = colors.Outline.WithAlpha(0.45f);
                Color thumbColor = Color.Lerp(idleColor, activeColor, hoverProgress);

                context.DrawRoundedRect(vThumb, new CornerRadius(cornerRadius), thumbColor);
            }
        }

        // 2. Render Horizontal ScrollBar
        if (scrollViewer.IsHorizontalScrollBarVisible)
        {
            var hTrack = scrollViewer.GetHorizontalTrackRect();
            var hThumb = scrollViewer.GetHorizontalThumbRect();

            if (hThumb.Width > 0 && hTrack.Height > 0)
            {
                float cornerRadius = hTrack.Height * 0.5f;
                float hoverProgress = Math.Clamp(
                    (hTrack.Height - ScrollViewer.NormalScrollBarWidth) /
                    Math.Max(1f, ScrollViewer.HoveredScrollBarWidth - ScrollViewer.NormalScrollBarWidth),
                    0f, 1f);

                if (hoverProgress > 0.001f)
                {
                    context.DrawRoundedRect(hTrack, new CornerRadius(cornerRadius), colors.SurfaceContainerHighest.WithAlpha(0.6f * hoverProgress));
                }

                Color activeColor = scrollViewer.IsHorizontalThumbDragging
                    ? colors.Primary
                    : colors.OnSurfaceVariant;

                Color idleColor = colors.Outline.WithAlpha(0.45f);
                Color thumbColor = Color.Lerp(idleColor, activeColor, hoverProgress);

                context.DrawRoundedRect(hThumb, new CornerRadius(cornerRadius), thumbColor);
            }
        }
    }
}

public class MaterialTitleBarRenderer(MaterialColorScheme colors) : ControlRenderer<TitleBar>
{
    public override void Render(TitleBar titleBar, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, titleBar.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        Color bg = titleBar.Background.A > 0 ? titleBar.Background : colors.SurfaceContainer;
        context.DrawRect(bounds, bg);

        // Subtle bottom border line
        context.DrawLine(
            new Point(bounds.Left, bounds.Bottom - 1),
            new Point(bounds.Right, bounds.Bottom - 1),
            colors.OutlineVariant.WithAlpha(0.35f),
            1f
        );
    }
}

public class MaterialComboBoxRenderer(MaterialColorScheme colors) : ControlRenderer<ComboBox>
{
    // Space kept free for the chevron on the right; matches the ComboBox layout.
    private const float ChevronAreaWidth = 28f;

    public override void Render(ComboBox comboBox, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, comboBox.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // Container background
        Color containerColor = comboBox.IsHovered ? colors.SurfaceContainerHigh : colors.SurfaceContainerHighest;
        context.DrawRoundedRect(bounds, comboBox.CornerRadius, containerColor);

        // Border / Active indicator
        if (comboBox.IsFocused || comboBox.IsDropDownOpen)
        {
            context.DrawRoundedRectOutline(bounds, comboBox.CornerRadius, colors.Primary, 2f);
        }
        else
        {
            context.DrawRoundedRectOutline(bounds, comboBox.CornerRadius, colors.OutlineVariant, 1f);
        }

        // Draw selection text if no custom template element is active, clipped so long text stops before the chevron
        if (comboBox.SelectionDisplayElement == null)
        {
            var padding = comboBox.Padding;
            float textY = (bounds.Height + comboBox.FontSize) * 0.5f - 2f;
            var textPos = new Point(padding.Left, textY);
            var textClip = new Rect(padding.Left, 0, Math.Max(0, bounds.Width - padding.Left - ChevronAreaWidth), bounds.Height);

            // Cached by the ComboBox when the selection changes, so rendering doesn't call ToString() per frame.
            string? selectedText = comboBox.SelectedItem != null ? comboBox.SelectionBoxText : null;
            if (selectedText != null)
            {
                using var clip = context.PushClip(textClip);
                context.DrawText(selectedText, textPos, colors.OnSurface, comboBox.FontSize, comboBox.FontFamily);
            }
            else if (comboBox.SelectedItem == null && !string.IsNullOrEmpty(comboBox.Placeholder))
            {
                using var clip = context.PushClip(textClip);
                context.DrawText(comboBox.Placeholder, textPos, colors.OnSurfaceVariant.WithAlpha(0.6f), comboBox.FontSize, comboBox.FontFamily);
            }
        }

        // Draw dropdown chevron arrow on the right
        float chevronCenterX = bounds.Right - 18f;
        float chevronCenterY = bounds.Height * 0.5f;
        Color chevronColor = (comboBox.IsHovered || comboBox.IsDropDownOpen) ? colors.Primary : colors.OnSurfaceVariant;

        float arrowSize = 4.5f;
        if (comboBox.IsDropDownOpen)
        {
            // Pointing up
            context.DrawLine(new Point(chevronCenterX - arrowSize, chevronCenterY + 2f), new Point(chevronCenterX, chevronCenterY - 3f), chevronColor, 1.8f);
            context.DrawLine(new Point(chevronCenterX, chevronCenterY - 3f), new Point(chevronCenterX + arrowSize, chevronCenterY + 2f), chevronColor, 1.8f);
        }
        else
        {
            // Pointing down
            context.DrawLine(new Point(chevronCenterX - arrowSize, chevronCenterY - 2f), new Point(chevronCenterX, chevronCenterY + 3f), chevronColor, 1.8f);
            context.DrawLine(new Point(chevronCenterX, chevronCenterY + 3f), new Point(chevronCenterX + arrowSize, chevronCenterY - 2f), chevronColor, 1.8f);
        }
    }
}

public class MaterialPopupRenderer(MaterialColorScheme colors) : ControlRenderer<Popup>
{
    public override void Render(Popup popup, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, popup.ActualBounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        Color bg = popup.Background.A > 0 ? popup.Background : colors.SurfaceContainerHigh;
        context.DrawRoundedRect(bounds, popup.CornerRadius, bg);

        // Outline: BorderThickness defaults to 1; 0 draws none.
        float borderThickness = popup.BorderThickness.Left;
        if (borderThickness > 0)
        {
            Color border = popup.BorderBrush.A > 0 ? popup.BorderBrush : colors.OutlineVariant.WithAlpha(0.5f);
            context.DrawRoundedRectOutline(bounds, popup.CornerRadius, border, borderThickness);
        }
    }
}

public class MaterialDialogRenderer(MaterialColorScheme colors) : ControlRenderer<Dialog>
{
    public override void Render(Dialog dialog, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, dialog.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // 1. Draw Material elevation shadow
        if (dialog.Elevation > 0)
        {
            context.DrawShadow(bounds, dialog.CornerRadius, dialog.Elevation, Color.Black);
        }

        // 2. Draw Dialog background container (SurfaceContainerHigh in Material 3)
        Color bg = dialog.Background.A > 0 ? dialog.Background : colors.SurfaceContainerHigh;
        context.DrawRoundedRect(bounds, dialog.CornerRadius, bg);

        // 3. Draw subtle outline border (BorderThickness defaults to 1; 0 draws none)
        float borderThickness = dialog.BorderThickness.Left;
        if (borderThickness > 0)
        {
            Color border = dialog.BorderBrush.A > 0 ? dialog.BorderBrush : colors.OutlineVariant.WithAlpha(0.35f);
            context.DrawRoundedRectOutline(bounds, dialog.CornerRadius, border, borderThickness);
        }
    }
}

/// <summary>Draws <see cref="Icon"/>s: custom geometry scaled into the bounds, or a Material Symbols glyph.</summary>
public class MaterialIconRenderer(MaterialColorScheme colors) : ControlRenderer<Icon>
{
    /// <inheritdoc/>
    public override void Render(Icon icon, ref DrawingContext context)
    {
        if (icon.Size <= 0) return;

        var bounds = new Rect(Point.Zero, icon.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // An unset foreground means the theme color; anything set (black included) is honored.
        Color fg = MaterialTextBlockRenderer.HasExplicitForeground(icon) ? icon.Foreground : colors.OnSurface;
        if (!icon.IsEnabled)
        {
            fg = fg.WithAlpha(fg.Af * 0.38f);
        }

        // Custom SKPath takes precedence over Material variable font glyph
        if (icon.Data != null && !icon.Data.IsEmpty)
        {
            var path = icon.Data;
            var pathBounds = path.Bounds;
            if (pathBounds.Width <= 0 || pathBounds.Height <= 0) return;

            float targetW = bounds.Width;
            float targetH = bounds.Height;

            float scale = Math.Min(targetW / pathBounds.Width, targetH / pathBounds.Height);
            float scaledW = pathBounds.Width * scale;
            float scaledH = pathBounds.Height * scale;

            float offsetX = bounds.X + (targetW - scaledW) * 0.5f - pathBounds.Left * scale;
            float offsetY = bounds.Y + (targetH - scaledH) * 0.5f - pathBounds.Top * scale;

            var transform = Matrix3x2.CreateScale(scale, scale) * Matrix3x2.CreateTranslation(offsetX, offsetY);
            using (context.PushTransform(transform))
            {
                if (icon.StrokeWidth > 0)
                {
                    float strokeW = icon.StrokeWidth / scale;
                    var strokePaint = context.PaintRegistry.GetStrokePaint(fg, strokeW);
                    context.Canvas.DrawPath(path, strokePaint);
                }
                else
                {
                    var fillPaint = context.PaintRegistry.GetFillPaint(fg);
                    context.Canvas.DrawPath(path, fillPaint);
                }
            }
            return;
        }

        if (icon.Kind == MaterialIconKind.None) return;

        // Cached per kind, so drawing doesn't allocate a string per frame.
        string glyph = MaterialIconFontManager.GetGlyph(icon.Kind);

        // 1. Resolve variable font typeface using the 4 axes
        var tf = MaterialIconFontManager.GetTypeface(icon.Fill, icon.Weight, icon.Grade, icon.OpticalSize);
        var font = context.PaintRegistry.GetFont(icon.Size, tf);

        // 2. Resolve color
        var paint = context.PaintRegistry.GetFillPaint(fg);

        // 3. Precise optical centering using font metrics
        font.GetFontMetrics(out var metrics);
        float glyphWidth = font.MeasureText(glyph.AsSpan());

        float x = bounds.X + (bounds.Width - glyphWidth) * 0.5f;
        float y = bounds.Y + (bounds.Height - (metrics.Ascent + metrics.Descent)) * 0.5f;

        context.Canvas.DrawText(glyph, x, y, SKTextAlign.Left, font, paint);
    }
}

/// <summary>
/// Draws an <see cref="Image"/>'s source inside its padding, scaled per <see cref="Image.Stretch"/> and
/// <see cref="Image.StretchDirection"/> and clipped to the padded area. The element's opacity is applied by the tree
/// renderer, not here.
/// </summary>
public class MaterialImageRenderer : ControlRenderer<Image>
{
    /// <inheritdoc/>
    public override void Render(Image element, ref DrawingContext context)
    {
        var source = element.Source;
        if (source == null || element.Bounds.Width <= 0 || element.Bounds.Height <= 0) return;

        var padding = element.Padding;
        var content = new Rect(
            padding.Left,
            padding.Top,
            Math.Max(0f, element.Bounds.Width - padding.Horizontal),
            Math.Max(0f, element.Bounds.Height - padding.Vertical));
        if (content.Width <= 0 || content.Height <= 0) return;

        Rect destRect = ComputeDestRect(content, new Size(source.Width, source.Height), element.Stretch, element.StretchDirection,
            element.HorizontalAlignment, element.VerticalAlignment);

        bool overflows = destRect.Left < content.Left - 0.01f || destRect.Top < content.Top - 0.01f ||
                         destRect.Right > content.Right + 0.01f || destRect.Bottom > content.Bottom + 0.01f;

        // Opacity 1: VisualTreeRenderer already applies element.Opacity through a layer.
        if (overflows)
        {
            using (context.PushClip(content))
            {
                context.DrawImage(source, destRect);
            }
        }
        else
        {
            context.DrawImage(source, destRect);
        }
    }

    private static Rect ComputeDestRect(Rect content, Size sourceSize, Stretch stretch, StretchDirection direction,
        HorizontalAlignment hAlign, VerticalAlignment vAlign)
    {
        if (sourceSize.IsEmpty) return Rect.Zero;

        var scale = Image.ComputeScale(new Size(content.Width, content.Height), sourceSize, stretch, direction);
        float drawW = sourceSize.Width * scale.Width;
        float drawH = sourceSize.Height * scale.Height;

        float x = hAlign switch
        {
            HorizontalAlignment.Center or HorizontalAlignment.Stretch => (content.Width - drawW) * 0.5f,
            HorizontalAlignment.Right => content.Width - drawW,
            _ => 0f
        };

        float y = vAlign switch
        {
            VerticalAlignment.Center or VerticalAlignment.Stretch => (content.Height - drawH) * 0.5f,
            VerticalAlignment.Bottom => content.Height - drawH,
            _ => 0f
        };

        return new Rect(content.X + x, content.Y + y, drawW, drawH);
    }
}
