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
/// <see cref="InputEditor.Auto"/> picks a check box for <see cref="bool"/> values, a slider for numbers with a
/// <see cref="InputSocketViewModel.Minimum"/> and <see cref="InputSocketViewModel.Maximum"/>, and a text box for other
/// numbers and strings; other types get no control. Values keep the socket type's <see cref="SocketType.ValueType"/>.
/// </remarks>
public static class InputEditors
{
    /// <summary>Creates the control for <paramref name="input"/> as its <see cref="InputSocketViewModel.Editor"/> says, or <c>null</c> for none.</summary>
    public static UIElement? Create(InputSocketViewModel input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return Resolve(input) switch
        {
            InputEditor.TextBox => CreateTextBox(input),
            InputEditor.Slider => CreateSlider(input),
            InputEditor.Knob => CreateKnob(input),
            InputEditor.CheckBox => CreateCheckBox(input),
            _ => null,
        };
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

    /// <summary>Creates a slider over the input's range (0..1 without one); whole-number types move in steps of 1.</summary>
    public static Slider CreateSlider(InputSocketViewModel input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var type = input.Type.ValueType ?? typeof(double);
        bool whole = IsWholeNumber(type);
        var slider = new Slider
        {
            Minimum = (float)(input.Minimum ?? 0),
            Maximum = (float)(input.Maximum ?? 1),
            IsSnapToTickEnabled = whole,
            TickFrequency = whole ? 1 : 0,
            ValueFormat = whole ? "{0:0}" : "{0:0.00}",
            MinWidth = 0,
        };
        slider.SmallChange = whole ? 1 : (slider.Maximum - slider.Minimum) / 100;
        slider.LargeChange = whole ? Math.Max(1, MathF.Round((slider.Maximum - slider.Minimum) / 10)) : (slider.Maximum - slider.Minimum) / 10;
        BindNumber(slider, input, type, () => slider.Value, v => slider.Value = v, h => slider.ValueChanged += h);
        return slider;
    }

    /// <summary>Creates a 28 px knob over the input's range (0..1 without one), reset by a double click to the value it started with.</summary>
    public static Knob CreateKnob(InputSocketViewModel input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var type = input.Type.ValueType ?? typeof(double);
        bool whole = IsWholeNumber(type);
        var knob = new Knob
        {
            Minimum = (float)(input.Minimum ?? 0),
            Maximum = (float)(input.Maximum ?? 1),
            Width = 28,
            Height = 28,
            ValueFormat = whole ? "{0:0}" : "{0:0.00}",
        };
        knob.SmallChange = whole ? 1 : (knob.Maximum - knob.Minimum) / 100;
        knob.LargeChange = whole ? Math.Max(1, MathF.Round((knob.Maximum - knob.Minimum) / 10)) : (knob.Maximum - knob.Minimum) / 10;
        knob.DefaultValue = (float)ToDouble(input.Value);
        BindNumber(knob, input, type, () => knob.Value, v => knob.Value = v, h => knob.ValueChanged += h);
        return knob;
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

    // Keeps a float control and a numeric input in sync; the input keeps its type.
    private static void BindNumber(UIElement control, InputSocketViewModel input, Type type, Func<float> get, Action<float> set, Action<EventHandler<float>> subscribe)
    {
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
