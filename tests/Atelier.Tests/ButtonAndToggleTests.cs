using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Input;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Animation;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Tests;

internal static class ButtonInput
{
    public static readonly Point Position = new(10, 10);

    public static PointerEventArgs Pointer(PointerButtons button = PointerButtons.Left) => new(Position, button);

    public static void Click(UIElement element, PointerButtons button = PointerButtons.Left)
    {
        element.OnPointerEntered(Pointer(PointerButtons.None));
        element.OnPointerPressed(Pointer(button));
        element.OnPointerReleased(Pointer(button));
    }

    public static KeyEventArgs KeyDown(Key key, bool isRepeat = false) => new(key, isDown: true, isRepeat: isRepeat);

    public static KeyEventArgs KeyUp(Key key) => new(key, isDown: false);
}

public class ButtonTestCommand : ICommand
{
    public bool CanExecuteValue { get; set; } = true;
    public List<object?> Executed { get; } = [];
    public Action? OnExecute { get; set; }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => CanExecuteValue;

    public void Execute(object? parameter)
    {
        Executed.Add(parameter);
        OnExecute?.Invoke();
    }

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public class ButtonTests
{
    [Theory]
    [InlineData(PointerButtons.Right)]
    [InlineData(PointerButtons.Middle)]
    public void NonLeftButtons_DoNotClick_AndAreNotHandled(PointerButtons button)
    {
        var command = new ButtonTestCommand();
        var btn = new Button("OK") { Command = command };
        int clicks = 0;
        btn.Click += (s, e) => clicks++;

        btn.OnPointerEntered(ButtonInput.Pointer(PointerButtons.None));
        var press = ButtonInput.Pointer(button);
        btn.OnPointerPressed(press);
        btn.OnPointerReleased(ButtonInput.Pointer(button));

        Assert.Equal(0, clicks);
        Assert.Empty(command.Executed);
        Assert.False(press.Handled); // bubbles on, e.g. to open a context menu
    }

    [Fact]
    public void LeftClick_RaisesClickOnce_AndExecutesCommandWithParameter()
    {
        var command = new ButtonTestCommand();
        var btn = new Button("OK") { Command = command, CommandParameter = "p" };
        int clicks = 0;
        btn.Click += (s, e) => clicks++;

        ButtonInput.Click(btn);

        Assert.Equal(1, clicks);
        Assert.Equal(new object?[] { "p" }, command.Executed);
    }

    [Fact]
    public void DisabledButton_IgnoresPointerAndKeys()
    {
        var btn = new Button("OK") { IsEnabled = false };
        int clicks = 0;
        btn.Click += (s, e) => clicks++;

        ButtonInput.Click(btn);
        var enter = ButtonInput.KeyDown(Key.Enter);
        btn.OnKeyDown(enter);
        btn.OnKeyDown(ButtonInput.KeyDown(Key.Space));
        btn.OnKeyUp(ButtonInput.KeyUp(Key.Space));

        Assert.Equal(0, clicks);
        Assert.False(enter.Handled);
    }

    [Fact]
    public void LeavingTheButton_CancelsTheClick()
    {
        var btn = new Button("OK");
        int clicks = 0;
        btn.Click += (s, e) => clicks++;

        btn.OnPointerEntered(ButtonInput.Pointer(PointerButtons.None));
        btn.OnPointerPressed(ButtonInput.Pointer());
        btn.OnPointerExited(ButtonInput.Pointer(PointerButtons.None));
        btn.OnPointerEntered(ButtonInput.Pointer(PointerButtons.None));
        btn.OnPointerReleased(ButtonInput.Pointer());

        Assert.Equal(0, clicks);
    }

    [Fact]
    public void Enter_ClicksOnKeyDown_AndIgnoresAutoRepeat()
    {
        var btn = new Button("OK");
        int clicks = 0;
        btn.Click += (s, e) => clicks++;

        var first = ButtonInput.KeyDown(Key.Enter);
        btn.OnKeyDown(first);
        var repeat = ButtonInput.KeyDown(Key.Enter, isRepeat: true);
        btn.OnKeyDown(repeat);
        btn.OnKeyDown(ButtonInput.KeyDown(Key.Enter, isRepeat: true));

        Assert.Equal(1, clicks);
        Assert.True(first.Handled);
        Assert.True(repeat.Handled);
    }

    [Fact]
    public void Space_ClicksOnKeyUp_Once()
    {
        var btn = new Button("OK");
        int clicks = 0;
        btn.Click += (s, e) => clicks++;

        btn.OnKeyDown(ButtonInput.KeyDown(Key.Space));
        btn.OnKeyDown(ButtonInput.KeyDown(Key.Space, isRepeat: true));
        Assert.Equal(0, clicks);

        btn.OnKeyUp(ButtonInput.KeyUp(Key.Space));
        Assert.Equal(1, clicks);

        // A key-up without a matching key-down on this button does nothing.
        btn.OnKeyUp(ButtonInput.KeyUp(Key.Space));
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void LosingFocus_CancelsSpacePress()
    {
        var btn = new Button("OK");
        int clicks = 0;
        btn.Click += (s, e) => clicks++;

        btn.OnKeyDown(ButtonInput.KeyDown(Key.Space));
        btn.OnLostFocus();
        btn.OnKeyUp(ButtonInput.KeyUp(Key.Space));

        Assert.Equal(0, clicks);
    }

    [Fact]
    public void AlreadyHandledKeys_AreIgnored()
    {
        var btn = new Button("OK");
        int clicks = 0;
        btn.Click += (s, e) => clicks++;

        var enter = ButtonInput.KeyDown(Key.Enter);
        enter.Handled = true;
        btn.OnKeyDown(enter);

        Assert.Equal(0, clicks);
    }

    [Fact]
    public void ClickModePress_ClicksOnPress_NotOnRelease()
    {
        var btn = new Button("OK") { ClickMode = ClickMode.Press };
        int clicks = 0;
        btn.Click += (s, e) => clicks++;

        btn.OnPointerEntered(ButtonInput.Pointer(PointerButtons.None));
        btn.OnPointerPressed(ButtonInput.Pointer());
        Assert.Equal(1, clicks);
        btn.OnPointerReleased(ButtonInput.Pointer());
        Assert.Equal(1, clicks);

        btn.OnKeyDown(ButtonInput.KeyDown(Key.Space));
        Assert.Equal(2, clicks);
        btn.OnKeyUp(ButtonInput.KeyUp(Key.Space));
        Assert.Equal(2, clicks);
    }

    [Fact]
    public void ClickModeHover_ClicksWhenThePointerEnters()
    {
        var btn = new Button("OK") { ClickMode = ClickMode.Hover };
        int clicks = 0;
        btn.Click += (s, e) => clicks++;

        btn.OnPointerEntered(ButtonInput.Pointer(PointerButtons.None));
        Assert.Equal(1, clicks);

        btn.OnPointerPressed(ButtonInput.Pointer());
        btn.OnPointerReleased(ButtonInput.Pointer());
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void WithoutAnimationClock_NoRippleIsLeftBehind()
    {
        ButtonBase.SetGlobalAnimationClock(null);
        var btn = new Button("OK");

        ButtonInput.Click(btn);

        Assert.False(btn.HasActiveRipple);
    }

    [Fact]
    public void QuickSecondPress_IsNotFadedOutByTheFirstRelease()
    {
        var clock = new AnimationClock();
        ButtonBase.SetGlobalAnimationClock(clock);
        try
        {
            var btn = new Button("OK");
            btn.OnPointerEntered(ButtonInput.Pointer(PointerButtons.None));

            btn.OnPointerPressed(ButtonInput.Pointer());
            clock.Update(0.05);
            btn.OnPointerReleased(ButtonInput.Pointer());
            clock.Update(0.05);
            Assert.True(btn.RippleOpacity < 0.25f); // first release fades

            btn.OnPointerPressed(ButtonInput.Pointer());
            clock.Update(0.05);
            clock.Update(0.05);

            Assert.Equal(0.25f, btn.RippleOpacity); // the first fade no longer runs

            btn.OnPointerReleased(ButtonInput.Pointer());
            clock.Update(0.3);
            Assert.False(btn.HasActiveRipple);
        }
        finally
        {
            ButtonBase.SetGlobalAnimationClock(null);
        }
    }
}

public class ToggleButtonTests
{
    [Fact]
    public void TwoState_ClickTogglesBetweenCheckedAndUnchecked()
    {
        var toggle = new ToggleButton("Bold");

        ButtonInput.Click(toggle);
        Assert.True(toggle.IsChecked);
        ButtonInput.Click(toggle);
        Assert.False(toggle.IsChecked);
    }

    [Fact]
    public void ThreeState_CyclesThroughIndeterminate_AndRaisesMatchingEvents()
    {
        var toggle = new CheckBox("All") { IsThreeState = true };
        var events = new List<string>();
        toggle.Checked += (s, e) => events.Add("checked");
        toggle.Unchecked += (s, e) => events.Add("unchecked");
        toggle.Indeterminate += (s, e) => events.Add("indeterminate");
        var changes = new List<bool?>();
        toggle.CheckedChanged += (s, v) => changes.Add(v);

        ButtonInput.Click(toggle);
        Assert.True(toggle.IsChecked);
        ButtonInput.Click(toggle);
        Assert.Null(toggle.IsChecked);
        ButtonInput.Click(toggle);
        Assert.False(toggle.IsChecked);

        Assert.Equal(new[] { "checked", "indeterminate", "unchecked" }, events);
        Assert.Equal(new bool?[] { true, null, false }, changes);
    }

    [Fact]
    public void Command_RunsAfterToggle_WithTheNewState()
    {
        var command = new ButtonTestCommand();
        var toggle = new ToggleButton { Command = command };
        bool? stateSeenByCommand = null;
        command.OnExecute = () => stateSeenByCommand = toggle.IsChecked;

        ButtonInput.Click(toggle);

        Assert.Single(command.Executed);
        Assert.True(stateSeenByCommand);
    }

    [Fact]
    public void IsEnabled_FollowsCommandCanExecute()
    {
        var command = new ButtonTestCommand { CanExecuteValue = false };
        var root = new StackPanel();
        var toggle = new CheckBox("Sync") { Command = command };
        root.Add(toggle);
        root.AttachToHost();

        Assert.False(toggle.IsEnabled);
        ButtonInput.Click(toggle);
        Assert.False(toggle.IsChecked);

        command.CanExecuteValue = true;
        command.RaiseCanExecuteChanged();
        Assert.True(toggle.IsEnabled);

        root.DetachFromHost();
    }

    [Fact]
    public void RightClick_DoesNotToggle()
    {
        var checkBox = new CheckBox("A");
        ButtonInput.Click(checkBox, PointerButtons.Right);
        Assert.False(checkBox.IsChecked);

        var sw = new Switch("B");
        ButtonInput.Click(sw, PointerButtons.Right);
        Assert.False(sw.IsChecked);
    }

    [Fact]
    public void HoldingEnter_TogglesOnlyOnce()
    {
        var checkBox = new CheckBox("A");
        int changes = 0;
        checkBox.CheckedChanged += (s, v) => changes++;

        checkBox.OnKeyDown(ButtonInput.KeyDown(Key.Enter));
        for (int i = 0; i < 5; i++)
        {
            checkBox.OnKeyDown(ButtonInput.KeyDown(Key.Enter, isRepeat: true));
        }

        Assert.Equal(1, changes);
        Assert.True(checkBox.IsChecked);
    }

    [Fact]
    public void Space_TogglesOnKeyUp()
    {
        var sw = new Switch();
        sw.OnKeyDown(ButtonInput.KeyDown(Key.Space));
        sw.OnKeyDown(ButtonInput.KeyDown(Key.Space, isRepeat: true));
        Assert.False(sw.IsChecked);

        sw.OnKeyUp(ButtonInput.KeyUp(Key.Space));
        Assert.True(sw.IsChecked);
    }

    [Fact]
    public void DisabledSwitch_DoesNotStayPressed()
    {
        var sw = new Switch { IsEnabled = false };

        sw.OnPointerEntered(ButtonInput.Pointer(PointerButtons.None));
        sw.OnPointerPressed(ButtonInput.Pointer());
        sw.OnPointerReleased(ButtonInput.Pointer());

        Assert.False(sw.IsPressed);
        Assert.False(sw.IsChecked);
    }

    [Fact]
    public void RapidDoubleToggle_AnimatesSmoothlyBack()
    {
        var clock = new AnimationClock();
        ButtonBase.SetGlobalAnimationClock(clock);
        try
        {
            var checkBox = new CheckBox();
            checkBox.IsChecked = true;
            clock.Update(0.05);
            float reached = checkBox.CheckAnimationProgress;
            Assert.InRange(reached, 0.01f, 0.99f);

            checkBox.IsChecked = false;
            float previous = reached;
            for (int i = 0; i < 20; i++)
            {
                clock.Update(0.016);
                Assert.True(checkBox.CheckAnimationProgress <= previous, "the first animation must not pull the value back up");
                previous = checkBox.CheckAnimationProgress;
            }

            Assert.Equal(0f, checkBox.CheckAnimationProgress);
        }
        finally
        {
            ButtonBase.SetGlobalAnimationClock(null);
        }
    }

    [Fact]
    public void Indeterminate_ShowsAsFilledCheckBox_AndMiddleOfSwitch()
    {
        var checkBox = new CheckBox { IsChecked = null };
        Assert.Equal(1f, checkBox.CheckAnimationProgress);

        var sw = new Switch { IsChecked = null };
        Assert.Equal(0.5f, sw.ThumbAnimationProgress);
    }

    [Theory]
    [InlineData(typeof(CheckBox))]
    [InlineData(typeof(Switch))]
    [InlineData(typeof(RadioButton))]
    public void ReplacingStringContent_KeepsOneChild(Type type)
    {
        var toggle = (ToggleButton)Activator.CreateInstance(type)!;

        toggle.Content = "A";
        toggle.Content = "B";

        Assert.Single(toggle.Children);
        var text = Assert.IsType<TextBlock>(toggle.Children[0]);
        Assert.Equal("B", text.Text);

        toggle.Content = null;
        Assert.Empty(toggle.Children);
    }

    [Fact]
    public void CheckBoxPadding_IsAppliedToMeasureAndIndicator()
    {
        var checkBox = new CheckBox();
        checkBox.Measure(new Size(500, 500));
        Assert.Equal(new Size(18, 26), checkBox.DesiredSize); // default padding 0,4 around the 18px box

        checkBox.Padding = new Thickness(5, 8);
        checkBox.Measure(new Size(500, 500));
        Assert.Equal(new Size(28, 34), checkBox.DesiredSize);

        checkBox.Arrange(new Rect(0, 0, 28, 34));
        Assert.Equal(new Rect(5, 8, 18, 18), checkBox.GetIndicatorBounds());
    }

    [Fact]
    public void CheckBoxContent_IsPlacedAfterThePaddedIndicator()
    {
        var label = new TextBlock("Label");
        var checkBox = new CheckBox { Content = label, Padding = new Thickness(4, 0) };
        checkBox.Measure(new Size(500, 500));
        checkBox.Arrange(new Rect(Point.Zero, checkBox.DesiredSize));

        Assert.Equal(4 + 18 + 8, label.Bounds.X);
        Assert.Equal(4 + 18 + 8 + label.DesiredSize.Width + 4, checkBox.DesiredSize.Width);
    }
}

public class RadioButtonBehaviorTests
{
    [Fact]
    public void ClickingACheckedRadioButton_RaisesNoChange()
    {
        var radio = new RadioButton("A") { IsChecked = true };
        var changes = new List<bool?>();
        radio.CheckedChanged += (s, v) => changes.Add(v);
        int clicks = 0;
        radio.Click += (s, e) => clicks++;

        ButtonInput.Click(radio);
        radio.OnKeyDown(ButtonInput.KeyDown(Key.Space));
        radio.OnKeyUp(ButtonInput.KeyUp(Key.Space));

        Assert.True(radio.IsChecked);
        Assert.Empty(changes);
        Assert.Equal(2, clicks);
    }

    [Fact]
    public void ClickingAnUncheckedRadioButton_UnchecksItsSiblings()
    {
        var panel = new StackPanel();
        var a = new RadioButton("A") { IsChecked = true };
        var b = new RadioButton("B");
        panel.Add(a);
        panel.Add(b);
        var aChanges = new List<bool?>();
        a.CheckedChanged += (s, v) => aChanges.Add(v);

        ButtonInput.Click(b);

        Assert.True(b.IsChecked);
        Assert.False(a.IsChecked);
        Assert.Equal(new bool?[] { false }, aChanges);
    }

    [Fact]
    public void NamedGroup_SpansParents_AndChangingTheNameLeavesTheGroup()
    {
        var root = new StackPanel();
        var left = new StackPanel();
        var right = new StackPanel();
        var a = new RadioButton { GroupName = "RadioBehaviorGroup" };
        var b = new RadioButton { GroupName = "RadioBehaviorGroup" };
        left.Add(a);
        right.Add(b);
        root.Add(left);
        root.Add(right);

        a.IsChecked = true;
        b.IsChecked = true;
        Assert.False(a.IsChecked);

        a.GroupName = "OtherRadioBehaviorGroup";
        a.IsChecked = true;
        Assert.True(b.IsChecked);
    }
}

public class RepeatButtonTests
{
    private static void WaitForPosts(QueueingTestDispatcher dispatcher, int count)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (dispatcher.PostCount < count)
        {
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), "timer did not tick");
            Thread.Sleep(2);
        }
    }

