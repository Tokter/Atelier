using System.Numerics;

namespace Atelier.Core.Platform;

/// <summary>The buttons of a gamepad, in the standard (Xbox) layout.</summary>
[Flags]
public enum GamepadButtons
{
    None = 0,
    A = 1 << 0,
    B = 1 << 1,
    X = 1 << 2,
    Y = 1 << 3,
    LeftBumper = 1 << 4,
    RightBumper = 1 << 5,
    Back = 1 << 6,
    Start = 1 << 7,
    Home = 1 << 8,
    LeftStick = 1 << 9,
    RightStick = 1 << 10,
    DPadUp = 1 << 11,
    DPadRight = 1 << 12,
    DPadDown = 1 << 13,
    DPadLeft = 1 << 14,
}

/// <summary>
/// The state of a gamepad at one moment, in the standard (Xbox) layout: sticks from −1 to 1 with <b>+X right and +Y up</b>,
/// triggers from 0 (released) to 1 (fully pressed), and the pressed buttons. Values are raw; apply a dead zone with
/// <see cref="Gamepads.ApplyDeadzone(Vector2, float)"/>.
/// </summary>
/// <param name="IsConnected">Whether a gamepad is connected at this index; all other values are neutral if not.</param>
/// <param name="Name">The gamepad's name, e.g. "Xbox Controller".</param>
/// <param name="LeftStick">The left stick.</param>
/// <param name="RightStick">The right stick.</param>
/// <param name="LeftTrigger">The left trigger.</param>
/// <param name="RightTrigger">The right trigger.</param>
/// <param name="Buttons">The pressed buttons.</param>
public readonly record struct GamepadState(
    bool IsConnected,
    string Name,
    Vector2 LeftStick,
    Vector2 RightStick,
    float LeftTrigger,
    float RightTrigger,
    GamepadButtons Buttons)
{
    /// <summary>Gets the state of a missing gamepad.</summary>
    public static GamepadState Disconnected { get; } = new(false, string.Empty, Vector2.Zero, Vector2.Zero, 0, 0, GamepadButtons.None);

    /// <summary>Returns whether all of <paramref name="buttons"/> are pressed.</summary>
    public bool IsPressed(GamepadButtons buttons) => (Buttons & buttons) == buttons && buttons != GamepadButtons.None;
}

/// <summary>Reads the gamepads of a platform; see <see cref="Gamepads.Current"/>.</summary>
public interface IGamepadProvider
{
    /// <summary>Gets the number of gamepad slots (connected or not) the platform reports.</summary>
    int Count { get; }

    /// <summary>Gets the current state of the gamepad at <paramref name="index"/>, or a disconnected state.</summary>
    GamepadState GetState(int index);
}

/// <summary>
/// Gamepad (game controller) input: poll <see cref="GetState"/> or <see cref="First"/> every frame, e.g. in a
/// simulation loop. The platform sets <see cref="Current"/> (the Silk.NET windows do, from GLFW's gamepad support, which
/// covers Xbox and most other controllers).
/// </summary>
/// <remarks>
/// The state updates once per loop iteration of the application. An idle window doesn't run its loop, so poll while
/// animating or rendering continuously.
/// </remarks>
public static class Gamepads
{
    /// <summary>Gets or sets the platform's gamepad provider; <c>null</c> when there is none.</summary>
    public static IGamepadProvider? Current { get; set; }

    /// <summary>Gets the state of the gamepad at <paramref name="index"/>, or a disconnected state.</summary>
    public static GamepadState GetState(int index = 0) =>
        Current is { } provider && index >= 0 && index < provider.Count ? provider.GetState(index) : GamepadState.Disconnected;

    /// <summary>Gets the state of the first connected gamepad, or a disconnected state.</summary>
    public static GamepadState First
    {
        get
        {
            if (Current is not { } provider) return GamepadState.Disconnected;
            for (int i = 0; i < provider.Count; i++)
            {
                var state = provider.GetState(i);
                if (state.IsConnected) return state;
            }
            return GamepadState.Disconnected;
        }
    }

    /// <summary>
    /// Applies a radial dead zone to a stick: positions within <paramref name="deadzone"/> of the center become zero, and
    /// the rest is rescaled so the output still goes smoothly from 0 to 1.
    /// </summary>
    public static Vector2 ApplyDeadzone(Vector2 stick, float deadzone)
    {
        float length = stick.Length();
        if (length <= deadzone || deadzone >= 1) return Vector2.Zero;
        float scaled = Math.Min(1, (length - deadzone) / (1 - deadzone));
        return stick / length * scaled;
    }

    /// <summary>Applies a dead zone to a trigger or a single axis (from 0 or −1 to 1), rescaling the rest.</summary>
    public static float ApplyDeadzone(float value, float deadzone)
    {
        float magnitude = Math.Abs(value);
        if (magnitude <= deadzone || deadzone >= 1) return 0;
        return Math.Sign(value) * Math.Min(1, (magnitude - deadzone) / (1 - deadzone));
    }
}
