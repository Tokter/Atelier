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

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyDescriptor{TTarget, TProp}"/> class.
    /// </summary>
    /// <param name="name">The property name.</param>
    /// <param name="displayName">The display name; <see langword="null"/> falls back to <paramref name="name"/>.</param>
    /// <param name="category">The category; <see langword="null"/> falls back to <c>"General"</c>.</param>
    /// <param name="getter">Reads the property value from a target.</param>
    /// <param name="setter">Writes the property value to a target, or <see langword="null"/> for a read-only property.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="getter"/> is <see langword="null"/>.</exception>
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
    /// <param name="target">The object to read from.</param>
    /// <returns>The property value.</returns>
    public TProp GetTypedValue(TTarget target) => _getter(target);

    /// <summary>
    /// Sets the property value without unboxing.
    /// </summary>
    /// <param name="target">The object to write to.</param>
    /// <param name="value">The value to set.</param>
    /// <exception cref="InvalidOperationException">The property is read-only.</exception>
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