    [Fact]
    public void RepeatsClickWhileHeld_AndStopsOnRelease()
    {
        using var dispatcher = new QueueingTestDispatcher();
        var button = new RepeatButton("+") { Delay = TimeSpan.FromMilliseconds(20), Interval = TimeSpan.FromMilliseconds(10) };
        int clicks = 0;
        button.Click += (s, e) => clicks++;

        button.OnPointerEntered(ButtonInput.Pointer(PointerButtons.None));
        button.OnPointerPressed(ButtonInput.Pointer());
        Assert.Equal(1, clicks); // clicks on press

        // Each tick posted to the UI thread clicks once.
        while (clicks < 4)
        {
            int seen = dispatcher.PostCount;
            dispatcher.RunPending();
            WaitForPosts(dispatcher, seen + 1);
        }

        button.OnPointerReleased(ButtonInput.Pointer());
        int afterRelease = clicks; // no extra click on release

        Thread.Sleep(50);
        dispatcher.RunPending();
        Assert.Equal(afterRelease, clicks);
    }

    [Fact]
    public void LeavingTheButton_StopsRepeating()
    {
        using var dispatcher = new QueueingTestDispatcher();
        var button = new RepeatButton { Delay = TimeSpan.FromMilliseconds(10), Interval = TimeSpan.FromMilliseconds(10) };
        int clicks = 0;
        button.Click += (s, e) => clicks++;

        button.OnPointerEntered(ButtonInput.Pointer(PointerButtons.None));
        button.OnPointerPressed(ButtonInput.Pointer());
        button.OnPointerExited(ButtonInput.Pointer(PointerButtons.None));

        Thread.Sleep(50);
        dispatcher.RunPending();
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void Detaching_StopsRepeating()
    {
        using var dispatcher = new QueueingTestDispatcher();
        var root = new StackPanel();
        var button = new RepeatButton { Delay = TimeSpan.FromMilliseconds(10), Interval = TimeSpan.FromMilliseconds(10) };
        root.Add(button);
        root.AttachToHost();
        int clicks = 0;
        button.Click += (s, e) => clicks++;

        button.OnKeyDown(ButtonInput.KeyDown(Key.Space)); // Space repeats too
        Assert.Equal(1, clicks);
        root.Remove(button);

        Thread.Sleep(50);
        dispatcher.RunPending();
        Assert.Equal(1, clicks);
        root.DetachFromHost();
    }
}

[Collection("KeybindingTests")]
public class KeybindingPendingChordTests
{
    [Fact]
    public void PendingChord_ExpiresAfterTimeout_WithoutAnotherKeyPress()
    {
        const string group = "ChordExpiry";
        var descriptor = new KeybindingDescriptor("Comment", group, "Ctrl+K, Ctrl+C", new AtelierRelayCommand(() => { }));
        KeybindingManager.RegisterKeybinding(descriptor);
        var previousTimeout = KeybindingHandler.ChordTimeout;
        KeybindingHandler.ChordTimeout = TimeSpan.FromMilliseconds(30);
        try
        {
            var handler = new KeybindingHandler(group);
            handler.OnKeyDown(new KeyEventArgs(Key.K, modifiers: ModifierKeys.Control));
            Assert.Equal("Ctrl+K", handler.PendingChord);

            Thread.Sleep(80);

            Assert.Null(handler.PendingChord);
        }
        finally
        {
            KeybindingHandler.ChordTimeout = previousTimeout;
            KeybindingManager.UnregisterKeybinding(descriptor);
        }
    }
}
