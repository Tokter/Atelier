using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a Material Design 3 <see cref="Switch"/>: a track of <see cref="Switch.TrackWidth"/>×<see cref="Switch.TrackHeight"/>
/// (52×32 in MD3, 40×24 with desktop sizing) and a handle that grows from half the track height (three quarters with an
/// icon) when off to three quarters when on, and to seven eighths while pressed — 16/24/28 px on the MD3 track.
/// </summary>
/// <remarks>
/// Off: surface-container-highest track with a 2 px outline border and an outline handle. On: primary track and an
/// on-primary handle. With <see cref="Switch.ShowThumbIcon"/> the handle shows a check (on) or a close mark (off).
/// Hover and press show the circular state layer around the handle (40 px, smaller in dense sizing); keyboard focus draws
/// the focus ring around the track. Disabled: on-surface at 12% (track) and 38% (handle), surface handle when on.
/// </remarks>
/// <param name="colors">The color scheme.</param>
/// <param name="sizing">The theme sizing (the state layer shrinks with the density); <c>null</c> for desktop sizing.</param>
public class MaterialSwitchRenderer(MaterialColorScheme colors, MaterialSizing? sizing = null) : ControlRenderer<Switch>
{
    private const float OutlineWidth = 2f;
    private readonly float _stateLayerRadius = (sizing ?? MaterialSizing.Desktop).StateLayerSize * 0.5f;

    /// <inheritdoc/>
    public override void Render(Switch toggle, ref DrawingContext context)
    {
        var pad = toggle.Padding;
        float trackWidth = toggle.TrackWidth;
        float trackHeight = toggle.TrackHeight;
        float trackX = pad.Left;
        float trackY = MathF.Round(pad.Top + (toggle.Bounds.Height - pad.Vertical - trackHeight) * 0.5f);
        var track = new Rect(trackX, trackY, trackWidth, trackHeight);
        var trackCorner = new CornerRadius(trackHeight * 0.5f);

        float progress = toggle.ThumbAnimationProgress; // 0 off .. 1 on
        bool enabled = toggle.IsEnabled;
        bool on = progress >= 0.5f;

        // Track.
        if (enabled)
        {
            context.DrawRoundedRect(track, trackCorner, Color.Lerp(colors.SurfaceContainerHighest, colors.Primary, progress));
            if (progress < 1f)
            {
                context.DrawRoundedRectOutline(track, trackCorner, colors.Outline.WithOpacity(1f - progress), OutlineWidth);
            }
        }
        else if (on)
        {
            context.DrawRoundedRect(track, trackCorner, MaterialDrawing.DisabledContainer(colors));
        }
        else
        {
            context.DrawRoundedRect(track, trackCorner, colors.SurfaceContainerHighest.WithOpacity(MaterialState.DisabledContainerOpacity));
            context.DrawRoundedRectOutline(track, trackCorner, MaterialDrawing.DisabledContainer(colors), OutlineWidth);
        }

        // Handle size (proportional to the track height) and position: its center moves from half the track height
        // from the left end to the same distance from the right end.
        float onDiameter = trackHeight * 0.75f;
        float offDiameter = toggle.ShowThumbIcon ? onDiameter : trackHeight * 0.5f;
        float diameter = toggle.IsPressed && enabled ? trackHeight * 0.875f : offDiameter + (onDiameter - offDiameter) * progress;
        float half = trackHeight * 0.5f;
        var handleCenter = new Point(trackX + half + (trackWidth - trackHeight) * progress, trackY + half);

        MaterialDrawing.DrawStateLayerCircle(ref context, handleCenter, on ? colors.Primary : colors.OnSurface,
            MaterialDrawing.HaloOpacity(toggle), _stateLayerRadius);

        Color handle;
        if (!enabled)
        {
            handle = on ? colors.Surface : MaterialDrawing.DisabledContent(colors);
        }
        else
        {
            bool active = toggle.IsHovered || toggle.IsPressed || toggle.IsFocusVisible;
            Color offHandle = active ? colors.OnSurfaceVariant : colors.Outline;
            Color onHandle = active ? colors.PrimaryContainer : colors.OnPrimary;
            handle = Color.Lerp(offHandle, onHandle, progress);
        }
        context.DrawCircle(handleCenter, diameter * 0.5f, handle);

        if (toggle.ShowThumbIcon)
        {
            DrawHandleIcon(ref context, handleCenter, trackHeight / 32f, on, enabled);
        }

        if (toggle.IsFocusVisible)
        {
            MaterialDrawing.DrawFocusRing(ref context, track, trackCorner, colors.Secondary);
        }
    }

    // The icon inside the handle (16 px on the MD3 track, scaled with it): a check when on (on-primary-container), a close
    // mark when off (surface-container-highest). Disabled icons use on-surface at 38%.
    private void DrawHandleIcon(ref DrawingContext context, in Point c, float scale, bool on, bool enabled)
    {
        float stroke = Math.Max(1.5f, 2f * scale);
        if (on)
        {
            Color color = enabled ? colors.OnPrimaryContainer : MaterialDrawing.DisabledContent(colors);
            MaterialDrawing.DrawPolyline(ref context,
                c.X - 4.5f * scale, c.Y + 0.5f * scale,
                c.X - 1.5f * scale, c.Y + 3.5f * scale,
                c.X + 4.5f * scale, c.Y - 3f * scale,
                color, stroke);
        }
        else
        {
            Color color = enabled ? colors.SurfaceContainerHighest : colors.SurfaceContainerHighest.WithOpacity(MaterialState.DisabledContentOpacity);
            float d = 3.5f * scale;
            context.DrawLine(new Point(c.X - d, c.Y - d), new Point(c.X + d, c.Y + d), color, stroke);
            context.DrawLine(new Point(c.X - d, c.Y + d), new Point(c.X + d, c.Y - d), color, stroke);
        }
    }
}
