using System.Numerics;
using Atelier.Core.Platform;
using Silk.NET.Input;

namespace Atelier.Platform.Silk;

/// <summary>
/// The gamepads of a Silk.NET input context (GLFW gamepads: Xbox and most other controllers), in Atelier's standard
/// layout: the stick Y axis is flipped to +Y up, and triggers are mapped to 0…1 whether the backend reports them from
/// −1 (GLFW's raw axes) or from 0.
/// </summary>
internal sealed class SilkGamepadProvider(IInputContext input) : IGamepadProvider
{
    // Triggers seen below zero report GLFW's −1…1 range (−1 released) and are remapped from then on.
    private readonly HashSet<(int Gamepad, int Trigger)> _signedTriggers = [];

    public IInputContext Input { get; } = input;

    public int Count => Input.Gamepads.Count;

    public GamepadState GetState(int index)
    {
        if (index < 0 || index >= Input.Gamepads.Count) return GamepadState.Disconnected;
        var gamepad = Input.Gamepads[index];
        if (!gamepad.IsConnected) return GamepadState.Disconnected;

        Vector2 Stick(int i) => i < gamepad.Thumbsticks.Count ? new Vector2(gamepad.Thumbsticks[i].X, -gamepad.Thumbsticks[i].Y) : Vector2.Zero;
        float Trigger(int i)
        {
            if (i >= gamepad.Triggers.Count) return 0;
            float raw = gamepad.Triggers[i].Position;
            if (raw < -0.01f) _signedTriggers.Add((index, i));
            return Math.Clamp(_signedTriggers.Contains((index, i)) ? (raw + 1) / 2 : raw, 0, 1);
        }

        var buttons = GamepadButtons.None;
        foreach (var button in gamepad.Buttons)
        {
            if (!button.Pressed) continue;
            buttons |= button.Name switch
            {
                ButtonName.A => GamepadButtons.A,
                ButtonName.B => GamepadButtons.B,
                ButtonName.X => GamepadButtons.X,
                ButtonName.Y => GamepadButtons.Y,
                ButtonName.LeftBumper => GamepadButtons.LeftBumper,
                ButtonName.RightBumper => GamepadButtons.RightBumper,
                ButtonName.Back => GamepadButtons.Back,
                ButtonName.Start => GamepadButtons.Start,
                ButtonName.Home => GamepadButtons.Home,
                ButtonName.LeftStick => GamepadButtons.LeftStick,
                ButtonName.RightStick => GamepadButtons.RightStick,
                ButtonName.DPadUp => GamepadButtons.DPadUp,
                ButtonName.DPadRight => GamepadButtons.DPadRight,
                ButtonName.DPadDown => GamepadButtons.DPadDown,
                ButtonName.DPadLeft => GamepadButtons.DPadLeft,
                _ => GamepadButtons.None,
            };
        }

        return new GamepadState(true, gamepad.Name, Stick(0), Stick(1), Trigger(0), Trigger(1), buttons);
    }
}
