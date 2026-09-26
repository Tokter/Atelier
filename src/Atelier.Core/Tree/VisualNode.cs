using System;
using System.Collections.Generic;
using System.Numerics;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;

namespace Atelier.Core.Tree;

/// <summary>
/// Base class for nodes in the visual tree: owns the parent/child links, layout and render transforms, coordinate
/// conversion, and upward propagation of visual and layout invalidation.
/// </summary>
/// <remarks>
/// The visual tree is also the inheritance tree for inheritable bindable properties, so attaching, moving or removing
/// a child re-evaluates the inherited values of its subtree.
/// </remarks>
public abstract class VisualNode : BindableObject
{
    private VisualNode? _parent;
    private readonly List<VisualNode> _children = [];

    /// <summary>Gets the parent node, or <c>null</c> if this node is a root or detached.</summary>
    public VisualNode? Parent => _parent;
    /// <summary>Gets the child nodes in insertion order, which is also the render order (last is topmost).</summary>
    public IReadOnlyList<VisualNode> Children => _children;

    /// <inheritdoc/>
    protected override BindableObject? InheritanceParent => _parent;
    /// <inheritdoc/>
    protected override IReadOnlyList<BindableObject> InheritanceChildren => _children;

    /// <summary>Identifies the <see cref="Transform"/> bindable property.</summary>
    public static readonly BindableProperty<Matrix3x2> TransformProperty =
        BindableProperty.Register<VisualNode, Matrix3x2>(
            nameof(Transform),
            Matrix3x2.Identity,
            (s, o, n) => ((VisualNode)s).OnTransformChanged(o, n));

    /// <summary>Identifies the <see cref="TransformOrigin"/> bindable property.</summary>
    public static readonly BindableProperty<Point> TransformOriginProperty =
        BindableProperty.Register<VisualNode, Point>(
            nameof(TransformOrigin),
            Point.Zero,
            (s, o, n) => ((VisualNode)s).OnTransformOriginChanged(o, n));

    /// <summary>Identifies the <see cref="RenderTransform"/> bindable property.</summary>
    public static readonly BindableProperty<Matrix3x2> RenderTransformProperty =
        BindableProperty.Register<VisualNode, Matrix3x2>(
            nameof(RenderTransform),
            Matrix3x2.Identity,
            (s, o, n) => ((VisualNode)s).OnRenderTransformChanged(o, n));

    /// <summary>Identifies the <see cref="RenderTransformOrigin"/> bindable property.</summary>
    public static readonly BindableProperty<Point> RenderTransformOriginProperty =
        BindableProperty.Register<VisualNode, Point>(
            nameof(RenderTransformOrigin),
            new Point(0.5f, 0.5f),
            (s, o, n) => ((VisualNode)s).OnRenderTransformOriginChanged(o, n));

    /// <summary>
    /// Gets or sets the layout transform, applied about <see cref="TransformOrigin"/>. The default is the identity.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="RenderTransform"/>, this transform takes part in layout: changing it invalidates both
    /// rendering and layout, and <see cref="UIElement"/> measures and arranges using the transformed bounds.
    /// </remarks>
    public Matrix3x2 Transform
    {
        get => GetValue(TransformProperty);
        set => SetValue(TransformProperty, value);
    }

    /// <summary>
    /// Gets or sets the origin of <see cref="Transform"/> as a fraction of the node's size, where (0, 0) is the top-left
    /// and (1, 1) the bottom-right corner. The default is <see cref="Point.Zero"/>. Changing it invalidates rendering and layout.
    /// </summary>
    public Point TransformOrigin
    {
        get => GetValue(TransformOriginProperty);
        set => SetValue(TransformOriginProperty, value);
    }

    /// <summary>
    /// Gets or sets a transform applied after layout, about <see cref="RenderTransformOrigin"/>. The default is the identity.
    /// </summary>
    /// <remarks>
    /// It affects rendering, hit testing and coordinate conversion but not layout: changing it only invalidates rendering.
    /// </remarks>
    public Matrix3x2 RenderTransform
    {
        get => GetValue(RenderTransformProperty);
        set => SetValue(RenderTransformProperty, value);
    }

