using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Threading;
using Atelier.Core.Tree;

namespace Atelier.Controls;

/// <summary>
/// An item of a <see cref="Menu"/> or <see cref="ContextMenu"/>: a header with an optional icon and keyboard shortcut
/// that runs a command, or opens a submenu of more items.
/// </summary>
/// <remarks>
/// <para>
/// The header can mark an access key with an underscore (<c>"_Save"</c>); it is underlined while the Alt key shows the
/// access keys, and pressing the letter picks the item. The shortcut shown on the right is
/// <see cref="InputGestureText"/>, or else the keybinding of <see cref="ButtonBase.Command"/> found with
/// <see cref="KeybindingManager"/> (updated when keybindings change). The item is disabled while the command can't
/// execute, and hidden then with <see cref="HideWhenDisabled"/>.
/// </para>
/// <para>
/// Items with <see cref="Items"/> (or <see cref="ItemsSource"/>) open a submenu: on click, on hover after
/// <see cref="SubmenuShowDelay"/>, or with the Right key. Other items run their command and close the menu, unless
/// <see cref="StaysOpenOnClick"/>. <see cref="IsCheckable"/> items toggle <see cref="IsChecked"/> and show a check mark;
/// items with a <see cref="GroupName"/> behave like radio buttons.
/// </para>
/// </remarks>
public class MenuItem : ButtonBase
{
    #region Properties

    /// <summary>Identifies the <see cref="Header"/> property.</summary>
    public static readonly BindableProperty<object?> HeaderProperty =
        BindableProperty.Register<MenuItem, object?>(nameof(Header), null, (s, o, n) => ((MenuItem)s).RebuildHeader());

    /// <summary>Identifies the <see cref="Icon"/> property.</summary>
    public static readonly BindableProperty<object?> IconProperty =
        BindableProperty.Register<MenuItem, object?>(nameof(Icon), null, (s, o, n) => ((MenuItem)s).OnIconChanged());

    /// <summary>Identifies the <see cref="InputGestureText"/> property.</summary>
    public static readonly BindableProperty<string?> InputGestureTextProperty =
        BindableProperty.Register<MenuItem, string?>(nameof(InputGestureText), null, (s, o, n) => ((MenuItem)s).UpdateGestureText());

    /// <summary>Identifies the <see cref="IsCheckable"/> property.</summary>
    public static readonly BindableProperty<bool> IsCheckableProperty =
        BindableProperty.Register<MenuItem, bool>(nameof(IsCheckable), false, (s, o, n) => ((MenuItem)s).OnCheckStateChanged());

    /// <summary>Identifies the <see cref="IsChecked"/> property.</summary>
    public static readonly BindableProperty<bool> IsCheckedProperty =
        BindableProperty.Register<MenuItem, bool>(nameof(IsChecked), false, (s, o, n) => ((MenuItem)s).OnIsCheckedChanged(n));

    /// <summary>Identifies the <see cref="GroupName"/> property.</summary>
    public static readonly BindableProperty<string?> GroupNameProperty =
        BindableProperty.Register<MenuItem, string?>(nameof(GroupName), null, (s, o, n) => ((MenuItem)s).OnCheckStateChanged());

    /// <summary>Identifies the <see cref="StaysOpenOnClick"/> property.</summary>
    public static readonly BindableProperty<bool> StaysOpenOnClickProperty =
        BindableProperty.Register<MenuItem, bool>(nameof(StaysOpenOnClick), false);

    /// <summary>Identifies the <see cref="HideWhenDisabled"/> property.</summary>
    public static readonly BindableProperty<bool> HideWhenDisabledProperty =
        BindableProperty.Register<MenuItem, bool>(nameof(HideWhenDisabled), false, (s, o, n) => ((MenuItem)s).UpdateHidden());

    /// <summary>Identifies the <see cref="ItemsSource"/> property.</summary>
    public static readonly BindableProperty<IEnumerable?> ItemsSourceProperty =
        BindableProperty.Register<MenuItem, IEnumerable?>(nameof(ItemsSource), null, (s, o, n) => ((MenuItem)s)._presenter.ItemsSource = n);

    /// <summary>Identifies the <see cref="IsSubmenuOpen"/> property.</summary>
    public static readonly BindableProperty<bool> IsSubmenuOpenProperty =
        BindableProperty.Register<MenuItem, bool>(nameof(IsSubmenuOpen), false, (s, o, n) => ((MenuItem)s).OnIsSubmenuOpenChanged(n), options: PropertyOptions.AffectsRender);

