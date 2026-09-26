using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Atelier.Core.Properties;

namespace Atelier.Core.Styling;

/// <summary>
/// Assigns a value to a <see cref="BindableProperty"/> as part of a <see cref="Style"/>.
/// </summary>
public class Setter
{
    /// <summary>
    /// Gets or sets the property the setter assigns.
    /// </summary>
    /// <remarks>Assigning this property directly bypasses the read-only and value checks done by the constructor.</remarks>
    public BindableProperty Property { get; set; }
    /// <summary>
    /// Gets or sets the value the setter assigns.
    /// </summary>
    /// <remarks>Assigning this property directly bypasses the validation done by the constructor.</remarks>
    public object? Value { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Setter"/> class.
    /// </summary>
    /// <param name="property">The property to assign.</param>
    /// <param name="value">The value to assign; validated against <paramref name="property"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="property"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="property"/> is read-only.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not valid for <paramref name="property"/>.</exception>
    public Setter(BindableProperty property, object? value)
    {
        Property = property ?? throw new ArgumentNullException(nameof(property));
        property.ThrowIfReadOnly();
        property.ValidateValue(value);
        Value = value;
    }
}

/// <summary>
/// A named or type-targeted set of <see cref="Setter"/>s applied to elements.
/// </summary>
/// <remarks>
/// When a style is applied, the setters of its <see cref="BasedOn"/> chain are applied first and the style's own setters
/// override them for the same property. A style with a <see cref="Key"/> applies to elements whose style key matches;
/// a style without a key and with a <see cref="TargetType"/> applies implicitly to elements of that type or a derived type.
/// Changing <see cref="Setters"/> does not restyle elements that already use the style.
/// </remarks>
public class Style : IEnumerable<Setter>
{
    /// <summary>
    /// Gets or sets the element type the style targets, or <see langword="null"/> for any type.
    /// </summary>
    /// <remarks>A style without a <see cref="Key"/> is only applied implicitly when this is set.</remarks>
    public Type? TargetType { get; set; }
    /// <summary>
    /// Gets or sets the key used to select the style explicitly, or <see langword="null"/> for an implicit style.
    /// </summary>
    public string? Key { get; set; }
    /// <summary>
    /// Gets or sets the style this style extends; its setters are applied first and overridden by this style's setters.
    /// </summary>
    public Style? BasedOn { get; set; }
    /// <summary>
    /// Gets the setters defined directly on this style, excluding those of <see cref="BasedOn"/>.
    /// </summary>
    public List<Setter> Setters { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Style"/> class with no key and no target type.
    /// </summary>
    public Style() { }

    /// <summary>
    /// Initializes a new implicit instance of the <see cref="Style"/> class for the given target type.
    /// </summary>
    /// <param name="targetType">The element type the style targets.</param>
    public Style(Type targetType)
    {
        TargetType = targetType;
    }

    /// <summary>
    /// Initializes a new keyed instance of the <see cref="Style"/> class.
    /// </summary>
    /// <param name="key">The style key.</param>
    /// <param name="targetType">The element type the style is restricted to, or <see langword="null"/> for any type.</param>
    /// <param name="basedOn">The style to extend, if any.</param>
    public Style(string key, Type? targetType = null, Style? basedOn = null)
    {
        Key = key;
        TargetType = targetType;
        BasedOn = basedOn;
    }

    /// <summary>
    /// Adds a setter for <paramref name="property"/>.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <param name="property">The property to assign.</param>
    /// <param name="value">The value to assign.</param>
    /// <returns>This style, for chaining.</returns>
    public Style Set<T>(BindableProperty<T> property, T value)
    {
        Setters.Add(new Setter(property, value));
        return this;
    }

    /// <summary>
    /// Adds an existing setter.
    /// </summary>
    /// <param name="setter">The setter to add.</param>
    /// <returns>This style, for chaining.</returns>
    public Style Add(Setter setter)
    {
        Setters.Add(setter);
        return this;
    }

    /// <summary>
    /// Adds a setter for <paramref name="property"/>; enables collection initializer syntax.
    /// </summary>
    /// <param name="property">The property to assign.</param>
    /// <param name="value">The value to assign.</param>
    public void Add(BindableProperty property, object? value)
    {
        Setters.Add(new Setter(property, value));
    }

    /// <summary>
    /// Returns an enumerator over <see cref="Setters"/>.
    /// </summary>
    /// <returns>An enumerator over the style's own setters.</returns>
    public IEnumerator<Setter> GetEnumerator() => Setters.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// An ordered collection of <see cref="Style"/>s that reports changes through <see cref="StylesChanged"/>.
/// </summary>
/// <remarks>
/// When styles are resolved, the first style with a matching key wins; for implicit styles the one with the most specific
/// target type wins (see <see cref="StyleManager.FindImplicitStyle"/>).
/// </remarks>
public class StyleCollection : Collection<Style>
{
    /// <summary>
    /// Occurs after every insertion, replacement, removal or clear.
    /// </summary>
    /// <remarks>
    /// Raised once for a whole <see cref="AddRange"/> call. Changes to the setters of a contained style do not raise it.
    /// </remarks>
    public event Action? StylesChanged;

    private bool _suppressChanged;

    /// <inheritdoc/>
    protected override void InsertItem(int index, Style item)
    {
        base.InsertItem(index, item);
        if (!_suppressChanged)
        {
            StylesChanged?.Invoke();
        }
    }

    /// <inheritdoc/>
    protected override void SetItem(int index, Style item)
    {
        base.SetItem(index, item);
        StylesChanged?.Invoke();
    }

    /// <inheritdoc/>
    protected override void RemoveItem(int index)
    {
        base.RemoveItem(index);
        StylesChanged?.Invoke();
    }

    /// <inheritdoc/>
    protected override void ClearItems()
    {
        base.ClearItems();
        StylesChanged?.Invoke();
    }

    /// <summary>
    /// Adds each style in order, then raises <see cref="StylesChanged"/> once (if anything was added), so that
    /// listeners re-apply styles once instead of once per style.
    /// </summary>
    /// <param name="styles">The styles to add.</param>
    public void AddRange(IEnumerable<Style> styles)
    {
        ArgumentNullException.ThrowIfNull(styles);

        int countBefore = Count;
        _suppressChanged = true;
        try
        {
            foreach (var s in styles)
            {
                Add(s);
            }
        }
        finally
        {
            _suppressChanged = false;
        }

        if (Count != countBefore)
        {
            StylesChanged?.Invoke();
        }
    }

    /// <summary>
    /// Replaces the contents with <paramref name="styles"/> and raises <see cref="StylesChanged"/> once.
    /// </summary>
    /// <param name="styles">The new styles, in order.</param>
    public void ReplaceAll(IEnumerable<Style> styles)
    {
        ArgumentNullException.ThrowIfNull(styles);

        _suppressChanged = true;
        try
        {
            Items.Clear();
            foreach (var s in styles)
            {
                Add(s);
            }
        }
        finally
        {
            _suppressChanged = false;
        }

        StylesChanged?.Invoke();
    }
}

/// <summary>
/// Holds application-wide styles.
/// </summary>
public static class StyleManager
{
    /// <summary>
    /// Gets the global styles, searched after the styles of an element and its ancestors.
    /// </summary>
    /// <remarks>
    /// Windows re-apply styles to their element tree once per frame after this collection changes
    /// (see <c>SilkWindow</c>). Elements that are not shown in a window can be updated with
    /// <see cref="Tree.UIElement.ApplyStylesToTree"/>.
    /// </remarks>
    public static StyleCollection GlobalStyles { get; } = new();

    /// <summary>
    /// Gets the default styles of the active theme, which the theme fills when it is activated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An implicit theme style (no <see cref="Style.Key"/>) is the lowest style layer of the elements of its target type:
    /// its setters apply to every property the element's own style (explicit, keyed or implicit, from
    /// <see cref="Tree.UIElement.Styles"/> or <see cref="GlobalStyles"/>) doesn't set, like WPF's theme styles. So an app
    /// style only needs to set what it changes. Keyed theme styles (e.g. typography) are found by
    /// <see cref="Tree.UIElement.StyleKey"/> after <see cref="GlobalStyles"/>.
    /// </para>
    /// <para>
    /// Implicit styles are matched by the most specific target type: a style for <c>RepeatButton</c> wins over one for
    /// <c>Button</c>. Windows re-apply styles when this collection changes, like for <see cref="GlobalStyles"/>.
    /// </para>
    /// </remarks>
    public static StyleCollection ThemeStyles { get; } = new();

    /// <summary>
    /// Finds the implicit style (one without a key) in <paramref name="styles"/> whose target type is closest to
    /// <paramref name="type"/>: its own type first, then each base type. Among styles for the same type the first wins.
    /// </summary>
    /// <param name="styles">The styles to search, or <c>null</c>.</param>
    /// <param name="type">The element type.</param>
    /// <returns>The most specific implicit style, or <c>null</c>.</returns>
    public static Style? FindImplicitStyle(StyleCollection? styles, Type type)
    {
        if (styles == null || styles.Count == 0)
        {
            return null;
        }

        for (Type? current = type; current != null && current != typeof(object); current = current.BaseType)
        {
            for (int i = 0; i < styles.Count; i++)
            {
                var style = styles[i];
                if (style.TargetType == current && string.IsNullOrEmpty(style.Key))
                {
                    return style;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the first style in <paramref name="styles"/> with <paramref name="key"/> that applies to
    /// <paramref name="element"/> (no target type, or one the element is an instance of).
    /// </summary>
    /// <param name="styles">The styles to search, or <c>null</c>.</param>
    /// <param name="key">The style key.</param>
    /// <param name="element">The element to style.</param>
    /// <returns>The matching style, or <c>null</c>.</returns>
    public static Style? FindKeyedStyle(StyleCollection? styles, string key, object element)
    {
        for (int i = 0; styles != null && i < styles.Count; i++)
        {
            var style = styles[i];
            if (style.Key == key && (style.TargetType == null || style.TargetType.IsInstanceOfType(element)))
            {
                return style;
            }
        }
        return null;
    }
}
