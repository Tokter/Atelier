using System;
using Atelier.Core.Animation;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

public enum ScrollBarVisibility
{
    Disabled,
    Auto,
    Hidden,
    Visible
}

public class ScrollViewer : ContentControl
{
    public static readonly BindableProperty<float> ScrollOffsetXProperty =
        BindableProperty.Register<ScrollViewer, float>(
            nameof(ScrollOffsetX),
            0f,
            (s, o, n) =>
            {
                var sv = (ScrollViewer)s;
                sv.UpdateChildArrange();
                sv.InvalidateArrange();
                sv.InvalidateVisual();
            }
        );

    public static readonly BindableProperty<float> ScrollOffsetYProperty =
        BindableProperty.Register<ScrollViewer, float>(
            nameof(ScrollOffsetY),
            0f,
            (s, o, n) =>
            {
                var sv = (ScrollViewer)s;
                sv.UpdateChildArrange();
                sv.InvalidateArrange();
                sv.InvalidateVisual();
            }
        );

    public static readonly BindableProperty<ScrollBarVisibility> HorizontalScrollBarVisibilityProperty =
        BindableProperty.Register<ScrollViewer, ScrollBarVisibility>(
            nameof(HorizontalScrollBarVisibility),
            ScrollBarVisibility.Disabled,
            (s, o, n) => ((ScrollViewer)s).InvalidateMeasure()
        );

    public static readonly BindableProperty<ScrollBarVisibility> VerticalScrollBarVisibilityProperty =
        BindableProperty.Register<ScrollViewer, ScrollBarVisibility>(
            nameof(VerticalScrollBarVisibility),
            ScrollBarVisibility.Auto,
            (s, o, n) => ((ScrollViewer)s).InvalidateMeasure()
        );

    public float ScrollOffsetX
    {
        get => GetValue(ScrollOffsetXProperty);
        set => SetValue(ScrollOffsetXProperty, value);
    }

