using System;
using System.Windows.Input;
using Atelier.Core.Animation;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Threading;

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
        BindableProperty.Register<Button, ICommand?>(nameof(Command), null, (s, o, n) => ((Button)s).OnCommandChanged(o, n));

    public static readonly BindableProperty<object?> CommandParameterProperty =
        BindableProperty.Register<Button, object?>(nameof(CommandParameter), null, (s, o, n) => ((Button)s).UpdateIsEnabledCore());

    public static readonly BindableProperty<ButtonVariant> VariantProperty =
        BindableProperty.Register<Button, ButtonVariant>(
            nameof(Variant),
            ButtonVariant.Filled,
            options: PropertyOptions.AffectsRender
        );

    public static readonly BindableProperty<float> ElevationProperty =
        BindableProperty.Register<Button, float>(
            nameof(Elevation),
            1f,
            options: PropertyOptions.AffectsRender
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

    static Button()
    {
        PaddingProperty.OverrideDefaultValue<Button>(new Thickness(16, 6));
        CornerRadiusProperty.OverrideDefaultValue<Button>(new CornerRadius(20)); // MD3 pill shape default
    }

    public Button()
    {
        IsFocusable = true;
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

    #region Command state

    // The command the button currently listens to. Only set while the button is displayed, so a long-lived command
    // never keeps a removed button alive through its CanExecuteChanged event.
    private ICommand? _observedCommand;

    /// <summary>
    /// The button is disabled while its <see cref="Command"/> cannot execute with the current <see cref="CommandParameter"/>.
    /// </summary>
    protected override bool IsEnabledCore => Command is not { } command || command.CanExecute(CommandParameter);

    private void OnCommandChanged(ICommand? oldCommand, ICommand? newCommand)
    {
        if (IsAttachedToVisualTree)
        {
            ObserveCommand(newCommand);
        }
        UpdateIsEnabledCore();
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree()
    {
        ObserveCommand(Command);
        UpdateIsEnabledCore(); // CanExecute may have changed while the button was not displayed
        base.OnAttachedToVisualTree();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree()
    {
        ObserveCommand(null);
        base.OnDetachedFromVisualTree();
    }

    private void ObserveCommand(ICommand? command)
    {
        if (_observedCommand == command)
        {
            return;
        }

        if (_observedCommand != null)
        {
            _observedCommand.CanExecuteChanged -= OnCanExecuteChanged;
        }

        _observedCommand = command;

        if (_observedCommand != null)
        {
            _observedCommand.CanExecuteChanged += OnCanExecuteChanged;
        }
    }

    private void OnCanExecuteChanged(object? sender, EventArgs e)
    {
        // Commands may raise this from a background thread; property changes must happen on the UI thread.
        if (Dispatcher.CheckAccess())
        {
            UpdateIsEnabledCore();
        }
        else
        {
            Dispatcher.Post(UpdateIsEnabledCore);
        }
    }

    #endregion

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
