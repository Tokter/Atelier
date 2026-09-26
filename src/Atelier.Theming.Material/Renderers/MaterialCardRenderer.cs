using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a Material Design 3 <see cref="Card"/> (12 px corners by default):
/// elevated (surface-container-low, elevation level 1), filled (surface-container-highest) or outlined (surface with a
/// 1 px outline-variant border).
/// </summary>
/// <remarks>
/// A set <see cref="Border.Background"/>, <see cref="Border.BorderBrush"/> or <see cref="Border.BorderThickness"/>
/// replaces the variant's value; <see cref="Border.Elevation"/> raises the elevation of any variant.
/// </remarks>
public class MaterialCardRenderer(MaterialColorScheme colors) : ControlRenderer<Card>
{
    /// <inheritdoc/>
    public override void Render(Card card, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, card.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var corner = card.CornerRadius;
        Color background;
        float elevation = card.Elevation;
        Color borderColor = card.BorderBrush;
        var borderThickness = card.BorderThickness;

        switch (card.Variant)
        {
            case CardVariant.Elevated:
                background = colors.SurfaceContainerLow;
                elevation = Math.Max(elevation, MaterialElevation.Level1);
                break;

            case CardVariant.Filled:
                background = colors.SurfaceContainerHighest;
                break;

            default: // Outlined
                background = colors.Surface;
                if (borderColor.A == 0) borderColor = colors.OutlineVariant;
                if (IsZero(borderThickness)) borderThickness = new Thickness(1f);
                break;
        }

        if (card.Background.A > 0)
        {
            background = card.Background;
        }

        if (elevation > 0)
        {
            context.DrawShadow(bounds, corner, elevation, colors.Shadow);
        }

        context.DrawRoundedRect(bounds, corner, background);
        context.DrawBorder(bounds, corner, borderThickness, borderColor);
    }

    private static bool IsZero(in Thickness t) => t.Left <= 0 && t.Top <= 0 && t.Right <= 0 && t.Bottom <= 0;
}
