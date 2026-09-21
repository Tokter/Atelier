using System;
using System.Text;
using Atelier.Core.Events;

namespace Atelier.Core.Keybinding;

/// <summary>
/// Represents a keyboard gesture consisting of a key and modifier keys (e.g. "Ctrl+S", "F12").
/// </summary>
public readonly struct KeybindingGesture : IEquatable<KeybindingGesture>
{
    public Key Key { get; }
    public ModifierKeys Modifiers { get; }

    public KeybindingGesture(Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        Key = key;
        Modifiers = modifiers;
    }

    public bool Matches(Key key, ModifierKeys modifiers)
    {
        return Key == key && Modifiers == modifiers;
    }

    public bool Matches(KeyEventArgs e)
    {
        if (e == null) return false;
        return Matches(e.Key, e.Modifiers);
    }

    public static bool TryParse(string? text, out KeybindingGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var parts = text!.Split('+');
        var modifiers = ModifierKeys.None;
        var key = Key.None;

        for (int i = 0; i < parts.Length; i++)
        {
            var part = parts[i].Trim();
            if (string.IsNullOrEmpty(part))
                continue;

            // If it's not the last part, or if it matches a modifier name
            if (i < parts.Length - 1 || IsModifier(part))
            {
                if (TryMapModifier(part, out var mod))
                {
                    modifiers |= mod;
                    continue;
                }
            }

            // Otherwise, it's the key part
            if (TryMapKey(part, out var parsedKey))
            {
                key = parsedKey;
            }
            else
            {
                return false;
            }
        }

        if (key == Key.None)
            return false;

        gesture = new KeybindingGesture(key, modifiers);
        return true;
    }

    public static KeybindingGesture Parse(string text)
    {
        if (!TryParse(text, out var gesture))
            throw new FormatException($"Invalid keybinding gesture string: '{text}'.");
        return gesture;
    }

    private static bool IsModifier(string token)
    {
        return token.Equals("ctrl", StringComparison.OrdinalIgnoreCase)
            || token.Equals("control", StringComparison.OrdinalIgnoreCase)
            || token.Equals("shift", StringComparison.OrdinalIgnoreCase)
            || token.Equals("alt", StringComparison.OrdinalIgnoreCase)
            || token.Equals("win", StringComparison.OrdinalIgnoreCase)
            || token.Equals("windows", StringComparison.OrdinalIgnoreCase)
            || token.Equals("cmd", StringComparison.OrdinalIgnoreCase)
            || token.Equals("meta", StringComparison.OrdinalIgnoreCase);
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
        if (token.Equals("alt", StringComparison.OrdinalIgnoreCase))
        {
            mod = ModifierKeys.Alt;
            return true;
        }
        if (token.Equals("win", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("windows", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("cmd", StringComparison.OrdinalIgnoreCase) ||
            token.Equals("meta", StringComparison.OrdinalIgnoreCase))
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
        if (token.Length == 1 && token[0] >= '0' && token[0] <= '9')
        {
            key = (Key)((int)Key.D0 + (token[0] - '0'));
            return true;
        }

        return Enum.TryParse(token, true, out key) && key != Key.None;
    }

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

    public bool Equals(KeybindingGesture other) => Key == other.Key && Modifiers == other.Modifiers;
    public override bool Equals(object? obj) => obj is KeybindingGesture other && Equals(other);
    public override int GetHashCode() => ((int)Key * 397) ^ (int)Modifiers;
    public static bool operator ==(KeybindingGesture left, KeybindingGesture right) => left.Equals(right);
    public static bool operator !=(KeybindingGesture left, KeybindingGesture right) => !left.Equals(right);
}
