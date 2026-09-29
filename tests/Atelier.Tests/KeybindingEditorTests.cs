using System;
using System.Collections.Generic;
using System.Linq;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;
using CommunityToolkit.Mvvm.Input;
using Xunit;

namespace Atelier.Tests;

public class ShortcutControlTests
{
    private static IEnumerable<T> Descendants<T>(VisualNode node) where T : VisualNode
    {
        foreach (var child in node.Children)
        {
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    [Fact]
    public void ShortcutView_ShowsKeyCapsPerStroke()
    {
        var view = new ShortcutView("Ctrl+K, Ctrl+Shift+C");
        var texts = Descendants<TextBlock>(view).Select(t => t.Text).ToArray();
        Assert.Equal(["Ctrl", "K", "then", "Ctrl", "Shift", "C"], texts);
        Assert.Equal(5, view.Children.OfType<Border>().Count());

        view.Shortcut = "D1";
        Assert.Equal(["1"], Descendants<TextBlock>(view).Select(t => t.Text));
        view.Shortcut = "";
        Assert.Empty(view.Children);
    }

    [Fact]
    public void Recorder_RecordsAStroke_AcceptedWithEnter()
    {
        var recorder = new ShortcutRecorder { Shortcut = "Ctrl+P" };
        string? recorded = null;
        recorder.ShortcutRecorded += (_, s) => recorded = s;
        recorder.StartRecording();
        Assert.True(recorder.IsRecording);

        var ctrl = new KeyEventArgs(Key.LeftCtrl, modifiers: ModifierKeys.Control);
        recorder.OnKeyDown(ctrl);
        Assert.True(ctrl.Handled);
        Assert.Null(recorded); // modifiers alone record nothing

        var key = new KeyEventArgs(Key.T, modifiers: ModifierKeys.Control | ModifierKeys.Shift);
        recorder.OnKeyDown(key);
        Assert.True(key.Handled); // the window's own Ctrl+Shift+T doesn't run
        Assert.True(recorder.IsRecording);
        Assert.Equal("Ctrl+P", recorder.Shortcut); // not accepted yet

        recorder.OnKeyDown(new KeyEventArgs(Key.Enter));
        Assert.False(recorder.IsRecording);
        Assert.Equal("Ctrl+Shift+T", recorded);
        Assert.Equal("Ctrl+Shift+T", recorder.Shortcut);
    }

    [Fact]
    public void Recorder_RecordsAChord_AndEscapeCancels()
    {
        var recorder = new ShortcutRecorder();
        recorder.StartRecording();
        recorder.OnKeyDown(new KeyEventArgs(Key.K, modifiers: ModifierKeys.Control));
        recorder.OnKeyDown(new KeyEventArgs(Key.C, modifiers: ModifierKeys.Control));
        Assert.False(recorder.IsRecording);
        Assert.Equal("Ctrl+K, Ctrl+C", recorder.Shortcut);

        recorder.StartRecording();
        recorder.OnKeyDown(new KeyEventArgs(Key.F2));
        recorder.OnKeyDown(new KeyEventArgs(Key.Escape));
        Assert.False(recorder.IsRecording);
        Assert.Equal("Ctrl+K, Ctrl+C", recorder.Shortcut);
    }

    [Fact]
    public void Dialog_DoesNotCloseOnKeysItsContentHandled()
    {
        var host = new DialogHost { Content = new Border() };
        var recorder = new ShortcutRecorder();
        var dialog = new Dialog("Shortcut") { Content = recorder }.AddButton("OK", DialogResult.Ok, isDefault: true);
        _ = dialog.ShowAsync(host);
        Assert.Same(dialog, host.Dialog);

        // Escape and Enter while recording bubble from the recorder to the dialog already handled.
        recorder.StartRecording();
        foreach (var key in new[] { Key.F2, Key.Escape })
        {
            var e = new KeyEventArgs(key);
            recorder.OnKeyDown(e);
            dialog.OnKeyDown(e);
        }
        Assert.Same(dialog, host.Dialog);

        dialog.OnKeyDown(new KeyEventArgs(Key.Escape));
        Assert.Null(host.Dialog);
    }

    [Fact]
    public void Recorder_StartsWithEnterOrSpace_AndIgnoresKeysOtherwise()
    {
        var recorder = new ShortcutRecorder();
        var other = new KeyEventArgs(Key.A);
        recorder.OnKeyDown(other);
        Assert.False(other.Handled);
        Assert.False(recorder.IsRecording);

        recorder.OnKeyDown(new KeyEventArgs(Key.Space));
        Assert.True(recorder.IsRecording);
        recorder.OnLostFocus();
        Assert.False(recorder.IsRecording);
    }
}

[Collection("KeybindingTests")]
public class KeybindingEditorTests : IDisposable
{
    private readonly RelayCommand _print = new(() => { });
    private readonly RelayCommand _preview = new(() => { });

    public KeybindingEditorTests()
    {
        KeybindingManager.ClearCustomizations();
        KeybindingManager.RegisterOrUpdateKeybinding(new KeybindingDescriptor("Print", "EditorTest", "Ctrl+P", _print,
            label: "_Print", description: "Print the page", icon: "Print"));
        KeybindingManager.RegisterOrUpdateKeybinding(new KeybindingDescriptor("Preview", "EditorTest", "Ctrl+Shift+P", _preview,
            label: "Print preview", icon: "Preview"));
    }

    public void Dispose()
    {
        KeybindingManager.ClearCustomizations();
        KeybindingManager.UnregisterKeybinding("EditorTest", "Print");
        KeybindingManager.UnregisterKeybinding("EditorTest", "Preview");
    }

    private static IEnumerable<T> Descendants<T>(VisualNode node) where T : VisualNode
    {
        foreach (var child in node.Children)
        {
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }

    private static Button ButtonWith(KeybindingEditor editor, string text) =>
        Descendants<Button>(editor).Single(b => b.Content as string == text);

    // Enter clicks a button on key-down.
    private static void Click(Button button) => button.OnKeyDown(new KeyEventArgs(Key.Enter));

    private static KeybindingEditor AttachedEditor(out StackPanel root)
    {
        var editor = new KeybindingEditor();
        root = new StackPanel();
        root.Add(editor);
        root.AttachToHost();
        return editor;
    }

    [Fact]
    public void ListsTheCommands_AndSearchFilters()
    {
        var editor = new KeybindingEditor();
        var labels = editor.VisibleRows.Select(r => Descendants<TextBlock>(r).First().Text).ToList();
        Assert.Contains("Print", labels); // the access key marker is not shown
        Assert.Contains("Print preview", labels);

        editor.SearchText = "preview";
        var visible = editor.VisibleRows.Select(r => Descendants<TextBlock>(r).First().Text).ToList();
        Assert.Equal(["Print preview"], visible);

        editor.SearchText = "Ctrl+P"; // shortcuts are searched too
        Assert.Contains("Print", editor.VisibleRows.Select(r => Descendants<TextBlock>(r).First().Text));
    }

    [Fact]
    public void GroupList_ListsOneGroup_TheChangedCommands_OrAll()
    {
        var editor = AttachedEditor(out var root);
        try
        {
            string Label(Button row) => Descendants<TextBlock>(row).First().Text;
            int all = editor.VisibleRows.Count();

            editor.ShowGroup("EditorTest");
            Assert.Equal("EditorTest", editor.SelectedGroup);
            Assert.Equal(["Print", "Print preview"], editor.VisibleRows.Select(Label).OrderBy(l => l));

            // The group list names the groups readably and counts their commands.
            var entry = Descendants<Button>(editor).First(b => Descendants<TextBlock>(b).FirstOrDefault()?.Text == "Editor test");
            Assert.Equal("2", Descendants<TextBlock>(entry).Last().Text);

            editor.ShowChangedCommands();
            Assert.True(editor.ShowsChangedCommandsOnly);
            Assert.Empty(editor.VisibleRows);
            KeybindingManager.SetCustomization("EditorTest", "Preview", new CommandCustomization(Keybinding: "F6"));
            Assert.Equal(["Print preview"], editor.VisibleRows.Select(Label));

            editor.ShowGroup(null);
            Assert.Equal(all, editor.VisibleRows.Count());
        }
        finally
        {
            root.DetachFromHost();
        }
    }

    [Fact]
    public void Select_ListsTheGroupOfACommandTheListDoesntShow()
    {
        var editor = new KeybindingEditor();
        editor.ShowGroup("Global");
        Assert.True(editor.Select("EditorTest", "Preview"));
        Assert.Equal("EditorTest", editor.SelectedGroup);
        Assert.Contains(editor.VisibleRows, r => r.Variant == ButtonVariant.Tonal);
    }

    [Fact]
    public void EditingTheLabelAndIcon_CustomizesTheCommand()
    {
        var editor = AttachedEditor(out var root);
        try
        {
            Assert.True(editor.Select("EditorTest", "Print"));
            Assert.Equal("_Print", editor.LabelBox.Text);

            editor.LabelBox.Text = "_Print page";
            Assert.Equal("_Print page", KeybindingManager.GetCustomization("EditorTest", "Print")?.Label);
            Assert.Equal("_Print page", editor.LabelBox.Text); // kept while typing

            editor.IconBox.Text = "not an icon";
            Assert.True(Validation.GetHasError(editor.IconBox));
            Assert.Null(KeybindingManager.GetCustomization("EditorTest", "Print")?.Icon);

            editor.IconBox.Text = "PictureAsPdf";
            Assert.False(Validation.GetHasError(editor.IconBox));
            Assert.Equal("PictureAsPdf", KeybindingManager.FindCommand("EditorTest", "Print")!.Icon);

            // Typing the default again removes the change.
            editor.LabelBox.Text = "_Print";
            editor.IconBox.Text = "Print";
            Assert.Null(KeybindingManager.GetCustomization("EditorTest", "Print"));
        }
        finally
        {
            root.DetachFromHost();
        }
    }

    [Fact]
    public void RecordingAShortcut_CustomizesIt_AndShowsConflicts()
    {
        var editor = AttachedEditor(out var root);
        try
        {
            editor.Select("EditorTest", "Print");
            Assert.Equal(string.Empty, editor.ConflictText);

            editor.ShortcutRecorder.StartRecording();
            editor.ShortcutRecorder.OnKeyDown(new KeyEventArgs(Key.P, modifiers: ModifierKeys.Control | ModifierKeys.Shift));
            editor.ShortcutRecorder.OnKeyDown(new KeyEventArgs(Key.Enter));
            Assert.Equal("Ctrl+Shift+P", KeybindingManager.FindCommand("EditorTest", "Print")!.Keybinding);
            Assert.Contains("Print preview", editor.ConflictText);

            // Removing the shortcut, then resetting the command.
            Click(ButtonWith(editor, "Remove"));
            Assert.Equal(string.Empty, KeybindingManager.FindCommand("EditorTest", "Print")!.Keybinding);
            Assert.Equal(string.Empty, editor.ConflictText);

            var reset = ButtonWith(editor, "Reset to default");
            Assert.True(reset.IsEnabled);
            Click(reset);
            Assert.Null(KeybindingManager.GetCustomization("EditorTest", "Print"));
            Assert.Equal("Ctrl+P", editor.ShortcutRecorder.Shortcut);
            Assert.False(reset.IsEnabled);
        }
        finally
        {
            root.DetachFromHost();
        }
    }

    [Fact]
    public void ChangesMadeElsewhere_UpdateTheEditor_AndResetAllClearsThem()
    {
        var editor = AttachedEditor(out var root);
        try
        {
            editor.Select("EditorTest", "Preview");
            KeybindingManager.SetCustomization("EditorTest", "Preview", new CommandCustomization(Label: "Preview", Keybinding: "F6"));
            Assert.Equal("Preview", editor.LabelBox.Text);
            Assert.Equal("F6", editor.ShortcutRecorder.Shortcut);

            Click(ButtonWith(editor, "Reset all"));
            Assert.Empty(KeybindingManager.Customizations);
            Assert.Equal("Print preview", editor.LabelBox.Text);
        }
        finally
        {
            root.DetachFromHost();
        }
    }
}
