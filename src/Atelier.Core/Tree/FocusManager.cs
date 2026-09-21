using System;
using System.Collections.Generic;
using Atelier.Core.Events;

namespace Atelier.Core.Tree;

public static class FocusManager
{
    private static UIElement? _currentFocused;
    private static readonly List<UIElement> _modalStack = [];

    public static event Action<UIElement?, UIElement?>? FocusChanged;

    public static UIElement? CurrentFocused => _currentFocused;
    public static UIElement? CurrentModal => _modalStack.Count > 0 ? _modalStack[^1] : null;

    /// <summary>
    /// Pushes a modal element scope (such as a Dialog). All focus cycling will be trapped
    /// within this modal scope, and elements outside the modal scope cannot receive focus.
    /// </summary>
    public static void PushModal(UIElement modalRoot)
    {
        if (!_modalStack.Contains(modalRoot))
        {
            _modalStack.Add(modalRoot);
        }

        // Focus the first focusable element inside the modal scope
        var focusables = new List<UIElement>();
        CollectFocusableElements(modalRoot, focusables);
        if (focusables.Count > 0)
        {
            SetFocus(focusables[0]);
        }
        else if (modalRoot.IsFocusable)
        {
            SetFocus(modalRoot);
        }
        else
        {
            SetFocus(null);
        }
    }

    /// <summary>
    /// Pops a modal element scope when dismissed.
    /// </summary>
    public static void PopModal(UIElement modalRoot)
    {
        _modalStack.Remove(modalRoot);
    }

    /// <summary>
    /// Clears all active modal scopes.
    /// </summary>
    public static void ClearModals()
    {
        _modalStack.Clear();
    }

    /// <summary>
    /// Resolves the effective target element that should receive keyboard input.
    /// If a modal scope is active, the target is guaranteed to be within the modal scope.
    /// </summary>
    public static UIElement? GetEffectiveKeyTarget(UIElement? fallbackRoot = null)
    {
        var modal = CurrentModal;
        if (modal != null)
        {
            if (_currentFocused != null && (_currentFocused == modal || _currentFocused.IsDescendantOf(modal)))
            {
                return _currentFocused;
            }
            return modal;
        }
        return _currentFocused ?? fallbackRoot;
    }

    /// <summary>
    /// Dispatches a key down event starting at the currently focused element (or active modal / fallback)
    /// and bubbling up the visual tree.
    /// </summary>
    public static bool DispatchKeyDown(KeyEventArgs e, UIElement? fallbackRoot = null)
    {
        var target = GetEffectiveKeyTarget(fallbackRoot);
        if (target != null)
        {
            target.DispatchBubbleKeyEvent(e, (el, args) => el.OnKeyDown(args));
            return e.Handled;
        }
        return false;
    }

    /// <summary>
    /// Dispatches a key up event starting at the currently focused element (or active modal / fallback)
    /// and bubbling up the visual tree.
    /// </summary>
    public static bool DispatchKeyUp(KeyEventArgs e, UIElement? fallbackRoot = null)
    {
        var target = GetEffectiveKeyTarget(fallbackRoot);
        if (target != null)
        {
            target.DispatchBubbleKeyEvent(e, (el, args) => el.OnKeyUp(args));
            return e.Handled;
        }
        return false;
    }

    /// <summary>
    /// Dispatches a text input event starting at the currently focused element (or active modal / fallback)
    /// and bubbling up the visual tree.
    /// </summary>
    public static bool DispatchTextInput(TextInputEventArgs e, UIElement? fallbackRoot = null)
    {
        var target = GetEffectiveKeyTarget(fallbackRoot);
        if (target != null)
        {
            target.DispatchBubbleKeyEvent(e, (el, args) => el.OnTextInput(args));
            return e.Handled;
        }
        return false;
    }

    public static void SetFocus(UIElement? element)
    {
        // Ascend to nearest focusable ancestor
        while (element != null && !element.IsFocusable)
        {
            element = element.Parent as UIElement;
        }

        // If a modal scope is active in the same visual tree, reject focusing any element outside the modal scope
        var modal = CurrentModal;
        if (modal != null && element != null)
        {
            if (GetRoot(element) == GetRoot(modal))
            {
                if (element != modal && !element.IsDescendantOf(modal))
                {
                    return;
                }
            }
        }

        if (_currentFocused == element) return;

        var old = _currentFocused;
        if (old != null)
        {
            old.OnLostFocus();
        }

        _currentFocused = element;

        if (_currentFocused != null)
        {
            _currentFocused.OnGotFocus();
        }

        FocusChanged?.Invoke(old, _currentFocused);
    }

    public static bool FocusNext(UIElement root)
    {
        var modal = CurrentModal;
        var effectiveRoot = (modal != null && GetRoot(modal) == GetRoot(root)) ? modal : root;
        var focusables = new List<UIElement>();
        CollectFocusableElements(effectiveRoot, focusables);

        if (focusables.Count == 0) return false;

        int currentIndex = _currentFocused != null ? focusables.IndexOf(_currentFocused) : -1;
        int nextIndex = (currentIndex + 1) % focusables.Count;

        SetFocus(focusables[nextIndex]);
        return true;
    }

    public static bool FocusPrevious(UIElement root)
    {
        var modal = CurrentModal;
        var effectiveRoot = (modal != null && GetRoot(modal) == GetRoot(root)) ? modal : root;
        var focusables = new List<UIElement>();
        CollectFocusableElements(effectiveRoot, focusables);

        if (focusables.Count == 0) return false;

        int currentIndex = _currentFocused != null ? focusables.IndexOf(_currentFocused) : -1;
        int prevIndex = currentIndex <= 0 ? focusables.Count - 1 : currentIndex - 1;

        SetFocus(focusables[prevIndex]);
        return true;
    }

    private static VisualNode GetRoot(VisualNode node)
    {
        VisualNode current = node;
        while (current.Parent != null)
        {
            current = current.Parent;
        }
        return current;
    }

    private static void CollectFocusableElements(UIElement element, List<UIElement> list)
    {
        if (element.Visibility != Primitives.Visibility.Visible || !element.IsEnabled)
            return;

        if (element.IsFocusable)
        {
            list.Add(element);
        }

        for (int i = 0; i < element.Children.Count; i++)
        {
            if (element.Children[i] is UIElement child)
            {
                CollectFocusableElements(child, list);
            }
        }
    }
}
