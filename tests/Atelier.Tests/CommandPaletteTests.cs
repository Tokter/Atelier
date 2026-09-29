using System;
using System.Linq;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Tree;
using Atelier.Layout;
using CommunityToolkit.Mvvm.Input;
using Xunit;

namespace Atelier.Tests;

[Collection("KeybindingTests")]
public class CommandPaletteTests : IDisposable
{
    private static readonly (string Name, string Label, string Description)[] Specs =
    [
        ("ToggleTheme", "Toggle theme", "Switch between light and dark"),
        ("NewWindow", "New window", "Open another window"),
        ("Themes", "Theme editor", ""),
        ("Export", "Export", "Save as PDF for printing"),
        ("Disabled", "Archive", "Can't run now"),
    ];

    private int _runs;
    private string? _lastRun;

    public CommandPaletteTests()
    {
        KeybindingManager.ClearCustomizations();
        foreach (var (name, label, description) in Specs)
        {
            var command = new RelayCommand(() => { _runs++; _lastRun = name; }, () => name != "Disabled");
            KeybindingManager.RegisterOrUpdateKeybinding(new KeybindingDescriptor(name, "Palette", "", command, label: label, description: description));
        }
    }

    public void Dispose()
    {
        foreach (var (name, _, _) in Specs) KeybindingManager.UnregisterKeybinding("Palette", name);
    }

    private static CommandPalette Palette() => new(KeybindingHandler.GetActiveCommands(new KeybindingHandler("Palette", null)));

    private static string[] Labels(CommandPalette palette) => palette.Results.Select(c => c.Descriptor.Label).ToArray();

    [Fact]
    public void Search_RanksPrefixesWordStartsInitialsAndDescriptions()
    {
        var palette = Palette();
        Assert.Equal(5, palette.Results.Count);

        palette.SearchText = "theme";
        Assert.Equal(["Theme editor", "Toggle theme"], Labels(palette)); // prefix before word start

        palette.SearchText = "tt";
        Assert.Equal("Toggle theme", Labels(palette)[0]); // initials

        palette.SearchText = "nwin";
        Assert.Equal(["New window"], Labels(palette)); // letters in order

        palette.SearchText = "pdf";
        Assert.Equal(["Export"], Labels(palette)); // description

        palette.SearchText = "zzz";
        Assert.Empty(palette.Results);
        Assert.Null(palette.HighlightedCommand);
    }

    [Fact]
    public void Highlight_SkipsCommandsThatCantRun_AndEnterRunsIt()
    {
        var palette = Palette();
        palette.SearchText = "ar"; // "Archive" (disabled) first, then others containing the letters
        Assert.Equal("Archive", Labels(palette)[0]);
        Assert.NotEqual("Disabled", palette.HighlightedCommand?.Descriptor.Name);

        palette.SearchText = "";
        var first = palette.HighlightedCommand!;
        palette.MoveHighlight(1);
        Assert.NotSame(first, palette.HighlightedCommand);
        palette.MoveHighlight(-10);
        Assert.Same(first, palette.HighlightedCommand);

        ActiveCommand? executed = null;
        palette.CommandExecuted += (_, c) => executed = c;
        var enter = new KeyEventArgs(Key.Enter);
        palette.OnPreviewKeyDown(enter);
        Assert.True(enter.Handled);
        Assert.Equal(1, _runs);
        Assert.Same(first, executed);
    }

    [Fact]
    public void RecentlyRunCommands_ComeFirst()
    {
        var palette = Palette();
        palette.SearchText = "export";
        Assert.True(palette.ExecuteHighlighted());
        Assert.Equal("Export", _lastRun);

        Assert.Equal("Export", Labels(Palette())[0]);
    }

    [Fact]
    public void Show_OpensWithTheFocusInTheSearch_AndRunsWithTheFocusBack()
    {
        var field = new TextBox();
        var panel = new StackPanel();
        panel.Add(field);
        var root = new KeybindingHandler("Palette", panel);
        root.AttachToHost();
        try
        {
            field.Focus();
            var palette = CommandPalette.Show(field, c => c.Descriptor.Name != "NewWindow");
            Assert.Contains(PopupManager.ActivePopups, p => p.Child == palette);
            Assert.True(palette.SearchBox.IsFocused);
            Assert.DoesNotContain(palette.Commands, c => c.Descriptor.Name == "NewWindow");

            palette.SearchText = "toggle";
            FocusManager.DispatchKeyDown(new KeyEventArgs(Key.Enter), root);
            Assert.Equal("ToggleTheme", _lastRun);
            Assert.DoesNotContain(PopupManager.ActivePopups, p => p.Child == palette);
            Assert.True(field.IsFocused);

            // Escape closes it without running anything.
            var again = CommandPalette.Show(field);
            PopupManager.HandleKeyDown(new KeyEventArgs(Key.Escape), root);
            Assert.DoesNotContain(PopupManager.ActivePopups, p => p.Child == again);
            Assert.Equal(1, _runs);
        }
        finally
        {
            root.DetachFromHost();
        }
    }
}
