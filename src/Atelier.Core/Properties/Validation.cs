using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Atelier.Core.Properties;

/// <summary>
/// Exposes validation errors of bound values as read-only attached properties on the binding targets.
/// </summary>
/// <remarks>
/// <para>
/// When a binding's source implements <see cref="INotifyDataErrorInfo"/> and the binding knows which source property it reads
/// (a simple getter such as <c>vm =&gt; vm.Email</c>), the errors the source reports for that property appear in
/// <see cref="ErrorsProperty"/> of the target, and <see cref="HasErrorProperty"/> is <c>true</c> while there are any.
/// Errors from all bindings of an element are combined.
/// </para>
/// <para>Controls use these to show an error state; for example <c>TextBox</c> shows the first error below the field.</para>
/// </remarks>
public sealed class Validation
{
    private static readonly IReadOnlyList<object> s_noErrors = Array.Empty<object>();

    private static readonly BindablePropertyKey<bool> HasErrorPropertyKey =
        BindableProperty.RegisterAttachedReadOnly<Validation, BindableObject, bool>(
            "HasError", false, options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender);

    private static readonly BindablePropertyKey<IReadOnlyList<object>> ErrorsPropertyKey =
        BindableProperty.RegisterAttachedReadOnly<Validation, BindableObject, IReadOnlyList<object>>(
            "Errors", s_noErrors, options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender);

    /// <summary>Identifies the read-only attached <c>HasError</c> property.</summary>
    public static readonly BindableProperty<bool> HasErrorProperty = HasErrorPropertyKey.Property;

    /// <summary>Identifies the read-only attached <c>Errors</c> property.</summary>
    public static readonly BindableProperty<IReadOnlyList<object>> ErrorsProperty = ErrorsPropertyKey.Property;

    // Errors per binding, per target. Weak on the target so validation state never keeps an element alive.
    private static readonly ConditionalWeakTable<BindableObject, Dictionary<object, List<object>>> s_errorsByBinding = new();

    private Validation()
    {
    }

    /// <summary>Gets whether any binding of <paramref name="element"/> currently reports validation errors.</summary>
    public static bool GetHasError(BindableObject element) => element.GetValue(HasErrorProperty);

    /// <summary>Gets the validation errors currently reported by the bindings of <paramref name="element"/>.</summary>
    public static IReadOnlyList<object> GetErrors(BindableObject element) => element.GetValue(ErrorsProperty);

    /// <summary>
    /// Records the errors reported by one binding on <paramref name="target"/> and recomputes the combined errors.
    /// </summary>
    /// <param name="target">The binding target.</param>
    /// <param name="binding">Identifies the reporting binding.</param>
    /// <param name="errors">The binding's current errors; <c>null</c> or empty when it has none.</param>
    internal static void SetBindingErrors(BindableObject target, object binding, List<object>? errors)
    {
        bool hasErrors = errors is { Count: > 0 };
        if (!s_errorsByBinding.TryGetValue(target, out var byBinding))
        {
            if (!hasErrors)
            {
                return;
            }
            byBinding = new Dictionary<object, List<object>>(ReferenceEqualityComparer.Instance);
            s_errorsByBinding.Add(target, byBinding);
        }

        if (hasErrors)
        {
            byBinding[binding] = errors!;
        }
        else if (!byBinding.Remove(binding))
        {
            return;
        }

        if (byBinding.Count == 0)
        {
            s_errorsByBinding.Remove(target);
            target.ClearValue(ErrorsPropertyKey);
            target.ClearValue(HasErrorPropertyKey);
            return;
        }

        var combined = new List<object>();
        foreach (var bindingErrors in byBinding.Values)
        {
            combined.AddRange(bindingErrors);
        }

        target.SetValue(ErrorsPropertyKey, combined.AsReadOnly());
        target.SetValue(HasErrorPropertyKey, true);
    }
}
