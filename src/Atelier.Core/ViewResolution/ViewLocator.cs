using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using Atelier.Core.Tree;

namespace Atelier.Core.ViewResolution;

/// <summary>
/// Default registry and convention-based implementation of <see cref="IViewLocator"/>.
/// </summary>
/// <remarks>
/// <para>Resolution order for a view model of type <c>T</c>:</para>
/// <list type="number">
///   <item><description>A registration for exactly <c>T</c>.</description></item>
///   <item><description>The registration for the closest base class of <c>T</c>, then for an interface of <c>T</c>
///   (in registration order).</description></item>
///   <item><description>Fallback locators added with <see cref="AddLocator"/>.</description></item>
///   <item><description>Naming convention (<c>FooViewModel</c> → <c>FooView</c>), if <see cref="EnableConventionLookup"/> is set.
///   This uses reflection, so view types found only by convention may be removed by trimming or Native AOT; register them
///   explicitly in trimmed apps.</description></item>
///   <item><description><see cref="FallbackFactory"/>.</description></item>
/// </list>
/// <para>Lookups are cached per view model type, so resolving many items of the same type costs one dictionary lookup each.</para>
/// </remarks>
public class ViewLocator : IViewLocator
{
    private static ViewLocator _current = new();

    // Convention results depend only on the view model type, so they are shared by all locators.
    private static readonly ConcurrentDictionary<Type, ConventionView?> s_conventionCache = new();

    /// <summary>
    /// Gets or sets the global default <see cref="ViewLocator"/> instance used across the framework.
    /// </summary>
    public static ViewLocator Current
    {
        get => _current;
        set => _current = value ?? throw new ArgumentNullException(nameof(value));
    }

    private readonly Dictionary<Type, Func<object, UIElement>> _registrations = new();
    private readonly List<Type> _registrationOrder = [];
    private readonly Dictionary<Type, Func<object, UIElement>?> _resolvedRegistrations = new();
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
        return Register(typeof(TViewModel), vm => factory((TViewModel)vm));
    }

    /// <summary>
    /// Registers a mapping from TViewModel to a parameterless-constructible TView.
    /// </summary>
    public ViewLocator Register<TViewModel, TView>()
        where TViewModel : class
        where TView : UIElement, new()
    {
        return Register(typeof(TViewModel), static _ => new TView());
    }

    /// <summary>
    /// Registers a view factory for a specific runtime ViewModel type.
    /// </summary>
    public ViewLocator Register(Type viewModelType, Func<object, UIElement> factory)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
        ArgumentNullException.ThrowIfNull(factory);

        if (!_registrations.ContainsKey(viewModelType))
        {
            _registrationOrder.Add(viewModelType);
        }
        _registrations[viewModelType] = factory;
        _resolvedRegistrations.Clear();
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
        _registrationOrder.Clear();
        _resolvedRegistrations.Clear();
        _fallbackLocators.Clear();
    }

    /// <inheritdoc/>
    public virtual bool CanResolve(object? data)
    {
        if (data == null) return false;

        var type = data.GetType();

        if (FindRegistration(type) != null) return true;

        for (int i = 0; i < _fallbackLocators.Count; i++)
        {
            if (_fallbackLocators[i].CanResolve(data)) return true;
        }

        if (EnableConventionLookup && GetConventionView(type) != null)
        {
            return true;
        }

        return FallbackFactory != null;
    }

    /// <inheritdoc/>
    public virtual UIElement? ResolveView(object? data)
    {
        if (data == null) return null;

        var type = data.GetType();

        // 1 + 2. Exact registration, else closest base class, else an interface
        var factory = FindRegistration(type);
        if (factory != null)
        {
            return factory(data);
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
        if (EnableConventionLookup && GetConventionView(type) is { } convention)
        {
            var view = convention.Create(data);
            if (view != null) return view;
        }

        // 5. Custom Fallback Factory
        return FallbackFactory?.Invoke(data);
    }

    private Func<object, UIElement>? FindRegistration(Type type)
    {
        if (_resolvedRegistrations.TryGetValue(type, out var cached))
        {
            return cached;
        }

        var resolved = FindRegistrationUncached(type);
        _resolvedRegistrations[type] = resolved;
        return resolved;
    }

    private Func<object, UIElement>? FindRegistrationUncached(Type type)
    {
        // Exact type, then base classes from nearest to farthest: the most-derived registration wins.
        for (Type? t = type; t != null; t = t.BaseType)
        {
            if (_registrations.TryGetValue(t, out var factory))
            {
                return factory;
            }
        }

        // Interfaces last, in registration order, so the choice is deterministic.
        for (int i = 0; i < _registrationOrder.Count; i++)
        {
            var registered = _registrationOrder[i];
            if (registered.IsInterface && registered.IsAssignableFrom(type))
            {
                return _registrations[registered];
            }
        }

        return null;
    }

    private static ConventionView? GetConventionView(Type vmType) =>
        s_conventionCache.GetOrAdd(vmType, static t => TryResolveConventionType(t, out var viewType)
            ? new ConventionView(viewType!, t)
            : null);

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

    /// <summary>
    /// A view type found by naming convention, with its constructor looked up once.
    /// </summary>
    private sealed class ConventionView
    {
        private readonly Type _viewType;
        private readonly ConstructorInfo? _viewModelConstructor;

        public ConventionView(Type viewType, Type viewModelType)
        {
            _viewType = viewType;
            _viewModelConstructor = viewType.GetConstructor([viewModelType]);
        }

        public UIElement? Create(object viewModel)
        {
            if (_viewModelConstructor != null && _viewModelConstructor.Invoke([viewModel]) is UIElement withViewModel)
            {
                return withViewModel;
            }

            return Activator.CreateInstance(_viewType) as UIElement;
        }
    }
}
