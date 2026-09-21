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
        float boxSize = 18f;
        float y = (checkBox.Bounds.Height - boxSize) * 0.5f;
        var boxRect = new Rect(0, y, boxSize, boxSize);
        var corner = new CornerRadius(2);

        float progress = checkBox.CheckAnimationProgress;
        bool isEnabled = checkBox.IsEnabled;

        if (!isEnabled)
        {
            Color disabledColor = colors.OnSurface.WithAlpha(0.38f);
            if (progress <= 0.01f)
            {
                context.DrawRoundedRectOutline(boxRect, corner, disabledColor, 2f);
            }
            else
            {
                context.DrawRoundedRect(boxRect, corner, disabledColor);
                if (progress > 0.1f)
                {
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
                    context.DrawPathOutline(path, colors.Surface, 2.0f);
                }
            }
            return;
        }

        if (progress <= 0.01f)
        {
            // Unchecked state: outline only
            Color outlineColor = checkBox.IsHovered ? colors.OnSurface : colors.Outline;
            context.DrawRoundedRectOutline(boxRect, corner, outlineColor, 2f);
        }
        else
        {
            // Checked / Transitioning state: fill primary
            Color fillColor = Color.Lerp(colors.Outline, colors.Primary, progress);
            context.DrawRoundedRect(boxRect, corner, fillColor);

            // Draw vector checkmark morphing with progress
            if (progress > 0.1f)
            {
                using var builder = new SKPathBuilder();
                // Checkmark coordinates relative to box
                float x1 = boxRect.X + 3.5f;
                float y1 = boxRect.Y + 9f;
                float x2 = boxRect.X + 7.5f;
                float y2 = boxRect.Y + 13f;
                float x3 = boxRect.X + 14.5f;
                float y3 = boxRect.Y + 5.5f;

                builder.MoveTo(x1, y1);
                builder.LineTo(x2, y2);
                // Animate checkmark second stroke
                float endX = x2 + (x3 - x2) * progress;
                float endY = y2 + (y3 - y2) * progress;
                builder.LineTo(endX, endY);

                using var path = builder.Detach();
                context.DrawPathOutline(path, colors.OnPrimary, 2.0f);
            }
        }

        // Draw focus ring
        if (checkBox.IsFocused)
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
        float size = 20f;
        float y = (radioButton.Bounds.Height - size) * 0.5f;
        var center = new Point(size * 0.5f, y + size * 0.5f);
        float radius = 9f;
        bool isEnabled = radioButton.IsEnabled;

        if (!isEnabled)
        {
            Color disabledColor = colors.OnSurface.WithAlpha(0.38f);
            if (radioButton.IsChecked)
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

        if (radioButton.IsChecked)
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
        float trackW = 40f;
        float trackH = 22f;
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

public class MaterialTextBoxRenderer(MaterialColorScheme colors) : ControlRenderer<TextBox>
{
    public override void Render(TextBox textBox, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, textBox.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // Container background
        Color containerColor = colors.SurfaceContainerHighest;
        context.DrawRoundedRect(bounds, textBox.CornerRadius, containerColor);

        // Active indicator line or focus outline
        if (textBox.IsFocused)
        {
            context.DrawRoundedRectOutline(bounds, textBox.CornerRadius, colors.Primary, 2f);
        }
        else
        {
            // Bottom line for filled text box, taking CornerRadius into account
            float startX = bounds.Left + textBox.CornerRadius.BottomLeft;
            float endX = bounds.Right - textBox.CornerRadius.BottomRight;

            if (endX > startX)
            {
                using var clip = context.PushRoundedClip(bounds, textBox.CornerRadius);
                context.DrawLine(
                    new Point(startX, bounds.Bottom - 1),
                    new Point(endX, bounds.Bottom - 1),
                    colors.OnSurfaceVariant,
                    1f
                );
            }
        }

        // Draw text or placeholder
        float textY = (bounds.Height + textBox.FontSize) * 0.5f - 2;
        var textPos = new Point(textBox.Padding.Left, textY);

        // Draw selection highlight behind text
        if (textBox.IsFocused && textBox.HasSelection && !string.IsNullOrEmpty(textBox.Text))
        {
            int selStart = textBox.SelectionStart;
            int selLen = textBox.SelectionLength;

            float selStartX = textBox.Padding.Left;
            if (selStart > 0)
            {
                var prefix = textBox.Text[..Math.Min(selStart, textBox.Text.Length)];
                selStartX += context.MeasureText(prefix, textBox.FontSize, textBox.FontFamily).Width;
            }

            var selSubstring = textBox.Text.Substring(selStart, Math.Min(selLen, textBox.Text.Length - selStart));
            float selWidth = context.MeasureText(selSubstring, textBox.FontSize, textBox.FontFamily).Width;

            float selTop = (bounds.Height - textBox.FontSize) * 0.5f - 2;
            float selHeight = textBox.FontSize + 4;

            context.DrawRect(new Rect(selStartX, selTop, selWidth, selHeight), colors.Primary.WithAlpha(0.35f));
        }

        if (!string.IsNullOrEmpty(textBox.Text))
        {
            context.DrawText(textBox.Text, textPos, colors.OnSurface, textBox.FontSize, textBox.FontFamily);
        }
        else if (!string.IsNullOrEmpty(textBox.Placeholder))
        {
            context.DrawText(textBox.Placeholder, textPos, colors.OnSurfaceVariant.WithAlpha(0.6f), textBox.FontSize, textBox.FontFamily);
        }

        // Draw blinking caret
        if (textBox.IsFocused && textBox.CaretVisible && !textBox.HasSelection)
        {
            float caretX = textBox.Padding.Left;
            if (!string.IsNullOrEmpty(textBox.Text) && textBox.CaretIndex > 0)
            {
                var textBeforeCaret = textBox.Text[..Math.Min(textBox.CaretIndex, textBox.Text.Length)];
                var measured = context.MeasureText(textBeforeCaret, textBox.FontSize, textBox.FontFamily);
                caretX += measured.Width;
            }

            float caretWidth = Math.Max(1f, MathF.Round(textBox.CaretWidth));
            float caretTop = (bounds.Height - textBox.FontSize) * 0.5f - 1f;
            float caretHeight = textBox.FontSize + 2f;

            context.DrawPixelRect(new Rect(caretX, caretTop, caretWidth, caretHeight), colors.Primary);
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

        // Position track: if ShowValueIndicator, leave headroom for the floating value bubble
        float trackCenterY = slider.ShowValueIndicator && bounds.Height >= 36f
            ? bounds.Height - 14f
            : bounds.Height * 0.5f;
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
            string text = string.Format(slider.ValueFormat, slider.Value);
            float fontSize = 11f;

            var textSize = context.MeasureText(text, fontSize, bold: true);
            float bubbleWidth = Math.Max(28f, textSize.Width + 14f);
            float bubbleHeight = 20f;
            float bubbleCorner = bubbleHeight * 0.5f;

            // Center bubble above knob, clamping horizontally within bounds
            float bubbleX = Math.Clamp(handleX - bubbleWidth * 0.5f, bounds.Left, bounds.Right - bubbleWidth);
            float bubbleY = handleCenter.Y - handleRadius - 4f - bubbleHeight;
            if (bubbleY < bounds.Top) bubbleY = bounds.Top;

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

public class MaterialTextBlockRenderer(MaterialColorScheme colors) : ControlRenderer<TextBlock>
{
    public override void Render(TextBlock textBlock, ref DrawingContext context)
    {
        if (string.IsNullOrEmpty(textBlock.Text)) return;

        Color textColor;
        if (textBlock.Foreground != Color.Black)
        {
            textColor = textBlock.Foreground;
        }
        else if (textBlock.Muted)
        {
            textColor = colors.OnSurfaceVariant;
        }
        else
        {
            textColor = colors.OnSurface;
        }

        if (!textBlock.IsEnabled)
        {
            textColor = textColor.WithAlpha(textColor.Af * 0.38f);
        }

        if (textBlock.TextWrapping == TextWrapping.Wrap && textBlock.Bounds.Width > 0)
        {
            var (_, lines) = TextMeasurer.MeasureWrapped(
                textBlock.Text,
                textBlock.Bounds.Width,
                textBlock.FontSize,
                textBlock.FontFamily,
                textBlock.Bold
            );

            float lineHeight = TextMeasurer.GetFontSpacing(textBlock.FontSize, textBlock.FontFamily, textBlock.Bold);
            if (lineHeight <= 0) lineHeight = textBlock.FontSize * 1.35f;
            float textY = textBlock.FontSize;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                float textX = 0;

                if (textBlock.TextAlignment != TextAlignment.Left)
                {
                    var measured = context.MeasureText(line, textBlock.FontSize, textBlock.FontFamily, textBlock.Bold);
                    if (textBlock.TextAlignment == TextAlignment.Center)
                    {
                        textX = (textBlock.Bounds.Width - measured.Width) * 0.5f;
                    }
                    else if (textBlock.TextAlignment == TextAlignment.Right)
                    {
                        textX = textBlock.Bounds.Width - measured.Width;
                    }
                }

                context.DrawText(line, new Point(textX, textY), textColor, textBlock.FontSize, textBlock.FontFamily, textBlock.Bold);
                textY += lineHeight;
            }
        }
        else
        {
            float textY = textBlock.FontSize;
            float textX = 0;

            if (textBlock.TextAlignment != TextAlignment.Left)
            {
                var measured = context.MeasureText(textBlock.Text, textBlock.FontSize, textBlock.FontFamily, textBlock.Bold);
                if (textBlock.TextAlignment == TextAlignment.Center)
                {
                    textX = (textBlock.Bounds.Width - measured.Width) * 0.5f;
                }
                else if (textBlock.TextAlignment == TextAlignment.Right)
                {
                    textX = textBlock.Bounds.Width - measured.Width;
                }
            }

            context.DrawText(textBlock.Text, new Point(textX, textY), textColor, textBlock.FontSize, textBlock.FontFamily, textBlock.Bold);
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

        var corner = card.CornerRadius.TopLeft > 0 ? card.CornerRadius : new CornerRadius(12f);

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
        if (scrollViewer.CanScrollVertically)
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
        if (scrollViewer.CanScrollHorizontally)
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

        // Draw selection text if no custom template element is active
        if (comboBox.SelectionDisplayElement == null)
        {
            float textY = (bounds.Height + comboBox.FontSize) * 0.5f - 2f;
            var textPos = new Point(comboBox.Padding.Left, textY);

            if (comboBox.SelectedItem != null)
            {
                context.DrawText(comboBox.SelectedItem.ToString() ?? string.Empty, textPos, colors.OnSurface, comboBox.FontSize, comboBox.FontFamily);
            }
            else if (!string.IsNullOrEmpty(comboBox.Placeholder))
            {
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

        Color border = popup.BorderBrush.A > 0 ? popup.BorderBrush : colors.OutlineVariant.WithAlpha(0.5f);
        float borderThickness = popup.BorderThickness.Left > 0 ? popup.BorderThickness.Left : 1f;
        context.DrawRoundedRectOutline(bounds, popup.CornerRadius, border, borderThickness);
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

        // 3. Draw subtle outline border
        Color border = dialog.BorderBrush.A > 0 ? dialog.BorderBrush : colors.OutlineVariant.WithAlpha(0.35f);
        float borderThickness = dialog.BorderThickness.Left > 0 ? dialog.BorderThickness.Left : 1f;
        context.DrawRoundedRectOutline(bounds, dialog.CornerRadius, border, borderThickness);
    }
}

public class MaterialIconRenderer(MaterialColorScheme colors) : ControlRenderer<Icon>
{
    public override void Render(Icon icon, ref DrawingContext context)
    {
        if (icon.Size <= 0) return;

        var bounds = new Rect(Point.Zero, icon.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        Color fg = (icon.Foreground != Color.Black && icon.Foreground.A > 0) ? icon.Foreground : colors.OnSurface;
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

        string glyph = char.ConvertFromUtf32((int)icon.Kind);

        // 1. Resolve variable font typeface using the 4 axes
        var tf = MaterialIconFontManager.GetTypeface(icon.Fill, icon.Weight, icon.Grade, icon.OpticalSize);
        var font = context.PaintRegistry.GetFont(icon.Size, tf);

        // 2. Resolve color
        var paint = context.PaintRegistry.GetFillPaint(fg);

        // 3. Precise optical centering using font metrics
        font.GetFontMetrics(out var metrics);
        float glyphWidth = font.MeasureText(glyph);

        float x = bounds.X + (bounds.Width - glyphWidth) * 0.5f;
        float y = bounds.Y + (bounds.Height - (metrics.Ascent + metrics.Descent)) * 0.5f;

        context.Canvas.DrawText(glyph, x, y, SKTextAlign.Left, font, paint);
    }
}

public class MaterialImageRenderer : ControlRenderer<Image>
{
    public override void Render(Image element, ref DrawingContext context)
    {
        var source = element.Source;
        if (source == null || element.Bounds.Width <= 0 || element.Bounds.Height <= 0) return;

        Rect destRect = ComputeDestRect(element.Bounds.Size, new Size(source.Width, source.Height), element.Stretch, element.HorizontalAlignment, element.VerticalAlignment);
        context.DrawImage(source, destRect, element.Opacity);
    }

    private static Rect ComputeDestRect(Size boundsSize, Size sourceSize, Stretch stretch, HorizontalAlignment hAlign, VerticalAlignment vAlign)
    {
        if (sourceSize.IsEmpty || boundsSize.IsEmpty) return Rect.Zero;

        float targetW = boundsSize.Width;
        float targetH = boundsSize.Height;
        float srcW = sourceSize.Width;
        float srcH = sourceSize.Height;

        float drawW = srcW;
        float drawH = srcH;

        switch (stretch)
        {
            case Stretch.None:
                drawW = srcW;
                drawH = srcH;
                break;
            case Stretch.Fill:
                drawW = targetW;
                drawH = targetH;
                break;
            case Stretch.Uniform:
                float scale = Math.Min(targetW / srcW, targetH / srcH);
                drawW = srcW * scale;
                drawH = srcH * scale;
                break;
            case Stretch.UniformToFill:
                float fillScale = Math.Max(targetW / srcW, targetH / srcH);
                drawW = srcW * fillScale;
                drawH = srcH * fillScale;
                break;
        }

        float x = 0;
        switch (hAlign)
        {
            case HorizontalAlignment.Center:
            case HorizontalAlignment.Stretch:
                x = (targetW - drawW) * 0.5f;
                break;
            case HorizontalAlignment.Right:
                x = targetW - drawW;
                break;
        }

        float y = 0;
        switch (vAlign)
        {
            case VerticalAlignment.Center:
            case VerticalAlignment.Stretch:
                y = (targetH - drawH) * 0.5f;
                break;
            case VerticalAlignment.Bottom:
                y = targetH - drawH;
                break;
        }

        return new Rect(x, y, drawW, drawH);
    }
}
