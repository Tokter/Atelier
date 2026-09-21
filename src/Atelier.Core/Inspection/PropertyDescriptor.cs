using System;

namespace Atelier.Core.Inspection;

/// <summary>
/// A strongly-typed, reflection-free property descriptor that provides zero-boxing access
/// as well as non-generic object-level access for UI inspector tools.
/// </summary>
/// <typeparam name="TTarget">The declaring target object type.</typeparam>
/// <typeparam name="TProp">The property value type.</typeparam>
public sealed class PropertyDescriptor<TTarget, TProp> : IPropertyDescriptor
{
    private readonly Func<TTarget, TProp> _getter;
    private readonly Action<TTarget, TProp>? _setter;

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string DisplayName { get; }

    /// <inheritdoc />
    public string Category { get; }

    /// <inheritdoc />
    public Type PropertyType => typeof(TProp);

    /// <inheritdoc />
    public bool IsReadOnly => _setter is null;

    /// <summary>
    /// Gets the strongly typed getter delegate.
    /// </summary>
    public Func<TTarget, TProp> Getter => _getter;

    /// <summary>
    /// Gets the strongly typed setter delegate (null if read-only).
    /// </summary>
    public Action<TTarget, TProp>? Setter => _setter;

    public PropertyDescriptor(
        string name,
        string displayName,
        string category,
        Func<TTarget, TProp> getter,
        Action<TTarget, TProp>? setter = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        DisplayName = displayName ?? name;
        Category = category ?? "General";
        _getter = getter ?? throw new ArgumentNullException(nameof(getter));
        _setter = setter;
    }

    /// <summary>
    /// Reads the property value without boxing.
    /// </summary>
    public TProp GetTypedValue(TTarget target) => _getter(target);

    /// <summary>
    /// Sets the property value without unboxing.
    /// </summary>
    public void SetTypedValue(TTarget target, TProp value)
    {
        if (_setter is null)
            throw new InvalidOperationException($"Property {Name} on {typeof(TTarget).Name} is read-only.");

        _setter(target, value);
    }

    /// <inheritdoc />
    public object? GetValue(object target)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        return _getter((TTarget)target);
    }

    /// <inheritdoc />
    public void SetValue(object target, object? value)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (_setter is null)
            throw new InvalidOperationException($"Property {Name} on {typeof(TTarget).Name} is read-only.");

        TProp typedValue = value is null ? default! : (TProp)value;
        _setter((TTarget)target, typedValue);
    }
}
