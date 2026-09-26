using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Atelier.Core.Properties;

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
/// Implemented by bindings so the property system can report typed target changes without boxing value types.
/// </summary>
/// <typeparam name="T">The target property's value type.</typeparam>
internal interface ITypedBindingSubscription<in T>
{
    /// <summary>Typed counterpart of <see cref="IBindingSubscription.OnTargetPropertyChanged(object?)"/>.</summary>
    void OnTargetPropertyChanged(T newValue);
}

/// <summary>
/// Specifies the direction of data flow in a binding.
/// </summary>
public enum BindingMode
{
    /// <summary><see cref="TwoWay"/> when a setter is supplied, otherwise <see cref="OneWay"/>.</summary>
    Default = 0,

    /// <summary>Source changes update the target; target changes are not written back.</summary>
    OneWay = 1,

    /// <summary>Source changes update the target and target changes are written back (requires a setter).</summary>
    TwoWay = 2,

    /// <summary>
    /// The target is set once from the source and then not updated from source notifications (a data-context binding
    /// still re-reads when the data context changes). Useful for values that never change, as it avoids the subscription.
    /// </summary>
    OneTime = 3,

    /// <summary>
    /// Target changes are written to the source (requires a setter); the source never updates the target. The target's
    /// current value is written to the source when the binding is created (and when the data context changes).
    /// </summary>
    OneWayToSource = 4,
}

/// <summary>
/// Optional settings for a binding created with <see cref="BindableObject.SetBinding{TTarget, TSource}(BindableProperty{TTarget}, TSource, Func{TSource, TTarget}, Action{TSource, TTarget}?, UpdateSourceTrigger, BindingOptions{TTarget}?, string?)"/>.
/// </summary>
/// <typeparam name="T">The target property's value type.</typeparam>
public sealed class BindingOptions<T>
{
    private readonly T _fallbackValue = default!;
    private readonly T _targetNullValue = default!;

    /// <summary>Gets the direction of data flow. The default is <see cref="BindingMode.Default"/>.</summary>
    public BindingMode Mode { get; init; }

    /// <summary>
    /// Gets the value used when the binding cannot produce one: the getter throws (for example a
    /// <see cref="NullReferenceException"/> for <c>x =&gt; x.Customer.Name</c> while <c>Customer</c> is null), or a
    /// data-context binding has no data context of the expected type. Without a fallback, a throwing getter propagates its
    /// exception and a missing data context clears the bound value.
    /// </summary>
    public T FallbackValue
    {
        get => _fallbackValue;
        init { _fallbackValue = value; HasFallbackValue = true; }
    }

    /// <summary>Gets whether <see cref="FallbackValue"/> was set.</summary>
    public bool HasFallbackValue { get; private init; }

    /// <summary>
    /// Gets the value shown when the getter returns <c>null</c>. In a two-way binding, a target value equal to it is written
    /// back to the source as <c>null</c>.
    /// </summary>
    public T TargetNullValue
    {
        get => _targetNullValue;
        init { _targetNullValue = value; HasTargetNullValue = true; }
    }

    /// <summary>Gets whether <see cref="TargetNullValue"/> was set.</summary>
    public bool HasTargetNullValue { get; private init; }
}

