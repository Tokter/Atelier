using System;
using System.Threading;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Threading;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// Shows tooltips: set <see cref="ToolTipProperty"/> on any element (<c>.ToolTip("Save")</c> in markup) and the service
/// opens a <see cref="Controls.ToolTip"/> next to it while the pointer rests on it.
/// </summary>
/// <remarks>
/// <para>
/// A tooltip opens after <see cref="InitialShowDelay"/> (or the element's <see cref="ShowDelayProperty"/>). Moving to
/// another element with a tooltip within <see cref="BetweenShowDelay"/> after one closed opens it at once. A plain
/// (string) tooltip closes when the pointer leaves the element; an interactive (rich) tooltip stays open while the
/// pointer moves onto it and closes <see cref="InteractiveCloseDelay"/> after the pointer left both. Pressing a mouse
/// button, turning the wheel or pressing Escape closes a tooltip; after a press on the element its tooltip only opens
/// again once the pointer has left it.
/// </para>
/// <para>
/// One tooltip is shown at a time, for all windows. The platform layer reports the pointer and input through
/// <see cref="OnPointerOver"/>, <see cref="OnPointerPressed"/>, <see cref="OnPointerWheel"/> and <see cref="Close"/>.
/// Delays use <see cref="TimeProvider"/>, which tests can replace.
/// </para>
/// </remarks>
public static class ToolTipService
{
    #region Attached properties

    /// <summary>
    /// Identifies the ToolTip attached property: what an element's tooltip shows. A string makes a plain tooltip; an
    /// element (such as a <see cref="RichToolTip"/>) or any other object makes a rich tooltip. <c>null</c> (the
    /// default) shows none.
    /// </summary>
    public static readonly BindableProperty<object?> ToolTipProperty =
        BindableProperty.RegisterAttached<ToolTipOwner, UIElement, object?>("ToolTip", null, OnToolTipChanged);

    /// <summary>
    /// Identifies the Placement attached property: where the tooltip appears. The default is
    /// <see cref="PlacementMode.Bottom"/>; <see cref="PlacementMode.Pointer"/> places it below the pointer.
    /// </summary>
    public static readonly BindableProperty<PlacementMode> PlacementProperty =
        BindableProperty.RegisterAttached<ToolTipOwner, UIElement, PlacementMode>("ToolTipPlacement", PlacementMode.Bottom);

    /// <summary>
    /// Identifies the ShowDelay attached property: how long the pointer must rest on the element before its tooltip
    /// opens. <c>null</c> (the default) uses <see cref="InitialShowDelay"/>.
    /// </summary>
    public static readonly BindableProperty<TimeSpan?> ShowDelayProperty =
        BindableProperty.RegisterAttached<ToolTipOwner, UIElement, TimeSpan?>("ToolTipShowDelay", null);

    /// <summary>Identifies the IsEnabled attached property: whether the element's tooltip is shown. The default is <c>true</c>.</summary>
    public static readonly BindableProperty<bool> IsEnabledProperty =
        BindableProperty.RegisterAttached<ToolTipOwner, UIElement, bool>("ToolTipIsEnabled", true);

    /// <summary>
    /// Identifies the ShowOnDisabled attached property: whether the tooltip is shown while the element is disabled,
    /// for example to explain why. The default is <c>false</c>.
    /// </summary>
    public static readonly BindableProperty<bool> ShowOnDisabledProperty =
        BindableProperty.RegisterAttached<ToolTipOwner, UIElement, bool>("ToolTipShowOnDisabled", false);

    /// <summary>
    /// Identifies the IsInteractive attached property: whether the tooltip receives pointer input and stays open while
    /// the pointer is over it. <c>null</c> (the default) makes rich tooltips interactive and plain ones not.
    /// </summary>
    public static readonly BindableProperty<bool?> IsInteractiveProperty =
        BindableProperty.RegisterAttached<ToolTipOwner, UIElement, bool?>("ToolTipIsInteractive", null);

    // Attached properties need an owner type for their registry key; ToolTipService is static.
    private sealed class ToolTipOwner;

    /// <summary>Gets the tooltip of <paramref name="element"/>.</summary>
    public static object? GetToolTip(UIElement element) => element.GetValue(ToolTipProperty);

    /// <summary>
    /// Gets what the tooltip of <paramref name="element"/> shows right now: its <see cref="ToolTipProperty"/>, or for a
    /// <see cref="TextBlock"/> with <see cref="TextBlock.ShowsToolTipWhenTrimmed"/>, its full text while it is trimmed.
    /// </summary>
    public static object? GetEffectiveToolTip(UIElement element) =>
        GetToolTip(element) ?? (element is TextBlock { ShowsToolTipWhenTrimmed: true, IsTextTrimmed: true } text ? text.Text : null);