    public float ScrollOffsetY
    {
        get => GetValue(ScrollOffsetYProperty);
        set => SetValue(ScrollOffsetYProperty, value);
    }

    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => GetValue(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => GetValue(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    public Size Extent { get; private set; } = Size.Zero;
    public Size Viewport { get; private set; } = Size.Zero;

    public float MaxScrollX => HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled
        ? 0f
        : Math.Max(0, Extent.Width - Viewport.Width);

    public float MaxScrollY => VerticalScrollBarVisibility == ScrollBarVisibility.Disabled
        ? 0f
        : Math.Max(0, Extent.Height - Viewport.Height);

    public bool CanScrollHorizontally =>
        (HorizontalScrollBarVisibility == ScrollBarVisibility.Auto && MaxScrollX > 0) ||
        HorizontalScrollBarVisibility == ScrollBarVisibility.Visible;

    public bool CanScrollVertically =>
        (VerticalScrollBarVisibility == ScrollBarVisibility.Auto && MaxScrollY > 0) ||
        VerticalScrollBarVisibility == ScrollBarVisibility.Visible;

    public bool IsVerticalScrollBarHovered { get; internal set; }
    public bool IsHorizontalScrollBarHovered { get; internal set; }

    public bool IsScrollBarHovered => IsVerticalScrollBarHovered || IsHorizontalScrollBarHovered;

    private enum DragMode { None, Vertical, Horizontal }
    private DragMode _dragMode = DragMode.None;

    public bool IsVerticalThumbDragging => _dragMode == DragMode.Vertical;
    public bool IsHorizontalThumbDragging => _dragMode == DragMode.Horizontal;
    public bool IsThumbDragging => _dragMode != DragMode.None;

    private static AnimationClock? _clock;
    public static void SetGlobalAnimationClock(AnimationClock? clock) => _clock = clock;

    public const float NormalScrollBarWidth = 6f;
    public const float HoveredScrollBarWidth = 10f;

    public float CurrentVerticalScrollBarWidth { get; private set; } = NormalScrollBarWidth;
    public float CurrentHorizontalScrollBarWidth { get; private set; } = NormalScrollBarWidth;

    public float ScrollBarWidth => CurrentVerticalScrollBarWidth;
    public float ScrollBarMargin => 2f;
    public float ScrollBarHitWidth => 28f;

    private FloatAnimation? _vWidthAnim;
    private FloatAnimation? _hWidthAnim;

    private float _dragStartMousePos;
    private float _dragStartScrollOffset;

    public ScrollViewer()
    {
        ClipToBounds = true;
    }

    private void AnimateVerticalScrollBarWidth(float targetWidth)
    {
        if (MathF.Abs(CurrentVerticalScrollBarWidth - targetWidth) < 0.01f)
            return;

        if (_clock == null)
        {
            CurrentVerticalScrollBarWidth = targetWidth;
            InvalidateVisual();
            return;
        }

        FloatAnimation? anim = null;
        anim = new FloatAnimation(
            from: CurrentVerticalScrollBarWidth,
            to: targetWidth,
            duration: TimeSpan.FromMilliseconds(targetWidth > CurrentVerticalScrollBarWidth ? 180 : 250),
            onUpdate: val =>
            {
                if (_vWidthAnim == anim)
                {
                    CurrentVerticalScrollBarWidth = val;
                    InvalidateVisual();
                }
            },
            easing: Easing.EaseOutCubic
        );

        _vWidthAnim = anim;
        _clock.Add(anim);
    }

    private void AnimateHorizontalScrollBarWidth(float targetWidth)
    {
        if (MathF.Abs(CurrentHorizontalScrollBarWidth - targetWidth) < 0.01f)
            return;

        if (_clock == null)
        {
            CurrentHorizontalScrollBarWidth = targetWidth;
            InvalidateVisual();
            return;
        }

        FloatAnimation? anim = null;
        anim = new FloatAnimation(
            from: CurrentHorizontalScrollBarWidth,
            to: targetWidth,
            duration: TimeSpan.FromMilliseconds(targetWidth > CurrentHorizontalScrollBarWidth ? 180 : 250),
            onUpdate: val =>
            {
                if (_hWidthAnim == anim)
                {
                    CurrentHorizontalScrollBarWidth = val;
                    InvalidateVisual();
                }
            },
            easing: Easing.EaseOutCubic
        );

        _hWidthAnim = anim;
        _clock.Add(anim);
    }

    public Rect GetVerticalTrackRect()
    {
        float w = Bounds.Width;
        float h = Bounds.Height;
        float currentW = CurrentVerticalScrollBarWidth;
        float bottomReserved = CanScrollHorizontally ? CurrentHorizontalScrollBarWidth + ScrollBarMargin * 2 : ScrollBarMargin;
        return new Rect(
            w - currentW - ScrollBarMargin,
            ScrollBarMargin,
            currentW,
            Math.Max(0, h - ScrollBarMargin - bottomReserved)
        );
    }

    public Rect GetVerticalThumbRect()
    {
        var track = GetVerticalTrackRect();
        if (MaxScrollY <= 0 || track.Height <= 0 || Extent.Height <= 0) return Rect.Zero;

        float viewportRatio = Math.Clamp(Viewport.Height / Extent.Height, 0.05f, 1.0f);
        float thumbHeight = Math.Max(24f, track.Height * viewportRatio);
        float availableTravel = track.Height - thumbHeight;

        float scrollRatio = Math.Clamp(ScrollOffsetY / MaxScrollY, 0f, 1f);
        float thumbY = track.Y + scrollRatio * availableTravel;

        return new Rect(track.X, thumbY, track.Width, thumbHeight);
    }

    public Rect GetHorizontalTrackRect()
    {
        float w = Bounds.Width;
        float h = Bounds.Height;
        float currentH = CurrentHorizontalScrollBarWidth;
        float rightReserved = CanScrollVertically ? CurrentVerticalScrollBarWidth + ScrollBarMargin * 2 : ScrollBarMargin;
        return new Rect(
            ScrollBarMargin,
            h - currentH - ScrollBarMargin,
            Math.Max(0, w - ScrollBarMargin - rightReserved),
            currentH
        );
    }

    public Rect GetHorizontalThumbRect()
    {
        var track = GetHorizontalTrackRect();
        if (MaxScrollX <= 0 || track.Width <= 0 || Extent.Width <= 0) return Rect.Zero;

        float viewportRatio = Math.Clamp(Viewport.Width / Extent.Width, 0.05f, 1.0f);
        float thumbWidth = Math.Max(24f, track.Width * viewportRatio);
        float availableTravel = track.Width - thumbWidth;

        float scrollRatio = Math.Clamp(ScrollOffsetX / MaxScrollX, 0f, 1f);
        float thumbX = track.X + scrollRatio * availableTravel;

        return new Rect(thumbX, track.Y, thumbWidth, track.Height);
    }

    // Backward compatibility aliases
    public Rect GetTrackRect() => GetVerticalTrackRect();
    public Rect GetThumbRect() => GetVerticalThumbRect();

    public override UIElement? HitTest(Point point)
    {
        if (Visibility != Visibility.Visible || !IsHitTestVisible || !Bounds.Contains(point))
        {
            return null;
        }

        Point localPoint = point.Offset(-Bounds.X, -Bounds.Y);

        // Check vertical scrollbar hit zone
        float effectiveVHitWidth = (IsVerticalScrollBarHovered || IsVerticalThumbDragging)
            ? ScrollBarHitWidth
            : (CurrentVerticalScrollBarWidth + ScrollBarMargin * 2);

        if (CanScrollVertically && localPoint.X >= Bounds.Width - effectiveVHitWidth)
        {
            return this;
        }

        // Check horizontal scrollbar hit zone
        float effectiveHHitWidth = (IsHorizontalScrollBarHovered || IsHorizontalThumbDragging)
            ? ScrollBarHitWidth
            : (CurrentHorizontalScrollBarWidth + ScrollBarMargin * 2);

        if (CanScrollHorizontally && localPoint.Y >= Bounds.Height - effectiveHHitWidth)
        {
            return this;
        }

        // Otherwise check children in reverse order
        for (int i = Children.Count - 1; i >= 0; i--)
        {
            if (Children[i] is UIElement child)
            {
                var hit = child.HitTest(localPoint);
                if (hit != null) return hit;
            }
        }

        return this;
    }

    public override void OnPointerWheel(PointerWheelEventArgs e)
    {
        base.OnPointerWheel(e);

        // Horizontal wheel scroll (DeltaX or when vertical scroll is disabled / unavailable)
        if (e.DeltaX != 0 && CanScrollHorizontally)
        {
            float newOffsetX = Math.Clamp(ScrollOffsetX - e.DeltaX * 40f, 0, MaxScrollX);
            if (newOffsetX != ScrollOffsetX)
            {
                ScrollOffsetX = newOffsetX;
                e.Handled = true;
                return;
            }
        }

        // Vertical wheel scroll (DeltaY)
        if (e.DeltaY != 0)
        {
            if (CanScrollVertically)
            {
                float newOffsetY = Math.Clamp(ScrollOffsetY - e.DeltaY * 40f, 0, MaxScrollY);
                if (newOffsetY != ScrollOffsetY)
                {
                    ScrollOffsetY = newOffsetY;
                    e.Handled = true;
                    return;
                }
            }
            else if (CanScrollHorizontally)
            {
                // Fallback to horizontal scroll if vertical is not scrollable
                float newOffsetX = Math.Clamp(ScrollOffsetX - e.DeltaY * 40f, 0, MaxScrollX);
                if (newOffsetX != ScrollOffsetX)
                {
                    ScrollOffsetX = newOffsetX;
                    e.Handled = true;
                    return;
                }
            }
        }
    }

    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_dragMode == DragMode.Vertical)
        {
            var track = GetVerticalTrackRect();
            float viewportRatio = Math.Clamp(Viewport.Height / Extent.Height, 0.05f, 1.0f);
            float thumbHeight = Math.Max(24f, track.Height * viewportRatio);
            float availableTravel = track.Height - thumbHeight;

            if (availableTravel > 0 && MaxScrollY > 0)
            {
                float deltaY = e.Position.Y - _dragStartMousePos;
                float scrollDelta = (deltaY / availableTravel) * MaxScrollY;
                ScrollOffsetY = Math.Clamp(_dragStartScrollOffset + scrollDelta, 0, MaxScrollY);
            }
            e.Handled = true;
            return;
        }

        if (_dragMode == DragMode.Horizontal)
        {
            var track = GetHorizontalTrackRect();
            float viewportRatio = Math.Clamp(Viewport.Width / Extent.Width, 0.05f, 1.0f);
            float thumbWidth = Math.Max(24f, track.Width * viewportRatio);
            float availableTravel = track.Width - thumbWidth;

            if (availableTravel > 0 && MaxScrollX > 0)
            {
                float deltaX = e.Position.X - _dragStartMousePos;
                float scrollDelta = (deltaX / availableTravel) * MaxScrollX;
                ScrollOffsetX = Math.Clamp(_dragStartScrollOffset + scrollDelta, 0, MaxScrollX);
            }
            e.Handled = true;
            return;
        }

        // Hover tracking
        bool wasVHovered = IsVerticalScrollBarHovered;
        bool wasHHovered = IsHorizontalScrollBarHovered;

        IsVerticalScrollBarHovered = CanScrollVertically &&
            e.Position.X >= Bounds.Width - ScrollBarHitWidth &&
            e.Position.Y <= Bounds.Height - (CanScrollHorizontally ? ScrollBarHitWidth : 0);

        IsHorizontalScrollBarHovered = CanScrollHorizontally &&
            e.Position.Y >= Bounds.Height - ScrollBarHitWidth &&
            e.Position.X <= Bounds.Width - (CanScrollVertically ? ScrollBarHitWidth : 0);

        if (wasVHovered != IsVerticalScrollBarHovered)
        {
            float targetW = (IsVerticalScrollBarHovered || IsVerticalThumbDragging)
                ? HoveredScrollBarWidth
                : NormalScrollBarWidth;
            AnimateVerticalScrollBarWidth(targetW);
        }

        if (wasHHovered != IsHorizontalScrollBarHovered)
        {
            float targetW = (IsHorizontalScrollBarHovered || IsHorizontalThumbDragging)
                ? HoveredScrollBarWidth
                : NormalScrollBarWidth;
            AnimateHorizontalScrollBarWidth(targetW);
        }
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.Button == PointerButtons.Left)
        {
            // Vertical scrollbar interaction
            if (CanScrollVertically)
            {
                var vThumb = GetVerticalThumbRect();
                var vTrack = GetVerticalTrackRect();
                float hitW = Math.Max(ScrollBarHitWidth, vTrack.Width + ScrollBarMargin * 2);
                var vThumbHit = new Rect(Bounds.Width - hitW, vThumb.Y, hitW, vThumb.Height);

                if (vThumbHit.Contains(e.Position))
                {
                    CapturePointer();
                    _dragMode = DragMode.Vertical;
                    _dragStartMousePos = e.Position.Y;
                    _dragStartScrollOffset = ScrollOffsetY;
                    AnimateVerticalScrollBarWidth(HoveredScrollBarWidth);
                    e.Handled = true;
                    InvalidateVisual();
                    return;
                }
                else if (vTrack.Contains(e.Position) ||
                    (e.Position.X >= Bounds.Width - hitW &&
                     e.Position.Y <= Bounds.Height - (CanScrollHorizontally ? hitW : 0)))
                {
                    // Vertical Page jump
                    if (e.Position.Y < vThumb.Y)
                    {
                        ScrollOffsetY = Math.Max(0, ScrollOffsetY - Viewport.Height * 0.8f);
                    }
                    else
                    {
                        ScrollOffsetY = Math.Min(MaxScrollY, ScrollOffsetY + Viewport.Height * 0.8f);
                    }
                    AnimateVerticalScrollBarWidth(HoveredScrollBarWidth);
                    e.Handled = true;
                    InvalidateVisual();
                    return;
                }
            }

            // Horizontal scrollbar interaction
            if (CanScrollHorizontally)
            {
                var hThumb = GetHorizontalThumbRect();
                var hTrack = GetHorizontalTrackRect();
                float hitH = Math.Max(ScrollBarHitWidth, hTrack.Height + ScrollBarMargin * 2);
                var hThumbHit = new Rect(hThumb.X, Bounds.Height - hitH, hThumb.Width, hitH);

                if (hThumbHit.Contains(e.Position))
                {
                    CapturePointer();
                    _dragMode = DragMode.Horizontal;
                    _dragStartMousePos = e.Position.X;
                    _dragStartScrollOffset = ScrollOffsetX;
                    AnimateHorizontalScrollBarWidth(HoveredScrollBarWidth);
                    e.Handled = true;
                    InvalidateVisual();
                    return;
                }
                else if (hTrack.Contains(e.Position) ||
                    (e.Position.Y >= Bounds.Height - hitH &&
                     e.Position.X <= Bounds.Width - (CanScrollVertically ? hitH : 0)))
                {
                    // Horizontal Page jump
                    if (e.Position.X < hThumb.X)
                    {
                        ScrollOffsetX = Math.Max(0, ScrollOffsetX - Viewport.Width * 0.8f);
                    }
                    else
                    {
                        ScrollOffsetX = Math.Min(MaxScrollX, ScrollOffsetX + Viewport.Width * 0.8f);
                    }
                    AnimateHorizontalScrollBarWidth(HoveredScrollBarWidth);
                    e.Handled = true;
                    InvalidateVisual();
                    return;
                }
            }
        }
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragMode != DragMode.None)
        {
            var oldDrag = _dragMode;
            _dragMode = DragMode.None;
            ReleasePointerCapture();
            e.Handled = true;

            if (oldDrag == DragMode.Vertical && !IsVerticalScrollBarHovered)
            {
                AnimateVerticalScrollBarWidth(NormalScrollBarWidth);
            }
            else if (oldDrag == DragMode.Horizontal && !IsHorizontalScrollBarHovered)
            {
                AnimateHorizontalScrollBarWidth(NormalScrollBarWidth);
            }

            InvalidateVisual();
        }
    }

    public override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (_dragMode == DragMode.None)
        {
            if (IsVerticalScrollBarHovered)
            {
                IsVerticalScrollBarHovered = false;
                AnimateVerticalScrollBarWidth(NormalScrollBarWidth);
            }
            if (IsHorizontalScrollBarHovered)
            {
                IsHorizontalScrollBarHovered = false;
                AnimateHorizontalScrollBarWidth(NormalScrollBarWidth);
            }
            InvalidateVisual();
        }
    }

    internal void UpdateChildArrange()
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        float effectiveScrollX = Math.Clamp(ScrollOffsetX, 0, MaxScrollX);
        float effectiveScrollY = Math.Clamp(ScrollOffsetY, 0, MaxScrollY);

        if (Children.Count > 0 && Children[0] is UIElement child && child.Visibility != Visibility.Collapsed)
        {
            float childWidth = HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled
                ? Math.Max(Bounds.Width, Extent.Width)
                : Bounds.Width;

            float childHeight = VerticalScrollBarVisibility != ScrollBarVisibility.Disabled
                ? Math.Max(Bounds.Height, Extent.Height)
                : Bounds.Height;

            child.InvalidateArrange();
            child.Arrange(new Rect(-effectiveScrollX, -effectiveScrollY, childWidth, childHeight));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Viewport = availableSize;

        if (Children.Count > 0 && Children[0] is UIElement child && child.Visibility != Visibility.Collapsed)
        {
            float measureW = HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled
                ? float.PositiveInfinity
                : availableSize.Width;

            float measureH = VerticalScrollBarVisibility != ScrollBarVisibility.Disabled
                ? float.PositiveInfinity
                : availableSize.Height;

            child.Measure(new Size(measureW, measureH));
            Extent = new Size(
                HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled ? child.DesiredSize.Width : Math.Min(availableSize.Width, child.DesiredSize.Width),
                VerticalScrollBarVisibility != ScrollBarVisibility.Disabled ? child.DesiredSize.Height : Math.Min(availableSize.Height, child.DesiredSize.Height)
            );

            return new Size(
                Math.Min(availableSize.Width, child.DesiredSize.Width),
                Math.Min(availableSize.Height, child.DesiredSize.Height)
            );
        }

        Extent = Size.Zero;
        return Size.Zero;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Viewport = finalSize;
        float effectiveScrollX = Math.Clamp(ScrollOffsetX, 0, MaxScrollX);
        float effectiveScrollY = Math.Clamp(ScrollOffsetY, 0, MaxScrollY);

        if (Children.Count > 0 && Children[0] is UIElement child && child.Visibility != Visibility.Collapsed)
        {
            float childWidth = HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled
                ? Math.Max(finalSize.Width, Extent.Width)
                : finalSize.Width;

            float childHeight = VerticalScrollBarVisibility != ScrollBarVisibility.Disabled
                ? Math.Max(finalSize.Height, Extent.Height)
                : finalSize.Height;

            child.InvalidateArrange();
            child.Arrange(new Rect(-effectiveScrollX, -effectiveScrollY, childWidth, childHeight));
        }

        return finalSize;
    }
}
