using System;
using System.Globalization;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Controls;

/// <summary>
/// Wraps an element (such as an icon or a button) and shows a Material Design 3 badge at its top-right corner: a small
/// dot, or a pill with a short text or count.
/// </summary>
/// <remarks>
/// <para>
/// Without <see cref="Text"/> and <see cref="Count"/> the badge is the small 6 px dot, for "something new". With a text
/// or a count it is the large badge: 16 px high and at least as wide, with the label in 11 px medium text. A count above
/// <see cref="MaxCount"/> shows as "999+", and a count of 0 hides the badge unless <see cref="ShowZero"/> is set.
/// </para>
/// <para>
/// The badge is drawn over the corner and doesn't take part in layout: the <see cref="Badge"/> is as large as its
/// content, and the badge may reach a few pixels past it (add a margin where that matters). The large badge starts 12 px
/// left of the content's right edge and 4 px above its top; the dot sits inside the corner. Move it with
/// <see cref="BadgeHorizontalOffset"/> and <see cref="BadgeVerticalOffset"/>. The badge doesn't take pointer input.
/// </para>
/// <para>
/// Themes draw it in the error color with on-error text (MD3); <see cref="BadgeBackground"/> and
/// <see cref="BadgeForeground"/> replace them.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// new Badge(new Icon(MaterialIconKind.Mail)).Count(unread);
/// new Badge(new Button("Updates")).IsBadgeVisible(hasUpdates);   // a dot
/// </code>
/// </example>
public class Badge : Control
{
    /// <summary>Identifies the <see cref="Content"/> property.</summary>
    public static readonly BindableProperty<UIElement?> ContentProperty =
        BindableProperty.Register<Badge, UIElement?>(nameof(Content), null, (s, o, n) => ((Badge)s).OnContentChanged(o, n));

    /// <summary>Identifies the <see cref="Text"/> property.</summary>
    public static readonly BindableProperty<string?> TextProperty =
        BindableProperty.Register<Badge, string?>(nameof(Text), null, (s, o, n) => ((Badge)s).OnBadgeChanged());

    /// <summary>Identifies the <see cref="Count"/> property.</summary>
    public static readonly BindableProperty<int?> CountProperty =
        BindableProperty.Register<Badge, int?>(nameof(Count), null, (s, o, n) => ((Badge)s).OnBadgeChanged());

    /// <summary>Identifies the <see cref="MaxCount"/> property.</summary>
    public static readonly BindableProperty<int> MaxCountProperty =
        BindableProperty.Register<Badge, int>(nameof(MaxCount), 999, (s, o, n) => ((Badge)s).OnBadgeChanged(), validateValue: static v => v >= 0);

    /// <summary>Identifies the <see cref="ShowZero"/> property.</summary>
    public static readonly BindableProperty<bool> ShowZeroProperty =
        BindableProperty.Register<Badge, bool>(nameof(ShowZero), false, (s, o, n) => ((Badge)s).OnBadgeChanged());

    /// <summary>Identifies the <see cref="IsBadgeVisible"/> property.</summary>
    public static readonly BindableProperty<bool> IsBadgeVisibleProperty =
        BindableProperty.Register<Badge, bool>(nameof(IsBadgeVisible), true, (s, o, n) => ((Badge)s).OnBadgeChanged());

    /// <summary>Identifies the <see cref="BadgeHorizontalOffset"/> property.</summary>
    public static readonly BindableProperty<float> BadgeHorizontalOffsetProperty =
        BindableProperty.Register<Badge, float>(nameof(BadgeHorizontalOffset), 0f, (s, o, n) => ((Badge)s).UpdateBadgeBounds(), validateValue: float.IsFinite);

    /// <summary>Identifies the <see cref="BadgeVerticalOffset"/> property.</summary>
    public static readonly BindableProperty<float> BadgeVerticalOffsetProperty =
        BindableProperty.Register<Badge, float>(nameof(BadgeVerticalOffset), 0f, (s, o, n) => ((Badge)s).UpdateBadgeBounds(), validateValue: float.IsFinite);

    /// <summary>Identifies the <see cref="BadgeBackground"/> property.</summary>
    public static readonly BindableProperty<Color> BadgeBackgroundProperty =
        BindableProperty.Register<Badge, Color>(nameof(BadgeBackground), Color.Transparent, options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="BadgeForeground"/> property.</summary>
    public static readonly BindableProperty<Color> BadgeForegroundProperty =
        BindableProperty.Register<Badge, Color>(nameof(BadgeForeground), Color.Transparent, options: PropertyOptions.AffectsRender);

    /// <summary>The diameter of the small badge (MD3: 6).</summary>
    public const float SmallSize = 6f;

    /// <summary>The height of the large badge (MD3: 16), also its minimum width.</summary>
    public const float LargeHeight = 16f;

    /// <summary>The space left and right of the large badge's label (MD3: 4).</summary>
    public const float LargePadding = 4f;

    /// <summary>The size of the large badge's label (MD3 label-small: 11).</summary>
    public const float LabelFontSize = 11f;

    private string? _label;
    private bool _isShown;
    private float _width;

    /// <summary>Initializes a badge without content.</summary>
    public Badge()
    {
        OnBadgeChanged();
    }

    /// <summary>Initializes a badge on <paramref name="content"/>.</summary>
    /// <param name="content">The element the badge sits on.</param>
    public Badge(UIElement content) : this()
    {
        Content = content;
    }

    /// <summary>Gets or sets the element the badge sits on, such as an icon or a button.</summary>
    public UIElement? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>
    /// Gets or sets a short label for the large badge, such as "New"; it takes precedence over <see cref="Count"/>.
    /// <c>null</c> or empty (the default) shows the count, or the small dot without one.
    /// </summary>
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    /// <summary>
    /// Gets or sets the number shown in the large badge; <c>null</c> (the default) for none. Numbers above
    /// <see cref="MaxCount"/> show as "999+"; 0 hides the badge unless <see cref="ShowZero"/> is set.
    /// </summary>
    public int? Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }

