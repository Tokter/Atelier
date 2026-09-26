using System;
using Atelier.Core.Inspection;

namespace Atelier.Controls;

/// <summary>
/// Provides data for the <see cref="PropertyGrid.PropertyValueChanged"/> event.
/// </summary>
public sealed class PropertyValueChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the target object that was modified.
    /// </summary>
    public object Target { get; }

    /// <summary>
    /// Gets the property descriptor that was modified.
    /// </summary>
    public IPropertyDescriptor Property { get; }

    /// <summary>
    /// Gets the previous value of the property before the change.
    /// </summary>
    public object? OldValue { get; }

    /// <summary>
    /// Gets the new value of the property after the change.
    /// </summary>
    public object? NewValue { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyValueChangedEventArgs"/> class.
    /// </summary>
    /// <param name="target">The modified object.</param>
    /// <param name="property">The descriptor of the modified property.</param>
    /// <param name="oldValue">The value before the change.</param>
    /// <param name="newValue">The value after the change.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="property"/> is <see langword="null"/>.</exception>
    public PropertyValueChangedEventArgs(object target, IPropertyDescriptor property, object? oldValue, object? newValue)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Property = property ?? throw new ArgumentNullException(nameof(property));
        OldValue = oldValue;
        NewValue = newValue;
    }
}

/// <summary>
/// Provides data for the cancelable <see cref="PropertyGrid.PropertyValueChanging"/> event, raised before an editor
/// sets a property.
/// </summary>
public sealed class PropertyValueChangingEventArgs : EventArgs
{
    /// <summary>
    /// Gets the object about to be modified.
    /// </summary>
    public object Target { get; }

    /// <summary>
    /// Gets the descriptor of the property about to be modified.
    /// </summary>
    public IPropertyDescriptor Property { get; }

    /// <summary>
    /// Gets the current value of the property.
    /// </summary>
    public object? OldValue { get; }

    /// <summary>
    /// Gets the value the editor is about to set.
    /// </summary>
    public object? NewValue { get; }

    /// <summary>
    /// Gets or sets whether the change is canceled; the editor then shows the current value again.
    /// </summary>
    public bool Cancel { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyValueChangingEventArgs"/> class.
    /// </summary>
    /// <param name="target">The object about to be modified.</param>
    /// <param name="property">The descriptor of the property.</param>
    /// <param name="oldValue">The current value.</param>
    /// <param name="newValue">The proposed value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="property"/> is <see langword="null"/>.</exception>
    public PropertyValueChangingEventArgs(object target, IPropertyDescriptor property, object? oldValue, object? newValue)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Property = property ?? throw new ArgumentNullException(nameof(property));
        OldValue = oldValue;
        NewValue = newValue;
    }
}

/// <summary>
/// Provides data for the <see cref="PropertyGrid.PropertyValueError"/> event, raised when a property getter or setter
/// throws.
/// </summary>
public sealed class PropertyValueErrorEventArgs : EventArgs
{
    /// <summary>
    /// Gets the inspected object.
    /// </summary>
    public object Target { get; }

    /// <summary>
    /// Gets the descriptor of the failing property.
    /// </summary>
    public IPropertyDescriptor Property { get; }

    /// <summary>
    /// Gets the exception thrown by the getter or setter.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the value that was being set, or <see langword="null"/> for a read error.
    /// </summary>
    public object? AttemptedValue { get; }

    /// <summary>
    /// Gets whether the getter (<see langword="true"/>) or the setter (<see langword="false"/>) threw.
    /// </summary>
    public bool IsReadError { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyValueErrorEventArgs"/> class.
    /// </summary>
    /// <param name="target">The inspected object.</param>
    /// <param name="property">The descriptor of the failing property.</param>
    /// <param name="exception">The exception thrown by the accessor.</param>
    /// <param name="attemptedValue">The value being set, or <see langword="null"/> for a read error.</param>
    /// <param name="isReadError">Whether the getter threw.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/>, <paramref name="property"/> or <paramref name="exception"/> is <see langword="null"/>.</exception>
    public PropertyValueErrorEventArgs(object target, IPropertyDescriptor property, Exception exception, object? attemptedValue, bool isReadError)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Property = property ?? throw new ArgumentNullException(nameof(property));
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        AttemptedValue = attemptedValue;
        IsReadError = isReadError;
    }
}
