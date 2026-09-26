using System;
using Atelier.Core.Animation;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Threading;

namespace Atelier.Controls;

/// <summary>
/// The Material Design 3 emphasis levels of a <see cref="Button"/>, from highest to lowest.
/// </summary>
public enum ButtonVariant
{
    /// <summary>A solid primary-colored container, for the most important action.</summary>
    Filled,

    /// <summary>A tinted surface container with a shadow.</summary>
    Elevated,

    /// <summary>A secondary-container fill, between filled and outlined in emphasis.</summary>
    Tonal,

    /// <summary>A transparent container with an outline.</summary>
    Outlined,

    /// <summary>Text only, for the lowest-emphasis actions.</summary>
    Text
}

/// <summary>
/// A push button that raises <see cref="ButtonBase.Click"/> and executes its <see cref="ButtonBase.Command"/>, drawn in
/// one of the Material Design 3 <see cref="ButtonVariant"/>s with an ink ripple on press.
/// </summary>
/// <remarks>
/// The default <see cref="Control.Padding"/> is 16×6 and the default <see cref="Control.CornerRadius"/> is 20 (a pill).
/// The ripple is only shown while an animation clock is set (see <see cref="ButtonBase.SetGlobalAnimationClock"/>).
/// </remarks>
public class Button : ButtonBase
{
    /// <summary>Identifies the <see cref="Variant"/> property.</summary>
    public static readonly BindableProperty<ButtonVariant> VariantProperty =
        BindableProperty.Register<Button, ButtonVariant>(nameof(Variant), ButtonVariant.Filled, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="Elevation"/> property.</summary>
    public static readonly BindableProperty<float> ElevationProperty =
        BindableProperty.Register<Button, float>(nameof(Elevation), 1f, options: PropertyOptions.AffectsRender);

    /// <summary>Gets or sets the visual style. The default is <see cref="ButtonVariant.Filled"/>.</summary>
    public ButtonVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }

    /// <summary>
    /// Gets or sets the resting shadow elevation of the <see cref="ButtonVariant.Filled"/> and
    /// <see cref="ButtonVariant.Elevated"/> variants; hover and press raise it. The default is 1.
    /// </summary>
    public float Elevation { get => GetValue(ElevationProperty); set => SetValue(ElevationProperty, value); }

    /// <summary>Gets the center of the current ink ripple, in local coordinates.</summary>
    public Point RippleCenter { get; private set; } = Point.Zero;

    /// <summary>Gets how far the ripple has expanded, from 0 to 1 (1 covers the whole button).</summary>
    public float RippleProgress { get; private set; }

    /// <summary>Gets the ripple's opacity; 0 when no ripple is shown.</summary>
    public float RippleOpacity { get; private set; }

    /// <summary>Gets whether a ripple is currently visible.</summary>
    public bool HasActiveRipple => RippleOpacity > 0f;

    private const float RippleStartOpacity = 0.25f;

    private FloatAnimation? _rippleExpand;
    private FloatAnimation? _rippleFade;
    private Action<float>? _setRippleProgress;
    private Action<float>? _setRippleOpacity;

    static Button()
    {
        PaddingProperty.OverrideDefaultValue<Button>(new Thickness(16, 6));
        CornerRadiusProperty.OverrideDefaultValue<Button>(new CornerRadius(20)); // MD3 pill shape default
    }

    /// <summary>Initializes a new, empty button.</summary>
    public Button()
    {
    }

    /// <summary>Initializes a new button showing <paramref name="text"/>.</summary>
    public Button(string text) : this()
    {
        Content = new TextBlock(text) { VerticalAlignment = VerticalAlignment.Center };
    }

