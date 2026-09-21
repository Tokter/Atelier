using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Atelier.Core.Styling;

namespace Atelier.Core.Properties;

/// <summary>
/// Represents the base class for objects that participate in the Atelier property and data-binding system.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BindableObject"/> is analogous to WPF's <c>DependencyObject</c> or Avalonia's <c>AvaloniaObject</c>.
/// It provides core infrastructure for:
/// </para>
/// <list type="bullet">
///   <item><description>A 4-tier value precedence resolution model (Local &gt; Styled &gt; Inherited &gt; Default).</description></item>
///   <item><description>Property change notification via <see cref="INotifyPropertyChanged"/>.</description></item>
///   <item><description>Value coercion via <see cref="CoerceValueCallback{T}"/>.</description></item>
///   <item><description>Hierarchical property value inheritance across visual and logical trees (e.g. <see cref="DataContext"/>, font sizes).</description></item>
///   <item><description>Strongly-typed and AOT-safe data bindings (both one-way and two-way) with configurable source update triggers.</description></item>
/// </list>
/// <para>
/// <b>Value Precedence:</b> When resolving the effective value of a property via <see cref="GetValue{T}(BindableProperty{T})"/>,
/// values are evaluated in the following priority order:
/// </para>
/// <list type="number">
///   <item><term>1. Local / Explicit Value</term><description>Directly set via <see cref="SetValue{T}(BindableProperty{T}, T)"/> or <see cref="SetValueUntyped(BindableProperty, object?)"/>.</description></item>
///   <item><term>2. Styled Value</term><description>Applied by active themes, style setters, or triggers.</description></item>
///   <item><term>3. Inherited Value</term><description>Inherited from an ancestor in the visual/logical tree if <see cref="BindableProperty.Inherits"/> is <c>true</c>.</description></item>
///   <item><term>4. Default Value</term><description>The fallback value defined at property registration time via <see cref="BindableProperty{T}.DefaultValue"/>.</description></item>
/// </list>
/// </remarks>
public class BindableObject : INotifyPropertyChanged
{
    private readonly Dictionary<int, object?> _localValues = new();
    private readonly Dictionary<int, object?> _styleValues = new();
    private readonly Dictionary<int, IBindingSubscription> _bindings = new();

    /// <summary>
    /// Occurs when a property value changes, implementing <see cref="INotifyPropertyChanged"/>.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Identifies the <see cref="DataContext"/> inheritable bindable property.
    /// </summary>
    public static readonly BindableProperty<object?> DataContextProperty =
        BindableProperty.Register<BindableObject, object?>(
            nameof(DataContext),
            null,
            propertyChanged: (sender, oldVal, newVal) => sender.OnDataContextChanged(oldVal, newVal),
            inherits: true
        );

    /// <summary>
    /// Gets or sets the data context for this object, which serves as the default data source for bindings.
    /// </summary>
    /// <remarks>
    /// The <see cref="DataContext"/> value automatically inherits down the tree hierarchy to child elements
    /// unless explicitly overridden on a child element.
    /// </remarks>
    public object? DataContext
    {
        get => GetValue(DataContextProperty);
        set => SetValue(DataContextProperty, value);
    }

    /// <summary>
    /// Gets the parent <see cref="BindableObject"/> in the tree hierarchy from which inheritable property values are resolved.
    /// </summary>
    /// <remarks>
    /// Derived classes representing nodes in an element or visual tree (such as UI elements or controls)
    /// override this property to point to their parent container.
    /// </remarks>
    protected virtual BindableObject? InheritanceParent => null;

    /// <summary>
    /// Gets the collection of child <see cref="BindableObject"/> instances that should receive propagation notifications
    /// when an inheritable property value changes on this object.
    /// </summary>
    protected virtual IEnumerable<BindableObject> InheritanceChildren => Array.Empty<BindableObject>();

    /// <summary>
    /// Determines whether a local (explicit) value has been set on this object for the specified bindable property.
    /// </summary>
    /// <param name="property">The bindable property to check.</param>
    /// <returns><c>true</c> if a local value is present; otherwise, <c>false</c>.</returns>
    public bool HasLocalValue(BindableProperty property)
    {
        return _localValues.ContainsKey(property.Id);
    }

