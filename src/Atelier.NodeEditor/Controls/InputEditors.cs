using System.ComponentModel;
using System.Globalization;
using Atelier.Controls;
using Atelier.Core.Tree;

namespace Atelier.Nodes;

/// <summary>
/// Creates the controls that edit unconnected inputs' values next to their sockets (the default
/// <see cref="NodeEditor.InputEditorFactory"/>), kept in sync with <see cref="InputSocketViewModel.Value"/> both ways.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="InputEditor.Auto"/> picks a check box for <see cref="bool"/> values, a slider for numbers with a
/// <see cref="InputSocketViewModel.Minimum"/> and <see cref="InputSocketViewModel.Maximum"/>, and a text box for other
/// numbers and strings; other types get no control. Values keep the socket type's <see cref="SocketType.ValueType"/>.
/// </para>
/// <para>
/// The node editor has no knob of its own: <see cref="InputEditor.Knob"/> gets a slider until an app registers a knob
/// factory with <see cref="Register"/>, for example one making an <c>Atelier.Audio.Knob</c> set up with
/// <see cref="GetNumberRange"/> and <see cref="BindNumber"/>. Registering replaces the built-in control of any editor.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// InputEditors.Register(InputEditor.Knob, input =>
/// {
///     var range = InputEditors.GetNumberRange(input);
///     var knob = new Knob { Minimum = range.Minimum, Maximum = range.Maximum, SmallChange = range.SmallChange,
///         LargeChange = range.LargeChange, ValueFormat = range.ValueFormat, Width = 28, Height = 28 };
///     InputEditors.BindNumber(knob, input, v => knob.Value = v, h => knob.ValueChanged += h);
///     return knob;
/// });
/// </code>
/// </example>
public static class InputEditors
{
    private static readonly Dictionary<InputEditor, Func<InputSocketViewModel, UIElement?>> s_factories = [];

    /// <summary>Creates the control for <paramref name="input"/> as its <see cref="InputSocketViewModel.Editor"/> says, or <c>null</c> for none.</summary>
    public static UIElement? Create(InputSocketViewModel input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var editor = Resolve(input);
        Func<InputSocketViewModel, UIElement?>? factory;
        lock (s_factories)
        {
            s_factories.TryGetValue(editor, out factory);
        }
        if (factory != null) return factory(input);
        return editor switch
        {
            InputEditor.TextBox => CreateTextBox(input),
            InputEditor.Slider or InputEditor.Knob => CreateSlider(input),
            InputEditor.CheckBox => CreateCheckBox(input),
            _ => null,
        };
    }

    /// <summary>
    /// Makes <see cref="Create"/> use <paramref name="factory"/> for inputs whose editor is <paramref name="editor"/>,
    /// replacing the built-in control; <c>null</c> restores the built-in one.
    /// </summary>
    /// <param name="editor">The editor kind; not <see cref="InputEditor.Auto"/>, which resolves to another kind.</param>
    /// <param name="factory">Creates the control for an input, or returns <c>null</c> for none.</param>
    public static void Register(InputEditor editor, Func<InputSocketViewModel, UIElement?>? factory)
    {
        if (editor == InputEditor.Auto) throw new ArgumentException("Auto resolves to another editor and can't be registered.", nameof(editor));
        lock (s_factories)
        {
            if (factory == null) s_factories.Remove(editor);
            else s_factories[editor] = factory;
        }
    }

