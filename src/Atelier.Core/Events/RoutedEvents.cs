using System;
using Atelier.Core.Primitives;

namespace Atelier.Core.Events;

public enum RoutingStrategy
{
    Direct,
    Bubble,
    Tunnel
}

public class RoutedEvent(string name, Type handlerType, RoutingStrategy strategy)
{
    public string Name { get; } = name;
    public Type HandlerType { get; } = handlerType;
    public RoutingStrategy Strategy { get; } = strategy;

    public override string ToString() => Name;
}

public class RoutedEventArgs : EventArgs
{
    public RoutedEvent? RoutedEvent { get; internal set; }
    public object? Source { get; internal set; }
    public object? OriginalSource { get; internal set; }
    public bool Handled { get; set; }

    public RoutedEventArgs() { }
    public RoutedEventArgs(RoutedEvent routedEvent) => RoutedEvent = routedEvent;
}

public enum PointerButtons
{
    None = 0,
    Left = 1 << 0,
    Right = 1 << 1,
    Middle = 1 << 2,
    XButton1 = 1 << 3,
    XButton2 = 1 << 4
}

public class PointerEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Position relative to the receiving element (local coordinates).
    /// </summary>
    public Point Position { get; }

    /// <summary>
    /// Position relative to the window (screen coordinates).
    /// </summary>
    public Point ScreenPosition { get; }

    public PointerButtons Button { get; }
    public ulong TimestampMs { get; }

    public PointerEventArgs(Point position, PointerButtons button = PointerButtons.None, ulong timestampMs = 0)
        : this(position, position, button, timestampMs)
    {
    }

    public PointerEventArgs(Point localPosition, Point screenPosition, PointerButtons button = PointerButtons.None, ulong timestampMs = 0)
    {
        Position = localPosition;
        ScreenPosition = screenPosition;
        Button = button;
        TimestampMs = timestampMs;
    }

    public virtual PointerEventArgs WithPosition(Point localPosition) =>
        new(localPosition, ScreenPosition, Button, TimestampMs)
        {
            RoutedEvent = RoutedEvent,
            Source = Source,
            OriginalSource = OriginalSource,
            Handled = Handled
        };
}

public class PointerWheelEventArgs : PointerEventArgs
{
    public float DeltaX { get; }
    public float DeltaY { get; }

    public PointerWheelEventArgs(Point position, float deltaX, float deltaY, ulong timestampMs = 0)
        : this(position, position, deltaX, deltaY, timestampMs)
    {
    }

    public PointerWheelEventArgs(Point localPosition, Point screenPosition, float deltaX, float deltaY, ulong timestampMs = 0)
        : base(localPosition, screenPosition, PointerButtons.Middle, timestampMs)
    {
        DeltaX = deltaX;
        DeltaY = deltaY;
    }

    public override PointerEventArgs WithPosition(Point localPosition) =>
        new PointerWheelEventArgs(localPosition, ScreenPosition, DeltaX, DeltaY, TimestampMs)
        {
            RoutedEvent = RoutedEvent,
            Source = Source,
            OriginalSource = OriginalSource,
            Handled = Handled
        };
}

[Flags]
public enum ModifierKeys
{
    None = 0,
    Shift = 1 << 0,
    Control = 1 << 1,
    Alt = 1 << 2,
    Windows = 1 << 3
}

public enum Key
{
    None = 0,
    Backspace,
    Tab,
    Enter,
    Escape,
    Space,
    PageUp,
    PageDown,
    End,
    Home,
    Left,
    Up,
    Right,
    Down,
    Delete,
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12
}

public class KeyEventArgs : RoutedEventArgs
{
    public Key Key { get; }
    public int KeyCode { get; }
    public ModifierKeys Modifiers { get; }
    public bool IsDown { get; }

    public KeyEventArgs(Key key, int keyCode = 0, ModifierKeys modifiers = ModifierKeys.None, bool isDown = true)
    {
        Key = key;
        KeyCode = keyCode;
        Modifiers = modifiers;
        IsDown = isDown;
    }

    public KeyEventArgs(int keyCode, ModifierKeys modifiers, bool isDown)
        : this(Key.None, keyCode, modifiers, isDown)
    {
    }
}

public class TextInputEventArgs : RoutedEventArgs
{
    public string Text { get; }

    public TextInputEventArgs(string text)
    {
        Text = text;
    }
}