    /// <summary>
    /// Gets the current effective value of a strongly-typed bindable property, evaluated according to the
    /// 4-tier precedence (Local &gt; Styled &gt; Inherited &gt; Default).
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="property">The strongly-typed bindable property to evaluate.</param>
    /// <returns>The current effective value of the property.</returns>
    public T GetValue<T>(BindableProperty<T> property)
    {
        // 1. Local / Explicit value (highest precedence)
        if (_localValues.TryGetValue(property.Id, out var localVal))
        {
            return (T)localVal!;
        }

        // 2. Styled value
        if (_styleValues.TryGetValue(property.Id, out var styleVal))
        {
            return (T)styleVal!;
        }

        // 3. Inherited value
        if (property.Inherits && TryGetInheritedValue<T>(property, out var inheritedVal))
        {
            return inheritedVal;
        }

        // 4. Default value
        return property.DefaultValue;
    }

    /// <summary>
    /// Gets the current effective value of a bindable property as an untyped <see cref="object"/>, evaluated according to the
    /// 4-tier precedence (Local &gt; Styled &gt; Inherited &gt; Default).
    /// </summary>
    /// <param name="property">The bindable property to evaluate.</param>
    /// <returns>The current effective value of the property, or <c>null</c>.</returns>
    public object? GetValueUntyped(BindableProperty property)
    {
        if (_localValues.TryGetValue(property.Id, out var localVal))
        {
            return localVal;
        }

        if (_styleValues.TryGetValue(property.Id, out var styleVal))
        {
            return styleVal;
        }

        if (property.Inherits && TryGetInheritedValueUntyped(property, out var inheritedVal))
        {
            return inheritedVal;
        }

        return property.GetDefaultValueUntyped();
    }

    /// <summary>
    /// Sets the local (explicit) value of a strongly-typed bindable property.
    /// </summary>
    /// <remarks>
    /// If the property defines a <see cref="BindableProperty{T}.CoerceValue"/> callback, the proposed value is coerced first.
    /// If the coerced value differs from the previous effective value:
    /// <list type="bullet">
    ///   <item><description>The property's <see cref="BindableProperty{T}.PropertyChanged"/> callback is invoked.</description></item>
    ///   <item><description>The <see cref="PropertyChanged"/> event is raised.</description></item>
    ///   <item><description>If <see cref="BindableProperty.Inherits"/> is <c>true</c>, the new value is propagated down the inheritance tree.</description></item>
    ///   <item><description>Any active two-way data binding on this property is updated.</description></item>
    /// </list>
    /// </remarks>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="property">The strongly-typed bindable property to set.</param>
    /// <param name="value">The new value to set.</param>
    /// <returns><c>true</c> if the local value was updated; <c>false</c> if the local value was already equal to the coerced value.</returns>
    public bool SetValue<T>(BindableProperty<T> property, T value)
    {
        T effectiveValue = property.CoerceValue != null ? property.CoerceValue(this, value) : value;
        T oldEffectiveValue = GetValue(property);

        bool hadLocal = _localValues.TryGetValue(property.Id, out var boxedLocal);
        if (hadLocal && EqualityComparer<T>.Default.Equals((T)boxedLocal!, effectiveValue))
        {
            return false;
        }

        _localValues[property.Id] = effectiveValue;

        if (!EqualityComparer<T>.Default.Equals(oldEffectiveValue, effectiveValue))
        {
            property.PropertyChanged?.Invoke(this, oldEffectiveValue, effectiveValue);
            OnPropertyChanged(property.Name);

            if (property.Inherits)
            {
                NotifyInheritedPropertyChanged(property, oldEffectiveValue, effectiveValue);
            }
        }

        // Update active two-way binding if present
        if (_bindings.TryGetValue(property.Id, out var binding))
        {
            binding.OnTargetPropertyChanged(effectiveValue);
        }

        return true;
    }

