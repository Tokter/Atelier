using System;
using System.Collections.Generic;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;

namespace Atelier.Controls;

public partial class KeybindingHandler
{
    /// <summary>
    /// Gets or sets how far (in pixels) the pointer must move with a button held before the press becomes a drag rather
    /// than a click. The default is 4.
    /// </summary>
    public static float DragThreshold { get; set; } = 4f;

    private PendingPress? _press;
    private IDragOperation? _drag;

    /// <summary>Gets whether a drag command is running (see <see cref="IDragCommand"/>).</summary>
    public bool IsDragging => _drag != null;

    /// <inheritdoc/>
    /// <remarks>
    /// A double click runs a <c>"DoubleClick"</c> keybinding. Otherwise, when a click or drag of the button is bound, the
    /// handler captures the pointer: releasing it before it moved <see cref="DragThreshold"/> pixels runs the click
    /// keybinding (such as <c>"RightClick"</c>), moving further starts the drag command (such as <c>"MiddleDrag"</c>).
    /// Elements inside that handle the press themselves keep it.
    /// </remarks>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || _press != null || _drag != null || !HasAnyGroup()) return;

        var origin = e.OriginalSource as UIElement;
        if (e.Button == PointerButtons.Left && e.ClickCount == 2
            && TryExecuteStroke(new KeybindingGesture(PointerGesture.DoubleClick, e.Modifiers), origin))
        {
            e.Handled = true;
            return;
        }

        var click = new KeybindingGesture(KeybindingGesture.ClickOf(e.Button), e.Modifiers);
        var drag = new KeybindingGesture(KeybindingGesture.DragOf(e.Button), e.Modifiers);
        if (!click.IsPointer || (!IsBound(click) && !IsBound(drag))) return;

        _press = new PendingPress(e.Button, e.Modifiers, e.ScreenPosition, origin);
        CapturePointer();
        e.Handled = true;
    }

    /// <inheritdoc/>
    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_drag != null)
        {
            _drag.Update(e.ScreenPosition, e.Modifiers);
            e.Handled = true;
            return;
        }
        if (_press is not { } press) return;

        e.Handled = true;
        var moved = e.ScreenPosition - press.ScreenPosition;
        if (moved.X * moved.X + moved.Y * moved.Y < DragThreshold * DragThreshold) return;

        // Too far for a click: a drag, if a drag command takes it.
        _press = null;
        _drag = BeginDrag(press);
        if (_drag == null)
        {
            if (IsPointerCaptured) ReleasePointerCapture();
            return;
        }
        _drag.Update(e.ScreenPosition, e.Modifiers);
    }

    /// <inheritdoc/>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_drag is { } drag)
        {
            _drag = null;
            e.Handled = true;
            if (IsPointerCaptured) ReleasePointerCapture();
            drag.Complete(e.ScreenPosition, e.Modifiers);
            return;
        }
        if (_press is not { } press || press.Button != e.Button) return;

        _press = null;
        e.Handled = true;
        if (IsPointerCaptured) ReleasePointerCapture();
        TryExecuteStroke(new KeybindingGesture(KeybindingGesture.ClickOf(press.Button), press.Modifiers), press.Origin);
    }

    /// <inheritdoc/>
    /// <remarks>Cancels a drag in progress.</remarks>
    protected override void OnLostPointerCapture()
    {
        base.OnLostPointerCapture();
        _press = null;
        CancelDrag();
    }

    /// <inheritdoc/>
    /// <remarks>Runs a <c>"WheelUp"</c> or <c>"WheelDown"</c> keybinding (with the modifiers held) once per wheel event.</remarks>
    public override void OnPointerWheel(PointerWheelEventArgs e)
    {
        base.OnPointerWheel(e);
        if (e.Handled || e.DeltaY == 0 || !HasAnyGroup()) return;

        var stroke = new KeybindingGesture(e.DeltaY > 0 ? PointerGesture.WheelUp : PointerGesture.WheelDown, e.Modifiers);
        if (TryExecuteStroke(stroke, e.OriginalSource as UIElement))
        {
            e.Handled = true;
        }
    }

    /// <summary>Cancels the drag in progress, if any (as Escape does).</summary>
    public void CancelDrag()
    {
        if (_drag is not { } drag) return;
        _drag = null;
        if (IsPointerCaptured) ReleasePointerCapture();
        drag.Cancel();
    }

    // Escape cancels a drag; other keys are left to the drag's own handling.
    private bool HandleDragKey(KeyEventArgs e)
    {
        if (_drag == null) return false;
        if (e.Key == Key.Escape)
        {
            CancelDrag();
            e.Handled = true;
        }
        return true;
    }

    private bool HasAnyGroup() => !string.IsNullOrEmpty(Group) || HasAdditionalGroups();

    // Whether any scope has a keybinding for the stroke.
    private bool IsBound(KeybindingGesture stroke)
    {
        foreach (var scope in Scopes())
        {
            if (KeybindingManager.FindKeybindings(scope.Group, stroke).Count > 0) return true;
        }
        return false;
    }

    // Runs the keybinding of a single stroke, like a key press does.
    private bool TryExecuteStroke(KeybindingGesture stroke, UIElement? origin)
    {
        var strokes = new[] { stroke };
        foreach (var scope in Scopes())
        {
            IEnumerable<object?> targets = scope.Target != null ? new[] { scope.Target } : TargetCandidates(origin);
            foreach (var target in targets)
            {
                if (KeybindingManager.TryExecuteSequence(scope.Group, strokes, target)) return true;
            }
        }
        return false;
    }

    // Starts the first drag command bound to the press's drag gesture that takes it.
    private IDragOperation? BeginDrag(PendingPress press)
    {
        var stroke = new KeybindingGesture(KeybindingGesture.DragOf(press.Button), press.Modifiers);
        foreach (var scope in Scopes())
        {
            foreach (var descriptor in KeybindingManager.FindKeybindings(scope.Group, stroke))
            {
                if (descriptor.Command is not IDragCommand command) continue;
                IEnumerable<object?> targets = scope.Target != null ? new[] { scope.Target } : TargetCandidates(press.Origin);
                foreach (var target in targets)
                {
                    if (!command.CanExecute(target)) continue;
                    var operation = command.BeginDrag(new DragStart(target, this, press.Origin, press.ScreenPosition, press.Button, press.Modifiers));
                    if (operation != null) return operation;
                }
            }
        }
        return null;
    }

    private sealed record PendingPress(PointerButtons Button, ModifierKeys Modifiers, Point ScreenPosition, UIElement? Origin);
}
