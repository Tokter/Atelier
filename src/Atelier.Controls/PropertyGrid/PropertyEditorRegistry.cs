using System;
using System.Collections.Generic;
using System.Globalization;
using Atelier.Core.Events;
using Atelier.Core.Inspection;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// Registry of property editor factories mapping types or predicates to UIElement editor creators.
/// </summary>
/// <remarks>
/// <para>
/// Editors are created from the inspected object's <see cref="IPropertyDescriptor"/>s without reflecting over its members.
/// The enum editors read enum names and values through the <see cref="Enum"/> metadata APIs (cached once per enum type),
/// which are Native AOT compatible.
/// </para>
/// <para>
/// Text-based editors for numbers and colors commit when Enter is pressed or focus leaves the editor; Escape restores the
/// current value. String editors commit on every change. Nullable value types accept an empty text (or the
/// indeterminate check state) as <see langword="null"/>.
/// </para>
/// </remarks>
public class PropertyEditorRegistry
{
    private static readonly object s_true = true;
    private static readonly object s_false = false;
    private static readonly Color s_swatchBorder = Color.FromArgb(0x40, 0, 0, 0);
    private static readonly Dictionary<Type, EnumInfo> s_enumCache = new();

    private readonly Dictionary<Type, Func<PropertyEditorContext, UIElement>> _typeFactories = new();
    private readonly List<(Func<PropertyEditorContext, bool> Predicate, Func<PropertyEditorContext, UIElement> Factory)> _predicateFactories = [];

    /// <summary>
    /// Gets the global default editor registry pre-configured with built-in editors.
    /// </summary>
    /// <remarks>
    /// The registry is shared by every <see cref="PropertyGrid"/> in the process: registrations made here are global and
    /// live for the lifetime of the process (factories and anything they capture are never released). Prefer
    /// <see cref="PropertyGrid.EditorRegistry"/> for per-grid editors.
    /// </remarks>
    public static PropertyEditorRegistry Default { get; } = CreateDefaultRegistry();

    /// <summary>
    /// Raised after a factory is registered, so the owning grid can recreate its editors.
    /// </summary>
    internal event Action? Changed;

    /// <summary>
    /// Registers an editor factory for the specified property type <typeparamref name="T"/>, replacing any factory
    /// registered for the same type.
    /// </summary>
    /// <typeparam name="T">The property type edited by the factory's editors.</typeparam>
    /// <param name="factory">Creates the editor for a property.</param>
    public void Register<T>(Func<PropertyEditorContext, UIElement> factory) =>
        Register(typeof(T), factory);

    /// <summary>
    /// Registers an editor factory for the specified property <paramref name="propertyType"/>, replacing any factory
    /// registered for the same type.
    /// </summary>
    /// <param name="propertyType">The property type edited by the factory's editors.</param>
    /// <param name="factory">Creates the editor for a property.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public void Register(Type propertyType, Func<PropertyEditorContext, UIElement> factory)
    {
        if (propertyType == null) throw new ArgumentNullException(nameof(propertyType));
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        _typeFactories[propertyType] = factory;
        Changed?.Invoke();
    }

    /// <summary>
    /// Registers an editor factory matched by a custom predicate. Predicates registered later take precedence.
    /// </summary>
    /// <param name="predicate">Returns <see langword="true"/> for the properties the factory handles.</param>
    /// <param name="factory">Creates the editor for a property.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public void Register(Func<PropertyEditorContext, bool> predicate, Func<PropertyEditorContext, UIElement> factory)
    {
        if (predicate == null) throw new ArgumentNullException(nameof(predicate));
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        _predicateFactories.Add((predicate, factory));
        Changed?.Invoke();
    }

    /// <summary>
    /// Creates an editor <see cref="UIElement"/> for the given context.
    /// </summary>
    /// <remarks>
    /// Lookup order: a factory registered for the exact property type; then one for the underlying type of a
    /// <see cref="Nullable{T}"/>; then predicates, newest first; then (for registries other than <see cref="Default"/>)
    /// the same lookup in <see cref="Default"/>; finally a read-only text editor (<see cref="CreateFallbackEditor"/>).
    /// </remarks>
    /// <param name="context">The context of the edited property.</param>
    /// <returns>The editor element.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
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

