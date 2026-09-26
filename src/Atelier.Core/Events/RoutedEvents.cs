using System;
using Atelier.Core.Primitives;

namespace Atelier.Core.Events;

/// <summary>
/// Describes how a <see cref="RoutedEvent"/> travels through the element tree.
/// </summary>
/// <remarks>
/// This value is descriptive metadata only. The current dispatch code
/// (<see cref="Tree.UIElement.DispatchBubblePointerEvent{T}(T, Action{Tree.UIElement, T})"/> and
/// <see cref="Tree.UIElement.DispatchBubbleKeyEvent{T}(T, Action{Tree.UIElement, T})"/>) always bubbles and does not read it.
/// </remarks>
public enum RoutingStrategy
{
    /// <summary>The event is raised only on the source element.</summary>
    Direct,
    /// <summary>The event is raised on the source element and then on each ancestor.</summary>
    Bubble,
    /// <summary>The event is raised from the root down to the source element. Not implemented by the dispatch code yet.</summary>
    Tunnel
}

/// <summary>
/// Identifies a routed event by name, handler type and <see cref="RoutingStrategy"/>.
/// </summary>
/// <remarks>
/// Not used by the dispatch code yet; events are dispatched by bubbling regardless of any <see cref="RoutedEvent"/>
/// attached to the event args.
/// </remarks>
/// <param name="name">The event name.</param>
/// <param name="handlerType">The delegate type of the event handlers.</param>
/// <param name="strategy">The routing strategy the event declares.</param>
public class RoutedEvent(string name, Type handlerType, RoutingStrategy strategy)
{
    /// <summary>
    /// Gets the event name.
    /// </summary>
    public string Name { get; } = name;
    /// <summary>
    /// Gets the delegate type of the event handlers.
    /// </summary>
    public Type HandlerType { get; } = handlerType;
    /// <summary>
    /// Gets the routing strategy the event declares.
    /// </summary>
    public RoutingStrategy Strategy { get; } = strategy;

    /// <summary>
    /// Returns the event <see cref="Name"/>.
    /// </summary>
    /// <returns>The event name.</returns>
    public override string ToString() => Name;
}

/// <summary>
/// Base class for event data of events routed through the element tree.
/// </summary>
public class RoutedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the routed event these args were created for, if any.
    /// </summary>
    public RoutedEvent? RoutedEvent { get; internal set; }
    /// <summary>
    /// Gets the element on which dispatch started.
    /// </summary>
    /// <remarks>
    /// Set by the dispatch methods if not already set, and not updated while the event bubbles to ancestors.
    /// </remarks>
    public object? Source { get; internal set; }
    /// <summary>
    /// Gets the element on which the event was originally raised.
    /// </summary>
    public object? OriginalSource { get; internal set; }
    /// <summary>
    /// Gets or sets whether the event has been handled; setting it to <see langword="true"/> stops further bubbling.
    /// </summary>
    public bool Handled { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RoutedEventArgs"/> class without an associated routed event.
    /// </summary>
    public RoutedEventArgs() { }
    /// <summary>
    /// Initializes a new instance of the <see cref="RoutedEventArgs"/> class for the given routed event.
    /// </summary>
    /// <param name="routedEvent">The routed event these args belong to.</param>
    public RoutedEventArgs(RoutedEvent routedEvent) => RoutedEvent = routedEvent;
}

/// <summary>
/// Identifies pointer (mouse) buttons.
/// </summary>
public enum PointerButtons
{
    /// <summary>No button.</summary>
    None = 0,
    /// <summary>The left (primary) button.</summary>
    Left = 1 << 0,
    /// <summary>The right (secondary) button.</summary>
    Right = 1 << 1,
    /// <summary>The middle button or wheel.</summary>
    Middle = 1 << 2,
    /// <summary>The first extended button (typically "back").</summary>
    XButton1 = 1 << 3,
    /// <summary>The second extended button (typically "forward").</summary>
    XButton2 = 1 << 4
}

