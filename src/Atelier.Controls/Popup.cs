using System;
using System.Numerics;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Controls;

public class Popup : Control
{
    public static readonly BindableProperty<bool> IsOpenProperty =
        BindableProperty.Register<Popup, bool>(
            nameof(IsOpen),
            false,
            (s, o, n) => ((Popup)s).OnIsOpenChanged(o, n)
        );

    public static readonly BindableProperty<UIElement?> ChildProperty =
        BindableProperty.Register<Popup, UIElement?>(
            nameof(Child),
            null,
            (s, o, n) => ((Popup)s).OnChildChanged(o, n)
        );

    public static readonly BindableProperty<UIElement?> PlacementTargetProperty =
        BindableProperty.Register<Popup, UIElement?>(nameof(PlacementTarget), null);

    public static readonly BindableProperty<PlacementMode> PlacementProperty =
        BindableProperty.Register<Popup, PlacementMode>(nameof(Placement), PlacementMode.Bottom);

    public static readonly BindableProperty<float> HorizontalOffsetProperty =
        BindableProperty.Register<Popup, float>(nameof(HorizontalOffset), 0f);

    public static readonly BindableProperty<float> VerticalOffsetProperty =
        BindableProperty.Register<Popup, float>(nameof(VerticalOffset), 0f);

    public static readonly BindableProperty<bool> StaysOpenProperty =
        BindableProperty.Register<Popup, bool>(nameof(StaysOpen), false);

    public static readonly BindableProperty<float> ElevationProperty =
        BindableProperty.Register<Popup, float>(nameof(Elevation), 6f);

    public static readonly BindableProperty<bool> MatchTargetWidthProperty =
        BindableProperty.Register<Popup, bool>(nameof(MatchTargetWidth), false);

    public static readonly BindableProperty<Color> BorderBrushProperty =
        BindableProperty.Register<Popup, Color>(nameof(BorderBrush), Color.Transparent, options: PropertyOptions.AffectsRender);

    public static readonly BindableProperty<Thickness> BorderThicknessProperty =
        BindableProperty.Register<Popup, Thickness>(nameof(BorderThickness), Thickness.Zero, options: PropertyOptions.AffectsRender);

