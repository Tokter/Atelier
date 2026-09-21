using System;
using System.Collections.Generic;
using Atelier.Core.Tree;

namespace Atelier.Core.ViewResolution;

/// <summary>
/// Default registry and convention-based implementation of <see cref="IViewLocator"/>.
/// </summary>
public class ViewLocator : IViewLocator
{
    private static ViewLocator _current = new();

    /// <summary>
    /// Gets or sets the global default <see cref="ViewLocator"/> instance used across the framework.
    /// </summary>
    public static ViewLocator Current
    {
        get => _current;
        set => _current = value ?? throw new ArgumentNullException(nameof(value));
    }

    private readonly Dictionary<Type, Func<object, UIElement>> _registrations = new();
    private readonly List<IViewLocator> _fallbackLocators = [];

    /// <summary>
    /// Gets or sets whether to attempt convention-based view discovery when an explicit registration is not found.
    /// Replaces 'ViewModel' with 'View' (e.g. 'CustomerDetailViewModel' -> 'CustomerDetailView').
    /// </summary>
    public bool EnableConventionLookup { get; set; } = true;

    /// <summary>
    /// Optional fallback factory invoked when neither registration nor convention resolves a view.
    /// </summary>
    public Func<object, UIElement>? FallbackFactory { get; set; }

    /// <summary>
    /// Registers a view factory for a specific ViewModel type.
    /// </summary>
    public ViewLocator Register<TViewModel>(Func<TViewModel, UIElement> factory) where TViewModel : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        _registrations[typeof(TViewModel)] = vm => factory((TViewModel)vm);
        return this;
    }

    /// <summary>
    /// Registers a mapping from TViewModel to a parameterless-constructible TView.
    /// </summary>
    public ViewLocator Register<TViewModel, TView>()
        where TViewModel : class
        where TView : UIElement, new()
    {
        _registrations[typeof(TViewModel)] = _ => new TView();
        return this;
    }

    /// <summary>
    /// Registers a view factory for a specific runtime ViewModel type.
    /// </summary>
    public ViewLocator Register(Type viewModelType, Func<object, UIElement> factory)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
        ArgumentNullException.ThrowIfNull(factory);
        _registrations[viewModelType] = factory;
        return this;
    }

    /// <summary>
    /// Adds a child/fallback locator to query if this locator does not have an explicit match.
    /// </summary>
    public ViewLocator AddLocator(IViewLocator locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        if (!_fallbackLocators.Contains(locator))
        {
            _fallbackLocators.Add(locator);
        }
        return this;
    }

    /// <summary>
    /// Clears all explicit registrations and child locators.
    /// </summary>
    public void Clear()
    {
        _registrations.Clear();
        _fallbackLocators.Clear();
    }

    public virtual bool CanResolve(object? data)
    {
        if (data == null) return false;

        var type = data.GetType();

        if (_registrations.ContainsKey(type)) return true;

        foreach (var regType in _registrations.Keys)
        {
            if (regType.IsAssignableFrom(type)) return true;
        }

        for (int i = 0; i < _fallbackLocators.Count; i++)
        {
            if (_fallbackLocators[i].CanResolve(data)) return true;
        }

        if (EnableConventionLookup && TryResolveConventionType(type, out _))
        {
            return true;
        }

        return FallbackFactory != null;
    }

    public virtual UIElement? ResolveView(object? data)
    {
        if (data == null) return null;

        var type = data.GetType();

        // 1. Exact registration match
        if (_registrations.TryGetValue(type, out var factory))
        {
            return factory(data);
        }

        // 2. Polymorphic / assignable base type or interface match
        foreach (var (regType, regFactory) in _registrations)
        {
            if (regType.IsAssignableFrom(type))
            {
                return regFactory(data);
            }
        }

        // 3. Query child / fallback locators
        for (int i = 0; i < _fallbackLocators.Count; i++)
        {
            if (_fallbackLocators[i].CanResolve(data))
            {
                var view = _fallbackLocators[i].ResolveView(data);
                if (view != null) return view;
            }
        }

        // 4. Convention-based lookup: Replace 'ViewModel' with 'View'
        if (EnableConventionLookup && TryResolveConventionType(type, out var viewType))
        {
            var ctor = viewType!.GetConstructor([type]);
            if (ctor != null && ctor.Invoke([data]) is UIElement vmConstructedView)
            {
                return vmConstructedView;
            }

            if (Activator.CreateInstance(viewType!) is UIElement conventionView)
            {
                return conventionView;
            }
        }

        // 5. Custom Fallback Factory
        if (FallbackFactory != null)
        {
            return FallbackFactory(data);
        }

        return null;
    }

    private static bool TryResolveConventionType(Type vmType, out Type? viewType)
    {
        viewType = null;
        string vmFullName = vmType.FullName ?? vmType.Name;
        if (!vmFullName.EndsWith("ViewModel", StringComparison.Ordinal)) return false;

        // Try replacing "ViewModel" suffix with "View"
        string viewFullName = string.Concat(vmFullName.AsSpan(0, vmFullName.Length - 9), "View");
        var targetType = vmType.Assembly.GetType(viewFullName);
        if (targetType != null && typeof(UIElement).IsAssignableFrom(targetType))
        {
            viewType = targetType;
            return true;
        }

        // Try replacing ".ViewModels." namespace with ".Views."
        if (vmFullName.Contains(".ViewModels.", StringComparison.Ordinal))
        {
            string nsSwapped = vmFullName.Replace(".ViewModels.", ".Views.", StringComparison.Ordinal);
            if (nsSwapped.EndsWith("ViewModel", StringComparison.Ordinal))
            {
                nsSwapped = string.Concat(nsSwapped.AsSpan(0, nsSwapped.Length - 9), "View");
            }

            targetType = vmType.Assembly.GetType(nsSwapped);
            if (targetType != null && typeof(UIElement).IsAssignableFrom(targetType))
            {
                viewType = targetType;
                return true;
            }
        }

        return false;
    }
}