    /// <summary>
    /// Sets the local (explicit) value of a bindable property using an untyped <see cref="object"/> value.
    /// </summary>
    /// <param name="property">The bindable property to set.</param>
    /// <param name="value">The untyped value to set.</param>
    /// <returns><c>true</c> if the local value was updated; <c>false</c> if the local value was already equal to the provided value.</returns>
    public bool SetValueUntyped(BindableProperty property, object? value)
    {
        object? oldEffectiveValue = GetValueUntyped(property);

        bool hadLocal = _localValues.TryGetValue(property.Id, out var boxedLocal);
        if (hadLocal && Equals(boxedLocal, value))
        {
            return false;
        }

        _localValues[property.Id] = value;

        if (!Equals(oldEffectiveValue, value))
        {
            property.InvokePropertyChangedUntyped(this, oldEffectiveValue, value);
            OnPropertyChanged(property.Name);

            if (property.Inherits)
            {
                NotifyInheritedPropertyChanged(property, oldEffectiveValue, value);
            }
        }

        if (_bindings.TryGetValue(property.Id, out var binding))
        {
            binding.OnTargetPropertyChanged(value);
        }

        return true;
    }

    /// <summary>
    /// Clears the local (explicit) value for the specified strongly-typed bindable property,
    /// causing its effective value to fall back to the next level in the precedence chain (Styled, Inherited, or Default).
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="property">The strongly-typed bindable property to clear.</param>
    public void ClearValue<T>(BindableProperty<T> property)
    {
        if (_localValues.Remove(property.Id, out var boxedOld))
        {
            T oldEffective = (T)boxedOld!;
            T newEffective = GetValue(property);

            if (!EqualityComparer<T>.Default.Equals(oldEffective, newEffective))
            {
                property.PropertyChanged?.Invoke(this, oldEffective, newEffective);
                OnPropertyChanged(property.Name);

                if (property.Inherits)
                {
                    NotifyInheritedPropertyChanged(property, oldEffective, newEffective);
                }
            }
        }
    }

    /// <summary>
    /// Clears the local (explicit) value for the specified untyped bindable property,
    /// causing its effective value to fall back to the next level in the precedence chain (Styled, Inherited, or Default).
    /// </summary>
    /// <param name="property">The bindable property to clear.</param>
    public void ClearValue(BindableProperty property)
    {
        if (_localValues.Remove(property.Id, out var boxedOld))
        {
            object? oldEffective = boxedOld;
            object? newEffective = GetValueUntyped(property);

            if (!Equals(oldEffective, newEffective))
            {
                property.InvokePropertyChangedUntyped(this, oldEffective, newEffective);
                OnPropertyChanged(property.Name);

                if (property.Inherits)
                {
                    NotifyInheritedPropertyChanged(property, oldEffective, newEffective);
                }
            }
        }
    }

    internal void ApplyStyleSetters(IReadOnlyList<Setter> setters)
    {
        foreach (var setter in setters)
        {
            var prop = setter.Property;
            object? oldVal = GetValueUntyped(prop);
            _styleValues[prop.Id] = setter.Value;

            if (!HasLocalValue(prop))
            {
                object? newVal = GetValueUntyped(prop);
                if (!Equals(oldVal, newVal))
                {
                    prop.InvokePropertyChangedUntyped(this, oldVal, newVal);
                    OnPropertyChanged(prop.Name);

                    if (prop.Inherits)
                    {
                        NotifyInheritedPropertyChanged(prop, oldVal, newVal);
                    }
                }
            }
        }
    }

    internal void ClearStyleSetters(IEnumerable<BindableProperty>? propertiesToClear = null)
    {
        if (propertiesToClear != null)
        {
            foreach (var prop in propertiesToClear)
            {
                if (_styleValues.Remove(prop.Id, out _))
                {
                    if (!HasLocalValue(prop))
                    {
                        object? newVal = GetValueUntyped(prop);
                        prop.InvokePropertyChangedUntyped(this, null, newVal);
                        OnPropertyChanged(prop.Name);

                        if (prop.Inherits)
                        {
                            NotifyInheritedPropertyChanged(prop, null, newVal);
                        }
                    }
                }
            }
        }
        else
        {
            _styleValues.Clear();
        }
    }

    private bool TryGetInheritedValue<T>(BindableProperty<T> property, out T value)
    {
        var current = InheritanceParent;
        while (current != null)
        {
            if (current._localValues.TryGetValue(property.Id, out var local))
            {
                value = (T)local!;
                return true;
            }

            if (current._styleValues.TryGetValue(property.Id, out var styleVal))
            {
                value = (T)styleVal!;
                return true;
            }

            // Cross-type matching for inheritable properties with same name and compatible type
            var candidates = BindableProperty.GetInheritablePropertiesByName(property.Name);
            for (int i = 0; i < candidates.Count; i++)
            {
                var cand = candidates[i];
                if (cand.Id != property.Id && (cand.PropertyType == typeof(T) || typeof(T).IsAssignableFrom(cand.PropertyType)))
                {
                    if (current._localValues.TryGetValue(cand.Id, out var localCand))
                    {
                        value = (T)localCand!;
                        return true;
                    }
                    if (current._styleValues.TryGetValue(cand.Id, out var styleCand))
                    {
                        value = (T)styleCand!;
                        return true;
                    }
                }
            }

            current = current.InheritanceParent;
        }

        value = default!;
        return false;
    }

