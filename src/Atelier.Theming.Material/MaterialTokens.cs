using System;
using Atelier.Core.Primitives;

namespace Atelier.Theming.Material;

/// <summary>
/// Material Design 3 shape tokens: corner radii of the shape scale (md.sys.shape).
/// </summary>
public static class MaterialShape
{
    /// <summary>No rounding.</summary>
    public const float None = 0f;
    /// <summary>4 px: text fields, menus, tooltips.</summary>
    public const float ExtraSmall = 4f;
    /// <summary>8 px: chips, snackbars.</summary>
    public const float Small = 8f;
    /// <summary>12 px: cards.</summary>
    public const float Medium = 12f;
    /// <summary>16 px: navigation drawers, large FABs.</summary>
    public const float Large = 16f;
    /// <summary>28 px: dialogs, bottom sheets.</summary>
    public const float ExtraLarge = 28f;
    /// <summary>Fully rounded (pill or circle): buttons, switches, sliders. Drawing clamps it to half the shorter side.</summary>
    public const float Full = 9999f;
}

/// <summary>
/// Material Design 3 state tokens (md.sys.state): the opacities of the state layer drawn over a component in the
/// color of its content, and the opacities of disabled content and containers.
/// </summary>
public static class MaterialState
{
    /// <summary>Hovered state layer opacity.</summary>
    public const float HoverOpacity = 0.08f;
    /// <summary>Focused state layer opacity.</summary>
    public const float FocusOpacity = 0.12f;
    /// <summary>Pressed state layer opacity.</summary>
    public const float PressedOpacity = 0.12f;
    /// <summary>Dragged state layer opacity.</summary>
    public const float DraggedOpacity = 0.16f;
    /// <summary>Opacity of disabled content (text, icons): on-surface at 38%.</summary>
    public const float DisabledContentOpacity = 0.38f;
    /// <summary>Opacity of disabled containers and outlines: on-surface at 12%.</summary>
    public const float DisabledContainerOpacity = 0.12f;
}

/// <summary>
/// Material Design 3 elevation levels (md.sys.elevation) in dp, as passed to the shadow renderer.
/// </summary>
public static class MaterialElevation
{
    /// <summary>Level 0: no shadow.</summary>
    public const float Level0 = 0f;
    /// <summary>Level 1 (1 dp): elevated buttons and cards, slider handles.</summary>
    public const float Level1 = 1f;
    /// <summary>Level 2 (3 dp): menus, hovered elevated buttons.</summary>
    public const float Level2 = 3f;
    /// <summary>Level 3 (6 dp): dialogs, FABs.</summary>
    public const float Level3 = 6f;
    /// <summary>Level 4 (8 dp).</summary>
    public const float Level4 = 8f;
    /// <summary>Level 5 (12 dp).</summary>
    public const float Level5 = 12f;
}

/// <summary>
/// Material Design 3 focus indicator tokens (md.comp.focus-ring): a ring in the secondary color, drawn outside the
/// component with a gap and following its shape, shown for keyboard focus only.
/// </summary>
public static class MaterialFocusRing
{
    /// <summary>The ring thickness.</summary>
    public const float Width = 3f;
    /// <summary>The gap between the component's edge and the ring.</summary>
    public const float OuterOffset = 2f;
}

/// <summary>
/// A text style of the Material Design 3 type scale: size, line height (baseline-to-baseline) and weight.
/// </summary>
/// <param name="Size">The font size in pixels.</param>
/// <param name="LineHeight">The line height in pixels.</param>
/// <param name="Weight">The font weight.</param>
public readonly record struct MaterialTextStyle(float Size, float LineHeight, FontWeight Weight);

/// <summary>
/// The Material Design 3 type scale (md.sys.typescale), in pixels at 1 rem = 16 px.
/// </summary>
public static class MaterialTypescale
{
    /// <summary>57/64, regular.</summary>
    public static readonly MaterialTextStyle DisplayLarge = new(57f, 64f, FontWeight.Normal);
    /// <summary>45/52, regular.</summary>
    public static readonly MaterialTextStyle DisplayMedium = new(45f, 52f, FontWeight.Normal);
    /// <summary>36/44, regular.</summary>
    public static readonly MaterialTextStyle DisplaySmall = new(36f, 44f, FontWeight.Normal);
    /// <summary>32/40, regular.</summary>
    public static readonly MaterialTextStyle HeadlineLarge = new(32f, 40f, FontWeight.Normal);
    /// <summary>28/36, regular.</summary>
    public static readonly MaterialTextStyle HeadlineMedium = new(28f, 36f, FontWeight.Normal);
    /// <summary>24/32, regular.</summary>
    public static readonly MaterialTextStyle HeadlineSmall = new(24f, 32f, FontWeight.Normal);
    /// <summary>22/28, regular.</summary>
    public static readonly MaterialTextStyle TitleLarge = new(22f, 28f, FontWeight.Normal);
    /// <summary>16/24, medium.</summary>
    public static readonly MaterialTextStyle TitleMedium = new(16f, 24f, FontWeight.Medium);
    /// <summary>14/20, medium.</summary>
    public static readonly MaterialTextStyle TitleSmall = new(14f, 20f, FontWeight.Medium);
    /// <summary>16/24, regular.</summary>
    public static readonly MaterialTextStyle BodyLarge = new(16f, 24f, FontWeight.Normal);
    /// <summary>14/20, regular.</summary>
    public static readonly MaterialTextStyle BodyMedium = new(14f, 20f, FontWeight.Normal);
    /// <summary>12/16, regular.</summary>
    public static readonly MaterialTextStyle BodySmall = new(12f, 16f, FontWeight.Normal);
    /// <summary>14/20, medium: buttons, tabs.</summary>
    public static readonly MaterialTextStyle LabelLarge = new(14f, 20f, FontWeight.Medium);
    /// <summary>12/16, medium.</summary>
    public static readonly MaterialTextStyle LabelMedium = new(12f, 16f, FontWeight.Medium);
    /// <summary>11/16, medium.</summary>
    public static readonly MaterialTextStyle LabelSmall = new(11f, 16f, FontWeight.Medium);
}
