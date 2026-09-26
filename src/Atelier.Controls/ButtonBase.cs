using System;
using System.Windows.Input;
using Atelier.Core.Animation;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Threading;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// Specifies when a <see cref="ButtonBase"/> raises its <see cref="ButtonBase.Click"/> event.
/// </summary>
public enum ClickMode
{
    /// <summary>Click when the left button is pressed and released over the control, or Space is released.</summary>
    Release,

    /// <summary>Click as soon as the left button or Space is pressed.</summary>
    Press,

    /// <summary>Click when the pointer enters the control; the keyboard behaves as with <see cref="Release"/>.</summary>
    Hover
}

/// <summary>
/// The base class of clickable controls such as <see cref="Button"/> and <see cref="ToggleButton"/>: raises
/// <see cref="Click"/> and executes <see cref="Command"/> in response to the pointer and keyboard.
/// </summary>
/// <remarks>
/// <para>
/// Only the left pointer button clicks; other buttons are left unhandled so they bubble (for example to open a context
/// menu). With the default <see cref="ClickMode.Release"/>, the pointer must be pressed and released while over the
/// control; leaving the control cancels the press. From the keyboard, Enter clicks on key-down and Space on key-up, as in
/// WPF; auto-repeated key presses and keys already handled by a descendant are ignored.
/// </para>
/// <para>
/// A disabled control ignores all input. While <see cref="Command"/> cannot execute with <see cref="CommandParameter"/>,
/// the control disables itself (see <see cref="UIElement.IsEnabled"/>). String content is shown as a
/// <see cref="TextBlock"/>.
/// </para>
/// </remarks>
public abstract class ButtonBase : ContentControl
{
    /// <summary>Identifies the <see cref="Command"/> property.</summary>
    public static readonly BindableProperty<ICommand?> CommandProperty =
        BindableProperty.Register<ButtonBase, ICommand?>(nameof(Command), null, (s, o, n) => ((ButtonBase)s).OnCommandChanged(n));

    /// <summary>Identifies the <see cref="CommandParameter"/> property.</summary>
    public static readonly BindableProperty<object?> CommandParameterProperty =
        BindableProperty.Register<ButtonBase, object?>(nameof(CommandParameter), null, (s, o, n) => ((ButtonBase)s).UpdateIsEnabledCore());

    /// <summary>Identifies the <see cref="ClickMode"/> property.</summary>
    public static readonly BindableProperty<ClickMode> ClickModeProperty =
        BindableProperty.Register<ButtonBase, ClickMode>(nameof(ClickMode), ClickMode.Release);

    /// <summary>
    /// Gets or sets the command executed after <see cref="Click"/> is raised. The control is disabled while the command
    /// cannot execute. The default is <c>null</c>.
    /// </summary>
    public ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    /// <summary>Gets or sets the parameter passed to <see cref="Command"/>. The default is <c>null</c>.</summary>
    public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

    /// <summary>Gets or sets when <see cref="Click"/> is raised. The default is <see cref="ClickMode.Release"/>.</summary>
    public ClickMode ClickMode { get => GetValue(ClickModeProperty); set => SetValue(ClickModeProperty, value); }

    /// <summary>Occurs when the control is clicked with the pointer or activated with Enter or Space.</summary>
    public event EventHandler? Click;

    // A left-button press (or Space) that started while enabled and hasn't ended yet.
    private bool _isLeftButtonDown;
    private bool _isSpaceDown;

    static ButtonBase()
    {
        IsFocusableProperty.OverrideDefaultValue<ButtonBase>(true);
    }

    /// <summary>
    /// Gets whether a press started by the left pointer button or Space is in progress; it ends on release, when the
    /// pointer leaves, or when the control is disabled, loses focus or is removed from the tree.
    /// </summary>
    protected bool IsPressActive => _isLeftButtonDown || _isSpaceDown;

    /// <summary>
    /// Raises <see cref="Click"/> and executes <see cref="Command"/> if it can execute. Override to add behavior that
    /// happens on every click, such as toggling.
    /// </summary>
    protected virtual void OnClick()
    {
        Click?.Invoke(this, EventArgs.Empty);

        if (Command is { } command && command.CanExecute(CommandParameter))
        {
            command.Execute(CommandParameter);
        }
    }

    /// <summary>
    /// Called when a press by the left pointer button or Space starts on the enabled control, before a
    /// <see cref="ClickMode.Press"/> click. The base implementation does nothing.
    /// </summary>
    /// <param name="position">The pointer position in local coordinates; the control's center for Space.</param>
    protected virtual void OnPressStarted(Point position)
    {
    }

    /// <summary>
    /// Called when the press reported by <see cref="OnPressStarted"/> ends, whether or not it clicks. The base
    /// implementation does nothing.
    /// </summary>
    protected virtual void OnPressEnded()
    {
    }

    /// <inheritdoc/>
    /// <remarks>Shows string content as a <see cref="TextBlock"/> unless a <see cref="ContentControl.ContentTemplate"/> is set.</remarks>
    public override UIElement? ResolveContentView(object? content)
    {
        if (content is string text && ContentTemplate == null)
        {
            return new TextBlock(text) { VerticalAlignment = VerticalAlignment.Center };
        }

        return base.ResolveContentView(content);
    }

    #region Pointer input

