using System;
using System.Collections.Generic;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Theming.Material;

namespace Atelier.Tests;

/// <summary>A manually advanced clock for testing delays; timers fire synchronously inside <see cref="Advance"/>.</summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private readonly List<FakeTimer> _timers = [];
    private long _ticks;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => _ticks;

    public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(_ticks);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FakeTimer(this, callback, state, _ticks + dueTime.Ticks);
        _timers.Add(timer);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        long end = _ticks + by.Ticks;
        while (true)
        {
            FakeTimer? next = null;
            foreach (var timer in _timers)
            {
                if (timer.Due <= end && (next == null || timer.Due < next.Due))
                {
                    next = timer;
                }
            }

            if (next == null)
            {
                break;
            }

            _ticks = Math.Max(_ticks, next.Due);
            _timers.Remove(next);
            next.Fire();
        }
        _ticks = end;
    }

    public void AdvanceMs(double milliseconds) => Advance(TimeSpan.FromMilliseconds(milliseconds));

    private sealed class FakeTimer(FakeTimeProvider owner, TimerCallback callback, object? state, long due) : ITimer
    {
        public long Due { get; private set; } = due;

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Due = owner._ticks + dueTime.Ticks;
            if (!owner._timers.Contains(this))
            {
                owner._timers.Add(this);
            }
            return true;
        }

        public void Dispose() => owner._timers.Remove(this);

        public System.Threading.Tasks.ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
    }
}

public partial class ToolTipTestViewModel : ObservableObject
{
    [ObservableProperty]
    private string _status = "Online";
}

