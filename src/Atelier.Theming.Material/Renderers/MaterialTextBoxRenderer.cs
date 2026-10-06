using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Rendering;
using SkiaSharp;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a Material Design 3 <see cref="TextBox"/> (outlined or filled text field): container, leading icon, floating
/// label, text with selection and caret, and supporting text.
/// </summary>
/// <remarks>
/// <para>
/// Outlined: a 1 px outline (on-surface when hovered, 2 px primary when focused, error when invalid) with a gap behind
/// the floating label. Filled: a surface-container-highest container with rounded top corners and a 1 px active
/// indicator (2 px primary when focused), plus the on-surface hover state layer.
/// </para>
/// <para>
/// The label animates between body-large inside the field and body-small (12 px) on the outline. Text positions come
/// from the text box's cached character offsets, so nothing is measured or allocated per frame.
/// </para>
/// <para>
/// Multi-line fields draw only their visible lines (from the text box's cached line layout), keep the icons and the
/// resting label beside the first line, and show a 4 px scroll indicator 4 px inside the right edge while the lines
/// don't fit.
/// </para>
/// </remarks>
public class MaterialTextBoxRenderer(MaterialColorScheme colors) : ControlRenderer<TextBox>
{
    private const float FloatingLabelSize = 12f; // body-small
    private const float LeadingIconSize = 24f;
    private const float LeadingIconX = 12f;
    private const float SupportingTextHeight = 20f;
    private const float SupportingTextSize = 12f;
    private const float ScrollIndicatorWidth = 4f;
    private const float ScrollIndicatorInset = 4f;

    /// <inheritdoc/>
    public override void Render(TextBox textBox, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, textBox.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        bool enabled = textBox.IsEnabled;
        bool focused = textBox.IsFocused;
        bool hasError = textBox.HasValidationError;
        bool outlined = textBox.Variant != TextBoxVariant.Filled;
        float progress = textBox.LabelAnimationProgress;

        float supportingHeight = textBox.HasSupportingText ? SupportingTextHeight : 0f;
        var container = new Rect(bounds.Left, bounds.Top, bounds.Width, Math.Max(36f, bounds.Height - supportingHeight));
        float textStartX = textBox.GetTextContentStartX();

        // Label geometry (needed first: the outline has a gap behind the floating label).
        float labelSize = textBox.FontSize + (FloatingLabelSize - textBox.FontSize) * progress;
        float labelX = textStartX + ((outlined ? container.Left + 16f : textStartX) - textStartX) * progress;
        // A multi-line field keeps the resting label and the icons beside its first line.
        float rowHeight = textBox.IsMultiline ? textBox.GetFieldRowHeight() : container.Height;
        float restingY = container.Top + (rowHeight + textBox.FontSize) * 0.5f - 2f;
        float floatingY = outlined ? container.Top + FloatingLabelSize * 0.5f : container.Top + 8f + FloatingLabelSize;
        float labelY = restingY + (floatingY - restingY) * progress;

        if (outlined)
        {
            DrawOutline(textBox, container, enabled, focused, hasError, progress, labelX, labelSize, ref context);
        }
        else
        {
            DrawFilledContainer(textBox, container, enabled, focused, hasError, ref context);
        }

        if (textBox.HasLeadingIcon)
        {
            DrawLeadingIcon(textBox, container, rowHeight, enabled, ref context);
        }

        if (textBox.HasTrailingIcon)
        {
            DrawTrailingIcon(textBox, enabled, hasError, ref context);
        }

        if (textBox.HasLabel)
        {
            Color labelColor = !enabled ? MaterialDrawing.DisabledContent(colors)
                : hasError ? colors.Error
                : Color.Lerp(colors.OnSurfaceVariant, focused ? colors.Primary : colors.OnSurfaceVariant, progress);
            context.DrawText(textBox.Label, new Point(labelX, labelY), labelColor, labelSize, textBox.FontFamily);
        }

        DrawText(textBox, container, textStartX, enabled, focused, hasError, progress, ref context);

        // Supporting text: the first validation error replaces it, in the error color.
        if (textBox.HasSupportingText)
        {
            Color supportColor = !enabled ? MaterialDrawing.DisabledContent(colors)
                : hasError ? colors.Error
                : colors.OnSurfaceVariant;
            context.DrawText(textBox.DisplayedSupportingText, new Point(container.Left + 16f, container.Bottom + 16f),
                supportColor, SupportingTextSize, textBox.FontFamily);
        }
    }

