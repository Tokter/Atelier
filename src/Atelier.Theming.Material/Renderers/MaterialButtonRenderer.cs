using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws <see cref="Button"/>s as Material Design 3 common buttons (filled, tonal, elevated, outlined and text) and
/// supplies their label color. <see cref="TitleBarButton"/>s are drawn as window caption buttons.
/// </summary>
/// <remarks>
/// Per the MD3 tokens: the container and label colors come from the variant; hover, keyboard focus and press add a
/// state layer in the label color (8%/12%/12%); disabled buttons use on-surface at 12% (container) and 38% (label);
/// filled and tonal buttons rise to level 1 on hover, elevated buttons rest at level 1 and rise to level 2. A set
/// <see cref="Control.Background"/> replaces the variant's container color, a set <see cref="Control.Foreground"/> the
/// label color. Keyboard focus draws the focus ring.
/// </remarks>
public class MaterialButtonRenderer(MaterialColorScheme colors) : ControlRenderer<Button>, IContentColorProvider
{
    // Windows caption button colors for the close button.
    private static readonly Color CloseHoverColor = Color.FromRgb(196, 43, 28);
    private static readonly Color ClosePressedColor = Color.FromRgb(200, 60, 48);

    /// <inheritdoc/>
    public override void Render(Button button, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, button.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (button is TitleBarButton captionButton)
        {
            RenderCaptionButton(captionButton, bounds, ref context);
            return;
        }

        var corner = button.CornerRadius;
        bool enabled = button.IsEnabled;
        Color content = GetContentColor(button);

        // Elevation (shadow) per variant and state.
        float elevation = enabled ? GetElevation(button) : 0f;
        if (elevation > 0)
        {
            context.DrawShadow(bounds, corner, elevation, colors.Shadow);
        }

        // Container.
        Color container = enabled ? GetContainerColor(button) : DisabledContainerColor(button.Variant);
        if (container.A > 0)
        {
            context.DrawRoundedRect(bounds, corner, container);
        }

        // State layer in the label color.
        MaterialDrawing.DrawStateLayer(ref context, bounds, corner, content, MaterialDrawing.StateLayerOpacity(button));

        if (button.Variant == ButtonVariant.Outlined)
        {
            Color outline = enabled ? colors.Outline : MaterialDrawing.DisabledContainer(colors);
            context.DrawRoundedRectOutline(bounds, corner, outline, 1f);
        }

        if (button.HasActiveRipple)
        {
            using var clip = context.PushRoundedClip(bounds, corner);
            float radius = MathF.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height) * button.RippleProgress;
            context.DrawCircle(button.RippleCenter, radius, content.WithOpacity(button.RippleOpacity));
        }

        if (button.IsFocusVisible)
        {
            MaterialDrawing.DrawFocusRing(ref context, bounds, corner, colors.Secondary);
        }
    }

    /// <inheritdoc/>
    /// <remarks>The label color of the button's variant and state; disabled labels are reported as on-surface.</remarks>
    public bool TryGetContentColor(UIElement element, out Color color)
    {
        if (element is not Button button)
        {
            color = default;
            return false;
        }

        color = button is TitleBarButton caption ? GetCaptionContentColor(caption)
            : button.IsEnabled ? GetContentColor(button)
            : colors.OnSurface;
        return true;
    }

    private Color GetContainerColor(Button button)
    {
        if (MaterialDrawing.IsSet(button, Control.BackgroundProperty))
        {
            return button.Background;
        }

        return button.Variant switch
        {
            ButtonVariant.Filled => colors.Primary,
            ButtonVariant.Tonal => colors.SecondaryContainer,
            ButtonVariant.Elevated => colors.SurfaceContainerLow,
            _ => Color.Transparent, // outlined, text
        };
    }

    private Color DisabledContainerColor(ButtonVariant variant) =>
        variant is ButtonVariant.Filled or ButtonVariant.Tonal or ButtonVariant.Elevated
            ? MaterialDrawing.DisabledContainer(colors)
            : Color.Transparent;

    private Color GetContentColor(Button button)
    {
        if (MaterialDrawing.IsSet(button, Control.ForegroundProperty))
        {
            return button.Foreground;
        }

        return button.Variant switch
        {
            ButtonVariant.Filled => colors.OnPrimary,
            ButtonVariant.Tonal => colors.OnSecondaryContainer,
            _ => colors.Primary, // elevated, outlined, text
        };
    }

    // MD3: filled and tonal rest at level 0 and rise to level 1 on hover; elevated rests at its elevation (level 1 by
    // default) and rises to level 2 on hover. Pressing returns to the resting level.
    private static float GetElevation(Button button)
    {
        bool hover = button.IsHovered && !button.IsPressed;
        return button.Variant switch
        {
            ButtonVariant.Elevated => hover ? Math.Max(button.Elevation, MaterialElevation.Level2) : Math.Max(button.Elevation, MaterialElevation.Level1),
            ButtonVariant.Filled or ButtonVariant.Tonal => hover ? MaterialElevation.Level1 : 0f,
            _ => 0f,
        };
    }

    private void RenderCaptionButton(TitleBarButton button, in Rect bounds, ref DrawingContext context)
    {
        Color background = Color.Transparent;
        if (button.IsEnabled)
        {
            if (button.IsCloseButton)
            {
                background = button.IsPressed ? ClosePressedColor : button.IsHovered ? CloseHoverColor : Color.Transparent;
            }
            else
            {
                background = colors.OnSurface.WithOpacity(button.IsPressed ? MaterialState.PressedOpacity : button.IsHovered ? MaterialState.HoverOpacity : 0f);
            }
        }

        context.DrawRect(bounds, background);
    }

    private Color GetCaptionContentColor(TitleBarButton button) =>
        button.IsCloseButton && button.IsEnabled && (button.IsHovered || button.IsPressed) ? Color.White : colors.OnSurface;
}