        // 3. Custom predicates, newest first
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

        registry.Register<string>(CreateStringEditor);
        registry.Register<bool>(CreateBoolEditor);

        registry.Register<int>(CreateIntEditor);
        registry.Register<long>(CreateLongEditor);
        registry.Register<short>(CreateShortEditor);
        registry.Register<byte>(CreateByteEditor);
        registry.Register<uint>(CreateUIntEditor);
        registry.Register<ulong>(CreateULongEditor);
        registry.Register<ushort>(CreateUShortEditor);
        registry.Register<sbyte>(CreateSByteEditor);

        registry.Register<float>(CreateFloatEditor);
        registry.Register<double>(CreateDoubleEditor);
        registry.Register<decimal>(CreateDecimalEditor);

        registry.Register<Color>(CreateColorEditor);

        registry.Register(static ctx => ctx.ValueType.IsEnum, CreateEnumEditor);

        return registry;
    }

    #region Built-In Editor Implementations

    /// <summary>
    /// Creates a text editor for a <see cref="string"/> property that commits on every text change. A value rejected
    /// by the setter stays in the editor and is flagged through <see cref="PropertyEditorContext.Error"/>.
    /// </summary>
    /// <param name="context">The context of the edited property.</param>
    /// <returns>A <see cref="TextBox"/>.</returns>
    public static UIElement CreateStringEditor(PropertyEditorContext context)
    {
        var textBox = CreateTextBox(context.Value as string ?? string.Empty, context.IsReadOnly);

        bool isSelfUpdating = false;
        if (!context.IsReadOnly)
        {
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
        }

        context.ValueChanged += val =>
        {
            if (isSelfUpdating) return;
            string newStr = val as string ?? string.Empty;
            if (textBox.Text != newStr)
            {
                isSelfUpdating = true;
                try
                {
                    textBox.Text = newStr;
                }
                finally
                {
                    isSelfUpdating = false;
                }
            }
        };

        return textBox;
    }

    /// <summary>
    /// Creates a check box for a <see cref="bool"/> property; for <c>bool?</c> the check box is three-state and the
    /// indeterminate state means <see langword="null"/>.
    /// </summary>
    /// <param name="context">The context of the edited property.</param>
    /// <returns>A <see cref="CheckBox"/>.</returns>
    public static UIElement CreateBoolEditor(PropertyEditorContext context)
    {
        bool nullable = context.IsNullable;
        var checkBox = new CheckBox
        {
            IsThreeState = nullable,
            IsChecked = ToCheckState(context.Value, nullable),
            IsEnabled = !context.IsReadOnly,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (context.IsReadOnly)
        {
            checkBox.Opacity = 0.7f;
        }

        bool isSelfUpdating = false;
        if (!context.IsReadOnly)
        {
            checkBox.CheckedChanged += (s, isChecked) =>
            {
                if (isSelfUpdating) return;
                object? newValue = isChecked switch
                {
                    true => s_true,
                    false => s_false,
                    null => nullable ? null : s_false
                };

                isSelfUpdating = true;
                try
                {
                    if (!context.UpdateValue(newValue) && !context.HasError)
                    {
                        checkBox.IsChecked = ToCheckState(context.Value, nullable);
                    }
                }
                finally
                {
                    isSelfUpdating = false;
                }
            };
        }

        context.ValueChanged += val =>
        {
            if (isSelfUpdating) return;
            isSelfUpdating = true;
            try
            {
                checkBox.IsChecked = ToCheckState(val, nullable);
            }
            finally
            {
                isSelfUpdating = false;
            }
        };

        return checkBox;
    }

    private static bool? ToCheckState(object? value, bool nullable) =>
        value is bool b ? b : (nullable ? null : false);

    /// <summary>Creates a numeric editor for an <see cref="int"/> or <c>int?</c> property.</summary>
    /// <param name="context">The context of the edited property.</param>
    /// <returns>A <see cref="TextBox"/> that commits on Enter or when it loses focus.</returns>
    public static UIElement CreateIntEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, static s => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : null);

    /// <summary>Creates a numeric editor for a <see cref="long"/> or <c>long?</c> property.</summary>
    /// <param name="context">The context of the edited property.</param>
    /// <returns>A <see cref="TextBox"/> that commits on Enter or when it loses focus.</returns>
    public static UIElement CreateLongEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, static s => long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : null);

    /// <summary>Creates a numeric editor for a <see cref="short"/> or <c>short?</c> property.</summary>
    /// <param name="context">The context of the edited property.</param>
    /// <returns>A <see cref="TextBox"/> that commits on Enter or when it loses focus.</returns>
    public static UIElement CreateShortEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, static s => short.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out short v) ? v : null);

    /// <summary>Creates a numeric editor for a <see cref="byte"/> or <c>byte?</c> property.</summary>
    /// <param name="context">The context of the edited property.</param>
    /// <returns>A <see cref="TextBox"/> that commits on Enter or when it loses focus.</returns>
    public static UIElement CreateByteEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, static s => byte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte v) ? v : null);

    /// <summary>Creates a numeric editor for a <see cref="uint"/> or <c>uint?</c> property.</summary>
    /// <param name="context">The context of the edited property.</param>
    /// <returns>A <see cref="TextBox"/> that commits on Enter or when it loses focus.</returns>
    public static UIElement CreateUIntEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, static s => uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint v) ? v : null);

    /// <summary>Creates a numeric editor for a <see cref="ulong"/> or <c>ulong?</c> property.</summary>
    /// <param name="context">The context of the edited property.</param>
    /// <returns>A <see cref="TextBox"/> that commits on Enter or when it loses focus.</returns>
    public static UIElement CreateULongEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, static s => ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong v) ? v : null);

    /// <summary>Creates a numeric editor for a <see cref="ushort"/> or <c>ushort?</c> property.</summary>
    /// <param name="context">The context of the edited property.</param>
    /// <returns>A <see cref="TextBox"/> that commits on Enter or when it loses focus.</returns>
    public static UIElement CreateUShortEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, static s => ushort.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out ushort v) ? v : null);

    /// <summary>Creates a numeric editor for an <see cref="sbyte"/> or <c>sbyte?</c> property.</summary>
    /// <param name="context">The context of the edited property.</param>
    /// <returns>A <see cref="TextBox"/> that commits on Enter or when it loses focus.</returns>
    public static UIElement CreateSByteEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, static s => sbyte.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out sbyte v) ? v : null);

    /// <summary>Creates a numeric editor for a <see cref="float"/> or <c>float?</c> property (invariant culture, no thousands separators).</summary>
    /// <param name="context">The context of the edited property.</param>
    /// <returns>A <see cref="TextBox"/> that commits on Enter or when it loses focus.</returns>
    public static UIElement CreateFloatEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, static s => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : null);

    /// <summary>Creates a numeric editor for a <see cref="double"/> or <c>double?</c> property (invariant culture, no thousands separators).</summary>
    /// <param name="context">The context of the edited property.</param>
    /// <returns>A <see cref="TextBox"/> that commits on Enter or when it loses focus.</returns>
    public static UIElement CreateDoubleEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, static s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : null);

    /// <summary>Creates a numeric editor for a <see cref="decimal"/> or <c>decimal?</c> property (invariant culture, no thousands separators).</summary>
    /// <param name="context">The context of the edited property.</param>
    /// <returns>A <see cref="TextBox"/> that commits on Enter or when it loses focus.</returns>
    public static UIElement CreateDecimalEditor(PropertyEditorContext context) =>
        CreateNumericEditor(context, static s => decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal v) ? v : null);

    private static UIElement CreateNumericEditor(PropertyEditorContext context, Func<string, object?> parser)
    {
        var textBox = CreateTextBox(FormatNumericValue(context.Value), context.IsReadOnly);
        AttachCommitBehavior(
            context,
            textBox,
            (string text, out object? value) =>
            {
                value = parser(text);
                return value != null;
            },
            (tb, value) => SetTextIfChanged(tb, FormatNumericValue(value)));
        return textBox;
    }

    private delegate bool TextParser(string text, out object? value);

    // Commit-on-Enter/LostFocus behavior shared by the numeric and color editors. Invalid text reverts to the current
    // value; a value rejected by the setter stays in the editor with the context's error shown; Escape reverts.
    private static void AttachCommitBehavior(
        PropertyEditorContext context,
        TextBox textBox,
        TextParser parser,
        Action<TextBox, object?> display)
    {
        bool isSelfUpdating = false;

        void ShowCurrent()
        {
            isSelfUpdating = true;
            try
            {
                display(textBox, context.Value);
            }
            finally
            {
                isSelfUpdating = false;
            }
        }

        if (!context.IsReadOnly)
        {
            void Commit()
            {
                string text = textBox.Text.Trim();
                object? parsed;
                if (text.Length == 0 && context.IsNullable)
                {
                    parsed = null;
                }
                else if (!parser(text, out parsed))
                {
                    ShowCurrent();
                    return;
                }

                bool committed;
                isSelfUpdating = true;
                try
                {
                    committed = context.UpdateValue(parsed);
                }
                finally
                {
                    isSelfUpdating = false;
                }

                // Normalize the text on success ("007" -> "7") and revert when canceled; keep rejected text visible.
                if (committed || !context.HasError)
                {
                    ShowCurrent();
                }
            }

            textBox.LostFocus += (s, e) => Commit();
            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    Commit();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    context.SetError(null);
                    ShowCurrent();
                    e.Handled = true;
                }
            };
        }

        context.ValueChanged += val =>
        {
            if (isSelfUpdating) return;
            isSelfUpdating = true;
            try
            {
                display(textBox, val);
            }
            finally
            {
                isSelfUpdating = false;
            }
        };
    }

    private static string FormatNumericValue(object? val)
    {
        if (val == null) return string.Empty;
        if (val is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);
        return val.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Creates a drop-down editor for an enum property, or a set of check boxes for a <see cref="FlagsAttribute"/> enum.
    /// A nullable enum gets a leading "(none)" entry.
    /// </summary>
    /// <param name="context">The context of the edited property.</param>
    /// <returns>A <see cref="ComboBox"/>, or a <see cref="WrapPanel"/> of <see cref="CheckBox"/>es for flags.</returns>
    public static UIElement CreateEnumEditor(PropertyEditorContext context)
    {
        EnumInfo info = GetEnumInfo(context.ValueType);
        if (info.IsFlags)
        {
            return CreateFlagsEditor(context, info);
        }

        bool nullable = context.IsNullable;
        string[] items = nullable ? info.NamesWithNone : info.Names;
        var comboBox = new ComboBox
        {
            ItemsSource = items,
            SelectedIndex = IndexOfValue(info, context.Value, nullable),
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

        bool isSelfUpdating = false;
        if (!context.IsReadOnly)
        {
            comboBox.SelectionChanged += (s, selected) =>
            {
                if (isSelfUpdating || selected is not string name) return;
                int index = Array.IndexOf(items, name);
                if (index < 0) return;

                object? newValue = nullable ? (index == 0 ? null : info.Values[index - 1]) : info.Values[index];
                isSelfUpdating = true;
                try
                {
                    if (!context.UpdateValue(newValue) && !context.HasError)
                    {
                        comboBox.SelectedIndex = IndexOfValue(info, context.Value, nullable);
                    }
                }
                finally
                {
                    isSelfUpdating = false;
                }
            };
        }

        context.ValueChanged += val =>
        {
            if (isSelfUpdating) return;
            int index = IndexOfValue(info, val, nullable);
            if (comboBox.SelectedIndex != index)
            {
                isSelfUpdating = true;
                try
                {
                    comboBox.SelectedIndex = index;
                }
                finally
                {
                    isSelfUpdating = false;
                }
            }
        };

        return comboBox;
    }

    private static int IndexOfValue(EnumInfo info, object? value, bool nullable)
    {
        if (value == null)
            return nullable ? 0 : -1;

        object[] values = info.Values;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i].Equals(value))
                return nullable ? i + 1 : i;
        }
        return -1;
    }

    private static UIElement CreateFlagsEditor(PropertyEditorContext context, EnumInfo info)
    {
        var panel = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalSpacing = 12,
            VerticalSpacing = 4,
            VerticalAlignment = VerticalAlignment.Center
        };

        var boxes = new List<CheckBox>(info.Names.Length);
        var boxBits = new List<ulong>(info.Names.Length);
        bool isSelfUpdating = false;

        void Sync(object? value)
        {
            ulong bits = ToBits(value, info.TypeCode);
            isSelfUpdating = true;
            try
            {
                for (int i = 0; i < boxes.Count; i++)
                {
                    boxes[i].IsChecked = (bits & boxBits[i]) == boxBits[i];
                }
            }
            finally
            {
                isSelfUpdating = false;
            }
        }

        for (int i = 0; i < info.Names.Length; i++)
        {
            ulong flag = info.Bits[i];
            if (flag == 0) continue; // "None" is the state with every box cleared

            var box = new CheckBox(info.Names[i])
            {
                IsEnabled = !context.IsReadOnly,
                VerticalAlignment = VerticalAlignment.Center
            };
            if (!context.IsReadOnly)
            {
                box.CheckedChanged += (s, isChecked) =>
                {
                    if (isSelfUpdating) return;
                    ulong current = ToBits(context.Value, info.TypeCode);
                    ulong updated = isChecked == true ? current | flag : current & ~flag;
                    context.UpdateValue(Enum.ToObject(info.EnumType, updated));
                    Sync(context.Value); // composite flags (e.g. All = A | B) follow their parts
                };
            }
            boxes.Add(box);
            boxBits.Add(flag);
            panel.Add(box);
        }

        if (context.IsReadOnly)
        {
            panel.Opacity = 0.7f;
        }

        Sync(context.Value);
        context.ValueChanged += val =>
        {
            if (!isSelfUpdating) Sync(val);
        };

        return panel;
    }

    /// <summary>
    /// Creates a hex color editor (<c>#RRGGBB</c> or <c>#AARRGGBB</c>) with a swatch for a <see cref="Color"/> or
    /// <c>Color?</c> property. The swatch previews valid input while typing; the value is committed on Enter or when
    /// the text box loses focus. A nullable color shows an empty text for <see langword="null"/> and accepts clearing.
    /// </summary>
    /// <param name="context">The context of the edited property.</param>
    /// <returns>A horizontal <see cref="StackPanel"/> with the swatch and the text box.</returns>
    public static UIElement CreateColorEditor(PropertyEditorContext context)
    {
        object? initial = context.Value;
        var swatch = new Border
        {
            Width = 24,
            Height = 24,
            CornerRadius = new CornerRadius(4),
            Background = initial is Color c ? c : Color.Transparent,
            BorderThickness = new Thickness(1),
            BorderBrush = s_swatchBorder,
            VerticalAlignment = VerticalAlignment.Center
        };

        var hexBox = CreateTextBox(FormatColor(initial), context.IsReadOnly);
        hexBox.Width = 105;

        if (context.IsReadOnly)
        {
            swatch.Opacity = 0.7f;
        }
        else
        {
            // Live preview only; committing happens on Enter / LostFocus.
            hexBox.TextChanged += (s, text) =>
            {
                if (Color.TryParseHex(text.AsSpan(), out Color preview))
                {
                    swatch.Background = preview;
                }
            };
        }

        AttachCommitBehavior(
            context,
            hexBox,
            static (string text, out object? value) =>
            {
                bool ok = Color.TryParseHex(text.AsSpan(), out Color parsed);
                value = ok ? parsed : null;
                return ok;
            },
            (tb, value) =>
            {
                swatch.Background = value is Color color ? color : Color.Transparent;
                SetTextIfChanged(tb, FormatColor(value));
            });

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

    private static string FormatColor(object? value) => value is Color c ? c.ToString() : string.Empty;

    /// <summary>
    /// Creates a read-only text editor showing the value's <see cref="object.ToString"/>; used for types without an
    /// editor.
    /// </summary>
    /// <param name="context">The context of the displayed property.</param>
    /// <returns>A read-only <see cref="TextBox"/>.</returns>
    public static UIElement CreateFallbackEditor(PropertyEditorContext context)
    {
        var textBox = CreateTextBox(context.Value?.ToString() ?? string.Empty, isReadOnly: true);
        context.ValueChanged += val => SetTextIfChanged(textBox, val?.ToString() ?? string.Empty);
        return textBox;
    }

    private static TextBox CreateTextBox(string text, bool isReadOnly)
    {
        var textBox = new TextBox(text)
        {
            IsReadOnly = isReadOnly,
            Height = 32,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        if (isReadOnly)
        {
            textBox.Opacity = 0.7f;
        }
        return textBox;
    }

    private static void SetTextIfChanged(TextBox textBox, string text)
    {
        if (textBox.Text != text)
        {
            textBox.Text = text;
        }
    }

    #endregion

    #region Enum metadata

    // Enum names, boxed values and raw bits, computed once per enum type.
    private sealed class EnumInfo
    {
        public required Type EnumType { get; init; }
        public required TypeCode TypeCode { get; init; }
        public required string[] Names { get; init; }
        public required string[] NamesWithNone { get; init; }
        public required object[] Values { get; init; }
        public required ulong[] Bits { get; init; }
        public required bool IsFlags { get; init; }
    }

    private static EnumInfo GetEnumInfo(Type enumType)
    {
        lock (s_enumCache)
        {
            if (s_enumCache.TryGetValue(enumType, out EnumInfo? cached))
                return cached;

            string[] names = Enum.GetNames(enumType);
            Array raw = Enum.GetValuesAsUnderlyingType(enumType);
            TypeCode typeCode = Type.GetTypeCode(enumType);
            var values = new object[names.Length];
            var bits = new ulong[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                bits[i] = ToBits(raw.GetValue(i), typeCode);
                values[i] = Enum.ToObject(enumType, bits[i]);
            }

            var namesWithNone = new string[names.Length + 1];
            namesWithNone[0] = "(none)";
            Array.Copy(names, 0, namesWithNone, 1, names.Length);

            var info = new EnumInfo
            {
                EnumType = enumType,
                TypeCode = typeCode,
                Names = names,
                NamesWithNone = namesWithNone,
                Values = values,
                Bits = bits,
                IsFlags = enumType.IsDefined(typeof(FlagsAttribute), inherit: false)
            };
            s_enumCache[enumType] = info;
            return info;
        }
    }

    // Raw bits of a boxed enum or underlying integer (a boxed enum unboxes to its underlying type).
    private static ulong ToBits(object? value, TypeCode typeCode)
    {
        if (value == null) return 0;
        return typeCode switch
        {
            TypeCode.SByte => unchecked((ulong)(sbyte)value),
            TypeCode.Byte => (byte)value,
            TypeCode.Int16 => unchecked((ulong)(short)value),
            TypeCode.UInt16 => (ushort)value,
            TypeCode.Int32 => unchecked((ulong)(int)value),
            TypeCode.UInt32 => (uint)value,
            TypeCode.Int64 => unchecked((ulong)(long)value),
            TypeCode.UInt64 => (ulong)value,
            _ => 0
        };
    }

    #endregion
}
