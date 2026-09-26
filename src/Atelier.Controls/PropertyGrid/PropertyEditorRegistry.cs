using System;
using System.Collections.Generic;
using System.Globalization;
using Atelier.Core.Inspection;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// Registry of property editor factories mapping types or predicates to UIElement editor creators.
/// 100% Native AOT compatible with zero reflection.
/// </summary>
public class PropertyEditorRegistry
{
    private readonly Dictionary<Type, Func<PropertyEditorContext, UIElement>> _typeFactories = new();
    private readonly List<(Func<PropertyEditorContext, bool> Predicate, Func<PropertyEditorContext, UIElement> Factory)> _predicateFactories = [];

    /// <summary>
    /// Gets the global default editor registry pre-configured with built-in editors.
    /// </summary>
    public static PropertyEditorRegistry Default { get; } = CreateDefaultRegistry();

    /// <summary>
    /// Registers an editor factory for the specified property type <typeparamref name="T"/>.
    /// </summary>
    public void Register<T>(Func<PropertyEditorContext, UIElement> factory) =>
        Register(typeof(T), factory);

    /// <summary>
    /// Registers an editor factory for the specified property <paramref name="propertyType"/>.
    /// </summary>
    public void Register(Type propertyType, Func<PropertyEditorContext, UIElement> factory)
    {
        if (propertyType == null) throw new ArgumentNullException(nameof(propertyType));
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        _typeFactories[propertyType] = factory;
    }

    /// <summary>
    /// Registers an editor factory matched by a custom predicate.
    /// </summary>
    public void Register(Func<PropertyEditorContext, bool> predicate, Func<PropertyEditorContext, UIElement> factory)
    {
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        _predicateFactories.Add((predicate, factory));
    }

    /// <summary>
    /// Creates an editor <see cref="UIElement"/> for the given context.
    /// </summary>
    public UIElement CreateEditor(PropertyEditorContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        Type propType = context.Descriptor.PropertyType;

        // 1. Exact type match in this registry
        if (_typeFactories.TryGetValue(propType, out var factory))
        {
            return factory(context);
        }

        // 2. Underlying nullable type match
        Type? underlying = Nullable.GetUnderlyingType(propType);
        if (underlying != null && _typeFactories.TryGetValue(underlying, out factory))
        {
            return factory(context);
        }

        // 3. Custom predicates
        for (int i = _predicateFactories.Count - 1; i >= 0; i--)
        {
            if (_predicateFactories[i].Predicate(context))
            {
                return _predicateFactories[i].Factory(context);
            }
        }

        // 4. Fallback to Default if this is a custom instance registry
        if (!ReferenceEquals(this, Default))
        {
            return Default.CreateEditor(context);
        }

        // 5. Fallback read-only text box
        return CreateFallbackEditor(context);
    }

    private static PropertyEditorRegistry CreateDefaultRegistry()
    {
        var registry = new PropertyEditorRegistry();

        // String editor
        registry.Register<string>(CreateStringEditor);

        // Boolean editor
        registry.Register<bool>(CreateBoolEditor);

        // Integer types
        registry.Register<int>(CreateIntEditor);
        registry.Register<long>(CreateLongEditor);
        registry.Register<short>(CreateShortEditor);
        registry.Register<byte>(CreateByteEditor);
        registry.Register<uint>(CreateUIntEditor);
        registry.Register<ulong>(CreateULongEditor);
        registry.Register<ushort>(CreateUShortEditor);
        registry.Register<sbyte>(CreateSByteEditor);

        // Floating-point types
        registry.Register<float>(CreateFloatEditor);
        registry.Register<double>(CreateDoubleEditor);
        registry.Register<decimal>(CreateDecimalEditor);

        // Color editor
        registry.Register<Color>(CreateColorEditor);

        // Enum predicate
        registry.Register(
            ctx => (Nullable.GetUnderlyingType(ctx.Descriptor.PropertyType) ?? ctx.Descriptor.PropertyType).IsEnum,
            CreateEnumEditor
        );

        return registry;
    }

    #region Built-In Editor Implementations