    /// <summary>Sets the tooltip of <paramref name="element"/>: a string, an element or any object; <c>null</c> removes it.</summary>
    public static void SetToolTip(UIElement element, object? toolTip) => element.SetValue(ToolTipProperty, toolTip);

    /// <summary>Gets where the tooltip of <paramref name="element"/> appears.</summary>
    public static PlacementMode GetPlacement(UIElement element) => element.GetValue(PlacementProperty);

    /// <summary>Sets where the tooltip of <paramref name="element"/> appears.</summary>
    public static void SetPlacement(UIElement element, PlacementMode placement) => element.SetValue(PlacementProperty, placement);

    /// <summary>Gets the show delay of <paramref name="element"/>'s tooltip; <c>null</c> uses <see cref="InitialShowDelay"/>.</summary>
    public static TimeSpan? GetShowDelay(UIElement element) => element.GetValue(ShowDelayProperty);

    /// <summary>Sets the show delay of <paramref name="element"/>'s tooltip; <c>null</c> uses <see cref="InitialShowDelay"/>.</summary>
    public static void SetShowDelay(UIElement element, TimeSpan? delay) => element.SetValue(ShowDelayProperty, delay);

    /// <summary>Gets whether <paramref name="element"/>'s tooltip is shown.</summary>
    public static bool GetIsEnabled(UIElement element) => element.GetValue(IsEnabledProperty);

    /// <summary>Sets whether <paramref name="element"/>'s tooltip is shown.</summary>
    public static void SetIsEnabled(UIElement element, bool isEnabled) => element.SetValue(IsEnabledProperty, isEnabled);

    /// <summary>Gets whether <paramref name="element"/>'s tooltip is shown while the element is disabled.</summary>
    public static bool GetShowOnDisabled(UIElement element) => element.GetValue(ShowOnDisabledProperty);

    /// <summary>Sets whether <paramref name="element"/>'s tooltip is shown while the element is disabled.</summary>
    public static void SetShowOnDisabled(UIElement element, bool show) => element.SetValue(ShowOnDisabledProperty, show);

    /// <summary>Gets whether <paramref name="element"/>'s tooltip is interactive; <c>null</c> decides by the content.</summary>
    public static bool? GetIsInteractive(UIElement element) => element.GetValue(IsInteractiveProperty);

    /// <summary>Sets whether <paramref name="element"/>'s tooltip is interactive; <c>null</c> decides by the content.</summary>
    public static void SetIsInteractive(UIElement element, bool? isInteractive) => element.SetValue(IsInteractiveProperty, isInteractive);

    #endregion

    #region Settings

    /// <summary>Gets or sets how long the pointer must rest on an element before its tooltip opens. The default is 500 ms.</summary>
    public static TimeSpan InitialShowDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Gets or sets how long after a tooltip closed the next one opens without delay, so moving along a toolbar shows
    /// each tooltip at once. The default is 1 s.
    /// </summary>
    public static TimeSpan BetweenShowDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets how long an interactive tooltip stays open after the pointer left both it and its element, so the
    /// pointer can cross the gap between them. The default is 300 ms.
    /// </summary>
    public static TimeSpan InteractiveCloseDelay { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Gets or sets whether moving the keyboard focus to an element (with Tab) shows its tooltip. The default is
    /// <c>false</c>.
    /// </summary>
    public static bool ShowOnKeyboardFocus { get; set; }

    /// <summary>Gets or sets the time source of the delays; tests can use a fake one. The default is <see cref="TimeProvider.System"/>.</summary>
    public static TimeProvider TimeProvider
    {
        get => _timeProvider;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Close();
            _timeProvider = value;
            _closedTimestamp = long.MinValue; // timestamps of another provider don't compare
        }
    }

    private static TimeProvider _timeProvider = TimeProvider.System;

    #endregion

    #region State

    private static ToolTip? _toolTip;
    private static UIElement? _owner;          // the element whose tooltip is open
    private static UIElement? _pending;        // the element whose tooltip is about to open
    private static UIElement? _suppressed;     // pressed while hovered: no tooltip until the pointer leaves it
    private static UIElement? _pointerTarget;  // the element with a tooltip under the pointer
    private static bool _pointerOverToolTip;
    private static bool _openedByKeyboard;
    private static long _closedTimestamp = long.MinValue;
    private static ITimer? _timer;
    private static int _timerGeneration;

    static ToolTipService()
    {
        FocusManager.FocusChanged += OnFocusChanged;
    }

