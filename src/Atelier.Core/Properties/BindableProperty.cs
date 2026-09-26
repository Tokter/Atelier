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
/// Represents a callback that decides whether a value is acceptable for a <see cref="BindableProperty{T}"/>.
/// Unlike coercion, a rejected value is not adjusted: the set operation throws an <see cref="ArgumentException"/>.
/// </summary>
/// <typeparam name="T">The type of the property value.</typeparam>
/// <param name="value">The proposed value.</param>
/// <returns><c>true</c> if the value is valid; otherwise, <c>false</c>.</returns>
public delegate bool ValidateValueCallback<T>(T value);

/// <summary>
/// Declares side effects of a bindable property that the framework applies automatically whenever
/// the property's effective value changes on a <see cref="Tree.UIElement"/>.
/// </summary>
[Flags]
public enum PropertyOptions
{
    /// <summary>No automatic side effects.</summary>
    None = 0,

    /// <summary>A change invalidates the element's measure (and therefore also its arrange).</summary>
    AffectsMeasure = 1,

    /// <summary>A change invalidates the element's arrange.</summary>
    AffectsArrange = 2,

    /// <summary>A change requires the element to be redrawn.</summary>
    AffectsRender = 4,
}

/// <summary>
/// Identifies where the effective value of a bindable property comes from; see <see cref="BindableObject.GetValueSource(BindableProperty)"/>.
/// </summary>
public enum ValueSource
{
    /// <summary>The property's default value (possibly overridden for the object's type).</summary>
    Default = 0,

    /// <summary>A value inherited from an ancestor.</summary>
    Inherited = 1,

    /// <summary>A value applied by a style setter.</summary>
    Style = 2,

    /// <summary>A local value, set directly or written by a data binding.</summary>
    Local = 3,

