using System;
using System.Numerics;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Controls;

/// <summary>
/// Displays a <see cref="Child"/> above all other content of its window, positioned next to a
/// <see cref="PlacementTarget"/> and kept inside the window.
/// </summary>
/// <remarks>
/// The popup takes no space in its parent's layout. While <see cref="IsOpen"/> it is registered with
/// <see cref="PopupManager"/>, which positions it after each layout pass, renders it on top of the window's tree and
/// routes pointer and keyboard input to it first. Unless <see cref="StaysOpen"/> is set, a click outside the popup or
/// Escape closes it (light dismiss). A popup closes automatically when it is removed from a displayed tree.
/// </remarks>
public class Popup : Control
{
    /// <summary>Identifies the <see cref="IsOpen"/> property.</summary>
    public static readonly BindableProperty<bool> IsOpenProperty =
        BindableProperty.Register<Popup, bool>(
            nameof(IsOpen),
            false,
            (s, o, n) => ((Popup)s).OnIsOpenChanged(o, n)
        );

    /// <summary>Identifies the <see cref="Child"/> property.</summary>
    public static readonly BindableProperty<UIElement?> ChildProperty =
        BindableProperty.Register<Popup, UIElement?>(
            nameof(Child),
            null,
            (s, o, n) => ((Popup)s).OnChildChanged(o, n)
        );

