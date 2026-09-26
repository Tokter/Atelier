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
///   <item><description>Fast integer-keyed lookup (<see cref="Id"/>) without reflection.</description></item>
///   <item><description>Hierarchical value inheritance across visual and logical trees (e.g., <c>DataContext</c>, <c>FontSize</c>).</description></item>
///   <item><description>Styling and theming support via property setters.</description></item>
///   <item><description>Reactive change notifications (<see cref="PropertyChangedCallback{T}"/>) and value coercion (<see cref="CoerceValueCallback{T}"/>).</description></item>
/// </list>
/// <para>
/// Inheritable properties that share a <see cref="Name"/> act as aliases of each other: a child reading
/// <c>TextBlock.FontSizeProperty</c> inherits a value set through <c>Control.FontSizeProperty</c> on an ancestor,
/// provided the ancestor's property type is assignable to the child's.
/// </para>
/// </remarks>
public abstract class BindableProperty
{
    private static readonly object _registrationLock = new();
    private static readonly ConcurrentDictionary<string, BindableProperty> _registry = new();
    private static readonly ConcurrentDictionary<string, BindableProperty[]> _inheritablePropertiesByName = new();
    private static int _nextId = 0;
    private static volatile BindableProperty?[] _byId = new BindableProperty?[64];
    private static volatile BindableProperty[] _allInheritable = Array.Empty<BindableProperty>();

    /// <summary>
    /// Gets the globally unique integer identifier assigned to this bindable property.
    /// Used internally as the key for value storage on <see cref="BindableObject"/>.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets the name of the bindable property.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the <see cref="Type"/> of the class that registered and owns this bindable property.
    /// For attached properties this is the declaring type (e.g. <c>Grid</c> for <c>Grid.Row</c>).
    /// </summary>
    public Type OwnerType { get; }

    /// <summary>
    /// Gets the <see cref="Type"/> of objects this property applies to. For regular properties this equals
    /// <see cref="OwnerType"/>; for attached properties it is the host type the property can be set on.
    /// </summary>
    public Type TargetType { get; }

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
    /// Gets a value indicating whether this is an attached property, i.e. declared by one type
    /// (<see cref="OwnerType"/>) but set on instances of another (<see cref="TargetType"/>).
    /// </summary>
    public bool IsAttached { get; }

    /// <summary>
    /// The properties consulted, in order, on each ancestor when resolving an inherited value for this property:
    /// this property first, then same-named inheritable properties whose values are assignable to it.
    /// </summary>
    internal BindableProperty[] InheritanceSources { get; private set; }

    /// <summary>
    /// The properties whose inherited value can change when this property changes on an ancestor:
    /// this property plus every same-named inheritable property that lists it in <see cref="InheritanceSources"/>.
    /// </summary>
    internal BindableProperty[] InheritanceDependents { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BindableProperty"/> class and registers it in the internal static registry.
    /// </summary>
    /// <param name="name">The name of the property.</param>
    /// <param name="ownerType">The <see cref="Type"/> of the class that owns (declares) this property.</param>
    /// <param name="targetType">The <see cref="Type"/> of objects the property can be set on.</param>
    /// <param name="propertyType">The <see cref="Type"/> of values stored by this property.</param>
    /// <param name="inherits"><c>true</c> if the property's value should inherit down the tree; otherwise, <c>false</c>.</param>
    /// <param name="isAttached"><c>true</c> if this is an attached property.</param>
    /// <exception cref="InvalidOperationException">A property with the same name is already registered on <paramref name="ownerType"/>.</exception>
    private protected BindableProperty(string name, Type ownerType, Type targetType, Type propertyType, bool inherits, bool isAttached)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(ownerType);

        Name = name;
        OwnerType = ownerType;
        TargetType = targetType;
        PropertyType = propertyType;
        Inherits = inherits;
        IsAttached = isAttached;
        InheritanceSources = new[] { this };
        InheritanceDependents = new[] { this };

        lock (_registrationLock)
        {
            string key = $"{ownerType.FullName}.{name}";
            if (!_registry.TryAdd(key, this))
            {
                throw new InvalidOperationException(
                    $"A bindable property named '{name}' is already registered on '{ownerType.FullName}'.");
            }

            Id = ++_nextId;

            var byId = _byId;
            if (Id >= byId.Length)
            {
                Array.Resize(ref byId, Math.Max(byId.Length * 2, Id + 1));
            }
            byId[Id] = this;
            _byId = byId;

            if (inherits)
            {
                RegisterInheritable();
            }
        }
    }