    /// <summary>The style key of the secondary parts of an item: the shortcut text and the submenu arrow (MD3: on-surface-variant).</summary>
    public const string SecondaryStyleKey = "MenuItemSecondary";

    /// <summary>
    /// Gets or sets how long the pointer rests on an item with a submenu before it opens (and on another item before an
    /// open submenu closes), for all menus. The default is 300 ms; zero opens at once.
    /// </summary>
    public static TimeSpan SubmenuShowDelay { get; set; } = TimeSpan.FromMilliseconds(300);

    // Layout (MD3 menu list items).
    private const float HorizontalPadding = 12f;
    private const float IconSize = 20f;
    private const float IconGap = 12f;
    private const float GestureGap = 32f;
    private const float ArrowSize = 20f;
    private const float ArrowGap = 8f;

    private readonly ContentControl _iconHost = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Icon _checkMark = new(MaterialIconKind.Check, IconSize) { Visibility = Visibility.Collapsed };
    private readonly ContentControl _headerHost = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _gesture = new() { StyleKey = SecondaryStyleKey, Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };
    private readonly Icon _arrow = new(MaterialIconKind.ArrowRight, ArrowSize) { StyleKey = SecondaryStyleKey, Visibility = Visibility.Collapsed };
    private readonly Popup _popup;
    private readonly MenuItemsPresenter _presenter = new();
    private DispatcherTimer? _hoverTimer;
    private bool _isObservingKeybindings;
    private bool _isUpdatingChecks;

    static MenuItem()
    {
        IsFocusableProperty.OverrideDefaultValue<MenuItem>(true);
        HorizontalContentAlignmentProperty.OverrideDefaultValue<MenuItem>(HorizontalAlignment.Stretch);
    }

    /// <summary>Initializes an empty menu item.</summary>
    public MenuItem()
    {
        _presenter.OwnerItem = this;
        _popup = new Popup
        {
            PlacementTarget = this,
            Placement = PlacementMode.Right,
            VerticalOffset = -8,
            StaysOpen = false,
            Child = _presenter,
        };
        _popup.Closed += (_, _) => OnPopupClosed();
        _presenter.Items.CollectionChanged += (_, _) => OnSubmenuItemsChanged();

        AddChild(_iconHost);
        AddChild(_checkMark);
        AddChild(_headerHost);
        AddChild(_gesture);
        AddChild(_arrow);
        AddChild(_popup);
        RebuildHeader();
    }

    /// <summary>Initializes a menu item with a header and an optional command.</summary>
    /// <param name="header">The header; an underscore marks the access key, e.g. <c>"_Open"</c>.</param>
    /// <param name="command">The command it runs.</param>
    public MenuItem(object? header, System.Windows.Input.ICommand? command = null) : this()
    {
        Header = header;
        Command = command;
    }

    /// <summary>Gets or sets the header: a string (with an optional <c>_</c> access key), an element, or any object.</summary>
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <summary>Gets or sets the icon before the header: a <see cref="MaterialIconKind"/> or an element; <c>null</c> for none.</summary>
    public object? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>
    /// Gets or sets the shortcut text shown on the right; <c>null</c> (the default) shows the keybinding of the command, if
    /// it has one.
    /// </summary>
    public string? InputGestureText { get => GetValue(InputGestureTextProperty); set => SetValue(InputGestureTextProperty, value); }

    /// <summary>Gets or sets whether a click toggles <see cref="IsChecked"/>. The default is <c>false</c>.</summary>
    public bool IsCheckable { get => GetValue(IsCheckableProperty); set => SetValue(IsCheckableProperty, value); }

    /// <summary>Gets or sets whether the item is checked; checked items show a check mark (or a dot in a radio group).</summary>
    public bool IsChecked { get => GetValue(IsCheckedProperty); set => SetValue(IsCheckedProperty, value); }

    /// <summary>
    /// Gets or sets the radio group: checking a checkable item unchecks the items of the same group in the same menu, and
    /// clicking a checked one keeps it checked. <c>null</c> (the default) for none.
    /// </summary>
    public string? GroupName { get => GetValue(GroupNameProperty); set => SetValue(GroupNameProperty, value); }

    /// <summary>Gets or sets whether the menu stays open after the item is clicked. The default is <c>false</c>.</summary>
    public bool StaysOpenOnClick { get => GetValue(StaysOpenOnClickProperty); set => SetValue(StaysOpenOnClickProperty, value); }