    /// <summary>
    /// Gets or sets the origin of <see cref="RenderTransform"/> as a fraction of the node's size. The default is (0.5, 0.5),
    /// the center. Changing it only invalidates rendering.
    /// </summary>
    public Point RenderTransformOrigin
    {
        get => GetValue(RenderTransformOriginProperty);
        set => SetValue(RenderTransformOriginProperty, value);
    }

    /// <summary>
    /// Occurs when <see cref="InvalidateVisual"/> is called on this node or on any of its descendants.
    /// </summary>
    public event Action? NeedsVisualUpdate;
    /// <summary>
    /// Occurs when <see cref="InvalidateLayout"/> is called on this node or on any of its descendants, including when
    /// children are added or removed.
    /// </summary>
    public event Action? NeedsLayoutUpdate;

    /// <summary>Called when <see cref="Transform"/> changes. The base implementation invalidates rendering and layout.</summary>
    /// <param name="oldValue">The previous transform.</param>
    /// <param name="newValue">The new transform.</param>
    protected virtual void OnTransformChanged(Matrix3x2 oldValue, Matrix3x2 newValue)
    {
        InvalidateVisual();
        InvalidateLayout();
    }

    /// <summary>Called when <see cref="TransformOrigin"/> changes. The base implementation invalidates rendering and layout.</summary>
    /// <param name="oldValue">The previous origin.</param>
    /// <param name="newValue">The new origin.</param>
    protected virtual void OnTransformOriginChanged(Point oldValue, Point newValue)
    {
        InvalidateVisual();
        InvalidateLayout();
    }

    /// <summary>Called when <see cref="RenderTransform"/> changes. The base implementation invalidates rendering only.</summary>
    /// <param name="oldValue">The previous transform.</param>
    /// <param name="newValue">The new transform.</param>
    protected virtual void OnRenderTransformChanged(Matrix3x2 oldValue, Matrix3x2 newValue)
    {
        InvalidateVisual();
    }

    /// <summary>Called when <see cref="RenderTransformOrigin"/> changes. The base implementation invalidates rendering only.</summary>
    /// <param name="oldValue">The previous origin.</param>
    /// <param name="newValue">The new origin.</param>
    protected virtual void OnRenderTransformOriginChanged(Point oldValue, Point newValue)
    {
        InvalidateVisual();
    }

    /// <summary>
    /// Gets the layout transform as it applies to this node. The base implementation returns <see cref="Transform"/>
    /// without taking <see cref="TransformOrigin"/> into account; <see cref="UIElement"/> applies the origin using its bounds.
    /// </summary>
    /// <returns>The effective layout transform.</returns>
    public virtual Matrix3x2 GetEffectiveTransform()
    {
        return Transform;
    }

    /// <summary>
    /// Gets <see cref="Transform"/> applied about <see cref="TransformOrigin"/> for a node of the given size.
    /// </summary>
    /// <remarks>
    /// Returns <see cref="Transform"/> unchanged when the origin is <see cref="Point.Zero"/> or both dimensions are
    /// zero or negative.
    /// </remarks>
    /// <param name="width">The node width used to resolve the relative origin.</param>
    /// <param name="height">The node height used to resolve the relative origin.</param>
    /// <returns>The layout transform, including the translation to and from the origin.</returns>
    public virtual Matrix3x2 GetEffectiveTransform(float width, float height)
    {
        if (Transform.IsIdentity)
            return Matrix3x2.Identity;

        if (TransformOrigin == Point.Zero || (width <= 0 && height <= 0))
            return Transform;

        float ox = width * TransformOrigin.X;
        float oy = height * TransformOrigin.Y;

        return Matrix3x2.CreateTranslation(-ox, -oy) * Transform * Matrix3x2.CreateTranslation(ox, oy);
    }

    /// <summary>
    /// Gets <see cref="RenderTransform"/> applied about <see cref="RenderTransformOrigin"/> for a node of the given size.
    /// </summary>
    /// <remarks>Returns <see cref="RenderTransform"/> unchanged when both dimensions are zero or negative.</remarks>
    /// <param name="width">The node width used to resolve the relative origin.</param>
    /// <param name="height">The node height used to resolve the relative origin.</param>
    /// <returns>The render transform, including the translation to and from the origin.</returns>
    public virtual Matrix3x2 GetEffectiveRenderTransform(float width, float height)
    {
        if (RenderTransform.IsIdentity)
            return Matrix3x2.Identity;

        if (width <= 0 && height <= 0)
            return RenderTransform;

        float ox = width * RenderTransformOrigin.X;
        float oy = height * RenderTransformOrigin.Y;

        return Matrix3x2.CreateTranslation(-ox, -oy) * RenderTransform * Matrix3x2.CreateTranslation(ox, oy);
    }

