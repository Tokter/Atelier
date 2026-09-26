using System;
using System.Collections.Generic;
using System.Numerics;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Styling;

namespace Atelier.Core.Tree;

public abstract class UIElement : VisualNode
{
    #region Bindable Properties

    public static readonly BindableProperty<float> WidthProperty =
        BindableProperty.Register<UIElement, float>(nameof(Width), float.NaN, options: PropertyOptions.AffectsMeasure);

    public static readonly BindableProperty<float> HeightProperty =
        BindableProperty.Register<UIElement, float>(nameof(Height), float.NaN, options: PropertyOptions.AffectsMeasure);

    public static readonly BindableProperty<float> MinWidthProperty =
        BindableProperty.Register<UIElement, float>(nameof(MinWidth), 0f, options: PropertyOptions.AffectsMeasure);

    public static readonly BindableProperty<float> MaxWidthProperty =
        BindableProperty.Register<UIElement, float>(nameof(MaxWidth), float.PositiveInfinity, options: PropertyOptions.AffectsMeasure);

    public static readonly BindableProperty<float> MinHeightProperty =
        BindableProperty.Register<UIElement, float>(nameof(MinHeight), 0f, options: PropertyOptions.AffectsMeasure);

    public static readonly BindableProperty<float> MaxHeightProperty =
        BindableProperty.Register<UIElement, float>(nameof(MaxHeight), float.PositiveInfinity, options: PropertyOptions.AffectsMeasure);

    public static readonly BindableProperty<Thickness> MarginProperty =
        BindableProperty.Register<UIElement, Thickness>(nameof(Margin), Thickness.Zero, options: PropertyOptions.AffectsMeasure);

    public static readonly BindableProperty<HorizontalAlignment> HorizontalAlignmentProperty =
        BindableProperty.Register<UIElement, HorizontalAlignment>(nameof(HorizontalAlignment), HorizontalAlignment.Stretch, options: PropertyOptions.AffectsArrange);

    public static readonly BindableProperty<VerticalAlignment> VerticalAlignmentProperty =
        BindableProperty.Register<UIElement, VerticalAlignment>(nameof(VerticalAlignment), VerticalAlignment.Stretch, options: PropertyOptions.AffectsArrange);

    public static readonly BindableProperty<Visibility> VisibilityProperty =
        BindableProperty.Register<UIElement, Visibility>(nameof(Visibility), Visibility.Visible, options: PropertyOptions.AffectsMeasure);

    public static readonly BindableProperty<float> OpacityProperty =
        BindableProperty.Register<UIElement, float>(nameof(Opacity), 1.0f, options: PropertyOptions.AffectsRender);

    public static readonly BindableProperty<bool> IsEnabledProperty =
        BindableProperty.Register<UIElement, bool>(nameof(IsEnabled), true, options: PropertyOptions.AffectsRender, inherits: true);

    public static readonly BindableProperty<bool> ClipToBoundsProperty =
        BindableProperty.Register<UIElement, bool>(nameof(ClipToBounds), false, options: PropertyOptions.AffectsRender);

    public static readonly BindableProperty<string?> StyleKeyProperty =
        BindableProperty.Register<UIElement, string?>(
            nameof(StyleKey),
            null,
            (s, o, n) => ((UIElement)s).OnStyleKeyChanged(o, n));

    #endregion

    #region Property Accessors