    private void DrawOutline(TextBox textBox, in Rect container, bool enabled, bool focused, bool hasError,
        float progress, float labelX, float labelSize, ref DrawingContext context)
    {
        Color color;
        float width = 1f;
        if (!enabled)
        {
            color = MaterialDrawing.DisabledContainer(colors);
        }
        else if (hasError)
        {
            color = colors.Error;
            width = focused ? 2f : 1f;
        }
        else if (focused)
        {
            color = colors.Primary;
            width = 2f;
        }
        else
        {
            color = textBox.IsHovered ? colors.OnSurface : colors.Outline;
        }

        // Leave a gap in the top edge behind the floating label, so the field works on any background.
        int save = -1;
        if (textBox.HasLabel && progress > 0.05f)
        {
            float labelWidth = context.MeasureText(textBox.Label, labelSize, textBox.FontFamily).Width;
            float gap = (labelWidth + 8f) * progress;
            save = context.Canvas.Save();
            context.Canvas.ClipRect(new SKRect(labelX - 4f, container.Top - width, labelX - 4f + gap, container.Top + width + 1f),
                SKClipOperation.Difference, antialias: true);
        }

        context.DrawRoundedRectOutline(container, textBox.CornerRadius, color, width);

        if (save >= 0)
        {
            context.Canvas.RestoreToCount(save);
        }
    }

    private void DrawFilledContainer(TextBox textBox, in Rect container, bool enabled, bool focused, bool hasError, ref DrawingContext context)
    {
        var topCorners = new CornerRadius(textBox.CornerRadius.TopLeft, textBox.CornerRadius.TopRight, 0, 0);
        context.DrawRoundedRect(container, topCorners,
            enabled ? colors.SurfaceContainerHighest : colors.OnSurface.WithOpacity(0.04f));

        if (enabled && textBox.IsHovered && !focused)
        {
            MaterialDrawing.DrawStateLayer(ref context, container, topCorners, colors.OnSurface, MaterialState.HoverOpacity);
        }

        // Active indicator along the bottom edge.
        Color indicator;
        float height = 1f;
        if (!enabled)
        {
            indicator = colors.OnSurface.WithOpacity(0.38f);
        }
        else if (focused)
        {
            indicator = hasError ? colors.Error : colors.Primary;
            height = 2f;
        }
        else
        {
            indicator = hasError ? colors.Error : textBox.IsHovered ? colors.OnSurface : colors.OnSurfaceVariant;
        }

        context.DrawRect(new Rect(container.Left, container.Bottom - height, container.Width, height), indicator);
    }

    private void DrawLeadingIcon(TextBox textBox, in Rect container, float rowHeight, bool enabled, ref DrawingContext context)
    {
        Color color = !enabled ? MaterialDrawing.DisabledContent(colors) : colors.OnSurfaceVariant;
        string glyph = MaterialIconFontManager.GetGlyph(textBox.LeadingIconKind);
        var font = context.PaintRegistry.GetFont(LeadingIconSize, MaterialIconFontManager.GetTypeface(0, 400, 0, LeadingIconSize));
        font.GetFontMetrics(out var metrics);
        float glyphWidth = font.MeasureText(glyph.AsSpan());
        float x = container.Left + LeadingIconX + (LeadingIconSize - glyphWidth) * 0.5f;
        float top = container.Top + (rowHeight - LeadingIconSize) * 0.5f;
        float y = top + (LeadingIconSize - (metrics.Ascent + metrics.Descent)) * 0.5f;
        context.DrawText(glyph, x, y, font, color);
    }

    // The trailing icon button: on-surface-variant (error while invalid), with a circular state layer on hover and press.
    private void DrawTrailingIcon(TextBox textBox, bool enabled, bool hasError, ref DrawingContext context)
    {
        var target = textBox.GetTrailingIconBounds();
        var center = new Point(target.X + target.Width * 0.5f, target.Y + target.Height * 0.5f);
        Color color = !enabled ? MaterialDrawing.DisabledContent(colors) : hasError ? colors.Error : colors.OnSurfaceVariant;

        float stateOpacity = !enabled ? 0f
            : textBox.IsTrailingIconPressed ? MaterialState.PressedOpacity
            : textBox.IsTrailingIconHovered ? MaterialState.HoverOpacity
            : 0f;
        MaterialDrawing.DrawStateLayerCircle(ref context, center, colors.OnSurfaceVariant, stateOpacity, target.Width * 0.5f);

        string glyph = MaterialIconFontManager.GetGlyph(textBox.TrailingIconKind);
        var font = context.PaintRegistry.GetFont(LeadingIconSize, MaterialIconFontManager.GetTypeface(0, 400, 0, LeadingIconSize));
        font.GetFontMetrics(out var metrics);
        float glyphWidth = font.MeasureText(glyph.AsSpan());
        float x = center.X - glyphWidth * 0.5f;
        float y = center.Y - (metrics.Ascent + metrics.Descent) * 0.5f;
        context.DrawText(glyph, x, y, font, color);
    }

