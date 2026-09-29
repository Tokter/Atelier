using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Styling;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Theming;

public interface IControlRenderer
{
    void Render(UIElement element, ref DrawingContext context);
    void RenderOverlay(UIElement element, ref DrawingContext context) { }
}

public interface IControlRenderer<in T> : IControlRenderer where T : UIElement
{
    void Render(T element, ref DrawingContext context);
    void RenderOverlay(T element, ref DrawingContext context) { }
}

public abstract class ControlRenderer<T> : IControlRenderer<T> where T : UIElement
{
    public virtual void Render(T element, ref DrawingContext context) { }
    public virtual void RenderOverlay(T element, ref DrawingContext context) { }

    void IControlRenderer.Render(UIElement element, ref DrawingContext context)
    {
        if (element is T typedElement)
        {
            Render(typedElement, ref context);
        }
    }

    void IControlRenderer.RenderOverlay(UIElement element, ref DrawingContext context)
    {
        if (element is T typedElement)
        {
            RenderOverlay(typedElement, ref context);
        }
    }
}

/// <summary>
/// Maps element types to the renderers that draw them. An element uses the renderer registered for its own type or,
/// failing that, for its closest base type.
/// </summary>
/// <remarks>
/// Lookups run for every element on every frame, so the resolved renderer (or the absence of one) is cached per element
/// type; the cache is lock-free to read and is cleared whenever a renderer is registered.
/// </remarks>
public class RendererRegistry
{
    private readonly object _registrationLock = new();
    private readonly Dictionary<Type, IControlRenderer> _renderers = new();
    private readonly ConcurrentDictionary<Type, IControlRenderer?> _resolved = new();

    /// <summary>
    /// Registers <paramref name="renderer"/> for elements of type <typeparamref name="T"/> and derived types without a
    /// more specific renderer, replacing any renderer registered for <typeparamref name="T"/>.
    /// </summary>
    public void Register<T>(IControlRenderer<T> renderer) where T : UIElement
    {
        ArgumentNullException.ThrowIfNull(renderer);
        lock (_registrationLock)
        {
            _renderers[typeof(T)] = renderer;

            // A new renderer can change the result for any derived type, including types that had none.
            _resolved.Clear();
        }
    }

    /// <summary>
    /// Gets the renderer for elements of <paramref name="type"/>: the one registered for the type or its closest base
    /// type, or <c>null</c> if there is none.
    /// </summary>
    public IControlRenderer? GetRenderer(Type type)
    {
        return _resolved.TryGetValue(type, out var renderer) ? renderer : Resolve(type);
    }

    /// <summary>Gets the renderer for elements of <typeparamref name="T"/>; see <see cref="GetRenderer(Type)"/>.</summary>
    public IControlRenderer<T>? GetRenderer<T>() where T : UIElement
    {
        return GetRenderer(typeof(T)) as IControlRenderer<T>;
    }

    // Under the lock, so a concurrent Register can't be overwritten by a result computed from the old registrations.
    private IControlRenderer? Resolve(Type type)
    {
        lock (_registrationLock)
        {
            IControlRenderer? renderer = null;
            for (Type? current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                if (_renderers.TryGetValue(current, out renderer))
                {
                    break;
                }
            }

            _resolved[type] = renderer;
            return renderer;
        }
    }
}

/// <summary>
/// Implemented by renderers of containers that define the color of their content, like a filled button whose label is
/// drawn in the theme's "on primary" color. Text and icons without a foreground of their own use the color of their
/// nearest such ancestor (see <see cref="ContentColor"/>).
/// </summary>
public interface IContentColorProvider
{
    /// <summary>
    /// Gets the color content of <paramref name="element"/> is drawn in, for its current state (variant, hover, pressed,
    /// selected, ...). Disabled elements should report their enabled color: text and icons apply the disabled opacity.
    /// </summary>
    /// <param name="element">An element of the renderer's type.</param>
    /// <param name="color">The content color.</param>
    /// <returns><c>false</c> if the element doesn't define a content color in its current state.</returns>
    bool TryGetContentColor(UIElement element, out Color color);
}

/// <summary>
/// Resolves the color of text and icons that don't set a foreground themselves.
/// </summary>
public static class ContentColor
{
    /// <summary>
    /// Resolves the foreground of <paramref name="element"/> (a text block, icon, ...) the way content colors inherit:
    /// a foreground set on the element itself wins; otherwise the nearest ancestor that either sets a foreground itself
    /// (locally, by a style or an animation) or whose renderer provides a content color (<see cref="IContentColorProvider"/>)
    /// decides; otherwise <paramref name="fallback"/> (the theme's default text color).
    /// </summary>
    /// <remarks>Walks the ancestors; no allocations.</remarks>
    /// <param name="element">The element whose content color is needed.</param>
    /// <param name="foregroundProperty">The inheritable foreground property (e.g. <c>Control.ForegroundProperty</c>).</param>
    /// <param name="renderers">The renderers to ask, or <c>null</c> to only honor set foregrounds.</param>
    /// <param name="fallback">The color used when nothing defines one.</param>
    /// <param name="isExplicit">Whether the color came from a set foreground (not from a provider or the fallback).</param>
    public static Color Resolve(
        UIElement element,
        BindableProperty<Color> foregroundProperty,
        RendererRegistry? renderers,
        Color fallback,
        out bool isExplicit)
    {
        isExplicit = true;
        if (IsSetOn(element, foregroundProperty))
        {
            return element.GetValue(foregroundProperty);
        }

        for (var node = element.Parent; node != null; node = node.Parent)
        {
            if (node is not UIElement ancestor)
            {
                continue;
            }

            if (IsSetOn(ancestor, foregroundProperty))
            {
                return ancestor.GetValue(foregroundProperty);
            }

            if (renderers?.GetRenderer(ancestor.GetType()) is IContentColorProvider provider &&
                provider.TryGetContentColor(ancestor, out var color))
            {
                isExplicit = false;
                return color;
            }
        }

        isExplicit = false;
        return fallback;
    }

