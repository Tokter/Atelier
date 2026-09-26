using System;
using System.Collections.Generic;
using Atelier.Core.Events;

namespace Atelier.Core.Tree;

/// <summary>
/// Tracks keyboard focus and modal scopes, and routes keyboard input to the focused element.
/// </summary>
/// <remarks>
/// Focus state is global (one focused element for the application). Elements removed from the tree lose focus
/// automatically, and modal scopes inside a removed subtree are dropped, so this class never keeps a detached
/// element alive.
/// </remarks>
public static class FocusManager
{
    private readonly struct ModalScope
    {
        public ModalScope(UIElement root, UIElement? previousFocus)
        {
            Root = root;
            PreviousFocus = previousFocus;
        }

        public UIElement Root { get; }
        public UIElement? PreviousFocus { get; }
    }

    private static UIElement? _currentFocused;
    private static readonly List<ModalScope> _modalStack = [];

    // Reused by focus traversal; FocusNext/FocusPrevious/PushModal finish reading it before they change focus.
    private static readonly List<UIElement> s_focusables = [];

    /// <summary>
    /// Occurs after focus moves, with the previously and newly focused elements (either may be <c>null</c>).
    /// </summary>
    /// <remarks>This is a static event: subscribers stay alive until they unsubscribe.</remarks>
    public static event Action<UIElement?, UIElement?>? FocusChanged;

    /// <summary>Gets the element that currently has keyboard focus, or <c>null</c>.</summary>
    public static UIElement? CurrentFocused => _currentFocused;

    /// <summary>Gets the innermost active modal scope, or <c>null</c> if none is active.</summary>
    public static UIElement? CurrentModal => _modalStack.Count > 0 ? _modalStack[^1].Root : null;

