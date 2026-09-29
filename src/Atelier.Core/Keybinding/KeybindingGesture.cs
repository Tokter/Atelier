using System;
using System.Collections.Generic;
using System.Text;
using Atelier.Core.Events;

namespace Atelier.Core.Keybinding;

/// <summary>A pointer action a keybinding can be bound to, like a keyboard key (see <see cref="KeybindingGesture.Pointer"/>).</summary>
public enum PointerGesture
{
    /// <summary>No pointer action: the gesture is a key.</summary>
    None,

    /// <summary>A left-button click: pressed and released without moving.</summary>
    LeftClick,

    /// <summary>A right-button click.</summary>
    RightClick,

    /// <summary>A middle-button click.</summary>
    MiddleClick,

    /// <summary>A left-button double click.</summary>
    DoubleClick,

    /// <summary>Dragging with the left button held; runs an <see cref="IDragCommand"/>.</summary>
    LeftDrag,

    /// <summary>Dragging with the right button held.</summary>
    RightDrag,

    /// <summary>Dragging with the middle button held.</summary>
    MiddleDrag,

    /// <summary>Turning the wheel up (away from the user), per notch.</summary>
    WheelUp,

    /// <summary>Turning the wheel down, per notch.</summary>
    WheelDown,
}

/// <summary>
/// Represents a gesture that triggers a keybinding: a key or a pointer action (a click, a drag or a wheel turn) with the
/// modifier keys held, e.g. <c>"Ctrl+S"</c>, <c>"Ctrl+Shift+L"</c>, <c>"Ctrl+WheelUp"</c> or <c>"Shift+RightDrag"</c>.
/// Supports order-independent parsing and comparison of modifier keys.
/// </summary>
public readonly struct KeybindingGesture : IEquatable<KeybindingGesture>
{
    private static readonly char[] GestureDelimiters = new[] { '+', '-', ',', '|', ';', ' ' };

    /// <summary>Gets the non-modifier key of the gesture; <see cref="Key.None"/> for a pointer gesture.</summary>
    public Key Key { get; }

    /// <summary>Gets the pointer action of the gesture; <see cref="PointerGesture.None"/> for a key.</summary>
    public PointerGesture Pointer { get; }

    /// <summary>Gets the modifier keys that must be held.</summary>
    public ModifierKeys Modifiers { get; }

    /// <summary>Gets whether the gesture is a pointer action rather than a key.</summary>
    public bool IsPointer => Pointer != PointerGesture.None;

    /// <summary>
    /// Creates a gesture from a key and modifiers.
    /// </summary>
    public KeybindingGesture(Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        Key = key;
        Modifiers = modifiers;
    }

    /// <summary>
    /// Creates a pointer gesture from a pointer action and modifiers.
    /// </summary>
    public KeybindingGesture(PointerGesture pointer, ModifierKeys modifiers = ModifierKeys.None)
    {
        Pointer = pointer;
        Modifiers = modifiers;
    }

    /// <summary>Determines whether the key and the exact set of modifiers match this gesture.</summary>
    public bool Matches(Key key, ModifierKeys modifiers)
    {
        return !IsPointer && Key == key && Modifiers == modifiers;
    }

    /// <summary>Determines whether a key event matches this gesture.</summary>
    public bool Matches(KeyEventArgs e)
    {
        if (e == null) return false;
        return Matches(e.Key, e.Modifiers);
    }

    /// <summary>Determines whether two gestures are the same shortcut.</summary>
    public bool Matches(KeybindingGesture other) => Equals(other);

    /// <summary>Determines whether a gesture string (e.g. <c>"Shift+Ctrl+L"</c>) is the same shortcut; invalid strings never match.</summary>
    public bool Matches(string? gestureString)
    {
        if (TryParse(gestureString, out var other))
        {
            return Matches(other);
        }
        return false;
    }

    /// <summary>
    /// Compares two gesture strings to check if they represent the same shortcut,
    /// regardless of modifier ordering, casing, or delimiter style (e.g. "Ctrl+Shift+L" == "Shift+Ctrl+L").
    /// </summary>
    public static bool Matches(string? gestureA, string? gestureB)
    {
        if (string.IsNullOrWhiteSpace(gestureA) || string.IsNullOrWhiteSpace(gestureB))
            return false;

        if (TryParse(gestureA, out var ga) && TryParse(gestureB, out var gb))
        {
            return ga.Equals(gb);
        }

        return false;
    }

    /// <summary>
    /// Checks if two gesture strings represent the same shortcut regardless of modifier order.
    /// </summary>
    public static bool AreEquivalent(string? gestureA, string? gestureB) => Matches(gestureA, gestureB);

    /// <summary>
    /// Normalizes a gesture or chord string to canonical format: <c>"Ctrl+Alt+Shift+Win+Key"</c> per stroke, strokes
    /// separated by <c>", "</c> (e.g. <c>"Ctrl+K, Ctrl+C"</c>). Invalid strings are returned unchanged.
    /// </summary>
    public static string Normalize(string? gestureString)
    {
        if (TryParseSequence(gestureString, out var strokes))
        {
            return FormatSequence(strokes);
        }
        return gestureString ?? string.Empty;
    }

    /// <summary>
    /// Parses a single-stroke gesture or a multi-stroke chord.
    /// </summary>
    /// <remarks>
    /// A chord is written as complete strokes separated by commas, e.g. <c>"Ctrl+K, Ctrl+C"</c>: every comma-separated
    /// part must itself be a valid gesture with a non-modifier key. Otherwise the whole text is parsed as a single gesture,
    /// so commas used as token separators (<c>"Shift, Control+L"</c>) keep working. Pointer gestures can't be part of a
    /// chord.
    /// </remarks>
    /// <param name="text">The gesture or chord string.</param>
    /// <param name="strokes">The parsed strokes (one for a single gesture), or empty on failure.</param>
    /// <returns><c>true</c> if the string is a valid gesture or chord.</returns>
    public static bool TryParseSequence(string? text, out KeybindingGesture[] strokes)
    {
        strokes = Array.Empty<KeybindingGesture>();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (text.Contains(','))
        {
            var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 2)
            {
                var chord = new KeybindingGesture[parts.Length];
                bool allComplete = true;
                for (int i = 0; i < parts.Length && allComplete; i++)
                {
                    allComplete = TryParseStroke(parts[i], out chord[i]) && !chord[i].IsPointer;
                }

                if (allComplete)
                {
                    strokes = chord;
                    return true;
                }
            }
        }

        if (TryParseStroke(text, out var single))
        {
            strokes = [single];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Formats strokes in canonical form, separated by <c>", "</c>.
    /// </summary>
    public static string FormatSequence(IReadOnlyList<KeybindingGesture> strokes)
    {
        if (strokes.Count == 1)
        {
            return strokes[0].ToString();
        }

        var parts = new string[strokes.Count];
        for (int i = 0; i < strokes.Count; i++)
        {
            parts[i] = strokes[i].ToString();
        }
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Parses a gesture such as <c>"Ctrl+Shift+L"</c> or <c>"Ctrl+WheelUp"</c>. Modifiers may appear in any order and any
    /// case; <c>+</c>, <c>-</c>, <c>,</c>, <c>|</c>, <c>;</c> and spaces separate tokens. Common aliases (<c>Control</c>,
    /// <c>Cmd</c>, <c>Esc</c>, <c>Del</c>, <c>Return</c>) are accepted. Exactly one non-modifier key or one pointer action
    /// (a <see cref="PointerGesture"/> name; <c>"Click"</c> and <c>"Drag"</c> mean the left button) is required; if
    /// several keys are given, the last one wins.
    /// </summary>
    /// <remarks>Returns <c>false</c> for a multi-stroke chord; use <see cref="TryParseSequence"/> for those.</remarks>
    /// <param name="text">The gesture string.</param>
    /// <param name="gesture">The parsed gesture, or <c>default</c> on failure.</param>
    /// <returns><c>true</c> if the string is a valid single-stroke gesture.</returns>
    public static bool TryParse(string? text, out KeybindingGesture gesture)
    {
        if (TryParseSequence(text, out var strokes) && strokes.Length == 1)
        {
            gesture = strokes[0];
            return true;
        }

        gesture = default;
        return false;
    }

    private static bool TryParseStroke(string? text, out KeybindingGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var parts = text!.Split(GestureDelimiters, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        var modifiers = ModifierKeys.None;
        var key = Key.None;
        var pointer = PointerGesture.None;

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i].Trim();
            if (string.IsNullOrEmpty(part))
                continue;

            if (TryMapModifier(part, out var mod))
            {
                modifiers |= mod;
                continue;
            }

            if (TryMapPointer(part, out var parsedPointer))
            {
                if (pointer != PointerGesture.None)
                    return false; // one pointer action per stroke
                pointer = parsedPointer;
                continue;
            }

            if (TryMapKey(part, out var parsedKey))
            {
                key = parsedKey;
                continue;
            }

            return false;
        }

        // Exactly one of a key and a pointer action.
        if ((key == Key.None) == (pointer == PointerGesture.None))
            return false;

        gesture = pointer != PointerGesture.None ? new KeybindingGesture(pointer, modifiers) : new KeybindingGesture(key, modifiers);
        return true;
    }

    /// <summary>
    /// Parses a gesture string; see <see cref="TryParse"/> for the accepted format.
    /// </summary>
    /// <exception cref="FormatException">The string is not a valid gesture.</exception>
    public static KeybindingGesture Parse(string text)
    {
        if (!TryParse(text, out var gesture))
            throw new FormatException($"Invalid keybinding gesture string: '{text}'.");
        return gesture;
    }

    /// <summary>Gets the click gesture of <paramref name="button"/> (left, right or middle), or <see cref="PointerGesture.None"/>.</summary>
    public static PointerGesture ClickOf(PointerButtons button) => button switch
    {
        PointerButtons.Left => PointerGesture.LeftClick,
        PointerButtons.Right => PointerGesture.RightClick,
        PointerButtons.Middle => PointerGesture.MiddleClick,
        _ => PointerGesture.None,
    };

    /// <summary>Gets the drag gesture of <paramref name="button"/> (left, right or middle), or <see cref="PointerGesture.None"/>.</summary>
    public static PointerGesture DragOf(PointerButtons button) => button switch
    {
        PointerButtons.Left => PointerGesture.LeftDrag,
        PointerButtons.Right => PointerGesture.RightDrag,
        PointerButtons.Middle => PointerGesture.MiddleDrag,
        _ => PointerGesture.None,
    };

    private static bool TryMapModifier(string token, out ModifierKeys mod)
    {
        if (token.Equals("ctrl", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("control", StringComparison.OrdinalIgnoreCase))
        {
            mod = ModifierKeys.Control;
            return true;
        }
        if (token.Equals("shift", StringComparison.OrdinalIgnoreCase))
        {
            mod = ModifierKeys.Shift;
            return true;
        }
        if (token.Equals("alt", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("opt", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("option", StringComparison.OrdinalIgnoreCase))
        {
            mod = ModifierKeys.Alt;
            return true;
        }
        if (token.Equals("win", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("windows", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("cmd", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("command", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("meta", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("super", StringComparison.OrdinalIgnoreCase))
        {
            mod = ModifierKeys.Windows;
            return true;
        }
        mod = ModifierKeys.None;
        return false;
    }

    private static bool TryMapPointer(string token, out PointerGesture pointer)
    {
        if (token.Equals("click", StringComparison.OrdinalIgnoreCase))
        {
            pointer = PointerGesture.LeftClick;
            return true;
        }
        if (token.Equals("drag", StringComparison.OrdinalIgnoreCase))
        {
            pointer = PointerGesture.LeftDrag;
            return true;
        }
        if (token.Equals("leftdoubleclick", StringComparison.OrdinalIgnoreCase))
        {
            pointer = PointerGesture.DoubleClick;
            return true;
        }

        // Only names: Enum.TryParse would also accept numbers.
        pointer = PointerGesture.None;
        return char.IsLetter(token[0]) && Enum.TryParse(token, true, out pointer) && pointer != PointerGesture.None && Enum.IsDefined(pointer);
    }

    private static bool TryMapKey(string token, out Key key)
    {
        if (token.Equals("esc", StringComparison.OrdinalIgnoreCase))
        {
            key = Key.Escape;
            return true;
        }
        if (token.Equals("del", StringComparison.OrdinalIgnoreCase))
        {
            key = Key.Delete;
            return true;
        }
        if (token.Equals("return", StringComparison.OrdinalIgnoreCase))
        {
            key = Key.Enter;
            return true;
        }
        if (token.Equals("space", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("spacebar", StringComparison.OrdinalIgnoreCase))
        {
            key = Key.Space;
            return true;
        }
        if (token.Length == 1 && token[0] >= '0' && token[0] <= '9')
        {
            key = (Key)((int)Key.D0 + (token[0] - '0'));
            return true;
        }

        // Enum.TryParse also accepts numeric strings ("42" would become (Key)42), so only accept key names.
        if (char.IsDigit(token[0]) || token[0] == '-' || token[0] == '+')
        {
            key = Key.None;
            return false;
        }

        return Enum.TryParse(token, true, out key) && key != Key.None && Enum.IsDefined(key);
    }

    /// <summary>
    /// Formats the gesture for display, e.g. in a menu: like <see cref="ToString"/> but with keys as they are labeled,
    /// such as <c>"Ctrl+1"</c> instead of <c>"Ctrl+D1"</c>, <c>"Ctrl+,"</c> instead of <c>"Ctrl+Comma"</c> and
    /// <c>"Ctrl+Wheel up"</c> instead of <c>"Ctrl+WheelUp"</c>.
    /// </summary>
    public string ToDisplayString()
    {
        var sb = new StringBuilder();
        if (Modifiers.HasFlag(ModifierKeys.Control)) sb.Append("Ctrl+");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) sb.Append("Alt+");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) sb.Append("Shift+");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) sb.Append("Win+");
        sb.Append(MainDisplayName());
        return sb.ToString();
    }

    /// <summary>
    /// Gets the keys of the gesture as they are labeled, modifiers first, e.g. <c>["Ctrl", "Shift", "T"]</c> or
    /// <c>["Shift", "Right drag"]</c>, for showing each key separately (as key caps).
    /// </summary>
    public IReadOnlyList<string> GetDisplayParts()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(MainDisplayName());
        return parts;
    }

    /// <summary>Formats strokes for display, separated by <c>", "</c> (e.g. <c>"Ctrl+K, Ctrl+C"</c>).</summary>
    public static string FormatSequenceForDisplay(IReadOnlyList<KeybindingGesture> strokes)
    {
        if (strokes.Count == 1) return strokes[0].ToDisplayString();
        var parts = new string[strokes.Count];
        for (int i = 0; i < strokes.Count; i++) parts[i] = strokes[i].ToDisplayString();
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Formats a gesture or chord string for display (see <see cref="ToDisplayString"/>), or returns it unchanged when it
    /// doesn't parse.
    /// </summary>
    public static string FormatForDisplay(string? gestureString) =>
        TryParseSequence(gestureString, out var strokes) ? FormatSequenceForDisplay(strokes) : gestureString ?? string.Empty;

    private string MainDisplayName() => IsPointer ? PointerDisplayName(Pointer) : KeyDisplayName(Key);

    private static string PointerDisplayName(PointerGesture pointer) => pointer switch
    {
        PointerGesture.LeftClick => "Click",
        PointerGesture.RightClick => "Right click",
        PointerGesture.MiddleClick => "Middle click",
        PointerGesture.DoubleClick => "Double click",
        PointerGesture.LeftDrag => "Drag",
        PointerGesture.RightDrag => "Right drag",
        PointerGesture.MiddleDrag => "Middle drag",
        PointerGesture.WheelUp => "Wheel up",
        PointerGesture.WheelDown => "Wheel down",
        _ => pointer.ToString(),
    };

    private static string KeyDisplayName(Key key) => key switch
    {
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => "Num " + (char)('0' + (key - Key.NumPad0)),
        Key.Minus => "-",
        Key.Equal => "=",
        Key.Comma => ",",
        Key.Period => ".",
        Key.Slash => "/",
        Key.Semicolon => ";",
        Key.Apostrophe => "'",
        Key.LeftBracket => "[",
        Key.RightBracket => "]",
        Key.Backslash => "\\",
        Key.GraveAccent => "`",
        Key.NumPadAdd => "Num +",
        Key.NumPadSubtract => "Num -",
        Key.NumPadMultiply => "Num *",
        Key.NumPadDivide => "Num /",
        Key.Delete => "Del",
        Key.Insert => "Ins",
        Key.Escape => "Esc",
        Key.PageUp => "PgUp",
        Key.PageDown => "PgDn",
        _ => key.ToString(),
    };

    /// <summary>Formats the gesture in canonical form, e.g. <c>"Ctrl+Alt+Shift+Win+K"</c> or <c>"Ctrl+WheelUp"</c>.</summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        if (Modifiers.HasFlag(ModifierKeys.Control)) sb.Append("Ctrl+");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) sb.Append("Alt+");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) sb.Append("Shift+");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) sb.Append("Win+");
        if (IsPointer) sb.Append(Pointer);
        else sb.Append(Key);
        return sb.ToString();
    }

    /// <inheritdoc/>
    public bool Equals(KeybindingGesture other) => Key == other.Key && Pointer == other.Pointer && Modifiers == other.Modifiers;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is KeybindingGesture other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Key, Pointer, Modifiers);

    /// <summary>Determines whether two gestures are the same shortcut.</summary>
    public static bool operator ==(KeybindingGesture left, KeybindingGesture right) => left.Equals(right);

    /// <summary>Determines whether two gestures differ.</summary>
    public static bool operator !=(KeybindingGesture left, KeybindingGesture right) => !left.Equals(right);
}