    /// <summary>Gets or sets whether the item is hidden instead of grayed out while it is disabled. The default is <c>false</c>.</summary>
    public bool HideWhenDisabled { get => GetValue(HideWhenDisabledProperty); set => SetValue(HideWhenDisabledProperty, value); }

    /// <summary>Gets or sets the collection the submenu's items are generated from.</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>Gets or sets whether the submenu is open.</summary>
    public bool IsSubmenuOpen { get => GetValue(IsSubmenuOpenProperty); set => SetValue(IsSubmenuOpenProperty, value); }

    /// <summary>Gets the items of the submenu: menu items, separators or data items.</summary>
    public ObservableCollection<object> Items => _presenter.Items;

    /// <summary>Gets whether the item has a submenu.</summary>
    public bool HasItems => _presenter.Items.Count > 0;

    /// <summary>Gets the item list of the submenu.</summary>
    public MenuItemsPresenter Submenu => _presenter;

    /// <summary>Gets the submenu's popup.</summary>
    public Popup SubmenuPopup => _popup;

    /// <summary>Gets the root of the menu this item belongs to.</summary>
    public IMenuRoot? Root { get; private set; }

    /// <summary>Gets the item whose submenu this item is in, or <c>null</c> at the top level.</summary>
    public MenuItem? ParentItem { get; private set; }

    /// <summary>Gets the data item this menu item was generated for, or <c>null</c>.</summary>
    public object? DataItem { get; private set; }

    /// <summary>Gets whether the item is a header of a <see cref="Menu"/> bar.</summary>
    public bool IsTopLevel => ParentItem == null && Root is Menu;

    /// <summary>Gets the displayed shortcut text, or an empty string.</summary>
    public string GestureText => _gesture.Text;

    /// <summary>Gets the header's text block when the header is text, otherwise <c>null</c>.</summary>
    public TextBlock? HeaderTextBlock { get; private set; }

    /// <summary>Gets the index of the access key in <see cref="HeaderTextBlock"/>'s text, or -1.</summary>
    public int AccessKeyIndex { get; private set; } = -1;

    /// <summary>Gets the key that picks the item in its menu: the access key, else the header's first letter or digit.</summary>
    public char? AccessKey { get; private set; }

    /// <summary>Gets whether the item has a marked access key (an underscore in the header).</summary>
    public bool HasExplicitAccessKey => AccessKeyIndex >= 0;

    /// <summary>Gets whether a checked item with an icon shows the check as a background behind the icon.</summary>
    public bool ShowsCheckBehindIcon => IsCheckable && IsChecked && Icon != null;

    /// <summary>Gets the bounds of the icon column, in the item's coordinates.</summary>
    public Rect IconBounds { get; private set; }

    /// <summary>Occurs when <see cref="IsChecked"/> changes, with the new value.</summary>
    public event EventHandler<bool>? CheckedChanged;

    /// <summary>Occurs after the submenu opened.</summary>
    public event EventHandler? SubmenuOpened;

    /// <summary>Occurs after the submenu closed.</summary>
    public event EventHandler? SubmenuClosed;

    #endregion

    #region Tree

    internal void AttachToMenu(IMenuRoot? root, MenuItem? parent, object? dataItem)
    {
        Root = root;
        ParentItem = parent;
        if (dataItem != null) DataItem = dataItem;
        _presenter.SetRoot(root);

        bool topLevel = IsTopLevel;
        _popup.Placement = topLevel ? PlacementMode.Bottom : PlacementMode.Right;
        _popup.VerticalOffset = topLevel ? 0 : -8;
        RebuildHeader();
        UpdateGestureText();
        UpdateParts();
    }

    internal void OnSubmenuItemsChanged()
    {
        ClickMode = HasItems ? ClickMode.Press : ClickMode.Release;
        UpdateParts();
        ParentPresenter?.UpdateColumns();
    }

    private MenuItemsPresenter? ParentPresenter => ParentItem?.Submenu ?? (Root as ContextMenu)?.Presenter;

    private IEnumerable<MenuItem> Siblings
    {
        get
        {
            if (Parent is not UIElement panel) yield break;
            foreach (var child in panel.Children)
            {
                if (child is MenuItem item && item != this) yield return item;
            }
        }
    }

    #endregion

    #region Header, icon and parts