    /// <summary>Gets the element whose tooltip is open, or <c>null</c>.</summary>
    public static UIElement? CurrentOwner => _owner;

    /// <summary>Gets the open tooltip popup, or <c>null</c> when none is open.</summary>
    public static ToolTip? CurrentToolTip => _owner != null ? _toolTip : null;

    /// <summary>Occurs after a tooltip opened; the argument is the element it belongs to.</summary>
    public static event EventHandler<UIElement>? ToolTipOpened;

    /// <summary>Occurs after a tooltip closed; the argument is the element it belonged to.</summary>
    public static event EventHandler<UIElement>? ToolTipClosed;

    #endregion

    #region Platform hooks

    /// <summary>
    /// Reports the element under the pointer: the deepest hit element of the window's tree, or of an open popup.
    /// Called by the platform layer on every pointer move, with <c>null</c> when the pointer leaves the window.
    /// </summary>
    /// <param name="element">The element under the pointer, or <c>null</c>.</param>
    public static void OnPointerOver(UIElement? element)
    {
        // Over the open (interactive) tooltip itself: keep it open.
        if (_toolTip != null && _owner != null && element != null && IsInside(element, _toolTip))
        {
            if (!_pointerOverToolTip)
            {
                _pointerOverToolTip = true;
                CancelTimer();
            }
            return;
        }

        bool leftToolTip = _pointerOverToolTip;
        _pointerOverToolTip = false;

        var target = FindTarget(element);
        if (_suppressed != null && target != _suppressed)
        {
            _suppressed = null;
        }

        if (!leftToolTip && target == _pointerTarget && (target == null || target == _owner || target == _pending))
        {
            // Nothing changed; if an interactive tooltip's close was scheduled because the pointer moved onto the
            // tooltip and back, the owner is hovered again: keep it open.
            if (target != null && target == _owner)
            {
                CancelTimer();
            }
            return;
        }

        _pointerTarget = target;

        if (_owner != null && target != _owner)
        {
            if (target == null && _toolTip!.IsInteractive && !_openedByKeyboard)
            {
                // Give the pointer time to cross the gap onto the tooltip.
                Schedule(InteractiveCloseDelay, Close);
                return;
            }
            Close();
        }

        if (target == null || target == _suppressed)
        {
            CancelPending();
            return;
        }

        if (target == _owner)
        {
            CancelTimer();
            return;
        }

        ScheduleOpen(target, byKeyboard: false);
    }

    /// <summary>
    /// Reports a mouse button press. Closes the tooltip unless the press is inside an interactive one, and keeps the
    /// pressed element's tooltip from reopening until the pointer leaves it. Called by the platform layer.
    /// </summary>
    public static void OnPointerPressed()
    {
        if (_pointerOverToolTip && _owner != null)
        {
            return;
        }

        _suppressed = _pointerTarget ?? _owner;
        CancelPending();
        Close();
    }

    /// <summary>Reports a mouse wheel turn; closes the tooltip. Called by the platform layer.</summary>
    public static void OnPointerWheel()
    {
        if (_pointerOverToolTip)
        {
            return;
        }

        _suppressed = _pointerTarget ?? _owner;
        CancelPending();
        Close();
    }

    /// <summary>
    /// Closes the open tooltip, if any, and cancels one about to open. Called when a window deactivates or closes, and
    /// usable from app code.
    /// </summary>
    public static void Close()
    {
        CancelTimer();
        _pending = null;
        if (_owner == null)
        {
            return;
        }

        var owner = _owner;
        _owner = null;
        _pointerOverToolTip = false;
        _openedByKeyboard = false;
        owner.DetachedFromVisualTree -= OnOwnerDetached;
        _closedTimestamp = TimeProvider.GetTimestamp();

        var toolTip = _toolTip!;
        toolTip.IsOpen = false;
        toolTip.Content = null;
        toolTip.PlacementTarget = null;
        toolTip.DataContext = null;
        toolTip.DetachFromHost();

        ToolTipClosed?.Invoke(null, owner);
    }

    /// <summary>
    /// Opens the tooltip of <paramref name="element"/> at once, closing any other. Does nothing if the element has no
    /// tooltip to show.
    /// </summary>
    public static void Show(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        CancelTimer();
        Open(element, byKeyboard: false);
    }

    #endregion

    #region Opening and closing

    private static void ScheduleOpen(UIElement target, bool byKeyboard)
    {
        _pending = target;
        bool warm = _closedTimestamp != long.MinValue && TimeProvider.GetElapsedTime(_closedTimestamp) < BetweenShowDelay;
        var delay = warm ? TimeSpan.Zero : GetShowDelay(target) ?? InitialShowDelay;
        Schedule(delay, () =>
        {
            if (_pending == target)
            {
                _pending = null;
                Open(target, byKeyboard);
            }
        });
    }

