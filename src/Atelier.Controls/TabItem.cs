using System;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// A tab of a <see cref="TabControl"/>: its header in the tab strip (icon, header content and an optional close
/// button), and the content shown while it is selected.
/// </summary>
/// <remarks>
/// Add tab items to <see cref="ItemsControl.Items"/> directly, or bind <see cref="ItemsControl.ItemsSource"/> to data
/// items; the tab control then creates a tab item per data item, with the header from
/// <see cref="TabControl.HeaderTemplate"/> and the content from <see cref="TabControl.ContentTemplate"/>.
/// </remarks>
public class TabItem : Control
{
    /// <summary>Identifies the <see cref="Header"/> property.</summary>
    public static readonly BindableProperty<object?> HeaderProperty =
        BindableProperty.Register<TabItem, object?>(nameof(Header), null, (s, o, n) => ((TabItem)s).RebuildHeader());

    /// <summary>Identifies the <see cref="Icon"/> property.</summary>
    public static readonly BindableProperty<MaterialIconKind> IconProperty =
        BindableProperty.Register<TabItem, MaterialIconKind>(nameof(Icon), MaterialIconKind.None, (s, o, n) => ((TabItem)s).RebuildHeader());

    /// <summary>Identifies the <see cref="Content"/> property.</summary>
    public static readonly BindableProperty<object?> ContentProperty =
        BindableProperty.Register<TabItem, object?>(nameof(Content), null, (s, o, n) => ((TabItem)s).Owner?.OnTabContentChanged((TabItem)s));

    /// <summary>Identifies the <see cref="IsCloseable"/> property.</summary>
    public static readonly BindableProperty<bool?> IsCloseableProperty =
        BindableProperty.Register<TabItem, bool?>(nameof(IsCloseable), null, (s, o, n) => ((TabItem)s).UpdateCloseButton());

    /// <summary>The style key of the tabs' close buttons.</summary>
    public const string CloseButtonStyleKey = "TabCloseButton";

    private readonly DockPanel _row = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly StackPanel _label = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Button _closeButton;
    private bool _isSelected;

    static TabItem()
    {
        CursorProperty.OverrideDefaultValue<TabItem>(CursorType.Hand);
    }

    /// <summary>Initializes an empty tab.</summary>
    public TabItem()
    {
        _closeButton = new Button
        {
            Variant = ButtonVariant.Text,
            Content = new Icon(MaterialIconKind.Close, 18),
            Width = 24,
            Height = 24,
            MinWidth = 24,
            MinHeight = 24,
            Padding = new Thickness(3),
            Margin = new Thickness(8, 0, 0, 0),
            CornerRadius = new CornerRadius(12),
            VerticalAlignment = VerticalAlignment.Center,
            IsFocusable = false,
            Visibility = Visibility.Collapsed,
            StyleKey = CloseButtonStyleKey,
        };
        ToolTipService.SetToolTip(_closeButton, "Close tab");
        _closeButton.Click += (_, _) => Owner?.CloseTab(this);

        DockPanel.SetDock(_closeButton, Dock.Right);
        _row.Add(_closeButton);
        _row.Add(_label); // fills the rest
        AddChild(_row);
        RebuildHeader();
    }

    /// <summary>Initializes a tab with a header and content.</summary>
    /// <param name="header">The header: a string or an element.</param>
    /// <param name="content">The content shown while the tab is selected.</param>
    public TabItem(object? header, object? content = null) : this()
    {
        Header = header;
        Content = content;
    }

    /// <summary>
    /// Gets or sets the header: a string, an element, or any object (shown as text). A
    /// <see cref="TabControl.HeaderTemplate"/> replaces it.
    /// </summary>
    public object? Header { get => GetValue(HeaderProperty); set => SetValue(HeaderProperty, value); }

    /// <summary>Gets or sets an icon shown with the header; <see cref="MaterialIconKind.None"/> (the default) for none.</summary>
    public MaterialIconKind Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>Gets or sets the content shown while the tab is selected: an element or a view model.</summary>
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>
    /// Gets or sets whether the tab has a close button (and closes with a middle click); <c>null</c> (the default) follows
    /// <see cref="TabControl.AreTabsCloseable"/>.
    /// </summary>
    public bool? IsCloseable { get => GetValue(IsCloseableProperty); set => SetValue(IsCloseableProperty, value); }

    /// <summary>Gets whether the tab can be closed: <see cref="IsCloseable"/>, or the tab control's default.</summary>
    public bool IsEffectivelyCloseable => IsCloseable ?? Owner?.AreTabsCloseable ?? false;