    private bool TryGetInheritedValueUntyped(BindableProperty property, out object? value)
    {
        var current = InheritanceParent;
        while (current != null)
        {
            if (current._localValues.TryGetValue(property.Id, out var local))
            {
                value = local;
                return true;
            }

            if (current._styleValues.TryGetValue(property.Id, out var styleVal))
            {
                value = styleVal;
                return true;
            }

            var candidates = BindableProperty.GetInheritablePropertiesByName(property.Name);
            for (int i = 0; i < candidates.Count; i++)
            {
                var cand = candidates[i];
                if (cand.Id != property.Id && property.PropertyType.IsAssignableFrom(cand.PropertyType))
                {
                    if (current._localValues.TryGetValue(cand.Id, out var localCand))
                    {
                        value = localCand;
                        return true;
                    }
                    if (current._styleValues.TryGetValue(cand.Id, out var styleCand))
                    {
                        value = styleCand;
                        return true;
                    }
                }
            }

            current = current.InheritanceParent;
        }

        value = null;
        return false;
    }

    private static object? GetValueUntypedFromAncestor(BindableObject? ancestor, BindableProperty property)
    {
        if (ancestor == null) return property.GetDefaultValueUntyped();
        return ancestor.GetValueUntyped(property);
    }

    internal void NotifyInheritedPropertyChanged(BindableProperty property, object? oldValue, object? newValue)
    {
        foreach (var child in InheritanceChildren)
        {
            if (!child.HasLocalValue(property) && !child._styleValues.ContainsKey(property.Id))
            {
                property.InvokePropertyChangedUntyped(child, oldValue, newValue);
                child.OnPropertyChanged(property.Name);
                child.NotifyInheritedPropertyChanged(property, oldValue, newValue);
            }
        }
    }

    internal virtual void OnInheritanceParentChanged(BindableObject? oldParent, BindableObject? newParent)
    {
        // Propagate DataContext change if not set locally or via style
        if (!HasLocalValue(DataContextProperty) && !_styleValues.ContainsKey(DataContextProperty.Id))
        {
            object? oldDc = oldParent?.DataContext;
            object? newDc = newParent?.DataContext;
            if (!Equals(oldDc, newDc))
            {
                DataContextProperty.InvokePropertyChangedUntyped(this, oldDc, newDc);
                OnPropertyChanged(nameof(DataContext));
                OnDataContextChanged(oldDc, newDc);
            }
        }

        // Notify inheritable properties that may have changed due to reparenting
        var inheritableProps = BindableProperty.GetInheritablePropertiesByName(nameof(DataContext));
        // Also check font/colors
        var fontSizes = BindableProperty.GetInheritablePropertiesByName("FontSize");
        foreach (var prop in fontSizes)
        {
            if (!HasLocalValue(prop) && !_styleValues.ContainsKey(prop.Id))
            {
                object? oldVal = GetValueUntypedFromAncestor(oldParent, prop);
                object? newVal = GetValueUntypedFromAncestor(newParent, prop);
                if (!Equals(oldVal, newVal))
                {
                    prop.InvokePropertyChangedUntyped(this, oldVal, newVal);
                    OnPropertyChanged(prop.Name);
                }
            }
        }

        var foregrounds = BindableProperty.GetInheritablePropertiesByName("Foreground");
        foreach (var prop in foregrounds)
        {
            if (!HasLocalValue(prop) && !_styleValues.ContainsKey(prop.Id))
            {
                object? oldVal = GetValueUntypedFromAncestor(oldParent, prop);
                object? newVal = GetValueUntypedFromAncestor(newParent, prop);
                if (!Equals(oldVal, newVal))
                {
                    prop.InvokePropertyChangedUntyped(this, oldVal, newVal);
                    OnPropertyChanged(prop.Name);
                }
            }
        }

        foreach (var child in InheritanceChildren)
        {
            child.OnInheritanceParentChanged(this, this);
        }
    }