/// <summary>
/// The shared implementation of <see cref="PropertyBinding{TTarget, TSource}"/> and
/// <see cref="DataContextBinding{TTarget, TDataContext}"/>: they differ only in where the source comes from.
/// </summary>
internal sealed class BindingEngine<TTarget, TSource> : IBindingSubscription, ITypedBindingSubscription<TTarget>
    where TSource : class
{
    private readonly WeakReference<BindableObject> _targetRef;
    private readonly BindableProperty<TTarget> _property;
    private readonly Func<TSource, TTarget> _getter;
    private readonly Action<TSource, TTarget>? _setter;
    private readonly UpdateSourceTrigger _updateSourceTrigger;
    private readonly BindingMode _mode;
    private readonly BindingOptions<TTarget>? _options;
    private readonly string? _sourcePropertyName;

    private TSource? _source;
    private INotifyPropertyChanged? _inpc;
    private INotifyDataErrorInfo? _errorInfo;
    private TTarget _pendingValue = default!;
    private bool _hasPendingValue;
    private bool _isUpdating;
    private bool _hasAppliedValue;
    private bool _isDisposed;
    private QueuedTargetUpdate? _queuedUpdate;

    public BindingEngine(
        BindableObject target,
        BindableProperty<TTarget> property,
        Func<TSource, TTarget> getter,
        Action<TSource, TTarget>? setter,
        UpdateSourceTrigger updateSourceTrigger,
        BindingOptions<TTarget>? options,
        string? sourcePropertyName)
    {
        ArgumentNullException.ThrowIfNull(getter);
        BindingHelpers.ValidateTrigger(target, updateSourceTrigger);

        _mode = (options?.Mode ?? BindingMode.Default) switch
        {
            BindingMode.Default => setter != null ? BindingMode.TwoWay : BindingMode.OneWay,
            var mode => mode,
        };

        if (setter == null && (_mode == BindingMode.TwoWay || _mode == BindingMode.OneWayToSource))
        {
            throw new ArgumentException($"{nameof(BindingMode)}.{_mode} requires a setter.", nameof(setter));
        }

        _targetRef = new WeakReference<BindableObject>(target);
        _property = property;
        _getter = getter;
        _setter = setter;
        _updateSourceTrigger = updateSourceTrigger;
        _options = options;
        _sourcePropertyName = sourcePropertyName;

        if (WritesToSource && target is Tree.UIElement uie && _updateSourceTrigger == UpdateSourceTrigger.LostFocus)
        {
            uie.LostFocus += OnTargetLostFocus;
        }
    }

    private bool WritesToSource => _mode is BindingMode.TwoWay or BindingMode.OneWayToSource;

    private bool ListensToSource => _mode is BindingMode.OneWay or BindingMode.TwoWay;

    /// <summary>
    /// Switches to a new source (or none) and synchronizes: the target is updated from it, or for
    /// <see cref="BindingMode.OneWayToSource"/> the target value is written to it.
    /// </summary>
    public void SetSource(TSource? source)
    {
        if (_isDisposed)
        {
            return;
        }

        UnhookSource();
        _source = source;

        if (ListensToSource && source is INotifyPropertyChanged inpc)
        {
            _inpc = inpc;
            _inpc.PropertyChanged += OnSourcePropertyChanged;
        }

        // Errors are tracked per source property, so the property the getter reads must be known.
        if (_sourcePropertyName != null && source is INotifyDataErrorInfo errorInfo)
        {
            _errorInfo = errorInfo;
            _errorInfo.ErrorsChanged += OnSourceErrorsChanged;
        }
        RefreshErrors();

        if (_mode == BindingMode.OneWayToSource)
        {
            UpdateSource();
        }
        else
        {
            UpdateTarget();
        }
    }

    private void UnhookSource()
    {
        if (_inpc != null)
        {
            _inpc.PropertyChanged -= OnSourcePropertyChanged;
            _inpc = null;
        }

        if (_errorInfo != null)
        {
            _errorInfo.ErrorsChanged -= OnSourceErrorsChanged;
            _errorInfo = null;
        }
    }

    private void OnTargetLostFocus(object? sender, EventArgs e)
    {
        if (_hasPendingValue)
        {
            _hasPendingValue = false;
            UpdateSourceTyped(_pendingValue);
        }
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_targetRef.TryGetTarget(out _))
        {
            Dispose();
            return;
        }

        if (!BindingHelpers.Affects(e, _sourcePropertyName))
        {
            return;
        }

        // View models may change from background work; the target may only be updated on the UI thread.
        if (Threading.Dispatcher.CheckAccess())
        {
            UpdateTarget();
        }
        else
        {
            (_queuedUpdate ??= new QueuedTargetUpdate(UpdateTarget)).Post();
        }
    }

    private void OnSourceErrorsChanged(object? sender, DataErrorsChangedEventArgs e)
    {
        if (!_targetRef.TryGetTarget(out _))
        {
            Dispose();
            return;
        }

        if (!string.IsNullOrEmpty(e.PropertyName) && !string.Equals(e.PropertyName, _sourcePropertyName, StringComparison.Ordinal))
        {
            return;
        }

        // View models may raise this from background validation; the target must be updated on the UI thread.
        if (Threading.Dispatcher.CheckAccess())
        {
            RefreshErrors();
        }
        else
        {
            Threading.Dispatcher.Post(RefreshErrors);
        }
    }

    private void RefreshErrors()
    {
        if (!_targetRef.TryGetTarget(out var target))
        {
            return;
        }

        List<object>? errors = null;
        if (!_isDisposed && _errorInfo != null && _errorInfo.HasErrors)
        {
            foreach (var error in _errorInfo.GetErrors(_sourcePropertyName))
            {
                if (error != null)
                {
                    (errors ??= new List<object>()).Add(error);
                }
            }
        }

        Validation.SetBindingErrors(target, this, errors);
    }

    public void UpdateTarget()
    {
        if (_isUpdating || _isDisposed || _mode == BindingMode.OneWayToSource) return;
        _isUpdating = true;
        try
        {
            if (!_targetRef.TryGetTarget(out var target))
            {
                return;
            }

            if (_source == null)
            {
                if (_options is { HasFallbackValue: true })
                {
                    target.SetValue(_property, _options.FallbackValue);
                    _hasAppliedValue = true;
                }
                else if (_hasAppliedValue)
                {
                    // No usable source: drop the stale value written for the previous one.
                    target.ClearValue(_property);
                    _hasAppliedValue = false;
                }
                _hasPendingValue = false;
                return;
            }

            TTarget value;
            try
            {
                value = _getter(_source);
            }
            catch (Exception ex) when (_options is { HasFallbackValue: true })
            {
                Debug.WriteLine($"[Binding] Getter for {_property.OwnerType.Name}.{_property.Name} failed, using FallbackValue: {ex.Message}");
                value = _options.FallbackValue;
            }

            if (value is null && _options is { HasTargetNullValue: true })
            {
                value = _options.TargetNullValue;
            }

            target.SetValue(_property, value);
            _hasAppliedValue = true;
            _hasPendingValue = false;

            // If the target coerced the value, write the coerced value back so source and target agree.
            if (_mode == BindingMode.TwoWay)
            {
                var actual = target.GetValue(_property);
                if (!EqualityComparer<TTarget>.Default.Equals(actual, value))
                {
                    _setter!(_source, actual);
                }
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    public void OnTargetPropertyChanged(object? newValue) => OnTargetChanged((TTarget)newValue!);

    void ITypedBindingSubscription<TTarget>.OnTargetPropertyChanged(TTarget newValue) => OnTargetChanged(newValue);

    private void OnTargetChanged(TTarget newValue)
    {
        if (_isUpdating || !WritesToSource) return;

        if (_updateSourceTrigger == UpdateSourceTrigger.PropertyChanged)
        {
            UpdateSourceTyped(newValue);
        }
        else
        {
            _pendingValue = newValue;
            _hasPendingValue = true;
        }
    }

    public void UpdateSource()
    {
        if (_hasPendingValue)
        {
            _hasPendingValue = false;
            UpdateSourceTyped(_pendingValue);
        }
        else if (_targetRef.TryGetTarget(out var target))
        {
            UpdateSourceTyped(target.GetValue(_property));
        }
    }

    private void UpdateSourceTyped(TTarget value)
    {
        if (_isUpdating || _isDisposed || !WritesToSource || _source == null) return;

        if (_options is { HasTargetNullValue: true } && EqualityComparer<TTarget>.Default.Equals(value, _options.TargetNullValue))
        {
            value = default!;
        }

        _isUpdating = true;
        try
        {
            _setter!(_source, value);
        }
        finally
        {
            _isUpdating = false;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        // Only unsubscribe: like before, removing a binding leaves the last bound value in place.
        _isDisposed = true;
        UnhookSource();
        _source = null;

        if (_targetRef.TryGetTarget(out var target))
        {
            Validation.SetBindingErrors(target, this, null);
            if (target is Tree.UIElement uie)
            {
                uie.LostFocus -= OnTargetLostFocus;
            }
        }
    }
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
public sealed class PropertyBinding<TTarget, TSource> : IBindingSubscription, ITypedBindingSubscription<TTarget>
    where TSource : class
{
    private readonly BindingEngine<TTarget, TSource> _engine;

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyBinding{TTarget, TSource}"/> class and synchronizes the target
    /// with <paramref name="source"/>.
    /// </summary>
    /// <param name="target">The target <see cref="BindableObject"/> on which the property resides.</param>
    /// <param name="property">The target <see cref="BindableProperty{TTarget}"/> to bind.</param>
    /// <param name="source">The source object instance providing data.</param>
    /// <param name="getter">The getter delegate to extract values from the source object.</param>
    /// <param name="setter">An optional setter delegate to write values back to the source object.</param>
    /// <param name="updateSourceTrigger">Specifies when target changes are pushed back to the source.</param>
    /// <param name="sourcePropertyName">
    /// The source property the getter reads, if known. When set, source notifications for other properties are ignored,
    /// and validation errors (<see cref="INotifyDataErrorInfo"/>) of that property are reported through <see cref="Validation"/>.
    /// </param>
    /// <param name="options">Optional mode, fallback and null-value settings.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="updateSourceTrigger"/> is <see cref="UpdateSourceTrigger.LostFocus"/> but <paramref name="target"/> is not a
    /// <see cref="Tree.UIElement"/>, or the mode requires a setter and none was given.
    /// </exception>
    public PropertyBinding(
        BindableObject target,
        BindableProperty<TTarget> property,
        TSource source,
        Func<TSource, TTarget> getter,
        Action<TSource, TTarget>? setter,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        string? sourcePropertyName = null,
        BindingOptions<TTarget>? options = null)
    {
        _engine = new BindingEngine<TTarget, TSource>(target, property, getter, setter, updateSourceTrigger, options, sourcePropertyName);
        _engine.SetSource(source);
    }

    /// <inheritdoc/>
    public void UpdateTarget() => _engine.UpdateTarget();

    /// <inheritdoc/>
    public void UpdateSource() => _engine.UpdateSource();

    /// <inheritdoc/>
    public void OnTargetPropertyChanged(object? newValue) => _engine.OnTargetPropertyChanged(newValue);

    void ITypedBindingSubscription<TTarget>.OnTargetPropertyChanged(TTarget newValue) =>
        ((ITypedBindingSubscription<TTarget>)_engine).OnTargetPropertyChanged(newValue);

    /// <summary>
    /// Unsubscribes from the source and target and clears validation errors reported by this binding.
    /// </summary>
    public void Dispose() => _engine.Dispose();
}

/// <summary>
/// Represents a strongly-typed data binding subscription connecting a target <see cref="BindableProperty{TTarget}"/>
/// on a <see cref="BindableObject"/> to its current <see cref="BindableObject.DataContext"/> of type <typeparamref name="TDataContext"/>.
/// </summary>
/// <remarks>
/// The binding follows the target's <see cref="BindableObject.DataContext"/>: when it changes, the binding switches to the new
/// data context. When the data context is <c>null</c> or not a <typeparamref name="TDataContext"/>, the target shows the
/// <see cref="BindingOptions{T}.FallbackValue"/> if one was given, otherwise the value previously written by this binding is cleared.
/// </remarks>
/// <typeparam name="TTarget">The data type of the target property.</typeparam>
/// <typeparam name="TDataContext">The expected type of the data context. Must be a reference type.</typeparam>
public sealed class DataContextBinding<TTarget, TDataContext> : IBindingSubscription, ITypedBindingSubscription<TTarget>
    where TDataContext : class
{
    private readonly BindingEngine<TTarget, TDataContext> _engine;
    private readonly IDisposable _dataContextSubscription;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataContextBinding{TTarget, TDataContext}"/> class and synchronizes the
    /// target with the current data context.
    /// </summary>
    /// <param name="target">The target <see cref="BindableObject"/> on which the property resides.</param>
    /// <param name="property">The target <see cref="BindableProperty{TTarget}"/> to bind.</param>
    /// <param name="getter">The getter delegate to extract values from the data context.</param>
    /// <param name="setter">An optional setter delegate to write values back to the data context.</param>
    /// <param name="updateSourceTrigger">Specifies when target changes are pushed back to the data context.</param>
    /// <param name="sourcePropertyName">
    /// The data-context property the getter reads, if known. When set, notifications for other properties are ignored, and
    /// validation errors (<see cref="INotifyDataErrorInfo"/>) of that property are reported through <see cref="Validation"/>.
    /// </param>
    /// <param name="options">Optional mode, fallback and null-value settings.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="updateSourceTrigger"/> is <see cref="UpdateSourceTrigger.LostFocus"/> but <paramref name="target"/> is not a
    /// <see cref="Tree.UIElement"/>, or the mode requires a setter and none was given.
    /// </exception>
    public DataContextBinding(
        BindableObject target,
        BindableProperty<TTarget> property,
        Func<TDataContext, TTarget> getter,
        Action<TDataContext, TTarget>? setter,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        string? sourcePropertyName = null,
        BindingOptions<TTarget>? options = null)
    {
        _engine = new BindingEngine<TTarget, TDataContext>(target, property, getter, setter, updateSourceTrigger, options, sourcePropertyName);
        _dataContextSubscription = target.Subscribe(BindableObject.DataContextProperty, OnTargetDataContextChanged);
        _engine.SetSource(target.DataContext as TDataContext);
    }

    private void OnTargetDataContextChanged(BindableObject sender, object? oldValue, object? newValue)
    {
        _engine.SetSource(newValue as TDataContext);
    }

    /// <inheritdoc/>
    public void UpdateTarget() => _engine.UpdateTarget();

    /// <inheritdoc/>
    public void UpdateSource() => _engine.UpdateSource();

    /// <inheritdoc/>
    public void OnTargetPropertyChanged(object? newValue) => _engine.OnTargetPropertyChanged(newValue);

    void ITypedBindingSubscription<TTarget>.OnTargetPropertyChanged(TTarget newValue) =>
        ((ITypedBindingSubscription<TTarget>)_engine).OnTargetPropertyChanged(newValue);

    /// <summary>
    /// Unsubscribes from the data context and target and clears validation errors reported by this binding.
    /// </summary>
    public void Dispose()
    {
        _dataContextSubscription.Dispose();
        _engine.Dispose();
    }
}

/// <summary>
/// A one-way binding whose value is computed from several sources, created with
/// <see cref="BindableObject.SetMultiBinding{TTarget}(BindableProperty{TTarget}, Func{TTarget}, INotifyPropertyChanged[])"/>.
/// </summary>
/// <remarks>
/// The target is re-evaluated whenever any source raises <see cref="INotifyPropertyChanged.PropertyChanged"/>. The target is
/// held weakly; the sources are held strongly until the binding is disposed.
/// </remarks>
/// <typeparam name="TTarget">The data type of the target property.</typeparam>
public sealed class MultiSourceBinding<TTarget> : IBindingSubscription
{
    private readonly WeakReference<BindableObject> _targetRef;
    private readonly BindableProperty<TTarget> _property;
    private readonly Func<TTarget> _getter;
    private readonly INotifyPropertyChanged[] _sources;
    private bool _isUpdating;
    private bool _isDisposed;
    private QueuedTargetUpdate? _queuedUpdate;

    /// <summary>
    /// Initializes a new multi-source binding and sets the target from <paramref name="getter"/>.
    /// </summary>
    /// <param name="target">The target object.</param>
    /// <param name="property">The target property.</param>
    /// <param name="getter">Computes the value from the sources (typically a lambda capturing them).</param>
    /// <param name="sources">The objects whose changes trigger re-evaluation.</param>
    public MultiSourceBinding(BindableObject target, BindableProperty<TTarget> property, Func<TTarget> getter, params INotifyPropertyChanged[] sources)
    {
        ArgumentNullException.ThrowIfNull(getter);
        ArgumentNullException.ThrowIfNull(sources);

        _targetRef = new WeakReference<BindableObject>(target);
        _property = property;
        _getter = getter;
        _sources = (INotifyPropertyChanged[])sources.Clone();

        foreach (var source in _sources)
        {
            source.PropertyChanged += OnSourcePropertyChanged;
        }

        UpdateTarget();
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_targetRef.TryGetTarget(out _))
        {
            Dispose();
            return;
        }

        // Sources may change from background work; the target may only be updated on the UI thread.
        if (Threading.Dispatcher.CheckAccess())
        {
            UpdateTarget();
        }
        else
        {
            (_queuedUpdate ??= new QueuedTargetUpdate(UpdateTarget)).Post();
        }
    }

    /// <inheritdoc/>
    public void UpdateTarget()
    {
        if (_isUpdating || _isDisposed || !_targetRef.TryGetTarget(out var target)) return;
        _isUpdating = true;
        try
        {
            target.SetValue(_property, _getter());
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>Does nothing: a multi-source binding is one-way.</summary>
    public void UpdateSource()
    {
    }

    /// <summary>Does nothing: a multi-source binding is one-way.</summary>
    public void OnTargetPropertyChanged(object? newValue)
    {
    }

    /// <summary>Unsubscribes from all sources.</summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        foreach (var source in _sources)
        {
            source.PropertyChanged -= OnSourcePropertyChanged;
        }
    }
}

/// <summary>
/// Runs a binding's target update on the UI thread for source changes raised on other threads. Changes arriving while
/// an update is already queued are coalesced into it, since the update reads the source's latest value anyway.
/// </summary>
internal sealed class QueuedTargetUpdate
{
    private readonly Action _run;
    private int _isQueued;

    public QueuedTargetUpdate(Action updateTarget)
    {
        _run = () =>
        {
            // Reset before reading the source, so a change made during the update queues another one.
            Volatile.Write(ref _isQueued, 0);
            updateTarget();
        };
    }

    public void Post()
    {
        if (Interlocked.Exchange(ref _isQueued, 1) == 0)
        {
            Threading.Dispatcher.Post(_run);
        }
    }
}

internal static class BindingHelpers
{
    /// <summary>
    /// Determines whether a source <see cref="INotifyPropertyChanged.PropertyChanged"/> notification can affect a binding
    /// that reads <paramref name="sourcePropertyName"/>. A <c>null</c> or empty property name in the notification means
    /// "everything changed"; a binding without a known source property is always affected.
    /// </summary>
    public static bool Affects(PropertyChangedEventArgs e, string? sourcePropertyName) =>
        sourcePropertyName == null
        || string.IsNullOrEmpty(e.PropertyName)
        || string.Equals(e.PropertyName, sourcePropertyName, StringComparison.Ordinal);

    /// <summary>
    /// Extracts the source property a getter reads from its source text (supplied through
    /// <see cref="CallerArgumentExpressionAttribute"/>), for the simple forms <c>x =&gt; x.Name</c> and
    /// <c>x =&gt; x.Name.Inner</c>, optionally with null-forgiving <c>!</c> or null-conditional <c>?.</c> (the first member is returned). Anything else (method calls, operators, several
    /// members, a delegate variable) returns <c>null</c>, meaning the binding updates on every source change.
    /// </summary>
    public static string? GetSourcePropertyName(string? getterExpression)
    {
        if (string.IsNullOrWhiteSpace(getterExpression))
        {
            return null;
        }

        ReadOnlySpan<char> text = getterExpression.AsSpan().Trim();
        if (text.StartsWith("static ", StringComparison.Ordinal))
        {
            text = text[7..].TrimStart();
        }

        int arrow = text.IndexOf("=>", StringComparison.Ordinal);
        if (arrow <= 0)
        {
            return null;
        }

        // Parameter: "x", "(x)" or "(SomeType x)".
        ReadOnlySpan<char> parameter = text[..arrow].Trim();
        if (parameter.Length >= 2 && parameter[0] == '(' && parameter[^1] == ')')
        {
            parameter = parameter[1..^1].Trim();
            int space = parameter.LastIndexOf(' ');
            if (space >= 0)
            {
                parameter = parameter[(space + 1)..];
            }
        }

        if (ReadIdentifier(parameter) != parameter.Length)
        {
            return null;
        }

        // Body: the parameter, then one or more ".Member" segments and nothing else.
        ReadOnlySpan<char> body = text[(arrow + 2)..].Trim();
        if (!body.StartsWith(parameter, StringComparison.Ordinal) || body.Length <= parameter.Length || body[parameter.Length] != '.')
        {
            return null;
        }

        ReadOnlySpan<char> rest = body[(parameter.Length + 1)..];
        int nameLength = ReadIdentifier(rest);
        if (nameLength == 0)
        {
            return null;
        }

        ReadOnlySpan<char> name = rest[..nameLength];
        ReadOnlySpan<char> tail = SkipNullOperators(rest[nameLength..]);
        while (tail.Length > 0)
        {
            if (tail[0] != '.')
            {
                return null;
            }

            tail = tail[1..];
            int segment = ReadIdentifier(tail);
            if (segment == 0)
            {
                return null;
            }
            tail = SkipNullOperators(tail[segment..]);
        }

        return name.ToString();
    }

    // Skips a null-forgiving '!' and the '?' of a null-conditional "?." after a member: x.Email!, x.Customer?.Name.
    private static ReadOnlySpan<char> SkipNullOperators(ReadOnlySpan<char> text)
    {
        if (text.Length > 0 && text[0] == '!')
        {
            text = text[1..];
        }

        if (text.Length > 1 && text[0] == '?' && text[1] == '.')
        {
            text = text[1..];
        }

        return text;
    }

    // Length of the C# identifier at the start of text (0 if there is none).
    private static int ReadIdentifier(ReadOnlySpan<char> text)
    {
        if (text.Length == 0 || !(char.IsLetter(text[0]) || text[0] == '_'))
        {
            return 0;
        }

        int length = 1;
        while (length < text.Length && (char.IsLetterOrDigit(text[length]) || text[length] == '_'))
        {
            length++;
        }
        return length;
    }

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