    public Color BorderBrush
    {
        get => GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    public Thickness BorderThickness
    {
        get => GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public UIElement? Child
    {
        get => GetValue(ChildProperty);
        set => SetValue(ChildProperty, value);
    }

    public UIElement? PlacementTarget
    {
        get => GetValue(PlacementTargetProperty);
        set => SetValue(PlacementTargetProperty, value);
    }

    public PlacementMode Placement
    {
        get => GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    public float HorizontalOffset
    {
        get => GetValue(HorizontalOffsetProperty);
        set => SetValue(HorizontalOffsetProperty, value);
    }

    public float VerticalOffset
    {
        get => GetValue(VerticalOffsetProperty);
        set => SetValue(VerticalOffsetProperty, value);
    }

    public bool StaysOpen
    {
        get => GetValue(StaysOpenProperty);
        set => SetValue(StaysOpenProperty, value);
    }

    public float Elevation
    {
        get => GetValue(ElevationProperty);
        set => SetValue(ElevationProperty, value);
    }

    public bool MatchTargetWidth
    {
        get => GetValue(MatchTargetWidthProperty);
        set => SetValue(MatchTargetWidthProperty, value);
    }

    public Rect ActualBounds { get; private set; } = Rect.Zero;

    public event EventHandler? Opened;
    public event EventHandler? Closed;

    static Popup()
    {
        CornerRadiusProperty.OverrideDefaultValue<Popup>(new CornerRadius(8));
    }

    public Popup()
    {
        IsOverlayElement = true;
        Visibility = Visibility.Collapsed;
        IsHitTestVisible = false;
    }

    public override Point OverlayOrigin => ActualBounds.Location;

    private void OnIsOpenChanged(bool oldVal, bool newVal)
    {
        Visibility = newVal ? Visibility.Visible : Visibility.Collapsed;
        IsHitTestVisible = newVal;
        if (newVal)
        {
            PopupManager.OpenPopup(this);
            Opened?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            PopupManager.ClosePopup(this);
            ActualBounds = Rect.Zero;
            Closed?.Invoke(this, EventArgs.Empty);
        }
        InvalidateVisual();
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

    /// <summary>
    /// Computes the smart placement of the popup within the viewport, flipping or clamping as necessary.
    /// </summary>
    public Rect ComputeSmartPlacement(Size viewportSize)
    {
        if (Child == null || viewportSize.Width <= 0 || viewportSize.Height <= 0)
            return Rect.Zero;

        // 1. Measure child against available viewport bounds
        Child.Measure(viewportSize);
        var desired = Child.DesiredSize;
        float popupWidth = Math.Max(MinWidth, desired.Width);
        float popupHeight = Math.Max(MinHeight, desired.Height);

        if (MaxWidth > 0) popupWidth = Math.Min(popupWidth, MaxWidth);
        if (MaxHeight > 0) popupHeight = Math.Min(popupHeight, MaxHeight);

        // Target coordinates in screen/window space
        Point targetScreenPos = PlacementTarget != null ? PlacementTarget.PointToScreen(Point.Zero) : Point.Zero;
        float targetWidth = PlacementTarget?.Bounds.Width ?? 0;
        float targetHeight = PlacementTarget?.Bounds.Height ?? 0;

        if (MatchTargetWidth && targetWidth > 0)
        {
            popupWidth = Math.Max(popupWidth, targetWidth);
        }

        float x = targetScreenPos.X + HorizontalOffset;
        float y = targetScreenPos.Y + targetHeight + VerticalOffset;

        switch (Placement)
        {
            case PlacementMode.Bottom:
            case PlacementMode.BottomLeft:
            {
                float spaceBelow = viewportSize.Height - (targetScreenPos.Y + targetHeight + VerticalOffset);
                float spaceAbove = targetScreenPos.Y - VerticalOffset;

                // If bottom overflows and space above is greater than space below: flip above!
                if (y + popupHeight > viewportSize.Height && spaceAbove > spaceBelow)
                {
                    y = targetScreenPos.Y - popupHeight - VerticalOffset;
                }
                break;
            }

            case PlacementMode.BottomRight:
            {
                x = targetScreenPos.X + targetWidth - popupWidth + HorizontalOffset;
                float spaceBelow = viewportSize.Height - (targetScreenPos.Y + targetHeight + VerticalOffset);
                float spaceAbove = targetScreenPos.Y - VerticalOffset;

                if (y + popupHeight > viewportSize.Height && spaceAbove > spaceBelow)
                {
                    y = targetScreenPos.Y - popupHeight - VerticalOffset;
                }
                break;
            }

            case PlacementMode.Top:
            {
                y = targetScreenPos.Y - popupHeight - VerticalOffset;
                float spaceAbove = targetScreenPos.Y - VerticalOffset;
                float spaceBelow = viewportSize.Height - (targetScreenPos.Y + targetHeight + VerticalOffset);

                if (y < 0 && spaceBelow > spaceAbove)
                {
                    y = targetScreenPos.Y + targetHeight + VerticalOffset;
                }
                break;
            }

            case PlacementMode.Right:
            {
                x = targetScreenPos.X + targetWidth + HorizontalOffset;
                y = targetScreenPos.Y + VerticalOffset;
                if (x + popupWidth > viewportSize.Width)
                {
                    x = targetScreenPos.X - popupWidth - HorizontalOffset;
                }
                break;
            }

            case PlacementMode.Left:
            {
                x = targetScreenPos.X - popupWidth - HorizontalOffset;
                y = targetScreenPos.Y + VerticalOffset;
                if (x < 0)
                {
                    x = targetScreenPos.X + targetWidth + HorizontalOffset;
                }
                break;
            }

            case PlacementMode.Center:
            {
                x = (viewportSize.Width - popupWidth) * 0.5f + HorizontalOffset;
                y = (viewportSize.Height - popupHeight) * 0.5f + VerticalOffset;
                break;
            }
        }

        // 2. Viewport Clamping: ensure the popup stays strictly within visible window area
        if (y + popupHeight > viewportSize.Height)
        {
            y = Math.Max(0, viewportSize.Height - popupHeight);
        }
        if (y < 0)
        {
            y = 0;
            popupHeight = Math.Min(popupHeight, viewportSize.Height);
        }

        if (x + popupWidth > viewportSize.Width)
        {
            x = Math.Max(0, viewportSize.Width - popupWidth);
        }
        if (x < 0)
        {
            x = 0;
            popupWidth = Math.Min(popupWidth, viewportSize.Width);
        }

        return new Rect(x, y, popupWidth, popupHeight);
    }

    public void UpdatePlacement(Size viewportSize)
    {
        if (!IsOpen || Child == null)
        {
            ActualBounds = Rect.Zero;
            return;
        }

        ActualBounds = ComputeSmartPlacement(viewportSize);

        // Arrange child inside the popup's local coordinate system
        Child.Arrange(new Rect(Point.Zero, ActualBounds.Size));
    }

    protected override Size MeasureOverride(Size availableSize) => Size.Zero;
    protected override Size ArrangeOverride(Size finalSize) => Size.Zero;
    public override UIElement? HitTest(Point point) => null;

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
