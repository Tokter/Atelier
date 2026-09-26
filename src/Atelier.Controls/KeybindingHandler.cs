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
/// The command's target is resolved in order: the <see cref="BindableObject.DataContext"/> of the element the key event
/// originated from, the distinct data contexts of its ancestors up to this handler, then this handler's own data context
/// (or <c>null</c> when there is none).
/// </para>
/// </remarks>
public class KeybindingHandler : ContentControl
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
    /// Gets the strokes of a chord that has been started but not completed (e.g. <c>"Ctrl+K"</c>), or <c>null</c>.
    /// Useful for showing a "waiting for second key" hint.
    /// </summary>
    public string? PendingChord => _pendingStrokes.Count == 0 ? null : KeybindingGesture.FormatSequence(_pendingStrokes);

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

        if (e.Handled || string.IsNullOrEmpty(Group) || e.Key == Key.None || IsModifierKey(e.Key))
            return;

        long now = Environment.TickCount64;
        if (_pendingStrokes.Count > 0 && now - _pendingSinceMs > (long)ChordTimeout.TotalMilliseconds)
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

        if (KeybindingManager.IsKeybindingPrefix(Group, _pendingStrokes))
        {
            _pendingSinceMs = now;
            e.Handled = true;
            return true;
        }

        return false;
    }

    private bool TryExecute(KeyEventArgs e)
    {
        // 1. Try DataContext of the initially focused element that originated the key event
        var initialElement = e.OriginalSource as UIElement;
        object? initialTarget = initialElement?.DataContext;

        if (initialTarget != null && KeybindingManager.TryExecuteSequence(Group, _pendingStrokes, initialTarget))
        {
            return true;
        }

        // 2. Walk up the visual ancestor chain from initialElement up to this KeybindingHandler,
        // trying each ancestor's distinct DataContext (e.g. subviews, pages, cards)
        var current = initialElement?.Parent as UIElement;
        while (current != null && current != this)
        {
            if (current.DataContext != null && !ReferenceEquals(current.DataContext, initialTarget)
                && KeybindingManager.TryExecuteSequence(Group, _pendingStrokes, current.DataContext))
            {
                return true;
            }
            current = current.Parent as UIElement;
        }

        // 3. Fall back to this KeybindingHandler's own DataContext
        if (DataContext != null && !ReferenceEquals(DataContext, initialTarget))
        {
            return KeybindingManager.TryExecuteSequence(Group, _pendingStrokes, DataContext);
        }

        // 4. Try null target (for parameterless or class-level [Keybinding] commands)
        return initialTarget == null && DataContext == null
            && KeybindingManager.TryExecuteSequence(Group, _pendingStrokes, null);
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftShift or Key.RightShift or
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftWindows or Key.RightWindows;
}
