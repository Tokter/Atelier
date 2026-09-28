using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Theming.Material.Renderers;

/// <summary>
/// Draws a <see cref="CalendarDayButton"/> as an MD3 date: the selected date in a primary circle with on-primary text,
/// today with a 1 px primary outline and primary text, other dates as on-surface text.
/// </summary>
/// <remarks>Hover, focus and press add a state layer in the content color; keyboard focus draws the focus ring.</remarks>
/// <param name="colors">The color scheme.</param>
public class MaterialCalendarDayRenderer(MaterialColorScheme colors) : ControlRenderer<CalendarDayButton>, IContentColorProvider
{
    /// <inheritdoc/>
    public override void Render(CalendarDayButton day, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, day.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var center = new Point(bounds.Width * 0.5f, bounds.Height * 0.5f);
        float radius = Math.Min(bounds.Width, bounds.Height) * 0.5f;

        if (day.IsSelected)
        {
            context.DrawCircle(center, radius, day.IsEnabled ? colors.Primary : MaterialDrawing.DisabledContainer(colors));
        }
        else if (day.IsToday)
        {
            context.DrawCircleOutline(center, radius, day.IsEnabled ? colors.Primary : MaterialDrawing.DisabledContent(colors), 1f);
        }

        MaterialDrawing.DrawStateLayerCircle(ref context, center, GetContentColor(day), MaterialDrawing.StateLayerOpacity(day), radius);
        if (day.IsFocusVisible)
        {
            MaterialDrawing.DrawFocusRingCircle(ref context, center, radius, colors.Secondary);
        }
    }

    private Color GetContentColor(CalendarDayButton day) =>
        !day.IsEnabled ? MaterialDrawing.DisabledContent(colors)
        : day.IsSelected ? colors.OnPrimary
        : day.IsToday ? colors.Primary
        : colors.OnSurface;

    /// <inheritdoc/>
    public bool TryGetContentColor(UIElement element, out Color color)
    {
        color = GetContentColor((CalendarDayButton)element);
        return true;
    }
}

/// <summary>
/// Draws a <see cref="CalendarYearButton"/> as an MD3 year pill: the selected year filled with primary, the current year
/// outlined in primary, other years as on-surface-variant text.
/// </summary>
/// <param name="colors">The color scheme.</param>
public class MaterialCalendarYearRenderer(MaterialColorScheme colors) : ControlRenderer<CalendarYearButton>, IContentColorProvider
{
    /// <inheritdoc/>
    public override void Render(CalendarYearButton year, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, year.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var corner = new CornerRadius(bounds.Height * 0.5f);
        if (year.IsSelected)
        {
            context.DrawRoundedRect(bounds, corner, colors.Primary);
        }
        else if (year.IsCurrent)
        {
            context.DrawRoundedRectOutline(bounds, corner, colors.Primary, 1f);
        }

        MaterialDrawing.DrawStateLayer(ref context, bounds, corner, GetContentColor(year), MaterialDrawing.StateLayerOpacity(year));
        if (year.IsFocusVisible)
        {
            MaterialDrawing.DrawFocusRing(ref context, bounds, corner, colors.Secondary);
        }
    }

    private Color GetContentColor(CalendarYearButton year) =>
        !year.IsEnabled ? MaterialDrawing.DisabledContent(colors)
        : year.IsSelected ? colors.OnPrimary
        : year.IsCurrent ? colors.Primary
        : colors.OnSurfaceVariant;

    /// <inheritdoc/>
    public bool TryGetContentColor(UIElement element, out Color color)
    {
        color = GetContentColor((CalendarYearButton)element);
        return true;
    }
}

/// <summary>
/// Draws a <see cref="CalendarMenuItem"/> as an MD3 menu item: on-surface text with a state layer, the selected entry on
/// a surface-container-highest background.
/// </summary>
/// <param name="colors">The color scheme.</param>
public class MaterialCalendarMenuItemRenderer(MaterialColorScheme colors) : ControlRenderer<CalendarMenuItem>, IContentColorProvider
{
    /// <inheritdoc/>
    public override void Render(CalendarMenuItem item, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, item.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        if (item.IsSelected)
        {
            context.DrawRect(bounds, colors.SurfaceContainerHighest);
        }
        MaterialDrawing.DrawStateLayer(ref context, bounds, CornerRadius.Zero, colors.OnSurface, MaterialDrawing.StateLayerOpacity(item));
        if (item.IsFocusVisible)
        {
            MaterialDrawing.DrawFocusRing(ref context, bounds, CornerRadius.Zero, colors.Secondary, offset: -2f);
        }
    }

