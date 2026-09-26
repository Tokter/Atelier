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
///   <item><term>0. Animated Value</term><description>Supplied by a running animation or transition via <see cref="SetAnimatedValue{T}(BindableProperty{T}, T)"/>. It overrides all other layers without replacing them, so clearing it restores the underlying value.</description></item>
///   <item><term>1. Local / Explicit Value</term><description>Directly set via <see cref="SetValue{T}(BindableProperty{T}, T)"/> or <see cref="SetValueUntyped(BindableProperty, object?)"/>. Data bindings also write to this layer, so a local set replaces a one-way bound value until the source changes again.</description></item>
///   <item><term>2. Styled Value</term><description>Applied by active themes, style setters, or triggers.</description></item>
///   <item><term>3. Inherited Value</term><description>Inherited from an ancestor in the visual/logical tree if <see cref="BindableProperty.Inherits"/> is <c>true</c>.</description></item>
///   <item><term>4. Default Value</term><description>The fallback value defined at property registration time via <see cref="BindableProperty{T}.DefaultValue"/>.</description></item>
/// </list>
/// <para>
/// Change callbacks and <see cref="PropertyChanged"/> are raised only when the <em>effective</em> value actually changes,
/// on this object and on every descendant that inherits it.
/// </para>
/// </remarks>
public class BindableObject : INotifyPropertyChanged
{
    private readonly Dictionary<int, object?> _localValues = new();
    private readonly Dictionary<int, object?> _styleValues = new();
    private readonly Dictionary<int, IBindingSubscription> _bindings = new();

    // Allocated on first use: most objects are never animated or observed per property.
    private Dictionary<int, object?>? _animatedValues;
    private Dictionary<int, PropertySubscription[]>? _subscriptions;

    // Uncoerced values, kept only for entries where coercion changed the value, so CoerceValue() can re-evaluate them.
    private Dictionary<int, object?>? _localBaseValues;
    private Dictionary<int, object?>? _styleBaseValues;

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
    protected virtual IReadOnlyList<BindableObject> InheritanceChildren => Array.Empty<BindableObject>();

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
        if (TryGetOwnValue(property.Id, out var own))
        {
            return (T)own!;
        }

        if (property.Inherits && TryGetInheritedValue(property, out var inherited))
        {
            return (T)inherited!;
        }