    /// <summary>
    /// Pushes a modal element scope (such as a Dialog). All focus cycling will be trapped
    /// within this modal scope, and elements outside the modal scope cannot receive focus.
    /// </summary>
    /// <remarks>
    /// The currently focused element is remembered and gets focus back when the scope is popped with <see cref="PopModal"/>.
    /// </remarks>
    /// <param name="modalRoot">The root element of the modal scope.</param>
    public static void PushModal(UIElement modalRoot)
    {
        ArgumentNullException.ThrowIfNull(modalRoot);

        if (IndexOfModal(modalRoot) < 0)
        {
            _modalStack.Add(new ModalScope(modalRoot, _currentFocused));
        }

        // Focus the first focusable element inside the modal scope
        s_focusables.Clear();
        CollectFocusableElements(modalRoot, s_focusables);
        var first = s_focusables.Count > 0 ? s_focusables[0] : null;
        s_focusables.Clear();

        if (first != null)
        {
            SetFocus(first);
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
    /// Pops a modal element scope when dismissed. If focus was inside the scope (or nowhere), it returns to the
    /// element that was focused when the scope was pushed.
    /// </summary>
    /// <param name="modalRoot">The root element passed to <see cref="PushModal"/>.</param>
    public static void PopModal(UIElement modalRoot)
    {
        int index = IndexOfModal(modalRoot);
        if (index < 0)
        {
            return;
        }

        var scope = _modalStack[index];
        _modalStack.RemoveAt(index);

        bool focusWasInScope = _currentFocused == null || IsSelfOrDescendant(_currentFocused, modalRoot);
        if (focusWasInScope)
        {
            SetFocus(scope.PreviousFocus);
        }
    }

    /// <summary>
    /// Clears all active modal scopes.
    /// </summary>
    public static void ClearModals()
    {
        _modalStack.Clear();
    }

    /// <summary>
    /// Called when <paramref name="subtreeRoot"/> is removed from its parent: drops focus and modal scopes inside the
    /// removed subtree so that it can be garbage-collected and no longer receives keyboard input.
    /// </summary>
    internal static void OnSubtreeDetached(UIElement subtreeRoot)
    {
        for (int i = _modalStack.Count - 1; i >= 0; i--)
        {
            var scope = _modalStack[i];
            if (IsSelfOrDescendant(scope.Root, subtreeRoot))
            {
                _modalStack.RemoveAt(i);
            }
            else if (scope.PreviousFocus != null && IsSelfOrDescendant(scope.PreviousFocus, subtreeRoot))
            {
                _modalStack[i] = new ModalScope(scope.Root, previousFocus: null);
            }
        }

        if (_currentFocused != null && IsSelfOrDescendant(_currentFocused, subtreeRoot))
        {
            SetFocus(null);
        }
    }

    /// <summary>
    /// Resolves the effective target element that should receive keyboard input.
    /// If a modal scope is active, the target is guaranteed to be within the modal scope.
    /// </summary>
    /// <param name="fallbackRoot">The element to use when nothing is focused and no modal scope is active.</param>
    public static UIElement? GetEffectiveKeyTarget(UIElement? fallbackRoot = null)
    {
        var modal = CurrentModal;
        if (modal != null)
        {
            if (_currentFocused != null && IsSelfOrDescendant(_currentFocused, modal))
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
    /// <returns><c>true</c> if a handler marked the event as handled.</returns>
    public static bool DispatchKeyDown(KeyEventArgs e, UIElement? fallbackRoot = null)
    {
        var target = GetEffectiveKeyTarget(fallbackRoot);
        if (target != null)
        {
            target.DispatchBubbleKeyEvent(e, static (el, args) => el.OnKeyDown(args));
            return e.Handled;
        }
        return false;
    }

    /// <summary>
    /// Dispatches a key up event starting at the currently focused element (or active modal / fallback)
    /// and bubbling up the visual tree.
    /// </summary>
    /// <returns><c>true</c> if a handler marked the event as handled.</returns>
    public static bool DispatchKeyUp(KeyEventArgs e, UIElement? fallbackRoot = null)
    {
        var target = GetEffectiveKeyTarget(fallbackRoot);
        if (target != null)
        {
            target.DispatchBubbleKeyEvent(e, static (el, args) => el.OnKeyUp(args));
            return e.Handled;
        }
        return false;
    }

    /// <summary>
    /// Dispatches a text input event starting at the currently focused element (or active modal / fallback)
    /// and bubbling up the visual tree.
    /// </summary>
    /// <returns><c>true</c> if a handler marked the event as handled.</returns>
    public static bool DispatchTextInput(TextInputEventArgs e, UIElement? fallbackRoot = null)
    {
        var target = GetEffectiveKeyTarget(fallbackRoot);
        if (target != null)
        {
            target.DispatchBubbleKeyEvent(e, static (el, args) => el.OnTextInput(args));
            return e.Handled;
        }
        return false;
    }

    /// <summary>
    /// Moves keyboard focus to <paramref name="element"/>, or to its nearest focusable ancestor if it is not focusable.
    /// </summary>
    /// <remarks>
    /// While a modal scope is active, requests for elements outside it (in the same tree) are ignored.
    /// Pass <c>null</c> to clear focus.
    /// </remarks>
    /// <param name="element">The element to focus, or <c>null</c>.</param>
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
            if (GetRoot(element) == GetRoot(modal) && !IsSelfOrDescendant(element, modal))
            {
                return;
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

    /// <summary>
    /// Moves focus to the next focusable element in tree order (wrapping around), within the active modal scope if any.
    /// </summary>
    /// <param name="root">The root of the tree to search when no modal scope applies.</param>
    /// <returns><c>true</c> if there was an element to focus.</returns>
    public static bool FocusNext(UIElement root) => MoveFocus(root, forward: true);

    /// <summary>
    /// Moves focus to the previous focusable element in tree order (wrapping around), within the active modal scope if any.
    /// </summary>
    /// <param name="root">The root of the tree to search when no modal scope applies.</param>
    /// <returns><c>true</c> if there was an element to focus.</returns>
    public static bool FocusPrevious(UIElement root) => MoveFocus(root, forward: false);

    private static bool MoveFocus(UIElement root, bool forward)
    {
        var modal = CurrentModal;
        var effectiveRoot = (modal != null && GetRoot(modal) == GetRoot(root)) ? modal : root;

        s_focusables.Clear();
        CollectFocusableElements(effectiveRoot, s_focusables);
        int count = s_focusables.Count;
        if (count == 0)
        {
            return false;
        }

        int currentIndex = _currentFocused != null ? s_focusables.IndexOf(_currentFocused) : -1;
        int targetIndex = forward
            ? (currentIndex + 1) % count
            : (currentIndex <= 0 ? count - 1 : currentIndex - 1);

        var target = s_focusables[targetIndex];
        s_focusables.Clear();

        SetFocus(target);
        return true;
    }

    private static int IndexOfModal(UIElement modalRoot)
    {
        for (int i = 0; i < _modalStack.Count; i++)
        {
            if (_modalStack[i].Root == modalRoot)
            {
                return i;
            }
        }
        return -1;
    }

    private static bool IsSelfOrDescendant(UIElement element, UIElement ancestor) =>
        element == ancestor || element.IsDescendantOf(ancestor);

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
