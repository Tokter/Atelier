using System;
using System.Collections.Generic;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// Shows a keyboard shortcut as key caps: <c>"Ctrl+K, Ctrl+C"</c> becomes [Ctrl] [K] then [Ctrl] [C]. Text that isn't
/// a valid shortcut is shown as it is; an empty shortcut shows nothing.
/// </summary>
/// <remarks>The key caps are <see cref="Border"/>s with <see cref="KeyCapStyleKey"/> around <see cref="TextBlock"/>s with <see cref="KeyTextStyleKey"/>.</remarks>
public class ShortcutView : StackPanel
{
    /// <summary>The style key of the key caps (<see cref="Border"/>s).</summary>
    public const string KeyCapStyleKey = "ShortcutKeyCap";

    /// <summary>The style key of the key labels (<see cref="TextBlock"/>s in the key caps).</summary>
    public const string KeyTextStyleKey = "ShortcutKeyText";

    /// <summary>Identifies the <see cref="Shortcut"/> property.</summary>
    public static readonly BindableProperty<string?> ShortcutProperty =
        BindableProperty.Register<ShortcutView, string?>(nameof(Shortcut), null, (s, o, n) => ((ShortcutView)s).Rebuild());

    /// <summary>Initializes an empty shortcut view.</summary>
    public ShortcutView()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 4;
        VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>Initializes a view showing <paramref name="shortcut"/>.</summary>
    public ShortcutView(string? shortcut) : this()
    {
        Shortcut = shortcut;
    }

    /// <summary>Gets or sets the shortcut, such as <c>"Ctrl+Shift+T"</c> or <c>"Ctrl+K, Ctrl+C"</c>.</summary>
    public string? Shortcut { get => GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }

    /// <summary>Shows <paramref name="strokes"/> directly, e.g. a chord being recorded.</summary>
    internal void ShowStrokes(IReadOnlyList<KeybindingGesture> strokes)
    {
        Clear();
        for (int i = 0; i < strokes.Count; i++)
        {
            if (i > 0) Add(new TextBlock("then") { Muted = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 0) });
            foreach (string part in strokes[i].GetDisplayParts()) Add(KeyCap(part));
        }
    }

    private void Rebuild()
    {
        if (KeybindingGesture.TryParseSequence(Shortcut, out var strokes))
        {
            ShowStrokes(strokes);
            return;
        }
        Clear();
        if (!string.IsNullOrWhiteSpace(Shortcut)) Add(new TextBlock(Shortcut) { VerticalAlignment = VerticalAlignment.Center });
    }

    private static Border KeyCap(string key) => new()
    {
        StyleKey = KeyCapStyleKey,
        Padding = new Thickness(7, 1),
        CornerRadius = new CornerRadius(5),
        BorderThickness = new Thickness(1),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock(key) { StyleKey = KeyTextStyleKey },
    };
}

/// <summary>
/// A field that records a shortcut, with instructions below it: click it (or press Enter or Space while it has the
/// focus) and press the keys, or click, drag or turn the wheel for a pointer gesture (see <see cref="PointerGesture"/>).
/// A second key makes a chord (<c>"Ctrl+K, Ctrl+C"</c>); Enter accepts a single stroke, Escape cancels.
/// </summary>
/// <remarks>
/// While recording, the field handles every key, so shortcuts of the window (and menu access keys) don't run. Tab
/// can't be recorded (it moves the focus), and moving the focus away cancels the recording.
/// </remarks>
public class ShortcutRecorder : ContentControl
{
    /// <summary>The style key of the frame (a <see cref="Border"/>) while not recording.</summary>
    public const string FrameStyleKey = "ShortcutRecorderFrame";

    /// <summary>The style key of the frame while recording.</summary>
    public const string RecordingFrameStyleKey = "ShortcutRecorderFrameRecording";

    /// <summary>Identifies the <see cref="Shortcut"/> property.</summary>
    public static readonly BindableProperty<string?> ShortcutProperty =
        BindableProperty.Register<ShortcutRecorder, string?>(nameof(Shortcut), null, (s, o, n) => ((ShortcutRecorder)s).UpdateDisplay());

