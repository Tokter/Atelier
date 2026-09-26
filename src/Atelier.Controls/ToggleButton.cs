using System;
using Atelier.Core.Animation;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;

namespace Atelier.Controls;

/// <summary>
/// A button that switches between checked and unchecked (and, with <see cref="IsThreeState"/>, indeterminate) each time
/// it is clicked. The base class of <see cref="CheckBox"/>, <see cref="RadioButton"/> and <see cref="Switch"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IsChecked"/> is a <see cref="Nullable{T}">bool?</see> as in WPF and Avalonia: <c>null</c> is the
/// indeterminate state. A click calls <see cref="OnToggle"/> and then raises <see cref="ButtonBase.Click"/> and executes
/// <see cref="ButtonBase.Command"/>, so the command sees the new state.
/// </para>
/// <para>
/// A standalone toggle button is drawn like a <see cref="ButtonVariant.Tonal"/> button when checked and an
/// <see cref="ButtonVariant.Outlined"/> one otherwise.
/// </para>
/// </remarks>
public class ToggleButton : ButtonBase
{
    /// <summary>Identifies the <see cref="IsChecked"/> property.</summary>
    public static readonly BindableProperty<bool?> IsCheckedProperty =
        BindableProperty.Register<ToggleButton, bool?>(
            nameof(IsChecked),
            false,
            (s, o, n) => ((ToggleButton)s).OnIsCheckedChanged(o, n),
            options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="IsThreeState"/> property.</summary>
    public static readonly BindableProperty<bool> IsThreeStateProperty =
        BindableProperty.Register<ToggleButton, bool>(nameof(IsThreeState), false);

    /// <summary>
    /// Gets or sets whether the control is checked (<c>true</c>), unchecked (<c>false</c>) or indeterminate
    /// (<c>null</c>). The default is <c>false</c>. Code and bindings may set <c>null</c> even when
    /// <see cref="IsThreeState"/> is <c>false</c>.
    /// </summary>
    public bool? IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    /// <summary>
    /// Gets or sets whether clicking cycles through the indeterminate state (unchecked → checked → indeterminate →
    /// unchecked) instead of just toggling. The default is <c>false</c>.
    /// </summary>
    public bool IsThreeState { get => GetValue(IsThreeStateProperty); set => SetValue(IsThreeStateProperty, value); }

    /// <summary>Occurs when <see cref="IsChecked"/> becomes <c>true</c>.</summary>
    public event EventHandler? Checked;

    /// <summary>Occurs when <see cref="IsChecked"/> becomes <c>false</c>.</summary>
    public event EventHandler? Unchecked;

    /// <summary>Occurs when <see cref="IsChecked"/> becomes <c>null</c> (indeterminate).</summary>
    public event EventHandler? Indeterminate;

    /// <summary>
    /// Occurs after <see cref="Checked"/>, <see cref="Unchecked"/> or <see cref="Indeterminate"/> whenever
    /// <see cref="IsChecked"/> changes, with the new value.
    /// </summary>
    public event EventHandler<bool?>? CheckedChanged;

    /// <summary>
    /// Gets the animated check state used for drawing: 0 when unchecked, 1 when checked or indeterminate, and values in
    /// between while animating from one to the other.
    /// </summary>
    public float CheckAnimationProgress { get; private set; }

    /// <summary>Gets how long the <see cref="CheckAnimationProgress"/> transition takes. The default is 180 ms.</summary>
    protected virtual TimeSpan CheckAnimationDuration => TimeSpan.FromMilliseconds(180);

    private FloatAnimation? _checkAnimation;
    private Action<float>? _setCheckProgress;

    static ToggleButton()
    {
        PaddingProperty.OverrideDefaultValue<ToggleButton>(new Thickness(16, 6));
        CornerRadiusProperty.OverrideDefaultValue<ToggleButton>(new CornerRadius(20));
    }

    /// <summary>Initializes a new, unchecked toggle button.</summary>
    public ToggleButton()
    {
    }

    /// <summary>Initializes a new, unchecked toggle button showing <paramref name="text"/>.</summary>
    public ToggleButton(string text) : this()
    {
        Content = text;
    }

    /// <summary>
    /// Advances <see cref="IsChecked"/> in response to a click: unchecked → checked → (indeterminate if
    /// <see cref="IsThreeState"/>) → unchecked.
    /// </summary>
    protected virtual void OnToggle()
    {
        IsChecked = IsChecked switch
        {
            true => IsThreeState ? null : false,
            null => false,
            false => true
        };
    }

    /// <inheritdoc/>
    /// <remarks>Calls <see cref="OnToggle"/> before raising <see cref="ButtonBase.Click"/> and executing the command.</remarks>
    protected override void OnClick()
    {
        OnToggle();
        base.OnClick();
    }

    /// <summary>
    /// Gets the <see cref="CheckAnimationProgress"/> value that represents <paramref name="isChecked"/>. The default maps
    /// <c>false</c> to 0 and everything else to 1.
    /// </summary>
    protected virtual float GetCheckProgressTarget(bool? isChecked) => isChecked == false ? 0f : 1f;

    /// <summary>
    /// Called when <see cref="IsChecked"/> changes. The base implementation animates <see cref="CheckAnimationProgress"/>
    /// and raises <see cref="Checked"/>, <see cref="Unchecked"/> or <see cref="Indeterminate"/>, then
    /// <see cref="CheckedChanged"/>.
    /// </summary>
    protected virtual void OnIsCheckedChanged(bool? oldValue, bool? newValue)
    {
        AnimateCheckProgress(GetCheckProgressTarget(newValue));

        switch (newValue)
        {
            case true:
                Checked?.Invoke(this, EventArgs.Empty);
                break;
            case false:
                Unchecked?.Invoke(this, EventArgs.Empty);
                break;
            default:
                Indeterminate?.Invoke(this, EventArgs.Empty);
                break;
        }

        CheckedChanged?.Invoke(this, newValue);
    }

    private void AnimateCheckProgress(float target)
    {
        _setCheckProgress ??= p =>
        {
            CheckAnimationProgress = p;
            InvalidateVisual();
        };

        if (AnimationClock == null || target == CheckAnimationProgress)
        {
            _checkAnimation?.Stop();
            _checkAnimation = null;
            _setCheckProgress(target);
            return;
        }

        // Stops the previous animation, so a quick second toggle continues from where the first one got to.
        StartAnimation(ref _checkAnimation, new FloatAnimation(
            CheckAnimationProgress,
            target,
            CheckAnimationDuration,
            _setCheckProgress,
            Easing.EmphasizedDecelerate));
    }
}
