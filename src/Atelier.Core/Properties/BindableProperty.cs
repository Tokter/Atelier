using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Atelier.Core.Properties;

/// <summary>
/// Represents a callback invoked when the effective value of a <see cref="BindableProperty{T}"/> changes on a <see cref="BindableObject"/>.
/// </summary>
/// <typeparam name="T">The type of the property value.</typeparam>
/// <param name="sender">The <see cref="BindableObject"/> instance whose property value changed.</param>
/// <param name="oldValue">The previous effective value of the property prior to this change.</param>
/// <param name="newValue">The new effective value of the property after this change.</param>
public delegate void PropertyChangedCallback<T>(BindableObject sender, T oldValue, T newValue);

/// <summary>
/// Represents a callback used to coerce or constrain a proposed property value before it is stored or applied to a <see cref="BindableObject"/>.
/// </summary>
/// <typeparam name="T">The type of the property value.</typeparam>
/// <param name="sender">The <see cref="BindableObject"/> instance on which the value is being coerced.</param>
/// <param name="baseValue">The raw or proposed value to be coerced.</param>
/// <returns>The coerced value that will be set on the target property.</returns>
public delegate T CoerceValueCallback<T>(BindableObject sender, T baseValue);

/// <summary>
/// Represents the definition and metadata of a bindable property in the Atelier property system.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BindableProperty"/> is analogous to WPF's <c>DependencyProperty</c> or Avalonia's <c>StyledProperty</c>.
/// It provides a static identifier for properties, enabling features such as:
/// </para>
/// <list type="bullet">
///   <item><description>Fast integer-indexed lookup (<see cref="Id"/>) without reflection or boxing overhead.</description></item>
///   <item><description>Hierarchical value inheritance across visual and logical trees (e.g., <c>DataContext</c>, <c>FontSize</c>).</description></item>
///   <item><description>Styling and theming support via property setters.</description></item>
///   <item><description>Reactive change notifications (<see cref="PropertyChangedCallback{T}"/>) and value coercion (<see cref="CoerceValueCallback{T}"/>).</description></item>
/// </list>
/// </remarks>
public abstract class BindableProperty
{
    private static int _nextId = 0;
    private static readonly ConcurrentDictionary<string, BindableProperty> _registry = new();
    private static readonly ConcurrentDictionary<string, List<BindableProperty>> _inheritablePropertiesByName = new();
    private static readonly object _inheritableLock = new();

    /// <summary>
    /// Gets the globally unique integer identifier assigned to this bindable property.
    /// Used internally for high-performance direct dictionary and array slot lookups on <see cref="BindableObject"/>.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets the name of the bindable property.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the <see cref="Type"/> of the class that registered and owns this bindable property.
    /// </summary>
    public Type OwnerType { get; }

    /// <summary>
    /// Gets the <see cref="Type"/> of values stored and returned by this bindable property.
    /// </summary>
    public Type PropertyType { get; }

