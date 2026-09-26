using System;
using Atelier.Core.Animation;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// Specifies whether a <see cref="ScrollViewer"/> scrolls along an axis and whether it shows a scroll bar for it.
/// </summary>
public enum ScrollBarVisibility
{
    /// <summary>No scrolling: the content is measured with the viewport size along this axis and no bar is shown.</summary>
    Disabled,
    /// <summary>Scrolls when the content is larger than the viewport; the bar is shown only then.</summary>
    Auto,
    /// <summary>Scrolls (wheel, keyboard and code) when the content is larger than the viewport, but never shows a bar.</summary>
    Hidden,
    /// <summary>Scrolls when the content is larger than the viewport; the bar is always shown.</summary>
    Visible
}

/// <summary>
/// Provides data for <see cref="ScrollViewer.ScrollChanged"/>: the new scroll state and the change since the previous
/// event (changes are negative when a value decreased).
/// </summary>
public sealed class ScrollChangedEventArgs : EventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ScrollChangedEventArgs"/> class.</summary>
    /// <param name="offset">The new offsets (X horizontal, Y vertical).</param>
    /// <param name="previousOffset">The offsets reported by the previous event.</param>
    /// <param name="extent">The new content size.</param>
    /// <param name="previousExtent">The extent reported by the previous event.</param>
    /// <param name="viewport">The new viewport size.</param>
    /// <param name="previousViewport">The viewport reported by the previous event.</param>
    public ScrollChangedEventArgs(Point offset, Point previousOffset, Size extent, Size previousExtent, Size viewport, Size previousViewport)
    {
        HorizontalOffset = offset.X;
        VerticalOffset = offset.Y;
        HorizontalChange = offset.X - previousOffset.X;
        VerticalChange = offset.Y - previousOffset.Y;
        ExtentWidth = extent.Width;
        ExtentHeight = extent.Height;
        ExtentWidthChange = extent.Width - previousExtent.Width;
        ExtentHeightChange = extent.Height - previousExtent.Height;
        ViewportWidth = viewport.Width;
        ViewportHeight = viewport.Height;
        ViewportWidthChange = viewport.Width - previousViewport.Width;
        ViewportHeightChange = viewport.Height - previousViewport.Height;
    }

    /// <summary>Gets the new <see cref="ScrollViewer.ScrollOffsetX"/>.</summary>
    public float HorizontalOffset { get; }
    /// <summary>Gets the new <see cref="ScrollViewer.ScrollOffsetY"/>.</summary>
    public float VerticalOffset { get; }
    /// <summary>Gets the change of the horizontal offset.</summary>
    public float HorizontalChange { get; }
    /// <summary>Gets the change of the vertical offset.</summary>
    public float VerticalChange { get; }
    /// <summary>Gets the new extent width.</summary>
    public float ExtentWidth { get; }
    /// <summary>Gets the new extent height.</summary>
    public float ExtentHeight { get; }
    /// <summary>Gets the change of the extent width.</summary>
    public float ExtentWidthChange { get; }
    /// <summary>Gets the change of the extent height.</summary>
    public float ExtentHeightChange { get; }
    /// <summary>Gets the new viewport width.</summary>
    public float ViewportWidth { get; }
    /// <summary>Gets the new viewport height.</summary>
    public float ViewportHeight { get; }
    /// <summary>Gets the change of the viewport width.</summary>
    public float ViewportWidthChange { get; }
    /// <summary>Gets the change of the viewport height.</summary>
    public float ViewportHeightChange { get; }
}