    private void DrawText(TextBox textBox, in Rect container, float textStartX, bool enabled, bool focused, bool hasError,
        float progress, ref DrawingContext context)
    {
        if (textBox.IsMultiline)
        {
            DrawLines(textBox, container, textStartX, enabled, focused, hasError, progress, ref context);
            return;
        }

        float textY = textBox.Variant == TextBoxVariant.Filled && textBox.HasLabel
            ? container.Top + container.Height - 14f
            : container.Top + (container.Height + textBox.FontSize) * 0.5f - 2f;

        float originX = textBox.GetTextOriginX();
        float viewportWidth = textBox.GetViewportWidth();
        bool hasText = textBox.Text.Length > 0;

        using var clip = context.PushClip(new Rect(textStartX, container.Top + 1f, viewportWidth, Math.Max(0f, container.Height - 2f)));

        if (focused && textBox.HasSelection && hasText)
        {
            int start = textBox.SelectionStart;
            float startX = originX + textBox.GetCharacterOffset(start);
            float width = textBox.GetCharacterOffset(start + textBox.SelectionLength) - textBox.GetCharacterOffset(start);
            context.DrawRect(new Rect(startX, textY - textBox.FontSize - 1f, width, textBox.FontSize + 4f), colors.Primary.WithOpacity(0.35f));
        }

        if (hasText)
        {
            // An unset foreground means the theme's input text color; anything set (black included) is honored.
            Color baseColor = MaterialDrawing.IsSet(textBox, Control.ForegroundProperty) ? textBox.Foreground : colors.OnSurface;
            Color textColor = enabled ? baseColor : baseColor.WithOpacity(MaterialState.DisabledContentOpacity);
            context.DrawText(textBox.DisplayText, new Point(originX, textY), textColor, textBox.FontSize, textBox.FontFamily);
        }
        else if (!string.IsNullOrEmpty(textBox.Placeholder) && (!textBox.HasLabel || progress > 0.8f))
        {
            float x = originX;
            if (textBox.TextAlignment != TextAlignment.Left)
            {
                float free = viewportWidth - context.MeasureText(textBox.Placeholder, textBox.FontSize, textBox.FontFamily).Width;
                x = textStartX + Math.Max(0f, textBox.TextAlignment == TextAlignment.Center ? free * 0.5f : free);
            }

            Color color = enabled ? colors.OnSurfaceVariant : MaterialDrawing.DisabledContent(colors);
            context.DrawText(textBox.Placeholder, new Point(x, textY), color, textBox.FontSize, textBox.FontFamily);
        }

        if (focused && enabled && textBox.CaretVisible && !textBox.HasSelection)
        {
            float caretX = originX + textBox.GetCharacterOffset(textBox.CaretIndex);
            float caretWidth = Math.Max(1f, MathF.Round(textBox.CaretWidth));
            context.DrawPixelRect(new Rect(caretX, textY - textBox.FontSize, caretWidth, textBox.FontSize + 2f),
                hasError ? colors.Error : colors.Primary);
        }
    }