    // Set on the element itself (locally, by a style, coercion or an animation), not inherited or default.
    private static bool IsSetOn(UIElement element, BindableProperty<Color> property) =>
        element.GetValueSource(property) > ValueSource.Inherited;
}

/// <summary>
/// A visual theme: the renderers that draw each control type and the default styles for them.
/// </summary>
public abstract class Theme
{
    /// <summary>Gets the display name of the theme.</summary>
    public abstract string Name { get; }

    /// <summary>Gets whether the theme uses light content on a dark background.</summary>
    public abstract bool IsDark { get; }

    /// <summary>Gets the renderers of this theme.</summary>
    public RendererRegistry Renderers { get; } = new();

    /// <summary>
    /// Gets the theme's styles: implicit default styles per control type (sizes, shapes, alignment, typography) and
    /// keyed styles such as typography. They are copied to <see cref="StyleManager.ThemeStyles"/> when the theme becomes
    /// <see cref="ThemeManager.Current"/>, so creating a theme has no global side effects.
    /// </summary>
    public StyleCollection Styles { get; } = new();

    // The extensions (see ThemeExtensions) already applied to this theme.
    internal HashSet<Action<Theme>> AppliedExtensions { get; } = new();
}

/// <summary>
/// Lets libraries with controls of their own (such as a node editor) add their renderers and styles to themes, since a
/// theme only knows the controls of the libraries it was built with.
/// </summary>
/// <remarks>
/// An extension is applied once to each theme: to the current one when it's registered, and to every other theme when it
/// becomes <see cref="ThemeManager.Current"/>. It can check the theme's type (for example for a Material theme's colors)
/// and should fall back to a neutral look for themes it doesn't know.
/// </remarks>
public static class ThemeExtensions
{
    private static readonly List<Action<Theme>> s_extensions = new();

    /// <summary>Registers <paramref name="extend"/>, which adds renderers and styles to a theme; registering it again does nothing.</summary>
    public static void Register(Action<Theme> extend)
    {
        ArgumentNullException.ThrowIfNull(extend);
        lock (s_extensions)
        {
            if (s_extensions.Contains(extend)) return;
            s_extensions.Add(extend);
        }
        if (ThemeManager.HasTheme)
        {
            var theme = ThemeManager.Current;
            ApplyTo(theme);
            StyleManager.ThemeStyles.ReplaceAll(theme.Styles);
        }
    }

    /// <summary>Applies the registered extensions that <paramref name="theme"/> doesn't have yet.</summary>
    public static void ApplyTo(Theme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        Action<Theme>[] extensions;
        lock (s_extensions)
        {
            extensions = s_extensions.ToArray();
        }
        foreach (var extend in extensions)
        {
            if (theme.AppliedExtensions.Add(extend)) extend(theme);
        }
    }
}

/// <summary>
/// Holds the active <see cref="Theme"/>.
/// </summary>
public static class ThemeManager
{
    private static Theme? _currentTheme;

    /// <summary>Occurs after <see cref="Current"/> changed.</summary>
    public static event Action<Theme>? ThemeChanged;

    /// <summary>
    /// Gets or sets the active theme. Setting it applies the registered <see cref="ThemeExtensions"/> to the theme, installs its <see cref="Theme.Styles"/> as
    /// <see cref="StyleManager.ThemeStyles"/> (windows then restyle their trees) and raises <see cref="ThemeChanged"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Reading it before a theme was set.</exception>
    public static Theme Current
    {
        get => _currentTheme ?? throw new InvalidOperationException("No theme has been initialized. Please set ThemeManager.Current.");
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (_currentTheme != value)
            {
                _currentTheme = value;
                ThemeExtensions.ApplyTo(value);
                StyleManager.ThemeStyles.ReplaceAll(value.Styles);
                ThemeChanged?.Invoke(value);
            }
        }
    }

    /// <summary>Gets whether a theme has been set.</summary>
    public static bool HasTheme => _currentTheme != null;

    /// <summary>
    /// Removes the active theme and its styles, returning to the unthemed state (for example between tests).
    /// <see cref="ThemeChanged"/> is not raised.
    /// </summary>
    public static void Reset()
    {
        _currentTheme = null;
        StyleManager.ThemeStyles.Clear();
    }
}