    /// <summary>A value supplied by a running animation or transition.</summary>
    Animation = 4,
}

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
    /// Gets a value indicating whether this property is read-only: it can only be set by code holding its
    /// <see cref="BindablePropertyKey{T}"/> (see <see cref="RegisterReadOnly{TOwner, T}"/>).
    /// </summary>
    public bool IsReadOnly { get; }

    /// <summary>
    /// Gets the side effects applied automatically when this property's effective value changes.
    /// </summary>
    public PropertyOptions Options { get; }

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
    /// <param name="isReadOnly"><c>true</c> if this property can only be set through its <see cref="BindablePropertyKey{T}"/>.</param>
    /// <param name="options">Side effects applied automatically when the effective value changes.</param>
    /// <exception cref="InvalidOperationException">A property with the same name is already registered on <paramref name="ownerType"/>.</exception>
    private protected BindableProperty(
        string name,
        Type ownerType,
        Type targetType,
        Type propertyType,
        bool inherits,
        bool isAttached,
        bool isReadOnly,
        PropertyOptions options)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(ownerType);

        Name = name;
        OwnerType = ownerType;
        TargetType = targetType;
        PropertyType = propertyType;
        Inherits = inherits;
        IsAttached = isAttached;
        IsReadOnly = isReadOnly;
        Options = options;
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
    /// Determines whether <paramref name="value"/> can be stored by this property.
    /// </summary>
    /// <param name="value">The candidate value.</param>
    /// <returns>
    /// <c>true</c> if the value is an instance of <see cref="PropertyType"/> (or <c>null</c> for a nullable property type)
    /// and passes the property's <see cref="ValidateValueCallback{T}"/>, if any.
    /// </returns>
    public abstract bool IsValidValue(object? value);

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> if <paramref name="value"/> cannot be stored by this property.
    /// </summary>
    /// <param name="value">The candidate value.</param>
    public void ValidateValue(object? value)
    {
        if (!IsValidValue(value))
        {
            throw InvalidValue(value);
        }
    }

    private protected ArgumentException InvalidValue(object? value)
    {
        return new ArgumentException(
            $"Value '{value ?? "null"}' of type '{value?.GetType().FullName ?? "null"}' is not valid for property " +
            $"'{OwnerType.Name}.{Name}' of type '{PropertyType.FullName}'.",
            nameof(value));
    }

    internal void ThrowIfReadOnly()
    {
        if (IsReadOnly)
        {
            throw new InvalidOperationException(
                $"Property '{OwnerType.Name}.{Name}' is read-only and can only be set through its BindablePropertyKey.");
        }
    }

    internal abstract void InvokePropertyChangedUntyped(BindableObject sender, object? oldValue, object? newValue);

    /// <summary>
    /// Gets the default value that applies to objects of <paramref name="objectType"/>, taking
    /// <see cref="BindableProperty{T}.OverrideDefaultValue{TDerived}(T)"/> into account.
    /// </summary>
    internal abstract object? GetDefaultValueUntyped(Type objectType);

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
    /// <param name="coerceValue">An optional callback used to constrain proposed values before they are assigned.</param>
    /// <param name="inherits"><c>true</c> if this property should inherit values from ancestor objects down the tree; otherwise, <c>false</c>.</param>
    /// <param name="options">Layout and rendering side effects applied automatically when the effective value changes.</param>
    /// <param name="validateValue">An optional callback that rejects invalid values; setting a rejected value throws an <see cref="ArgumentException"/>.</param>
    /// <returns>A typed <see cref="BindableProperty{T}"/> descriptor representing the registered property.</returns>
    /// <exception cref="InvalidOperationException">A property with the same name is already registered on <typeparamref name="TOwner"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="defaultValue"/> is rejected by <paramref name="validateValue"/>.</exception>
    public static BindableProperty<T> Register<TOwner, T>(
        string name,
        T defaultValue = default!,
        PropertyChangedCallback<T>? propertyChanged = null,
        CoerceValueCallback<T>? coerceValue = null,
        bool inherits = false,
        PropertyOptions options = PropertyOptions.None,
        ValidateValueCallback<T>? validateValue = null)
        where TOwner : BindableObject
    {
        return new BindableProperty<T>(
            name, typeof(TOwner), typeof(TOwner), defaultValue, propertyChanged, coerceValue, validateValue,
            inherits, isAttached: false, isReadOnly: false, options);
    }

    /// <summary>
    /// Registers a new read-only bindable property. The returned key is required to set the value, so keep it
    /// private to the owner and expose only <see cref="BindablePropertyKey{T}.Property"/> publicly.
    /// </summary>
    /// <example>
    /// <code>
    /// private static readonly BindablePropertyKey&lt;bool&gt; IsHoveredPropertyKey =
    ///     BindableProperty.RegisterReadOnly&lt;UIElement, bool&gt;(nameof(IsHovered), false);
    /// public static readonly BindableProperty&lt;bool&gt; IsHoveredProperty = IsHoveredPropertyKey.Property;
    /// </code>
    /// </example>
    /// <typeparam name="TOwner">The type of the owning class, which must inherit from <see cref="BindableObject"/>.</typeparam>
    /// <typeparam name="T">The data type of the property value.</typeparam>
    /// <param name="name">The name of the bindable property.</param>
    /// <param name="defaultValue">The default value.</param>
    /// <param name="propertyChanged">An optional callback invoked whenever the effective value of this property changes.</param>
    /// <param name="inherits"><c>true</c> if this property should inherit values from ancestor objects down the tree.</param>
    /// <param name="options">Layout and rendering side effects applied automatically when the effective value changes.</param>
    /// <returns>The key granting write access to the new property.</returns>
    public static BindablePropertyKey<T> RegisterReadOnly<TOwner, T>(
        string name,
        T defaultValue = default!,
        PropertyChangedCallback<T>? propertyChanged = null,
        bool inherits = false,
        PropertyOptions options = PropertyOptions.None)
        where TOwner : BindableObject
    {
        var property = new BindableProperty<T>(
            name, typeof(TOwner), typeof(TOwner), defaultValue, propertyChanged, coerceValue: null, validateValue: null,
            inherits, isAttached: false, isReadOnly: true, options);
        return new BindablePropertyKey<T>(property);
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
    /// <param name="coerceValue">An optional callback used to constrain proposed values before they are assigned.</param>
    /// <param name="inherits"><c>true</c> if this property should inherit values from ancestor objects down the tree; otherwise, <c>false</c>.</param>
    /// <param name="options">Layout and rendering side effects applied to the target element when the effective value changes.</param>
    /// <param name="validateValue">An optional callback that rejects invalid values.</param>
    /// <returns>A typed <see cref="BindableProperty{T}"/> descriptor representing the registered attached property.</returns>
    /// <exception cref="InvalidOperationException">A property with the same name is already registered on <typeparamref name="TOwner"/>.</exception>
    public static BindableProperty<T> RegisterAttached<TOwner, TTarget, T>(
        string name,
        T defaultValue = default!,
        PropertyChangedCallback<T>? propertyChanged = null,
        CoerceValueCallback<T>? coerceValue = null,
        bool inherits = false,
        PropertyOptions options = PropertyOptions.None,
        ValidateValueCallback<T>? validateValue = null)
        where TTarget : BindableObject
    {
        return new BindableProperty<T>(
            name, typeof(TOwner), typeof(TTarget), defaultValue, propertyChanged, coerceValue, validateValue,
            inherits, isAttached: true, isReadOnly: false, options);
    }
}