    /// <summary>
    /// Creates and attaches a strongly-typed data binding between a target bindable property on this object
    /// and a specific, explicit source object instance.
    /// </summary>
    /// <remarks>
    /// If an existing binding was active on this property, it is automatically disposed and replaced.
    /// If the source object implements <see cref="INotifyPropertyChanged"/>, updates from the source are
    /// automatically pushed to the target property. If a <paramref name="setter"/> is provided, changes
    /// to the target property are pushed back to the source according to <paramref name="updateSourceTrigger"/>.
    /// </remarks>
    /// <typeparam name="TTarget">The data type of the target bindable property.</typeparam>
    /// <typeparam name="TSource">The type of the source object. Must be a reference type.</typeparam>
    /// <param name="property">The target bindable property on this object to bind.</param>
    /// <param name="source">The explicit source object supplying the bound value.</param>
    /// <param name="getter">A function delegate extracting the target value from the source object.</param>
    /// <param name="setter">An optional action delegate writing the target value back to the source object for two-way binding.</param>
    /// <param name="updateSourceTrigger">Specifies when two-way changes are pushed back to the source object. Defaults to <see cref="UpdateSourceTrigger.PropertyChanged"/>.</param>
    public void SetBinding<TTarget, TSource>(
        BindableProperty<TTarget> property,
        TSource source,
        Func<TSource, TTarget> getter,
        Action<TSource, TTarget>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged)
        where TSource : class
    {
        if (_bindings.Remove(property.Id, out var existing))
        {
            existing.Dispose();
        }

        var binding = new PropertyBinding<TTarget, TSource>(this, property, source, getter, setter, updateSourceTrigger);
        _bindings[property.Id] = binding;
    }

    /// <summary>
    /// Creates and attaches a strongly-typed data binding between a target bindable property on this object
    /// and its current <see cref="DataContext"/>.
    /// </summary>
    /// <remarks>
    /// If an existing binding was active on this property, it is automatically disposed and replaced.
    /// This binding automatically subscribes to data context changes on this object. When the <see cref="DataContext"/>
    /// changes or if the current data context raises <see cref="INotifyPropertyChanged.PropertyChanged"/>, the target
    /// property value is updated. If a <paramref name="setter"/> is provided, two-way updates are propagated back to the
    /// data context according to <paramref name="updateSourceTrigger"/>.
    /// </remarks>
    /// <typeparam name="TTarget">The data type of the target bindable property.</typeparam>
    /// <typeparam name="TDataContext">The expected type of the data context object. Must be a reference type.</typeparam>
    /// <param name="property">The target bindable property on this object to bind.</param>
    /// <param name="getter">A function delegate extracting the target value from the data context.</param>
    /// <param name="setter">An optional action delegate writing the target value back to the data context for two-way binding.</param>
    /// <param name="updateSourceTrigger">Specifies when two-way changes are pushed back to the data context. Defaults to <see cref="UpdateSourceTrigger.PropertyChanged"/>.</param>
    public void SetBinding<TTarget, TDataContext>(
        BindableProperty<TTarget> property,
        Func<TDataContext, TTarget> getter,
        Action<TDataContext, TTarget>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged)
        where TDataContext : class
    {
        if (_bindings.Remove(property.Id, out var existing))
        {
            existing.Dispose();
        }

        var binding = new DataContextBinding<TTarget, TDataContext>(this, property, getter, setter, updateSourceTrigger);
        _bindings[property.Id] = binding;
    }

    /// <summary>
    /// Manually forces the active binding on the specified property to push its current target value back to the source object.
    /// </summary>
    /// <remarks>
    /// This is typically used when a binding is configured with <see cref="UpdateSourceTrigger.Explicit"/>,
    /// or to force an immediate write-back of pending values before submitting a form or dialog.
    /// </remarks>
    /// <param name="property">The bindable property whose binding source should be updated.</param>
    public void UpdateBindingSource(BindableProperty property)
    {
        if (_bindings.TryGetValue(property.Id, out var binding))
        {
            binding.UpdateSource();
        }
    }