    private void RebuildHeader()
    {
        HeaderTextBlock = null;
        AccessKeyIndex = -1;
        UIElement? content;

        if (Root?.ItemTemplate is { } template && DataItem != null)
        {
            content = template(DataItem);
            content.DataContext = DataItem;
        }
        else
        {
            switch (Header)
            {
                case null:
                    content = null;
                    break;
                case UIElement element:
                    content = element;
                    break;
                case var other:
                    string text = AccessText.Parse(other.ToString() ?? string.Empty, out int accessIndex);
                    AccessKeyIndex = accessIndex;
                    content = HeaderTextBlock = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
                    break;
            }
        }

        _headerHost.Content = content;
        AccessKey = FindAccessKey();
        InvalidateMeasure();
    }

    private char? FindAccessKey()
    {
        string? text = HeaderTextBlock?.Text ?? (Header as string);
        if (string.IsNullOrEmpty(text)) return null;
        if (AccessKeyIndex >= 0 && AccessKeyIndex < text.Length) return char.ToUpperInvariant(text[AccessKeyIndex]);
        foreach (char c in text)
        {
            if (char.IsLetterOrDigit(c)) return char.ToUpperInvariant(c);
        }
        return null;
    }

    private void OnIconChanged()
    {
        _iconHost.Content = Icon switch
        {
            MaterialIconKind kind => new Icon(kind, IconSize),
            var other => other,
        };
        UpdateParts();
        ParentPresenter?.UpdateColumns();
    }

    private void OnCheckStateChanged()
    {
        UpdateParts();
        ParentPresenter?.UpdateColumns();
    }

    private void OnIsCheckedChanged(bool isChecked)
    {
        UpdateParts();
        if (isChecked && IsCheckable && !string.IsNullOrEmpty(GroupName) && !_isUpdatingChecks)
        {
            foreach (var sibling in Siblings)
            {
                if (sibling.IsCheckable && sibling.GroupName == GroupName && sibling.IsChecked)
                {
                    sibling._isUpdatingChecks = true;
                    sibling.IsChecked = false;
                    sibling._isUpdatingChecks = false;
                }
            }
        }
        CheckedChanged?.Invoke(this, isChecked);
        InvalidateVisual();
    }