    /// <summary>
    /// Gets a value indicating whether this property's effective value automatically inherits
    /// down the visual/logical tree hierarchy from ancestor <see cref="BindableObject"/> instances
    /// when not explicitly set locally or through styling.
    /// </summary>
    public bool Inherits { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BindableProperty"/> class and registers it in the internal static registry.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <param name="ownerType">The <see cref="Type"/> of the class that owns this property.</param>
    /// <param name="propertyType">The <see cref="Type"/> of values stored by this property.</param>
    /// <param name="inherits"><c>true</c> if the property's value should inherit down the tree; otherwise, <c>false</c>.</param>
    protected BindableProperty(string name, Type ownerType, Type propertyType, bool inherits)
    {
        Id = System.Threading.Interlocked.Increment(ref _nextId);
        Name = name;
        OwnerType = ownerType;
        PropertyType = propertyType;
        Inherits = inherits;

        _registry[$"{ownerType.FullName}.{name}"] = this;

        if (inherits)
        {
            lock (_inheritableLock)
            {
                var list = _inheritablePropertiesByName.GetOrAdd(name, _ => new List<BindableProperty>());
                list.Add(this);
            }
        }
    }

    /// <summary>
    /// Retrieves all registered inheritable bindable properties that share the specified property name.
    /// </summary>
    /// <param name="name">The name of the inheritable property to look up (e.g., <c>"DataContext"</c>, <c>"FontSize"</c>).</param>
    /// <returns>A read-only list containing all matching inheritable <see cref="BindableProperty"/> instances.</returns>
    public static IReadOnlyList<BindableProperty> GetInheritablePropertiesByName(string name)
    {
        lock (_inheritableLock)
        {
            if (_inheritablePropertiesByName.TryGetValue(name, out var list))
            {
                return list.ToArray();
            }
            return Array.Empty<BindableProperty>();
        }
    }

    /// <summary>
    /// Finds a registered bindable property for a given owner type or any of its base classes by name.
    /// </summary>
    /// <param name="ownerType">The owner type or derived type to start searching from.</param>
    /// <param name="name">The name of the bindable property.</param>
    /// <returns>The matching <see cref="BindableProperty"/> if found; otherwise, <c>null</c>.</returns>
    public static BindableProperty? FindByName(Type ownerType, string name)
    {
        Type? current = ownerType;
        while (current != null && current != typeof(object))
        {
            if (_registry.TryGetValue($"{current.FullName}.{name}", out var prop))
            {
                return prop;
            }
            current = current.BaseType;
        }
        return null;
    }

    internal abstract void InvokePropertyChangedUntyped(BindableObject sender, object? oldValue, object? newValue);
    internal abstract object? GetDefaultValueUntyped();

    /// <summary>
    /// Registers a new strongly-typed bindable property with the Atelier property system.
    /// </summary>
    /// <typeparam name="TOwner">The type of the owning class, which must inherit from <see cref="BindableObject"/>.</typeparam>
    /// <typeparam name="T">The data type of the property value.</typeparam>
    /// <param name="name">The name of the bindable property (typically defined with <c>nameof(...)</c>).</param>
    /// <param name="defaultValue">The fallback default value returned when no local, styled, or inherited value is present.</param>
    /// <param name="propertyChanged">An optional callback invoked whenever the effective value of this property changes.</param>
    /// <param name="coerceValue">An optional callback used to constrain or validate proposed values before they are assigned.</param>
    /// <param name="inherits"><c>true</c> if this property should inherit values from ancestor objects down the tree; otherwise, <c>false</c>.</param>
    /// <returns>A typed <see cref="BindableProperty{T}"/> descriptor representing the registered property.</returns>
    public static BindableProperty<T> Register<TOwner, T>(
        string name,
        T defaultValue = default!,
        PropertyChangedCallback<T>? propertyChanged = null,
        CoerceValueCallback<T>? coerceValue = null,
        bool inherits = false)
        where TOwner : BindableObject
    {
        return new BindableProperty<T>(name, typeof(TOwner), defaultValue, propertyChanged, coerceValue, inherits);
    }
}

/// <summary>
/// Represents a strongly-typed bindable property definition that carries type information,
/// typed default values, and typed change and coercion callbacks.
/// </summary>
/// <typeparam name="T">The data type of the property value.</typeparam>
public sealed class BindableProperty<T> : BindableProperty
{
    /// <summary>
    /// Gets the strongly-typed default value returned when no local, styled, or inherited value is present.
    /// </summary>
    public T DefaultValue { get; }

    /// <summary>
    /// Gets the optional callback invoked when the effective value of this property changes on a <see cref="BindableObject"/>,
    /// or <c>null</c> if no callback was registered.
    /// </summary>
    public PropertyChangedCallback<T>? PropertyChanged { get; }

    /// <summary>
    /// Gets the optional callback used to coerce or constrain proposed values before assignment,
    /// or <c>null</c> if no coercion callback was registered.
    /// </summary>
    public CoerceValueCallback<T>? CoerceValue { get; }

    internal BindableProperty(
        string name,
        Type ownerType,
        T defaultValue,
        PropertyChangedCallback<T>? propertyChanged,
        CoerceValueCallback<T>? coerceValue,
        bool inherits)
        : base(name, ownerType, typeof(T), inherits)
    {
        DefaultValue = defaultValue;
        PropertyChanged = propertyChanged;
        CoerceValue = coerceValue;
    }

    internal override void InvokePropertyChangedUntyped(BindableObject sender, object? oldValue, object? newValue)
    {
        T oldTyped = oldValue is T o ? o : DefaultValue;
        T newTyped = newValue is T n ? n : DefaultValue;
        PropertyChanged?.Invoke(sender, oldTyped, newTyped);
    }

    internal override object? GetDefaultValueUntyped() => DefaultValue;
}
