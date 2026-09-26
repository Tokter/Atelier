using System;
using System.Collections.Generic;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// A Material Design 3 check box: a square indicator that shows a check mark when checked and a dash when
/// indeterminate, followed by optional content (typically a label).
/// </summary>
/// <remarks>
/// Clicking, Space or Enter toggles <see cref="ToggleButton.IsChecked"/>; set <see cref="ToggleButton.IsThreeState"/> to
/// include the indeterminate state. The indicator is placed inside <see cref="Control.Padding"/> (default 0×4) and the
/// content follows it after an 8 px gap, vertically centered.
/// </remarks>
public class CheckBox : ToggleButton
{
    /// <summary>The gap between the indicator and the content.</summary>
    protected const float ContentSpacing = 8f;

    static CheckBox()
    {
        PaddingProperty.OverrideDefaultValue<CheckBox>(new Thickness(0, 4));
    }

    /// <summary>Initializes a new, unchecked check box without content.</summary>
    public CheckBox()
    {
    }

    /// <summary>Initializes a new, unchecked check box labeled <paramref name="text"/>.</summary>
    public CheckBox(string text) : this()
    {
        Content = new TextBlock(text);
    }

    /// <summary>Gets the width and height of the check indicator (18 for the check box).</summary>
    public virtual float IndicatorSize => 18f;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var pad = Padding;
        float indicator = IndicatorSize;

        if (CurrentView is { } child && child.Visibility != Visibility.Collapsed)
        {
            child.Measure(new Size(
                Math.Max(0, availableSize.Width - pad.Horizontal - indicator - ContentSpacing),
                Math.Max(0, availableSize.Height - pad.Vertical)));
            return new Size(
                pad.Horizontal + indicator + ContentSpacing + child.DesiredSize.Width,
                pad.Vertical + Math.Max(indicator, child.DesiredSize.Height));
        }

        return new Size(pad.Horizontal + indicator, pad.Vertical + indicator);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var pad = Padding;

        if (CurrentView is { } child && child.Visibility != Visibility.Collapsed)
        {
            float x = pad.Left + IndicatorSize + ContentSpacing;
            float y = pad.Top + (finalSize.Height - pad.Vertical - child.DesiredSize.Height) * 0.5f;
            child.Arrange(new Rect(x, y, Math.Max(0, finalSize.Width - pad.Right - x), child.DesiredSize.Height));
        }

        return finalSize;
    }

    /// <summary>
    /// Gets the indicator's bounds in local coordinates: inside <see cref="Control.Padding"/> on the left, vertically
    /// centered. Renderers draw the box (or circle) here.
    /// </summary>
    public Rect GetIndicatorBounds()
    {
        var pad = Padding;
        float size = IndicatorSize;
        return new Rect(pad.Left, pad.Top + (Bounds.Height - pad.Vertical - size) * 0.5f, size, size);
    }
}

/// <summary>
/// A Material Design 3 radio button: one of a group of mutually exclusive options.
/// </summary>
/// <remarks>
/// <para>
/// Clicking, Space or Enter checks the radio button; clicking a checked one does nothing (no change events), so only
/// checking another option unchecks it. Checking it, from input or code, unchecks the other radio buttons of its group.
/// </para>
/// <para>
/// Radio buttons with the same <see cref="GroupName"/> form a group within one window (element tree), like WPF; radio
/// buttons that are not displayed share one scope. Radio buttons without a group name form a group with the unnamed
/// radio buttons that share their parent.
/// </para>
/// </remarks>
public class RadioButton : CheckBox
{
    // Named groups only; unnamed radio buttons find their group through their parent. Entries of collected radio buttons
    // are pruned whenever a group is scanned.
    private static readonly Dictionary<string, List<WeakReference<RadioButton>>> _groups = new();

    /// <summary>Identifies the <see cref="GroupName"/> property.</summary>
    public static readonly BindableProperty<string?> GroupNameProperty =
        BindableProperty.Register<RadioButton, string?>(
            nameof(GroupName),
            null,
            (s, o, n) => ((RadioButton)s).OnGroupNameChanged(o, n));

    /// <summary>
    /// Gets or sets the name of the group of mutually exclusive radio buttons; <c>null</c> (the default) groups the radio
    /// button with its unnamed siblings.
    /// </summary>
    public string? GroupName
    {
        get => GetValue(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }

    // The weak reference registered for this radio button in its named group, reused so re-registering doesn't allocate.
    private WeakReference<RadioButton>? _groupEntry;

    /// <summary>Initializes a new, unchecked radio button without content.</summary>
    public RadioButton()
    {
    }

    /// <summary>Initializes a new, unchecked radio button labeled <paramref name="text"/>.</summary>
    public RadioButton(string text) : this()
    {
        Content = new TextBlock(text);
    }

    /// <summary>Gets the diameter of the radio indicator (20).</summary>
    public override float IndicatorSize => 20f;

    /// <summary>Checks the radio button; a checked radio button stays checked.</summary>
    protected override void OnToggle()
    {
        IsChecked = true;
    }

    /// <inheritdoc/>
    /// <remarks>Checking the radio button unchecks the others in its group.</remarks>
    protected override void OnIsCheckedChanged(bool? oldValue, bool? newValue)
    {
        base.OnIsCheckedChanged(oldValue, newValue);
        if (newValue == true)
        {
            UncheckOthersInGroup();
        }
    }

    private void OnGroupNameChanged(string? oldGroup, string? newGroup)
    {
        if (oldGroup != null)
        {
            Unregister(oldGroup);
        }

        if (newGroup != null)
        {
            if (!_groups.TryGetValue(newGroup, out var list))
            {
                list = [];
                _groups[newGroup] = list;
            }
            list.Add(_groupEntry ??= new WeakReference<RadioButton>(this));
        }

        if (IsChecked == true)
        {
            UncheckOthersInGroup();
        }
    }

    private void Unregister(string group)
    {
        if (!_groups.TryGetValue(group, out var list))
        {
            return;
        }

        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (!list[i].TryGetTarget(out var rb) || rb == this)
            {
                list.RemoveAt(i);
            }
        }

        if (list.Count == 0)
        {
            _groups.Remove(group);
        }
    }

    private void UncheckOthersInGroup()
    {
        string? group = GroupName;
        if (group == null)
        {
            if (Parent is not { } parent)
            {
                return;
            }

            // Iterated by index and bounds-checked, as unchecking runs user code that may change the children.
            var siblings = parent.Children;
            for (int i = siblings.Count - 1; i >= 0; i--)
            {
                if (i < siblings.Count && siblings[i] is RadioButton rb && rb != this && rb.GroupName == null)
                {
                    rb.IsChecked = false;
                }
            }
            return;
        }

        if (!_groups.TryGetValue(group, out var list))
        {
            return;
        }

        var scope = GetGroupScope(this);
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (i >= list.Count)
            {
                continue;
            }

            if (!list[i].TryGetTarget(out var rb))
            {
                list.RemoveAt(i);
            }
            else if (rb != this && GetGroupScope(rb) == scope)
            {
                rb.IsChecked = false;
            }
        }
    }

    // The window's tree root while displayed; null (the shared scope) otherwise.
    private static VisualNode? GetGroupScope(VisualNode node)
    {
        if (!node.IsAttachedToVisualTree)
        {
            return null;
        }

        while (node.Parent != null)
        {
            node = node.Parent;
        }
        return node;
    }
}