    /// <inheritdoc/>
    public bool TryGetContentColor(UIElement element, out Color color)
    {
        color = element.IsEnabled ? colors.OnSurface : MaterialDrawing.DisabledContent(colors);
        return true;
    }
}

/// <summary>
/// Draws a <see cref="ClockDial"/> as the MD3 time picker dial: a surface-container-highest face with on-surface
/// numbers, a primary handle (a 48 px circle on a 2 px track from an 8 px center dot), and on-primary numbers inside the
/// handle. Between two minute labels the handle shows a small on-primary dot.
/// </summary>
/// <param name="colors">The color scheme.</param>
public class MaterialClockDialRenderer(MaterialColorScheme colors) : ControlRenderer<ClockDial>
{
    private const float LabelSize = 16f;       // body-large
    private const float InnerLabelSize = 14f;  // the inner ring of a 24-hour dial is tighter

    /// <inheritdoc/>
    public override void Render(ClockDial dial, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, dial.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        int layer = dial.IsEnabled ? -1 : context.SaveOpacityLayer(MaterialState.DisabledContentOpacity);
        var center = dial.Center;
        context.DrawCircle(center, Math.Min(bounds.Width, bounds.Height) * 0.5f, colors.SurfaceContainerHighest);

        DrawLabels(dial, ref context, colors.OnSurface);

        var handle = dial.HandleCenter;
        context.DrawLine(center, handle, colors.Primary, 2f);
        context.DrawCircle(center, 4f, colors.Primary);
        context.DrawCircle(handle, ClockDial.HandleRadius, colors.Primary);

        // The numbers under the handle, in on-primary.
        var handleRect = new Rect(handle.X - ClockDial.HandleRadius, handle.Y - ClockDial.HandleRadius, ClockDial.HandleRadius * 2, ClockDial.HandleRadius * 2);
        using (context.PushRoundedClip(handleRect, new CornerRadius(ClockDial.HandleRadius)))
        {
            DrawLabels(dial, ref context, colors.OnPrimary);
        }

        if (dial.IsHandleBetweenLabels)
        {
            context.DrawCircle(handle, 2f, colors.OnPrimary);
        }

        if (dial.IsFocusVisible)
        {
            MaterialDrawing.DrawFocusRingCircle(ref context, handle, ClockDial.HandleRadius, colors.Secondary);
        }

        if (layer >= 0) context.Canvas.RestoreToCount(layer);
    }

    private static void DrawLabels(ClockDial dial, ref DrawingContext context, Color color)
    {
        foreach (var label in dial.Labels)
        {
            float size = label.IsInner ? InnerLabelSize : LabelSize;
            var position = dial.GetPosition(label.Angle, label.IsInner);
            float width = context.MeasureText(label.Text, size).Width;
            context.DrawText(label.Text, new Point(position.X - width * 0.5f, position.Y + size * 0.36f), color, size);
        }
    }
}

