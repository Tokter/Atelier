using System;
using System.Linq;
using System.Threading;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;

namespace Atelier.Tests;

[Collection("KeybindingTests")]
public class KeybindingChordTests
{
    private static KeybindingDescriptor Register(string group, string name, string gesture, Action onExecute)
    {
        var descriptor = new KeybindingDescriptor(name, group, gesture, new AtelierRelayCommand(onExecute));
        KeybindingManager.RegisterKeybinding(descriptor);
        return descriptor;
    }

    private static KeyEventArgs Press(Key key, ModifierKeys modifiers = ModifierKeys.Control) => new(key, modifiers: modifiers);

    [Fact]
    public void Parser_DistinguishesChordsFromCommaSeparatedTokens()
    {
        Assert.True(KeybindingGesture.TryParseSequence("Ctrl+K, Ctrl+C", out var chord));
        Assert.Equal(new[] { new KeybindingGesture(Key.K, ModifierKeys.Control), new KeybindingGesture(Key.C, ModifierKeys.Control) }, chord);
        Assert.False(KeybindingGesture.TryParse("Ctrl+K, Ctrl+C", out _));

        Assert.True(KeybindingGesture.TryParse("Shift, Control+L", out var single));
        Assert.Equal(new KeybindingGesture(Key.L, ModifierKeys.Control | ModifierKeys.Shift), single);

        Assert.Equal("Ctrl+K, Ctrl+Shift+C", KeybindingGesture.Normalize("control+k,  shift+ctrl+c"));
    }

    [Fact]
    public void Chord_ExecutesAfterAllStrokes_AndReportsPendingState()
    {
        const string group = "ChordExec";
        int executed = 0;
        var descriptor = Register(group, "Comment", "Ctrl+K, Ctrl+C", () => executed++);
        try
        {
            var handler = new KeybindingHandler(group);

            var first = Press(Key.K);
            handler.OnKeyDown(first);
            Assert.True(first.Handled);
            Assert.Equal("Ctrl+K", handler.PendingChord);
            Assert.Equal(0, executed);

            handler.OnKeyDown(Press(Key.C));
            Assert.Equal(1, executed);
            Assert.Null(handler.PendingChord);
        }
        finally
        {
            KeybindingManager.UnregisterKeybinding(descriptor);
        }
    }

    [Fact]
    public void NonContinuingKey_CancelsChord_AndIsHandledOnItsOwn()
    {
        const string group = "ChordCancel";
        int chord = 0, cut = 0;
        var chordDescriptor = Register(group, "Comment", "Ctrl+K, Ctrl+C", () => chord++);
        var cutDescriptor = Register(group, "Cut", "Ctrl+X", () => cut++);
        try
        {
            var handler = new KeybindingHandler(group);

            handler.OnKeyDown(Press(Key.K));
            var second = Press(Key.X);
            handler.OnKeyDown(second);

            Assert.Equal(0, chord);
            Assert.Equal(1, cut);
            Assert.True(second.Handled);
            Assert.Null(handler.PendingChord);
        }
        finally
        {
            KeybindingManager.UnregisterKeybinding(chordDescriptor);
            KeybindingManager.UnregisterKeybinding(cutDescriptor);
        }
    }

    [Fact]
    public void ModifierKeyPress_DoesNotCancelPendingChord()
    {
        const string group = "ChordModifier";
        int executed = 0;
        var descriptor = Register(group, "Comment", "Ctrl+K, Ctrl+C", () => executed++);
        try
        {
            var handler = new KeybindingHandler(group);

            handler.OnKeyDown(Press(Key.K));
            handler.OnKeyDown(Press(Key.LeftCtrl));
            handler.OnKeyDown(Press(Key.C));

            Assert.Equal(1, executed);
        }
        finally
        {
            KeybindingManager.UnregisterKeybinding(descriptor);
        }
    }

    [Fact]
    public void PendingChord_ExpiresAfterTimeout()
    {
        const string group = "ChordTimeout";
        int executed = 0;
        var descriptor = Register(group, "Comment", "Ctrl+K, Ctrl+C", () => executed++);
        var previousTimeout = KeybindingHandler.ChordTimeout;
        try
        {
            KeybindingHandler.ChordTimeout = TimeSpan.FromMilliseconds(1);
            var handler = new KeybindingHandler(group);

            handler.OnKeyDown(Press(Key.K));
            Thread.Sleep(20);
            handler.OnKeyDown(Press(Key.C));

            Assert.Equal(0, executed);
        }
        finally
        {
            KeybindingHandler.ChordTimeout = previousTimeout;
            KeybindingManager.UnregisterKeybinding(descriptor);
        }
    }

    [Fact]
    public void GetConflicts_ReportsDuplicatesAndShadowedChords()
    {
        const string group = "ConflictGroup";
        var single = Register(group, "Kill", "Ctrl+K", () => { });
        var chord = Register(group, "Comment", "Ctrl+K, Ctrl+C", () => { });
        var duplicateA = Register(group, "SaveA", "Ctrl+S", () => { });
        var duplicateB = Register(group, "SaveB", "Ctrl+S", () => { });
        try
        {
            var conflicts = KeybindingManager.GetConflicts().Where(c => c.Group == group).ToList();

            Assert.Contains(conflicts, c => c.First == single && c.Second == chord && c.IsPrefix);
            Assert.Contains(conflicts, c => c.First == duplicateA && c.Second == duplicateB && !c.IsPrefix);
            Assert.Equal(2, conflicts.Count);
        }
        finally
        {
            foreach (var d in new[] { single, chord, duplicateA, duplicateB })
            {
                KeybindingManager.UnregisterKeybinding(d);
            }
        }
    }

    [Fact]
    public void SingleStrokeLookup_DoesNotMatchChords()
    {
        const string group = "ChordSingleLookup";
        var descriptor = Register(group, "Comment", "Ctrl+K, Ctrl+C", () => { });
        try
        {
            Assert.Null(KeybindingManager.FindKeybinding(group, Key.C, ModifierKeys.Control));
            Assert.Null(KeybindingManager.FindKeybinding(group, Key.K, ModifierKeys.Control));
        }
        finally
        {
            KeybindingManager.UnregisterKeybinding(descriptor);
        }
    }
}
