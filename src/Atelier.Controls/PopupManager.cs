using System;
using System.Collections.Generic;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Controls;

/// <summary>
/// Tracks open <see cref="Popup"/>s and routes input to them before the regular element tree.
/// </summary>
/// <remarks>
/// With several windows, every method that handles input, layout or rendering takes the window's content root, so each
/// window only sees the popups inside its own tree. Passing <c>null</c> considers all open popups. Popups are kept in
/// opening order; a popup opened later is above the ones opened before it.
/// </remarks>
public static class PopupManager
{
    private static readonly List<Popup> _activePopups = [];

    // Reused snapshot of the open popups of one window, so handlers may open or close popups while input is routed.
    // A nested call (from an event handler) finds it taken and uses a fresh list.
    private static List<Popup>? s_snapshot = [];

    /// <summary>Gets all open popups, in opening order (topmost last).</summary>
    public static IReadOnlyList<Popup> ActivePopups => _activePopups;

    /// <summary>Gets whether any popup is open, in any window.</summary>
    public static bool HasActivePopups => _activePopups.Count > 0;

    /// <summary>Gets the most recently opened popup, in any window, or <c>null</c>.</summary>
    public static Popup? TopmostPopup => _activePopups.Count > 0 ? _activePopups[^1] : null;

    /// <summary>Occurs when a popup opens.</summary>
    public static event Action<Popup>? PopupOpened;

    /// <summary>Occurs when a popup closes.</summary>
    public static event Action<Popup>? PopupClosed;

    /// <summary>Registers an opening popup. Called by <see cref="Popup.IsOpen"/>; does nothing if it is already registered.</summary>
    /// <param name="popup">The popup that opened.</param>
    public static void OpenPopup(Popup popup)
    {
        if (!_activePopups.Contains(popup))
        {
            _activePopups.Add(popup);
            PopupOpened?.Invoke(popup);
        }
    }

    /// <summary>Unregisters a closing popup. Called by <see cref="Popup.IsOpen"/>; does nothing if it is not registered.</summary>
    /// <param name="popup">The popup that closed.</param>
    public static void ClosePopup(Popup popup)
    {
        if (_activePopups.Remove(popup))
        {
            PopupClosed?.Invoke(popup);
        }
    }