    private static void CancelPending()
    {
        if (_pending != null)
        {
            _pending = null;
            CancelTimer();
        }
    }

    private static void Open(UIElement target, bool byKeyboard)
    {
        if (byKeyboard && !(ShowOnKeyboardFocus && FocusManager.IsFocusVisible && target.IsFocused))
        {
            return;
        }

        var content = GetEffectiveToolTip(target);
        if (content == null || !CanShow(target) || !target.IsAttachedToVisualTree)
        {
            return;
        }

        if (_owner != null)
        {
            Close();
        }

        var toolTip = _toolTip ??= CreateToolTip();
        toolTip.Content = content;
        toolTip.IsInteractive = GetIsInteractive(target) ?? toolTip.IsRich;
        toolTip.Placement = byKeyboard && GetPlacement(target) == PlacementMode.Pointer ? PlacementMode.Bottom : GetPlacement(target);
        toolTip.PlacementTarget = target;
        toolTip.DataContext = target.DataContext;

        // The tooltip is its own tree, attached to the target's window, so styles, inherited values and attach events
        // work as in the window's tree.
        toolTip.AttachToHost(target.Host);
        toolTip.ApplyStylesToTree();

        _owner = target;
        _openedByKeyboard = byKeyboard;
        target.DetachedFromVisualTree += OnOwnerDetached;
        toolTip.IsOpen = true;
        ToolTipOpened?.Invoke(null, target);
    }

    private static ToolTip CreateToolTip()
    {
        var toolTip = new ToolTip();

        // PopupManager closes transient popups itself on Escape and on presses outside them; follow along, and keep the
        // tooltip from reopening until the pointer leaves its element.
        toolTip.Closed += (_, _) =>
        {
            if (_owner != null)
            {
                _suppressed = _owner;
                Close();
            }
        };
        return toolTip;
    }

    private static void OnOwnerDetached(object? sender, EventArgs e) => Close();

    private static void OnToolTipChanged(BindableObject sender, object? oldValue, object? newValue)
    {
        if (sender != _owner)
        {
            return;
        }

        if (newValue == null)
        {
            Close();
        }
        else
        {
            // Update the open tooltip in place, e.g. a bound status text.
            _toolTip!.Content = newValue;
            _toolTip.IsInteractive = GetIsInteractive(_owner) ?? _toolTip.IsRich;
            _toolTip.ApplyStylesToTree();
        }
    }

    private static void OnFocusChanged(UIElement? oldFocus, UIElement? newFocus)
    {
        if (_openedByKeyboard && _owner != null && _owner == oldFocus)
        {
            Close();
        }

        if (ShowOnKeyboardFocus && newFocus != null && GetEffectiveToolTip(newFocus) != null)
        {
            // Whether the focus came from the keyboard is checked when the delay ends (IsFocusVisible).
            ScheduleOpen(newFocus, byKeyboard: true);
        }
    }

    // The nearest element (the hit element or an ancestor) with a tooltip that can be shown.
    private static UIElement? FindTarget(UIElement? element)
    {
        for (var node = element; node != null; node = node.Parent as UIElement)
        {
            if (node is ToolTip)
            {
                return null;
            }

            if (GetEffectiveToolTip(node) != null && GetIsEnabled(node))
            {
                return CanShow(node) ? node : null;
            }
        }
        return null;
    }

    private static bool CanShow(UIElement element) =>
        GetIsEnabled(element) && element.Visibility == Visibility.Visible && (element.IsEnabled || GetShowOnDisabled(element));

    private static bool IsInside(UIElement element, UIElement ancestor)
    {
        for (VisualNode? node = element; node != null; node = node.Parent)
        {
            if (node == ancestor)
            {
                return true;
            }
        }
        return false;
    }

    #endregion

    #region Timer

    private static void Schedule(TimeSpan delay, Action action)
    {
        CancelTimer();
        if (delay <= TimeSpan.Zero)
        {
            action();
            return;
        }

        // Timer callbacks come from a background thread; the action runs on the UI thread, unless it was superseded.
        int generation = _timerGeneration;
        _timer = TimeProvider.CreateTimer(_ => Dispatcher.UIThread.Post(() =>
        {
            if (generation == _timerGeneration)
            {
                _timer?.Dispose();
                _timer = null;
                action();
            }
        }), null, delay, Timeout.InfiniteTimeSpan);
    }

    private static void CancelTimer()
    {
        _timerGeneration++;
        _timer?.Dispose();
        _timer = null;
    }

    #endregion
}