    public float Width { get => GetValue(WidthProperty); set => SetValue(WidthProperty, value); }
    public float Height { get => GetValue(HeightProperty); set => SetValue(HeightProperty, value); }
    public float MinWidth { get => GetValue(MinWidthProperty); set => SetValue(MinWidthProperty, value); }
    public float MaxWidth { get => GetValue(MaxWidthProperty); set => SetValue(MaxWidthProperty, value); }
    public float MinHeight { get => GetValue(MinHeightProperty); set => SetValue(MinHeightProperty, value); }
    public float MaxHeight { get => GetValue(MaxHeightProperty); set => SetValue(MaxHeightProperty, value); }
    public Thickness Margin { get => GetValue(MarginProperty); set => SetValue(MarginProperty, value); }
    public HorizontalAlignment HorizontalAlignment { get => GetValue(HorizontalAlignmentProperty); set => SetValue(HorizontalAlignmentProperty, value); }
    public VerticalAlignment VerticalAlignment { get => GetValue(VerticalAlignmentProperty); set => SetValue(VerticalAlignmentProperty, value); }
    public Visibility Visibility { get => GetValue(VisibilityProperty); set => SetValue(VisibilityProperty, value); }
    public float Opacity { get => GetValue(OpacityProperty); set => SetValue(OpacityProperty, value); }
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }
    public bool ClipToBounds { get => GetValue(ClipToBoundsProperty); set => SetValue(ClipToBoundsProperty, value); }
    public string? StyleKey { get => GetValue(StyleKeyProperty); set => SetValue(StyleKeyProperty, value); }

    private Style? _style;
    public Style? Style
    {
        get => _style;
        set
        {
            if (_style != value)
            {
                _style = value;
                ApplyStyles();
            }
        }
    }

    public StyleCollection Styles { get; } = new();

    #endregion

    public UIElement()
    {
        Styles.StylesChanged += () => ApplyStylesToTree();
    }

    private void OnStyleKeyChanged(string? oldValue, string? newValue)
    {
        ApplyStyles();
    }

    public void ApplyStyles()
    {
        Style? resolvedStyle = ResolveStyle();
        if (resolvedStyle != null)
        {
            var chain = new List<Style>();
            var cur = resolvedStyle;
            while (cur != null)
            {
                chain.Add(cur);
                cur = cur.BasedOn;
            }
            chain.Reverse();

            var effectiveSetters = new Dictionary<int, Setter>();
            for (int i = 0; i < chain.Count; i++)
            {
                var s = chain[i];
                for (int j = 0; j < s.Setters.Count; j++)
                {
                    var setter = s.Setters[j];
                    effectiveSetters[setter.Property.Id] = setter;
                }
            }

            SetStyleValues(effectiveSetters.Values);
        }
        else
        {
            SetStyleValues(null);
        }
    }

    public void ApplyStylesToTree()
    {
        ApplyStyles();
        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is UIElement child)
            {
                child.ApplyStylesToTree();
            }
        }
    }

    protected virtual Style? ResolveStyle()
    {
        if (_style != null) return _style;

        string? key = StyleKey;
        if (!string.IsNullOrEmpty(key))
        {
            UIElement? current = this;
            while (current != null)
            {
                for (int i = 0; i < current.Styles.Count; i++)
                {
                    var s = current.Styles[i];
                    if (s.Key == key && (s.TargetType == null || s.TargetType.IsInstanceOfType(this)))
                    {
                        return s;
                    }
                }
                current = current.Parent as UIElement;
            }

            for (int i = 0; i < StyleManager.GlobalStyles.Count; i++)
            {
                var s = StyleManager.GlobalStyles[i];
                if (s.Key == key && (s.TargetType == null || s.TargetType.IsInstanceOfType(this)))
                {
                    return s;
                }
            }

            return null;
        }

        Type controlType = GetType();
        UIElement? curr = this;
        while (curr != null)
        {
            for (int i = 0; i < curr.Styles.Count; i++)
            {
                var s = curr.Styles[i];
                if (string.IsNullOrEmpty(s.Key) && s.TargetType != null && s.TargetType.IsAssignableFrom(controlType))
                {
                    return s;
                }
            }
            curr = curr.Parent as UIElement;
        }

        for (int i = 0; i < StyleManager.GlobalStyles.Count; i++)
        {
            var s = StyleManager.GlobalStyles[i];
            if (string.IsNullOrEmpty(s.Key) && s.TargetType != null && s.TargetType.IsAssignableFrom(controlType))
            {
                return s;
            }
        }

        return null;
    }

    protected override void OnPropertyValueChanged(BindableProperty property, object? oldValue, object? newValue)
    {
        base.OnPropertyValueChanged(property, oldValue, newValue);

        var options = property.Options;
        if (options == PropertyOptions.None)
        {
            return;
        }

        if ((options & PropertyOptions.AffectsMeasure) != 0)
        {
            InvalidateMeasure();
        }
        else if ((options & PropertyOptions.AffectsArrange) != 0)
        {
            InvalidateArrange();
        }

        if ((options & PropertyOptions.AffectsRender) != 0)
        {
            InvalidateVisual();
        }
    }

    internal override void OnInheritanceParentChanged(BindableObject? oldParent, BindableObject? newParent)
    {
        base.OnInheritanceParentChanged(oldParent, newParent);

        // Style resolution walks up the tree, so the whole moved subtree may resolve different styles now.
        ApplyStylesToTree();
    }

    protected override void OnChildAdded(VisualNode child)
    {
        base.OnChildAdded(child);
        InvalidateMeasure();
    }

    protected override void OnChildRemoved(VisualNode child)
    {
        base.OnChildRemoved(child);
        InvalidateMeasure();
    }

    public Size DesiredSize { get; private set; } = Size.Zero;
    public Rect Bounds { get; private set; } = Rect.Zero;
    public Size RenderSize => Bounds.Size;

    public bool IsMeasureValid { get; private set; }
    public bool IsArrangeValid { get; private set; }

    private static readonly BindablePropertyKey<bool> IsHoveredPropertyKey =
        BindableProperty.RegisterReadOnly<UIElement, bool>(nameof(IsHovered), false);
    private static readonly BindablePropertyKey<bool> IsPressedPropertyKey =
        BindableProperty.RegisterReadOnly<UIElement, bool>(nameof(IsPressed), false);
    private static readonly BindablePropertyKey<bool> IsFocusedPropertyKey =
        BindableProperty.RegisterReadOnly<UIElement, bool>(nameof(IsFocused), false);

    /// <summary>Identifies the read-only <see cref="IsHovered"/> property.</summary>
    public static readonly BindableProperty<bool> IsHoveredProperty = IsHoveredPropertyKey.Property;
    /// <summary>Identifies the read-only <see cref="IsPressed"/> property.</summary>
    public static readonly BindableProperty<bool> IsPressedProperty = IsPressedPropertyKey.Property;
    /// <summary>Identifies the read-only <see cref="IsFocused"/> property.</summary>
    public static readonly BindableProperty<bool> IsFocusedProperty = IsFocusedPropertyKey.Property;

    public bool IsHovered { get => GetValue(IsHoveredProperty); internal set => SetValue(IsHoveredPropertyKey, value); }
    public bool IsPressed { get => GetValue(IsPressedProperty); internal set => SetValue(IsPressedPropertyKey, value); }
    public bool IsFocused { get => GetValue(IsFocusedProperty); internal set => SetValue(IsFocusedPropertyKey, value); }
    public bool IsFocusable { get; set; } = false;
    public bool IsHitTestVisible { get; set; } = true;
    public bool IsOverlayElement { get; protected set; } = false;
    public virtual Point OverlayOrigin => Point.Zero;

    public void Focus()
    {
        FocusManager.SetFocus(this);
    }

    public void Unfocus()
    {
        if (FocusManager.CurrentFocused == this)
        {
            FocusManager.SetFocus(null);
        }
    }

    public static event Action<UIElement?>? PointerCaptureChanged;
    public static UIElement? CapturedElement { get; private set; }
    public bool IsPointerCaptured => CapturedElement == this;

    public bool CapturePointer()
    {
        if (CapturedElement != this)
        {
            CapturedElement = this;
            PointerCaptureChanged?.Invoke(this);
        }
        return true;
    }

    public void ReleasePointerCapture()
    {
        if (CapturedElement == this)
        {
            CapturedElement = null;
            PointerCaptureChanged?.Invoke(null);
        }
    }

    public static void ReleaseCurrentPointerCapture()
    {
        if (CapturedElement != null)
        {
            CapturedElement = null;
            PointerCaptureChanged?.Invoke(null);
        }
    }

    protected override void OnTransformChanged(Matrix3x2 oldValue, Matrix3x2 newValue)
    {
        base.OnTransformChanged(oldValue, newValue);
        InvalidateMeasure();
    }

    protected override void OnTransformOriginChanged(Point oldValue, Point newValue)
    {
        base.OnTransformOriginChanged(oldValue, newValue);
        InvalidateMeasure();
    }

    public override Matrix3x2 GetEffectiveTransform()
    {
        return GetEffectiveTransform(Bounds.Width, Bounds.Height);
    }

    public Rect ComputeTransformedBounds(float width, float height)
    {
        var eff = GetEffectiveTransform(width, height);
        if (eff.IsIdentity)
        {
            return new Rect(0, 0, width, height);
        }

        var p0 = Vector2.Transform(new Vector2(0, 0), eff);
        var p1 = Vector2.Transform(new Vector2(width, 0), eff);
        var p2 = Vector2.Transform(new Vector2(width, height), eff);
        var p3 = Vector2.Transform(new Vector2(0, height), eff);

        float minX = MathF.Min(MathF.Min(p0.X, p1.X), MathF.Min(p2.X, p3.X));
        float maxX = MathF.Max(MathF.Max(p0.X, p1.X), MathF.Max(p2.X, p3.X));
        float minY = MathF.Min(MathF.Min(p0.Y, p1.Y), MathF.Min(p2.Y, p3.Y));
        float maxY = MathF.Max(MathF.Max(p0.Y, p1.Y), MathF.Max(p2.Y, p3.Y));

        return new Rect(minX, minY, MathF.Max(0, maxX - minX), MathF.Max(0, maxY - minY));
    }

    public override Matrix3x2 GetLocalTransform()
    {
        var translation = IsOverlayElement
            ? Matrix3x2.CreateTranslation(OverlayOrigin.X, OverlayOrigin.Y)
            : Matrix3x2.CreateTranslation(Bounds.X, Bounds.Y);

        var eff = GetEffectiveTransform();
        var baseTransform = eff.IsIdentity ? translation : (eff * translation);

        if (RenderTransform.IsIdentity)
            return baseTransform;

        var renderEff = GetEffectiveRenderTransform(Bounds.Width, Bounds.Height);
        return renderEff.IsIdentity ? baseTransform : (renderEff * baseTransform);
    }

    public Rect TransformRectToScreen(Rect localRect)
    {
        var m = GetTransformToRoot();
        if (m.IsIdentity)
        {
            return localRect;
        }

        var p1 = Vector2.Transform(new Vector2(localRect.Left, localRect.Top), m);
        var p2 = Vector2.Transform(new Vector2(localRect.Right, localRect.Top), m);
        var p3 = Vector2.Transform(new Vector2(localRect.Right, localRect.Bottom), m);
        var p4 = Vector2.Transform(new Vector2(localRect.Left, localRect.Bottom), m);

        float minX = MathF.Min(MathF.Min(p1.X, p2.X), MathF.Min(p3.X, p4.X));
        float maxX = MathF.Max(MathF.Max(p1.X, p2.X), MathF.Max(p3.X, p4.X));
        float minY = MathF.Min(MathF.Min(p1.Y, p2.Y), MathF.Min(p3.Y, p4.Y));
        float maxY = MathF.Max(MathF.Max(p1.Y, p2.Y), MathF.Max(p3.Y, p4.Y));

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    public Rect GetScreenBounds() => TransformRectToScreen(new Rect(0, 0, Bounds.Width, Bounds.Height));

    public void DispatchBubblePointerEvent<T>(T e, Action<UIElement, T> action) where T : PointerEventArgs
    {
        UIElement? current = this;
        while (current != null)
        {
            Point localPos = current.PointToClient(e.ScreenPosition);
            var localE = (T)e.WithPosition(localPos);
            localE.Source ??= this;
            localE.OriginalSource ??= this;

            action(current, localE);

            if (localE.Handled)
            {
                e.Handled = true;
                break;
            }

            if (current.IsOverlayElement)
            {
                break;
            }

            current = current.Parent as UIElement;
        }
    }

    public void DispatchBubbleKeyEvent<T>(T e, Action<UIElement, T> action) where T : RoutedEventArgs
    {
        e.Source ??= this;
        e.OriginalSource ??= this;

        UIElement? current = this;
        while (current != null)
        {
            action(current, e);

            if (e.Handled)
            {
                break;
            }

            if (current == FocusManager.CurrentModal || current.IsOverlayElement)
            {
                break;
            }

            current = current.Parent as UIElement;
        }
    }

    private Size _previousAvailableSize = Size.Zero;
    private Rect _previousFinalRect = Rect.Zero;
    private Size _untransformedDesiredSize = Size.Zero;
    public Size UntransformedDesiredSize => _untransformedDesiredSize;

    public void InvalidateMeasure()
    {
        if (IsMeasureValid)
        {
            IsMeasureValid = false;
            IsArrangeValid = false;
            base.InvalidateLayout();

            UIElement? parent = Parent as UIElement;
            while (parent != null)
            {
                if (!parent.IsMeasureValid && !parent.IsArrangeValid)
                    break;

                parent.IsMeasureValid = false;
                parent.IsArrangeValid = false;
                parent = parent.Parent as UIElement;
            }
        }
    }

    public void InvalidateArrange()
    {
        if (IsArrangeValid)
        {
            IsArrangeValid = false;
            base.InvalidateLayout();

            UIElement? parent = Parent as UIElement;
            while (parent != null)
            {
                if (!parent.IsArrangeValid)
                    break;

                parent.IsArrangeValid = false;
                parent = parent.Parent as UIElement;
            }
        }
    }

    public void Measure(Size availableSize)
    {
        if (Visibility == Visibility.Collapsed)
        {
            DesiredSize = Size.Zero;
            _untransformedDesiredSize = Size.Zero;
            IsMeasureValid = true;
            return;
        }

        if (IsMeasureValid && _previousAvailableSize == availableSize)
        {
            return;
        }

        _previousAvailableSize = availableSize;

        // Apply Margin
        var margin = Margin;
        var innerAvailable = availableSize.Deflate(margin);

        Size childAvailable = innerAvailable;
        var transform = Transform;
        if (!transform.IsIdentity)
        {
            // If finite constraints, transform constraint by inverse of linear part of transform
            if (!float.IsInfinity(childAvailable.Width) || !float.IsInfinity(childAvailable.Height))
            {
                var linear = new Matrix3x2(transform.M11, transform.M12, transform.M21, transform.M22, 0, 0);
                if (Matrix3x2.Invert(linear, out var invLinear))
                {
                    float w = float.IsInfinity(childAvailable.Width)
                        ? float.PositiveInfinity
                        : MathF.Abs(invLinear.M11) * childAvailable.Width + MathF.Abs(invLinear.M21) * childAvailable.Height;
                    float h = float.IsInfinity(childAvailable.Height)
                        ? float.PositiveInfinity
                        : MathF.Abs(invLinear.M12) * childAvailable.Width + MathF.Abs(invLinear.M22) * childAvailable.Height;
                    childAvailable = new Size(w, h);
                }
            }
        }

        // Apply explicit Width / Height / Min / Max constraints
        float constraintWidth = !float.IsNaN(Width) ? Width : childAvailable.Width;
        float constraintHeight = !float.IsNaN(Height) ? Height : childAvailable.Height;

        constraintWidth = Math.Clamp(constraintWidth, MinWidth, MaxWidth);
        constraintHeight = Math.Clamp(constraintHeight, MinHeight, MaxHeight);

        Size measured = MeasureOverride(new Size(constraintWidth, constraintHeight));

        // Clamping to min/max/explicit
        float finalWidth = !float.IsNaN(Width) ? Width : measured.Width;
        float finalHeight = !float.IsNaN(Height) ? Height : measured.Height;

        finalWidth = Math.Clamp(finalWidth, MinWidth, MaxWidth);
        finalHeight = Math.Clamp(finalHeight, MinHeight, MaxHeight);

        _untransformedDesiredSize = new Size(finalWidth, finalHeight);

        if (Parent == null || transform.IsIdentity)
        {
            DesiredSize = new Size(finalWidth, finalHeight).Inflate(margin.Horizontal, margin.Vertical);
        }
        else
        {
            var transBounds = ComputeTransformedBounds(finalWidth, finalHeight);
            DesiredSize = new Size(transBounds.Width, transBounds.Height).Inflate(margin.Horizontal, margin.Vertical);
        }

        IsMeasureValid = true;
    }

    public void Arrange(Rect finalRect)
    {
        if (Visibility == Visibility.Collapsed)
        {
            Bounds = Rect.Zero;
            IsArrangeValid = true;
            return;
        }

        if (IsArrangeValid && _previousFinalRect == finalRect)
        {
            return;
        }

        _previousFinalRect = finalRect;

        var transform = Transform;
        if (Parent == null && !transform.IsIdentity)
        {
            Bounds = finalRect;
            ArrangeOverride(finalRect.Size);
            IsArrangeValid = true;
            return;
        }

        var margin = Margin;
        Rect innerRect = finalRect.Deflate(margin);

        if (transform.IsIdentity)
        {
            // Apply explicit / desired constraints
            float childWidth = !float.IsNaN(Width) ? Width : DesiredSize.Width - margin.Horizontal;
            float childHeight = !float.IsNaN(Height) ? Height : DesiredSize.Height - margin.Vertical;

            childWidth = Math.Clamp(childWidth, MinWidth, MaxWidth);
            childHeight = Math.Clamp(childHeight, MinHeight, MaxHeight);

            float maxArrangeWidth = Math.Clamp(innerRect.Width, MinWidth, MaxWidth);
            float maxArrangeHeight = Math.Clamp(innerRect.Height, MinHeight, MaxHeight);

            Size arrangeSize = new Size(maxArrangeWidth, maxArrangeHeight);

            if (HorizontalAlignment != HorizontalAlignment.Stretch)
            {
                arrangeSize = new Size(Math.Min(arrangeSize.Width, childWidth), arrangeSize.Height);
            }

            if (VerticalAlignment != VerticalAlignment.Stretch)
            {
                arrangeSize = new Size(arrangeSize.Width, Math.Min(arrangeSize.Height, childHeight));
            }

            Size arrangedContentSize = ArrangeOverride(arrangeSize);

            // Calculate final positioned rect within innerRect based on alignment and constraints
            float width = HorizontalAlignment == HorizontalAlignment.Stretch
                ? Math.Clamp(innerRect.Width, MinWidth, MaxWidth)
                : Math.Clamp(arrangedContentSize.Width, MinWidth, MaxWidth);

            float height = VerticalAlignment == VerticalAlignment.Stretch
                ? Math.Clamp(innerRect.Height, MinHeight, MaxHeight)
                : Math.Clamp(arrangedContentSize.Height, MinHeight, MaxHeight);

            float x = innerRect.X;
            float y = innerRect.Y;

            switch (HorizontalAlignment)
            {
                case HorizontalAlignment.Center:
                    x += (innerRect.Width - width) * 0.5f;
                    break;
                case HorizontalAlignment.Right:
                    x += innerRect.Width - width;
                    break;
            }

            switch (VerticalAlignment)
            {
                case VerticalAlignment.Center:
                    y += (innerRect.Height - height) * 0.5f;
                    break;
                case VerticalAlignment.Bottom:
                    y += innerRect.Height - height;
                    break;
            }

            Bounds = new Rect(x, y, width, height);
            IsArrangeValid = true;
            return;
        }

        // Transformed element path:
        float unWidth = !float.IsNaN(Width) ? Width : _untransformedDesiredSize.Width;
        float unHeight = !float.IsNaN(Height) ? Height : _untransformedDesiredSize.Height;

        unWidth = Math.Clamp(unWidth, MinWidth, MaxWidth);
        unHeight = Math.Clamp(unHeight, MinHeight, MaxHeight);

        Size childArrangeSize = new Size(unWidth, unHeight);
        Size contentSize = ArrangeOverride(childArrangeSize);

        float finalUnWidth = !float.IsNaN(Width) ? Width : contentSize.Width;
        float finalUnHeight = !float.IsNaN(Height) ? Height : contentSize.Height;

        finalUnWidth = Math.Clamp(finalUnWidth, MinWidth, MaxWidth);
        finalUnHeight = Math.Clamp(finalUnHeight, MinHeight, MaxHeight);

        var transBounds = ComputeTransformedBounds(finalUnWidth, finalUnHeight);

        float bboxX = innerRect.X;
        switch (HorizontalAlignment)
        {
            case HorizontalAlignment.Center:
                bboxX += (innerRect.Width - transBounds.Width) * 0.5f;
                break;
            case HorizontalAlignment.Right:
                bboxX += innerRect.Width - transBounds.Width;
                break;
            case HorizontalAlignment.Stretch:
                if (innerRect.Width > transBounds.Width)
                {
                    bboxX += (innerRect.Width - transBounds.Width) * 0.5f;
                }
                break;
        }

        float bboxY = innerRect.Y;
        switch (VerticalAlignment)
        {
            case VerticalAlignment.Center:
                bboxY += (innerRect.Height - transBounds.Height) * 0.5f;
                break;
            case VerticalAlignment.Bottom:
                bboxY += innerRect.Height - transBounds.Height;
                break;
            case VerticalAlignment.Stretch:
                if (innerRect.Height > transBounds.Height)
                {
                    bboxY += (innerRect.Height - transBounds.Height) * 0.5f;
                }
                break;
        }

        float xTrans = bboxX - transBounds.X;
        float yTrans = bboxY - transBounds.Y;

        Bounds = new Rect(xTrans, yTrans, finalUnWidth, finalUnHeight);
        IsArrangeValid = true;
    }

    protected virtual Size MeasureOverride(Size availableSize)
    {
        Size size = Size.Zero;
        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is UIElement child)
            {
                child.Measure(availableSize);
                size = new Size(Math.Max(size.Width, child.DesiredSize.Width), Math.Max(size.Height, child.DesiredSize.Height));
            }
        }
        return size;
    }

    protected virtual Size ArrangeOverride(Size finalSize)
    {
        for (int i = 0; i < Children.Count; i++)
        {
            if (Children[i] is UIElement child)
            {
                child.Arrange(new Rect(Point.Zero, finalSize));
            }
        }
        return finalSize;
    }

    public virtual UIElement? HitTest(Point point)
    {
        if (Visibility != Visibility.Visible || !IsHitTestVisible)
        {
            return null;
        }

        var eff = GetEffectiveTransform();
        var renderEff = RenderTransform.IsIdentity ? Matrix3x2.Identity : GetEffectiveRenderTransform(Bounds.Width, Bounds.Height);
        Point localPoint;

        if (eff.IsIdentity && renderEff.IsIdentity)
        {
            // Fast-path: untransformed element
            if (!Bounds.Contains(point))
            {
                return null;
            }
            localPoint = point.Offset(-Bounds.X, -Bounds.Y);
        }
        else
        {
            // Transformed element: invert local transform (which includes Bounds translation and local transform)
            var localTransform = GetLocalTransform();
            if (!Matrix3x2.Invert(localTransform, out var inv))
            {
                return null;
            }

            var v = Vector2.Transform(new Vector2(point.X, point.Y), inv);
            localPoint = new Point(v.X, v.Y);

            var localRect = new Rect(0, 0, Bounds.Width, Bounds.Height);
            if (!localRect.Contains(localPoint))
            {
                return null;
            }
        }

        // Check children in reverse order (topmost first)
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (Children[i] is UIElement child)
            {
                if (child.IsOverlayElement) continue;
                var hit = child.HitTest(localPoint);
                if (hit != null)
                {
                    return hit;
                }
            }
        }

        return this;
    }

    #region Input Event Handlers (Virtual Hooks)

    public event EventHandler<PointerEventArgs>? PointerEntered;
    public event EventHandler<PointerEventArgs>? PointerExited;
    public event EventHandler<PointerEventArgs>? PointerPressed;
    public event EventHandler<PointerEventArgs>? PointerReleased;
    public event EventHandler<PointerEventArgs>? PointerMoved;
    public event EventHandler<PointerWheelEventArgs>? PointerWheel;
    public event EventHandler<KeyEventArgs>? KeyDown;
    public event EventHandler<KeyEventArgs>? KeyUp;
    public event EventHandler<TextInputEventArgs>? TextInput;

    public virtual void OnPointerEntered(PointerEventArgs e)
    {
        IsHovered = true;
        PointerEntered?.Invoke(this, e);
        InvalidateVisual();
    }

    public virtual void OnPointerExited(PointerEventArgs e)
    {
        IsHovered = false;
        IsPressed = false;
        PointerExited?.Invoke(this, e);
        InvalidateVisual();
    }

    public virtual void OnPointerPressed(PointerEventArgs e)
    {
        IsPressed = true;
        PointerPressed?.Invoke(this, e);
        InvalidateVisual();
    }

    public virtual void OnPointerReleased(PointerEventArgs e)
    {
        IsPressed = false;
        PointerReleased?.Invoke(this, e);
        InvalidateVisual();
    }

    public virtual void OnPointerMoved(PointerEventArgs e)
    {
        PointerMoved?.Invoke(this, e);
    }

    public virtual void OnPointerWheel(PointerWheelEventArgs e)
    {
        PointerWheel?.Invoke(this, e);
    }

    public event EventHandler? GotFocus;
    public event EventHandler? LostFocus;

    public virtual void OnGotFocus()
    {
        IsFocused = true;
        GotFocus?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    public virtual void OnLostFocus()
    {
        IsFocused = false;
        LostFocus?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    public virtual void OnKeyDown(KeyEventArgs e)
    {
        KeyDown?.Invoke(this, e);
    }

    public virtual void OnKeyUp(KeyEventArgs e)
    {
        KeyUp?.Invoke(this, e);
    }

    public virtual void OnTextInput(TextInputEventArgs e)
    {
        TextInput?.Invoke(this, e);
    }

    #endregion
}
