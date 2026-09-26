using System;
using System.Collections.Generic;
using System.Numerics;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Styling;

namespace Atelier.Core.Tree;

/// <summary>
/// Base class for elements that take part in layout, styling and input: adds size constraints, alignment, visibility,
/// the two-pass measure/arrange layout, hit testing, focus, pointer capture and input event hooks.
/// </summary>
public abstract class UIElement : VisualNode
{
    #region Bindable Properties

    /// <summary>Identifies the <see cref="Width"/> bindable property.</summary>
    public static readonly BindableProperty<float> WidthProperty =
        BindableProperty.Register<UIElement, float>(nameof(Width), float.NaN, options: PropertyOptions.AffectsMeasure);

    /// <summary>Identifies the <see cref="Height"/> bindable property.</summary>
    public static readonly BindableProperty<float> HeightProperty =
        BindableProperty.Register<UIElement, float>(nameof(Height), float.NaN, options: PropertyOptions.AffectsMeasure);

    /// <summary>Identifies the <see cref="MinWidth"/> bindable property.</summary>
    public static readonly BindableProperty<float> MinWidthProperty =
        BindableProperty.Register<UIElement, float>(nameof(MinWidth), 0f, options: PropertyOptions.AffectsMeasure);

    /// <summary>Identifies the <see cref="MaxWidth"/> bindable property.</summary>
    public static readonly BindableProperty<float> MaxWidthProperty =
        BindableProperty.Register<UIElement, float>(nameof(MaxWidth), float.PositiveInfinity, options: PropertyOptions.AffectsMeasure);

    /// <summary>Identifies the <see cref="MinHeight"/> bindable property.</summary>
    public static readonly BindableProperty<float> MinHeightProperty =
        BindableProperty.Register<UIElement, float>(nameof(MinHeight), 0f, options: PropertyOptions.AffectsMeasure);

    /// <summary>Identifies the <see cref="MaxHeight"/> bindable property.</summary>
    public static readonly BindableProperty<float> MaxHeightProperty =
        BindableProperty.Register<UIElement, float>(nameof(MaxHeight), float.PositiveInfinity, options: PropertyOptions.AffectsMeasure);

    /// <summary>Identifies the <see cref="Margin"/> bindable property.</summary>
    public static readonly BindableProperty<Thickness> MarginProperty =
        BindableProperty.Register<UIElement, Thickness>(nameof(Margin), Thickness.Zero, options: PropertyOptions.AffectsMeasure);

    /// <summary>Identifies the <see cref="HorizontalAlignment"/> bindable property.</summary>
    public static readonly BindableProperty<HorizontalAlignment> HorizontalAlignmentProperty =
        BindableProperty.Register<UIElement, HorizontalAlignment>(nameof(HorizontalAlignment), HorizontalAlignment.Stretch, options: PropertyOptions.AffectsArrange);

    /// <summary>Identifies the <see cref="VerticalAlignment"/> bindable property.</summary>
    public static readonly BindableProperty<VerticalAlignment> VerticalAlignmentProperty =
        BindableProperty.Register<UIElement, VerticalAlignment>(nameof(VerticalAlignment), VerticalAlignment.Stretch, options: PropertyOptions.AffectsArrange);

    /// <summary>Identifies the <see cref="Visibility"/> bindable property.</summary>
    public static readonly BindableProperty<Visibility> VisibilityProperty =
        BindableProperty.Register<UIElement, Visibility>(nameof(Visibility), Visibility.Visible, options: PropertyOptions.AffectsMeasure);