    /// <summary>Gets or sets the largest count shown as a number; larger ones show a "+". The default is 999.</summary>
    public int MaxCount { get => GetValue(MaxCountProperty); set => SetValue(MaxCountProperty, value); }

    /// <summary>Gets or sets whether a <see cref="Count"/> of 0 is shown. The default is <c>false</c> (hidden).</summary>
    public bool ShowZero { get => GetValue(ShowZeroProperty); set => SetValue(ShowZeroProperty, value); }

    /// <summary>Gets or sets whether the badge is shown. The default is <c>true</c>.</summary>
    public bool IsBadgeVisible { get => GetValue(IsBadgeVisibleProperty); set => SetValue(IsBadgeVisibleProperty, value); }

    /// <summary>Gets or sets how far the badge moves right of its default place (negative: left). The default is 0.</summary>
    public float BadgeHorizontalOffset { get => GetValue(BadgeHorizontalOffsetProperty); set => SetValue(BadgeHorizontalOffsetProperty, value); }

    /// <summary>Gets or sets how far the badge moves down from its default place (negative: up). The default is 0.</summary>
    public float BadgeVerticalOffset { get => GetValue(BadgeVerticalOffsetProperty); set => SetValue(BadgeVerticalOffsetProperty, value); }

    /// <summary>
    /// Gets or sets the badge's color; transparent (the default) uses the theme's (MD3: error). Unlike
    /// <see cref="Control.Background"/> it doesn't affect the content.
    /// </summary>
    public Color BadgeBackground { get => GetValue(BadgeBackgroundProperty); set => SetValue(BadgeBackgroundProperty, value); }

    /// <summary>
    /// Gets or sets the color of the badge's label; transparent (the default) uses the theme's (MD3: on-error). Unlike
    /// <see cref="Control.Foreground"/> it isn't inherited by the content.
    /// </summary>
    public Color BadgeForeground { get => GetValue(BadgeForegroundProperty); set => SetValue(BadgeForegroundProperty, value); }

    /// <summary>Gets whether the badge is shown: <see cref="IsBadgeVisible"/>, and not a hidden zero count.</summary>
    public bool IsBadgeShown => _isShown;

    /// <summary>
    /// Gets the label of the large badge (the text, or the count with "+" above <see cref="MaxCount"/>), or <c>null</c>
    /// for the small dot.
    /// </summary>
    public string? Label => _label;

    /// <summary>Gets whether the badge is the small dot (no label).</summary>
    public bool IsSmall => _label == null;

    /// <summary>Gets where the badge is drawn, in the element's coordinates; it may extend past the bounds.</summary>
    public Rect BadgeBounds { get; private set; }

    private void OnContentChanged(UIElement? oldContent, UIElement? newContent)
    {
        if (oldContent != null) RemoveChild(oldContent);
        if (newContent != null) AddChild(newContent);
        InvalidateMeasure();
    }

    private void OnBadgeChanged()
    {
        int? count = Count;
        _label = !string.IsNullOrEmpty(Text) ? Text
            : count is { } c ? (c > MaxCount ? MaxCount.ToString(CultureInfo.CurrentCulture) + "+" : c.ToString(CultureInfo.CurrentCulture))
            : null;
        _isShown = IsBadgeVisible && !(string.IsNullOrEmpty(Text) && count is <= 0 && !ShowZero);
        InvalidateVisual();
        UpdateBadgeBounds();
    }

    private void UpdateBadgeBounds()
    {
        float width = _width;
        float x, y, w, h;
        if (_label == null)
        {
            w = h = SmallSize;
            x = width - SmallSize;
            y = 0;
        }
        else
        {
            h = LargeHeight;
            w = Math.Max(LargeHeight, TextMeasurer.GetFont(LabelFontSize, FontFamily, FontWeight.Medium).MeasureText(_label) + LargePadding * 2);
            x = width - 12f;
            y = -4f;
        }
        var bounds = new Rect(x + BadgeHorizontalOffset, y + BadgeVerticalOffset, w, h);
        if (bounds == BadgeBounds) return;
        BadgeBounds = bounds;
        InvalidateVisual();
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var content = Content;
        if (content == null) return Size.Zero;
        content.Measure(availableSize);
        return content.DesiredSize;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        Content?.Arrange(new Rect(Point.Zero, finalSize));
        _width = finalSize.Width;
        UpdateBadgeBounds();
        return finalSize;
    }
}
