using System;
using System.Collections.Generic;
using System.Numerics;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;

namespace Atelier.Core.Tree;

public abstract class VisualNode : BindableObject
{
    private VisualNode? _parent;
    private readonly List<VisualNode> _children = [];

    public VisualNode? Parent => _parent;
    public IReadOnlyList<VisualNode> Children => _children;

    protected override BindableObject? InheritanceParent => _parent;
    protected override IEnumerable<BindableObject> InheritanceChildren => _children;

    public static readonly BindableProperty<Matrix3x2> TransformProperty =
        BindableProperty.Register<VisualNode, Matrix3x2>(
            nameof(Transform),
            Matrix3x2.Identity,
            (s, o, n) => ((VisualNode)s).OnTransformChanged(o, n));

    public static readonly BindableProperty<Point> TransformOriginProperty =
        BindableProperty.Register<VisualNode, Point>(
            nameof(TransformOrigin),
            Point.Zero,
            (s, o, n) => ((VisualNode)s).OnTransformOriginChanged(o, n));

    public static readonly BindableProperty<Matrix3x2> RenderTransformProperty =
        BindableProperty.Register<VisualNode, Matrix3x2>(
            nameof(RenderTransform),
            Matrix3x2.Identity,
            (s, o, n) => ((VisualNode)s).OnRenderTransformChanged(o, n));

    public static readonly BindableProperty<Point> RenderTransformOriginProperty =
        BindableProperty.Register<VisualNode, Point>(
            nameof(RenderTransformOrigin),
            new Point(0.5f, 0.5f),
            (s, o, n) => ((VisualNode)s).OnRenderTransformOriginChanged(o, n));

    public Matrix3x2 Transform
    {
        get => GetValue(TransformProperty);
        set => SetValue(TransformProperty, value);
    }

    public Point TransformOrigin
    {
        get => GetValue(TransformOriginProperty);
        set => SetValue(TransformOriginProperty, value);
    }

    public Matrix3x2 RenderTransform
    {
        get => GetValue(RenderTransformProperty);
        set => SetValue(RenderTransformProperty, value);
    }

    public Point RenderTransformOrigin
    {
        get => GetValue(RenderTransformOriginProperty);
        set => SetValue(RenderTransformOriginProperty, value);
    }

    public event Action? NeedsVisualUpdate;
    public event Action? NeedsLayoutUpdate;

    protected virtual void OnTransformChanged(Matrix3x2 oldValue, Matrix3x2 newValue)
    {
        InvalidateVisual();
        InvalidateLayout();
    }

    protected virtual void OnTransformOriginChanged(Point oldValue, Point newValue)
    {
        InvalidateVisual();
        InvalidateLayout();
    }

    protected virtual void OnRenderTransformChanged(Matrix3x2 oldValue, Matrix3x2 newValue)
    {
        InvalidateVisual();
    }

    protected virtual void OnRenderTransformOriginChanged(Point oldValue, Point newValue)
    {
        InvalidateVisual();
    }

    public virtual Matrix3x2 GetEffectiveTransform()
    {
        return Transform;
    }

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

    public virtual Matrix3x2 GetLocalTransform()
    {
        return GetEffectiveTransform();
    }

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

    public Matrix3x2 GetTransformToRoot() => GetTransformToAncestor(null);

    public Point PointToScreen(Point localPoint)
    {
        var matrix = GetTransformToRoot();
        var v = Vector2.Transform(new Vector2(localPoint.X, localPoint.Y), matrix);
        return new Point(v.X, v.Y);
    }

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

    public Point PointToNode(Point localPoint, VisualNode targetNode)
    {
        var screenPoint = PointToScreen(localPoint);
        return targetNode.PointToClient(screenPoint);
    }

    public void AddChild(VisualNode child)
    {
        if (child._parent != null)
        {
            child._parent.RemoveChild(child);
        }

        var oldParent = child._parent;
        child._parent = this;
        _children.Add(child);

        child.OnInheritanceParentChanged(oldParent, this);
        OnChildAdded(child);
        InvalidateLayout();
    }

    public void InsertChild(int index, VisualNode child)
    {
        if (child._parent != null)
        {
            child._parent.RemoveChild(child);
        }

        var oldParent = child._parent;
        child._parent = this;
        _children.Insert(index, child);

        child.OnInheritanceParentChanged(oldParent, this);
        OnChildAdded(child);
        InvalidateLayout();
    }

    public bool RemoveChild(VisualNode child)
    {
        if (_children.Remove(child))
        {
            child._parent = null;
            child.OnInheritanceParentChanged(this, null);
            OnChildRemoved(child);
            InvalidateLayout();
            return true;
        }

        return false;
    }

    public void ClearChildren()
    {
        for (int i = 0; i < _children.Count; i++)
        {
            var child = _children[i];
            child._parent = null;
            child.OnInheritanceParentChanged(this, null);
            OnChildRemoved(child);
        }
        _children.Clear();
        InvalidateLayout();
    }

    protected virtual void OnChildAdded(VisualNode child) { }
    protected virtual void OnChildRemoved(VisualNode child) { }

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

    public virtual void InvalidateVisual()
    {
        NeedsVisualUpdate?.Invoke();
        _parent?.InvalidateVisual();
    }

    public virtual void InvalidateLayout()
    {
        NeedsLayoutUpdate?.Invoke();
        _parent?.InvalidateLayout();
    }
}