/// <summary>
/// Grants write access to a read-only <see cref="BindableProperty{T}"/> registered with
/// <see cref="BindableProperty.RegisterReadOnly{TOwner, T}"/>.
/// </summary>
/// <typeparam name="T">The data type of the property value.</typeparam>
public sealed class BindablePropertyKey<T>
{
    internal BindablePropertyKey(BindableProperty<T> property)
    {
        Property = property;
    }

    /// <summary>
    /// Gets the read-only property this key unlocks. This is the identifier to expose publicly.
    /// </summary>
    public BindableProperty<T> Property { get; }
}

/// <summary>
/// Represents a strongly-typed bindable property definition that carries type information,
/// typed default values, and typed change, coercion and validation callbacks.
/// </summary>
/// <typeparam name="T">The data type of the property value.</typeparam>
public sealed class BindableProperty<T> : BindableProperty
{
    private static readonly bool _acceptsNull = !typeof(T).IsValueType || Nullable.GetUnderlyingType(typeof(T)) != null;

    private readonly object _defaultOverridesLock = new();
    private volatile Dictionary<Type, T>? _defaultOverrides;
    private readonly ConcurrentDictionary<Type, T> _resolvedDefaults = new();

    /// <summary>
    /// Gets the strongly-typed default value returned when no local, styled, or inherited value is present.
    /// Individual types may override it with <see cref="OverrideDefaultValue{TDerived}(T)"/>; see <see cref="GetDefaultValue(Type)"/>.
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
    /// Coercion applies to local and styled values. Inherited, default and animated values are not coerced.
    /// Use <see cref="BindableObject.CoerceValue(BindableProperty)"/> to re-run coercion when a value it depends on changes.
    /// </remarks>
    public CoerceValueCallback<T>? CoerceValue { get; }

    /// <summary>
    /// Gets the optional callback that rejects invalid values, or <c>null</c> if none was registered.
    /// </summary>
    public ValidateValueCallback<T>? ValidateValueCallback { get; }

    internal BindableProperty(
        string name,
        Type ownerType,
        Type targetType,
        T defaultValue,
        PropertyChangedCallback<T>? propertyChanged,
        CoerceValueCallback<T>? coerceValue,
        ValidateValueCallback<T>? validateValue,
        bool inherits,
        bool isAttached,
        bool isReadOnly,
        PropertyOptions options)
        : base(name, ownerType, targetType, typeof(T), inherits, isAttached, isReadOnly, options)
    {
        if (validateValue != null && !validateValue(defaultValue))
        {
            throw new ArgumentException(
                $"The default value '{defaultValue}' of property '{ownerType.Name}.{name}' is rejected by its validation callback.",
                nameof(defaultValue));
        }

        DefaultValue = defaultValue;
        PropertyChanged = propertyChanged;
        CoerceValue = coerceValue;
        ValidateValueCallback = validateValue;
    }

