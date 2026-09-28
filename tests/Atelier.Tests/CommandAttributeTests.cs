using System;
using System.Windows.Input;
using Atelier.Core.Keybinding;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Xunit;

namespace Atelier.Tests;

[Collection("KeybindingTests")]
public class CommandAttributeTests
{
    public CommandAttributeTests() => Atelier.Generated.GeneratedKeybindings.RegisterKeybindings();

    [Theory]
    [InlineData("ToggleTheme", "Toggle theme")]
    [InlineData("Save", "Save")]
    [InlineData("OpenURL", "Open URL")]
    [InlineData("OpenURLInBrowser", "Open URL in browser")]
    [InlineData("zoom_in", "Zoom in")]
    [InlineData("go-to-line", "Go to line")]
    [InlineData("Zoom100", "Zoom100")]
    [InlineData("", "")]
    public void LabelFromName_SplitsWordsAndKeepsAcronyms(string name, string label) =>
        Assert.Equal(label, KeybindingDescriptor.LabelFromName(name));

    [Fact]
    public void Descriptor_WithoutLabel_MakesOneFromTheName_AndHasNoDescriptionOrIcon()
    {
        var descriptor = new KeybindingDescriptor("NewWindow", "Global", "", new RelayCommand(() => { }));
        Assert.Equal("New window", descriptor.Label);
        Assert.Equal(string.Empty, descriptor.Description);
        Assert.Equal(string.Empty, descriptor.Icon);
        Assert.Equal(string.Empty, descriptor.Keybinding);
    }

    [Fact]
    public void CommandClass_WithoutShortcut_IsRegisteredWithItsPresentation()
    {
        var descriptor = KeybindingManager.FindCommand("Testing", "ShowLicense");
        Assert.NotNull(descriptor);
        Assert.Equal("License", descriptor!.Label);
        Assert.Equal("Show the version and the license", descriptor.Description);
        Assert.Equal("Info", descriptor.Icon);
        Assert.Equal(string.Empty, descriptor.Keybinding);
        Assert.IsType<ShowLicenseCommand>(descriptor.Command);
        Assert.Equal("Testing_ShowLicense", Atelier.Generated.GeneratedKeybindings.TestingShowLicenseKeybinding);

        // Any instance of the class is that command, e.g. one a button creates.
        Assert.Same(descriptor, KeybindingManager.FindCommand(new ShowLicenseCommand()));
    }

    [Fact]
    public void RelayCommandMethod_IsFoundFromTheViewModelsCommand()
    {
        var vm = new DocumentViewModel();
        var descriptor = KeybindingManager.FindCommand(vm.SaveCommand, targets: [vm]);
        Assert.NotNull(descriptor);
        Assert.Equal("Save", descriptor!.Name);
        Assert.Equal("Documents", descriptor.Group);
        Assert.Equal("Save the document", descriptor.Description);
        Assert.Equal("Save", descriptor.Icon);
        Assert.Equal("Ctrl+S", descriptor.Keybinding);
        Assert.Equal("Save", descriptor.Label); // made from the name

        Assert.True(KeybindingManager.TryExecuteGesture("Documents", "Ctrl+S", vm));
        Assert.Equal(1, vm.Saves);
    }

    [Fact]
    public void CommandProperty_AndKeybindingWithPresentation_AreRegistered()
    {
        var close = KeybindingManager.FindCommand("Documents", "Close");
        Assert.NotNull(close);
        Assert.Equal("Close document", close!.Label);
        Assert.Equal("Ctrl+W", close.Keybinding);

        var export = KeybindingManager.FindCommand("Documents", "Export");
        Assert.NotNull(export);
        Assert.Equal("Export as PDF", export!.Label);
        Assert.Equal("PictureAsPdf", export.Icon);
        Assert.Equal("Ctrl+Shift+E", export.Keybinding);
    }

    [Fact]
    public void AnotherAttributeNamedCommand_IsIgnored()
    {
        Assert.Null(KeybindingManager.FindCommand("Foreign", "Fake"));
    }

    [Fact]
    public void FindCommand_ByName_ReturnsNullForUnknownCommands()
    {
        Assert.Null(KeybindingManager.FindCommand("Testing", "NoSuchCommand"));
        Assert.Null(KeybindingManager.FindCommand("NoSuchGroup", "ShowLicense"));
    }
}

[Command("ShowLicense", "Testing", Label = "License", Description = "Show the version and the license", Icon = "Info")]
public class ShowLicenseCommand : AtelierCommand
{
    public override void Execute(object? parameter) { }
}

public partial class DocumentViewModel : ObservableObject
{
    public int Saves { get; private set; }

    [RelayCommand]
    [property: Command("Save", "Documents", Description = "Save the document", Icon = "Save", DefaultKeybinding = "Ctrl+S")]
    private void Save() => Saves++;

    [RelayCommand]
    [property: Keybinding("Export", "Documents", "Ctrl+Shift+E", Label = "Export as PDF", Icon = "PictureAsPdf")]
    private void Export() { }

    [Command("Close", "Documents", Label = "Close document", DefaultKeybinding = "Ctrl+W")]
    public ICommand CloseCommand { get; } = new RelayCommand(() => { });

    [Foreign.Command("Fake", "Foreign")]
    public ICommand FakeCommand { get; } = new RelayCommand(() => { });
}

public static class Foreign
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class CommandAttribute(string name, string group) : Attribute
    {
        public string Name { get; } = name;
        public string Group { get; } = group;
    }
}
