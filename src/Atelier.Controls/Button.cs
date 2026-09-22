using System;
using System.Windows.Input;
using Atelier.Core.Animation;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;

namespace Atelier.Controls;

public enum ButtonVariant
{
    Filled,
    Elevated,
    Tonal,
    Outlined,
    Text
}

public class Button : ContentControl
{
    public static readonly BindableProperty<ICommand?> CommandProperty =
        BindableProperty.Register<Button, ICommand?>(nameof(Command), null);

    public static readonly BindableProperty<object?> CommandParameterProperty =
        BindableProperty.Register<Button, object?>(nameof(CommandParameter), null);

    public static readonly BindableProperty<ButtonVariant> VariantProperty =
        BindableProperty.Register<Button, ButtonVariant>(
            nameof(Variant),
            ButtonVariant.Filled,
            (s, o, n) => ((Button)s).InvalidateVisual()
        );

    public static readonly BindableProperty<float> ElevationProperty =
        BindableProperty.Register<Button, float>(
            nameof(Elevation),
            1f,
            (s, o, n) => ((Button)s).InvalidateVisual()
        );

    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }
    public ButtonVariant Variant { get => GetValue(VariantProperty); set => SetValue(VariantProperty, value); }
    public float Elevation { get => GetValue(ElevationProperty); set => SetValue(ElevationProperty, value); }

    public event EventHandler? Click;

    // Ripple state for Material Design 3
    public Point RippleCenter { get; private set; } = Point.Zero;
    public float RippleProgress { get; private set; } = 0f;
    public float RippleOpacity { get; private set; } = 0f;
    public bool HasActiveRipple => RippleOpacity > 0f;

    public Button()
    {
        IsFocusable = true;
        Padding = new Thickness(16, 6);
        CornerRadius = new CornerRadius(20); // MD3 pill shape default
    }

    public Button(string text) : this()
    {
        Content = new TextBlock(text) { VerticalAlignment = VerticalAlignment.Center };
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        e.Handled = true;

        RippleCenter = e.Position;
        RippleProgress = 0f;
        RippleOpacity = 0.25f;

        // Animate ripple expansion
        var anim = new FloatAnimation(
            from: 0f,
            to: 1f,
            duration: TimeSpan.FromMilliseconds(350),
            onUpdate: p =>
            {
                RippleProgress = p;
                InvalidateVisual();
            },
            easing: Easing.EaseOutCubic
        );

        StartAnimation(anim);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        bool wasPressed = IsPressed;
        base.OnPointerReleased(e);
        e.Handled = true;

        if (wasPressed && IsHovered && IsEnabled)
        {
            if (Command != null && Command.CanExecute(CommandParameter))
            {
                Command.Execute(CommandParameter);
            }
            Click?.Invoke(this, EventArgs.Empty);
        }

        // Fade out ripple
        var fadeAnim = new FloatAnimation(
            from: RippleOpacity,
            to: 0f,
            duration: TimeSpan.FromMilliseconds(200),
            onUpdate: o =>
            {
                RippleOpacity = o;
                InvalidateVisual();
            },
            easing: Easing.Linear
        );

        StartAnimation(fadeAnim);
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsEnabled && (e.Key is Key.Enter or Key.Space))
        {
            if (Command != null && Command.CanExecute(CommandParameter))
            {
                Command.Execute(CommandParameter);
            }
            Click?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private static AnimationClock? _clock;
    public static void SetGlobalAnimationClock(AnimationClock clock) => _clock = clock;

    protected void StartAnimation(IAnimation animation)
    {
        _clock?.Add(animation);
    }
}