    /// <summary>
    /// Overrides the default value of this property for objects of type <typeparamref name="TDerived"/> and its subclasses.
    /// </summary>
    /// <remarks>
    /// Prefer this over assigning the property in a control's constructor: a constructor assignment creates a
    /// <em>local</em> value, which silently beats every style, while an overridden default sits at the bottom of
    /// the precedence chain. Call it from the derived type's static constructor.
    /// </remarks>
    /// <typeparam name="TDerived">The type (and its subclasses) the new default applies to.</typeparam>
    /// <param name="defaultValue">The default value for <typeparamref name="TDerived"/>.</param>
    /// <exception cref="ArgumentException"><typeparamref name="TDerived"/> cannot carry this property, or the value is invalid.</exception>
    public void OverrideDefaultValue<TDerived>(T defaultValue) where TDerived : BindableObject
    {
        if (!TargetType.IsAssignableFrom(typeof(TDerived)))
        {
            throw new ArgumentException(
                $"'{typeof(TDerived).Name}' does not derive from '{TargetType.Name}', so it cannot override the default of '{OwnerType.Name}.{Name}'.");
        }

        if (!IsValidValue(defaultValue))
        {
            throw InvalidValue(defaultValue);
        }

        lock (_defaultOverridesLock)
        {
            var overrides = _defaultOverrides == null ? new Dictionary<Type, T>() : new Dictionary<Type, T>(_defaultOverrides);
            overrides[typeof(TDerived)] = defaultValue;
            _defaultOverrides = overrides;
            _resolvedDefaults.Clear();
        }
    }

    /// <summary>
    /// Gets the default value that applies to objects of <paramref name="objectType"/>: the closest override
    /// registered with <see cref="OverrideDefaultValue{TDerived}(T)"/> on the type or a base type, else <see cref="DefaultValue"/>.
    /// </summary>
    /// <param name="objectType">The runtime type of the object.</param>
    /// <returns>The applicable default value.</returns>
    public T GetDefaultValue(Type objectType)
    {
        var overrides = _defaultOverrides;
        if (overrides == null)
        {
            return DefaultValue;
        }

        return _resolvedDefaults.GetOrAdd(objectType, static (type, state) =>
        {
            for (Type? t = type; t != null; t = t.BaseType)
            {
                if (state.overrides.TryGetValue(t, out var value))
                {
                    return value;
                }
            }
            return state.self.DefaultValue;
        }, (overrides, self: this));
    }

    /// <inheritdoc/>
    public override bool IsValidValue(object? value)
    {
        if (value is T typed)
        {
            return ValidateValueCallback == null || ValidateValueCallback(typed);
        }

        return value is null && _acceptsNull && (ValidateValueCallback == null || ValidateValueCallback(default!));
    }

    internal void ValidateTyped(T value)
    {
        if (ValidateValueCallback != null && !ValidateValueCallback(value))
        {
            throw InvalidValue(value);
        }
    }

    internal override bool HasCoercion => CoerceValue != null;

    internal override void InvokePropertyChangedUntyped(BindableObject sender, object? oldValue, object? newValue)
    {
        PropertyChanged?.Invoke(sender, (T)oldValue!, (T)newValue!);
    }

    internal override object? GetDefaultValueUntyped(Type objectType) => GetDefaultValue(objectType);

    internal override object? CoerceUntyped(BindableObject sender, object? value)
    {
        ValidateValue(value);
        return CoerceValue != null ? CoerceValue(sender, (T)value!) : value;
    }
}