    // Must be called under _registrationLock. Arrays are replaced (never mutated) so readers need no lock.
    private void RegisterInheritable()
    {
        _allInheritable = [.. _allInheritable, this];

        var sameName = _inheritablePropertiesByName.TryGetValue(Name, out var existing)
            ? existing
            : Array.Empty<BindableProperty>();

        foreach (var other in sameName)
        {
            // A value stored under 'this' can be inherited by readers of 'other'.
            if (other.PropertyType.IsAssignableFrom(PropertyType))
            {
                other.InheritanceSources = [.. other.InheritanceSources, this];
                InheritanceDependents = [.. InheritanceDependents, other];
            }

            // A value stored under 'other' can be inherited by readers of 'this'.
            if (PropertyType.IsAssignableFrom(other.PropertyType))
            {
                InheritanceSources = [.. InheritanceSources, other];
                other.InheritanceDependents = [.. other.InheritanceDependents, this];
            }
        }

        _inheritablePropertiesByName[Name] = [.. sameName, this];
    }

    /// <summary>
    /// Gets all registered inheritable bindable properties.
    /// </summary>
    public static IReadOnlyList<BindableProperty> AllInheritable => _allInheritable;

    internal static BindableProperty[] AllInheritableArray => _allInheritable;

    /// <summary>
    /// Retrieves all registered inheritable bindable properties that share the specified property name.
    /// </summary>
    /// <param name="name">The name of the inheritable property to look up (e.g., <c>"DataContext"</c>, <c>"FontSize"</c>).</param>
    /// <returns>A read-only list containing all matching inheritable <see cref="BindableProperty"/> instances.</returns>
    public static IReadOnlyList<BindableProperty> GetInheritablePropertiesByName(string name)
    {
        return _inheritablePropertiesByName.TryGetValue(name, out var list)
            ? list
            : Array.Empty<BindableProperty>();
    }

    /// <summary>
    /// Finds a registered bindable property for a given owner type or any of its base classes by name.
    /// Attached properties are found only through their declaring type (e.g. <c>FindByName(typeof(Grid), "Row")</c>).
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

    internal static BindableProperty FromId(int id) => _byId[id]!;

    /// <summary>
    /// Determines whether <paramref name="value"/> is of a type this property can store.
    /// </summary>
    /// <param name="value">The candidate value.</param>
    /// <returns><c>true</c> if the value is an instance of <see cref="PropertyType"/>, or <c>null</c> for a nullable property type.</returns>
    public abstract bool IsValidValue(object? value);

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> if <paramref name="value"/> cannot be stored by this property.
    /// </summary>
    /// <param name="value">The candidate value.</param>
    public void ValidateValue(object? value)
    {
        if (!IsValidValue(value))
        {
            throw new ArgumentException(
                $"Value '{value ?? "null"}' of type '{value?.GetType().FullName ?? "null"}' is not valid for property " +
                $"'{OwnerType.Name}.{Name}' of type '{PropertyType.FullName}'.",
                nameof(value));
        }
    }

    internal abstract void InvokePropertyChangedUntyped(BindableObject sender, object? oldValue, object? newValue);
    internal abstract object? GetDefaultValueUntyped();

    /// <summary>
    /// Validates <paramref name="value"/> and applies the property's coercion callback, if any.
    /// </summary>
    internal abstract object? CoerceUntyped(BindableObject sender, object? value);