    /// <summary>Identifies the <see cref="PlacementTarget"/> property.</summary>
    public static readonly BindableProperty<UIElement?> PlacementTargetProperty =
        BindableProperty.Register<Popup, UIElement?>(nameof(PlacementTarget), null, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="Placement"/> property.</summary>
    public static readonly BindableProperty<PlacementMode> PlacementProperty =
        BindableProperty.Register<Popup, PlacementMode>(nameof(Placement), PlacementMode.Bottom, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="HorizontalOffset"/> property.</summary>
    public static readonly BindableProperty<float> HorizontalOffsetProperty =
        BindableProperty.Register<Popup, float>(nameof(HorizontalOffset), 0f, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="VerticalOffset"/> property.</summary>
    public static readonly BindableProperty<float> VerticalOffsetProperty =
        BindableProperty.Register<Popup, float>(nameof(VerticalOffset), 0f, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="StaysOpen"/> property.</summary>
    public static readonly BindableProperty<bool> StaysOpenProperty =
        BindableProperty.Register<Popup, bool>(nameof(StaysOpen), false);

    /// <summary>Identifies the <see cref="Elevation"/> property.</summary>
    public static readonly BindableProperty<float> ElevationProperty =
        BindableProperty.Register<Popup, float>(nameof(Elevation), 6f, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="MatchTargetWidth"/> property.</summary>
    public static readonly BindableProperty<bool> MatchTargetWidthProperty =
        BindableProperty.Register<Popup, bool>(nameof(MatchTargetWidth), false, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="BorderBrush"/> property.</summary>
    public static readonly BindableProperty<Color> BorderBrushProperty =
        BindableProperty.Register<Popup, Color>(nameof(BorderBrush), Color.Transparent, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="BorderThickness"/> property.</summary>
    public static readonly BindableProperty<Thickness> BorderThicknessProperty =
        BindableProperty.Register<Popup, Thickness>(nameof(BorderThickness), new Thickness(1), options: PropertyOptions.AffectsRender);

    /// <summary>
    /// Gets or sets the outline color. The default (transparent) lets the theme choose the color.
    /// </summary>
    public Color BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the outline thickness; the theme draws its left value. The default is 1; 0 draws no outline.
    /// </summary>
    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the popup is shown. Opening registers it with <see cref="PopupManager"/> and raises
    /// <see cref="Opened"/>; closing (including light dismiss) raises <see cref="Closed"/>. The default is <c>false</c>.
    /// </summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Gets or sets the content shown in the popup.</summary>
    public UIElement? Child
    {
        get => GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
    }

    /// <summary>
    /// Gets or sets the element the popup is positioned against. When <c>null</c> (the default), the popup's parent
    /// element is used.
    /// </summary>
    public UIElement? PlacementTarget
    {
        get => GetValue(PlacementTargetProperty);
        set => SetValue(PlacementTargetProperty, value);
    }

    /// <summary>
    /// Gets or sets where the popup appears relative to the target. When it does not fit on that side, it flips to the
    /// opposite side if there is more room there; it is then clamped (and if necessary shrunk) to the window. The
    /// default is <see cref="PlacementMode.Bottom"/>.
    /// </summary>
    public PlacementMode Placement
    {
        get => GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>
    /// Gets or sets a horizontal shift added to the computed position; positive values move the popup right for every
    /// placement, including after flipping. The default is 0.
    /// </summary>
    public float HorizontalOffset
    {
        get => GetValue(HorizontalOffsetProperty);
        set => SetValue(HorizontalOffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets a vertical shift added to the computed position; positive values move the popup down for every
    /// placement, including after flipping. The default is 0.
    /// </summary>
    public float VerticalOffset
    {
        get => GetValue(VerticalOffsetProperty);
        set => SetValue(VerticalOffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the popup stays open on clicks outside it and on Escape. The default is <c>false</c>
    /// (light dismiss).
    /// </summary>
    public bool StaysOpen
    {
        get => GetValue(StaysOpenProperty);
        set => SetValue(StaysOpenProperty, value);
    }

    /// <summary>Gets or sets the shadow depth; 0 draws no shadow. The default is 6.</summary>
    public float Elevation
    {
        get => GetValue(ElevationProperty);
        set => SetValue(ElevationProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the popup is at least as wide as the placement target (still limited by the window and
    /// <see cref="UIElement.MaxWidth"/>). The child is measured at that width. The default is <c>false</c>.
    /// </summary>
    public bool MatchTargetWidth
    {
        get => GetValue(MatchTargetWidthProperty);
        set => SetValue(MatchTargetWidthProperty, value);
    }

    /// <summary>
    /// Gets the popup's position and size in window coordinates, computed by <see cref="UpdatePlacement"/>;
    /// <see cref="Rect.Zero"/> while closed.
    /// </summary>
    public Rect ActualBounds { get; private set; } = Rect.Zero;

    /// <summary>
    /// Occurs after the popup opens. If its tree has been laid out, <see cref="ActualBounds"/> is already computed;
    /// the next layout pass keeps it up to date.
    /// </summary>
    public event EventHandler? Opened;

    /// <summary>Occurs after the popup closes, for whatever reason (including light dismiss and removal from the tree).</summary>
    public event EventHandler? Closed;

    // Inputs of the last placement; while unchanged (and the child's layout is valid), UpdatePlacement does no work.
    private bool _placementValid;
    private Size _lastViewport;
    private Rect _lastTargetRect;

    static Popup()
    {
        CornerRadiusProperty.OverrideDefaultValue<Popup>(new CornerRadius(8));
    }

    /// <summary>Initializes a new, closed <see cref="Popup"/>.</summary>
    public Popup()
    {
        IsOverlayElement = true;
        Visibility = Visibility.Collapsed;
        IsHitTestVisible = false;
    }

    /// <summary>Gets the top-left corner of <see cref="ActualBounds"/>, where the popup's content is placed in the window.</summary>
    public override Point OverlayOrigin => ActualBounds.Location;

    /// <inheritdoc/>
    protected override void OnPropertyValueChanged<T>(BindableProperty<T> property, T oldValue, T newValue)
    {
        _placementValid = false;
        base.OnPropertyValueChanged(property, oldValue, newValue);
    }

    /// <summary>Closes the popup when it leaves the displayed tree, so it no longer lingers in <see cref="PopupManager"/>.</summary>
    protected override void OnDetachedFromVisualTree()
    {
        IsOpen = false;
        base.OnDetachedFromVisualTree();
    }

    private void OnIsOpenChanged(bool oldVal, bool newVal)
    {
        Visibility = newVal ? Visibility.Visible : Visibility.Collapsed;
        IsHitTestVisible = newVal;
        _placementValid = false;
        if (newVal)
        {
            PopupManager.OpenPopup(this);

            // Position now when a viewport is known, so Opened handlers see real bounds.
            var viewport = _lastViewport;
            if (viewport.Width <= 0 || viewport.Height <= 0)
            {
                viewport = GetRootSize();
            }
            if (viewport.Width > 0 && viewport.Height > 0)
            {
                UpdatePlacement(viewport);
            }

            InvalidateVisual();
            Opened?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            PopupManager.ClosePopup(this);
            ActualBounds = Rect.Zero;
            InvalidateVisual();
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    private Size GetRootSize()
    {
        VisualNode node = this;
        while (node.Parent != null)
        {
            node = node.Parent;
        }
        return node != this && node is UIElement root ? root.Bounds.Size : Size.Zero;
    }

    private void OnChildChanged(UIElement? oldChild, UIElement? newChild)
    {
        if (oldChild != null)
        {
            RemoveChild(oldChild);
        }
        if (newChild != null)
        {
            AddChild(newChild);
        }
    }

    // The target used for positioning: PlacementTarget, else the parent element.
    private UIElement? EffectiveTarget => PlacementTarget ?? Parent as UIElement;

    private Rect GetTargetRect()
    {
        var target = EffectiveTarget;
        if (target == null)
        {
            return Rect.Zero;
        }
        var pos = target.PointToScreen(Point.Zero);
        return new Rect(pos.X, pos.Y, target.Bounds.Width, target.Bounds.Height);
    }

    /// <summary>
    /// Measures the child and computes where the popup goes inside a window of size <paramref name="viewportSize"/>:
    /// placed per <see cref="Placement"/> and the offsets, flipped to the other side of the target when that side has
    /// more room, and finally shrunk to fit and clamped into the window.
    /// </summary>
    /// <param name="viewportSize">The window's size, in the coordinates of <see cref="ActualBounds"/>.</param>
    /// <returns>The popup's bounds in window coordinates, or <see cref="Rect.Zero"/> without a child or viewport.</returns>
    public Rect ComputeSmartPlacement(Size viewportSize)
    {
        var child = Child;
        if (child == null || viewportSize.Width <= 0 || viewportSize.Height <= 0)
            return Rect.Zero;

        // The popup can never be larger than the window (or its own maximum size).
        float maxWidth = Math.Min(viewportSize.Width, MaxWidth);
        float maxHeight = Math.Min(viewportSize.Height, MaxHeight);
        float minWidth = Math.Min(MinWidth, maxWidth);
        float minHeight = Math.Min(MinHeight, maxHeight);

        var target = GetTargetRect();

        child.Measure(new Size(maxWidth, maxHeight));
        var desired = child.DesiredSize;
        float popupWidth = Math.Clamp(desired.Width, minWidth, maxWidth);
        if (MatchTargetWidth && target.Width > 0)
        {
            popupWidth = Math.Min(Math.Max(popupWidth, target.Width), maxWidth);
        }

        // Re-measure at the width the child will actually be arranged with, so wrapping content gets the right height.
        if (popupWidth != desired.Width)
        {
            child.Measure(new Size(popupWidth, maxHeight));
            desired = child.DesiredSize;
        }
        float popupHeight = Math.Clamp(desired.Height, minHeight, maxHeight);

        float hOffset = HorizontalOffset;
        float vOffset = VerticalOffset;
        float spaceAbove = target.Y;
        float spaceBelow = viewportSize.Height - target.Bottom;
        float spaceLeft = target.X;
        float spaceRight = viewportSize.Width - target.Right;

        float x = target.X + hOffset;
        float y = target.Bottom + vOffset;

        switch (Placement)
        {
            case PlacementMode.Bottom:
            case PlacementMode.BottomLeft:
            case PlacementMode.BottomRight:
                if (Placement == PlacementMode.BottomRight)
                {
                    x = target.Right - popupWidth + hOffset;
                }
                if (y + popupHeight > viewportSize.Height && spaceAbove > spaceBelow)
                {
                    y = target.Y - popupHeight + vOffset;
                }
                break;

            case PlacementMode.Top:
                y = target.Y - popupHeight + vOffset;
                if (y < 0 && spaceBelow > spaceAbove)
                {
                    y = target.Bottom + vOffset;
                }
                break;

            case PlacementMode.Right:
                x = target.Right + hOffset;
                y = target.Y + vOffset;
                if (x + popupWidth > viewportSize.Width && spaceLeft > spaceRight)
                {
                    x = target.X - popupWidth + hOffset;
                }
                break;

            case PlacementMode.Left:
                x = target.X - popupWidth + hOffset;
                y = target.Y + vOffset;
                if (x < 0 && spaceRight > spaceLeft)
                {
                    x = target.Right + hOffset;
                }
                break;

            case PlacementMode.Center:
                x = (viewportSize.Width - popupWidth) * 0.5f + hOffset;
                y = (viewportSize.Height - popupHeight) * 0.5f + vOffset;
                break;
        }

        // Keep the popup inside the window; its size is already capped to the window above.
        x = Math.Clamp(x, 0, viewportSize.Width - popupWidth);
        y = Math.Clamp(y, 0, viewportSize.Height - popupHeight);

        return new Rect(x, y, popupWidth, popupHeight);
    }

    /// <summary>
    /// Recomputes <see cref="ActualBounds"/> for a window of size <paramref name="viewportSize"/> and arranges the child
    /// in it. Called by <see cref="PopupManager.UpdatePopups"/> after each layout pass.
    /// </summary>
    /// <remarks>
    /// Does no work when the viewport, the target's window position and size, the popup's properties and the child's
    /// layout are unchanged since the previous call. Sets <see cref="ActualBounds"/> to <see cref="Rect.Zero"/> while
    /// closed or without a child.
    /// </remarks>
    /// <param name="viewportSize">The window's size.</param>
    public void UpdatePlacement(Size viewportSize)
    {
        var child = Child;
        if (!IsOpen || child == null)
        {
            ActualBounds = Rect.Zero;
            _placementValid = false;
            return;
        }

        var targetRect = GetTargetRect();
        if (_placementValid && viewportSize == _lastViewport && targetRect == _lastTargetRect
            && child.IsMeasureValid && child.IsArrangeValid)
        {
            return;
        }

        _lastViewport = viewportSize;
        _lastTargetRect = targetRect;

        var bounds = ComputeSmartPlacement(viewportSize);
        if (bounds != ActualBounds)
        {
            ActualBounds = bounds;
            InvalidateVisual();
        }

        // Arrange child inside the popup's local coordinate system
        child.Arrange(new Rect(Point.Zero, ActualBounds.Size));
        _placementValid = true;
    }

    /// <inheritdoc/>
    /// <remarks>A popup takes no space in its parent; its child is measured by <see cref="UpdatePlacement"/>.</remarks>
    protected override Size MeasureOverride(Size availableSize) => Size.Zero;

    /// <inheritdoc/>
    /// <remarks>A popup takes no space in its parent; its child is arranged by <see cref="UpdatePlacement"/>.</remarks>
    protected override Size ArrangeOverride(Size finalSize) => Size.Zero;

    /// <summary>Always returns <c>null</c>: the popup is hit-tested by <see cref="PopupManager.HitTest"/>, not through its parent.</summary>
    /// <param name="point">Ignored.</param>
    public override UIElement? HitTest(Point point) => null;

    /// <summary>
    /// Draws the open popup at <see cref="ActualBounds"/>: its shadow, its themed background and outline, and its child
    /// clipped to <see cref="Control.CornerRadius"/>. Called by <see cref="PopupManager.RenderPopups"/>.
    /// </summary>
    /// <param name="context">The window's drawing context, in window coordinates.</param>
    /// <param name="presenter">Draws themed elements; <c>null</c> draws only the child's own content.</param>
    public void RenderPopup(ref DrawingContext context, IElementVisualPresenter? presenter)
    {
        if (!IsOpen || Child == null || ActualBounds.IsEmpty) return;

        // 1. Draw Material elevation shadow
        if (Elevation > 0)
        {
            context.DrawShadow(ActualBounds, CornerRadius, Elevation, Color.Black);
        }

        // 2. Position popup overlay at ActualBounds in window space
        using var transformScope = context.PushTransform(Matrix3x2.CreateTranslation(ActualBounds.X, ActualBounds.Y));

        // 3. Render popup background / outline if styled
        presenter?.Render(this, ref context);

        // 4. Render popup child visual tree clipped to CornerRadius
        using (context.PushRoundedClip(new Rect(Point.Zero, ActualBounds.Size), CornerRadius))
        {
            VisualTreeRenderer.Render(Child!, ref context, presenter);
        }
    }
}
