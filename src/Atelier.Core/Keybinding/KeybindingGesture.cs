using System;
using System.Text;
using Atelier.Core.Events;

namespace Atelier.Core.Keybinding;

/// <summary>
/// Represents a keyboard gesture consisting of a key and modifier keys (e.g. "Ctrl+S", "Ctrl+Shift+L").
/// Supports order-independent parsing and comparison of modifier keys.
/// </summary>
public readonly struct KeybindingGesture : IEquatable<KeybindingGesture>
{
    private static readonly char[] GestureDelimiters = new[] { '+', '-', ',', '|', ';', ' ' };

    /// <summary>Gets the non-modifier key of the gesture.</summary>
    public Key Key { get; }

    /// <summary>Gets the modifier keys that must be held.</summary>
    public ModifierKeys Modifiers { get; }

    /// <summary>
    /// Creates a gesture from a key and modifiers.
    /// </summary>
    public KeybindingGesture(Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        Key = key;
        Modifiers = modifiers;
    }

    /// <summary>Determines whether the key and the exact set of modifiers match this gesture.</summary>
    public bool Matches(Key key, ModifierKeys modifiers)
    {
        return Key == key && Modifiers == modifiers;
    }

    /// <summary>Determines whether a key event matches this gesture.</summary>
    public bool Matches(KeyEventArgs e)
    {
        if (e == null) return false;
        return Matches(e.Key, e.Modifiers);
    }

    /// <summary>Determines whether two gestures are the same shortcut.</summary>
    public bool Matches(KeybindingGesture other)
    {
        return Key == other.Key && Modifiers == other.Modifiers;
    }

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
    /// Normalizes a gesture string to canonical standard format ("Ctrl+Alt+Shift+Win+Key").
    /// </summary>
    public static string Normalize(string? gestureString)
    {
        if (TryParse(gestureString, out var gesture))
        {
            return gesture.ToString();
        }
        return gestureString ?? string.Empty;
    }

    /// <summary>
    /// Parses a gesture such as <c>"Ctrl+Shift+L"</c>. Modifiers may appear in any order and any case; <c>+</c>, <c>-</c>,
    /// <c>,</c>, <c>|</c>, <c>;</c> and spaces separate tokens. Common aliases (<c>Control</c>, <c>Cmd</c>, <c>Esc</c>, <c>Del</c>,
    /// <c>Return</c>) are accepted. Exactly one non-modifier key is required; if several are given, the last one wins.
    /// </summary>
    /// <param name="text">The gesture string.</param>
    /// <param name="gesture">The parsed gesture, or <c>default</c> on failure.</param>
    /// <returns><c>true</c> if the string is a valid gesture.</returns>
    public static bool TryParse(string? text, out KeybindingGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var parts = text!.Split(GestureDelimiters, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return false;

        var modifiers = ModifierKeys.None;
        var key = Key.None;

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

            if (TryMapKey(part, out var parsedKey))
            {
                key = parsedKey;
                continue;
            }

            return false;
        }

        if (key == Key.None)
            return false;

        gesture = new KeybindingGesture(key, modifiers);
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

    /// <summary>Formats the gesture in canonical form, e.g. <c>"Ctrl+Alt+Shift+Win+K"</c>.</summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        if (Modifiers.HasFlag(ModifierKeys.Control)) sb.Append("Ctrl+");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) sb.Append("Alt+");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) sb.Append("Shift+");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) sb.Append("Win+");
        sb.Append(Key);
        return sb.ToString();
    }

    /// <inheritdoc/>
    public bool Equals(KeybindingGesture other) => Key == other.Key && Modifiers == other.Modifiers;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is KeybindingGesture other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => ((int)Key * 397) ^ (int)Modifiers;

    /// <summary>Determines whether two gestures are the same shortcut.</summary>
    public static bool operator ==(KeybindingGesture left, KeybindingGesture right) => left.Equals(right);

    /// <summary>Determines whether two gestures differ.</summary>
    public static bool operator !=(KeybindingGesture left, KeybindingGesture right) => !left.Equals(right);
}