/// <summary>
/// Shows its <see cref="ContentControl.Content"/> in a clipped viewport that can be scrolled with the wheel, the keyboard,
/// the overlay scroll bars or code.
/// </summary>
/// <remarks>
/// <para>
/// Along an axis whose visibility is not <see cref="ScrollBarVisibility.Disabled"/>, the content is measured with infinite
/// space and its desired size becomes the <see cref="Extent"/>. <see cref="ScrollOffsetX"/> and
/// <see cref="ScrollOffsetY"/> are kept within 0..<see cref="MaxScrollX"/>/<see cref="MaxScrollY"/>: values set while
/// layout is valid are coerced immediately; values set while layout is pending (for example right after adding content)
/// are applied and clamped at the next arrange, so <c>ScrollToBottom()</c> after adding items scrolls to the new end. When
/// the content shrinks, the offsets are reduced and stay reduced if it grows again.
/// </para>
/// <para>
/// Keyboard: when a key bubbles up unhandled from a focused descendant (or the viewer itself is focused, see
/// <see cref="UIElement.IsFocusable"/>, default <c>false</c>), arrows scroll by a line, PageUp/PageDown by a viewport and
/// Home/End to the start/end. Keys that don't move the content are left unhandled so outer viewers can scroll.
/// </para>
/// </remarks>
public class ScrollViewer : ContentControl
{
    /// <summary>Identifies the <see cref="ScrollOffsetX"/> bindable property.</summary>
    public static readonly BindableProperty<float> ScrollOffsetXProperty =
        BindableProperty.Register<ScrollViewer, float>(
            nameof(ScrollOffsetX),
            0f,
            (s, o, n) => ((ScrollViewer)s).OnScrollOffsetChanged(),
            coerceValue: (s, v) => ((ScrollViewer)s).CoerceOffset(v, ((ScrollViewer)s).MaxScrollX)
        );

    /// <summary>Identifies the <see cref="ScrollOffsetY"/> bindable property.</summary>
    public static readonly BindableProperty<float> ScrollOffsetYProperty =
        BindableProperty.Register<ScrollViewer, float>(
            nameof(ScrollOffsetY),
            0f,
            (s, o, n) => ((ScrollViewer)s).OnScrollOffsetChanged(),
            coerceValue: (s, v) => ((ScrollViewer)s).CoerceOffset(v, ((ScrollViewer)s).MaxScrollY)
        );

    /// <summary>Identifies the <see cref="HorizontalScrollBarVisibility"/> bindable property.</summary>
    public static readonly BindableProperty<ScrollBarVisibility> HorizontalScrollBarVisibilityProperty =
        BindableProperty.Register<ScrollViewer, ScrollBarVisibility>(
            nameof(HorizontalScrollBarVisibility),
            ScrollBarVisibility.Disabled,
            options: PropertyOptions.AffectsMeasure
        );

    /// <summary>Identifies the <see cref="VerticalScrollBarVisibility"/> bindable property.</summary>
    public static readonly BindableProperty<ScrollBarVisibility> VerticalScrollBarVisibilityProperty =
        BindableProperty.Register<ScrollViewer, ScrollBarVisibility>(
            nameof(VerticalScrollBarVisibility),
            ScrollBarVisibility.Auto,
            options: PropertyOptions.AffectsMeasure
        );

    /// <summary>
    /// Gets or sets the horizontal scroll offset in pixels, kept within 0..<see cref="MaxScrollX"/>. Default 0.
    /// </summary>
    public float ScrollOffsetX
    {
        get => GetValue(ScrollOffsetXProperty);
        set => SetValue(ScrollOffsetXProperty, value);
    }

    /// <summary>
    /// Gets or sets the vertical scroll offset in pixels, kept within 0..<see cref="MaxScrollY"/>. Default 0.
    /// </summary>
    public float ScrollOffsetY
    {
        get => GetValue(ScrollOffsetYProperty);
        set => SetValue(ScrollOffsetYProperty, value);
    }

    /// <summary>Gets or sets horizontal scrolling and bar behavior. Default <see cref="ScrollBarVisibility.Disabled"/>.</summary>
    public ScrollBarVisibility HorizontalScrollBarVisibility
    {
        get => GetValue(HorizontalScrollBarVisibilityProperty);
        set => SetValue(HorizontalScrollBarVisibilityProperty, value);
    }