    /// <summary>
    /// Removes and disposes any active data binding subscription attached to the specified bindable property on this object.
    /// </summary>
    /// <param name="property">The bindable property whose binding subscription should be removed.</param>
    public void ClearBinding(BindableProperty property)
    {
        if (_bindings.Remove(property.Id, out var existing))
        {
            existing.Dispose();
        }
    }

    /// <summary>
    /// Invoked whenever the effective <see cref="DataContext"/> of this object changes.
    /// </summary>
    /// <param name="oldValue">The previous data context object, or <c>null</c>.</param>
    /// <param name="newValue">The new data context object, or <c>null</c>.</param>
    protected virtual void OnDataContextChanged(object? oldValue, object? newValue)
    {
    }

    /// <summary>
    /// Raises the <see cref="PropertyChanged"/> event for the specified property name.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed. Automatically supplied when called from a property member.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Specifies when changes in a two-way bound target property are written back to the source data object.
/// </summary>
public enum UpdateSourceTrigger
{
    /// <summary>
    /// Updates the binding source immediately whenever the target bindable property value changes.
    /// </summary>
    PropertyChanged = 0,

    /// <summary>
    /// Updates the binding source whenever the target element loses UI focus.
    /// </summary>
    LostFocus = 1,

    /// <summary>
    /// Updates the binding source only when <see cref="BindableObject.UpdateBindingSource(BindableProperty)"/> is explicitly invoked.
    /// </summary>
    Explicit = 2
}

/// <summary>
/// Represents an active data binding subscription connecting a target bindable property on a <see cref="BindableObject"/> to a data source.
/// </summary>
public interface IBindingSubscription : IDisposable
{
    /// <summary>
    /// Reads the current value from the source object and applies it to the target bindable property.
    /// </summary>
    void UpdateTarget();

    /// <summary>
    /// Pushes the current value of the target bindable property back into the source object.
    /// </summary>
    void UpdateSource();