/// <summary>
/// Draws a <see cref="TimePickerSegment"/>: as a selector, a primary-container box with on-primary-container digits when
/// selected and surface-container-highest with on-surface digits otherwise (display-large); as an input field,
/// surface-container-highest, or primary-container with a 2 px primary outline while focused (display-medium).
/// </summary>
/// <param name="colors">The color scheme.</param>
public class MaterialTimePickerSegmentRenderer(MaterialColorScheme colors) : ControlRenderer<TimePickerSegment>
{
    /// <inheritdoc/>
    public override void Render(TimePickerSegment segment, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, segment.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var corner = segment.CornerRadius;
        bool highlighted = segment.IsEditable ? segment.IsFocused : segment.IsSelected;
        Color container = highlighted ? colors.PrimaryContainer : colors.SurfaceContainerHighest;
        Color content = highlighted ? colors.OnPrimaryContainer : colors.OnSurface;
        if (!segment.IsEnabled)
        {
            container = MaterialDrawing.DisabledContainer(colors);
            content = MaterialDrawing.DisabledContent(colors);
        }

        context.DrawRoundedRect(bounds, corner, container);
        if (segment.IsEditable && segment.IsFocused)
        {
            context.DrawRoundedRectOutline(bounds, corner, colors.Primary, 2f);
        }
        else if (!segment.IsEditable)
        {
            float opacity = segment.IsPressed ? MaterialState.PressedOpacity : segment.IsHovered ? MaterialState.HoverOpacity : 0f;
            MaterialDrawing.DrawStateLayer(ref context, bounds, corner, content, opacity);
        }

        float size = segment.IsEditable ? 45f : 57f; // display-medium / display-large
        string text = segment.Text;
        float width = context.MeasureText(text, size).Width;
        float x = (bounds.Width - width) * 0.5f;
        float baseline = bounds.Height * 0.5f + size * 0.36f;
        context.DrawText(text, new Point(x, baseline), content, size);

        if (segment.ShowsCaret)
        {
            context.DrawPixelRect(new Rect(x + width + 1f, baseline - size * 0.74f, 2f, size * 0.84f), colors.Primary);
        }

        if (segment.IsFocusVisible && !segment.IsEditable)
        {
            MaterialDrawing.DrawFocusRing(ref context, bounds, corner, colors.Secondary);
        }
    }
}

/// <summary>
/// Draws a <see cref="TimePeriodSelector"/> as the MD3 period selector: a 1 px outline split into AM and PM, the selected
/// segment in tertiary-container with on-tertiary-container text, the other in on-surface-variant text (title-medium).
/// </summary>
/// <param name="colors">The color scheme.</param>
public class MaterialTimePeriodSelectorRenderer(MaterialColorScheme colors) : ControlRenderer<TimePeriodSelector>
{
    private const float LabelSize = 16f; // title-medium

    /// <inheritdoc/>
    public override void Render(TimePeriodSelector selector, ref DrawingContext context)
    {
        var bounds = new Rect(Point.Zero, selector.Bounds.Size);
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var corner = selector.CornerRadius;
        bool vertical = selector.Orientation == Atelier.Layout.Orientation.Vertical;
        int selected = selector.IsPm ? 1 : 0;
        bool enabled = selector.IsEnabled;

        using (context.PushRoundedClip(bounds, corner))
        {
            if (enabled)
            {
                context.DrawRect(selector.GetSegmentBounds(selected), colors.TertiaryContainer);
            }

            int hovered = selector.HoveredSegment;
            if (enabled && hovered >= 0)
            {
                Color layer = hovered == selected ? colors.OnTertiaryContainer : colors.OnSurfaceVariant;
                float opacity = selector.IsPressed ? MaterialState.PressedOpacity : MaterialState.HoverOpacity;
                context.DrawRect(selector.GetSegmentBounds(hovered), layer.WithOpacity(opacity));
            }
        }

        Color outline = enabled ? colors.Outline : MaterialDrawing.DisabledContainer(colors);
        context.DrawRoundedRectOutline(bounds, corner, outline, 1f);
        var second = selector.GetSegmentBounds(1);
        if (vertical)
        {
            context.DrawLine(new Point(0, second.Top), new Point(bounds.Width, second.Top), outline, 1f);
        }
        else
        {
            context.DrawLine(new Point(second.Left, 0), new Point(second.Left, bounds.Height), outline, 1f);
        }

        for (int i = 0; i < 2; i++)
        {
            var segment = selector.GetSegmentBounds(i);
            string text = i == 0 ? selector.AmText : selector.PmText;
            Color color = !enabled ? MaterialDrawing.DisabledContent(colors) : i == selected ? colors.OnTertiaryContainer : colors.OnSurfaceVariant;
            var size = context.MeasureText(text, LabelSize, null, FontWeight.Medium);
            float x = segment.Left + (segment.Width - size.Width) * 0.5f;
            float y = segment.Top + segment.Height * 0.5f + LabelSize * 0.36f;
            context.DrawText(text, new Point(x, y), color, LabelSize, null, FontWeight.Medium);
        }

        if (selector.IsFocusVisible)
        {
            MaterialDrawing.DrawFocusRing(ref context, bounds, corner, colors.Secondary);
        }
    }
}
