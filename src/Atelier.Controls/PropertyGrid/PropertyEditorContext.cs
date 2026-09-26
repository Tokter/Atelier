using System;
using Atelier.Core.Inspection;

namespace Atelier.Controls;

/// <summary>
/// Context provided to built-in and custom property editors to read, write and observe one property of the inspected
/// object. It catches exceptions thrown by the property's getter and setter and reports them through <see cref="Error"/>.
/// </summary>
/// <remarks>
/// Editors subscribe to <see cref="ValueChanged"/> to reflect values set through the context and changes detected by
/// <see cref="Refresh"/> (called by the grid for <see cref="System.ComponentModel.INotifyPropertyChanged"/> notifications,
/// after edits of other properties and by <see cref="PropertyGrid.Refresh"/>). Contexts and their editors live as long
/// as the grid's row for the property.
/// </remarks>
public sealed class PropertyEditorContext
{
    private object? _lastValue;
    private bool _isUpdating;

    /// <summary>
    /// Gets the parent <see cref="PropertyGrid"/> hosting the editor.
    /// </summary>
    public PropertyGrid Grid { get; }

    /// <summary>
    /// Gets the target object instance whose property is being edited.
    /// </summary>
    public object Target { get; }

    /// <summary>
    /// Gets the property descriptor containing metadata and strongly typed accessors.
    /// </summary>
    public IPropertyDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the current property value on the target object, or <see langword="null"/> if the getter throws (the
    /// exception is reported through <see cref="Error"/> and <see cref="PropertyGrid.PropertyValueError"/>).
    /// </summary>
    public object? Value
    {
        get
        {
            TryReadValue(out object? value);
            return value;
        }
    }

    /// <summary>
    /// Gets whether the property is read-only.
    /// </summary>
    public bool IsReadOnly => Descriptor.IsReadOnly;

    /// <summary>
    /// Gets whether the property type is a <see cref="Nullable{T}"/> value type, whose editors accept clearing to
    /// <see langword="null"/>.
    /// </summary>
    public bool IsNullable { get; }

    /// <summary>
    /// Gets the property type with <see cref="Nullable{T}"/> removed (for <c>int?</c> this is <see cref="int"/>).
    /// </summary>
    public Type ValueType { get; }

    /// <summary>
    /// Gets the current error message of the property (a failed read or write, or an error reported by the editor with
    /// <see cref="SetError"/>), or <see langword="null"/> if there is none.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Gets whether <see cref="Error"/> is set.
    /// </summary>
    public bool HasError => Error != null;

    /// <summary>
    /// Raised with the new value when a value is set through <see cref="UpdateValue"/> or when <see cref="Refresh"/>
    /// detects that the value changed.
    /// </summary>
    public event Action<object?>? ValueChanged;

    /// <summary>
    /// Raised when <see cref="Error"/> changes; the argument is the new message or <see langword="null"/>.
    /// </summary>
    public event Action<string?>? ErrorChanged;

    /// <summary>
    /// Initializes a new context and reads the current value of the property.
    /// </summary>
    /// <param name="grid">The grid hosting the editor.</param>
    /// <param name="target">The inspected object.</param>
    /// <param name="descriptor">The descriptor of the edited property.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public PropertyEditorContext(PropertyGrid grid, object target, IPropertyDescriptor descriptor)
    {
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));

        Type propertyType = descriptor.PropertyType;
        Type? underlying = propertyType is null ? null : Nullable.GetUnderlyingType(propertyType);
        IsNullable = underlying != null;
        ValueType = underlying ?? propertyType ?? typeof(object);

        TryReadValue(out _lastValue);
    }

    /// <summary>
    /// Gets the strongly typed current value of the property, or default if null/mismatched.
    /// </summary>
    public T? GetValue<T>()
    {
        object? val = Value;
        return val is T typed ? typed : default;
    }

    /// <summary>
    /// Sets a new property value on the target object and notifies the <see cref="PropertyGrid"/>.
    /// </summary>
    /// <remarks>
    /// Raises <see cref="PropertyGrid.PropertyValueChanging"/> first (a handler may cancel), then calls the setter. An
    /// exception thrown by the setter is caught, stored in <see cref="Error"/> and raised as
    /// <see cref="PropertyGrid.PropertyValueError"/>. On success the error is cleared, <see cref="ValueChanged"/> and
    /// <see cref="PropertyGrid.PropertyValueChanged"/> are raised and the grid refreshes the other rows.
    /// </remarks>
    /// <param name="newValue">The value to set.</param>
    /// <returns>
    /// <see langword="true"/> if the property now holds <paramref name="newValue"/> (including when it already did);
    /// <see langword="false"/> if the property is read-only, the change was canceled or the setter threw.
    /// </returns>
    public bool UpdateValue(object? newValue)
    {
        if (Descriptor.IsReadOnly)
            return false;

        TryReadValue(out object? oldValue);
        if (Equals(oldValue, newValue))
        {
            SetError(null);
            return true;
        }

        if (!Grid.RaisePropertyValueChanging(Target, Descriptor, oldValue, newValue))
            return false;

        _isUpdating = true;
        try
        {
            Descriptor.SetValue(Target, newValue);
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            Grid.RaisePropertyValueError(Target, Descriptor, ex, newValue, isReadError: false);
            return false;
        }
        finally
        {
            _isUpdating = false;
        }

        _lastValue = newValue;
        SetError(null);
        ValueChanged?.Invoke(newValue);
        Grid.OnValueCommitted(this, oldValue, newValue);
        return true;
    }

    /// <summary>
    /// Re-reads the property and raises <see cref="ValueChanged"/> if the value differs from the last value seen by
    /// this context. Does nothing while <see cref="UpdateValue"/> is running.
    /// </summary>
    public void Refresh()
    {
        if (_isUpdating)
            return;

        bool hadReadError = Error != null && _lastReadFailed;
        if (!TryReadValue(out object? value))
            return;

        if (!hadReadError && Equals(value, _lastValue))
            return;

        _lastValue = value;
        SetError(null);
        ValueChanged?.Invoke(value);
    }

    /// <summary>
    /// Sets or clears (<see langword="null"/>) the error shown for this property, for example when an editor rejects
    /// the text the user entered.
    /// </summary>
    /// <param name="message">The error message, or <see langword="null"/> to clear the error.</param>
    public void SetError(string? message)
    {
        if (message == null)
        {
            _lastReadFailed = false;
        }

        if (string.Equals(Error, message, StringComparison.Ordinal))
            return;

        Error = message;
        ErrorChanged?.Invoke(message);
    }

    private bool _lastReadFailed;

    // Reads the value, converting a getter exception into an error (reported once per distinct message).
    private bool TryReadValue(out object? value)
    {
        try
        {
            value = Descriptor.GetValue(Target);
            if (_lastReadFailed)
            {
                SetError(null);
            }
            return true;
        }
        catch (Exception ex)
        {
            value = null;
            string message = ex.Message;
            if (!_lastReadFailed || !string.Equals(Error, message, StringComparison.Ordinal))
            {
                SetError(message);
                _lastReadFailed = true;
                Grid.RaisePropertyValueError(Target, Descriptor, ex, null, isReadError: true);
            }
            return false;
        }
    }
}