/// <summary>
/// Event data for pointer (mouse) events.
/// </summary>
public class PointerEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Position relative to the receiving element (local coordinates).
    /// </summary>
    /// <remarks>
    /// Updated by <see cref="Tree.UIElement.DispatchBubblePointerEvent{T}(T, Action{Tree.UIElement, T})"/> as the event
    /// bubbles, so it is only meaningful during the handler call.
    /// </remarks>
    public Point Position { get; internal set; }

    /// <summary>
    /// Position relative to the window (screen coordinates).
    /// </summary>
    public Point ScreenPosition { get; }

    /// <summary>
    /// Gets the button associated with the event, or <see cref="PointerButtons.None"/> if there is none.
    /// </summary>
    public PointerButtons Button { get; }
    /// <summary>
    /// Gets the event timestamp in milliseconds, or 0 if the platform did not supply one.
    /// </summary>
    public ulong TimestampMs { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PointerEventArgs"/> class using the same point as local and screen position.
    /// </summary>
    /// <param name="position">The position, used for both <see cref="Position"/> and <see cref="ScreenPosition"/>.</param>
    /// <param name="button">The button associated with the event.</param>
    /// <param name="timestampMs">The event timestamp in milliseconds, or 0 if unknown.</param>
    public PointerEventArgs(Point position, PointerButtons button = PointerButtons.None, ulong timestampMs = 0)
        : this(position, position, button, timestampMs)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PointerEventArgs"/> class.
    /// </summary>
    /// <param name="localPosition">The position relative to the receiving element.</param>
    /// <param name="screenPosition">The position relative to the window.</param>
    /// <param name="button">The button associated with the event.</param>
    /// <param name="timestampMs">The event timestamp in milliseconds, or 0 if unknown.</param>
    public PointerEventArgs(Point localPosition, Point screenPosition, PointerButtons button = PointerButtons.None, ulong timestampMs = 0)
    {
        Position = localPosition;
        ScreenPosition = screenPosition;
        Button = button;
        TimestampMs = timestampMs;
    }

    /// <summary>
    /// Creates a copy of these args with a different local position.
    /// </summary>
    /// <remarks>
    /// The copy keeps <see cref="ScreenPosition"/>, <see cref="Button"/>, <see cref="TimestampMs"/> and the routed-event
    /// state (<see cref="RoutedEventArgs.RoutedEvent"/>, <see cref="RoutedEventArgs.Source"/>,
    /// <see cref="RoutedEventArgs.OriginalSource"/>, <see cref="RoutedEventArgs.Handled"/>). Setting
    /// <see cref="RoutedEventArgs.Handled"/> on the copy does not affect the original.
    /// </remarks>
    /// <param name="localPosition">The new position relative to the receiving element.</param>
    /// <returns>A new args instance of the same type.</returns>
    public virtual PointerEventArgs WithPosition(Point localPosition) =>
        new(localPosition, ScreenPosition, Button, TimestampMs)
        {
            RoutedEvent = RoutedEvent,
            Source = Source,
            OriginalSource = OriginalSource,
            Handled = Handled
        };
}