    /// <summary>
    /// Notifies the subscription that the target bindable property value has changed.
    /// </summary>
    /// <param name="newValue">The new value assigned to the target property.</param>
    void OnTargetPropertyChanged(object? newValue);
}

/// <summary>
/// Represents a strongly-typed data binding subscription connecting a target <see cref="BindableProperty{TTarget}"/>
/// on a <see cref="BindableObject"/> to an explicit source object instance of type <typeparamref name="TSource"/>.
/// </summary>
/// <typeparam name="TTarget">The data type of the target property.</typeparam>
/// <typeparam name="TSource">The type of the source object. Must be a reference type.</typeparam>
public sealed class PropertyBinding<TTarget, TSource> : IBindingSubscription
    where TSource : class
{
    private readonly WeakReference<BindableObject> _targetRef;
    private readonly BindableProperty<TTarget> _property;
    private readonly WeakReference<TSource> _sourceRef;
    private readonly Func<TSource, TTarget> _getter;
    private readonly Action<TSource, TTarget>? _setter;
    private readonly UpdateSourceTrigger _updateSourceTrigger;
    private readonly INotifyPropertyChanged? _inpc;
    private object? _pendingValue;
    private bool _hasPendingValue;
    private bool _isUpdating;

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyBinding{TTarget, TSource}"/> class.
    /// </summary>
    /// <param name="target">The target <see cref="BindableObject"/> on which the property resides.</param>
    /// <param name="property">The target <see cref="BindableProperty{TTarget}"/> to bind.</param>
    /// <param name="source">The source object instance providing data.</param>
    /// <param name="getter">The getter delegate to extract values from the source object.</param>
    /// <param name="setter">An optional setter delegate to write values back to the source object for two-way binding.</param>
    /// <param name="updateSourceTrigger">Specifies when two-way changes are pushed back to the source. Defaults to <see cref="UpdateSourceTrigger.PropertyChanged"/>.</param>
    public PropertyBinding(
        BindableObject target,
        BindableProperty<TTarget> property,
        TSource source,
        Func<TSource, TTarget> getter,
        Action<TSource, TTarget>? setter,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged)
    {
        _targetRef = new WeakReference<BindableObject>(target);
        _property = property;
        _sourceRef = new WeakReference<TSource>(source);
        _getter = getter;
        _setter = setter;
        _updateSourceTrigger = updateSourceTrigger;

        if (source is INotifyPropertyChanged inpc)
        {
            _inpc = inpc;
            _inpc.PropertyChanged += OnSourcePropertyChanged;
        }

        if (target is Tree.UIElement uie && _updateSourceTrigger == UpdateSourceTrigger.LostFocus)
        {
            uie.LostFocus += OnTargetLostFocus;
        }

        UpdateTarget();
    }

    private void OnTargetLostFocus(object? sender, EventArgs e)
    {
        if (_hasPendingValue)
        {
            UpdateSourceInternal(_pendingValue);
            _hasPendingValue = false;
        }
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateTarget();
    }

    /// <summary>
    /// Reads the current value from the source object via the getter delegate and sets it on the target property.
    /// </summary>
    public void UpdateTarget()
    {
        if (_isUpdating) return;
        _isUpdating = true;
        try
        {
            if (_targetRef.TryGetTarget(out var target) && _sourceRef.TryGetTarget(out var source))
            {
                var value = _getter(source);
                target.SetValue(_property, value);
                _hasPendingValue = false;
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>
    /// Handles changes to the target property, either updating the source immediately or caching the pending value based on <see cref="UpdateSourceTrigger"/>.
    /// </summary>
    /// <param name="newValue">The updated target value.</param>
    public void OnTargetPropertyChanged(object? newValue)
    {
        if (_isUpdating || _setter == null) return;

        if (_updateSourceTrigger == UpdateSourceTrigger.PropertyChanged)
        {
            UpdateSourceInternal(newValue);
        }
        else
        {
            _pendingValue = newValue;
            _hasPendingValue = true;
        }
    }

    /// <summary>
    /// Pushes pending or current target property values back to the source object via the setter delegate.
    /// </summary>
    public void UpdateSource()
    {
        if (_hasPendingValue)
        {
            UpdateSourceInternal(_pendingValue);
            _hasPendingValue = false;
        }
        else if (_targetRef.TryGetTarget(out var target))
        {
            var val = target.GetValue(_property);
            UpdateSourceInternal(val);
        }
    }

    private void UpdateSourceInternal(object? value)
    {
        if (_isUpdating || _setter == null) return;
        _isUpdating = true;
        try
        {
            if (_sourceRef.TryGetTarget(out var source))
            {
                _setter(source, (TTarget)value!);
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>
    /// Unsubscribes from all event handlers on the source and target to release references and avoid memory leaks.
    /// </summary>
    public void Dispose()
    {
        if (_inpc != null)
        {
            _inpc.PropertyChanged -= OnSourcePropertyChanged;
        }

        if (_targetRef.TryGetTarget(out var target) && target is Tree.UIElement uie)
        {
            uie.LostFocus -= OnTargetLostFocus;
        }
    }
}

/// <summary>
/// Represents a strongly-typed data binding subscription connecting a target <see cref="BindableProperty{TTarget}"/>
/// on a <see cref="BindableObject"/> to its current <see cref="BindableObject.DataContext"/> of type <typeparamref name="TDataContext"/>.
/// </summary>
/// <remarks>
/// This binding dynamically observes the <see cref="BindableObject.DataContext"/> of the target element.
/// If the data context instance changes or if the active data context raises <see cref="INotifyPropertyChanged.PropertyChanged"/>,
/// the target property is updated. For two-way bindings (when a setter is provided), changes to the target property
/// are pushed back to the data context according to <see cref="UpdateSourceTrigger"/>.
/// </remarks>
/// <typeparam name="TTarget">The data type of the target property.</typeparam>
/// <typeparam name="TDataContext">The expected type of the data context. Must be a reference type.</typeparam>
public sealed class DataContextBinding<TTarget, TDataContext> : IBindingSubscription
    where TDataContext : class
{
    private readonly WeakReference<BindableObject> _targetRef;
    private readonly BindableProperty<TTarget> _property;
    private readonly Func<TDataContext, TTarget> _getter;
    private readonly Action<TDataContext, TTarget>? _setter;
    private readonly UpdateSourceTrigger _updateSourceTrigger;
    private INotifyPropertyChanged? _currentInpc;
    private object? _pendingValue;
    private bool _hasPendingValue;
    private bool _isUpdating;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataContextBinding{TTarget, TDataContext}"/> class.
    /// </summary>
    /// <param name="target">The target <see cref="BindableObject"/> on which the property resides.</param>
    /// <param name="property">The target <see cref="BindableProperty{TTarget}"/> to bind.</param>
    /// <param name="getter">The getter delegate to extract values from the data context.</param>
    /// <param name="setter">An optional setter delegate to write values back to the data context for two-way binding.</param>
    /// <param name="updateSourceTrigger">Specifies when two-way changes are pushed back to the data context. Defaults to <see cref="UpdateSourceTrigger.PropertyChanged"/>.</param>
    public DataContextBinding(
        BindableObject target,
        BindableProperty<TTarget> property,
        Func<TDataContext, TTarget> getter,
        Action<TDataContext, TTarget>? setter,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged)
    {
        _targetRef = new WeakReference<BindableObject>(target);
        _property = property;
        _getter = getter;
        _setter = setter;
        _updateSourceTrigger = updateSourceTrigger;

        target.PropertyChanged += OnTargetPropertyChangedInternal;
        if (target is Tree.UIElement uie && _updateSourceTrigger == UpdateSourceTrigger.LostFocus)
        {
            uie.LostFocus += OnTargetLostFocus;
        }

        HookDataContext(target.DataContext);
        UpdateTarget();
    }

    private void OnTargetLostFocus(object? sender, EventArgs e)
    {
        if (_hasPendingValue)
        {
            UpdateSourceInternal(_pendingValue);
            _hasPendingValue = false;
        }
    }

    private void OnTargetPropertyChangedInternal(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BindableObject.DataContext))
        {
            if (_targetRef.TryGetTarget(out var target))
            {
                HookDataContext(target.DataContext);
                UpdateTarget();
            }
        }
    }

    private void HookDataContext(object? dataContext)
    {
        if (_currentInpc != null)
        {
            _currentInpc.PropertyChanged -= OnSourcePropertyChanged;
            _currentInpc = null;
        }

        if (dataContext is INotifyPropertyChanged inpc && dataContext is TDataContext)
        {
            _currentInpc = inpc;
            _currentInpc.PropertyChanged += OnSourcePropertyChanged;
        }
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateTarget();
    }

    /// <summary>
    /// Reads the current value from the active data context via the getter delegate and sets it on the target property.
    /// </summary>
    public void UpdateTarget()
    {
        if (_isUpdating) return;
        _isUpdating = true;
        try
        {
            if (_targetRef.TryGetTarget(out var target) && target.DataContext is TDataContext dc)
            {
                var value = _getter(dc);
                target.SetValue(_property, value);
                _hasPendingValue = false;
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>
    /// Handles changes to the target property, either updating the data context immediately or caching the pending value based on <see cref="UpdateSourceTrigger"/>.
    /// </summary>
    /// <param name="newValue">The updated target value.</param>
    public void OnTargetPropertyChanged(object? newValue)
    {
        if (_isUpdating || _setter == null) return;

        if (_updateSourceTrigger == UpdateSourceTrigger.PropertyChanged)
        {
            UpdateSourceInternal(newValue);
        }
        else
        {
            _pendingValue = newValue;
            _hasPendingValue = true;
        }
    }

    /// <summary>
    /// Pushes pending or current target property values back to the active data context via the setter delegate.
    /// </summary>
    public void UpdateSource()
    {
        if (_hasPendingValue)
        {
            UpdateSourceInternal(_pendingValue);
            _hasPendingValue = false;
        }
        else if (_targetRef.TryGetTarget(out var target))
        {
            var val = target.GetValue(_property);
            UpdateSourceInternal(val);
        }
    }

    private void UpdateSourceInternal(object? value)
    {
        if (_isUpdating || _setter == null) return;
        _isUpdating = true;
        try
        {
            if (_targetRef.TryGetTarget(out var target) && target.DataContext is TDataContext dc)
            {
                _setter(dc, (TTarget)value!);
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>
    /// Unsubscribes from all event handlers on the data context and target to release references and avoid memory leaks.
    /// </summary>
    public void Dispose()
    {
        if (_targetRef.TryGetTarget(out var target))
        {
            target.PropertyChanged -= OnTargetPropertyChangedInternal;
            if (target is Tree.UIElement uie)
            {
                uie.LostFocus -= OnTargetLostFocus;
            }
        }

        if (_currentInpc != null)
        {
            _currentInpc.PropertyChanged -= OnSourcePropertyChanged;
            _currentInpc = null;
        }
    }
}