    /// <inheritdoc/>
    /// <remarks>Starts a new ink ripple at <paramref name="position"/>, replacing any ripple still shown.</remarks>
    protected override void OnPressStarted(Point position)
    {
        base.OnPressStarted(position);
        _rippleFade?.Stop();
        _rippleFade = null;

        if (AnimationClock == null)
        {
            // Without a clock nothing would fade the ripple out again.
            return;
        }

        RippleCenter = position;
        RippleProgress = 0f;
        RippleOpacity = RippleStartOpacity;
        InvalidateVisual();

        StartAnimation(ref _rippleExpand, new FloatAnimation(
            from: 0f,
            to: 1f,
            duration: TimeSpan.FromMilliseconds(350),
            onUpdate: _setRippleProgress ??= p =>
            {
                RippleProgress = p;
                InvalidateVisual();
            },
            easing: Easing.EaseOutCubic));
    }

    /// <inheritdoc/>
    /// <remarks>Fades the ripple out.</remarks>
    protected override void OnPressEnded()
    {
        base.OnPressEnded();
        if (RippleOpacity <= 0f || AnimationClock == null)
        {
            return;
        }

        StartAnimation(ref _rippleFade, new FloatAnimation(
            from: RippleOpacity,
            to: 0f,
            duration: TimeSpan.FromMilliseconds(200),
            onUpdate: _setRippleOpacity ??= o =>
            {
                RippleOpacity = o;
                InvalidateVisual();
            },
            easing: Easing.Linear));
    }
}

/// <summary>
/// A <see cref="Button"/> that raises <see cref="ButtonBase.Click"/> repeatedly while it is held down, like the arrows
/// of a scroll bar or a numeric up/down control.
/// </summary>
/// <remarks>
/// The first click happens on press (<see cref="ButtonBase.ClickMode"/> defaults to <see cref="ClickMode.Press"/>),
/// the next one after <see cref="Delay"/>, and then one every <see cref="Interval"/> until the left pointer button or
/// Space is released, the pointer leaves the button, or the button is disabled, loses focus or is removed.
/// </remarks>
public class RepeatButton : Button
{
    /// <summary>Identifies the <see cref="Delay"/> property.</summary>
    public static readonly BindableProperty<TimeSpan> DelayProperty =
        BindableProperty.Register<RepeatButton, TimeSpan>(nameof(Delay), TimeSpan.FromMilliseconds(500), validateValue: IsPositive);

    /// <summary>Identifies the <see cref="Interval"/> property.</summary>
    public static readonly BindableProperty<TimeSpan> IntervalProperty =
        BindableProperty.Register<RepeatButton, TimeSpan>(nameof(Interval), TimeSpan.FromMilliseconds(33), validateValue: IsPositive);

    /// <summary>Gets or sets the time between the press and the first repeated click. The default is 500 ms.</summary>
    public TimeSpan Delay { get => GetValue(DelayProperty); set => SetValue(DelayProperty, value); }

    /// <summary>Gets or sets the time between repeated clicks after <see cref="Delay"/>. The default is 33 ms.</summary>
    public TimeSpan Interval { get => GetValue(IntervalProperty); set => SetValue(IntervalProperty, value); }

    private DispatcherTimer? _timer;

    static RepeatButton()
    {
        ClickModeProperty.OverrideDefaultValue<RepeatButton>(ClickMode.Press);
    }

    /// <summary>Initializes a new, empty repeat button.</summary>
    public RepeatButton()
    {
    }

    /// <summary>Initializes a new repeat button showing <paramref name="text"/>.</summary>
    public RepeatButton(string text) : base(text)
    {
    }

    private static bool IsPositive(TimeSpan value) => value > TimeSpan.Zero;

    /// <inheritdoc/>
    /// <remarks>Starts the repeat timer.</remarks>
    protected override void OnPressStarted(Point position)
    {
        base.OnPressStarted(position);
        if (_timer == null)
        {
            _timer = new DispatcherTimer();
            _timer.Tick += OnTimerTick;
        }

        _timer.Stop();
        _timer.Interval = Delay;
        _timer.Start();
    }

    /// <inheritdoc/>
    /// <remarks>Stops the repeat timer.</remarks>
    protected override void OnPressEnded()
    {
        _timer?.Stop();
        base.OnPressEnded();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (!IsPressActive || !IsEnabled)
        {
            _timer?.Stop();
            return;
        }

        var interval = Interval;
        if (_timer!.Interval != interval)
        {
            _timer.Interval = interval;
        }

        OnClick();
    }
}
