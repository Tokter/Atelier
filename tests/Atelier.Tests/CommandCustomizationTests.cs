using System;
using System.Linq;
using System.Text.Json;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Layout;
using CommunityToolkit.Mvvm.Input;
using Xunit;

namespace Atelier.Tests;

[Collection("KeybindingTests")]
public class CommandCustomizationTests : IDisposable
{
    private readonly RelayCommand _command = new(() => Runs++);
    private static int Runs;

    public CommandCustomizationTests()
    {
        KeybindingManager.ClearCustomizations();
        Runs = 0;
        KeybindingManager.RegisterOrUpdateKeybinding(
            new KeybindingDescriptor("Print", "Custom", "Ctrl+P", _command, label: "Print", description: "Print the page", icon: "Print"));
    }

    public void Dispose()
    {
        KeybindingManager.ClearCustomizations();
        KeybindingManager.UnregisterKeybinding("Custom", "Print");
        KeybindingManager.UnregisterKeybinding("Custom", "Later");
    }

    [Fact]
    public void Customization_ChangesTheRegisteredCommand_AndKeepsTheOriginal()
    {
        KeybindingManager.SetCustomization("Custom", "Print", new CommandCustomization(Label: "Print page", Icon: "PictureAsPdf", Keybinding: "shift+ctrl+p"));

        var descriptor = KeybindingManager.FindCommand("Custom", "Print")!;
        Assert.Equal("Print page", descriptor.Label);
        Assert.Equal("PictureAsPdf", descriptor.Icon);
        Assert.Equal("Ctrl+Shift+P", descriptor.Keybinding); // normalized
        Assert.Equal("Print the page", descriptor.Description);
        Assert.Same(_command, descriptor.Command);

        var original = KeybindingManager.GetDefault(descriptor);
        Assert.Equal("Print", original.Label);
        Assert.Equal("Ctrl+P", original.Keybinding);
    }

    [Fact]
    public void CustomizedShortcut_RunsTheCommand_AndTheOldOneNoLonger()
    {
        KeybindingManager.SetCustomization("Custom", "Print", new CommandCustomization(Keybinding: "Ctrl+Shift+P"));
        Assert.False(KeybindingManager.TryExecuteGesture("Custom", "Ctrl+P"));
        Assert.True(KeybindingManager.TryExecuteGesture("Custom", "Ctrl+Shift+P"));
        Assert.Equal(1, Runs);

        // An empty keybinding removes the shortcut.
        KeybindingManager.SetCustomization("Custom", "Print", new CommandCustomization(Keybinding: ""));
        Assert.False(KeybindingManager.TryExecuteGesture("Custom", "Ctrl+Shift+P"));
        Assert.Equal(string.Empty, KeybindingManager.FindCommand("Custom", "Print")!.Keybinding);
    }

    [Fact]
    public void Reset_RestoresTheDefaults()
    {
        KeybindingManager.SetCustomization("Custom", "Print", new CommandCustomization(Label: "Out"));
        KeybindingManager.ResetCustomization("Custom", "Print");
        var descriptor = KeybindingManager.FindCommand("Custom", "Print")!;
        Assert.IsNotType<CustomizedKeybindingDescriptor>(descriptor);
        Assert.Equal("Print", descriptor.Label);
        Assert.Null(KeybindingManager.GetCustomization("Custom", "Print"));

        // An empty customization is no customization.
        KeybindingManager.SetCustomization("Custom", "Print", new CommandCustomization());
        Assert.Empty(KeybindingManager.Customizations);
    }