/// <summary>
/// Event data for pointer wheel (scroll) events.
/// </summary>
/// <remarks>
/// <see cref="PointerEventArgs.Button"/> is always <see cref="PointerButtons.Middle"/>.
/// </remarks>
public class PointerWheelEventArgs : PointerEventArgs
{
    /// <summary>
    /// Gets the horizontal scroll amount, as reported by the platform.
    /// </summary>
    public float DeltaX { get; }
    /// <summary>
    /// Gets the vertical scroll amount, as reported by the platform.
    /// </summary>
    public float DeltaY { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PointerWheelEventArgs"/> class using the same point as local and screen position.
    /// </summary>
    /// <param name="position">The pointer position, used for both local and screen position.</param>
    /// <param name="deltaX">The horizontal scroll amount.</param>
    /// <param name="deltaY">The vertical scroll amount.</param>
    /// <param name="timestampMs">The event timestamp in milliseconds, or 0 if unknown.</param>
    public PointerWheelEventArgs(Point position, float deltaX, float deltaY, ulong timestampMs = 0)
        : this(position, position, deltaX, deltaY, timestampMs)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PointerWheelEventArgs"/> class.
    /// </summary>
    /// <param name="localPosition">The pointer position relative to the receiving element.</param>
    /// <param name="screenPosition">The pointer position relative to the window.</param>
    /// <param name="deltaX">The horizontal scroll amount.</param>
    /// <param name="deltaY">The vertical scroll amount.</param>
    /// <param name="timestampMs">The event timestamp in milliseconds, or 0 if unknown.</param>
    public PointerWheelEventArgs(Point localPosition, Point screenPosition, float deltaX, float deltaY, ulong timestampMs = 0)
        : base(localPosition, screenPosition, PointerButtons.Middle, timestampMs)
    {
        DeltaX = deltaX;
        DeltaY = deltaY;
    }

    /// <inheritdoc/>
    public override PointerEventArgs WithPosition(Point localPosition) =>
        new PointerWheelEventArgs(localPosition, ScreenPosition, DeltaX, DeltaY, TimestampMs)
        {
            RoutedEvent = RoutedEvent,
            Source = Source,
            OriginalSource = OriginalSource,
            Handled = Handled
        };
}

/// <summary>
/// Keyboard modifier keys held during a key event.
/// </summary>
[Flags]
public enum ModifierKeys
{
    /// <summary>No modifier.</summary>
    None = 0,
    /// <summary>Either Shift key.</summary>
    Shift = 1 << 0,
    /// <summary>Either Control key.</summary>
    Control = 1 << 1,
    /// <summary>Either Alt key.</summary>
    Alt = 1 << 2,
    /// <summary>Either Windows (Super/Command) key.</summary>
    Windows = 1 << 3
}

/// <summary>
/// Platform-independent key identifiers.
/// </summary>
/// <remarks>
/// Besides the named keys, the enum contains the letter keys <c>A</c> to <c>Z</c>, the top-row digit keys
/// <c>D0</c> to <c>D9</c>, and the function keys <c>F1</c> to <c>F12</c>. Keys the platform layer cannot map are
/// reported as <see cref="None"/>; use <see cref="KeyEventArgs.KeyCode"/> to identify them.
/// </remarks>
public enum Key
{
    /// <summary>No key, or a key without a mapping.</summary>
    None = 0,
    /// <summary>The Backspace key.</summary>
    Backspace,
    /// <summary>The Tab key.</summary>
    Tab,
    /// <summary>The Enter (Return) key.</summary>
    Enter,
    /// <summary>The Escape key.</summary>
    Escape,
    /// <summary>The Space bar.</summary>
    Space,
    /// <summary>The Page Up key.</summary>
    PageUp,
    /// <summary>The Page Down key.</summary>
    PageDown,
    /// <summary>The End key.</summary>
    End,
    /// <summary>The Home key.</summary>
    Home,
    /// <summary>The Left arrow key.</summary>
    Left,
    /// <summary>The Up arrow key.</summary>
    Up,
    /// <summary>The Right arrow key.</summary>
    Right,
    /// <summary>The Down arrow key.</summary>
    Down,
    /// <summary>The Delete key.</summary>
    Delete,
    /// <summary>The A key.</summary>
    A,
    /// <summary>The B key.</summary>
    B,
    /// <summary>The C key.</summary>
    C,
    /// <summary>The D key.</summary>
    D,
    /// <summary>The E key.</summary>
    E,
    /// <summary>The F key.</summary>
    F,
    /// <summary>The G key.</summary>
    G,
    /// <summary>The H key.</summary>
    H,
    /// <summary>The I key.</summary>
    I,
    /// <summary>The J key.</summary>
    J,
    /// <summary>The K key.</summary>
    K,
    /// <summary>The L key.</summary>
    L,
    /// <summary>The M key.</summary>
    M,
    /// <summary>The N key.</summary>
    N,
    /// <summary>The O key.</summary>
    O,
    /// <summary>The P key.</summary>
    P,
    /// <summary>The Q key.</summary>
    Q,
    /// <summary>The R key.</summary>
    R,
    /// <summary>The S key.</summary>
    S,
    /// <summary>The T key.</summary>
    T,
    /// <summary>The U key.</summary>
    U,
    /// <summary>The V key.</summary>
    V,
    /// <summary>The W key.</summary>
    W,
    /// <summary>The X key.</summary>
    X,
    /// <summary>The Y key.</summary>
    Y,
    /// <summary>The Z key.</summary>
    Z,
    /// <summary>The 0 key on the main keyboard row.</summary>
    D0,
    /// <summary>The 1 key on the main keyboard row.</summary>
    D1,
    /// <summary>The 2 key on the main keyboard row.</summary>
    D2,
    /// <summary>The 3 key on the main keyboard row.</summary>
    D3,
    /// <summary>The 4 key on the main keyboard row.</summary>
    D4,
    /// <summary>The 5 key on the main keyboard row.</summary>
    D5,
    /// <summary>The 6 key on the main keyboard row.</summary>
    D6,
    /// <summary>The 7 key on the main keyboard row.</summary>
    D7,
    /// <summary>The 8 key on the main keyboard row.</summary>
    D8,
    /// <summary>The 9 key on the main keyboard row.</summary>
    D9,
    /// <summary>The F1 function key.</summary>
    F1,
    /// <summary>The F2 function key.</summary>
    F2,
    /// <summary>The F3 function key.</summary>
    F3,
    /// <summary>The F4 function key.</summary>
    F4,
    /// <summary>The F5 function key.</summary>
    F5,
    /// <summary>The F6 function key.</summary>
    F6,
    /// <summary>The F7 function key.</summary>
    F7,
    /// <summary>The F8 function key.</summary>
    F8,
    /// <summary>The F9 function key.</summary>
    F9,
    /// <summary>The F10 function key.</summary>
    F10,
    /// <summary>The F11 function key.</summary>
    F11,
    /// <summary>The F12 function key.</summary>
    F12
}

/// <summary>
/// Event data for key press and release events.
/// </summary>
public class KeyEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Gets the mapped key, or <see cref="Key.None"/> if the key has no mapping.
    /// </summary>
    public Key Key { get; }
    /// <summary>
    /// Gets the raw, platform-specific key code, or 0 if not supplied.
    /// </summary>
    public int KeyCode { get; }
    /// <summary>
    /// Gets the modifier keys held when the event occurred.
    /// </summary>
    public ModifierKeys Modifiers { get; }
    /// <summary>
    /// Gets whether the key was pressed (<see langword="true"/>) or released (<see langword="false"/>).
    /// </summary>
    public bool IsDown { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyEventArgs"/> class.
    /// </summary>
    /// <param name="key">The mapped key.</param>
    /// <param name="keyCode">The raw, platform-specific key code, or 0 if unknown.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <param name="isDown"><see langword="true"/> for a key press; <see langword="false"/> for a release.</param>
    public KeyEventArgs(Key key, int keyCode = 0, ModifierKeys modifiers = ModifierKeys.None, bool isDown = true)
    {
        Key = key;
        KeyCode = keyCode;
        Modifiers = modifiers;
        IsDown = isDown;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyEventArgs"/> class from a raw key code, with <see cref="Key"/> set to <see cref="Key.None"/>.
    /// </summary>
    /// <param name="keyCode">The raw, platform-specific key code.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <param name="isDown"><see langword="true"/> for a key press; <see langword="false"/> for a release.</param>
    public KeyEventArgs(int keyCode, ModifierKeys modifiers, bool isDown)
        : this(Key.None, keyCode, modifiers, isDown)
    {
    }
}

/// <summary>
/// Event data for text input, delivered separately from key events.
/// </summary>
public class TextInputEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Gets the entered text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TextInputEventArgs"/> class.
    /// </summary>
    /// <param name="text">The entered text.</param>
    public TextInputEventArgs(string text)
    {
        Text = text;
    }
}
