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

public class RendererRegistry
{
    private readonly Dictionary<Type, IControlRenderer> _renderers = new();

    public void Register<T>(IControlRenderer<T> renderer) where T : UIElement
    {
        _renderers[typeof(T)] = renderer;
    }

    public IControlRenderer? GetRenderer(Type type)
    {
        Type? current = type;
        while (current != null && current != typeof(object))
        {
            if (_renderers.TryGetValue(current, out var renderer))
            {
                return renderer;
            }
            current = current.BaseType;
        }

        return null;
    }

    public IControlRenderer<T>? GetRenderer<T>() where T : UIElement
    {
        return GetRenderer(typeof(T)) as IControlRenderer<T>;
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