    // Multi-line text: only the visible lines, each selection segment the full line height (plus a sliver for a selected
    // line break), and a scroll indicator along the right edge while the lines don't fit.
    private void DrawLines(TextBox textBox, in Rect container, float textStartX, bool enabled, bool focused, bool hasError,
        float progress, ref DrawingContext context)
    {
        float viewportWidth = textBox.GetViewportWidth();
        float viewportTop = textBox.GetViewportTop();
        float viewportHeight = textBox.GetViewportHeight();
        float lineHeight = textBox.LineHeight;
        float scroll = textBox.VerticalScrollOffset;
        bool hasText = textBox.Text.Length > 0;

        // A little room above and below the viewport for accents and descenders, but never over the border.
        float clipTop = Math.Max(container.Top + 1f, viewportTop - 2f);
        float clipBottom = Math.Min(container.Bottom - 1f, viewportTop + viewportHeight + 2f);
        using (context.PushClip(new Rect(textStartX, clipTop, viewportWidth, Math.Max(0f, clipBottom - clipTop))))
        {
            int lineCount = textBox.LineCount;
            int first = Math.Max(0, (int)(scroll / lineHeight));
            int last = Math.Min(lineCount - 1, (int)((scroll + viewportHeight) / lineHeight));

            int selectionStart = textBox.SelectionStart;
            int selectionEnd = selectionStart + textBox.SelectionLength;
            bool drawSelection = focused && textBox.HasSelection && hasText;
            Color selectionColor = colors.Primary.WithOpacity(0.35f);

            Color baseColor = MaterialDrawing.IsSet(textBox, Control.ForegroundProperty) ? textBox.Foreground : colors.OnSurface;
            Color textColor = enabled ? baseColor : baseColor.WithOpacity(MaterialState.DisabledContentOpacity);

            for (int line = first; line <= last; line++)
            {
                float originX = textBox.GetLineOriginX(line);
                int lineStart = textBox.GetLineStart(line);
                int lineEnd = lineStart + textBox.GetLineLength(line);

                if (drawSelection && selectionStart <= lineEnd && selectionEnd >= lineStart)
                {
                    int from = Math.Max(selectionStart, lineStart);
                    int to = Math.Min(selectionEnd, lineEnd);
                    float x = originX + textBox.GetCharacterOffset(from) - textBox.GetCharacterOffset(lineStart);
                    float width = textBox.GetCharacterOffset(to) - textBox.GetCharacterOffset(from);
                    bool selectsLineBreak = selectionEnd > lineEnd && line < lineCount - 1 && textBox.GetLineStart(line + 1) > lineEnd;
                    if (selectsLineBreak)
                    {
                        width += MathF.Round(textBox.FontSize * 0.3f);
                    }
                    if (width > 0)
                    {
                        context.DrawRect(new Rect(x, textBox.GetLineTop(line), width, lineHeight), selectionColor);
                    }
                }

                if (hasText)
                {
                    context.DrawText(textBox.GetLineText(line), new Point(originX, textBox.GetLineBaselineY(line)), textColor,
                        textBox.FontSize, textBox.FontFamily);
                }
            }

            if (!hasText && !string.IsNullOrEmpty(textBox.Placeholder) && (!textBox.HasLabel || progress > 0.8f))
            {
                float x = textStartX;
                if (textBox.TextAlignment != TextAlignment.Left)
                {
                    float free = viewportWidth - context.MeasureText(textBox.Placeholder, textBox.FontSize, textBox.FontFamily).Width;
                    x += Math.Max(0f, textBox.TextAlignment == TextAlignment.Center ? free * 0.5f : free);
                }

                Color color = enabled ? colors.OnSurfaceVariant : MaterialDrawing.DisabledContent(colors);
                context.DrawText(textBox.Placeholder, new Point(x, textBox.GetLineBaselineY(0)), color, textBox.FontSize, textBox.FontFamily);
            }

            if (focused && enabled && textBox.CaretVisible && !textBox.HasSelection)
            {
                int caret = textBox.CaretIndex;
                float baseline = textBox.GetLineBaselineY(textBox.GetLineIndexFromCharacterIndex(caret));
                float caretWidth = Math.Max(1f, MathF.Round(textBox.CaretWidth));
                context.DrawPixelRect(new Rect(textBox.GetCharacterX(caret), baseline - textBox.FontSize, caretWidth, textBox.FontSize + 2f),
                    hasError ? colors.Error : colors.Primary);
            }
        }

        float contentHeight = textBox.GetContentHeight();
        if (contentHeight > viewportHeight + 0.5f)
        {
            float thumbHeight = Math.Max(16f, viewportHeight * viewportHeight / contentHeight);
            float maxScroll = contentHeight - viewportHeight;
            float thumbY = viewportTop + (viewportHeight - thumbHeight) * Math.Clamp(scroll / maxScroll, 0f, 1f);
            var thumb = new Rect(container.Right - ScrollIndicatorInset - ScrollIndicatorWidth, thumbY, ScrollIndicatorWidth, thumbHeight);
            Color thumbColor = colors.OnSurfaceVariant.WithOpacity(enabled ? 0.5f : 0.2f);
            context.DrawRoundedRect(thumb, new CornerRadius(ScrollIndicatorWidth * 0.5f), thumbColor);
        }
    }
}
