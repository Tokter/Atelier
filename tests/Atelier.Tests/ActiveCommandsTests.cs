using System;
using System.Linq;
using System.Windows.Input;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using CommunityToolkit.Mvvm.Input;
using Xunit;

namespace Atelier.Tests;

[Collection("KeybindingTests")]
public class ActiveCommandsTests : IDisposable
{
    private sealed class DocumentVm
    {
        public int Renames;
        public ICommand RenameCommand { get; }
        public DocumentVm() => RenameCommand = new RelayCommand(() => Renames++);
    }

    private sealed class PageVm
    {
        public int Refreshes;
        public bool CanRefresh = true;
        public ICommand RefreshCommand { get; }
        public PageVm() => RefreshCommand = new RelayCommand(() => Refreshes++, () => CanRefresh);
    }

    private readonly RelayCommand _save = new(() => { });
    private readonly RelayCommand _innerSave = new(() => { });
    private readonly RelayCommand _bold = new(() => { });

    public ActiveCommandsTests()
    {
        KeybindingManager.ClearCustomizations();
        KeybindingManager.RegisterOrUpdateKeybinding(new KeybindingDescriptor("Save", "Outer", "Ctrl+S", _save));
        KeybindingManager.RegisterOrUpdateKeybinding(new KeybindingDescriptor("QuickSave", "Inner", "Ctrl+S", _innerSave));
        KeybindingManager.RegisterOrUpdateKeybinding(new KeybindingDescriptor("Bold", "Inner", "Ctrl+B", _bold));
        KeybindingManager.RegisterOrUpdateKeybinding(new KeybindingDescriptor("Rename", "Inner", "F2",
            new PropertyKeybindingCommand<DocumentVm>("Rename", vm => vm.RenameCommand)));
        KeybindingManager.RegisterOrUpdateKeybinding(new KeybindingDescriptor("Refresh", "Page", "F5",
            new PropertyKeybindingCommand<PageVm>("Refresh", vm => vm.RefreshCommand)));
        KeybindingManager.RegisterOrUpdateKeybinding(new KeybindingDescriptor("Reload", "Page", "Ctrl+K, Ctrl+R",
            new PropertyKeybindingCommand<PageVm>("Reload", vm => vm.RefreshCommand)));
    }

    public void Dispose()
    {
        foreach (var (group, name) in new[] { ("Outer", "Save"), ("Inner", "QuickSave"), ("Inner", "Bold"), ("Inner", "Rename"), ("Page", "Refresh"), ("Page", "Reload") })
        {
            KeybindingManager.UnregisterKeybinding(group, name);
        }
    }

    // Outer handler (window) > panel > inner handler (document view) > text box, and a sibling outside the inner handler.
    private static (KeybindingHandler Outer, KeybindingHandler Inner, TextBox Field, Border Outside, DocumentVm Document) Tree()
    {
        var document = new DocumentVm();
        var field = new TextBox();
        var inner = new KeybindingHandler("Inner", field) { DataContext = document };
        var outside = new Border();
        var panel = new StackPanel();
        panel.Add(inner);
        panel.Add(outside);
        var outer = new KeybindingHandler("Outer", panel);
        return (outer, inner, field, outside, document);
    }

    private static string Id(ActiveCommand c) => $"{c.Descriptor.Group}/{c.Descriptor.Name}";

    [Fact]
    public void ListsTheGroupsFromTheFocusUp_InnermostFirst()
    {
        var (outer, inner, field, outside, document) = Tree();
        var commands = KeybindingHandler.GetActiveCommands(field);
        Assert.Equal(["Inner/Bold", "Inner/QuickSave", "Inner/Rename", "Outer/Save"], commands.Select(Id).OrderBy(s => s.StartsWith("Outer")).ThenBy(s => s));
        Assert.True(commands.TakeWhile(c => c.Descriptor.Group == "Inner").Count() == 3); // inner group first

        var rename = commands.Single(c => c.Descriptor.Name == "Rename");
        Assert.Same(document, rename.Target);
        Assert.Same(inner, rename.Handler);

        // Outside the inner handler only the outer group applies.
        Assert.Equal(["Outer/Save"], KeybindingHandler.GetActiveCommands(outside).Select(Id));
    }