    [Fact]
    public void InvalidKeybinding_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            KeybindingManager.SetCustomization("Custom", "Print", new CommandCustomization(Keybinding: "Ctrl+Nope")));
        Assert.Null(KeybindingManager.GetCustomization("Custom", "Print"));
    }

    [Fact]
    public void CommandsRegisteredLater_GetTheirCustomization()
    {
        KeybindingManager.SetCustomization("Custom", "Later", new CommandCustomization(Label: "Customized before"));
        KeybindingManager.RegisterOrUpdateKeybinding(new KeybindingDescriptor("Later", "Custom", "", new RelayCommand(() => { })));
        Assert.Equal("Customized before", KeybindingManager.FindCommand("Custom", "Later")!.Label);
    }

    [Fact]
    public void Changes_NotifyAndUpdateButtons()
    {
        var root = new StackPanel();
        var button = new Button { Command = _command };
        root.Add(button);
        root.AttachToHost();
        int customizationEvents = 0;
        EventHandler onChanged = (_, _) => customizationEvents++;
        KeybindingManager.CustomizationsChanged += onChanged;
        try
        {
            KeybindingManager.SetCustomization("Custom", "Print", new CommandCustomization(Label: "Print now", Keybinding: "F8"));
            Assert.Equal(1, customizationEvents);
            Assert.Equal("Print now", button.CommandInfo?.Label);
            Assert.Equal("Print the page (F8)", button.CommandToolTip);
            Assert.Equal("Print now", button.Children.OfType<StackPanel>().Single().Children.OfType<TextBlock>().Single().Text);

            // Setting the same customization again changes nothing.
            KeybindingManager.SetCustomization("Custom", "Print", new CommandCustomization(Label: "Print now", Keybinding: "F8"));
            Assert.Equal(1, customizationEvents);
        }
        finally
        {
            KeybindingManager.CustomizationsChanged -= onChanged;
            root.DetachFromHost();
        }
    }

    [Fact]
    public void Unregister_AcceptsTheOriginalOfACustomizedCommand()
    {
        var original = KeybindingManager.FindCommand("Custom", "Print")!;
        KeybindingManager.SetCustomization("Custom", "Print", new CommandCustomization(Label: "X"));
        Assert.True(KeybindingManager.UnregisterKeybinding(original));
        Assert.Null(KeybindingManager.FindCommand("Custom", "Print"));
    }

    [Fact]
    public void ExportAndImport_RoundTrip()
    {
        KeybindingManager.SetCustomization("Custom", "Print", new CommandCustomization(Label: "Print \"now\"", Keybinding: "Ctrl+K, Ctrl+P"));
        KeybindingManager.SetCustomization("Other", "Unregistered", new CommandCustomization(Icon: "<svg viewBox=\"0 0 24 24\"><circle cx=\"12\" cy=\"12\" r=\"6\"/></svg>"));
        string json = KeybindingManager.ExportCustomizations();

        KeybindingManager.ClearCustomizations();
        Assert.Equal("Print", KeybindingManager.FindCommand("Custom", "Print")!.Label);

        Assert.Equal(2, KeybindingManager.ImportCustomizations(json));
        Assert.Equal("Print \"now\"", KeybindingManager.FindCommand("Custom", "Print")!.Label);
        Assert.Equal("Ctrl+K, Ctrl+P", KeybindingManager.FindCommand("Custom", "Print")!.Keybinding);
        Assert.StartsWith("<svg", KeybindingManager.GetCustomization("Other", "Unregistered")!.Icon);
        Assert.Null(KeybindingManager.GetCustomization("Custom", "Print")!.Icon); // unchanged values stay unset
    }

    [Fact]
    public void Import_SkipsInvalidEntries_AndCanAdd()
    {
        KeybindingManager.SetCustomization("Custom", "Kept", new CommandCustomization(Label: "Kept"));
        const string json = """
            { "commands": [
                { "group": "Custom", "name": "Print", "keybinding": "F9" },
                { "group": "Custom", "name": "Broken", "keybinding": "Ctrl+Nope" },
                { "name": "NoGroup", "label": "x" },
                { "group": "Custom", "name": "Empty" },
                42
            ] }
            """;
        Assert.Equal(1, KeybindingManager.ImportCustomizations(json, replace: false));
        Assert.Equal("F9", KeybindingManager.FindCommand("Custom", "Print")!.Keybinding);
        Assert.NotNull(KeybindingManager.GetCustomization("Custom", "Kept"));

        Assert.ThrowsAny<JsonException>(() => KeybindingManager.ImportCustomizations("{ not json"));
    }
}