    /// <summary>Gets the editor <paramref name="input"/> gets: its <see cref="InputSocketViewModel.Editor"/>, with <see cref="InputEditor.Auto"/> resolved.</summary>
    public static InputEditor Resolve(InputSocketViewModel input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Editor != InputEditor.Auto) return input.Editor;
        var type = input.Type.ValueType;
        if (type == typeof(bool)) return InputEditor.CheckBox;
        if (IsNumeric(type)) return input.Minimum.HasValue && input.Maximum.HasValue ? InputEditor.Slider : InputEditor.TextBox;
        return type == typeof(string) ? InputEditor.TextBox : InputEditor.None;
    }

    /// <summary>Gets whether values of <paramref name="type"/> are numbers.</summary>
    public static bool IsNumeric(Type? type) =>
        type == typeof(double) || type == typeof(float) || type == typeof(int) || type == typeof(long)
        || type == typeof(short) || type == typeof(byte) || type == typeof(decimal);

    /// <summary>Creates a compact text box labeled with the input's name, for numbers (parsed as they're typed) or text.</summary>
    public static TextBox CreateTextBox(InputSocketViewModel input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var type = input.Type.ValueType;
        bool numeric = IsNumeric(type);
        var textBox = new TextBox { Label = input.Name, FieldHeight = 36, FontSize = 13, MinWidth = 0 };
        bool updating = false;

        void Show()
        {
            if (updating) return;
            // While typing, keep "1." or "-" as they are as long as they mean the current value.
            if (textBox.IsFocused && numeric && TryParse(textBox.Text, type!, out var typed) && Equals(typed, input.Value)) return;
            updating = true;
            textBox.Text = Format(input.Value);
            updating = false;
        }

        textBox.TextChanged += (_, text) =>
        {
            if (updating) return;
            updating = true;
            if (!numeric) input.Value = text;
            else if (TryParse(text, type!, out var value)) input.Value = value;
            updating = false;
        };
        textBox.LostFocus += (_, _) => Show(); // drop what doesn't parse
        Show();
        Sync(textBox, input, Show);
        return textBox;
    }

    /// <summary>Creates a slider over the input's range (see <see cref="GetNumberRange"/>); whole-number types snap to steps of 1.</summary>
    public static Slider CreateSlider(InputSocketViewModel input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var range = GetNumberRange(input);
        var slider = new Slider
        {
            Minimum = range.Minimum,
            Maximum = range.Maximum,
            IsSnapToTickEnabled = range.IsWholeNumber,
            TickFrequency = range.IsWholeNumber ? 1 : 0,
            ValueFormat = range.ValueFormat,
            MinWidth = 0,
        };
        slider.SmallChange = range.SmallChange;
        slider.LargeChange = range.LargeChange;
        BindNumber(slider, input, v => slider.Value = v, h => slider.ValueChanged += h);
        return slider;
    }

    /// <summary>
    /// Gets the range a number control for <paramref name="input"/> covers: its <see cref="InputSocketViewModel.Minimum"/>
    /// and <see cref="InputSocketViewModel.Maximum"/> (0..1 without them); steps of 1 and a tenth of the range for
    /// whole-number types, a hundredth and a tenth of the range otherwise; and a value format with two decimals for
    /// fractional types.
    /// </summary>
    public static NumberRange GetNumberRange(InputSocketViewModel input)
    {
        ArgumentNullException.ThrowIfNull(input);
        bool whole = IsWholeNumber(input.Type.ValueType ?? typeof(double));
        float minimum = (float)(input.Minimum ?? 0);
        float maximum = (float)(input.Maximum ?? 1);
        float span = maximum - minimum;
        return whole
            ? new NumberRange(minimum, maximum, 1, Math.Max(1, MathF.Round(span / 10)), true, "{0:0}")
            : new NumberRange(minimum, maximum, span / 100, span / 10, false, "{0:0.00}");
    }

    /// <summary>
    /// Keeps a number control and <paramref name="input"/> in sync both ways: the input keeps its type (rounded for
    /// whole-number types), and while the control is in a window it shows the input's value as that changes.
    /// </summary>
    /// <param name="control">The control.</param>
    /// <param name="input">The input it edits.</param>
    /// <param name="set">Shows a value in the control.</param>
    /// <param name="subscribe">Subscribes a handler to the control's value-changed event.</param>
    public static void BindNumber(UIElement control, InputSocketViewModel input, Action<float> set, Action<EventHandler<float>> subscribe)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(subscribe);
        var type = input.Type.ValueType ?? typeof(double);
        bool updating = false;
        void Show()
        {
            updating = true;
            set((float)ToDouble(input.Value));
            updating = false;
        }
        subscribe((_, value) =>
        {
            if (updating) return;
            var converted = FromDouble(value, type);
            if (!Equals(converted, input.Value)) input.Value = converted;
        });
        Show();
        Sync(control, input, Show);
    }

    /// <summary>Creates a check box labeled with the input's name.</summary>
    public static CheckBox CreateCheckBox(InputSocketViewModel input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var checkBox = new CheckBox(input.Name);
        bool updating = false;
        void Show()
        {
            updating = true;
            checkBox.IsChecked = input.Value is true;
            updating = false;
        }
        checkBox.CheckedChanged += (_, value) =>
        {
            if (!updating) input.Value = value == true;
        };
        Show();
        Sync(checkBox, input, Show);
        return checkBox;
    }

    /// <summary>Converts a number of any numeric type (or a numeric string) to a <see cref="double"/>; 0 otherwise.</summary>
    public static double ToDouble(object? value) => value switch
    {
        double d => d,
        IConvertible c => TryConvert(c),
        _ => 0,
    };

    /// <summary>Converts <paramref name="value"/> to <paramref name="type"/>, rounding for whole-number types.</summary>
    public static object FromDouble(double value, Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (IsWholeNumber(type)) value = Math.Round(value);
        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
    }

    private static double TryConvert(IConvertible value)
    {
        try
        {
            return value.ToDouble(CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return 0;
        }
    }

    private static bool IsWholeNumber(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte);

    private static string Format(object? value) => value switch
    {
        null => "",
        double d => d.ToString("0.###", CultureInfo.InvariantCulture),
        float f => f.ToString("0.###", CultureInfo.InvariantCulture),
        decimal m => m.ToString("0.###", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    private static bool TryParse(string text, Type type, out object? value)
    {
        value = null;
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
            && !double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out number))
        {
            return false;
        }
        try
        {
            value = FromDouble(number, type);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    // Updates the control when the input's value changes, while the control is in a window.
    private static void Sync(UIElement control, InputSocketViewModel input, Action show)
    {
        void OnChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InputSocketViewModel.Value)) show();
        }
        control.AttachedToVisualTree += (_, _) =>
        {
            input.PropertyChanged += OnChanged;
            show();
        };
        control.DetachedFromVisualTree += (_, _) => input.PropertyChanged -= OnChanged;
    }
}

/// <summary>The range and steps of a number control that edits an input (see <see cref="InputEditors.GetNumberRange"/>).</summary>
/// <param name="Minimum">The smallest value.</param>
/// <param name="Maximum">The largest value.</param>
/// <param name="SmallChange">The step of the arrow keys.</param>
/// <param name="LargeChange">The step of Page Up and Page Down.</param>
/// <param name="IsWholeNumber">Whether the input holds whole numbers.</param>
/// <param name="ValueFormat">The composite format of the value text, with the value as argument 0.</param>
public readonly record struct NumberRange(float Minimum, float Maximum, float SmallChange, float LargeChange, bool IsWholeNumber, string ValueFormat);
