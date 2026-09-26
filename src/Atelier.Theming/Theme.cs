using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

public abstract class Theme
{
    public abstract string Name { get; }
    public abstract bool IsDark { get; }
    public RendererRegistry Renderers { get; } = new();
}

public static class ThemeManager
{
    private static Theme? _currentTheme;

    public static event Action<Theme>? ThemeChanged;

    public static Theme Current
    {
        get => _currentTheme ?? throw new InvalidOperationException("No theme has been initialized. Please set ThemeManager.Current.");
        set
        {
            if (_currentTheme != value)
            {
                _currentTheme = value;
                ThemeChanged?.Invoke(value);
            }
        }
    }

    public static bool HasTheme => _currentTheme != null;
}
