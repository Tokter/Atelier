using System;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Markup;

/// <summary>Fluent methods for <see cref="Badge"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class BadgeMarkup
{
    /// <summary>Sets the element the badge sits on, such as an icon or a button.</summary>
    public static T Content<T>(this T badge, UIElement? content) where T : Badge => badge.Set(Badge.ContentProperty, content);

    /// <summary>Shows a short label, such as "New", in the large badge. <c>null</c> shows the count or the dot.</summary>
    public static T Text<T>(this T badge, string? text) where T : Badge => badge.Set(Badge.TextProperty, text);

    /// <summary>Shows a number in the large badge; <c>null</c> shows the dot. 0 hides the badge unless ShowZero is set.</summary>
    public static T Count<T>(this T badge, int? count) where T : Badge => badge.Set(Badge.CountProperty, count);

    /// <summary>Sets the largest count shown as a number; larger ones show a "+". The default is 999.</summary>
    public static T MaxCount<T>(this T badge, int maxCount) where T : Badge => badge.Set(Badge.MaxCountProperty, maxCount);

    /// <summary>Shows a count of 0 instead of hiding the badge.</summary>
    public static T ShowZero<T>(this T badge, bool showZero = true) where T : Badge => badge.Set(Badge.ShowZeroProperty, showZero);

    /// <summary>Shows or hides the badge (shown by default).</summary>
    public static T IsBadgeVisible<T>(this T badge, bool isVisible = true) where T : Badge => badge.Set(Badge.IsBadgeVisibleProperty, isVisible);

    /// <summary>Moves the badge right of its default place (negative: left).</summary>
    public static T BadgeHorizontalOffset<T>(this T badge, float offset) where T : Badge => badge.Set(Badge.BadgeHorizontalOffsetProperty, offset);

    /// <summary>Moves the badge down from its default place (negative: up).</summary>
    public static T BadgeVerticalOffset<T>(this T badge, float offset) where T : Badge => badge.Set(Badge.BadgeVerticalOffsetProperty, offset);

    /// <summary>Moves the badge from its default place: right by <paramref name="x"/> and down by <paramref name="y"/>.</summary>
    public static T BadgeOffset<T>(this T badge, float x, float y) where T : Badge => badge.BadgeHorizontalOffset(x).BadgeVerticalOffset(y);

    /// <summary>Sets the badge's color, replacing the theme's (MD3: error).</summary>
    public static T BadgeBackground<T>(this T badge, Color color) where T : Badge => badge.Set(Badge.BadgeBackgroundProperty, color);

    /// <summary>Sets the color of the badge's label, replacing the theme's (MD3: on-error).</summary>
    public static T BadgeForeground<T>(this T badge, Color color) where T : Badge => badge.Set(Badge.BadgeForegroundProperty, color);

    /// <summary>Binds the count to <paramref name="source"/>.</summary>
    public static T BindCount<T, TSource>(this T badge, TSource source, Func<TSource, int?> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Badge where TSource : class =>
        badge.BindToSource(Badge.CountProperty, source, getter, null, UpdateSourceTrigger.PropertyChanged, getterExpression);

    /// <summary>Binds the label to <paramref name="source"/>.</summary>
    public static T BindText<T, TSource>(this T badge, TSource source, Func<TSource, string?> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Badge where TSource : class =>
        badge.BindToSource(Badge.TextProperty, source, getter, null, UpdateSourceTrigger.PropertyChanged, getterExpression);

    /// <summary>Binds whether the badge is shown to <paramref name="source"/>.</summary>
    public static T BindIsBadgeVisible<T, TSource>(this T badge, TSource source, Func<TSource, bool> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Badge where TSource : class =>
        badge.BindToSource(Badge.IsBadgeVisibleProperty, source, getter, null, UpdateSourceTrigger.PropertyChanged, getterExpression);
}