        return property.GetDefaultValue(GetType());
    }

    /// <summary>
    /// Gets the current effective value of a bindable property as an untyped <see cref="object"/>, evaluated according to the
    /// 4-tier precedence (Local &gt; Styled &gt; Inherited &gt; Default).
    /// </summary>
    /// <param name="property">The bindable property to evaluate.</param>
    /// <returns>The current effective value of the property, or <c>null</c>.</returns>
    public object? GetValueUntyped(BindableProperty property)
    {
        if (TryGetOwnValue(property.Id, out var own))
        {
            return own;
        }

        if (property.Inherits && TryGetInheritedValue(property, out var inherited))
        {
            return inherited;
        }

        return property.GetDefaultValueUntyped(GetType());
    }

    /// <summary>
    /// Determines which layer of the precedence chain currently supplies the effective value of <paramref name="property"/>.
    /// </summary>
    /// <param name="property">The bindable property to inspect.</param>
    /// <returns>The <see cref="ValueSource"/> of the effective value.</returns>
    public ValueSource GetValueSource(BindableProperty property)
    {
        if (_animatedValues != null && _animatedValues.ContainsKey(property.Id)) return ValueSource.Animation;
        if (_localValues.ContainsKey(property.Id)) return ValueSource.Local;
        if (_styleValues.ContainsKey(property.Id)) return ValueSource.Style;
        if (property.Inherits && TryGetInheritedValue(property, out _)) return ValueSource.Inherited;
        return ValueSource.Default;
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
    ///   <item><description>If <see cref="BindableProperty.Inherits"/> is <c>true</c>, descendants whose inherited value changed are notified.</description></item>
    /// </list>
    /// Any active two-way data binding on this property is updated whenever the local value changes.
    /// </remarks>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="property">The strongly-typed bindable property to set.</param>
    /// <param name="value">The new value to set.</param>
    /// <returns><c>true</c> if the local value was updated; <c>false</c> if the local value was already equal to the coerced value.</returns>
    /// <exception cref="InvalidOperationException">The property is read-only.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is rejected by the property's validation callback.</exception>
    public bool SetValue<T>(BindableProperty<T> property, T value)
    {
        property.ThrowIfReadOnly();
        return SetValueTyped(property, value);
    }

    /// <summary>
    /// Sets the local value of a read-only bindable property. Only code holding the property's key can call this.
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="key">The key returned by <see cref="BindableProperty.RegisterReadOnly{TOwner, T}"/>.</param>
    /// <param name="value">The new value to set.</param>
    /// <returns><c>true</c> if the local value was updated; otherwise, <c>false</c>.</returns>
    public bool SetValue<T>(BindablePropertyKey<T> key, T value) => SetValueTyped(key.Property, value);

    /// <summary>
    /// Clears the local value of a read-only bindable property. Only code holding the property's key can call this.
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="key">The key returned by <see cref="BindableProperty.RegisterReadOnly{TOwner, T}"/>.</param>
    public void ClearValue<T>(BindablePropertyKey<T> key) => ClearLocalValueCore(key.Property);

    private bool SetValueTyped<T>(BindableProperty<T> property, T value)
    {
        property.ValidateTyped(value);

        // Fast path: setting the value it already has must not box (bindings and animations do this constantly).
        // With coercion the uncoerced base value still has to be tracked, so that case takes the regular path.
        if (property.CoerceValue == null && HasEqualValue(_localValues, property.Id, value))
        {
            return false;
        }

        object? coerced = property.CoerceValue != null ? property.CoerceValue(this, value) : value;
        return SetLocalValueCore(property, value, coerced);
    }

    private static bool HasEqualValue<T>(Dictionary<int, object?>? layer, int id, T value)
    {
        return layer != null
            && layer.TryGetValue(id, out var current)
            && (current is T typed ? EqualityComparer<T>.Default.Equals(typed, value) : current is null && value is null);
    }

    /// <summary>
    /// Sets the local (explicit) value of a bindable property using an untyped <see cref="object"/> value.
    /// </summary>
    /// <param name="property">The bindable property to set.</param>
    /// <param name="value">The untyped value to set.</param>
    /// <returns><c>true</c> if the local value was updated; <c>false</c> if the local value was already equal to the coerced value.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is not valid for the property's type.</exception>
    /// <exception cref="InvalidOperationException">The property is read-only.</exception>
    public bool SetValueUntyped(BindableProperty property, object? value)
    {
        property.ThrowIfReadOnly();
        object? coerced = property.CoerceUntyped(this, value);
        return SetLocalValueCore(property, value, coerced);
    }

    private bool SetLocalValueCore(BindableProperty property, object? baseValue, object? coerced)
    {
        if (property.HasCoercion)
        {
            TrackBaseValue(ref _localBaseValues, property.Id, baseValue, coerced);
        }

        if (_localValues.TryGetValue(property.Id, out var current) && Equals(current, coerced))
        {
            return false;
        }

        var change = BeginChange(property, captureDescendants: true);
        _localValues[property.Id] = coerced;
        EndChange(property, change);

        if (_bindings.TryGetValue(property.Id, out var binding))
        {
            binding.OnTargetPropertyChanged(coerced);
        }

        return true;
    }

    /// <summary>
    /// Clears the local (explicit) value for the specified strongly-typed bindable property,
    /// causing its effective value to fall back to the next level in the precedence chain (Styled, Inherited, or Default).
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="property">The strongly-typed bindable property to clear.</param>
    public void ClearValue<T>(BindableProperty<T> property) => ClearValue((BindableProperty)property);

    /// <summary>
    /// Clears the local (explicit) value for the specified untyped bindable property,
    /// causing its effective value to fall back to the next level in the precedence chain (Styled, Inherited, or Default).
    /// </summary>
    /// <remarks>An active binding on the property is not removed; use <see cref="ClearBinding(BindableProperty)"/> for that.</remarks>
    /// <param name="property">The bindable property to clear.</param>
    /// <exception cref="InvalidOperationException">The property is read-only.</exception>
    public void ClearValue(BindableProperty property)
    {
        property.ThrowIfReadOnly();
        ClearLocalValueCore(property);
    }

    private void ClearLocalValueCore(BindableProperty property)
    {
        if (!_localValues.ContainsKey(property.Id))
        {
            return;
        }

        _localBaseValues?.Remove(property.Id);

        var change = BeginChange(property, captureDescendants: true);
        _localValues.Remove(property.Id);
        EndChange(property, change);
    }

    /// <summary>
    /// Re-runs the property's coercion callback against the originally requested (uncoerced) local and styled values.
    /// </summary>
    /// <remarks>
    /// Call this from the change callback of a property that the coercion depends on, e.g. re-coerce <c>Value</c> when
    /// <c>Maximum</c> changes. A value that was clamped earlier is restored once the constraint allows it again.
    /// </remarks>
    /// <param name="property">The bindable property to re-coerce.</param>
    public void CoerceValue(BindableProperty property)
    {
        if (!property.HasCoercion)
        {
            return;
        }

        bool hasLocal = _localValues.TryGetValue(property.Id, out var local);
        bool hasStyle = _styleValues.TryGetValue(property.Id, out var styled);
        if (!hasLocal && !hasStyle)
        {
            return;
        }

        object? newLocal = hasLocal ? Recoerce(property, ref _localBaseValues, local) : null;
        object? newStyle = hasStyle ? Recoerce(property, ref _styleBaseValues, styled) : null;

        bool localChanged = hasLocal && !Equals(local, newLocal);
        bool styleChanged = hasStyle && !Equals(styled, newStyle);
        if (!localChanged && !styleChanged)
        {
            return;
        }

        var change = BeginChange(property, captureDescendants: true);
        if (localChanged) _localValues[property.Id] = newLocal;
        if (styleChanged) _styleValues[property.Id] = newStyle;
        EndChange(property, change);

        if (localChanged && _bindings.TryGetValue(property.Id, out var binding))
        {
            binding.OnTargetPropertyChanged(newLocal);
        }
    }

    private object? Recoerce(BindableProperty property, ref Dictionary<int, object?>? bases, object? current)
    {
        object? baseValue = bases != null && bases.TryGetValue(property.Id, out var b) ? b : current;
        object? coerced = property.CoerceUntyped(this, baseValue);
        TrackBaseValue(ref bases, property.Id, baseValue, coerced);
        return coerced;
    }

    private static void TrackBaseValue(ref Dictionary<int, object?>? bases, int id, object? baseValue, object? coerced)
    {
        if (Equals(baseValue, coerced))
        {
            bases?.Remove(id);
        }
        else
        {
            (bases ??= new())[id] = baseValue;
        }
    }

    /// <summary>
    /// Sets an animated value, which takes precedence over local, styled, inherited and default values without replacing them.
    /// </summary>
    /// <remarks>
    /// Use this from animations and transitions instead of <see cref="SetValue{T}(BindableProperty{T}, T)"/>: the element's own
    /// value (for example an <c>Opacity</c> of 0.5 set by the app) is preserved and comes back when
    /// <see cref="ClearAnimatedValue(BindableProperty)"/> is called. Animated values are validated but not coerced,
    /// and they are not written back through data bindings. Read-only properties can be animated.
    /// </remarks>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="property">The bindable property to animate.</param>
    /// <param name="value">The current animated value.</param>
    public void SetAnimatedValue<T>(BindableProperty<T> property, T value)
    {
        property.ValidateTyped(value);

        if (HasEqualValue(_animatedValues, property.Id, value))
        {
            return;
        }

        var change = BeginChange(property, captureDescendants: true);
        (_animatedValues ??= new())[property.Id] = value;
        EndChange(property, change);
    }

    /// <summary>
    /// Removes the animated value of <paramref name="property"/>, so the effective value falls back to the
    /// local, styled, inherited or default value underneath.
    /// </summary>
    /// <param name="property">The bindable property whose animation has finished.</param>
    public void ClearAnimatedValue(BindableProperty property)
    {
        if (_animatedValues == null || !_animatedValues.ContainsKey(property.Id))
        {
            return;
        }

        var change = BeginChange(property, captureDescendants: true);
        _animatedValues.Remove(property.Id);
        EndChange(property, change);
    }

    /// <summary>
    /// Subscribes to changes of the effective value of a single property on this object.
    /// </summary>
    /// <remarks>
    /// The handler runs after the property's registration callback and before <see cref="PropertyChanged"/> is raised.
    /// Prefer this over filtering <see cref="PropertyChanged"/> by name: it is typed, and it cannot confuse
    /// same-named properties from different owners.
    /// </remarks>
    /// <typeparam name="T">The type of the property value.</typeparam>
    /// <param name="property">The bindable property to observe.</param>
    /// <param name="handler">The handler receiving the object, the old value and the new value.</param>
    /// <returns>A token; dispose it to unsubscribe.</returns>
    public IDisposable Subscribe<T>(BindableProperty<T> property, PropertyChangedCallback<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var subscription = new PropertySubscription(this, property.Id, (sender, o, n) => handler(sender, (T)o!, (T)n!));

        // Copy-on-write arrays: subscribing is rare, notifying is frequent and must not allocate.
        _subscriptions ??= new();
        _subscriptions[property.Id] = _subscriptions.TryGetValue(property.Id, out var existing)
            ? [.. existing, subscription]
            : [subscription];
        return subscription;
    }

    private void Unsubscribe(PropertySubscription subscription)
    {
        if (_subscriptions == null || !_subscriptions.TryGetValue(subscription.PropertyId, out var existing))
        {
            return;
        }

        int index = Array.IndexOf(existing, subscription);
        if (index < 0)
        {
            return;
        }

        if (existing.Length == 1)
        {
            _subscriptions.Remove(subscription.PropertyId);
            return;
        }

        var remaining = new PropertySubscription[existing.Length - 1];
        Array.Copy(existing, 0, remaining, 0, index);
        Array.Copy(existing, index + 1, remaining, index, existing.Length - index - 1);
        _subscriptions[subscription.PropertyId] = remaining;
    }

    private sealed class PropertySubscription : IDisposable
    {
        private BindableObject? _owner;

        public PropertySubscription(BindableObject owner, int propertyId, Action<BindableObject, object?, object?> invoke)
        {
            _owner = owner;
            PropertyId = propertyId;
            Invoke = invoke;
        }

        public int PropertyId { get; }
        public Action<BindableObject, object?, object?> Invoke { get; }
        public bool IsDisposed => _owner == null;

        public void Dispose()
        {
            _owner?.Unsubscribe(this);
            _owner = null;
        }
    }

    // Scratch collections for SetStyleValues. A call takes ownership (sets the field to null) and hands it back
    // when done, so a re-entrant call from a change callback simply allocates its own instead of sharing one.
    [ThreadStatic] private static Dictionary<int, Setter>? t_styleSetterScratch;
    [ThreadStatic] private static List<int>? t_removedStyleIdScratch;

    private const int MaxStyleInheritanceDepth = 64;

    /// <summary>
    /// Replaces the complete set of styled values on this object with the setters of <paramref name="style"/> and its
    /// <see cref="Style.BasedOn"/> chain (derived setters win). Properties no longer styled revert to their inherited or
    /// default value; notifications are raised only for properties whose effective value changed.
    /// </summary>
    /// <param name="style">The resolved style, or <c>null</c> to clear all styled values.</param>
    /// <exception cref="InvalidOperationException">The <see cref="Style.BasedOn"/> chain is circular.</exception>
    internal void SetStyleValues(Style? style)
    {
        if (style == null && _styleValues.Count == 0)
        {
            return;
        }

        Dictionary<int, Setter>? next = null;
        List<int>? removed = null;
        try
        {
            if (style != null)
            {
                next = t_styleSetterScratch ?? new Dictionary<int, Setter>();
                t_styleSetterScratch = null;
                CollectSetters(style, next, depth: 0);
            }

            if (_styleValues.Count > 0)
            {
                foreach (var id in _styleValues.Keys)
                {
                    if (next == null || !next.ContainsKey(id))
                    {
                        if (removed == null)
                        {
                            removed = t_removedStyleIdScratch ?? new List<int>();
                            t_removedStyleIdScratch = null;
                        }
                        removed.Add(id);
                    }
                }

                if (removed != null)
                {
                    for (int i = 0; i < removed.Count; i++)
                    {
                        int id = removed[i];
                        var property = BindableProperty.FromId(id);
                        _styleBaseValues?.Remove(id);

                        var change = BeginChange(property, captureDescendants: !_localValues.ContainsKey(id));
                        _styleValues.Remove(id);
                        EndChange(property, change);
                    }
                }
            }

            if (next == null)
            {
                return;
            }

            foreach (var setter in next.Values)
            {
                var property = setter.Property;
                object? coerced = property.CoerceUntyped(this, setter.Value);

                if (property.HasCoercion)
                {
                    TrackBaseValue(ref _styleBaseValues, property.Id, setter.Value, coerced);
                }

                if (_styleValues.TryGetValue(property.Id, out var current) && Equals(current, coerced))
                {
                    continue;
                }

                var change = BeginChange(property, captureDescendants: !_localValues.ContainsKey(property.Id));
                _styleValues[property.Id] = coerced;
                EndChange(property, change);
            }
        }
        finally
        {
            if (next != null)
            {
                next.Clear();
                t_styleSetterScratch = next;
            }

            if (removed != null)
            {
                removed.Clear();
                t_removedStyleIdScratch = removed;
            }
        }
    }

    // Base styles first, so that setters of derived styles overwrite them.
    private static void CollectSetters(Style style, Dictionary<int, Setter> into, int depth)
    {
        if (depth > MaxStyleInheritanceDepth)
        {
            throw new InvalidOperationException(
                $"Style.BasedOn chain is deeper than {MaxStyleInheritanceDepth} levels; it is probably circular.");
        }

        if (style.BasedOn != null)
        {
            CollectSetters(style.BasedOn, into, depth + 1);
        }

        var setters = style.Setters;
        for (int i = 0; i < setters.Count; i++)
        {
            into[setters[i].Property.Id] = setters[i];
        }
    }

    #region Change tracking

    private readonly struct ValueChange
    {
        public ValueChange(object? oldValue, List<InheritedValueEntry>? descendants)
        {
            OldValue = oldValue;
            Descendants = descendants;
        }

        public object? OldValue { get; }
        public List<InheritedValueEntry>? Descendants { get; }
    }

    /// <summary>
    /// A descendant's effective value of an inheritable property, captured before a change so it can be diffed afterwards.
    /// </summary>
    internal readonly struct InheritedValueEntry
    {
        public InheritedValueEntry(BindableObject target, BindableProperty property, object? oldValue)
        {
            Target = target;
            Property = property;
            OldValue = oldValue;
        }

        public BindableObject Target { get; }
        public BindableProperty Property { get; }
        public object? OldValue { get; }
    }

    private ValueChange BeginChange(BindableProperty property, bool captureDescendants)
    {
        List<InheritedValueEntry>? descendants = null;
        if (captureDescendants && property.Inherits)
        {
            // Indexed loops over IReadOnlyList: foreach over the interface would box the enumerator on every change.
            var children = InheritanceChildren;
            for (int i = 0; i < children.Count; i++)
            {
                CaptureSubtree(children[i], property.InheritanceDependents, ref descendants);
            }
        }

        return new ValueChange(GetValueUntyped(property), descendants);
    }

    private void EndChange(BindableProperty property, ValueChange change)
    {
        object? newValue = GetValueUntyped(property);
        if (!Equals(change.OldValue, newValue))
        {
            RaiseEffectiveValueChanged(property, change.OldValue, newValue);
        }

        CommitInheritedValues(change.Descendants);
    }

    private void RaiseEffectiveValueChanged(BindableProperty property, object? oldValue, object? newValue)
    {
        property.InvokePropertyChangedUntyped(this, oldValue, newValue);
        OnPropertyValueChanged(property, oldValue, newValue);

        if (_subscriptions != null && _subscriptions.TryGetValue(property.Id, out var subscriptions))
        {
            // The array is never mutated, so handlers may subscribe or unsubscribe while being notified.
            foreach (var subscription in subscriptions)
            {
                if (!subscription.IsDisposed)
                {
                    subscription.Invoke(this, oldValue, newValue);
                }
            }
        }

        OnPropertyChanged(property.Name);
    }

    /// <summary>
    /// Invoked whenever the effective value of any bindable property on this object changes, after the property's
    /// registration callback and before per-property subscribers and <see cref="PropertyChanged"/>.
    /// </summary>
    /// <remarks>Derived classes use this to apply cross-cutting behavior such as <see cref="PropertyOptions"/>.</remarks>
    /// <param name="property">The property whose effective value changed.</param>
    /// <param name="oldValue">The previous effective value.</param>
    /// <param name="newValue">The new effective value.</param>
    protected virtual void OnPropertyValueChanged(BindableProperty property, object? oldValue, object? newValue)
    {
    }

    /// <summary>
    /// Captures the inherited values of this object and its whole subtree for every inheritable property.
    /// Call before changing <see cref="InheritanceParent"/>, then pass the result to <see cref="CommitInheritedValues"/> afterwards.
    /// </summary>
    internal List<InheritedValueEntry>? CaptureInheritedValues()
    {
        List<InheritedValueEntry>? entries = null;
        CaptureSubtree(this, BindableProperty.AllInheritableArray, ref entries);
        return entries;
    }

    /// <summary>
    /// Raises change notifications for every captured entry whose effective value differs from the captured one.
    /// Entries are processed in capture (pre-order) order, so ancestors are notified before their descendants.
    /// </summary>
    internal static void CommitInheritedValues(List<InheritedValueEntry>? entries)
    {
        if (entries == null)
        {
            return;
        }

        foreach (var entry in entries)
        {
            object? newValue = entry.Target.GetValueUntyped(entry.Property);
            if (!Equals(entry.OldValue, newValue))
            {
                entry.Target.RaiseEffectiveValueChanged(entry.Property, entry.OldValue, newValue);
            }
        }
    }

    private static void CaptureSubtree(BindableObject node, BindableProperty[] properties, ref List<InheritedValueEntry>? entries)
    {
        bool shadowsAll = true;
        foreach (var property in properties)
        {
            // Only nodes that actually carry the property are notified; this also keeps typed callbacks such as
            // (s, o, n) => ((TextBlock)s).InvalidateMeasure() from being invoked on unrelated node types.
            if (!node.HasOwnValue(property.Id) && property.TargetType.IsInstanceOfType(node))
            {
                (entries ??= new()).Add(new InheritedValueEntry(node, property, node.GetValueUntyped(property)));
            }

            if (shadowsAll && !node.HasOwnValueForAny(property.InheritanceSources))
            {
                shadowsAll = false;
            }
        }

        // If this node supplies its own value for every affected property, nothing below it can change.
        if (shadowsAll)
        {
            return;
        }

        var children = node.InheritanceChildren;
        for (int i = 0; i < children.Count; i++)
        {
            CaptureSubtree(children[i], properties, ref entries);
        }
    }

    #endregion

    #region Value lookup

    private bool TryGetOwnValue(int id, out object? value)
    {
        return (_animatedValues != null && _animatedValues.TryGetValue(id, out value))
            || _localValues.TryGetValue(id, out value)
            || _styleValues.TryGetValue(id, out value);
    }

    private bool HasOwnValue(int id) =>
        (_animatedValues != null && _animatedValues.ContainsKey(id)) || _localValues.ContainsKey(id) || _styleValues.ContainsKey(id);

    private bool HasOwnValueForAny(BindableProperty[] properties)
    {
        foreach (var property in properties)
        {
            if (HasOwnValue(property.Id))
            {
                return true;
            }
        }
        return false;
    }

    private bool TryGetInheritedValue(BindableProperty property, out object? value)
    {
        var sources = property.InheritanceSources;
        for (var current = InheritanceParent; current != null; current = current.InheritanceParent)
        {
            // The property itself first, then same-named aliases (e.g. Control.FontSize for TextBlock.FontSize).
            for (int i = 0; i < sources.Length; i++)
            {
                if (current.TryGetOwnValue(sources[i].Id, out value))
                {
                    return true;
                }
            }
        }

        value = null;
        return false;
    }

    #endregion

    /// <summary>
    /// Invoked after this object has been attached to, detached from, or moved to a different inheritance parent.
    /// Inherited value notifications for this object and its subtree have already been raised at this point.
    /// </summary>
    /// <param name="oldParent">The previous inheritance parent, or <c>null</c>.</param>
    /// <param name="newParent">The new inheritance parent, or <c>null</c>.</param>
    internal virtual void OnInheritanceParentChanged(BindableObject? oldParent, BindableObject? newParent)
    {
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
        property.ThrowIfReadOnly();

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
    /// property value is updated. If the data context becomes <c>null</c> or is not a <typeparamref name="TDataContext"/>,
    /// the value written by the binding is cleared. If a <paramref name="setter"/> is provided, two-way updates are
    /// propagated back to the data context according to <paramref name="updateSourceTrigger"/>.
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
        property.ThrowIfReadOnly();

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
    /// Updates the binding source whenever the target element loses UI focus. Requires a <see cref="Tree.UIElement"/> target.
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
/// <remarks>
/// The target is held weakly; if it is garbage-collected, the binding unsubscribes from the source on the next source notification.
/// The source is held strongly for the lifetime of the binding.
/// </remarks>
/// <typeparam name="TTarget">The data type of the target property.</typeparam>
/// <typeparam name="TSource">The type of the source object. Must be a reference type.</typeparam>
public sealed class PropertyBinding<TTarget, TSource> : IBindingSubscription
    where TSource : class
{
    private readonly WeakReference<BindableObject> _targetRef;
    private readonly BindableProperty<TTarget> _property;
    private readonly TSource _source;
    private readonly Func<TSource, TTarget> _getter;
    private readonly Action<TSource, TTarget>? _setter;
    private readonly UpdateSourceTrigger _updateSourceTrigger;
    private INotifyPropertyChanged? _inpc;
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
    /// <exception cref="ArgumentException"><paramref name="updateSourceTrigger"/> is <see cref="UpdateSourceTrigger.LostFocus"/> but <paramref name="target"/> is not a <see cref="Tree.UIElement"/>.</exception>
    public PropertyBinding(
        BindableObject target,
        BindableProperty<TTarget> property,
        TSource source,
        Func<TSource, TTarget> getter,
        Action<TSource, TTarget>? setter,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged)
    {
        BindingHelpers.ValidateTrigger(target, updateSourceTrigger);

        _targetRef = new WeakReference<BindableObject>(target);
        _property = property;
        _source = source;
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
        if (!_targetRef.TryGetTarget(out _))
        {
            Dispose();
            return;
        }

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
            if (_targetRef.TryGetTarget(out var target))
            {
                var value = _getter(_source);
                target.SetValue(_property, value);
                _hasPendingValue = false;

                // If the target coerced the value, write the coerced value back so source and target agree.
                if (_setter != null)
                {
                    var actual = target.GetValue(_property);
                    if (!EqualityComparer<TTarget>.Default.Equals(actual, value))
                    {
                        _setter(_source, actual);
                    }
                }
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
            _setter(_source, (TTarget)value!);
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
            _inpc = null;
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
/// the target property is updated. When the data context becomes <c>null</c> or is not a <typeparamref name="TDataContext"/>,
/// the value previously written by this binding is cleared. For two-way bindings (when a setter is provided), changes to the
/// target property are pushed back to the data context according to <see cref="UpdateSourceTrigger"/>.
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
    private readonly IDisposable _dataContextSubscription;
    private INotifyPropertyChanged? _currentInpc;
    private object? _pendingValue;
    private bool _hasPendingValue;
    private bool _isUpdating;
    private bool _hasAppliedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataContextBinding{TTarget, TDataContext}"/> class.
    /// </summary>
    /// <param name="target">The target <see cref="BindableObject"/> on which the property resides.</param>
    /// <param name="property">The target <see cref="BindableProperty{TTarget}"/> to bind.</param>
    /// <param name="getter">The getter delegate to extract values from the data context.</param>
    /// <param name="setter">An optional setter delegate to write values back to the data context for two-way binding.</param>
    /// <param name="updateSourceTrigger">Specifies when two-way changes are pushed back to the data context. Defaults to <see cref="UpdateSourceTrigger.PropertyChanged"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="updateSourceTrigger"/> is <see cref="UpdateSourceTrigger.LostFocus"/> but <paramref name="target"/> is not a <see cref="Tree.UIElement"/>.</exception>
    public DataContextBinding(
        BindableObject target,
        BindableProperty<TTarget> property,
        Func<TDataContext, TTarget> getter,
        Action<TDataContext, TTarget>? setter,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged)
    {
        BindingHelpers.ValidateTrigger(target, updateSourceTrigger);

        _targetRef = new WeakReference<BindableObject>(target);
        _property = property;
        _getter = getter;
        _setter = setter;
        _updateSourceTrigger = updateSourceTrigger;

        _dataContextSubscription = target.Subscribe(BindableObject.DataContextProperty, OnTargetDataContextChanged);
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

    private void OnTargetDataContextChanged(BindableObject sender, object? oldValue, object? newValue)
    {
        HookDataContext(newValue);
        UpdateTarget();
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
        if (!_targetRef.TryGetTarget(out _))
        {
            Dispose();
            return;
        }

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
            if (!_targetRef.TryGetTarget(out var target))
            {
                return;
            }

            if (target.DataContext is TDataContext dc)
            {
                var value = _getter(dc);
                target.SetValue(_property, value);
                _hasAppliedValue = true;
                _hasPendingValue = false;

                // If the target coerced the value, write the coerced value back so source and target agree.
                if (_setter != null)
                {
                    var actual = target.GetValue(_property);
                    if (!EqualityComparer<TTarget>.Default.Equals(actual, value))
                    {
                        _setter(dc, actual);
                    }
                }
            }
            else if (_hasAppliedValue)
            {
                // No usable data context: drop the stale value from the previous one.
                target.ClearValue(_property);
                _hasAppliedValue = false;
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
        _dataContextSubscription.Dispose();

        if (_targetRef.TryGetTarget(out var target))
        {
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

internal static class BindingHelpers
{
    public static void ValidateTrigger(BindableObject target, UpdateSourceTrigger trigger)
    {
        if (trigger == UpdateSourceTrigger.LostFocus && target is not Tree.UIElement)
        {
            throw new ArgumentException(
                $"{nameof(UpdateSourceTrigger)}.{nameof(UpdateSourceTrigger.LostFocus)} requires a {nameof(Tree.UIElement)} target, " +
                $"but the target is '{target.GetType().Name}'. Use {nameof(UpdateSourceTrigger.Explicit)} instead.",
                nameof(trigger));
        }
    }
}
