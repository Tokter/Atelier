using System;
using System.Collections.Generic;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// A control that intercepts bubbling keyboard events in the visual tree and executes
/// matching keybindings from a specified <see cref="KeybindingManager"/> group.
/// </summary>
/// <remarks>
/// <para>
/// Multi-stroke chords such as <c>"Ctrl+K, Ctrl+C"</c> are supported: a key press that starts a registered chord is
/// consumed and remembered (see <see cref="PendingChord"/>) until the chord completes, a key that doesn't continue it is
/// pressed (that key is then handled on its own), or <see cref="ChordTimeout"/> passes. Pressing a modifier key by itself
/// never affects a pending chord.
/// </para>
/// <para>
/// Pointer gestures work like keys: clicks (<c>"RightClick"</c>), double clicks, wheel turns (<c>"Ctrl+WheelUp"</c>) and
/// drags (<c>"MiddleDrag"</c>, which run an <see cref="IDragCommand"/>) that reach the handler run the keybindings bound
/// to them; see <see cref="OnPointerPressed"/>.
/// </para>
/// <para>
/// The command's target is resolved in order: the <see cref="BindableObject.DataContext"/> of the element the key event
/// originated from, the distinct data contexts of its ancestors up to this handler, then this handler's own data context
/// (or <c>null</c> when there is none).
/// </para>
/// <para>
/// Besides <see cref="Group"/>, the handler runs the groups of its <see cref="AdditionalScopes"/>, which can have a fixed
/// target (such as the view model of the page a window shows). <see cref="GetActiveCommands"/> lists the commands the
/// handlers around an element run.
/// </para>
/// </remarks>
public partial class KeybindingHandler : ContentControl
{
    /// <summary>Identifies the <see cref="Group"/> bindable property.</summary>
    public static readonly BindableProperty<string> GroupProperty =
        BindableProperty.Register<KeybindingHandler, string>(
            nameof(Group),
            string.Empty);

    /// <summary>
    /// Gets or sets how long a started chord waits for its next stroke. The default is 3 seconds.
    /// </summary>
    public static TimeSpan ChordTimeout { get; set; } = TimeSpan.FromSeconds(3);

    private readonly List<KeybindingGesture> _pendingStrokes = [];
    private long _pendingSinceMs;

    /// <summary>
    /// Gets or sets the keybinding group name to match against (e.g. "Global", "Detail").
    /// </summary>
    public string Group
    {
        get => GetValue(GroupProperty);
        set => SetValue(GroupProperty, value);
    }

    /// <summary>
    /// Gets further groups the handler runs after <see cref="Group"/>, each optionally for a fixed target. Their groups
    /// and targets can change at any time (e.g. when a window shows another page).
    /// </summary>
    public IList<KeybindingScope> AdditionalScopes { get; } = new List<KeybindingScope>();

    /// <summary>
    /// Gets the strokes of a chord that has been started but not completed (e.g. <c>"Ctrl+K"</c>), or <c>null</c>.
    /// Useful for showing a "waiting for second key" hint. Becomes <c>null</c> once <see cref="ChordTimeout"/> has passed.
    /// </summary>
    public string? PendingChord =>
        _pendingStrokes.Count == 0 || IsChordExpired(Environment.TickCount64) ? null : KeybindingGesture.FormatSequence(_pendingStrokes);

    private bool IsChordExpired(long nowMs) => nowMs - _pendingSinceMs > (long)ChordTimeout.TotalMilliseconds;

    /// <summary>Initializes a new handler without a group.</summary>
    public KeybindingHandler()
    {
    }

    /// <summary>Initializes a new handler for <paramref name="group"/> wrapping <paramref name="content"/>.</summary>
    public KeybindingHandler(string group, object? content = null)
    {
        Group = group;
        Content = content;
    }

    /// <summary>Creates a handler for <paramref name="group"/> wrapping <paramref name="content"/>.</summary>
    public static KeybindingHandler Create(string group, object? content = null) => new(group, content);

    /// <inheritdoc/>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!e.Handled && HandleDragKey(e))
            return;
        if (e.Handled || e.Key == Key.None || IsModifierKey(e.Key))
            return;
        if (string.IsNullOrEmpty(Group) && !HasAdditionalGroups())
            return;

        long now = Environment.TickCount64;
        if (_pendingStrokes.Count > 0 && IsChordExpired(now))
        {
            _pendingStrokes.Clear();
        }

        var stroke = new KeybindingGesture(e.Key, e.Modifiers);
        bool continuedChord = _pendingStrokes.Count > 0;
        _pendingStrokes.Add(stroke);

        if (TryHandleStrokes(e, now))
        {
            return;
        }

        // The key didn't continue the pending chord: cancel it and treat the key as a fresh first stroke.
        _pendingStrokes.Clear();
        if (continuedChord)
        {
            _pendingStrokes.Add(stroke);
            if (!TryHandleStrokes(e, now))
            {
                _pendingStrokes.Clear();
            }
        }
    }

    // Executes a completed keybinding, or consumes the key if it starts/continues a chord. Returns false if neither.
    private bool TryHandleStrokes(KeyEventArgs e, long now)
    {
        if (TryExecute(e))
        {
            _pendingStrokes.Clear();
            e.Handled = true;
            return true;
        }

        bool isPrefix = false;
        foreach (var scope in Scopes())
        {
            isPrefix |= KeybindingManager.IsKeybindingPrefix(scope.Group, _pendingStrokes);
        }
        if (isPrefix)
        {
            _pendingSinceMs = now;
            e.Handled = true;
            return true;
        }

        return false;
    }

    // Runs the command the strokes complete in the first scope that has one and a target it can run on.
    private bool TryExecute(KeyEventArgs e)
    {
        var origin = e.OriginalSource as UIElement;
        foreach (var scope in Scopes())
        {
            IEnumerable<object?> targets = scope.Target != null ? new[] { scope.Target } : TargetCandidates(origin);
            foreach (var target in targets)
            {
                if (KeybindingManager.TryExecuteSequence(scope.Group, _pendingStrokes, target))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private bool HasAdditionalGroups()
    {
        foreach (var scope in AdditionalScopes)
        {
            if (!string.IsNullOrEmpty(scope.Group)) return true;
        }
        return false;
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftShift or Key.RightShift or
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftWindows or Key.RightWindows;
}
