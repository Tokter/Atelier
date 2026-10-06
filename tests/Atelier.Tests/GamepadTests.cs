using System.Numerics;
using Atelier.Core.Platform;

namespace Atelier.Tests;

[Collection("Gamepads")]
public class GamepadTests
{
    private sealed class FakeProvider(params GamepadState[] states) : IGamepadProvider
    {
        public int Count => states.Length;
        public GamepadState GetState(int index) => states[index];
    }

    private static GamepadState Pad(string name, float leftTrigger = 0) =>
        new(true, name, Vector2.Zero, Vector2.Zero, leftTrigger, 0, GamepadButtons.None);

    [Fact]
    public void WithoutAProvider_EveryGamepadIsDisconnected()
    {
        var previous = Gamepads.Current;
        Gamepads.Current = null;
        try
        {
            Assert.False(Gamepads.First.IsConnected);
            Assert.False(Gamepads.GetState(0).IsConnected);
        }
        finally
        {
            Gamepads.Current = previous;
        }
    }

    [Fact]
    public void First_SkipsDisconnectedSlots_AndGetStateChecksTheIndex()
    {
        var previous = Gamepads.Current;
        Gamepads.Current = new FakeProvider(GamepadState.Disconnected, Pad("Second", 0.5f));
        try
        {
            Assert.Equal("Second", Gamepads.First.Name);
            Assert.Equal(0.5f, Gamepads.First.LeftTrigger);
            Assert.False(Gamepads.GetState(5).IsConnected);
            Assert.False(Gamepads.GetState(-1).IsConnected);
        }
        finally
        {
            Gamepads.Current = previous;
        }
    }

    [Fact]
    public void StickDeadzone_IsRadial_AndRescalesTheRest()
    {
        Assert.Equal(Vector2.Zero, Gamepads.ApplyDeadzone(new Vector2(0.1f, 0.1f), 0.2f));
        var full = Gamepads.ApplyDeadzone(new Vector2(0, 1), 0.2f);
        Assert.Equal(1, full.Y, 5);
        var half = Gamepads.ApplyDeadzone(new Vector2(0.6f, 0), 0.2f);
        Assert.Equal(0.5f, half.X, 5);
    }

    [Fact]
    public void AxisDeadzone_KeepsTheSign()
    {
        Assert.Equal(0, Gamepads.ApplyDeadzone(0.04f, 0.05f));
        Assert.Equal(-0.5f, Gamepads.ApplyDeadzone(-0.525f, 0.05f), 5);
        Assert.Equal(1, Gamepads.ApplyDeadzone(1f, 0.05f), 5);
    }

    [Fact]
    public void IsPressed_NeedsAllTheButtons()
    {
        var state = Pad("Pad") with { Buttons = GamepadButtons.A | GamepadButtons.LeftBumper };
        Assert.True(state.IsPressed(GamepadButtons.A));
        Assert.True(state.IsPressed(GamepadButtons.A | GamepadButtons.LeftBumper));
        Assert.False(state.IsPressed(GamepadButtons.A | GamepadButtons.B));
        Assert.False(state.IsPressed(GamepadButtons.None));
    }
}
