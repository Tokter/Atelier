namespace Atelier.Core.Primitives;

/// <summary>
/// The mouse cursor shown over an element (see <see cref="Tree.UIElement.Cursor"/>).
/// </summary>
public enum CursorType
{
    /// <summary>No cursor of its own: the element shows its parent's cursor (the arrow at the root).</summary>
    Default = 0,
    /// <summary>The standard arrow.</summary>
    Arrow,
    /// <summary>The text cursor, for editable or selectable text.</summary>
    IBeam,
    /// <summary>The pointing hand, for links.</summary>
    Hand,
    /// <summary>Crosshairs, for precise selection.</summary>
    Crosshair,
    /// <summary>A double arrow pointing left and right, for resizing horizontally (e.g. columns).</summary>
    SizeWestEast,
    /// <summary>A double arrow pointing up and down, for resizing vertically (e.g. rows).</summary>
    SizeNorthSouth,
    /// <summary>A diagonal double arrow from top-left to bottom-right.</summary>
    SizeNorthwestSoutheast,
    /// <summary>A diagonal double arrow from top-right to bottom-left.</summary>
    SizeNortheastSouthwest,
    /// <summary>Four arrows, for moving in any direction.</summary>
    SizeAll,
    /// <summary>A slashed circle: the action isn't possible here.</summary>
    NotAllowed,
    /// <summary>The busy cursor.</summary>
    Wait,
    /// <summary>The arrow with a busy indicator: still usable while working in the background.</summary>
    AppStarting,
}
