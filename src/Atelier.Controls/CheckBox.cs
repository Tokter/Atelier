using System;
using System.Collections.Generic;
using Atelier.Core.Animation;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Controls;

public class CheckBox : Control
{
    public static readonly BindableProperty<bool> IsCheckedProperty =
        BindableProperty.Register<CheckBox, bool>(
            nameof(IsChecked),
            false,
            (s, o, n) => ((CheckBox)s).OnIsCheckedChanged(o, n)
        );

    public static readonly BindableProperty<object?> ContentProperty =
        BindableProperty.Register<CheckBox, object?>(
            nameof(Content),
            null,
            (s, o, n) => ((CheckBox)s).OnContentChanged(o, n)
        );

    public bool IsChecked
    {
        get => GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public float CheckAnimationProgress { get; private set; } = 0f;

    public event EventHandler<bool>? CheckedChanged;

    private static AnimationClock? _clock;
    public static void SetGlobalAnimationClock(AnimationClock clock) => _clock = clock;

    public CheckBox()
    {
        IsFocusable = true;
        Padding = new Thickness(8, 6);
    }

    public CheckBox(string text) : this()
    {
        Content = new TextBlock(text);
    }

    private void OnContentChanged(object? oldContent, object? newContent)
    {
        if (oldContent is UIElement oldElement) RemoveChild(oldElement);
        if (newContent is string str)
        {
            AddChild(new TextBlock(str));
        }
        else if (newContent is UIElement newElement)
        {
            AddChild(newElement);
        }
        InvalidateMeasure();
    }

    protected virtual void OnIsCheckedChanged(bool oldValue, bool newValue)
    {
        float target = newValue ? 1f : 0f;
        if (_clock == null)
        {
            CheckAnimationProgress = target;
            InvalidateVisual();
        }
        else
        {
            var anim = new FloatAnimation(
                CheckAnimationProgress,
                target,
                TimeSpan.FromMilliseconds(180),
                p =>
                {
                    CheckAnimationProgress = p;
                    InvalidateVisual();
                },
                Easing.EmphasizedDecelerate
            );

            _clock.Add(anim);
        }
        CheckedChanged?.Invoke(this, newValue);
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        bool wasPressed = IsPressed;
        base.OnPointerReleased(e);
        e.Handled = true;

        if (wasPressed && IsHovered && IsEnabled)
        {
            IsChecked = !IsChecked;
        }
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsEnabled && (e.Key is Key.Space or Key.Enter))
        {
            IsChecked = !IsChecked;
            e.Handled = true;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        float boxSize = 20f;
        float spacing = 8f;

        if (Children.Count > 0 && Children[0] is UIElement child && child.Visibility != Visibility.Collapsed)
        {
            child.Measure(new Size(Math.Max(0, availableSize.Width - boxSize - spacing), availableSize.Height));
            return new Size(boxSize + spacing + child.DesiredSize.Width, Math.Max(boxSize, child.DesiredSize.Height));
        }

        return new Size(boxSize, boxSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        float boxSize = 20f;
        float spacing = 8f;

        if (Children.Count > 0 && Children[0] is UIElement child && child.Visibility != Visibility.Collapsed)
        {
            float childY = (finalSize.Height - child.DesiredSize.Height) * 0.5f;
            child.Arrange(new Rect(boxSize + spacing, childY, child.DesiredSize.Width, child.DesiredSize.Height));
        }

        return finalSize;
    }
}

public class RadioButton : CheckBox
{
    private static readonly Dictionary<string, List<WeakReference<RadioButton>>> _groups = new();

    public static readonly BindableProperty<string?> GroupNameProperty =
        BindableProperty.Register<RadioButton, string?>(
            nameof(GroupName),
            null,
            (s, o, n) => ((RadioButton)s).OnGroupNameChanged(o, n)
        );

    public string? GroupName
    {
        get => GetValue(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }

    public RadioButton()
    {
        RegisterInGroup(GroupName);
    }

    public RadioButton(string text) : this()
    {
        Content = new TextBlock(text);
    }

    private void OnGroupNameChanged(string? oldGroup, string? newGroup)
    {
        UnregisterFromGroup(oldGroup);
        RegisterInGroup(newGroup);
        if (IsChecked)
        {
            DeselectSiblings();
        }
    }

    protected override void OnIsCheckedChanged(bool oldValue, bool newValue)
    {
        base.OnIsCheckedChanged(oldValue, newValue);
        if (newValue)
        {
            RegisterInGroup(GroupName);
            DeselectSiblings();
        }
    }

    public override void OnPointerReleased(PointerEventArgs e)
    {
        bool wasPressed = IsPressed;
        base.OnPointerReleased(e);
        e.Handled = true;

        if (wasPressed && IsHovered && IsEnabled)
        {
            if (!IsChecked)
            {
                IsChecked = true;
            }
        }
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (IsEnabled && (e.Key is Key.Space or Key.Enter))
        {
            if (!IsChecked)
            {
                IsChecked = true;
            }
            e.Handled = true;
        }
    }

    private void RegisterInGroup(string? group)
    {
        string key = group ?? string.Empty;
        if (!_groups.TryGetValue(key, out var list))
        {
            list = [];
            _groups[key] = list;
        }

        list.RemoveAll(r => !r.TryGetTarget(out _));

        bool exists = false;
        foreach (var wr in list)
        {
            if (wr.TryGetTarget(out var rb) && rb == this)
            {
                exists = true;
                break;
            }
        }

        if (!exists)
        {
            list.Add(new WeakReference<RadioButton>(this));
        }
    }

    private void UnregisterFromGroup(string? group)
    {
        string key = group ?? string.Empty;
        if (_groups.TryGetValue(key, out var list))
        {
            list.RemoveAll(r => !r.TryGetTarget(out var rb) || rb == this);
        }
    }

    private void DeselectSiblings()
    {
        string key = GroupName ?? string.Empty;
        if (!_groups.TryGetValue(key, out var list)) return;

        list.RemoveAll(r => !r.TryGetTarget(out _));

        foreach (var wr in list)
        {
            if (wr.TryGetTarget(out var rb) && rb != this)
            {
                rb.IsChecked = false;
            }
        }
    }
}