    /// <summary>Identifies the <see cref="Placeholder"/> property.</summary>
    public static readonly BindableProperty<string> PlaceholderProperty =
        BindableProperty.Register<ShortcutRecorder, string>(nameof(Placeholder), "No shortcut", (s, o, n) => ((ShortcutRecorder)s).UpdateDisplay());

    private readonly List<KeybindingGesture> _strokes = [];
    private const long DoubleClickMs = 500;
    private (PointerButtons Button, ModifierKeys Modifiers, Point ScreenPosition, bool Dragged)? _pointerPress;
    private (long AtMs, ModifierKeys Modifiers)? _lastPointerClick;
    private readonly Border _frame;
    private readonly ShortcutView _keys = new();
    // A short status in the field, and the instructions below it (they wrap, so the field keeps its width).
    private readonly TextBlock _status = new() { Muted = true, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _instructions = new() { Muted = true, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 0, 0, 0) };

    static ShortcutRecorder()
    {
        IsFocusableProperty.OverrideDefaultValue<ShortcutRecorder>(true);
    }

    /// <summary>Initializes a recorder without a shortcut.</summary>
    public ShortcutRecorder()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        row.Add(_keys);
        row.Add(_status);
        _frame = new Border
        {
            StyleKey = FrameStyleKey,
            MinHeight = 40,
            Padding = new Thickness(12, 6),
            CornerRadius = new CornerRadius(8),
            Child = row,
        };
        var layout = new StackPanel { Spacing = 4 };
        layout.Add(_frame);
        layout.Add(_instructions);
        Content = layout;
        UpdateDisplay();
    }

    /// <summary>Gets or sets the shortcut, such as <c>"Ctrl+Shift+T"</c>; empty or <c>null</c> for none. Recording sets it.</summary>
    public string? Shortcut { get => GetValue(ShortcutProperty); set => SetValue(ShortcutProperty, value); }

    /// <summary>Gets or sets the text shown when there is no shortcut. The default is "No shortcut".</summary>
    public string Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <summary>Gets whether the field is recording keys.</summary>
    public bool IsRecording { get; private set; }

    /// <summary>Occurs when a recording was accepted, with the new shortcut (normalized, e.g. <c>"Ctrl+Shift+T"</c>).</summary>
    public event EventHandler<string>? ShortcutRecorded;

    /// <summary>Starts recording: the next key presses become the shortcut.</summary>
    public void StartRecording()
    {
        if (!IsEnabled) return;
        _strokes.Clear();
        IsRecording = true;
        if (!IsFocused) Focus();
        UpdateDisplay();
    }

    /// <summary>Stops recording without changing the shortcut.</summary>
    public void CancelRecording()
    {
        if (!IsRecording) return;
        IsRecording = false;
        _strokes.Clear();
        UpdateDisplay();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Not recording, a left click starts recording. While recording (before any key), pressing a button records a
    /// pointer gesture: a click, or a drag once the pointer moves; a double click right after recording a click records
    /// the double click.
    /// </remarks>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled) return;

        if (IsRecording && _strokes.Count == 0 && KeybindingGesture.ClickOf(e.Button) != PointerGesture.None)
        {
            _pointerPress = (e.Button, e.Modifiers, e.ScreenPosition, false);
            CapturePointer();
            e.Handled = true;
            return;
        }
        if (e.Button != PointerButtons.Left) return;

        // The second press of a double click that started as a recorded click.
        if (!IsRecording && e.ClickCount == 2 && _lastPointerClick is { } click
            && Environment.TickCount64 - click.AtMs < DoubleClickMs && click.Modifiers == e.Modifiers)
        {
            _lastPointerClick = null;
            Accept([new KeybindingGesture(PointerGesture.DoubleClick, e.Modifiers)]);
            e.Handled = true;
            return;
        }

        if (IsRecording) CancelRecording();
        else StartRecording();
        e.Handled = true;
    }

    /// <inheritdoc/>
    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_pointerPress is not { } press) return;

        e.Handled = true;
        var moved = e.ScreenPosition - press.ScreenPosition;
        if (moved.X * moved.X + moved.Y * moved.Y >= KeybindingHandler.DragThreshold * KeybindingHandler.DragThreshold)
        {
            _pointerPress = press with { Dragged = true };
        }
    }

    /// <inheritdoc/>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pointerPress is not { } press || press.Button != e.Button) return;

        _pointerPress = null;
        e.Handled = true;
        if (IsPointerCaptured) ReleasePointerCapture();
        var pointer = press.Dragged ? KeybindingGesture.DragOf(press.Button) : KeybindingGesture.ClickOf(press.Button);
        _lastPointerClick = pointer == PointerGesture.LeftClick ? (Environment.TickCount64, press.Modifiers) : null;
        Accept([new KeybindingGesture(pointer, press.Modifiers)]);
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture()
    {
        base.OnLostPointerCapture();
        _pointerPress = null;
    }

    /// <inheritdoc/>
    /// <remarks>While recording (before any key), turning the wheel records <c>"WheelUp"</c> or <c>"WheelDown"</c>.</remarks>
    public override void OnPointerWheel(PointerWheelEventArgs e)
    {
        base.OnPointerWheel(e);
        if (e.Handled || !IsRecording || _strokes.Count > 0 || e.DeltaY == 0) return;

        e.Handled = true;
        Accept([new KeybindingGesture(e.DeltaY > 0 ? PointerGesture.WheelUp : PointerGesture.WheelDown, e.Modifiers)]);
    }

    /// <inheritdoc/>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;

        if (!IsRecording)
        {
            if (e.Modifiers == ModifierKeys.None && e.Key is Key.Enter or Key.Space)
            {
                StartRecording();
                e.Handled = true;
            }
            return;
        }

        if (e.Key == Key.Tab && (e.Modifiers & ~ModifierKeys.Shift) == 0) return; // Tab moves the focus (ending the recording)
        e.Handled = true; // while recording, every other key belongs to the shortcut
        if (IsModifierKey(e.Key) || e.IsRepeat) return;

        if (e.Modifiers == ModifierKeys.None && e.Key == Key.Escape)
        {
            CancelRecording();
            return;
        }
        if (e.Modifiers == ModifierKeys.None && e.Key == Key.Enter && _strokes.Count > 0)
        {
            Accept();
            return;
        }

        _strokes.Add(new KeybindingGesture(e.Key, e.Modifiers));
        if (_strokes.Count == 2) Accept();
        else UpdateDisplay();
    }

    /// <inheritdoc/>
    public override void OnLostFocus()
    {
        base.OnLostFocus();
        CancelRecording();
    }

    private void Accept() => Accept(_strokes.ToArray());

    private void Accept(IReadOnlyList<KeybindingGesture> strokes)
    {
        string shortcut = KeybindingGesture.FormatSequence(strokes);
        IsRecording = false;
        _strokes.Clear();
        Shortcut = shortcut;
        UpdateDisplay();
        ShortcutRecorded?.Invoke(this, shortcut);
    }

    private void UpdateDisplay()
    {
        _frame.StyleKey = IsRecording ? RecordingFrameStyleKey : FrameStyleKey;
        if (IsRecording)
        {
            _keys.ShowStrokes(_strokes);
            _status.Text = _strokes.Count == 0 ? "Press the keys…" : string.Empty;
            _instructions.Text = _strokes.Count == 0
                ? "Press the shortcut's keys, or click, drag or turn the wheel. Esc cancels."
                : "Press a second key for a chord, or Enter to accept. Esc cancels.";
        }
        else
        {
            _keys.Shortcut = null; // rebuild even when the shortcut didn't change
            _keys.Shortcut = Shortcut;
            _status.Text = string.IsNullOrEmpty(Shortcut) ? Placeholder : string.Empty;
            _instructions.Text = "Click or press Enter to record a new shortcut.";
        }
        _status.Visibility = _status.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        InvalidateMeasure();
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftShift or Key.RightShift or
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftAlt or Key.RightAlt or
        Key.LeftWindows or Key.RightWindows;
}