    /// <inheritdoc/>
    /// <remarks>Clicks when <see cref="ClickMode"/> is <see cref="ClickMode.Hover"/>.</remarks>
    public override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (ClickMode == ClickMode.Hover && IsEnabled)
        {
            OnClick();
        }
    }

    /// <inheritdoc/>
    /// <remarks>Leaving the control cancels a pointer press without clicking.</remarks>
    public override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        EndPointerPress();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A left-button press on the enabled control starts a press, is marked handled, and clicks right away when
    /// <see cref="ClickMode"/> is <see cref="ClickMode.Press"/>.
    /// </remarks>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Button != PointerButtons.Left || !IsEnabled)
        {
            return;
        }

        e.Handled = true;
        EndPointerPress();
        _isLeftButtonDown = true;
        OnPressStarted(e.Position);

        if (ClickMode == ClickMode.Press)
        {
            OnClick();
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Ends a left-button press; with <see cref="ClickMode.Release"/> it clicks if the pointer is still over the control.
    /// </remarks>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        bool wasPressed = _isLeftButtonDown && IsPressed;
        base.OnPointerReleased(e);
        if (e.Button != PointerButtons.Left || !_isLeftButtonDown)
        {
            return;
        }

        e.Handled = true;
        EndPointerPress();

        if (wasPressed && IsHovered && IsEnabled && ClickMode == ClickMode.Release)
        {
            OnClick();
        }
    }

    private void EndPointerPress()
    {
        if (_isLeftButtonDown)
        {
            _isLeftButtonDown = false;
            OnPressEnded();
        }
    }

    #endregion

    #region Keyboard input

    /// <inheritdoc/>
    /// <remarks>Enter clicks; Space starts a press that clicks on key-up (or right away with <see cref="ClickMode.Press"/>).</remarks>
    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !IsEnabled)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (!e.IsRepeat)
            {
                OnClick();
            }
        }
        else if (e.Key == Key.Space)
        {
            e.Handled = true;
            if (!e.IsRepeat && !_isSpaceDown)
            {
                _isSpaceDown = true;
                OnPressStarted(new Point(Bounds.Width * 0.5f, Bounds.Height * 0.5f));
                if (ClickMode == ClickMode.Press)
                {
                    OnClick();
                }
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>Releasing Space after pressing it on this control clicks, unless <see cref="ClickMode"/> is <see cref="ClickMode.Press"/>.</remarks>
    public override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Handled || e.Key != Key.Space || !_isSpaceDown)
        {
            return;
        }

        e.Handled = true;
        EndSpacePress();
        if (IsEnabled && ClickMode != ClickMode.Press)
        {
            OnClick();
        }
    }

    /// <inheritdoc/>
    /// <remarks>Losing focus cancels a Space press without clicking.</remarks>
    public override void OnLostFocus()
    {
        base.OnLostFocus();
        EndSpacePress();
    }

    private void EndSpacePress()
    {
        if (_isSpaceDown)
        {
            _isSpaceDown = false;
            OnPressEnded();
        }
    }

    #endregion

    /// <summary>Ends a pointer or Space press in progress without clicking.</summary>
    protected void CancelPress()
    {
        EndPointerPress();
        EndSpacePress();
    }

    /// <inheritdoc/>
    /// <remarks>Becoming disabled cancels a press in progress.</remarks>
    protected override void OnPropertyValueChanged<T>(BindableProperty<T> property, T oldValue, T newValue)
    {
        base.OnPropertyValueChanged(property, oldValue, newValue);
        if (ReferenceEquals(property, IsEnabledProperty) && !IsEnabled)
        {
            CancelPress();
        }
    }

    #region Command state

    // The command the control currently listens to. Only set while the control is displayed, so a long-lived command
    // never keeps a removed control alive through its CanExecuteChanged event.
    private ICommand? _observedCommand;
    private Action? _updateIsEnabledCore;

    /// <summary>
    /// The control is disabled while its <see cref="Command"/> cannot execute with the current <see cref="CommandParameter"/>.
    /// </summary>
    protected override bool IsEnabledCore => Command is not { } command || command.CanExecute(CommandParameter);

    private void OnCommandChanged(ICommand? newCommand)
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
        UpdateIsEnabledCore(); // CanExecute may have changed while the control was not displayed
        base.OnAttachedToVisualTree();
    }

    /// <inheritdoc/>
    /// <remarks>Also cancels a press in progress.</remarks>
    protected override void OnDetachedFromVisualTree()
    {
        ObserveCommand(null);
        CancelPress();
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
            Dispatcher.Post(_updateIsEnabledCore ??= UpdateIsEnabledCore);
        }
    }

    #endregion

    #region Animation clock

    /// <summary>
    /// Gets the clock that runs the state animations of all button-like controls (ripples, check marks, switch thumbs),
    /// or <c>null</c> to apply state changes immediately without animating.
    /// </summary>
    protected static AnimationClock? AnimationClock { get; private set; }

    /// <summary>
    /// Sets the clock that runs the state animations of all button-like controls (<see cref="Button"/>,
    /// <see cref="CheckBox"/>, <see cref="RadioButton"/>, <see cref="Switch"/>, ...). The platform layer calls this at startup;
    /// pass <c>null</c> to disable the animations.
    /// </summary>
    /// <param name="clock">The clock, or <c>null</c>.</param>
    public static void SetGlobalAnimationClock(AnimationClock? clock) => AnimationClock = clock;

    /// <summary>
    /// Runs <paramref name="animation"/> on the <see cref="AnimationClock"/> after stopping <paramref name="current"/>, so
    /// two animations never write the same value, and stores it in <paramref name="current"/>.
    /// </summary>
    /// <returns><c>false</c> if there is no clock; the animation is then not started.</returns>
    protected static bool StartAnimation(ref FloatAnimation? current, FloatAnimation animation)
    {
        current?.Stop();
        current = null;
        if (AnimationClock is not { } clock)
        {
            return false;
        }

        current = animation;
        clock.Add(animation);
        return true;
    }

    #endregion
}