    private void UpdateParts()
    {
        bool topLevel = IsTopLevel;
        bool checkMark = IsCheckable && IsChecked && Icon == null;
        _checkMark.Kind = string.IsNullOrEmpty(GroupName) ? MaterialIconKind.Check : MaterialIconKind.FiberManualRecord;
        _checkMark.Size = string.IsNullOrEmpty(GroupName) ? IconSize : 14;
        _checkMark.Visibility = checkMark ? Visibility.Visible : Visibility.Collapsed;
        _iconHost.Visibility = Icon != null ? Visibility.Visible : Visibility.Collapsed;
        _arrow.Visibility = HasItems && !topLevel ? Visibility.Visible : Visibility.Collapsed;
        _gesture.Visibility = !topLevel && _gesture.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        MinHeight = topLevel ? 32 : 40;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void UpdateGestureText()
    {
        string text = InputGestureText ?? (Command is { } command
            ? KeybindingManager.GetGestureText(command, Root?.KeybindingGroup, CommandTargets()) ?? string.Empty
            : string.Empty);
        if (_gesture.Text == text) return;
        _gesture.Text = text;
        UpdateParts();
    }

    // The objects the command may belong to: the DataContexts from this item up, and around the menu's target.
    private IEnumerable<object?> CommandTargets()
    {
        object? last = null;
        for (VisualNode? node = this; node != null; node = node.Parent)
        {
            if (node is UIElement { DataContext: { } dc } && !ReferenceEquals(dc, last))
            {
                last = dc;
                yield return dc;
            }
        }
        for (VisualNode? node = Root?.MenuTarget; node != null; node = node.Parent)
        {
            if (node is UIElement { DataContext: { } dc } && !ReferenceEquals(dc, last))
            {
                last = dc;
                yield return dc;
            }
        }
    }

    private void UpdateHidden()
    {
        var visibility = HideWhenDisabled && !IsEnabled ? Visibility.Collapsed : Visibility.Visible;
        if (Visibility != visibility) Visibility = visibility;
    }

    /// <inheritdoc/>
    protected override void OnPropertyValueChanged<T>(BindableProperty<T> property, T oldValue, T newValue)
    {
        base.OnPropertyValueChanged(property, oldValue, newValue);
        if (ReferenceEquals(property, IsEnabledProperty))
        {
            UpdateHidden();
            _gesture.Opacity = _arrow.Opacity = IsEnabled ? 1f : 0.38f;
            if (!IsEnabled && IsSubmenuOpen) CloseSubmenu();
        }
        else if (ReferenceEquals(property, CommandProperty) || ReferenceEquals(property, DataContextProperty))
        {
            UpdateGestureText();
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        if (!_isObservingKeybindings)
        {
            KeybindingManager.KeybindingsChanged += OnKeybindingsChanged;
            MenuManager.AccessKeysVisibleChanged += OnAccessKeysVisibleChanged;
            _isObservingKeybindings = true;
        }
        UpdateGestureText();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree()
    {
        if (_isObservingKeybindings)
        {
            KeybindingManager.KeybindingsChanged -= OnKeybindingsChanged;
            MenuManager.AccessKeysVisibleChanged -= OnAccessKeysVisibleChanged;
            _isObservingKeybindings = false;
        }
        StopHoverTimer();
        base.OnDetachedFromVisualTree();
    }

    private void OnAccessKeysVisibleChanged(object? sender, EventArgs e) => InvalidateVisual();

    private void OnKeybindingsChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess()) UpdateGestureText();
        else Dispatcher.Post(UpdateGestureText);
    }

    #endregion

    #region Opening, closing and clicking

    /// <summary>
    /// Opens the submenu, closing the submenus of the other items at this level. With <paramref name="focusFirstItem"/>
    /// (the default after keyboard input) the keyboard focus moves to its first item, otherwise to the submenu itself.
    /// </summary>
    public void OpenSubmenu(bool? focusFirstItem = null)
    {
        if (!HasItems || !IsEnabled) return;
        StopHoverTimer();

        foreach (var sibling in Siblings)
        {
            if (sibling.IsSubmenuOpen) sibling.CloseSubmenu();
        }

        IsSubmenuOpen = true;
        if (focusFirstItem ?? FocusManager.IsFocusVisible)
        {
            _presenter.FocusFirstItem();
        }
        else if (!IsTopLevel || !IsFocused)
        {
            // Keep keyboard input in the menu without highlighting an item.
            _presenter.Focus();
        }
    }

    /// <summary>Closes the submenu and the submenus open in it.</summary>
    public void CloseSubmenu()
    {
        StopHoverTimer();
        foreach (var item in _presenter.MenuItems)
        {
            if (item.IsSubmenuOpen) item.CloseSubmenu();
        }
        IsSubmenuOpen = false;
    }

    private void OnIsSubmenuOpenChanged(bool open)
    {
        if (open)
        {
            _popup.IsOpen = true;
            if (IsTopLevel) (Root as Menu)?.OnTopLevelOpened(this);
            SubmenuOpened?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _popup.IsOpen = false;
        }
    }

    // The popup closed: by us, by a click outside, or by Escape (which closes one level).
    private void OnPopupClosed()
    {
        bool hadFocus = FocusManager.GetFocusedElement(this) is { } focused && (focused == _presenter || focused.IsDescendantOf(_presenter));
        foreach (var item in _presenter.MenuItems)
        {
            if (item.IsSubmenuOpen) item.CloseSubmenu();
        }
        IsSubmenuOpen = false;

        if (hadFocus && IsAttachedToVisualTree)
        {
            if (IsTopLevel && Root is Menu bar) bar.OnTopLevelClosed(this, returnFocus: true);
            else Focus();
        }
        else if (IsTopLevel && Root is Menu bar)
        {
            bar.OnTopLevelClosed(this, returnFocus: false);
        }
        SubmenuClosed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Picks the item as if it was clicked: opens its submenu, or runs its command and closes the menu.</summary>
    public void Activate(bool byKeyboard = false)
    {
        if (!IsEnabled) return;
        if (HasItems) OpenSubmenu(focusFirstItem: byKeyboard);
        else OnClick();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// An item with a submenu opens it (a menu bar header toggles it). Other items toggle their check state, close the
    /// menu (unless <see cref="StaysOpenOnClick"/>), raise <see cref="ButtonBase.Click"/> and run the command.
    /// </remarks>
    protected override void OnClick()
    {
        if (HasItems)
        {
            if (IsTopLevel && IsSubmenuOpen) CloseSubmenu();
            else OpenSubmenu();
            return;
        }

        if (IsCheckable)
        {
            IsChecked = !string.IsNullOrEmpty(GroupName) || !IsChecked;
        }

        if (!StaysOpenOnClick)
        {
            Root?.CloseMenu();
        }
        base.OnClick();
    }

    #endregion

    #region Pointer

    /// <inheritdoc/>
    /// <remarks>
    /// In a submenu the item takes the highlight, and after <see cref="SubmenuShowDelay"/> closes the other open
    /// submenus and opens its own. In a menu bar with an open menu, the header opens its menu at once.
    /// </remarks>
    public override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        if (!IsEnabled) return;

        if (IsTopLevel)
        {
            if (Root is Menu { OpenItem: { } open } && open != this && HasItems)
            {
                OpenSubmenu(focusFirstItem: false);
            }
            return;
        }

        Focus();
        StartHoverTimer();
    }

    /// <inheritdoc/>
    public override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        StopHoverTimer();
    }

    private void StartHoverTimer()
    {
        StopHoverTimer();
        if (SubmenuShowDelay <= TimeSpan.Zero)
        {
            OnHoverDelayElapsed();
            return;
        }
        _hoverTimer = new DispatcherTimer(SubmenuShowDelay, (_, _) =>
        {
            StopHoverTimer();
            OnHoverDelayElapsed();
        });
        _hoverTimer.Start();
    }

    private void StopHoverTimer()
    {
        _hoverTimer?.Stop();
        _hoverTimer = null;
    }

    private void OnHoverDelayElapsed()
    {
        if (!IsHovered && SubmenuShowDelay > TimeSpan.Zero) return;
        foreach (var sibling in Siblings)
        {
            if (sibling.IsSubmenuOpen) sibling.CloseSubmenu();
        }
        if (HasItems && !IsSubmenuOpen) OpenSubmenu(focusFirstItem: false);
    }

    #endregion

    #region Layout

    private bool IconColumn => IsTopLevel ? Icon != null : ParentPresenter?.HasIconColumn ?? (Icon != null || IsCheckable);

    private bool ArrowColumn => !IsTopLevel && (ParentPresenter?.HasSubmenuColumn ?? HasItems);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var infinite = new Size(float.PositiveInfinity, float.PositiveInfinity);
        _iconHost.Measure(infinite);
        _checkMark.Measure(infinite);
        _headerHost.Measure(infinite);
        _gesture.Measure(infinite);
        _arrow.Measure(infinite);

        float width = HorizontalPadding * 2 + _headerHost.DesiredSize.Width;
        if (IconColumn) width += IconSize + (IsTopLevel ? 8 : IconGap);
        if (_gesture.Visibility == Visibility.Visible) width += GestureGap + _gesture.DesiredSize.Width;
        if (ArrowColumn) width += ArrowGap + ArrowSize;

        float height = Math.Max(_headerHost.DesiredSize.Height, IconSize);
        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        float x = HorizontalPadding;
        float h = finalSize.Height;

        if (IconColumn)
        {
            IconBounds = new Rect(x, (h - IconSize) * 0.5f, IconSize, IconSize);
            ArrangeCentered(_iconHost, IconBounds);
            ArrangeCentered(_checkMark, IconBounds);
            x += IconSize + (IsTopLevel ? 8 : IconGap);
        }
        else
        {
            IconBounds = Rect.Zero;
        }

        float right = finalSize.Width - HorizontalPadding;
        if (ArrowColumn)
        {
            ArrangeCentered(_arrow, new Rect(right - ArrowSize, (h - ArrowSize) * 0.5f, ArrowSize, ArrowSize));
            right -= ArrowSize + ArrowGap;
        }
        if (_gesture.Visibility == Visibility.Visible)
        {
            var g = _gesture.DesiredSize;
            _gesture.Arrange(new Rect(right - g.Width, (h - g.Height) * 0.5f, g.Width, g.Height));
            right -= g.Width + GestureGap;
        }

        var header = _headerHost.DesiredSize;
        _headerHost.Arrange(new Rect(x, (h - header.Height) * 0.5f, Math.Max(0, Math.Min(header.Width, right - x)), header.Height));
        return finalSize;
    }

    private static void ArrangeCentered(UIElement element, Rect slot)
    {
        var size = element.DesiredSize;
        element.Arrange(new Rect(slot.X + (slot.Width - size.Width) * 0.5f, slot.Y + (slot.Height - size.Height) * 0.5f, size.Width, size.Height));
    }

    #endregion
}
