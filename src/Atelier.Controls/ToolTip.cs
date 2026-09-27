using System;
using Atelier.Core.Animation;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// The small popup that shows a tooltip next to an element. Tooltips are usually set with
/// <see cref="ToolTipService.ToolTipProperty"/> (<c>.ToolTip(...)</c> in markup), and <see cref="ToolTipService"/>
/// shows and hides this popup; it can also be used directly as a transient popup.
/// </summary>
/// <remarks>
/// <para>
/// A string <see cref="Content"/> makes a plain tooltip: short wrapped text on a small, not interactive surface that
/// lets pointer input through. Any other content makes a rich tooltip: an element (such as a <see cref="RichToolTip"/>)
/// is shown as is and can contain buttons or links; other objects are shown like <see cref="ContentControl.Content"/>.
/// Rich tooltips use the <see cref="RichStyleKey"/> style.
/// </para>
/// <para>
/// A tooltip is a transient popup (see <see cref="Popup.IsTransient"/>): it never takes input away from the window.
/// </para>
/// </remarks>
public class ToolTip : Popup
{
    /// <summary>The style key of rich tooltips; the theme styles plain tooltips with the implicit <see cref="ToolTip"/> style.</summary>
    public const string RichStyleKey = "RichToolTip";

    /// <summary>Identifies the <see cref="Content"/> property.</summary>
    public static readonly BindableProperty<object?> ContentProperty =
        BindableProperty.Register<ToolTip, object?>(nameof(Content), null, (s, o, n) => ((ToolTip)s).OnContentChanged(n));

    /// <summary>Identifies the <see cref="IsInteractive"/> property.</summary>
    public static readonly BindableProperty<bool> IsInteractiveProperty =
        BindableProperty.Register<ToolTip, bool>(nameof(IsInteractive), false);

    /// <summary>Identifies the <see cref="Gap"/> property.</summary>
    public static readonly BindableProperty<float> GapProperty =
        BindableProperty.Register<ToolTip, float>(nameof(Gap), 4f, options: PropertyOptions.AffectsRender);

    // Reused for plain text, so showing a string tooltip allocates no elements.
    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.Wrap, IsHitTestVisible = false };
    private ContentControl? _presenter;
    private FloatAnimation? _fade;

    /// <summary>Initializes a new, closed tooltip.</summary>
    public ToolTip()
    {
        IsTransient = true;
        StaysOpen = true;
        Opened += (_, _) => FadeIn();
    }

    /// <summary>
    /// Gets or sets what the tooltip shows: a string (plain tooltip), an element, or any object shown through the
    /// view locator or <see cref="ToString"/> (rich tooltips).
    /// </summary>
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>Gets whether the content makes this a rich tooltip (anything but a string).</summary>
    public bool IsRich { get; private set; }

    /// <summary>
    /// Gets or sets whether the tooltip receives pointer input, so its buttons and links work and it stays open while
    /// the pointer is over it. A non-interactive tooltip lets pointer input through. <see cref="ToolTipService"/> sets it
    /// per tooltip (rich tooltips are interactive by default). The default is <c>false</c>.
    /// </summary>
    public bool IsInteractive
    {
        get => GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    /// <summary>Gets or sets the space between the tooltip and the element (or pointer) it points at. The default is 4.</summary>
    public float Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <inheritdoc/>
    internal override bool AcceptsPointerInput => IsInteractive;

    /// <inheritdoc/>
    protected override Rect GetPlacementTargetRect()
    {
        var target = base.GetPlacementTargetRect();
        float gap = Gap;
        return new Rect(target.X - gap, target.Y - gap, target.Width + gap * 2, target.Height + gap * 2);
    }

    private void OnContentChanged(object? content)
    {
        switch (content)
        {
            case null:
                Child = null;
                break;
            case string text:
                _text.Text = text;
                Child = _text;
                break;
            case UIElement element:
                Child = element;
                break;
            default:
                _presenter ??= new ContentControl();
                _presenter.Content = content;
                Child = _presenter;
                break;
        }

        IsRich = content is not null and not string;
        StyleKey = IsRich ? RichStyleKey : null;
    }

    #region Animation

    private static AnimationClock? _clock;

    /// <summary>
    /// Sets the clock that fades tooltips in. The platform layer calls this at startup; <c>null</c> shows them at once.
    /// </summary>
    public static void SetGlobalAnimationClock(AnimationClock? clock) => _clock = clock;

    private void FadeIn()
    {
        _fade?.Stop();
        if (_clock == null)
        {
            Opacity = 1f;
            return;
        }

        Opacity = 0f;
        _fade = new FloatAnimation(0f, 1f, TimeSpan.FromMilliseconds(120), value => Opacity = value, Easing.EmphasizedDecelerate);
        _clock.Add(_fade);
    }

    #endregion
}