    internal abstract bool HasCoercion { get; }

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
    /// <exception cref="InvalidOperationException">A property with the same name is already registered on <typeparamref name="TOwner"/>.</exception>
    public static BindableProperty<T> Register<TOwner, T>(
        string name,
        T defaultValue = default!,
        PropertyChangedCallback<T>? propertyChanged = null,
        CoerceValueCallback<T>? coerceValue = null,
        bool inherits = false)
        where TOwner : BindableObject
    {
        return new BindableProperty<T>(name, typeof(TOwner), typeof(TOwner), defaultValue, propertyChanged, coerceValue, inherits, isAttached: false);
    }

    /// <summary>
    /// Registers a new strongly-typed attached property: a property declared by <typeparamref name="TOwner"/>
    /// (e.g. a layout panel) that is set on instances of <typeparamref name="TTarget"/> (e.g. the panel's children).
    /// </summary>
    /// <typeparam name="TOwner">The declaring type. It is used for registry lookup and does not need to be a <see cref="BindableObject"/>.</typeparam>
    /// <typeparam name="TTarget">The type of objects the property can be set on.</typeparam>
    /// <typeparam name="T">The data type of the property value.</typeparam>
    /// <param name="name">The name of the attached property (e.g. <c>"Row"</c>).</param>
    /// <param name="defaultValue">The fallback default value returned when no local, styled, or inherited value is present.</param>
    /// <param name="propertyChanged">An optional callback invoked whenever the effective value of this property changes.</param>
    /// <param name="coerceValue">An optional callback used to constrain or validate proposed values before they are assigned.</param>
    /// <param name="inherits"><c>true</c> if this property should inherit values from ancestor objects down the tree; otherwise, <c>false</c>.</param>
    /// <returns>A typed <see cref="BindableProperty{T}"/> descriptor representing the registered attached property.</returns>
    /// <exception cref="InvalidOperationException">A property with the same name is already registered on <typeparamref name="TOwner"/>.</exception>
    public static BindableProperty<T> RegisterAttached<TOwner, TTarget, T>(
        string name,
        T defaultValue = default!,
        PropertyChangedCallback<T>? propertyChanged = null,
        CoerceValueCallback<T>? coerceValue = null,
        bool inherits = false)
        where TTarget : BindableObject
    {
        return new BindableProperty<T>(name, typeof(TOwner), typeof(TTarget), defaultValue, propertyChanged, coerceValue, inherits, isAttached: true);
    }
}

/// <summary>
/// Represents a strongly-typed bindable property definition that carries type information,
/// typed default values, and typed change and coercion callbacks.
/// </summary>
/// <typeparam name="T">The data type of the property value.</typeparam>
public sealed class BindableProperty<T> : BindableProperty
{
    private static readonly bool _acceptsNull = !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null;

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
    /// <remarks>
    /// Coercion applies to local and styled values. Inherited and default values are not coerced.
    /// Use <see cref="BindableObject.CoerceValue(BindableProperty)"/> to re-run coercion when a value it depends on changes.
    /// </remarks>
    public CoerceValueCallback<T>? CoerceValue { get; }

    internal BindableProperty(
        string name,
        Type ownerType,
        Type targetType,
        T defaultValue,
        PropertyChangedCallback<T>? propertyChanged,
        CoerceValueCallback<T>? coerceValue,
        bool inherits,
        bool isAttached)
        : base(name, ownerType, targetType, typeof(T), inherits, isAttached)
    {
        DefaultValue = defaultValue;
        PropertyChanged = propertyChanged;
        CoerceValue = coerceValue;
    }

    /// <inheritdoc/>
    public override bool IsValidValue(object? value) => value is T || (value is null && _acceptsNull);

    internal override bool HasCoercion => CoerceValue != null;

    internal override void InvokePropertyChangedUntyped(BindableObject sender, object? oldValue, object? newValue)
    {
        PropertyChanged?.Invoke(sender, (T)oldValue!, (T)newValue!);
    }

    internal override object? GetDefaultValueUntyped() => DefaultValue;

    internal override object? CoerceUntyped(BindableObject sender, object? value)
    {
        ValidateValue(value);
        return CoerceValue != null ? CoerceValue(sender, (T)value!) : value;
    }
}
