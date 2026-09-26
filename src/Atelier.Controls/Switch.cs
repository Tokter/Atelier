using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// A Material Design 3 switch: a track with a sliding thumb that turns an option on or off, followed by optional content
/// (typically a label).
/// </summary>
/// <remarks>
/// Clicking, Space or Enter toggles <see cref="ToggleButton.IsChecked"/>. The 40×22 track is placed inside
/// <see cref="Control.Padding"/> (default 0×4) and the content follows it after a 10 px gap, vertically centered. The
/// indeterminate state (<c>null</c>) shows the thumb in the middle of the track.
/// </remarks>
public class Switch : ToggleButton
{
    /// <summary>Identifies the <see cref="ShowThumbIcon"/> property.</summary>
    public static readonly BindableProperty<bool> ShowThumbIconProperty =
        BindableProperty.Register<Switch, bool>(nameof(ShowThumbIcon), false, options: PropertyOptions.AffectsRender);

    /// <summary>
    /// Gets or sets whether the thumb shows a check mark when on and a dash when off. The default is <c>false</c>.
    /// </summary>
    public bool ShowThumbIcon
    {
        get => GetValue(ShowThumbIconProperty);
        set => SetValue(ShowThumbIconProperty, value);
    }

    /// <summary>Identifies the <see cref="TrackWidth"/> property.</summary>
    public static readonly BindableProperty<float> TrackWidthProperty =
        BindableProperty.Register<Switch, float>(nameof(TrackWidth), 52f, options: PropertyOptions.AffectsMeasure, validateValue: IsValidTrackSize);

    /// <summary>Identifies the <see cref="TrackHeight"/> property.</summary>
    public static readonly BindableProperty<float> TrackHeightProperty =
        BindableProperty.Register<Switch, float>(nameof(TrackHeight), 32f, options: PropertyOptions.AffectsMeasure, validateValue: IsValidTrackSize);

    /// <summary>
    /// Gets or sets the width of the track. The default is the Material Design 3 size, 52 px; themes may use a smaller
    /// desktop size.
    /// </summary>
    public float TrackWidth
    {
        get => GetValue(TrackWidthProperty);
        set => SetValue(TrackWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the height of the track; the handle sizes follow it. The default is the Material Design 3 size,
    /// 32 px; themes may use a smaller desktop size.
    /// </summary>
    public float TrackHeight
    {
        get => GetValue(TrackHeightProperty);
        set => SetValue(TrackHeightProperty, value);
    }

    private static bool IsValidTrackSize(float value) => float.IsFinite(value) && value > 0;

    private const float ContentSpacing = 12f;

    /// <summary>
    /// Gets the animated thumb position used for drawing: 0 at the off end, 1 at the on end.
    /// Same as <see cref="ToggleButton.CheckAnimationProgress"/>.
    /// </summary>
    public float ThumbAnimationProgress => CheckAnimationProgress;

    /// <inheritdoc/>
    /// <remarks>The switch uses 200 ms.</remarks>
    protected override TimeSpan CheckAnimationDuration => TimeSpan.FromMilliseconds(200);

    static Switch()
    {
        PaddingProperty.OverrideDefaultValue<Switch>(new Thickness(0, 4));
    }

    /// <summary>Initializes a new switch that is off and has no content.</summary>
    public Switch()
    {
    }

    /// <summary>Initializes a new switch that is off, labeled <paramref name="text"/>.</summary>
    public Switch(string text) : this()
    {
        Content = new TextBlock(text);
    }

    /// <inheritdoc/>
    /// <remarks>The indeterminate state maps to 0.5, the middle of the track.</remarks>
    protected override float GetCheckProgressTarget(bool? isChecked) => isChecked switch
    {
        true => 1f,
        false => 0f,
        null => 0.5f
    };

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var pad = Padding;

        if (CurrentView is { } child && child.Visibility != Visibility.Collapsed)
        {
            child.Measure(new Size(
                Math.Max(0, availableSize.Width - TrackWidth - ContentSpacing - pad.Horizontal),
                Math.Max(0, availableSize.Height - pad.Vertical)));
            return new Size(
                pad.Left + TrackWidth + ContentSpacing + child.DesiredSize.Width + pad.Right,
                pad.Top + Math.Max(TrackHeight, child.DesiredSize.Height) + pad.Bottom);
        }

        return new Size(pad.Left + TrackWidth + pad.Right, pad.Top + TrackHeight + pad.Bottom);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var pad = Padding;

        if (CurrentView is { } child && child.Visibility != Visibility.Collapsed)
        {
            float childX = pad.Left + TrackWidth + ContentSpacing;
            float childY = pad.Top + (finalSize.Height - pad.Vertical - child.DesiredSize.Height) * 0.5f;
            child.Arrange(new Rect(childX, childY, Math.Max(0, finalSize.Width - pad.Right - childX), child.DesiredSize.Height));
        }

        return finalSize;
    }
}
