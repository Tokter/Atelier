namespace Atelier.Core.Primitives;

/// <summary>
/// Specifies where a popup is positioned relative to its placement target.
/// </summary>
/// <remarks>
/// Except for <see cref="Center"/>, a popup that does not fit on the preferred side is flipped to the opposite side
/// (for the vertical modes, only when that side has more room), and the result is then clamped to the viewport.
/// </remarks>
public enum PlacementMode
{
    /// <summary>Below the target, left-aligned; same as <see cref="BottomLeft"/>.</summary>
    Bottom = 0,
    /// <summary>Above the target, left-aligned.</summary>
    Top = 1,
    /// <summary>To the left of the target, top-aligned.</summary>
    Left = 2,
    /// <summary>To the right of the target, top-aligned.</summary>
    Right = 3,
    /// <summary>Below the target, left edges aligned.</summary>
    BottomLeft = 4,
    /// <summary>Below the target, right edges aligned.</summary>
    BottomRight = 5,
    /// <summary>Centered in the viewport, ignoring the target's position.</summary>
    Center = 6
}