    [Fact]
    public void AnInnerCommandWithTheSameShortcut_ShadowsTheOuterOne()
    {
        var (_, _, field, _, _) = Tree();
        var commands = KeybindingHandler.GetActiveCommands(field);
        var save = commands.Single(c => c.Descriptor.Name == "Save");
        var quickSave = commands.Single(c => c.Descriptor.Name == "QuickSave");
        Assert.Same(quickSave, save.ShadowedBy);
        Assert.Null(quickSave.ShadowedBy);
    }

    [Fact]
    public void ViewModelCommands_AreOnlyListedWhereTheirViewModelIs()
    {
        var field = new TextBox();
        var handler = new KeybindingHandler("Inner", field); // no DocumentVm around
        var ids = KeybindingHandler.GetActiveCommands(field).Select(Id).ToList();
        Assert.Contains("Inner/Bold", ids);
        Assert.DoesNotContain("Inner/Rename", ids);
    }

    [Fact]
    public void ActiveCommand_RunsOnItsTarget()
    {
        var (_, _, field, _, document) = Tree();
        var rename = KeybindingHandler.GetActiveCommands(field).Single(c => c.Descriptor.Name == "Rename");
        Assert.True(rename.CanExecute);
        Assert.True(rename.TryExecute());
        Assert.Equal(1, document.Renames);
    }

    [Fact]
    public void AdditionalScope_ListsAndRunsItsGroup_OnItsTarget_WhereverTheFocusIs()
    {
        var (outer, _, field, outside, _) = Tree();
        var page = new PageVm();
        var scope = new KeybindingScope("Page", page);
        outer.AdditionalScopes.Add(scope);

        var refresh = KeybindingHandler.GetActiveCommands(outside).Single(c => c.Descriptor.Name == "Refresh");
        Assert.Same(page, refresh.Target);
        Assert.Same(outer, refresh.Handler);

        // Single keys and chords of the scope run from the handler, with the focus in the document view.
        outer.AttachToHost();
        try
        {
            field.Focus();
            FocusManager.DispatchKeyDown(new KeyEventArgs(Key.F5), outer);
            Assert.Equal(1, page.Refreshes);
            var first = new KeyEventArgs(Key.K, modifiers: ModifierKeys.Control);
            FocusManager.DispatchKeyDown(first, outer);
            Assert.True(first.Handled); // the chord started
            FocusManager.DispatchKeyDown(new KeyEventArgs(Key.R, modifiers: ModifierKeys.Control), outer);
            Assert.Equal(2, page.Refreshes);
        }
        finally
        {
            outer.DetachFromHost();
        }

        // A disabled command is still listed, but doesn't run.
        page.CanRefresh = false;
        refresh = KeybindingHandler.GetActiveCommands(outside).Single(c => c.Descriptor.Name == "Refresh");
        Assert.False(refresh.CanExecute);
        Assert.False(refresh.TryExecute());

        // The scope follows changes, e.g. another page.
        scope.Group = "";
        Assert.DoesNotContain(KeybindingHandler.GetActiveCommands(outside), c => c.Descriptor.Group == "Page");
    }

    [Fact]
    public void TheWalk_ContinuesFromAPopupToItsOwner()
    {
        var (outer, _, field, _, document) = Tree();
        outer.AttachToHost();
        var inPopup = new TextBox();
        var popup = new Popup { PlacementTarget = field, Child = inPopup };
        try
        {
            popup.IsOpen = true;
            var ids = KeybindingHandler.GetActiveCommands(inPopup).Select(Id).ToList();
            Assert.Contains("Inner/Bold", ids);
            Assert.Contains("Outer/Save", ids);
        }
        finally
        {
            popup.IsOpen = false;
            outer.DetachFromHost();
        }
    }
}