    /// <summary>
    /// Gets the transform that maps this node's local coordinates into its parent's coordinates.
    /// The base implementation returns <see cref="GetEffectiveTransform()"/>.
    /// </summary>
    /// <returns>The local-to-parent transform.</returns>
    public virtual Matrix3x2 GetLocalTransform()
    {
        return GetEffectiveTransform();
    }

    /// <summary>
    /// Gets the transform that maps this node's local coordinates into the coordinates of <paramref name="ancestor"/>,
    /// by composing <see cref="GetLocalTransform"/> of this node and each intermediate ancestor.
    /// </summary>
    /// <remarks>
    /// The walk stops after an overlay <see cref="UIElement"/> (see <see cref="UIElement.IsOverlayElement"/>), whose
    /// local transform already positions it in window coordinates. If <paramref name="ancestor"/> is not an ancestor,
    /// the result maps to the root.
    /// </remarks>
    /// <param name="ancestor">The ancestor whose coordinate space is the target, or <c>null</c> for the root.</param>
    /// <returns>The composed transform.</returns>
    public Matrix3x2 GetTransformToAncestor(VisualNode? ancestor)
    {
        var matrix = Matrix3x2.Identity;
        VisualNode? curr = this;
        while (curr != null && curr != ancestor)
        {
            matrix *= curr.GetLocalTransform();
            if (curr is UIElement { IsOverlayElement: true })
            {
                break;
            }
            curr = curr.Parent;
        }
        return matrix;
    }

    /// <summary>Gets the transform that maps this node's local coordinates into root (window) coordinates.</summary>
    /// <returns>The composed transform; see <see cref="GetTransformToAncestor"/>.</returns>
    public Matrix3x2 GetTransformToRoot() => GetTransformToAncestor(null);

    /// <summary>Converts a point from this node's local coordinates to root (window) coordinates.</summary>
    /// <param name="localPoint">The point in local coordinates.</param>
    /// <returns>The point in window coordinates.</returns>
    public Point PointToScreen(Point localPoint)
    {
        var matrix = GetTransformToRoot();
        var v = Vector2.Transform(new Vector2(localPoint.X, localPoint.Y), matrix);
        return new Point(v.X, v.Y);
    }

    /// <summary>Converts a point from root (window) coordinates to this node's local coordinates.</summary>
    /// <param name="screenPoint">The point in window coordinates.</param>
    /// <returns>The point in local coordinates, or <paramref name="screenPoint"/> unchanged if the transform to the root is not invertible.</returns>
    public Point PointToClient(Point screenPoint)
    {
        var matrix = GetTransformToRoot();
        if (Matrix3x2.Invert(matrix, out var inv))
        {
            var v = Vector2.Transform(new Vector2(screenPoint.X, screenPoint.Y), inv);
            return new Point(v.X, v.Y);
        }
        return screenPoint;
    }

    /// <summary>Converts a point from this node's local coordinates to another node's local coordinates, via window coordinates.</summary>
    /// <param name="localPoint">The point in this node's coordinates.</param>
    /// <param name="targetNode">The node whose coordinate space is the target.</param>
    /// <returns>The point in <paramref name="targetNode"/>'s coordinates.</returns>
    public Point PointToNode(Point localPoint, VisualNode targetNode)
    {
        var screenPoint = PointToScreen(localPoint);
        return targetNode.PointToClient(screenPoint);
    }

    /// <summary>
    /// Appends <paramref name="child"/> to <see cref="Children"/>, first removing it from its current parent if it has one.
    /// </summary>
    /// <remarks>
    /// Inherited property values of the child's subtree are re-evaluated, raising one change notification per value that
    /// actually changes. Then <see cref="OnChildAdded"/> is called and layout is invalidated.
    /// </remarks>
    /// <param name="child">The node to add.</param>
    public void AddChild(VisualNode child) => AttachChild(null, child);