    /// <summary>Gets or sets vertical scrolling and bar behavior. Default <see cref="ScrollBarVisibility.Auto"/>.</summary>
    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => GetValue(VerticalScrollBarVisibilityProperty);
        set => SetValue(VerticalScrollBarVisibilityProperty, value);
    }

    /// <summary>Gets the size of the content, as measured in the last layout pass.</summary>
    public Size Extent { get; private set; } = Size.Zero;

    /// <summary>Gets the size of the visible area, as of the last layout pass.</summary>
    public Size Viewport { get; private set; } = Size.Zero;

    /// <summary>Gets the largest horizontal offset: extent minus viewport width, or 0 when horizontal scrolling is disabled.</summary>
    public float MaxScrollX => HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled
        ? 0f
        : Math.Max(0, Extent.Width - Viewport.Width);

    /// <summary>Gets the largest vertical offset: extent minus viewport height, or 0 when vertical scrolling is disabled.</summary>
    public float MaxScrollY => VerticalScrollBarVisibility == ScrollBarVisibility.Disabled
        ? 0f
        : Math.Max(0, Extent.Height - Viewport.Height);

    /// <summary>
    /// Gets whether the content can currently scroll horizontally: scrolling isn't disabled (a hidden bar still scrolls)
    /// and the content is wider than the viewport.
    /// </summary>
    public bool CanScrollHorizontally => HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled && MaxScrollX > 0;

    /// <summary>
    /// Gets whether the content can currently scroll vertically: scrolling isn't disabled (a hidden bar still scrolls)
    /// and the content is taller than the viewport.
    /// </summary>
    public bool CanScrollVertically => VerticalScrollBarVisibility != ScrollBarVisibility.Disabled && MaxScrollY > 0;

    /// <summary>
    /// Gets whether the horizontal bar is shown: always for <see cref="ScrollBarVisibility.Visible"/>, and for
    /// <see cref="ScrollBarVisibility.Auto"/> when the content is wider than the viewport.
    /// </summary>
    public bool IsHorizontalScrollBarVisible =>
        HorizontalScrollBarVisibility == ScrollBarVisibility.Visible ||
        (HorizontalScrollBarVisibility == ScrollBarVisibility.Auto && MaxScrollX > 0);

    /// <summary>
    /// Gets whether the vertical bar is shown: always for <see cref="ScrollBarVisibility.Visible"/>, and for
    /// <see cref="ScrollBarVisibility.Auto"/> when the content is taller than the viewport.
    /// </summary>
    public bool IsVerticalScrollBarVisible =>
        VerticalScrollBarVisibility == ScrollBarVisibility.Visible ||
        (VerticalScrollBarVisibility == ScrollBarVisibility.Auto && MaxScrollY > 0);

    /// <summary>Gets whether the pointer is over the vertical bar's hit zone.</summary>
    public bool IsVerticalScrollBarHovered { get; internal set; }

    /// <summary>Gets whether the pointer is over the horizontal bar's hit zone.</summary>
    public bool IsHorizontalScrollBarHovered { get; internal set; }

    /// <summary>Gets whether the pointer is over either bar.</summary>
    public bool IsScrollBarHovered => IsVerticalScrollBarHovered || IsHorizontalScrollBarHovered;

    private enum DragMode { None, Vertical, Horizontal }
    private DragMode _dragMode = DragMode.None;

    /// <summary>Gets whether the vertical thumb is being dragged.</summary>
    public bool IsVerticalThumbDragging => _dragMode == DragMode.Vertical;

    /// <summary>Gets whether the horizontal thumb is being dragged.</summary>
    public bool IsHorizontalThumbDragging => _dragMode == DragMode.Horizontal;

    /// <summary>Gets whether either thumb is being dragged.</summary>
    public bool IsThumbDragging => _dragMode != DragMode.None;

    private static AnimationClock? _clock;

    /// <summary>
    /// Sets the clock that animates the bar width on hover for all scroll viewers; <c>null</c> (default) changes it instantly.
    /// </summary>
    /// <param name="clock">The animation clock, or <c>null</c>.</param>
    public static void SetGlobalAnimationClock(AnimationClock? clock) => _clock = clock;

    /// <summary>The bar thickness when not hovered.</summary>
    public const float NormalScrollBarWidth = 6f;

    /// <summary>The bar thickness while hovered or dragged.</summary>
    public const float HoveredScrollBarWidth = 10f;

    /// <summary>The distance scrolled by <see cref="LineUp"/>, <see cref="LineDown"/>, <see cref="LineLeft"/>, <see cref="LineRight"/> and the arrow keys.</summary>
    public const float LineScrollAmount = 16f;

    /// <summary>The distance scrolled per wheel notch.</summary>
    public const float WheelScrollAmount = 40f;

    /// <summary>Gets the current (possibly animating) thickness of the vertical bar.</summary>
    public float CurrentVerticalScrollBarWidth { get; private set; } = NormalScrollBarWidth;

    /// <summary>Gets the current (possibly animating) thickness of the horizontal bar.</summary>
    public float CurrentHorizontalScrollBarWidth { get; private set; } = NormalScrollBarWidth;

    /// <summary>Gets the current thickness of the vertical bar; same as <see cref="CurrentVerticalScrollBarWidth"/>.</summary>
    public float ScrollBarWidth => CurrentVerticalScrollBarWidth;

    /// <summary>Gets the gap between a bar and the viewer's edge.</summary>
    public float ScrollBarMargin => 2f;

    /// <summary>Gets the thickness of the pointer hit zone along each bar.</summary>
    public float ScrollBarHitWidth => 28f;

    /// <summary>
    /// Occurs when the scroll offsets, the extent or the viewport change. Raised after the offset is applied, or at the
    /// end of the arrange pass for layout-driven changes.
    /// </summary>
    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;

    private FloatAnimation? _vWidthAnim;
    private FloatAnimation? _hWidthAnim;

    private float _dragStartMousePos;
    private float _dragStartScrollOffset;

    private bool _isArranging;
    private bool _hasLayout;
    private Point _notifiedOffset;
    private Size _notifiedExtent;
    private Size _notifiedViewport;

    static ScrollViewer()
    {
        ClipToBoundsProperty.OverrideDefaultValue<ScrollViewer>(true);
    }

    /// <summary>Initializes a new, empty <see cref="ScrollViewer"/>.</summary>
    public ScrollViewer()
    {
    }

    #region Scrolling API

    /// <summary>Scrolls up by <see cref="LineScrollAmount"/>.</summary>
    public void LineUp() => ScrollToVerticalOffset(ScrollOffsetY - LineScrollAmount);

    /// <summary>Scrolls down by <see cref="LineScrollAmount"/>.</summary>
    public void LineDown() => ScrollToVerticalOffset(ScrollOffsetY + LineScrollAmount);

    /// <summary>Scrolls left by <see cref="LineScrollAmount"/>.</summary>
    public void LineLeft() => ScrollToHorizontalOffset(ScrollOffsetX - LineScrollAmount);

    /// <summary>Scrolls right by <see cref="LineScrollAmount"/>.</summary>
    public void LineRight() => ScrollToHorizontalOffset(ScrollOffsetX + LineScrollAmount);

    /// <summary>Scrolls up by one viewport height.</summary>
    public void PageUp() => ScrollToVerticalOffset(ScrollOffsetY - Viewport.Height);

    /// <summary>Scrolls down by one viewport height.</summary>
    public void PageDown() => ScrollToVerticalOffset(ScrollOffsetY + Viewport.Height);

    /// <summary>Scrolls left by one viewport width.</summary>
    public void PageLeft() => ScrollToHorizontalOffset(ScrollOffsetX - Viewport.Width);

    /// <summary>Scrolls right by one viewport width.</summary>
    public void PageRight() => ScrollToHorizontalOffset(ScrollOffsetX + Viewport.Width);

    /// <summary>Scrolls to the top edge.</summary>
    public void ScrollToTop() => ScrollToVerticalOffset(0f);

    /// <summary>Scrolls to the bottom edge (applied at the next arrange if layout is pending).</summary>
    public void ScrollToBottom() => ScrollToVerticalOffset(float.PositiveInfinity);

    /// <summary>Scrolls to the left edge.</summary>
    public void ScrollToLeftEnd() => ScrollToHorizontalOffset(0f);

    /// <summary>Scrolls to the right edge (applied at the next arrange if layout is pending).</summary>
    public void ScrollToRightEnd() => ScrollToHorizontalOffset(float.PositiveInfinity);

    /// <summary>Scrolls to the top-left corner.</summary>
    public void ScrollToHome()
    {
        ScrollToHorizontalOffset(0f);
        ScrollToVerticalOffset(0f);
    }

    /// <summary>Scrolls to the bottom edge and the left edge.</summary>
    public void ScrollToEnd()
    {
        ScrollToHorizontalOffset(0f);
        ScrollToVerticalOffset(float.PositiveInfinity);
    }

    /// <summary>Sets <see cref="ScrollOffsetY"/>; the value is clamped to the scrollable range.</summary>
    /// <param name="offset">The requested offset; <see cref="float.PositiveInfinity"/> means the end.</param>
    public void ScrollToVerticalOffset(float offset) => ScrollOffsetY = offset;

    /// <summary>Sets <see cref="ScrollOffsetX"/>; the value is clamped to the scrollable range.</summary>
    /// <param name="offset">The requested offset; <see cref="float.PositiveInfinity"/> means the end.</param>
    public void ScrollToHorizontalOffset(float offset) => ScrollOffsetX = offset;

    private bool IsScrollRangeKnown => _hasLayout && IsMeasureValid && IsArrangeValid;

    private float CoerceOffset(float value, float max)
    {
        if (float.IsNaN(value) || value < 0f) return 0f;
        // While layout is pending the range may be stale; ArrangeOverride clamps then.
        return IsScrollRangeKnown ? Math.Min(value, max) : value;
    }

    private void OnScrollOffsetChanged()
    {
        if (_isArranging)
        {
            return; // ArrangeOverride positions the content and raises ScrollChanged itself.
        }

        UpdateChildArrange();
        InvalidateVisual();
        RaiseScrollChangedIfNeeded();
    }

    private void RaiseScrollChangedIfNeeded()
    {
        var offset = new Point(ScrollOffsetX, ScrollOffsetY);
        var extent = Extent;
        var viewport = Viewport;
        if (offset == _notifiedOffset && extent == _notifiedExtent && viewport == _notifiedViewport)
        {
            return;
        }

        var oldOffset = _notifiedOffset;
        var oldExtent = _notifiedExtent;
        var oldViewport = _notifiedViewport;
        _notifiedOffset = offset;
        _notifiedExtent = extent;
        _notifiedViewport = viewport;

        // The args are only allocated when someone listens.
        ScrollChanged?.Invoke(this, new ScrollChangedEventArgs(offset, oldOffset, extent, oldExtent, viewport, oldViewport));
    }

    #endregion

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

        _vWidthAnim?.Stop();
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

        _hWidthAnim?.Stop();
        _hWidthAnim = anim;
        _clock.Add(anim);
    }

    /// <summary>Returns the vertical bar's track rectangle in local coordinates.</summary>
    public Rect GetVerticalTrackRect()
    {
        float w = Bounds.Width;
        float h = Bounds.Height;
        float currentW = CurrentVerticalScrollBarWidth;
        float bottomReserved = IsHorizontalScrollBarVisible ? CurrentHorizontalScrollBarWidth + ScrollBarMargin * 2 : ScrollBarMargin;
        return new Rect(
            w - currentW - ScrollBarMargin,
            ScrollBarMargin,
            currentW,
            Math.Max(0, h - ScrollBarMargin - bottomReserved)
        );
    }

    /// <summary>
    /// Returns the vertical thumb rectangle in local coordinates, or <see cref="Rect.Zero"/> when there is nothing to scroll.
    /// </summary>
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

    /// <summary>Returns the horizontal bar's track rectangle in local coordinates.</summary>
    public Rect GetHorizontalTrackRect()
    {
        float w = Bounds.Width;
        float h = Bounds.Height;
        float currentH = CurrentHorizontalScrollBarWidth;
        float rightReserved = IsVerticalScrollBarVisible ? CurrentVerticalScrollBarWidth + ScrollBarMargin * 2 : ScrollBarMargin;
        return new Rect(
            ScrollBarMargin,
            h - currentH - ScrollBarMargin,
            Math.Max(0, w - ScrollBarMargin - rightReserved),
            currentH
        );
    }

    /// <summary>
    /// Returns the horizontal thumb rectangle in local coordinates, or <see cref="Rect.Zero"/> when there is nothing to scroll.
    /// </summary>
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

    /// <summary>Same as <see cref="GetVerticalTrackRect"/>; kept for compatibility.</summary>
    public Rect GetTrackRect() => GetVerticalTrackRect();

    /// <summary>Same as <see cref="GetVerticalThumbRect"/>; kept for compatibility.</summary>
    public Rect GetThumbRect() => GetVerticalThumbRect();

    /// <inheritdoc/>
    /// <remarks>Points over a visible bar's hit zone hit the viewer itself; otherwise the content is hit-tested.</remarks>
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

        if (IsVerticalScrollBarVisible && localPoint.X >= Bounds.Width - effectiveVHitWidth)
        {
            return this;
        }

        // Check horizontal scrollbar hit zone
        float effectiveHHitWidth = (IsHorizontalScrollBarHovered || IsHorizontalThumbDragging)
            ? ScrollBarHitWidth
            : (CurrentHorizontalScrollBarWidth + ScrollBarMargin * 2);

        if (IsHorizontalScrollBarVisible && localPoint.Y >= Bounds.Height - effectiveHHitWidth)
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

    /// <inheritdoc/>
    /// <remarks>
    /// Scrolls vertically by <see cref="WheelScrollAmount"/> per notch (horizontally when only horizontal scrolling is
    /// possible, or for a horizontal wheel). Unhandled when the content can't move further, so outer viewers scroll.
    /// </remarks>
    public override void OnPointerWheel(PointerWheelEventArgs e)
    {
        base.OnPointerWheel(e);
        if (e.Handled) return;

        if (e.DeltaX != 0 && CanScrollHorizontally && TryScrollTo(ScrollOffsetX - e.DeltaX * WheelScrollAmount, horizontal: true))
        {
            e.Handled = true;
            return;
        }

        if (e.DeltaY != 0)
        {
            if (CanScrollVertically)
            {
                if (TryScrollTo(ScrollOffsetY - e.DeltaY * WheelScrollAmount, horizontal: false))
                {
                    e.Handled = true;
                }
            }
            else if (CanScrollHorizontally && TryScrollTo(ScrollOffsetX - e.DeltaY * WheelScrollAmount, horizontal: true))
            {
                e.Handled = true;
            }
        }
    }

    private bool TryScrollTo(float offset, bool horizontal)
    {
        float max = horizontal ? MaxScrollX : MaxScrollY;
        float target = Math.Clamp(offset, 0, max);
        float current = horizontal ? ScrollOffsetX : ScrollOffsetY;
        if (target == current)
        {
            return false;
        }

        if (horizontal) ScrollOffsetX = target;
        else ScrollOffsetY = target;
        return true;
    }

    /// <inheritdoc/>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEnabled) return;

        bool handled = e.Key switch
        {
            Key.Up => CanScrollVertically && TryScrollTo(ScrollOffsetY - LineScrollAmount, horizontal: false),
            Key.Down => CanScrollVertically && TryScrollTo(ScrollOffsetY + LineScrollAmount, horizontal: false),
            Key.Left => CanScrollHorizontally && TryScrollTo(ScrollOffsetX - LineScrollAmount, horizontal: true),
            Key.Right => CanScrollHorizontally && TryScrollTo(ScrollOffsetX + LineScrollAmount, horizontal: true),
            Key.PageUp => CanScrollVertically && TryScrollTo(ScrollOffsetY - Viewport.Height, horizontal: false),
            Key.PageDown => CanScrollVertically && TryScrollTo(ScrollOffsetY + Viewport.Height, horizontal: false),
            Key.Home => CanScrollVertically
                ? TryScrollTo(0f, horizontal: false)
                : CanScrollHorizontally && TryScrollTo(0f, horizontal: true),
            Key.End => CanScrollVertically
                ? TryScrollTo(MaxScrollY, horizontal: false)
                : CanScrollHorizontally && TryScrollTo(MaxScrollX, horizontal: true),
            _ => false
        };

        if (handled)
        {
            e.Handled = true;
        }
    }

    /// <inheritdoc/>
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

        IsVerticalScrollBarHovered = IsVerticalScrollBarVisible &&
            e.Position.X >= Bounds.Width - ScrollBarHitWidth &&
            e.Position.Y <= Bounds.Height - (IsHorizontalScrollBarVisible ? ScrollBarHitWidth : 0);

        IsHorizontalScrollBarHovered = IsHorizontalScrollBarVisible &&
            e.Position.Y >= Bounds.Height - ScrollBarHitWidth &&
            e.Position.X <= Bounds.Width - (IsVerticalScrollBarVisible ? ScrollBarHitWidth : 0);

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

    /// <inheritdoc/>
    /// <remarks>
    /// A left press on a visible bar's thumb starts a drag (capturing the pointer); on its track it pages by 80% of the
    /// viewport.
    /// </remarks>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.Button == PointerButtons.Left)
        {
            // Vertical scrollbar interaction
            if (IsVerticalScrollBarVisible)
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
                     e.Position.Y <= Bounds.Height - (IsHorizontalScrollBarVisible ? hitW : 0)))
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
            if (IsHorizontalScrollBarVisible)
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
                     e.Position.X <= Bounds.Width - (IsVerticalScrollBarVisible ? hitH : 0)))
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

    /// <inheritdoc/>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragMode != DragMode.None)
        {
            EndThumbDrag();
            e.Handled = true;
        }
    }

    /// <inheritdoc/>
    /// <remarks>Ends a thumb drag if capture is taken away (for example by another element or a window deactivation).</remarks>
    protected override void OnLostPointerCapture()
    {
        base.OnLostPointerCapture();
        EndThumbDrag();
    }

    private void EndThumbDrag()
    {
        if (_dragMode == DragMode.None)
        {
            return;
        }

        var oldDrag = _dragMode;
        _dragMode = DragMode.None;
        ReleasePointerCapture();

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

    /// <inheritdoc/>
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

    // Repositions the content for the current offsets without a full layout pass: only the content's own bounds move,
    // its subtree keeps its (relative) arrangement.
    internal void UpdateChildArrange()
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;

        if (Children.Count > 0 && Children[0] is UIElement child && child.Visibility != Visibility.Collapsed)
        {
            child.Arrange(GetContentRect(Bounds.Width, Bounds.Height));
        }
    }

    private Rect GetContentRect(float width, float height)
    {
        float offsetX = Math.Clamp(ScrollOffsetX, 0, MaxScrollX);
        float offsetY = Math.Clamp(ScrollOffsetY, 0, MaxScrollY);

        float childWidth = HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled
            ? Math.Max(width, Extent.Width)
            : width;

        float childHeight = VerticalScrollBarVisibility != ScrollBarVisibility.Disabled
            ? Math.Max(height, Extent.Height)
            : height;

        return new Rect(-offsetX, -offsetY, childWidth, childHeight);
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    /// <remarks>Clamps the offsets to the new scrollable range, positions the content and raises <see cref="ScrollChanged"/>.</remarks>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _isArranging = true;
        try
        {
            Viewport = finalSize;
            _hasLayout = true;

            // Store the clamped offsets so a later growth of the content doesn't jump back to a stale offset.
            float offsetX = Math.Clamp(ScrollOffsetX, 0, MaxScrollX);
            float offsetY = Math.Clamp(ScrollOffsetY, 0, MaxScrollY);
            if (offsetX != ScrollOffsetX) ScrollOffsetX = offsetX;
            if (offsetY != ScrollOffsetY) ScrollOffsetY = offsetY;

            if (Children.Count > 0 && Children[0] is UIElement child && child.Visibility != Visibility.Collapsed)
            {
                child.Arrange(GetContentRect(finalSize.Width, finalSize.Height));
            }
        }
        finally
        {
            _isArranging = false;
        }

        RaiseScrollChangedIfNeeded();
        return finalSize;
    }
}
