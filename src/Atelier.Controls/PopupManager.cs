using System;
using System.Collections.Generic;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Controls;

public static class PopupManager
{
    private static readonly List<Popup> _activePopups = [];

    public static IReadOnlyList<Popup> ActivePopups => _activePopups;
    public static bool HasActivePopups => _activePopups.Count > 0;
    public static Popup? TopmostPopup => _activePopups.Count > 0 ? _activePopups[^1] : null;

    public static event Action<Popup>? PopupOpened;
    public static event Action<Popup>? PopupClosed;

    public static void OpenPopup(Popup popup)
    {
        if (!_activePopups.Contains(popup))
        {
            _activePopups.Add(popup);
            PopupOpened?.Invoke(popup);
        }
    }

    public static void ClosePopup(Popup popup)
    {
        if (_activePopups.Remove(popup))
        {
            PopupClosed?.Invoke(popup);
        }
    }

    public static void CloseAllPopups()
    {
        for (int i = _activePopups.Count - 1; i >= 0; i--)
        {
            var p = _activePopups[i];
            p.IsOpen = false;
        }
        _activePopups.Clear();
    }

    /// <summary>
    /// Performs hit-testing on active popups in topmost-first order.
    /// </summary>
    public static UIElement? HitTest(Point screenPoint)
    {
        for (int i = _activePopups.Count - 1; i >= 0; i--)
        {
            var popup = _activePopups[i];
            if (!popup.IsOpen || popup.ActualBounds.IsEmpty) continue;

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
    /// If hit is inside an active popup, dispatches to it and returns true.
    /// If hit is outside and topmost popup has StaysOpen=false, dismisses the popup and returns true (consuming the click).
    /// </summary>
    public static bool HandleMouseDown(Point screenPoint, PointerButtons button)
    {
        if (!HasActivePopups) return false;

        var hit = HitTest(screenPoint);
        if (hit != null)
        {
            var e = new PointerEventArgs(screenPoint, screenPoint, button, (ulong)Environment.TickCount64);
            hit.DispatchBubblePointerEvent(e, (el, localE) => el.OnPointerPressed(localE));
            return true;
        }

        // Click is outside all popups
        bool intercepted = false;
        for (int i = _activePopups.Count - 1; i >= 0; i--)
        {
            var popup = _activePopups[i];
            if (!popup.StaysOpen)
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
    public static bool HandleMouseUp(Point screenPoint, PointerButtons button)
    {
        if (UIElement.CapturedElement != null)
        {
            bool isInsidePopup = false;
            for (int i = 0; i < _activePopups.Count; i++)
            {
                if (IsDescendantOf(UIElement.CapturedElement, _activePopups[i]))
                {
                    isInsidePopup = true;
                    break;
                }
            }

            if (isInsidePopup)
            {
                var captured = UIElement.CapturedElement;
                var e = new PointerEventArgs(screenPoint, screenPoint, button, (ulong)Environment.TickCount64);
                captured.DispatchBubblePointerEvent(e, (el, localE) => el.OnPointerReleased(localE));
                captured.ReleasePointerCapture();
                return true;
            }
        }

        if (!HasActivePopups) return false;

        var hit = HitTest(screenPoint);
        if (hit != null)
        {
            var e = new PointerEventArgs(screenPoint, screenPoint, button, (ulong)Environment.TickCount64);
            hit.DispatchBubblePointerEvent(e, (el, localE) => el.OnPointerReleased(localE));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Intercepts mouse move events.
    /// </summary>
    public static bool HandleMouseMove(Point screenPoint, ref UIElement? hoveredPopupElement)
    {
        if (!HasActivePopups) return false;

        var hit = HitTest(screenPoint);
        if (hit != hoveredPopupElement)
        {
            if (hoveredPopupElement != null)
            {
                var exitE = new PointerEventArgs(screenPoint, screenPoint);
                hoveredPopupElement.DispatchBubblePointerEvent(exitE, (el, localE) => el.OnPointerExited(localE));
            }

            hoveredPopupElement = hit;

            if (hoveredPopupElement != null)
            {
                var enterE = new PointerEventArgs(screenPoint, screenPoint);
                hoveredPopupElement.DispatchBubblePointerEvent(enterE, (el, localE) => el.OnPointerEntered(localE));
            }
        }

        if (hit != null)
        {
            var moveE = new PointerEventArgs(screenPoint, screenPoint);
            hit.DispatchBubblePointerEvent(moveE, (el, localE) => el.OnPointerMoved(localE));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Intercepts mouse wheel scroll events.
    /// </summary>
    public static bool HandleMouseScroll(Point screenPoint, float scrollX, float scrollY)
    {
        if (!HasActivePopups) return false;

        var hit = HitTest(screenPoint);
        if (hit != null)
        {
            var wheelE = new PointerWheelEventArgs(screenPoint, screenPoint, scrollX, scrollY);
            hit.DispatchBubblePointerEvent(wheelE, (el, localE) => el.OnPointerWheel(localE));
            return true;
        }

        // Swallow scrolling outside open modal popup
        return true;
    }

    /// <summary>
    /// Intercepts keyboard events (e.g. Escape to dismiss topmost popup).
    /// </summary>
    public static bool HandleKeyDown(KeyEventArgs e)
    {
        if (!HasActivePopups) return false;

        if (e.Key == Key.Escape)
        {
            for (int i = _activePopups.Count - 1; i >= 0; i--)
            {
                var popup = _activePopups[i];
                if (!popup.StaysOpen)
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
    /// Layout pass for all active popups against current window viewport.
    /// </summary>
    public static void UpdatePopups(Size viewportSize)
    {
        for (int i = 0; i < _activePopups.Count; i++)
        {
            var popup = _activePopups[i];
            if (popup.IsOpen)
            {
                popup.UpdatePlacement(viewportSize);
            }
        }
    }

    /// <summary>
    /// Renders active popups on top of the root visual tree.
    /// </summary>
    public static void RenderPopups(ref DrawingContext context, IElementVisualPresenter? presenter)
    {
        for (int i = 0; i < _activePopups.Count; i++)
        {
            var popup = _activePopups[i];
            if (popup.IsOpen)
            {
                popup.RenderPopup(ref context, presenter);
            }
        }
    }
}