    /// <summary>
    /// Inserts <paramref name="child"/> into <see cref="Children"/> at <paramref name="index"/>, first removing it from
    /// its current parent if it has one. Otherwise behaves like <see cref="AddChild"/>.
    /// </summary>
    /// <remarks>
    /// When the child is moved within the same parent, it is removed before inserting, so <paramref name="index"/>
    /// refers to the list without the child.
    /// </remarks>
    /// <param name="index">The position to insert at.</param>
    /// <param name="child">The node to insert.</param>
    public void InsertChild(int index, VisualNode child) => AttachChild(index, child);

    private void AttachChild(int? index, VisualNode child)
    {
        ArgumentNullException.ThrowIfNull(child);
        if (child._isHostRoot)
        {
            throw new InvalidOperationException("A node attached to a host as its root cannot be added as a child. Call DetachFromHost() first.");
        }
        if (child == this || IsDescendantOf(child))
        {
            throw new InvalidOperationException("A node cannot be added to itself or to one of its descendants.");
        }

        // Capture inherited values before relinking so a move raises one notification per real change.
        var inherited = child.CaptureInheritedValues();

        var oldParent = child._parent;
        oldParent?.DetachChild(child);

        child._parent = this;
        _children.Insert(index ?? _children.Count, child);

        CommitInheritedValues(inherited);
        child.OnInheritanceParentChanged(oldParent, this);

        // Only a change of attachment raises lifecycle events; moving between two attached parents raises none.
        child.SetAttachedToVisualTree(_isAttachedToVisualTree);

        OnChildAdded(child);
        InvalidateLayout();
    }

    /// <summary>
    /// Removes <paramref name="child"/> from this node, calls <see cref="OnChildRemoved"/>, invalidates layout and
    /// re-evaluates the inherited property values of the removed subtree.
    /// </summary>
    /// <param name="child">The node to remove.</param>
    /// <returns><c>true</c> if the child was removed; <c>false</c> if it is not a child of this node.</returns>
    public bool RemoveChild(VisualNode child)
    {
        if (child._parent != this)
        {
            return false;
        }

        var inherited = child.CaptureInheritedValues();
        DetachChild(child);
        CommitInheritedValues(inherited);
        child.OnInheritanceParentChanged(this, null);
        child.SetAttachedToVisualTree(false);
        return true;
    }

    #region Visual tree lifecycle

    private bool _isAttachedToVisualTree;
    private bool _isHostRoot;

    /// <summary>
    /// Gets a value indicating whether this node is part of a tree whose root has been attached to a host (such as a
    /// window) with <see cref="AttachToHost"/>, i.e. whether it is currently displayed.
    /// </summary>
    public bool IsAttachedToVisualTree => _isAttachedToVisualTree;

    /// <summary>
    /// Occurs when this node becomes part of a hosted tree: when its root is attached to a host, or when it (or an
    /// ancestor) is added under a node that already is.
    /// </summary>
    /// <remarks>
    /// Raised parent-first, after inherited values and styles have been updated. Moving a node between two attached
    /// parents raises neither this event nor <see cref="DetachedFromVisualTree"/>. Use it to acquire resources or
    /// subscribe to long-lived objects, and release them in <see cref="DetachedFromVisualTree"/>.
    /// </remarks>
    public event EventHandler? AttachedToVisualTree;

    /// <summary>
    /// Occurs when this node stops being part of a hosted tree: when it (or an ancestor) is removed from an attached
    /// parent, or when its root is detached from the host.
    /// </summary>
    /// <remarks>Raised children-first, so teardown mirrors <see cref="AttachedToVisualTree"/>.</remarks>
    public event EventHandler? DetachedFromVisualTree;

    /// <summary>
    /// Marks this node as the root of a hosted tree and raises <see cref="AttachedToVisualTree"/> for it and all
    /// descendants. Called by hosts such as windows when they start displaying the tree.
    /// </summary>
    /// <exception cref="InvalidOperationException">This node has a parent.</exception>
    public void AttachToHost()
    {
        if (_parent != null)
        {
            throw new InvalidOperationException("Only a node without a parent can be attached to a host.");
        }

        _isHostRoot = true;
        SetAttachedToVisualTree(true);
    }

