using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Atelier.Core.Events;

namespace Atelier.Core.Tree;

/// <summary>
/// Tracks keyboard focus and modal scopes, and routes keyboard input to the focused element.
/// </summary>
/// <remarks>
/// <para>
/// Focus is tracked per element tree (one tree per window): every tree has its own focused element and its own stack
/// of modal scopes, so each window remembers its focus while another window is active. Hosts call
/// <see cref="ActivateRoot"/> when their window becomes active; <see cref="CurrentFocused"/> and
/// <see cref="CurrentModal"/> report the active tree. Without a host (tests, headless use), the active tree is the one
/// in which focus was set last.
/// </para>
/// <para>
/// Elements removed from a tree, or moved to another tree, lose focus there automatically, and modal scopes inside a
/// removed subtree are dropped. Per-tree state is held weakly, so this class never keeps an element tree alive.
/// </para>
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

    private sealed class FocusScope
    {
        public UIElement? Focused;
        public readonly List<ModalScope> ModalStack = [];

        public UIElement? CurrentModal => ModalStack.Count > 0 ? ModalStack[^1].Root : null;
    }

    // Keyed by tree root. Weak keys: a closed window's tree (and the scope's references into it) can be collected.
    private static readonly ConditionalWeakTable<VisualNode, FocusScope> s_scopes = new();
    private static WeakReference<VisualNode>? s_activeRoot;
    private static bool s_activeRootSetByHost;

    // Reused by focus traversal; FocusNext/FocusPrevious/PushModal finish reading it before they change focus.
    private static readonly List<UIElement> s_focusables = [];

    /// <summary>
    /// Occurs after focus moves within a tree, with the previously and newly focused elements (either may be <c>null</c>).
    /// </summary>
    /// <remarks>This is a static event: subscribers stay alive until they unsubscribe.</remarks>
    public static event Action<UIElement?, UIElement?>? FocusChanged;

    /// <summary>Gets the focused element of the active tree (see <see cref="ActivateRoot"/>), or <c>null</c>.</summary>
    public static UIElement? CurrentFocused => ActiveScope?.Focused;

    /// <summary>Gets the innermost modal scope of the active tree, or <c>null</c> if none is active.</summary>
    public static UIElement? CurrentModal => ActiveScope?.CurrentModal;

    /// <summary>Gets the root of the active tree, or <c>null</c>.</summary>
    public static VisualNode? ActiveRoot => s_activeRoot != null && s_activeRoot.TryGetTarget(out var root) ? root : null;

    private static FocusScope? ActiveScope => ActiveRoot is { } root && s_scopes.TryGetValue(root, out var scope) ? scope : null;

    /// <summary>
    /// Marks the tree rooted at <paramref name="root"/> as active, typically because its window received OS focus.
    /// </summary>
    /// <remarks>
    /// Once a host has activated a tree, programmatic <see cref="SetFocus"/> calls in other trees no longer change the
    /// active tree. Passing <c>null</c> clears the active tree.
    /// </remarks>
    /// <param name="root">The root of the tree (the window content), or <c>null</c>.</param>
    public static void ActivateRoot(VisualNode? root)
    {
        s_activeRoot = root == null ? null : new WeakReference<VisualNode>(GetRoot(root));
        s_activeRootSetByHost = root != null;
    }

    /// <summary>Gets the focused element of the tree containing <paramref name="node"/>, or <c>null</c>.</summary>
    public static UIElement? GetFocusedElement(VisualNode node) =>
        s_scopes.TryGetValue(GetRoot(node), out var scope) ? scope.Focused : null;

    /// <summary>Gets the innermost modal scope of the tree containing <paramref name="node"/>, or <c>null</c>.</summary>
    public static UIElement? GetModal(VisualNode node) =>
        s_scopes.TryGetValue(GetRoot(node), out var scope) ? scope.CurrentModal : null;

    /// <summary>
    /// Pushes a modal element scope (such as a Dialog) in the tree containing <paramref name="modalRoot"/>. Focus cycling in
    /// that tree is trapped within the scope, and elements of that tree outside it cannot receive focus.
    /// </summary>
    /// <remarks>
    /// The tree's currently focused element is remembered and gets focus back when the scope is popped with <see cref="PopModal"/>.
    /// </remarks>
    /// <param name="modalRoot">The root element of the modal scope.</param>
    public static void PushModal(UIElement modalRoot)
    {
        ArgumentNullException.ThrowIfNull(modalRoot);

        var scope = GetOrCreateScope(modalRoot);
        if (IndexOfModal(scope, modalRoot) < 0)
        {
            scope.ModalStack.Add(new ModalScope(modalRoot, scope.Focused));
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
            SetScopeFocus(scope, null);
        }
    }

    /// <summary>
    /// Pops a modal element scope when dismissed. If focus was inside the scope (or nowhere), it returns to the
    /// element that was focused when the scope was pushed.
    /// </summary>
    /// <param name="modalRoot">The root element passed to <see cref="PushModal"/>.</param>
    public static void PopModal(UIElement modalRoot)
    {
        if (!TryFindModal(modalRoot, out var scope, out int index))
        {
            return;
        }

        var modal = scope.ModalStack[index];
        scope.ModalStack.RemoveAt(index);

        bool focusWasInScope = scope.Focused == null || IsSelfOrDescendant(scope.Focused, modalRoot);
        if (focusWasInScope)
        {
            SetScopeFocus(scope, modal.PreviousFocus);
        }
    }

    /// <summary>
    /// Clears all modal scopes of the active tree.
    /// </summary>
    public static void ClearModals()
    {
        ActiveScope?.ModalStack.Clear();
    }

    /// <summary>
    /// Called when <paramref name="subtreeRoot"/> leaves the tree rooted at <paramref name="oldRoot"/> (removed, or moved to
    /// another tree): drops focus and modal scopes of that tree inside the subtree, so it can be garbage-collected and no
    /// longer receives that tree's keyboard input.
    /// </summary>
    internal static void OnSubtreeLeftTree(UIElement subtreeRoot, VisualNode oldRoot)
    {
        if (!s_scopes.TryGetValue(oldRoot, out var scope))
        {
            return;
        }

        var modals = scope.ModalStack;
        for (int i = modals.Count - 1; i >= 0; i--)
        {
            var modal = modals[i];
            if (IsSelfOrDescendant(modal.Root, subtreeRoot))
            {
                modals.RemoveAt(i);
            }
            else if (modal.PreviousFocus != null && IsSelfOrDescendant(modal.PreviousFocus, subtreeRoot))
            {
                modals[i] = new ModalScope(modal.Root, previousFocus: null);
            }
        }

        if (scope.Focused != null && IsSelfOrDescendant(scope.Focused, subtreeRoot))
        {
            SetScopeFocus(scope, null);
        }
    }

    /// <summary>
    /// Called when a former tree root gets a parent: its tree's focus state no longer applies and is dropped.
    /// </summary>
    internal static void OnRootAdopted(VisualNode formerRoot)
    {
        if (s_scopes.TryGetValue(formerRoot, out var scope))
        {
            scope.ModalStack.Clear();
            SetScopeFocus(scope, null);
            s_scopes.Remove(formerRoot);
        }
    }

    /// <summary>
    /// Resolves the element that should receive keyboard input in the tree containing <paramref name="fallbackRoot"/>
    /// (or in the active tree when it is <c>null</c>). If that tree has a modal scope, the target is within it.
    /// </summary>
    /// <param name="fallbackRoot">An element of the target tree, used as the target when nothing there is focused.</param>
    public static UIElement? GetEffectiveKeyTarget(UIElement? fallbackRoot = null)
    {
        var scope = fallbackRoot != null ? GetScopeOrNull(fallbackRoot) : ActiveScope;
        var focused = scope?.Focused;
        var modal = scope?.CurrentModal;
        if (modal != null)
        {
            return focused != null && IsSelfOrDescendant(focused, modal) ? focused : modal;
        }
        return focused ?? fallbackRoot;
    }

    /// <summary>
    /// Dispatches a key down event to the focused element of the tree containing <paramref name="fallbackRoot"/> (or of
    /// the active tree), tunneling from the root and then bubbling back up.
    /// </summary>
    /// <returns><c>true</c> if a handler marked the event as handled.</returns>
    public static bool DispatchKeyDown(KeyEventArgs e, UIElement? fallbackRoot = null)
    {
        var target = GetEffectiveKeyTarget(fallbackRoot);
        if (target != null)
        {
            target.DispatchKeyEvent(e, static (el, args) => el.OnPreviewKeyDown(args), static (el, args) => el.OnKeyDown(args));
            return e.Handled;
        }
        return false;
    }

    /// <summary>
    /// Dispatches a key up event to the focused element of the tree containing <paramref name="fallbackRoot"/> (or of the
    /// active tree), tunneling from the root and then bubbling back up.
    /// </summary>
    /// <returns><c>true</c> if a handler marked the event as handled.</returns>
    public static bool DispatchKeyUp(KeyEventArgs e, UIElement? fallbackRoot = null)
    {
        var target = GetEffectiveKeyTarget(fallbackRoot);
        if (target != null)
        {
            target.DispatchKeyEvent(e, static (el, args) => el.OnPreviewKeyUp(args), static (el, args) => el.OnKeyUp(args));
            return e.Handled;
        }
        return false;
    }

    /// <summary>
    /// Dispatches a text input event to the focused element of the tree containing <paramref name="fallbackRoot"/> (or of
    /// the active tree), tunneling from the root and then bubbling back up.
    /// </summary>
    /// <returns><c>true</c> if a handler marked the event as handled.</returns>
    public static bool DispatchTextInput(TextInputEventArgs e, UIElement? fallbackRoot = null)
    {
        var target = GetEffectiveKeyTarget(fallbackRoot);
        if (target != null)
        {
            target.DispatchKeyEvent(e, static (el, args) => el.OnPreviewTextInput(args), static (el, args) => el.OnTextInput(args));
            return e.Handled;
        }
        return false;
    }

    /// <summary>
    /// Moves focus within the tree containing <paramref name="element"/> to that element, or to its nearest focusable
    /// ancestor if it is not focusable.
    /// </summary>
    /// <remarks>
    /// While the tree has a modal scope, requests for elements outside it are ignored. Passing <c>null</c> clears the
    /// focus of the active tree; use <see cref="ClearFocus"/> to clear a specific tree.
    /// </remarks>
    /// <param name="element">The element to focus, or <c>null</c>.</param>
    public static void SetFocus(UIElement? element)
    {
        if (element == null)
        {
            if (ActiveScope is { } activeScope)
            {
                SetScopeFocus(activeScope, null);
            }
            return;
        }

        var root = GetRoot(element);

        // Ascend to nearest focusable ancestor
        UIElement? target = element;
        while (target != null && !target.IsFocusable)
        {
            target = target.Parent as UIElement;
        }

        var scope = GetOrCreateScope(root);

        // Reject focusing any element outside the tree's modal scope
        var modal = scope.CurrentModal;
        if (modal != null && target != null && !IsSelfOrDescendant(target, modal))
        {
            return;
        }

        if (!s_activeRootSetByHost)
        {
            s_activeRoot = new WeakReference<VisualNode>(root);
        }

        SetScopeFocus(scope, target);
    }

    /// <summary>Clears the focus of the tree containing <paramref name="node"/>.</summary>
    /// <param name="node">Any node of the tree, typically its root.</param>
    public static void ClearFocus(VisualNode node)
    {
        if (s_scopes.TryGetValue(GetRoot(node), out var scope))
        {
            SetScopeFocus(scope, null);
        }
    }

    private static void SetScopeFocus(FocusScope scope, UIElement? element)
    {
        if (scope.Focused == element) return;

        var old = scope.Focused;
        old?.OnLostFocus();

        scope.Focused = element;
        element?.OnGotFocus();

        FocusChanged?.Invoke(old, element);
    }

    /// <summary>
    /// Moves focus to the next focusable element in tree order (wrapping around), within the tree's modal scope if any.
    /// </summary>
    /// <param name="root">The root of the tree to search when no modal scope applies.</param>
    /// <returns><c>true</c> if there was an element to focus.</returns>
    public static bool FocusNext(UIElement root) => MoveFocus(root, forward: true);

    /// <summary>
    /// Moves focus to the previous focusable element in tree order (wrapping around), within the tree's modal scope if any.
    /// </summary>
    /// <param name="root">The root of the tree to search when no modal scope applies.</param>
    /// <returns><c>true</c> if there was an element to focus.</returns>
    public static bool FocusPrevious(UIElement root) => MoveFocus(root, forward: false);

    private static bool MoveFocus(UIElement root, bool forward)
    {
        var scope = GetScopeOrNull(root);
        var effectiveRoot = scope?.CurrentModal ?? root;

        s_focusables.Clear();
        CollectFocusableElements(effectiveRoot, s_focusables);
        int count = s_focusables.Count;
        if (count == 0)
        {
            return false;
        }

        var focused = scope?.Focused;
        int currentIndex = focused != null ? s_focusables.IndexOf(focused) : -1;
        int targetIndex = forward
            ? (currentIndex + 1) % count
            : (currentIndex <= 0 ? count - 1 : currentIndex - 1);

        var target = s_focusables[targetIndex];
        s_focusables.Clear();

        SetFocus(target);
        return true;
    }

    private static FocusScope GetOrCreateScope(VisualNode node) => s_scopes.GetValue(GetRoot(node), static _ => new FocusScope());

    private static FocusScope? GetScopeOrNull(VisualNode node) => s_scopes.TryGetValue(GetRoot(node), out var scope) ? scope : null;

    private static int IndexOfModal(FocusScope scope, UIElement modalRoot)
    {
        for (int i = 0; i < scope.ModalStack.Count; i++)
        {
            if (scope.ModalStack[i].Root == modalRoot)
            {
                return i;
            }
        }
        return -1;
    }

    // The modal is normally in its current tree's scope; if it was moved or detached meanwhile, search all scopes.
    private static bool TryFindModal(UIElement modalRoot, out FocusScope scope, out int index)
    {
        if (GetScopeOrNull(modalRoot) is { } current && (index = IndexOfModal(current, modalRoot)) >= 0)
        {
            scope = current;
            return true;
        }

        foreach (var entry in s_scopes)
        {
            if ((index = IndexOfModal(entry.Value, modalRoot)) >= 0)
            {
                scope = entry.Value;
                return true;
            }
        }

        scope = null!;
        index = -1;
        return false;
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