    /// <summary>Identifies the <see cref="Opacity"/> bindable property.</summary>
    public static readonly BindableProperty<float> OpacityProperty =
        BindableProperty.Register<UIElement, float>(nameof(Opacity), 1.0f, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="IsEnabled"/> bindable property.</summary>
    public static readonly BindableProperty<bool> IsEnabledProperty =
        BindableProperty.Register<UIElement, bool>(nameof(IsEnabled), true, options: PropertyOptions.AffectsRender, inherits: true);

    /// <summary>Identifies the <see cref="ClipToBounds"/> bindable property.</summary>
    public static readonly BindableProperty<bool> ClipToBoundsProperty =
        BindableProperty.Register<UIElement, bool>(nameof(ClipToBounds), false, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="UseLayoutRounding"/> bindable property.</summary>
    public static readonly BindableProperty<bool> UseLayoutRoundingProperty =
        BindableProperty.Register<UIElement, bool>(nameof(UseLayoutRounding), false, options: PropertyOptions.AffectsMeasure, inherits: true);

    /// <summary>
    /// Gets or sets whether layout snaps this element (and, by inheritance, its descendants) to whole pixels, so edges and
    /// thin lines render crisply instead of being blurred across two pixels. The default is <c>false</c>; windows enable
    /// it for their content unless it was set explicitly.
    /// </summary>
    /// <remarks>
    /// Desired sizes are rounded up (so measured text is never clipped) and arranged edges are rounded to the nearest pixel,
    /// which keeps neighbouring elements from overlapping or leaving gaps. Elements with a <see cref="VisualNode.Transform"/>
    /// are not rounded. One layout unit is assumed to be one device pixel.
    /// </remarks>
    public bool UseLayoutRounding { get => GetValue(UseLayoutRoundingProperty); set => SetValue(UseLayoutRoundingProperty, value); }

    /// <summary>Identifies the <see cref="StyleKey"/> bindable property.</summary>
    public static readonly BindableProperty<string?> StyleKeyProperty =
        BindableProperty.Register<UIElement, string?>(
            nameof(StyleKey),
            null,
            (s, o, n) => ((UIElement)s).OnStyleKeyChanged(o, n));

    #endregion

    #region Property Accessors

    /// <summary>
    /// Gets or sets the explicit width, excluding <see cref="Margin"/>. The default, <see cref="float.NaN"/>, sizes the
    /// element to its content (or stretches it, per <see cref="HorizontalAlignment"/>). Clamped by <see cref="MinWidth"/>
    /// and <see cref="MaxWidth"/>. Changing it invalidates measure.
    /// </summary>
    public float Width { get => GetValue(WidthProperty); set => SetValue(WidthProperty, value); }
    /// <summary>
    /// Gets or sets the explicit height, excluding <see cref="Margin"/>. The default, <see cref="float.NaN"/>, sizes the
    /// element to its content (or stretches it, per <see cref="VerticalAlignment"/>). Clamped by <see cref="MinHeight"/>
    /// and <see cref="MaxHeight"/>. Changing it invalidates measure.
    /// </summary>
    public float Height { get => GetValue(HeightProperty); set => SetValue(HeightProperty, value); }
    /// <summary>
    /// Gets or sets the minimum width. The default is 0. When it exceeds <see cref="MaxWidth"/>, the minimum wins.
    /// Changing it invalidates measure.
    /// </summary>
    public float MinWidth { get => GetValue(MinWidthProperty); set => SetValue(MinWidthProperty, value); }
    /// <summary>
    /// Gets or sets the maximum width. The default is <see cref="float.PositiveInfinity"/>. Changing it invalidates measure.
    /// </summary>
    public float MaxWidth { get => GetValue(MaxWidthProperty); set => SetValue(MaxWidthProperty, value); }
    /// <summary>
    /// Gets or sets the minimum height. The default is 0. When it exceeds <see cref="MaxHeight"/>, the minimum wins.
    /// Changing it invalidates measure.
    /// </summary>
    public float MinHeight { get => GetValue(MinHeightProperty); set => SetValue(MinHeightProperty, value); }
    /// <summary>
    /// Gets or sets the maximum height. The default is <see cref="float.PositiveInfinity"/>. Changing it invalidates measure.
    /// </summary>
    public float MaxHeight { get => GetValue(MaxHeightProperty); set => SetValue(MaxHeightProperty, value); }
    /// <summary>
    /// Gets or sets the outer space around the element. It is included in <see cref="DesiredSize"/> but not in
    /// <see cref="Bounds"/>. The default is <see cref="Thickness.Zero"/>. Changing it invalidates measure.
    /// </summary>
    public Thickness Margin { get => GetValue(MarginProperty); set => SetValue(MarginProperty, value); }
    /// <summary>
    /// Gets or sets how the element is positioned horizontally within the slot its parent arranges it in. The default,
    /// <see cref="HorizontalAlignment.Stretch"/>, fills the slot width. Changing it invalidates arrange.
    /// </summary>
    public HorizontalAlignment HorizontalAlignment { get => GetValue(HorizontalAlignmentProperty); set => SetValue(HorizontalAlignmentProperty, value); }
    /// <summary>
    /// Gets or sets how the element is positioned vertically within the slot its parent arranges it in. The default,
    /// <see cref="VerticalAlignment.Stretch"/>, fills the slot height. Changing it invalidates arrange.
    /// </summary>
    public VerticalAlignment VerticalAlignment { get => GetValue(VerticalAlignmentProperty); set => SetValue(VerticalAlignmentProperty, value); }
    /// <summary>
    /// Gets or sets whether the element is shown. The default is <see cref="Visibility.Visible"/>. Changing it invalidates measure.
    /// </summary>
    /// <remarks>
    /// A <see cref="Visibility.Collapsed"/> element measures to zero size and arranges to <see cref="Rect.Zero"/>.
    /// A <see cref="Visibility.Hidden"/> element is still measured and arranged, so it keeps its layout space, but it is
    /// not rendered and <see cref="HitTest"/> ignores it.
    /// </remarks>
    public Visibility Visibility { get => GetValue(VisibilityProperty); set => SetValue(VisibilityProperty, value); }
    /// <summary>
    /// Gets or sets the opacity of the element and its children, from 0 (transparent) to 1 (opaque). The default is 1.
    /// Changing it invalidates rendering.
    /// </summary>
    public float Opacity { get => GetValue(OpacityProperty); set => SetValue(OpacityProperty, value); }
    /// <summary>
    /// Gets or sets whether the element accepts user interaction. The default is <c>true</c>. Changing it invalidates rendering.
    /// </summary>
    /// <remarks>
    /// This property is inherited: unless set locally, a descendant takes the value of its nearest ancestor. Disabled
    /// elements are skipped by keyboard focus navigation (<see cref="FocusManager.FocusNext"/>); input is still delivered
    /// to them, and controls check this property themselves before reacting.
    /// <para>
    /// An element can additionally disable itself through <see cref="IsEnabledCore"/> (for example a button whose command
    /// cannot execute); the effective value is then <c>false</c> regardless of the value set here, which comes back once
    /// <see cref="IsEnabledCore"/> returns <c>true</c> again.
    /// </para>
    /// </remarks>
    public bool IsEnabled { get => GetValue(IsEnabledProperty); set => SetValue(IsEnabledProperty, value); }

    /// <summary>
    /// Gets a value indicating whether the element itself allows being enabled. When <c>false</c>, <see cref="IsEnabled"/>
    /// is forced to <c>false</c> for this element and its descendants. The base implementation returns <c>true</c>.
    /// </summary>
    /// <remarks>Call <see cref="UpdateIsEnabledCore"/> whenever the value this returns may have changed.</remarks>
    protected virtual bool IsEnabledCore => true;

    /// <summary>
    /// Re-evaluates <see cref="IsEnabledCore"/> and forces or releases <see cref="IsEnabled"/> accordingly.
    /// </summary>
    protected void UpdateIsEnabledCore()
    {
        if (IsEnabledCore)
        {
            ClearCoercedValue(IsEnabledProperty);
        }
        else
        {
            SetCoercedValue(IsEnabledProperty, false);
        }
    }
    /// <summary>
    /// Gets or sets whether the rendering of the element's children is clipped to its bounds (the element's own
    /// background and shadow are not clipped). The default is <c>false</c>. Changing it invalidates rendering.
    /// </summary>
    public bool ClipToBounds { get => GetValue(ClipToBoundsProperty); set => SetValue(ClipToBoundsProperty, value); }
    /// <summary>
    /// Gets or sets the key of the style to apply, looked up in this element's and its ancestors' <see cref="Styles"/>
    /// and then in <see cref="StyleManager.GlobalStyles"/>. The default is <c>null</c>, which selects the implicit style
    /// for the element's type. Ignored when <see cref="Style"/> is set. Changing it re-applies styles.
    /// </summary>
    public string? StyleKey { get => GetValue(StyleKeyProperty); set => SetValue(StyleKeyProperty, value); }

    private Style? _style;
    /// <summary>
    /// Gets or sets an explicit style for this element, which takes precedence over <see cref="StyleKey"/> and implicit
    /// styles. Setting a different value re-applies styles to this element (not its descendants).
    /// </summary>
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

    private StyleCollection? _styles;

    /// <summary>
    /// Gets the styles defined on this element, which apply to it and its descendants.
    /// </summary>
    /// <remarks>Created on first access: most elements never define local styles.</remarks>
    public StyleCollection Styles
    {
        get
        {
            if (_styles == null)
            {
                _styles = new StyleCollection();
                _styles.StylesChanged += ApplyStylesToTree;
            }
            return _styles;
        }
    }

    #endregion

    private void OnStyleKeyChanged(string? oldValue, string? newValue)
    {
        ApplyStyles();
    }

    /// <summary>
    /// Resolves this element's style (see <see cref="ResolveStyle"/>) and applies its setters, including those of its
    /// <see cref="Styling.Style.BasedOn"/> chain. Only properties whose effective value changes raise notifications.
    /// </summary>
    public void ApplyStyles()
    {
        SetStyleValues(ResolveStyle());
    }

    /// <summary>
    /// Applies styles to this element and all descendant elements.
    /// </summary>
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

    /// <summary>
    /// Finds the style that applies to this element: the explicit <see cref="Style"/> if set; otherwise the nearest style
    /// matching <see cref="StyleKey"/> (or, without a key, the nearest implicit style for this element's type) in this
    /// element's or an ancestor's <see cref="Styles"/>, falling back to <see cref="StyleManager.GlobalStyles"/>.
    /// </summary>
    /// <returns>The resolved style, or <c>null</c> if none applies.</returns>
    protected virtual Style? ResolveStyle()
    {
        if (_style != null) return _style;

        string? key = StyleKey;
        if (!string.IsNullOrEmpty(key))
        {
            UIElement? current = this;
            while (current != null)
            {
                // Read the field, not the property, so walking ancestors doesn't create their style collections.
                var styles = current._styles;
                for (int i = 0; styles != null && i < styles.Count; i++)
                {
                    var s = styles[i];
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
            var styles = curr._styles;
            for (int i = 0; styles != null && i < styles.Count; i++)
            {
                var s = styles[i];
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

    /// <inheritdoc/>
    /// <remarks>
    /// Applies the property's <see cref="PropertyOptions"/>: <see cref="PropertyOptions.AffectsMeasure"/> invalidates
    /// measure (which also covers arrange), otherwise <see cref="PropertyOptions.AffectsArrange"/> invalidates arrange;
    /// <see cref="PropertyOptions.AffectsRender"/> additionally invalidates rendering.
    /// </remarks>
    protected override void OnPropertyValueChanged<T>(BindableProperty<T> property, T oldValue, T newValue)
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

        if (newParent == null)
        {
            // Removed from the tree: static input state must not keep the detached subtree alive or keep routing to it.
            FocusManager.OnSubtreeDetached(this);
            if (CapturedElement != null && (CapturedElement == this || CapturedElement.IsDescendantOf(this)))
            {
                ReleaseCurrentPointerCapture();
            }
        }

        // Style resolution walks up the tree, so the whole moved subtree may resolve different styles now.
        ApplyStylesToTree();
    }

    /// <inheritdoc/>
    /// <remarks>This implementation also invalidates measure.</remarks>
    protected override void OnChildAdded(VisualNode child)
    {
        base.OnChildAdded(child);
        InvalidateMeasure();
    }

    /// <inheritdoc/>
    /// <remarks>This implementation also invalidates measure.</remarks>
    protected override void OnChildRemoved(VisualNode child)
    {
        base.OnChildRemoved(child);
        InvalidateMeasure();
    }

    /// <summary>
    /// Gets the size computed by the last <see cref="Measure"/>, including <see cref="Margin"/> and, for a non-root
    /// element, the bounding box of its <see cref="VisualNode.Transform"/>.
    /// </summary>
    public Size DesiredSize { get; private set; } = Size.Zero;
    /// <summary>
    /// Gets the untransformed layout rectangle assigned by the last <see cref="Arrange"/>, in the parent's coordinates
    /// and excluding <see cref="Margin"/>. <see cref="Rect.Zero"/> when collapsed.
    /// </summary>
    public Rect Bounds { get; private set; } = Rect.Zero;
    /// <summary>Gets the arranged size of the element, i.e. the size of <see cref="Bounds"/>.</summary>
    public Size RenderSize => Bounds.Size;

    /// <summary>
    /// Gets a value indicating whether <see cref="DesiredSize"/> is up to date. Cleared by <see cref="InvalidateMeasure"/>.
    /// </summary>
    public bool IsMeasureValid { get; private set; }
    /// <summary>
    /// Gets a value indicating whether <see cref="Bounds"/> is up to date. Cleared by <see cref="InvalidateArrange"/> and
    /// <see cref="InvalidateMeasure"/>.
    /// </summary>
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

    /// <summary>
    /// Gets a value indicating whether the pointer is over the element. Set by <see cref="OnPointerEntered"/> and
    /// cleared by <see cref="OnPointerExited"/>.
    /// </summary>
    public bool IsHovered { get => GetValue(IsHoveredProperty); internal set => SetValue(IsHoveredPropertyKey, value); }
    /// <summary>
    /// Gets a value indicating whether a pointer button is pressed on the element. Set by <see cref="OnPointerPressed"/>
    /// and cleared by <see cref="OnPointerReleased"/> or <see cref="OnPointerExited"/>.
    /// </summary>
    public bool IsPressed { get => GetValue(IsPressedProperty); internal set => SetValue(IsPressedPropertyKey, value); }
    /// <summary>
    /// Gets a value indicating whether the element has keyboard focus. Set by <see cref="OnGotFocus"/> and cleared by
    /// <see cref="OnLostFocus"/>.
    /// </summary>
    public bool IsFocused { get => GetValue(IsFocusedProperty); internal set => SetValue(IsFocusedPropertyKey, value); }
    /// <summary>Identifies the <see cref="IsFocusable"/> bindable property.</summary>
    public static readonly BindableProperty<bool> IsFocusableProperty =
        BindableProperty.Register<UIElement, bool>(nameof(IsFocusable), false);

    /// <summary>Identifies the <see cref="IsHitTestVisible"/> bindable property.</summary>
    public static readonly BindableProperty<bool> IsHitTestVisibleProperty =
        BindableProperty.Register<UIElement, bool>(nameof(IsHitTestVisible), true);

    /// <summary>
    /// Gets or sets whether the element can receive keyboard focus. The default is <c>false</c>; focusing a
    /// non-focusable element moves focus to its nearest focusable ancestor.
    /// </summary>
    public bool IsFocusable { get => GetValue(IsFocusableProperty); set => SetValue(IsFocusableProperty, value); }

    /// <summary>
    /// Gets or sets whether <see cref="HitTest"/> can return this element or any of its descendants. The default is <c>true</c>.
    /// </summary>
    public bool IsHitTestVisible { get => GetValue(IsHitTestVisibleProperty); set => SetValue(IsHitTestVisibleProperty, value); }
    /// <summary>
    /// Gets a value indicating whether this element is an overlay (such as a popup) that is positioned in window
    /// coordinates at <see cref="OverlayOrigin"/> rather than within its parent.
    /// </summary>
    /// <remarks>
    /// Overlay elements are skipped by their parent's <see cref="HitTest"/>, end the coordinate walk in
    /// <see cref="VisualNode.GetTransformToAncestor"/>, and stop pointer and key events from bubbling past them.
    /// </remarks>
    public bool IsOverlayElement { get; protected set; } = false;
    /// <summary>
    /// Gets the window-coordinate position of an overlay element; used instead of <see cref="Bounds"/> for the
    /// translation in <see cref="GetLocalTransform"/> when <see cref="IsOverlayElement"/> is <c>true</c>.
    /// The base implementation returns <see cref="Point.Zero"/>.
    /// </summary>
    public virtual Point OverlayOrigin => Point.Zero;

    /// <summary>
    /// Gives keyboard focus to this element, or to its nearest focusable ancestor if <see cref="IsFocusable"/> is
    /// <c>false</c>, via <see cref="FocusManager.SetFocus"/>. Ignored if a modal scope in the same tree excludes it.
    /// </summary>
    public void Focus()
    {
        FocusManager.SetFocus(this);
    }

    /// <summary>Clears keyboard focus if this element currently has it; otherwise does nothing.</summary>
    public void Unfocus()
    {
        if (FocusManager.CurrentFocused == this)
        {
            FocusManager.SetFocus(null);
        }
    }

    /// <summary>
    /// Occurs when pointer capture moves to another element or is released (the argument is then <c>null</c>).
    /// </summary>
    /// <remarks>This is a static event: subscribers stay alive until they unsubscribe.</remarks>
    public static event Action<UIElement?>? PointerCaptureChanged;

    /// <summary>
    /// Gets the element that currently receives all pointer input, or <c>null</c>. Capture is released automatically
    /// when the capturing element is removed from the tree.
    /// </summary>
    public static UIElement? CapturedElement { get; private set; }

    /// <summary>Gets a value indicating whether this element currently has pointer capture.</summary>
    public bool IsPointerCaptured => CapturedElement == this;

    /// <summary>
    /// Routes all pointer input to this element until <see cref="ReleasePointerCapture"/> is called (typically for drags).
    /// </summary>
    /// <returns>Always <c>true</c>.</returns>
    public bool CapturePointer()
    {
        if (CapturedElement != this)
        {
            CapturedElement = this;
            PointerCaptureChanged?.Invoke(this);
        }
        return true;
    }

    /// <summary>
    /// Releases pointer capture if this element holds it, raising <see cref="PointerCaptureChanged"/>; otherwise does nothing.
    /// </summary>
    public void ReleasePointerCapture()
    {
        if (CapturedElement == this)
        {
            CapturedElement = null;
            PointerCaptureChanged?.Invoke(null);
        }
    }

    /// <summary>
    /// Releases pointer capture held by any element, raising <see cref="PointerCaptureChanged"/> if there was one.
    /// </summary>
    public static void ReleaseCurrentPointerCapture()
    {
        if (CapturedElement != null)
        {
            CapturedElement = null;
            PointerCaptureChanged?.Invoke(null);
        }
    }

    /// <inheritdoc/>
    /// <remarks>This implementation also invalidates measure.</remarks>
    protected override void OnTransformChanged(Matrix3x2 oldValue, Matrix3x2 newValue)
    {
        // Not calling base: InvalidateMeasure already notifies layout, so base's InvalidateLayout would walk the tree twice.
        InvalidateVisual();
        InvalidateMeasure();
    }

    /// <inheritdoc/>
    /// <remarks>This implementation also invalidates measure.</remarks>
    protected override void OnTransformOriginChanged(Point oldValue, Point newValue)
    {
        // Not calling base: see OnTransformChanged.
        InvalidateVisual();
        InvalidateMeasure();
    }

    /// <summary>
    /// Gets <see cref="VisualNode.Transform"/> applied about <see cref="VisualNode.TransformOrigin"/>, resolved against
    /// the current <see cref="Bounds"/> size.
    /// </summary>
    /// <returns>The effective layout transform.</returns>
    public override Matrix3x2 GetEffectiveTransform()
    {
        return GetEffectiveTransform(Bounds.Width, Bounds.Height);
    }

    /// <summary>
    /// Computes the axis-aligned bounding box of a <paramref name="width"/> by <paramref name="height"/> rectangle at the
    /// origin after applying the effective layout transform (see <see cref="VisualNode.GetEffectiveTransform(float, float)"/>).
    /// </summary>
    /// <param name="width">The untransformed width.</param>
    /// <param name="height">The untransformed height.</param>
    /// <returns>The transformed bounding box; its position can be negative.</returns>
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

    /// <summary>
    /// Gets the transform that maps this element's local coordinates into its parent's coordinates: the effective render
    /// transform, then the effective layout transform, then the translation to <see cref="Bounds"/> (or to
    /// <see cref="OverlayOrigin"/> for an overlay element).
    /// </summary>
    /// <returns>The local-to-parent transform.</returns>
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

    /// <summary>
    /// Transforms a rectangle from this element's local coordinates to root (window) coordinates.
    /// </summary>
    /// <param name="localRect">The rectangle in local coordinates.</param>
    /// <returns>The axis-aligned bounding box of the transformed rectangle.</returns>
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

    /// <summary>Gets the element's arranged area in root (window) coordinates, as an axis-aligned bounding box.</summary>
    /// <returns>The window-space bounding box of (0, 0, <see cref="Bounds"/> width, height).</returns>
    public Rect GetScreenBounds() => TransformRectToScreen(new Rect(0, 0, Bounds.Width, Bounds.Height));

    /// <summary>
    /// Raises a pointer event on this element and then on each ancestor until a handler sets
    /// <see cref="RoutedEventArgs.Handled"/>, an overlay element (such as a popup) is reached, or the root is reached.
    /// </summary>
    /// <remarks>
    /// The same <paramref name="e"/> instance is passed to every handler, with <see cref="PointerEventArgs.Position"/>
    /// updated to each receiving element's local coordinates. Handlers that need the position later must copy it,
    /// not keep the args object. The local position is mapped up one level at a time, so dispatch costs O(depth).
    /// </remarks>
    /// <typeparam name="T">The pointer event args type.</typeparam>
    /// <param name="e">The event args, with <see cref="PointerEventArgs.ScreenPosition"/> in window coordinates.</param>
    /// <param name="action">Invokes the handler for one element, e.g. <c>(el, args) =&gt; el.OnPointerPressed(args)</c>.</param>
    public void DispatchBubblePointerEvent<T>(T e, Action<UIElement, T> action) where T : PointerEventArgs
    {
        e.Source ??= this;
        e.OriginalSource ??= this;

        Point localPos = PointToClient(e.ScreenPosition);
        UIElement current = this;
        while (true)
        {
            e.Position = localPos;
            action(current, e);

            if (e.Handled || current.IsOverlayElement || current.Parent is not UIElement parent)
            {
                break;
            }

            // The local transform maps this element's coordinates into its parent's coordinates.
            var v = Vector2.Transform(new Vector2(localPos.X, localPos.Y), current.GetLocalTransform());
            localPos = new Point(v.X, v.Y);
            current = parent;
        }
    }

    /// <summary>
    /// Raises a keyboard or text event on this element and then on each ancestor until a handler sets
    /// <see cref="RoutedEventArgs.Handled"/>, the current modal root (<see cref="FocusManager.CurrentModal"/>) or an
    /// overlay element has been processed, or the root is reached.
    /// </summary>
    /// <typeparam name="T">The event args type.</typeparam>
    /// <param name="e">The event args, shared by all handlers. <see cref="RoutedEventArgs.Source"/> and
    /// <see cref="RoutedEventArgs.OriginalSource"/> default to this element if not already set.</param>
    /// <param name="action">Invokes the handler for one element, e.g. <c>(el, args) =&gt; el.OnKeyDown(args)</c>.</param>
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

    #region Tunneling + bubbling dispatch

    // The elements on an event's route and their local pointer positions, bottom-up. Reused per thread; a dispatch that
    // starts while another is running (a handler raising an event) gets its own buffer.
    private sealed class EventRoute
    {
        public readonly List<UIElement> Elements = new();
        public readonly List<Point> Positions = new();

        public int Count => Elements.Count;

        public void Add(UIElement element, Point position)
        {
            Elements.Add(element);
            Positions.Add(position);
        }

        public void Clear()
        {
            Elements.Clear();
            Positions.Clear();
        }
    }

    [ThreadStatic] private static EventRoute? t_eventRoute;

    private static EventRoute RentRoute()
    {
        var route = t_eventRoute ?? new EventRoute();
        t_eventRoute = null;
        return route;
    }

    private static void ReturnRoute(EventRoute route)
    {
        route.Clear();
        t_eventRoute = route;
    }

    /// <summary>
    /// Raises a pointer event in two phases: first the preview (tunneling) phase from the outermost element down to this one,
    /// then the bubbling phase from this element up. The route ends at the root or at an overlay element (such as a popup).
    /// </summary>
    /// <remarks>
    /// Setting <see cref="RoutedEventArgs.Handled"/> in either phase stops the event, so a parent's preview handler can
    /// intercept input before its children see it. As with <see cref="DispatchBubblePointerEvent{T}"/>, the same
    /// <paramref name="e"/> instance is passed to every handler with <see cref="PointerEventArgs.Position"/> set to the
    /// receiving element's local coordinates. The route is computed once and dispatch does not allocate.
    /// </remarks>
    /// <typeparam name="T">The pointer event args type.</typeparam>
    /// <param name="e">The event args, with <see cref="PointerEventArgs.ScreenPosition"/> in window coordinates.</param>
    /// <param name="preview">Invokes the preview handler, e.g. <c>(el, args) =&gt; el.OnPreviewPointerPressed(args)</c>.</param>
    /// <param name="bubble">Invokes the bubbling handler, e.g. <c>(el, args) =&gt; el.OnPointerPressed(args)</c>.</param>
    public void DispatchPointerEvent<T>(T e, Action<UIElement, T> preview, Action<UIElement, T> bubble) where T : PointerEventArgs
    {
        e.Source ??= this;
        e.OriginalSource ??= this;

        var route = RentRoute();
        try
        {
            Point localPos = PointToClient(e.ScreenPosition);
            UIElement current = this;
            while (true)
            {
                route.Add(current, localPos);
                if (current.IsOverlayElement || current.Parent is not UIElement parent)
                {
                    break;
                }

                var v = Vector2.Transform(new Vector2(localPos.X, localPos.Y), current.GetLocalTransform());
                localPos = new Point(v.X, v.Y);
                current = parent;
            }

            for (int i = route.Count - 1; i >= 0; i--)
            {
                e.Position = route.Positions[i];
                preview(route.Elements[i], e);
                if (e.Handled) return;
            }

            for (int i = 0; i < route.Count; i++)
            {
                e.Position = route.Positions[i];
                bubble(route.Elements[i], e);
                if (e.Handled) return;
            }
        }
        finally
        {
            ReturnRoute(route);
        }
    }

    /// <summary>
    /// Raises a keyboard or text event in two phases: the preview (tunneling) phase from the outermost element down to this
    /// one, then the bubbling phase back up. The route ends at the root, the active modal scope, or an overlay element.
    /// </summary>
    /// <remarks>Setting <see cref="RoutedEventArgs.Handled"/> in either phase stops the event. Dispatch does not allocate.</remarks>
    /// <typeparam name="T">The event args type.</typeparam>
    /// <param name="e">The event args.</param>
    /// <param name="preview">Invokes the preview handler, e.g. <c>(el, args) =&gt; el.OnPreviewKeyDown(args)</c>.</param>
    /// <param name="bubble">Invokes the bubbling handler, e.g. <c>(el, args) =&gt; el.OnKeyDown(args)</c>.</param>
    public void DispatchKeyEvent<T>(T e, Action<UIElement, T> preview, Action<UIElement, T> bubble) where T : RoutedEventArgs
    {
        e.Source ??= this;
        e.OriginalSource ??= this;

        var route = RentRoute();
        try
        {
            UIElement? current = this;
            while (current != null)
            {
                route.Add(current, Point.Zero);
                if (current == FocusManager.CurrentModal || current.IsOverlayElement)
                {
                    break;
                }
                current = current.Parent as UIElement;
            }

            for (int i = route.Count - 1; i >= 0; i--)
            {
                preview(route.Elements[i], e);
                if (e.Handled) return;
            }

            for (int i = 0; i < route.Count; i++)
            {
                bubble(route.Elements[i], e);
                if (e.Handled) return;
            }
        }
        finally
        {
            ReturnRoute(route);
        }
    }

    #endregion

    // Layout rounding. Infinite and NaN sizes pass through unchanged.
    private static float RoundUp(float value) => float.IsFinite(value) ? MathF.Ceiling(value - 0.0001f) : value;

    // Rounds the left/top and right/bottom edges independently, so adjacent elements stay flush.
    private static Rect RoundEdges(float x, float y, float width, float height)
    {
        float left = MathF.Round(x);
        float top = MathF.Round(y);
        float right = float.IsFinite(width) ? MathF.Round(x + width) : x + width;
        float bottom = float.IsFinite(height) ? MathF.Round(y + height) : y + height;
        return new Rect(left, top, right - left, bottom - top);
    }

    private Size _previousAvailableSize = Size.Zero;
    private Rect _previousFinalRect = Rect.Zero;
    private Size _untransformedDesiredSize = Size.Zero;
    /// <summary>
    /// Gets the size computed by the last <see cref="Measure"/> before applying <see cref="VisualNode.Transform"/> and
    /// adding <see cref="Margin"/>.
    /// </summary>
    public Size UntransformedDesiredSize => _untransformedDesiredSize;

    /// <summary>
    /// Marks this element's measure and arrange as invalid, so the next <see cref="Measure"/> and <see cref="Arrange"/>
    /// recompute them, and raises <see cref="VisualNode.NeedsLayoutUpdate"/> up to the root.
    /// </summary>
    /// <remarks>
    /// Ancestors are marked invalid too, stopping at the first one whose measure and arrange are both already invalid.
    /// Does nothing if measure is already invalid.
    /// </remarks>
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

    /// <summary>
    /// Marks this element's arrange as invalid, so the next <see cref="Arrange"/> recomputes it, and raises
    /// <see cref="VisualNode.NeedsLayoutUpdate"/> up to the root.
    /// </summary>
    /// <remarks>
    /// Ancestors' arrange is marked invalid too, stopping at the first one whose arrange is already invalid.
    /// Does nothing if arrange is already invalid.
    /// </remarks>
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

    /// <summary>
    /// Computes <see cref="DesiredSize"/> for the given available space. Called by the parent during the measure pass.
    /// </summary>
    /// <remarks>
    /// The available size is reduced by <see cref="Margin"/>, adjusted for a non-identity <see cref="VisualNode.Transform"/>,
    /// and constrained by <see cref="Width"/>, <see cref="Height"/> and the min/max properties before being passed to
    /// <see cref="MeasureOverride"/>; the result is clamped the same way. The work is skipped when measure is still
    /// valid and <paramref name="availableSize"/> equals the previous call's. A collapsed element gets a zero size
    /// without calling <see cref="MeasureOverride"/>.
    /// </remarks>
    /// <param name="availableSize">The space the parent offers, including margin; may be infinite.</param>
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

        constraintWidth = LayoutMath.ClampMinWins(constraintWidth, MinWidth, MaxWidth);
        constraintHeight = LayoutMath.ClampMinWins(constraintHeight, MinHeight, MaxHeight);

        Size measured = MeasureOverride(new Size(constraintWidth, constraintHeight));

        // Clamping to min/max/explicit
        float finalWidth = !float.IsNaN(Width) ? Width : measured.Width;
        float finalHeight = !float.IsNaN(Height) ? Height : measured.Height;

        finalWidth = LayoutMath.ClampMinWins(finalWidth, MinWidth, MaxWidth);
        finalHeight = LayoutMath.ClampMinWins(finalHeight, MinHeight, MaxHeight);

        _untransformedDesiredSize = new Size(finalWidth, finalHeight);

        if (Parent == null || transform.IsIdentity)
        {
            var desired = new Size(finalWidth, finalHeight).Inflate(margin.Horizontal, margin.Vertical);
            DesiredSize = UseLayoutRounding ? new Size(RoundUp(desired.Width), RoundUp(desired.Height)) : desired;
        }
        else
        {
            var transBounds = ComputeTransformedBounds(finalWidth, finalHeight);
            DesiredSize = new Size(transBounds.Width, transBounds.Height).Inflate(margin.Horizontal, margin.Vertical);
        }

        IsMeasureValid = true;
    }

    /// <summary>
    /// Positions and sizes the element within <paramref name="finalRect"/> and sets <see cref="Bounds"/>. Called by the
    /// parent during the arrange pass, after <see cref="Measure"/>.
    /// </summary>
    /// <remarks>
    /// The rectangle is reduced by <see cref="Margin"/>, then the element is sized and placed according to
    /// <see cref="HorizontalAlignment"/>, <see cref="VerticalAlignment"/>, <see cref="Width"/>, <see cref="Height"/> and
    /// the min/max properties, calling <see cref="ArrangeOverride"/> to arrange the content. With a non-identity
    /// <see cref="VisualNode.Transform"/>, alignment applies to the transformed bounding box, and a stretched element is
    /// centered rather than resized. The work is skipped when arrange is still valid and <paramref name="finalRect"/>
    /// equals the previous call's. A collapsed element gets <see cref="Rect.Zero"/> bounds without calling
    /// <see cref="ArrangeOverride"/>.
    /// </remarks>
    /// <param name="finalRect">The slot assigned by the parent, in the parent's coordinates and including margin.</param>
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

            childWidth = LayoutMath.ClampMinWins(childWidth, MinWidth, MaxWidth);
            childHeight = LayoutMath.ClampMinWins(childHeight, MinHeight, MaxHeight);

            float maxArrangeWidth = LayoutMath.ClampMinWins(innerRect.Width, MinWidth, MaxWidth);
            float maxArrangeHeight = LayoutMath.ClampMinWins(innerRect.Height, MinHeight, MaxHeight);

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
                ? LayoutMath.ClampMinWins(innerRect.Width, MinWidth, MaxWidth)
                : LayoutMath.ClampMinWins(arrangedContentSize.Width, MinWidth, MaxWidth);

            float height = VerticalAlignment == VerticalAlignment.Stretch
                ? LayoutMath.ClampMinWins(innerRect.Height, MinHeight, MaxHeight)
                : LayoutMath.ClampMinWins(arrangedContentSize.Height, MinHeight, MaxHeight);

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

            Bounds = UseLayoutRounding ? RoundEdges(x, y, width, height) : new Rect(x, y, width, height);
            IsArrangeValid = true;
            return;
        }

        // Transformed element path:
        float unWidth = !float.IsNaN(Width) ? Width : _untransformedDesiredSize.Width;
        float unHeight = !float.IsNaN(Height) ? Height : _untransformedDesiredSize.Height;

        unWidth = LayoutMath.ClampMinWins(unWidth, MinWidth, MaxWidth);
        unHeight = LayoutMath.ClampMinWins(unHeight, MinHeight, MaxHeight);

        Size childArrangeSize = new Size(unWidth, unHeight);
        Size contentSize = ArrangeOverride(childArrangeSize);

        float finalUnWidth = !float.IsNaN(Width) ? Width : contentSize.Width;
        float finalUnHeight = !float.IsNaN(Height) ? Height : contentSize.Height;

        finalUnWidth = LayoutMath.ClampMinWins(finalUnWidth, MinWidth, MaxWidth);
        finalUnHeight = LayoutMath.ClampMinWins(finalUnHeight, MinHeight, MaxHeight);

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

    /// <summary>
    /// Measures the element's content and returns the size it needs. Override to implement custom layout; the base
    /// implementation measures every child with <paramref name="availableSize"/> and returns the largest desired width
    /// and height.
    /// </summary>
    /// <param name="availableSize">The space available for content, excluding margin and already constrained by the size properties.</param>
    /// <returns>The desired content size, excluding margin.</returns>
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

    /// <summary>
    /// Arranges the element's content within the given size. Override to implement custom layout; the base
    /// implementation arranges every child in a rectangle at (0, 0) of size <paramref name="finalSize"/>.
    /// </summary>
    /// <param name="finalSize">The size available for content, excluding margin.</param>
    /// <returns>The size actually used; with non-stretch alignment it determines the element's final size.</returns>
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

    /// <summary>
    /// Finds the topmost element at <paramref name="point"/> in this element's subtree.
    /// </summary>
    /// <remarks>
    /// Returns <c>null</c> if this element is not <see cref="Visibility.Visible"/>, has <see cref="IsHitTestVisible"/>
    /// set to <c>false</c>, or does not contain the point (after applying its transforms). Children are tested last to
    /// first, so later children win; overlay children are skipped. Children outside this element's bounds are not found.
    /// </remarks>
    /// <param name="point">The point in the parent's coordinates.</param>
    /// <returns>The deepest element containing the point, this element if no child does, or <c>null</c>.</returns>
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

    // Preview (tunneling) events run from the outermost element down to the target before the bubbling events run back up;
    // see DispatchPointerEvent and DispatchKeyEvent. Marking one handled stops the event before children see it.

    /// <summary>Occurs, outermost element first, before <see cref="PointerPressed"/> bubbles.</summary>
    public event EventHandler<PointerEventArgs>? PreviewPointerPressed;
    /// <summary>Occurs, outermost element first, before <see cref="PointerReleased"/> bubbles.</summary>
    public event EventHandler<PointerEventArgs>? PreviewPointerReleased;
    /// <summary>Occurs, outermost element first, before <see cref="PointerMoved"/> bubbles.</summary>
    public event EventHandler<PointerEventArgs>? PreviewPointerMoved;
    /// <summary>Occurs, outermost element first, before <see cref="PointerWheel"/> bubbles.</summary>
    public event EventHandler<PointerWheelEventArgs>? PreviewPointerWheel;
    /// <summary>Occurs, outermost element first, before <see cref="KeyDown"/> bubbles.</summary>
    public event EventHandler<KeyEventArgs>? PreviewKeyDown;
    /// <summary>Occurs, outermost element first, before <see cref="KeyUp"/> bubbles.</summary>
    public event EventHandler<KeyEventArgs>? PreviewKeyUp;
    /// <summary>Occurs, outermost element first, before <see cref="TextInput"/> bubbles.</summary>
    public event EventHandler<TextInputEventArgs>? PreviewTextInput;

    /// <summary>Handles the preview (tunneling) phase of a pointer press. The base implementation raises <see cref="PreviewPointerPressed"/>.</summary>
    public virtual void OnPreviewPointerPressed(PointerEventArgs e) => PreviewPointerPressed?.Invoke(this, e);
    /// <summary>Handles the preview (tunneling) phase of a pointer release. The base implementation raises <see cref="PreviewPointerReleased"/>.</summary>
    public virtual void OnPreviewPointerReleased(PointerEventArgs e) => PreviewPointerReleased?.Invoke(this, e);
    /// <summary>Handles the preview (tunneling) phase of a pointer move. The base implementation raises <see cref="PreviewPointerMoved"/>.</summary>
    public virtual void OnPreviewPointerMoved(PointerEventArgs e) => PreviewPointerMoved?.Invoke(this, e);
    /// <summary>Handles the preview (tunneling) phase of a wheel event. The base implementation raises <see cref="PreviewPointerWheel"/>.</summary>
    public virtual void OnPreviewPointerWheel(PointerWheelEventArgs e) => PreviewPointerWheel?.Invoke(this, e);
    /// <summary>Handles the preview (tunneling) phase of a key press. The base implementation raises <see cref="PreviewKeyDown"/>.</summary>
    public virtual void OnPreviewKeyDown(KeyEventArgs e) => PreviewKeyDown?.Invoke(this, e);
    /// <summary>Handles the preview (tunneling) phase of a key release. The base implementation raises <see cref="PreviewKeyUp"/>.</summary>
    public virtual void OnPreviewKeyUp(KeyEventArgs e) => PreviewKeyUp?.Invoke(this, e);
    /// <summary>Handles the preview (tunneling) phase of text input. The base implementation raises <see cref="PreviewTextInput"/>.</summary>
    public virtual void OnPreviewTextInput(TextInputEventArgs e) => PreviewTextInput?.Invoke(this, e);

    /// <summary>Occurs when the pointer enters the element; raised by <see cref="OnPointerEntered"/>.</summary>
    public event EventHandler<PointerEventArgs>? PointerEntered;
    /// <summary>Occurs when the pointer leaves the element; raised by <see cref="OnPointerExited"/>.</summary>
    public event EventHandler<PointerEventArgs>? PointerExited;
    /// <summary>Occurs when a pointer button is pressed on the element; raised by <see cref="OnPointerPressed"/>.</summary>
    public event EventHandler<PointerEventArgs>? PointerPressed;
    /// <summary>Occurs when a pointer button is released on the element; raised by <see cref="OnPointerReleased"/>.</summary>
    public event EventHandler<PointerEventArgs>? PointerReleased;
    /// <summary>Occurs when the pointer moves over the element; raised by <see cref="OnPointerMoved"/>.</summary>
    public event EventHandler<PointerEventArgs>? PointerMoved;
    /// <summary>Occurs when the pointer wheel is scrolled over the element; raised by <see cref="OnPointerWheel"/>.</summary>
    public event EventHandler<PointerWheelEventArgs>? PointerWheel;
    /// <summary>Occurs when a key is pressed while the element or a descendant has focus; raised by <see cref="OnKeyDown"/>.</summary>
    public event EventHandler<KeyEventArgs>? KeyDown;
    /// <summary>Occurs when a key is released while the element or a descendant has focus; raised by <see cref="OnKeyUp"/>.</summary>
    public event EventHandler<KeyEventArgs>? KeyUp;
    /// <summary>Occurs when text is entered while the element or a descendant has focus; raised by <see cref="OnTextInput"/>.</summary>
    public event EventHandler<TextInputEventArgs>? TextInput;

    /// <summary>
    /// Called when the pointer enters the element. The base implementation sets <see cref="IsHovered"/>, raises
    /// <see cref="PointerEntered"/> and invalidates rendering.
    /// </summary>
    /// <param name="e">The event data.</param>
    public virtual void OnPointerEntered(PointerEventArgs e)
    {
        IsHovered = true;
        PointerEntered?.Invoke(this, e);
        InvalidateVisual();
    }

    /// <summary>
    /// Called when the pointer leaves the element. The base implementation clears <see cref="IsHovered"/> and
    /// <see cref="IsPressed"/>, raises <see cref="PointerExited"/> and invalidates rendering.
    /// </summary>
    /// <param name="e">The event data.</param>
    public virtual void OnPointerExited(PointerEventArgs e)
    {
        IsHovered = false;
        IsPressed = false;
        PointerExited?.Invoke(this, e);
        InvalidateVisual();
    }

    /// <summary>
    /// Called when a pointer button is pressed on the element or bubbles up from a descendant. The base implementation
    /// sets <see cref="IsPressed"/>, raises <see cref="PointerPressed"/> and invalidates rendering.
    /// </summary>
    /// <param name="e">The event data, with <see cref="PointerEventArgs.Position"/> in this element's coordinates.</param>
    public virtual void OnPointerPressed(PointerEventArgs e)
    {
        IsPressed = true;
        PointerPressed?.Invoke(this, e);
        InvalidateVisual();
    }

    /// <summary>
    /// Called when a pointer button is released on the element or bubbles up from a descendant. The base implementation
    /// clears <see cref="IsPressed"/>, raises <see cref="PointerReleased"/> and invalidates rendering.
    /// </summary>
    /// <param name="e">The event data, with <see cref="PointerEventArgs.Position"/> in this element's coordinates.</param>
    public virtual void OnPointerReleased(PointerEventArgs e)
    {
        IsPressed = false;
        PointerReleased?.Invoke(this, e);
        InvalidateVisual();
    }

    /// <summary>
    /// Called when the pointer moves over the element or the event bubbles up from a descendant. The base implementation
    /// raises <see cref="PointerMoved"/>.
    /// </summary>
    /// <param name="e">The event data, with <see cref="PointerEventArgs.Position"/> in this element's coordinates.</param>
    public virtual void OnPointerMoved(PointerEventArgs e)
    {
        PointerMoved?.Invoke(this, e);
    }

    /// <summary>
    /// Called when the pointer wheel is scrolled over the element or the event bubbles up from a descendant. The base
    /// implementation raises <see cref="PointerWheel"/>.
    /// </summary>
    /// <param name="e">The event data.</param>
    public virtual void OnPointerWheel(PointerWheelEventArgs e)
    {
        PointerWheel?.Invoke(this, e);
    }

    /// <summary>Occurs when the element receives keyboard focus; raised by <see cref="OnGotFocus"/>.</summary>
    public event EventHandler? GotFocus;
    /// <summary>Occurs when the element loses keyboard focus; raised by <see cref="OnLostFocus"/>.</summary>
    public event EventHandler? LostFocus;

    /// <summary>
    /// Called by <see cref="FocusManager"/> when the element receives keyboard focus. The base implementation sets
    /// <see cref="IsFocused"/>, raises <see cref="GotFocus"/> and invalidates rendering.
    /// </summary>
    public virtual void OnGotFocus()
    {
        IsFocused = true;
        GotFocus?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    /// <summary>
    /// Called by <see cref="FocusManager"/> when the element loses keyboard focus. The base implementation clears
    /// <see cref="IsFocused"/>, raises <see cref="LostFocus"/> and invalidates rendering.
    /// </summary>
    public virtual void OnLostFocus()
    {
        IsFocused = false;
        LostFocus?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    /// <summary>
    /// Called when a key is pressed while this element or a descendant has focus. The base implementation raises
    /// <see cref="KeyDown"/>.
    /// </summary>
    /// <param name="e">The event data; set <see cref="RoutedEventArgs.Handled"/> to stop bubbling.</param>
    public virtual void OnKeyDown(KeyEventArgs e)
    {
        KeyDown?.Invoke(this, e);
    }

    /// <summary>
    /// Called when a key is released while this element or a descendant has focus. The base implementation raises
    /// <see cref="KeyUp"/>.
    /// </summary>
    /// <param name="e">The event data; set <see cref="RoutedEventArgs.Handled"/> to stop bubbling.</param>
    public virtual void OnKeyUp(KeyEventArgs e)
    {
        KeyUp?.Invoke(this, e);
    }

    /// <summary>
    /// Called when text is entered while this element or a descendant has focus. The base implementation raises
    /// <see cref="TextInput"/>.
    /// </summary>
    /// <param name="e">The event data; set <see cref="RoutedEventArgs.Handled"/> to stop bubbling.</param>
    public virtual void OnTextInput(TextInputEventArgs e)
    {
        TextInput?.Invoke(this, e);
    }

    #endregion
}