    /// <summary>
    /// Closes every open popup in every window, topmost first. A popup reopened by a <see cref="Popup.Closed"/> handler
    /// stays open.
    /// </summary>
    public static void CloseAllPopups()
    {
        var popups = RentSnapshot(null, openOnly: false);
        try
        {
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                if (p.IsOpen)
                {
                    p.IsOpen = false;
                }
                else
                {
                    ClosePopup(p);
                }
            }
        }
        finally
        {
            ReturnSnapshot(popups);
        }
    }

    /// <summary>Determines whether a popup is open in the tree rooted at <paramref name="root"/> (or anywhere when <c>null</c>).</summary>
    /// <param name="root">The window's content root, or <c>null</c> for all windows.</param>
    public static bool HasActivePopupsIn(VisualNode? root)
    {
        for (int i = 0; i < _activePopups.Count; i++)
        {
            if (BelongsTo(_activePopups[i], root))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Performs hit-testing on the open popups of <paramref name="root"/>'s tree in topmost-first order.
    /// </summary>
    /// <param name="screenPoint">The point in window coordinates.</param>
    /// <param name="root">The window's content root, or <c>null</c> for all windows.</param>
    /// <returns>The deepest element hit inside the topmost popup containing the point, or <c>null</c>.</returns>
    public static UIElement? HitTest(Point screenPoint, VisualNode? root = null)
    {
        if (_activePopups.Count == 0) return null;

        var popups = RentSnapshot(root);
        try
        {
            return HitTest(popups, screenPoint, out _);
        }
        finally
        {
            ReturnSnapshot(popups);
        }
    }

    private static UIElement? HitTest(List<Popup> popups, Point screenPoint, out int popupIndex)
    {
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            var popup = popups[i];
            if (!popup.IsOpen || popup.ActualBounds.IsEmpty) continue;

            if (popup.ActualBounds.Contains(screenPoint))
            {
                popupIndex = i;
                Point localPoint = screenPoint.Offset(-popup.ActualBounds.X, -popup.ActualBounds.Y);
                var hit = popup.Child?.HitTest(localPoint);
                return hit ?? popup.Child ?? popup;
            }
        }
        popupIndex = -1;
        return null;
    }

    /// <summary>
    /// Intercepts a mouse button press in the window of <paramref name="root"/>.
    /// </summary>
    /// <remarks>
    /// A press outside all popups closes every popup that is not <see cref="Popup.StaysOpen"/> and is consumed if any was
    /// closed. A press inside a popup closes the light-dismiss popups opened above it (such as a submenu); if any was
    /// closed the press is consumed, otherwise it is routed to the element hit in the popup.
    /// </remarks>
    /// <param name="screenPoint">The pointer position in window coordinates.</param>
    /// <param name="button">The pressed button.</param>
    /// <param name="modifiers">The modifier keys held down.</param>
    /// <param name="clickCount">The number of consecutive clicks.</param>
    /// <param name="root">The window's content root, or <c>null</c> for all windows.</param>
    /// <returns><c>true</c> if the press was handled here and must not reach the window's tree.</returns>
    public static bool HandleMouseDown(Point screenPoint, PointerButtons button, ModifierKeys modifiers = ModifierKeys.None, int clickCount = 1, VisualNode? root = null)
    {
        if (_activePopups.Count == 0) return false;

        var popups = RentSnapshot(root);
        try
        {
            if (popups.Count == 0) return false;

            var hit = HitTest(popups, screenPoint, out int hitIndex);

            // Close light-dismiss popups above the one hit (all of them when the press is outside every popup).
            bool dismissed = false;
            for (int i = popups.Count - 1; i > hitIndex; i--)
            {
                var popup = popups[i];
                if (!popup.StaysOpen && popup.IsOpen)
                {
                    popup.IsOpen = false;
                    dismissed = true;
                }
            }

            if (dismissed || hit == null)
            {
                return dismissed;
            }

            var e = new PointerEventArgs(screenPoint, screenPoint, button, (ulong)Environment.TickCount64, modifiers, clickCount);
            hit.DispatchPointerEvent(e, static (el, a) => el.OnPreviewPointerPressed(a), static (el, a) => el.OnPointerPressed(a));
            return true;
        }
        finally
        {
            ReturnSnapshot(popups);
        }
    }

    private static bool IsDescendantOf(UIElement element, UIElement ancestor)
    {
        UIElement? curr = element;
        while (curr != null)
        {
            if (curr == ancestor) return true;
            curr = curr.Parent as UIElement;
        }
        return false;
    }

    /// <summary>
    /// Intercepts a mouse button release: delivered to the captured element when it is inside a popup (releasing the
    /// capture), otherwise to the element hit in a popup.
    /// </summary>
    /// <param name="screenPoint">The pointer position in window coordinates.</param>
    /// <param name="button">The released button.</param>
    /// <param name="modifiers">The modifier keys held down.</param>
    /// <param name="clickCount">The number of consecutive clicks.</param>
    /// <param name="root">The window's content root, or <c>null</c> for all windows.</param>
    /// <returns><c>true</c> if the release was handled here and must not reach the window's tree.</returns>
    public static bool HandleMouseUp(Point screenPoint, PointerButtons button, ModifierKeys modifiers = ModifierKeys.None, int clickCount = 1, VisualNode? root = null)
    {
        if (_activePopups.Count == 0) return false;

        var popups = RentSnapshot(root);
        try
        {
            if (popups.Count == 0) return false;

            var captured = UIElement.CapturedElement;
            if (captured != null)
            {
                for (int i = 0; i < popups.Count; i++)
                {
                    if (IsDescendantOf(captured, popups[i]))
                    {
                        var ce = new PointerEventArgs(screenPoint, screenPoint, button, (ulong)Environment.TickCount64, modifiers, clickCount);
                        captured.DispatchPointerEvent(ce, static (el, a) => el.OnPreviewPointerReleased(a), static (el, a) => el.OnPointerReleased(a));
                        captured.ReleasePointerCapture();
                        return true;
                    }
                }
            }

            var hit = HitTest(popups, screenPoint, out _);
            if (hit != null)
            {
                var e = new PointerEventArgs(screenPoint, screenPoint, button, (ulong)Environment.TickCount64, modifiers, clickCount);
                hit.DispatchPointerEvent(e, static (el, a) => el.OnPreviewPointerReleased(a), static (el, a) => el.OnPointerReleased(a));
                return true;
            }

            return false;
        }
        finally
        {
            ReturnSnapshot(popups);
        }
    }

    /// <summary>
    /// Intercepts a mouse move: updates the hovered popup element (raising pointer exited/entered) and routes the move to
    /// the element hit in a popup.
    /// </summary>
    /// <remarks>
    /// When the pointer is outside every popup, or no popup is open any more, the previously hovered popup element gets
    /// pointer exited and <paramref name="hoveredPopupElement"/> is cleared.
    /// </remarks>
    /// <param name="screenPoint">The pointer position in window coordinates.</param>
    /// <param name="hoveredPopupElement">The window's hovered popup element; updated by this call.</param>
    /// <param name="modifiers">The modifier keys held down.</param>
    /// <param name="root">The window's content root, or <c>null</c> for all windows.</param>
    /// <returns><c>true</c> if the pointer is over a popup and the move must not reach the window's tree.</returns>
    public static bool HandleMouseMove(Point screenPoint, ref UIElement? hoveredPopupElement, ModifierKeys modifiers = ModifierKeys.None, VisualNode? root = null)
    {
        UIElement? hit = null;
        if (_activePopups.Count > 0)
        {
            var popups = RentSnapshot(root);
            try
            {
                hit = HitTest(popups, screenPoint, out _);
            }
            finally
            {
                ReturnSnapshot(popups);
            }
        }

        if (hit != hoveredPopupElement)
        {
            if (hoveredPopupElement != null)
            {
                var exitE = new PointerEventArgs(screenPoint, screenPoint, modifiers: modifiers);
                hoveredPopupElement.DispatchBubblePointerEvent(exitE, static (el, localE) => el.OnPointerExited(localE));
            }

            hoveredPopupElement = hit;

            if (hit != null)
            {
                var enterE = new PointerEventArgs(screenPoint, screenPoint, modifiers: modifiers);
                hit.DispatchBubblePointerEvent(enterE, static (el, localE) => el.OnPointerEntered(localE));
            }
        }

        if (hit != null)
        {
            var moveE = new PointerEventArgs(screenPoint, screenPoint, modifiers: modifiers);
            hit.DispatchPointerEvent(moveE, static (el, a) => el.OnPreviewPointerMoved(a), static (el, a) => el.OnPointerMoved(a));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Intercepts a mouse wheel event: routed to the element hit in a popup. Outside all popups, the wheel is swallowed
    /// while a light-dismiss popup (not <see cref="Popup.StaysOpen"/>) is open, so content behind it doesn't scroll away
    /// from it; with only <see cref="Popup.StaysOpen"/> popups open it passes through to the window's tree.
    /// </summary>
    /// <param name="screenPoint">The pointer position in window coordinates.</param>
    /// <param name="scrollX">The horizontal wheel delta.</param>
    /// <param name="scrollY">The vertical wheel delta.</param>
    /// <param name="modifiers">The modifier keys held down.</param>
    /// <param name="root">The window's content root, or <c>null</c> for all windows.</param>
    /// <returns><c>true</c> if the event was handled or swallowed and must not reach the window's tree.</returns>
    public static bool HandleMouseScroll(Point screenPoint, float scrollX, float scrollY, ModifierKeys modifiers = ModifierKeys.None, VisualNode? root = null)
    {
        if (_activePopups.Count == 0) return false;

        var popups = RentSnapshot(root);
        try
        {
            var hit = HitTest(popups, screenPoint, out _);
            if (hit != null)
            {
                var wheelE = new PointerWheelEventArgs(screenPoint, screenPoint, scrollX, scrollY, (ulong)Environment.TickCount64, modifiers);
                hit.DispatchPointerEvent(wheelE, static (el, a) => el.OnPreviewPointerWheel(a), static (el, a) => el.OnPointerWheel(a));
                return true;
            }

            for (int i = 0; i < popups.Count; i++)
            {
                if (!popups[i].StaysOpen)
                {
                    return true;
                }
            }
            return false;
        }
        finally
        {
            ReturnSnapshot(popups);
        }
    }

    /// <summary>
    /// Intercepts keyboard events: Escape closes the topmost popup of the window that is not <see cref="Popup.StaysOpen"/>.
    /// </summary>
    /// <param name="e">The key event; marked handled when a popup was closed.</param>
    /// <param name="root">The window's content root, or <c>null</c> for all windows.</param>
    /// <returns><c>true</c> if a popup was closed.</returns>
    public static bool HandleKeyDown(KeyEventArgs e, VisualNode? root = null)
    {
        if (_activePopups.Count == 0 || e.Key != Key.Escape) return false;

        for (int i = _activePopups.Count - 1; i >= 0; i--)
        {
            var popup = _activePopups[i];
            if (!popup.StaysOpen && popup.IsOpen && BelongsTo(popup, root))
            {
                popup.IsOpen = false;
                e.Handled = true;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Layout pass for the open popups of <paramref name="root"/>'s tree against the window viewport. Call after the
    /// window's tree has been arranged.
    /// </summary>
    /// <param name="viewportSize">The window's size.</param>
    /// <param name="root">The window's content root, or <c>null</c> for all windows.</param>
    public static void UpdatePopups(Size viewportSize, VisualNode? root = null)
    {
        for (int i = 0; i < _activePopups.Count; i++)
        {
            var popup = _activePopups[i];
            if (popup.IsOpen && BelongsTo(popup, root))
            {
                popup.UpdatePlacement(viewportSize);
            }
        }
    }

    /// <summary>
    /// Renders the open popups of <paramref name="root"/>'s tree on top of the window's element tree, in opening order.
    /// </summary>
    /// <param name="context">The window's drawing context.</param>
    /// <param name="presenter">Draws themed elements.</param>
    /// <param name="root">The window's content root, or <c>null</c> for all windows.</param>
    public static void RenderPopups(ref DrawingContext context, IElementVisualPresenter? presenter, VisualNode? root = null)
    {
        for (int i = 0; i < _activePopups.Count; i++)
        {
            var popup = _activePopups[i];
            if (popup.IsOpen && BelongsTo(popup, root))
            {
                popup.RenderPopup(ref context, presenter);
            }
        }
    }

    // Copies the (open) popups of root's tree, in opening order, into a reusable list; one tree walk per popup.
    private static List<Popup> RentSnapshot(VisualNode? root, bool openOnly = true)
    {
        var list = s_snapshot ?? [];
        s_snapshot = null;
        for (int i = 0; i < _activePopups.Count; i++)
        {
            var popup = _activePopups[i];
            if ((!openOnly || popup.IsOpen) && BelongsTo(popup, root))
            {
                list.Add(popup);
            }
        }
        return list;
    }

    private static void ReturnSnapshot(List<Popup> list)
    {
        list.Clear();
        s_snapshot = list;
    }

    private static bool BelongsTo(Popup popup, VisualNode? root)
    {
        if (root == null)
        {
            return true;
        }

        VisualNode node = popup;
        while (node.Parent != null)
        {
            node = node.Parent;
        }
        return node == root;
    }
}