public sealed class ToolTipTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly TimeProvider _originalTime = ToolTipService.TimeProvider;
    private readonly bool _originalKeyboard = ToolTipService.ShowOnKeyboardFocus;
    private readonly StackPanel _root;
    private readonly Button _save;
    private readonly Button _open;
    private readonly Button _plain;

    public ToolTipTests()
    {
        ToolTipService.Close();
        PopupManager.CloseAllPopups();
        ToolTipService.TimeProvider = _time;

        _save = new Button("Save").ToolTip("Save the document");
        _open = new Button("Open").ToolTip("Open a document");
        _plain = new Button("No tooltip");
        _root = new StackPanel().Children(_save, _open, _plain);
        _root.AttachToHost();
        _root.Measure(new Size(800, 600));
        _root.Arrange(new Rect(0, 0, 800, 600));
    }

    public void Dispose()
    {
        ToolTipService.Close();
        PopupManager.CloseAllPopups();
        ToolTipService.TimeProvider = _originalTime;
        ToolTipService.ShowOnKeyboardFocus = _originalKeyboard;
        _root.DetachFromHost();
    }

    // Moves the pointer onto an element, and lets a long pause pass so the next tooltip isn't "warm".
    private void Cool() => _time.AdvanceMs(5000);

    [Fact]
    public void Hover_OpensAfterTheInitialDelay()
    {
        ToolTipService.OnPointerOver(_save);
        _time.AdvanceMs(499);
        Assert.Null(ToolTipService.CurrentOwner);

        _time.AdvanceMs(1);
        Assert.Same(_save, ToolTipService.CurrentOwner);
        var toolTip = ToolTipService.CurrentToolTip!;
        Assert.True(toolTip.IsOpen);
        Assert.Equal("Save the document", toolTip.Content);
        Assert.False(toolTip.IsRich);
        Assert.False(toolTip.IsInteractive);
    }

    [Fact]
    public void Hover_OverADescendant_ShowsTheAncestorsToolTip()
    {
        var label = new TextBlock("Label");
        var card = new Border().Child(label).ToolTip("Card tooltip");
        var root = new StackPanel().Children(card);
        root.AttachToHost();

        ToolTipService.OnPointerOver(label);
        _time.AdvanceMs(500);

        Assert.Same(card, ToolTipService.CurrentOwner);
        root.DetachFromHost();
    }

    [Fact]
    public void LeavingBeforeTheDelay_CancelsTheTooltip()
    {
        ToolTipService.OnPointerOver(_save);
        _time.AdvanceMs(300);
        ToolTipService.OnPointerOver(_plain);
        _time.AdvanceMs(1000);

        Assert.Null(ToolTipService.CurrentOwner);
    }

    [Fact]
    public void PlainToolTip_ClosesWhenThePointerLeaves_AndTheNextOpensAtOnce()
    {
        ToolTipService.OnPointerOver(_save);
        _time.AdvanceMs(500);

        ToolTipService.OnPointerOver(_plain);
        Assert.Null(ToolTipService.CurrentOwner);

        // Within BetweenShowDelay the next tooltip opens without delay.
        _time.AdvanceMs(200);
        ToolTipService.OnPointerOver(_open);
        Assert.Same(_open, ToolTipService.CurrentOwner);

        // After a long pause the full delay applies again.
        ToolTipService.OnPointerOver(null);
        Cool();
        ToolTipService.OnPointerOver(_save);
        Assert.Null(ToolTipService.CurrentOwner);
        _time.AdvanceMs(500);
        Assert.Same(_save, ToolTipService.CurrentOwner);
    }

    [Fact]
    public void Press_ClosesTheTooltip_UntilThePointerLeavesTheElement()
    {
        ToolTipService.OnPointerOver(_save);
        _time.AdvanceMs(500);
        ToolTipService.OnPointerPressed();
        Assert.Null(ToolTipService.CurrentOwner);

        // Still over the pressed element: no new tooltip.
        ToolTipService.OnPointerOver(_save);
        Cool();
        Assert.Null(ToolTipService.CurrentOwner);

        // Leave and come back: it opens again.
        ToolTipService.OnPointerOver(_plain);
        ToolTipService.OnPointerOver(_save);
        _time.AdvanceMs(500);
        Assert.Same(_save, ToolTipService.CurrentOwner);
    }

    [Fact]
    public void Wheel_ClosesTheTooltip()
    {
        ToolTipService.OnPointerOver(_save);
        _time.AdvanceMs(500);
        ToolTipService.OnPointerWheel();

        Assert.Null(ToolTipService.CurrentOwner);
    }

    [Fact]
    public void Escape_ClosesTheTooltipBeforeOtherPopups()
    {
        ToolTipService.OnPointerOver(_save);
        _time.AdvanceMs(500);

        var escape = new KeyEventArgs(Key.Escape, 0, ModifierKeys.None, true);
        Assert.True(PopupManager.HandleKeyDown(escape, _root));
        Assert.Null(ToolTipService.CurrentOwner);
    }

    [Fact]
    public void DisabledElement_ShowsItsTooltipOnlyWithShowOnDisabled()
    {
        _save.IsEnabled = false;
        ToolTipService.OnPointerOver(_save);
        _time.AdvanceMs(1000);
        Assert.Null(ToolTipService.CurrentOwner);

        _save.ToolTipShowOnDisabled();
        ToolTipService.OnPointerOver(_plain);
        ToolTipService.OnPointerOver(_save);
        _time.AdvanceMs(500);
        Assert.Same(_save, ToolTipService.CurrentOwner);
    }

    [Fact]
    public void PerElementShowDelay_OverridesTheGlobalDelay()
    {
        _save.ToolTipShowDelay(TimeSpan.FromMilliseconds(100));
        ToolTipService.OnPointerOver(_save);
        _time.AdvanceMs(100);

        Assert.Same(_save, ToolTipService.CurrentOwner);
    }

    [Fact]
    public void RichToolTip_StaysOpenWhileThePointerIsOverIt()
    {
        var retry = new Button("Retry");
        var rich = new RichToolTip().Title("Sync paused").Text("Changes are kept locally.").Actions(retry);
        _save.ToolTip(rich);

        ToolTipService.OnPointerOver(_save);
        _time.AdvanceMs(500);
        var toolTip = ToolTipService.CurrentToolTip!;
        Assert.True(toolTip.IsRich);
        Assert.True(toolTip.IsInteractive);

        // Leaving the element starts the grace period; reaching the tooltip keeps it open.
        ToolTipService.OnPointerOver(_plain);
        _time.AdvanceMs(200);
        ToolTipService.OnPointerOver(retry);
        _time.AdvanceMs(1000);
        Assert.Same(_save, ToolTipService.CurrentOwner);

        // A press on its button doesn't close it.
        ToolTipService.OnPointerPressed();
        Assert.Same(_save, ToolTipService.CurrentOwner);

        // Leaving both closes it after the grace period.
        ToolTipService.OnPointerOver(null);
        _time.AdvanceMs(299);
        Assert.Same(_save, ToolTipService.CurrentOwner);
        _time.AdvanceMs(1);
        Assert.Null(ToolTipService.CurrentOwner);
    }

    [Fact]
    public void IsInteractive_OverridesTheDefaultForTheContent()
    {
        _save.ToolTip(new TextBlock("Element, but not interactive")).ToolTipIsInteractive(false);
        ToolTipService.OnPointerOver(_save);
        _time.AdvanceMs(500);
        Assert.False(ToolTipService.CurrentToolTip!.IsInteractive);

        // Leaving closes it immediately, like a plain tooltip.
        ToolTipService.OnPointerOver(_plain);
        Assert.Null(ToolTipService.CurrentOwner);
    }

    [Fact]
    public void BoundToolTip_UpdatesTheOpenTooltip()
    {
        var vm = new ToolTipTestViewModel();
        _save.BindToolTip(vm, v => v.Status);
        ToolTipService.OnPointerOver(_save);
        _time.AdvanceMs(500);
        Assert.Equal("Online", ToolTipService.CurrentToolTip!.Content);

        vm.Status = "Offline";
        Assert.Equal("Offline", ToolTipService.CurrentToolTip!.Content);
    }

    [Fact]
    public void RemovingTheOwner_ClosesItsTooltip()
    {
        ToolTipService.OnPointerOver(_save);
        _time.AdvanceMs(500);
        _root.Remove(_save);

        Assert.Null(ToolTipService.CurrentOwner);
    }

    [Fact]
    public void PlainToolTip_LetsPointerInputThrough_AndDoesNotConsumeClicks()
    {
        ToolTipService.OnPointerOver(_save);
        _time.AdvanceMs(500);
        var toolTip = ToolTipService.CurrentToolTip!;
        PopupManager.UpdatePopups(new Size(800, 600), _root);
        var center = new Point(toolTip.ActualBounds.X + toolTip.ActualBounds.Width / 2, toolTip.ActualBounds.Y + toolTip.ActualBounds.Height / 2);

        Assert.Null(PopupManager.HitTest(center, _root));
        Assert.False(PopupManager.HandleMouseDown(center, PointerButtons.Left, root: _root));
        Assert.Null(ToolTipService.CurrentOwner);
    }

    [Fact]
    public void ToolTip_IsPlacedBelowTheElement_WithAGap()
    {
        ToolTipService.OnPointerOver(_open);
        _time.AdvanceMs(500);
        var toolTip = ToolTipService.CurrentToolTip!;
        PopupManager.UpdatePopups(new Size(800, 600), _root);

        Assert.Equal(_open.Bounds.Bottom + toolTip.Gap, toolTip.ActualBounds.Y, 1);
        Assert.Equal(_open.Bounds.X, toolTip.ActualBounds.X, 1);
    }

    [Fact]
    public void PointerPlacement_PlacesTheTooltipBelowThePointer()
    {
        UIElement? hovered = null;
        PopupManager.HandleMouseMove(new Point(120, 10), ref hovered, root: _root);
        _save.ToolTipPlacement(PlacementMode.Pointer);

        ToolTipService.OnPointerOver(_save);
        _time.AdvanceMs(500);
        var toolTip = ToolTipService.CurrentToolTip!;
        PopupManager.UpdatePopups(new Size(800, 600), _root);

        Assert.Equal(120 - toolTip.Gap, toolTip.ActualBounds.X, 1);
        Assert.True(toolTip.ActualBounds.Y >= 10 + 20, $"The tooltip at {toolTip.ActualBounds.Y} covers the pointer.");
    }

    [Fact]
    public void KeyboardFocus_ShowsTooltips_OnlyWhenEnabledGlobally()
    {
        FocusManager.FocusNext(_root);
        Assert.True(_save.IsFocused);
        _time.AdvanceMs(1000);
        Assert.Null(ToolTipService.CurrentOwner);

        ToolTipService.ShowOnKeyboardFocus = true;
        FocusManager.FocusNext(_root);
        Assert.True(_open.IsFocused);
        _time.AdvanceMs(500);
        Assert.Same(_open, ToolTipService.CurrentOwner);

        // Moving the focus on closes it.
        FocusManager.FocusNext(_root);
        Assert.NotSame(_open, ToolTipService.CurrentOwner);
        FocusManager.ClearFocus(_root);
    }

    [Fact]
    public void ToolTip_GetsTheOwnersDataContext()
    {
        var vm = new ToolTipTestViewModel { Status = "Busy" };
        var label = new TextBlock().BindText((ToolTipTestViewModel v) => v.Status);
        _save.DataContext(vm).ToolTip(label);

        ToolTipService.OnPointerOver(_save);
        _time.AdvanceMs(500);

        Assert.Equal("Busy", label.Text);
    }

    [Fact]
    public void Theme_StylesPlainAndRichTooltipsDifferently()
    {
        using var theme = ActiveTheme.Use(MaterialTheme.CreateLight());

        ToolTipService.OnPointerOver(_save);
        _time.AdvanceMs(500);
        var plain = ToolTipService.CurrentToolTip!;
        Assert.Equal(new CornerRadius(4), plain.CornerRadius);
        Assert.Equal(0f, plain.Elevation);

        _open.ToolTip(new RichToolTip().Title("Rich"));
        ToolTipService.OnPointerOver(_open);
        var rich = ToolTipService.CurrentToolTip!;
        Assert.Equal(new CornerRadius(12), rich.CornerRadius);
        Assert.True(rich.Elevation > 0);
    }

    [Fact]
    public void Popup_Padding_SurroundsTheChild()
    {
        var target = new Border().Size(100, 30);
        var child = new Border().Size(50, 20);
        var popup = new Popup().Child(child).PlacementTarget(target).Padding(10);
        var root = new StackPanel().Children(target, popup);
        root.AttachToHost();
        root.Measure(new Size(800, 600));
        root.Arrange(new Rect(0, 0, 800, 600));

        popup.IsOpen = true;
        popup.UpdatePlacement(new Size(800, 600));

        Assert.Equal(70f, popup.ActualBounds.Width);
        Assert.Equal(40f, popup.ActualBounds.Height);
        Assert.Equal(new Rect(10, 10, 50, 20), child.Bounds);
        popup.IsOpen = false;
        root.DetachFromHost();
    }
}