    /// <summary>
    /// Ends hosting of this root and raises <see cref="DetachedFromVisualTree"/> for all descendants and this node.
    /// Does nothing if this node is not attached to a host.
    /// </summary>
    public void DetachFromHost()
    {
        if (!_isHostRoot)
        {
            return;
        }

        _isHostRoot = false;
        SetAttachedToVisualTree(false);
    }

    /// <summary>
    /// Called when this node becomes part of a hosted tree. The base implementation raises <see cref="AttachedToVisualTree"/>.
    /// </summary>
    protected virtual void OnAttachedToVisualTree() => AttachedToVisualTree?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Called when this node stops being part of a hosted tree. The base implementation raises <see cref="DetachedFromVisualTree"/>.
    /// </summary>
    protected virtual void OnDetachedFromVisualTree() => DetachedFromVisualTree?.Invoke(this, EventArgs.Empty);

    private void SetAttachedToVisualTree(bool attached)
    {
        if (_isAttachedToVisualTree == attached)
        {
            return;
        }

        _isAttachedToVisualTree = attached;

        // Indexed loops: a handler may add or remove children. Nodes added meanwhile are handled by AttachChild, and the
        // equality check above makes a second visit harmless.
        if (attached)
        {
            OnAttachedToVisualTree();
            for (int i = 0; i < _children.Count; i++)
            {
                _children[i].SetAttachedToVisualTree(true);
            }
        }
        else
        {
            for (int i = _children.Count - 1; i >= 0; i--)
            {
                if (i < _children.Count)
                {
                    _children[i].SetAttachedToVisualTree(false);
                }
            }
            OnDetachedFromVisualTree();
        }
    }

    #endregion

    // Unlinks a child without raising inheritance notifications; callers are responsible for those.
    private void DetachChild(VisualNode child)
    {
        _children.Remove(child);
        child._parent = null;
        OnChildRemoved(child);
        InvalidateLayout();
    }

    /// <summary>Removes all children, last to first, as if by calling <see cref="RemoveChild"/> for each.</summary>
    public void ClearChildren()
    {
        while (_children.Count > 0)
        {
            RemoveChild(_children[^1]);
        }
    }

    /// <summary>
    /// Called after <paramref name="child"/> has been linked to this node and its inherited values updated, before
    /// layout is invalidated. The base implementation does nothing.
    /// </summary>
    /// <param name="child">The added child.</param>
    protected virtual void OnChildAdded(VisualNode child) { }
    /// <summary>
    /// Called after <paramref name="child"/> has been unlinked from this node, before layout is invalidated and before
    /// the child's inherited values are updated. The base implementation does nothing.
    /// </summary>
    /// <param name="child">The removed child.</param>
    protected virtual void OnChildRemoved(VisualNode child) { }

    /// <summary>Determines whether this node is a strict descendant of <paramref name="ancestor"/>.</summary>
    /// <param name="ancestor">The candidate ancestor.</param>
    /// <returns><c>true</c> if <paramref name="ancestor"/> is found among this node's parents; <c>false</c> otherwise, including when it is this node.</returns>
    public bool IsDescendantOf(VisualNode ancestor)
    {
        var current = _parent;
        while (current != null)
        {
            if (current == ancestor) return true;
            current = current.Parent;
        }
        return false;
    }

    /// <summary>
    /// Requests a redraw: raises <see cref="NeedsVisualUpdate"/> on this node and then on every ancestor up to the root.
    /// </summary>
    public virtual void InvalidateVisual()
    {
        NeedsVisualUpdate?.Invoke();
        _parent?.InvalidateVisual();
    }

    /// <summary>
    /// Requests a layout pass: raises <see cref="NeedsLayoutUpdate"/> on this node and then on every ancestor up to the root.
    /// </summary>
    /// <remarks>
    /// This only notifies listeners; it does not mark measure or arrange as invalid. On a <see cref="UIElement"/>, use
    /// <see cref="UIElement.InvalidateMeasure"/> or <see cref="UIElement.InvalidateArrange"/> to do that.
    /// </remarks>
    public virtual void InvalidateLayout()
    {
        NeedsLayoutUpdate?.Invoke();
        _parent?.InvalidateLayout();
    }
}
