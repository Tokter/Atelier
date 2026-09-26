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
/// window only sees the popups inside its own tree. Passing <c>null</c> considers all open popups.
/// </remarks>
public static class PopupManager
{
    private static readonly List<Popup> _activePopups = [];

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

    /// <summary>Registers an opening popup.</summary>
    public static void OpenPopup(Popup popup)
    {
        if (!_activePopups.Contains(popup))
        {
            _activePopups.Add(popup);
            PopupOpened?.Invoke(popup);
        }
    }

    /// <summary>Unregisters a closing popup.</summary>
    public static void ClosePopup(Popup popup)
    {
        if (_activePopups.Remove(popup))
        {
            PopupClosed?.Invoke(popup);
        }
    }

    /// <summary>Closes every open popup in every window.</summary>
    public static void CloseAllPopups()
    {
        for (int i = _activePopups.Count - 1; i >= 0; i--)
        {
            var p = _activePopups[i];
            p.IsOpen = false;
        }
        _activePopups.Clear();
    }

    /// <summary>Determines whether a popup is open in the tree rooted at <paramref name="root"/> (or anywhere when <c>null</c>).</summary>
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
    public static UIElement? HitTest(Point screenPoint, VisualNode? root = null)
    {
        for (int i = _activePopups.Count - 1; i >= 0; i--)
        {
            var popup = _activePopups[i];
            if (!popup.IsOpen || popup.ActualBounds.IsEmpty || !BelongsTo(popup, root)) continue;

            if (popup.ActualBounds.Contains(screenPoint))
            {
                Point localPoint = screenPoint.Offset(-popup.ActualBounds.X, -popup.ActualBounds.Y);
                var hit = popup.Child?.HitTest(localPoint);
                return hit ?? popup.Child ?? popup;
            }
        }
        return null;
    }

    /// <summary>
    /// Intercepts mouse down events.
    /// If hit is inside an open popup, dispatches to it and returns true.
    /// If hit is outside, popups with StaysOpen=false are dismissed and the click is consumed.
    /// </summary>
    public static bool HandleMouseDown(Point screenPoint, PointerButtons button, ModifierKeys modifiers = ModifierKeys.None, int clickCount = 1, VisualNode? root = null)
    {
        if (!HasActivePopupsIn(root)) return false;

        var hit = HitTest(screenPoint, root);
        if (hit != null)
        {
            var e = new PointerEventArgs(screenPoint, screenPoint, button, (ulong)Environment.TickCount64, modifiers, clickCount);
            hit.DispatchPointerEvent(e, static (el, a) => el.OnPreviewPointerPressed(a), static (el, a) => el.OnPointerPressed(a));
            return true;
        }

        // Click is outside all popups of this window
        bool intercepted = false;
        for (int i = _activePopups.Count - 1; i >= 0; i--)
        {
            var popup = _activePopups[i];
            if (!popup.StaysOpen && BelongsTo(popup, root))
            {
                popup.IsOpen = false;
                intercepted = true;
            }
        }

        return intercepted;
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
    /// Intercepts mouse up events.
    /// </summary>
    public static bool HandleMouseUp(Point screenPoint, PointerButtons button, ModifierKeys modifiers = ModifierKeys.None, int clickCount = 1, VisualNode? root = null)
    {
        if (UIElement.CapturedElement != null)
        {
            bool isInsidePopup = false;
            for (int i = 0; i < _activePopups.Count; i++)
            {
                if (BelongsTo(_activePopups[i], root) && IsDescendantOf(UIElement.CapturedElement, _activePopups[i]))
                {
                    isInsidePopup = true;
                    break;
                }
            }

            if (isInsidePopup)
            {
                var captured = UIElement.CapturedElement;
                var e = new PointerEventArgs(screenPoint, screenPoint, button, (ulong)Environment.TickCount64, modifiers, clickCount);
                captured.DispatchPointerEvent(e, static (el, a) => el.OnPreviewPointerReleased(a), static (el, a) => el.OnPointerReleased(a));
                captured.ReleasePointerCapture();
                return true;
            }
        }

        if (!HasActivePopupsIn(root)) return false;

        var hit = HitTest(screenPoint, root);
        if (hit != null)
        {
            var e = new PointerEventArgs(screenPoint, screenPoint, button, (ulong)Environment.TickCount64, modifiers, clickCount);
            hit.DispatchPointerEvent(e, static (el, a) => el.OnPreviewPointerReleased(a), static (el, a) => el.OnPointerReleased(a));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Intercepts mouse move events.
    /// </summary>
    public static bool HandleMouseMove(Point screenPoint, ref UIElement? hoveredPopupElement, ModifierKeys modifiers = ModifierKeys.None, VisualNode? root = null)
    {
        if (!HasActivePopupsIn(root)) return false;

        var hit = HitTest(screenPoint, root);
        if (hit != hoveredPopupElement)
        {
            if (hoveredPopupElement != null)
            {
                var exitE = new PointerEventArgs(screenPoint, screenPoint, modifiers: modifiers);
                hoveredPopupElement.DispatchBubblePointerEvent(exitE, (el, localE) => el.OnPointerExited(localE));
            }

            hoveredPopupElement = hit;

            if (hoveredPopupElement != null)
            {
                var enterE = new PointerEventArgs(screenPoint, screenPoint, modifiers: modifiers);
                hoveredPopupElement.DispatchBubblePointerEvent(enterE, (el, localE) => el.OnPointerEntered(localE));
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
    /// Intercepts mouse wheel scroll events. While a popup is open in the window, scrolling outside it is swallowed.
    /// </summary>
    public static bool HandleMouseScroll(Point screenPoint, float scrollX, float scrollY, ModifierKeys modifiers = ModifierKeys.None, VisualNode? root = null)
    {
        if (!HasActivePopupsIn(root)) return false;

        var hit = HitTest(screenPoint, root);
        if (hit != null)
        {
            var wheelE = new PointerWheelEventArgs(screenPoint, screenPoint, scrollX, scrollY, (ulong)Environment.TickCount64, modifiers);
            hit.DispatchPointerEvent(wheelE, static (el, a) => el.OnPreviewPointerWheel(a), static (el, a) => el.OnPointerWheel(a));
            return true;
        }

        // Swallow scrolling outside open modal popup
        return true;
    }

    /// <summary>
    /// Intercepts keyboard events (Escape dismisses the topmost popup of the window that is not StaysOpen).
    /// </summary>
    public static bool HandleKeyDown(KeyEventArgs e, VisualNode? root = null)
    {
        if (!HasActivePopupsIn(root)) return false;

        if (e.Key == Key.Escape)
        {
            for (int i = _activePopups.Count - 1; i >= 0; i--)
            {
                var popup = _activePopups[i];
                if (!popup.StaysOpen && BelongsTo(popup, root))
                {
                    popup.IsOpen = false;
                    e.Handled = true;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Layout pass for the open popups of <paramref name="root"/>'s tree against the window viewport.
    /// </summary>
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
    /// Renders the open popups of <paramref name="root"/>'s tree on top of the window's element tree.
    /// </summary>
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