    public static UIElement CreateStringEditor(PropertyEditorContext context)
    {
        var textBox = new TextBox(context.Value as string ?? string.Empty)
        {
            IsReadOnly = context.IsReadOnly,
            Height = 32,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4),
            VerticalAlignment = VerticalAlignment.Center
        };

        if (context.IsReadOnly)
        {
            textBox.Opacity = 0.7f;
        }
        else
        {
            bool isSelfUpdating = false;
            textBox.TextChanged += (s, text) =>
            {
                if (isSelfUpdating) return;
                isSelfUpdating = true;
                try
                {
                    context.UpdateValue(text);
                }
                finally
                {
                    isSelfUpdating = false;
                }
            };

            context.ValueChanged += val =>
            {
                if (isSelfUpdating) return;
                string newStr = val as string ?? string.Empty;
                if (textBox.Text != newStr)
                {
                    textBox.Text = newStr;
                }
            };
        }

        return textBox;
    }

    public static UIElement CreateBoolEditor(PropertyEditorContext context)
    {
        var checkBox = new CheckBox
        {
            IsChecked = context.Value is bool b && b,
            IsEnabled = !context.IsReadOnly,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (context.IsReadOnly)
        {
            checkBox.Opacity = 0.7f;
        }
        else
        {
            bool isSelfUpdating = false;
            checkBox.CheckedChanged += (s, isChecked) =>
            {
                if (isSelfUpdating) return;
                isSelfUpdating = true;
                try
                {
                    context.UpdateValue(isChecked == true);
                }
                finally
                {
                    isSelfUpdating = false;
                }
            };

            context.ValueChanged += val =>
            {
                if (isSelfUpdating) return;
                bool newBool = val is bool bVal && bVal;
                if (checkBox.IsChecked != newBool)
                {
                    checkBox.IsChecked = newBool;
                }
            };
        }

        return checkBox;
    }

    public static UIElement CreateIntEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, s => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : null);

    public static UIElement CreateLongEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, s => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : null);

    public static UIElement CreateShortEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, s => short.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out short v) ? v : null);

    public static UIElement CreateByteEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, s => byte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte v) ? v : null);

    public static UIElement CreateUIntEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, s => uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint v) ? v : null);

    public static UIElement CreateULongEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, s => ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong v) ? v : null);

    public static UIElement CreateUShortEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, s => ushort.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort v) ? v : null);

    public static UIElement CreateSByteEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, s => sbyte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte v) ? v : null);

    public static UIElement CreateFloatEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, s => float.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float v) ? v : null);

    public static UIElement CreateDoubleEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, s => double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double v) ? v : null);

    public static UIElement CreateDecimalEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, s => decimal.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out decimal v) ? v : null);

    private static UIElement CreateNumericEditor(PropertyEditorContext context, Func<string, object?> parser)
    {
        string initialText = FormatNumericValue(context.Value);
        var textBox = new TextBox(initialText)
        {
            IsReadOnly = context.IsReadOnly,
            Height = 32,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4),
            VerticalAlignment = VerticalAlignment.Center
        };

        if (context.IsReadOnly)
        {
            textBox.Opacity = 0.7f;
        }
        else
        {
            bool isSelfUpdating = false;

            void Commit()
            {
                string text = textBox.Text.Trim();
                object? parsed = parser(text);
                if (parsed != null)
                {
                    isSelfUpdating = true;
                    try
                    {
                        context.UpdateValue(parsed);
                    }
                    finally
                    {
                        isSelfUpdating = false;
                    }
                }
                else
                {
                    // Revert to current valid value
                    textBox.Text = FormatNumericValue(context.Value);
                }
            }

            textBox.LostFocus += (s, e) => Commit();

            textBox.TextChanged += (s, text) =>
            {
                if (isSelfUpdating) return;
                object? parsed = parser(text.Trim());
                if (parsed != null)
                {
                    isSelfUpdating = true;
                    try
                    {
                        context.UpdateValue(parsed);
                    }
                    finally
                    {
                        isSelfUpdating = false;
                    }
                }
            };

            context.ValueChanged += val =>
            {
                if (isSelfUpdating) return;
                string newFormatted = FormatNumericValue(val);
                if (textBox.Text != newFormatted)
                {
                    textBox.Text = newFormatted;
                }
            };
        }

        return textBox;
    }

    private static string FormatNumericValue(object? val)
    {
        if (val == null) return "0";
        if (val is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);
        return val.ToString() ?? "0";
    }

    public static UIElement CreateEnumEditor(PropertyEditorContext context)
    {
        Type propType = context.Descriptor.PropertyType;
        Type enumType = Nullable.GetUnderlyingType(propType) ?? propType;

        var names = Enum.GetNames(enumType);
        var comboBox = new ComboBox
        {
            ItemsSource = names,
            SelectedItem = context.Value?.ToString(),
            IsEnabled = !context.IsReadOnly,
            Height = 32,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4),
            VerticalAlignment = VerticalAlignment.Center
        };

        if (context.IsReadOnly)
        {
            comboBox.Opacity = 0.7f;
        }
        else
        {
            bool isSelfUpdating = false;
            comboBox.SelectionChanged += (s, selected) =>
            {
                if (isSelfUpdating) return;
                if (selected is string strVal && Enum.TryParse(enumType, strVal, out var enumVal) && enumVal != null)
                {
                    isSelfUpdating = true;
                    try
                    {
                        context.UpdateValue(enumVal);
                    }
                    finally
                    {
                        isSelfUpdating = false;
                    }
                }
            };

            context.ValueChanged += val =>
            {
                if (isSelfUpdating) return;
                string? strVal = val?.ToString();
                if (!Equals(comboBox.SelectedItem, strVal))
                {
                    comboBox.SelectedItem = strVal;
                }
            };
        }

        return comboBox;
    }

    public static UIElement CreateColorEditor(PropertyEditorContext context)
    {
        Color initialColor = context.Value is Color c ? c : Color.Black;

        var swatch = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(4),
            Background = initialColor,
            BorderThickness = new Thickness(1),
            BorderBrush = Color.FromHex("#40000000"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var hexBox = new TextBox(initialColor.ToString())
        {
            IsReadOnly = context.IsReadOnly,
            Height = 32,
            Width = 105,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4),
            VerticalAlignment = VerticalAlignment.Center
        };

        if (context.IsReadOnly)
        {
            hexBox.Opacity = 0.7f;
            swatch.Opacity = 0.7f;
        }
        else
        {
            bool isSelfUpdating = false;

            void CommitHex()
            {
                string text = hexBox.Text.Trim();
                try
                {
                    var parsed = Color.FromHex(text);
                    swatch.Background = parsed;
                    isSelfUpdating = true;
                    try
                    {
                        context.UpdateValue(parsed);
                    }
                    finally
                    {
                        isSelfUpdating = false;
                    }
                }
                catch
                {
                    // Revert invalid hex
                    Color cur = context.Value is Color cv ? cv : Color.Black;
                    hexBox.Text = cur.ToString();
                    swatch.Background = cur;
                }
            }

            hexBox.LostFocus += (s, e) => CommitHex();

            hexBox.TextChanged += (s, text) =>
            {
                if (isSelfUpdating) return;
                try
                {
                    var parsed = Color.FromHex(text.Trim());
                    swatch.Background = parsed;
                    isSelfUpdating = true;
                    try
                    {
                        context.UpdateValue(parsed);
                    }
                    finally
                    {
                        isSelfUpdating = false;
                    }
                }
                catch
                {
                    // In-progress input typing, don't update until valid or lost focus
                }
            };

            context.ValueChanged += val =>
            {
                if (isSelfUpdating) return;
                if (val is Color newColor)
                {
                    swatch.Background = newColor;
                    string hex = newColor.ToString();
                    if (hexBox.Text != hex)
                    {
                        hexBox.Text = hex;
                    }
                }
            };
        }

        var container = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        container.Add(swatch);
        container.Add(hexBox);

        return container;
    }

    public static UIElement CreateFallbackEditor(PropertyEditorContext context)
    {
        var textBox = new TextBox(context.Value?.ToString() ?? string.Empty)
        {
            IsReadOnly = true,
            Opacity = 0.7f,
            Height = 32,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4),
            VerticalAlignment = VerticalAlignment.Center
        };

        context.ValueChanged += val =>
        {
            textBox.Text = val?.ToString() ?? string.Empty;
        };

        return textBox;
    }

    #endregion
}
