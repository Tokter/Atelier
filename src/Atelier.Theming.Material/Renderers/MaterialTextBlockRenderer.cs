using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Rendering;
using SkiaSharp;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws <see cref="TextBlock"/> text using the line layout the text block computed during measure, in its effective
/// font weight and its resolved content color.
/// </summary>
/// <remarks>
/// The color is the text block's own foreground if set, else that of the nearest ancestor that sets a foreground or
/// defines a content color (a filled button's label is on-primary), else on-surface. <see cref="TextBlock.Muted"/> text
/// uses on-surface-variant (or the resolved color at 60%); disabled text 38% opacity.
/// </remarks>
/// <param name="colors">The color scheme.</param>
/// <param name="renderers">The theme's renderers, asked for content colors; <c>null</c> only honors set foregrounds.</param>
public class MaterialTextBlockRenderer(MaterialColorScheme colors, RendererRegistry? renderers = null) : ControlRenderer<TextBlock>
{
    /// <summary>
    /// Returns whether <paramref name="element"/>'s foreground was set on it or on an ancestor (locally, by a style, a
    /// binding or an animation). An unset foreground means "use the theme color".
    /// </summary>
    internal static bool HasExplicitForeground(UIElement element) =>
        element.GetValueSource(Control.ForegroundProperty) != Atelier.Core.Properties.ValueSource.Default;

    /// <summary>
    /// Resolves the color <paramref name="textBlock"/> is drawn in: its content color (see the class remarks); muted text
    /// uses on-surface-variant or the set color at 60% opacity; disabled text 38% opacity.
    /// </summary>
    public Color GetTextColor(TextBlock textBlock)
    {
        Color color = ContentColor.Resolve(textBlock, Control.ForegroundProperty, renderers, colors.OnSurface, out bool isExplicit);

        if (textBlock.Muted)
        {
            color = isExplicit || color != colors.OnSurface ? color.WithOpacity(0.6f) : colors.OnSurfaceVariant;
        }

        return textBlock.IsEnabled ? color : color.WithOpacity(MaterialState.DisabledContentOpacity);
    }

    /// <inheritdoc/>
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
            var weight = textBlock.EffectiveFontWeight;

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

                    context.DrawText(textBlock.GetLineText(i), new Point(textX, textY), textColor, textBlock.FontSize,
                        textBlock.FontFamily, weight, textBlock.Italic);
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