    /// <summary>Gets whether this is the selected tab.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        internal set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            InvalidateVisual();
        }
    }

    /// <summary>Gets whether the tab is being dragged to a new place.</summary>
    public bool IsDragging { get; internal set; }

    /// <summary>Gets the tab control the tab belongs to, or <c>null</c>.</summary>
    public TabControl? Owner { get; private set; }

    /// <summary>Gets the item the tab stands for: a data item, or the tab itself when it was added as a tab item.</summary>
    public object? Item { get; private set; }

    /// <summary>Gets the close button.</summary>
    public Button CloseButton => _closeButton;

    /// <summary>
    /// Gets the bounds of the header's icon and text within the tab (without the close button), which the MD3 primary
    /// tabs' indicator spans.
    /// </summary>
    public Rect LabelBounds
    {
        get
        {
            var row = _row.Bounds;
            var label = _label.Bounds;
            return new Rect(row.X + label.X, row.Y + label.Y, label.Width, label.Height);
        }
    }

    internal void Attach(TabControl owner, object item)
    {
        Owner = owner;
        Item = item;
        RebuildHeader();
    }

    internal void Detach()
    {
        Owner = null;
        IsSelected = false;
        IsDragging = false;
    }

    /// <summary>Recreates the header content, for example after the tab control's header template or style changed.</summary>
    public void RebuildHeader()
    {
        _label.Clear();
        var style = Owner?.TabStyle ?? TabStyle.Primary;

        UIElement? content;
        if (Owner?.HeaderTemplate is { } template && Item != null)
        {
            content = template(Item);
            content.DataContext = Item;
        }
        else
        {
            content = Header switch
            {
                null => null,
                UIElement element => element,
                var other => new TextBlock
                {
                    Text = other.ToString() ?? string.Empty,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ShowsToolTipWhenTrimmed = true,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
        }

        // MD3 primary tabs stack the icon above the label; secondary and browser tabs put it before the label.
        bool stacked = style == TabStyle.Primary && Icon != MaterialIconKind.None && content != null;
        _label.Orientation = stacked ? Orientation.Vertical : Orientation.Horizontal;
        _label.Spacing = stacked ? 2 : 8;

        if (Icon != MaterialIconKind.None)
        {
            _label.Add(new Icon(Icon, style == TabStyle.Browser ? 18 : 24) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        }
        if (content != null)
        {
            content.HorizontalAlignment = HorizontalAlignment.Center;
            _label.Add(content);
        }

        Padding = style == TabStyle.Browser ? new Thickness(12, 0, 6, 0) : new Thickness(16, 0);
        MinWidth = style == TabStyle.Browser ? 100 : 90;
        MaxWidth = style == TabStyle.Browser ? 240 : float.PositiveInfinity;
        MinHeight = style == TabStyle.Browser ? 40 : stacked ? 64 : 48;
        FontWeight = style == TabStyle.Browser ? Core.Primitives.FontWeight.Normal : Core.Primitives.FontWeight.Medium;
        _row.HorizontalAlignment = style == TabStyle.Browser ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        _label.HorizontalAlignment = style == TabStyle.Browser ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        UpdateCloseButton();
        InvalidateMeasure();
    }

    internal void UpdateCloseButton()
    {
        _closeButton.Visibility = IsEffectivelyCloseable ? Visibility.Visible : Visibility.Collapsed;
        InvalidateMeasure();
    }

    #region Input

    /// <inheritdoc/>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || !IsEnabled || Owner == null) return;

        if (e.Button == PointerButtons.Middle && IsEffectivelyCloseable)
        {
            e.Handled = true;
            Owner.CloseTab(this);
            return;
        }

        if (e.Button == PointerButtons.Left)
        {
            e.Handled = true;
            Owner.OnTabPressed(this, e);
        }
    }

    /// <inheritdoc/>
    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (IsPointerCaptured) Owner?.OnTabDragMoved(this, e);
    }

    /// <inheritdoc/>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (IsPointerCaptured)
        {
            e.Handled = true;
            Owner?.OnTabReleased(this);
            ReleasePointerCapture();
        }
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture()
    {
        base.OnLostPointerCapture();
        Owner?.OnTabDragCanceled(this);
    }

    #endregion

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        _row.Measure(new Size(Math.Max(0, availableSize.Width - padding.Horizontal), availableSize.Height));
        return new Size(_row.DesiredSize.Width + padding.Horizontal, _row.DesiredSize.Height + padding.Vertical);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var padding = Padding;
        var inner = new Rect(padding.Left, padding.Top, Math.Max(0, finalSize.Width - padding.Horizontal), Math.Max(0, finalSize.Height - padding.Vertical));
        float height = Math.Min(inner.Height, _row.DesiredSize.Height);
        float width = _row.HorizontalAlignment == HorizontalAlignment.Stretch ? inner.Width : Math.Min(inner.Width, _row.DesiredSize.Width);
        float x = inner.X + (inner.Width - width) * 0.5f;
        _row.Arrange(new Rect(x, inner.Y + (inner.Height - height) * 0.5f, width, height));
        return finalSize;
    }
}
