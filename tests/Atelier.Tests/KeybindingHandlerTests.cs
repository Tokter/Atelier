using System;
using System.Collections.Generic;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Xunit;

namespace Atelier.Tests;

public class KeybindingHandlerTests
{
    [Theory]
    [InlineData("F11", Key.F11, ModifierKeys.None)]
    [InlineData("f11", Key.F11, ModifierKeys.None)]
    [InlineData("F12", Key.F12, ModifierKeys.None)]
    [InlineData("f12", Key.F12, ModifierKeys.None)]
    [InlineData("Ctrl+S", Key.S, ModifierKeys.Control)]
    [InlineData("ctrl+s", Key.S, ModifierKeys.Control)]
    [InlineData("ctrl + s", Key.S, ModifierKeys.Control)]
    [InlineData("Control+S", Key.S, ModifierKeys.Control)]
    [InlineData("Ctrl+Shift+P", Key.P, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData("Alt+F4", Key.F4, ModifierKeys.Alt)]
    [InlineData("Shift+Enter", Key.Enter, ModifierKeys.Shift)]
    [InlineData("Return", Key.Enter, ModifierKeys.None)]
    [InlineData("Esc", Key.Escape, ModifierKeys.None)]
    [InlineData("Del", Key.Delete, ModifierKeys.None)]
    [InlineData("1", Key.D1, ModifierKeys.None)]
    public void KeybindingGesture_TryParse_ParsesCorrectly(string text, Key expectedKey, ModifierKeys expectedMods)
    {
        bool success = KeybindingGesture.TryParse(text, out var gesture);
        Assert.True(success);
        Assert.Equal(expectedKey, gesture.Key);
        Assert.Equal(expectedMods, gesture.Modifiers);
    }

    [Fact]
    public void KeybindingGesture_Matches_KeyEventArgs()
    {
        var gesture = KeybindingGesture.Parse("Ctrl+Shift+Z");
        var matchingEvent = new KeyEventArgs(Key.Z, 0, ModifierKeys.Control | ModifierKeys.Shift, true);
        var nonMatchingKey = new KeyEventArgs(Key.Y, 0, ModifierKeys.Control | ModifierKeys.Shift, true);
        var nonMatchingMod = new KeyEventArgs(Key.Z, 0, ModifierKeys.Control, true);

        Assert.True(gesture.Matches(matchingEvent));
        Assert.False(gesture.Matches(nonMatchingKey));
        Assert.False(gesture.Matches(nonMatchingMod));
    }

    [Fact]
    public void KeybindingGesture_InvalidStrings_ReturnFalse()
    {
        Assert.False(KeybindingGesture.TryParse(null, out _));
        Assert.False(KeybindingGesture.TryParse("", out _));
        Assert.False(KeybindingGesture.TryParse("   ", out _));
        Assert.False(KeybindingGesture.TryParse("Ctrl+", out _));
        Assert.False(KeybindingGesture.TryParse("UnknownKey", out _));
    }

    [Fact]
    public void KeybindingHandler_VisualTree_BubblesFromTextBox_ToDetail_AndGlobal()
    {
        KeybindingManager.Clear();

        // 1. Register "Global" F12 keybinding
        object? globalExecutedTarget = null;
        var globalCommand = new AtelierRelayCommand(target =>
        {
            globalExecutedTarget = target;
        });
        KeybindingManager.RegisterKeybinding(new KeybindingDescriptor("Debug", "Global", "F12", globalCommand));

        // 2. Register "Detail" Ctrl+S keybinding
        object? detailExecutedTarget = null;
        var detailCommand = new AtelierRelayCommand(target =>
        {
            detailExecutedTarget = target;
        });
        KeybindingManager.RegisterKeybinding(new KeybindingDescriptor("Save", "Detail", "Ctrl+S", detailCommand));

        // 3. Construct visual tree:
        // Global KeybindingHandler ("Global")
        //   -> Detail KeybindingHandler ("Detail")
        //        -> TextBox (focused, with detailVm DataContext)
        var detailVm = new DetailTestViewModel { ItemName = "Product 1" };
        var textBox = new TextBox { DataContext = detailVm };

        var detailHandler = new KeybindingHandler("Detail", textBox);
        var globalHandler = new KeybindingHandler("Global", detailHandler);

        // Focus the textBox
        FocusManager.SetFocus(textBox);
        Assert.Equal(textBox, FocusManager.CurrentFocused);

        // Test Scenario A: Press F12 while focused on TextBox
        // - TextBox does not handle F12
        // - Detail KeybindingHandler has no F12 binding, leaves e.Handled = false
        // - Global KeybindingHandler handles F12, receives detailVm as parameter, sets e.Handled = true
        var f12Args = new KeyEventArgs(Key.F12, 0, ModifierKeys.None, true);
        bool dispatchedF12 = FocusManager.DispatchKeyDown(f12Args, globalHandler);

        Assert.True(dispatchedF12);
        Assert.True(f12Args.Handled);
        Assert.Same(detailVm, globalExecutedTarget);
        Assert.Null(detailExecutedTarget);

        // Test Scenario B: Press Ctrl+S while focused on TextBox
        // - TextBox does not handle Ctrl+S
        // - Detail KeybindingHandler handles Ctrl+S with detailVm parameter, sets e.Handled = true
        // - Never reaches Global KeybindingHandler
        globalExecutedTarget = null;
        var ctrlSArgs = new KeyEventArgs(Key.S, 0, ModifierKeys.Control, true);
        bool dispatchedCtrlS = FocusManager.DispatchKeyDown(ctrlSArgs, globalHandler);

        Assert.True(dispatchedCtrlS);
        Assert.True(ctrlSArgs.Handled);
        Assert.Same(detailVm, detailExecutedTarget);
        Assert.Null(globalExecutedTarget);

        // Test Scenario C: Press unhandled key F1
        // - Neither Detail nor Global handles F1
        // - Event remains unhandled
        var f1Args = new KeyEventArgs(Key.F1, 0, ModifierKeys.None, true);
        bool dispatchedF1 = FocusManager.DispatchKeyDown(f1Args, globalHandler);

        Assert.False(dispatchedF1);
        Assert.False(f1Args.Handled);

        // Cleanup
        FocusManager.SetFocus(null);
    }

    [Fact]
    public void KeybindingHandler_FallsBackToSelfDataContext_WhenOriginalSourceHasNoDataContext()
    {
        KeybindingManager.Clear();

        object? executedTarget = null;
        var command = new AtelierRelayCommand(target =>
        {
            executedTarget = target;
        });
        KeybindingManager.RegisterKeybinding(new KeybindingDescriptor("Refresh", "Global", "F5", command));

        var globalVm = new GlobalTestViewModel { AppName = "AtelierApp" };
        var emptyElement = new FocusableTestElement(); // No DataContext

        var handler = new KeybindingHandler("Global", emptyElement)
        {
            DataContext = globalVm
        };

        FocusManager.SetFocus(emptyElement);
        var f5Args = new KeyEventArgs(Key.F5, 0, ModifierKeys.None, true);
        bool dispatched = FocusManager.DispatchKeyDown(f5Args, handler);

        Assert.True(dispatched);
        Assert.True(f5Args.Handled);
        Assert.Same(globalVm, executedTarget);

        FocusManager.SetFocus(null);
    }
}

public class DetailTestViewModel
{
    public string ItemName { get; set; } = string.Empty;
}

public class GlobalTestViewModel
{
    public string AppName { get; set; } = string.Empty;
}

public class FocusableTestElement : UIElement
{
    public FocusableTestElement()
    {
        IsFocusable = true;
    }
}
