using System.ComponentModel;
using System.Globalization;
using Atelier.Controls;
using Atelier.Core.Inspection;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.DevTools;

/// <summary>
/// An element as the property grid inspects it: one descriptor per bindable property that applies to the element's
/// type (attached ones, e.g. Grid.Row, included), in categories by declaring type. Changes of the element are reported,
/// so the grid shows live values.
/// </summary>
public sealed class ElementInspection : IInspectableObject, INotifyPropertyChanged, IDisposable
{
    private readonly IReadOnlyList<IPropertyDescriptor> _descriptors;

    /// <summary>Initializes the inspection of <paramref name="element"/>.</summary>
    public ElementInspection(UIElement element)
    {
        Element = element;
        _descriptors = BindableProperty.GetPropertiesFor(element.GetType())
            .Select(p => (IPropertyDescriptor)new BindablePropertyDescriptor(p))
            .ToList();
        element.PropertyChanged += OnElementPropertyChanged;
    }

    /// <summary>Gets the inspected element.</summary>
    public UIElement Element { get; }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc/>
    public IReadOnlyList<IPropertyDescriptor> GetProperties() => _descriptors;

    private void OnElementPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, e);
        // Attached properties are named Owner.Name in the grid.
        foreach (var descriptor in _descriptors)
        {
            if (descriptor is BindablePropertyDescriptor { Property.IsAttached: true } attached && attached.Property.Name == e.PropertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(attached.Name));
            }
        }
    }

    /// <summary>Stops following the element.</summary>
    public void Dispose() => Element.PropertyChanged -= OnElementPropertyChanged;

    /// <summary>Gets a short description of an element: its type and, when it has one, its text or header.</summary>
    public static string Describe(UIElement element)
    {
        string name = element.GetType().Name;
        string? text = element switch
        {
            TextBlock block => block.Text,
            TextBox box => string.IsNullOrEmpty(box.Label) ? box.Text : box.Label,
            ContentControl { Content: string content } => content,
            ContentControl { Content: TextBlock { Text: var contentText } } => contentText,
            Icon icon => icon.Kind.ToString(),
            _ => null,
        };
        if (element.StyleKey is { Length: > 0 } key) name += $" [{key}]";
        if (string.IsNullOrWhiteSpace(text)) return name;
        text = text.ReplaceLineEndings(" ");
        return $"{name}  \"{(text.Length > 40 ? text[..40] + "…" : text)}\"";
    }

    /// <summary>Formats a value for the tools: numbers without noise, colors as hex, thicknesses as their sides.</summary>
    public static string Format(object? value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        float f => f.ToString("0.###", CultureInfo.InvariantCulture),
        double d => d.ToString("0.###", CultureInfo.InvariantCulture),
        Color c => c.ToString(),
        Thickness t => FormatThickness(t.Left, t.Top, t.Right, t.Bottom),
        CornerRadius r => FormatThickness(r.TopLeft, r.TopRight, r.BottomRight, r.BottomLeft),
        Size s => $"{s.Width:0.##} × {s.Height:0.##}",
        Rect r => $"{r.X:0.##}, {r.Y:0.##}  {r.Width:0.##} × {r.Height:0.##}",
        UIElement e => Describe(e),
        _ => value.ToString() ?? string.Empty,
    };

    // "8", "8,4" (horizontal, vertical) or "1,2,3,4", as they are typed.
    private static string FormatThickness(float a, float b, float c, float d)
    {
        string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        if (a == b && b == c && c == d) return F(a);
        if (a == c && b == d) return $"{F(a)},{F(b)}";
        return $"{F(a)},{F(b)},{F(c)},{F(d)}";
    }

    /// <summary>Parses "8", "8,4" or "1,2,3,4" (commas or spaces) into four values; <c>false</c> if it isn't that.</summary>
    public static bool TryParseSides(string text, out float a, out float b, out float c, out float d)
    {
        a = b = c = d = 0;
        var parts = text.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
        var values = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])) return false;
        }
        switch (values.Length)
        {
            case 1: a = b = c = d = values[0]; return true;
            case 2: a = c = values[0]; b = d = values[1]; return true;
            case 4: a = values[0]; b = values[1]; c = values[2]; d = values[3]; return true;
            default: return false;
        }
    }

    /// <summary>Registers the tools' editors on <paramref name="grid"/>: text editors for thicknesses and corner radii.</summary>
    public static void RegisterEditors(PropertyGrid grid)
    {
        grid.RegisterEditor<Thickness>(context => SidesEditor(context,
            v => v is Thickness t ? Format(t) : string.Empty,
            text => TryParseSides(text, out var l, out var t, out var r, out var b) ? new Thickness(l, t, r, b) : null));
        grid.RegisterEditor<CornerRadius>(context => SidesEditor(context,
            v => v is CornerRadius r ? Format(r) : string.Empty,
            text => TryParseSides(text, out var tl, out var tr, out var br, out var bl) ? new CornerRadius(tl, tr, br, bl) : null));
    }

    // A text box that stores the parsed value on Enter or when it loses focus.
    private static UIElement SidesEditor(PropertyEditorContext context, Func<object?, string> format, Func<string, object?> parse)
    {
        var box = new TextBox
        {
            Text = format(context.Value),
            Height = 32,
            FieldHeight = 32,
            Padding = new Thickness(8, 0),
            IsReadOnly = context.IsReadOnly,
            VerticalAlignment = Atelier.Core.Primitives.VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(box, "All sides (8), horizontal and vertical (8,4), or each side (1,2,3,4)");
        void Commit()
        {
            if (context.IsReadOnly) return;
            if (parse(box.Text) is { } value)
            {
                context.SetError(null);
                context.UpdateValue(value);
            }
            else
            {
                context.SetError("Use 8, 8,4 or 1,2,3,4");
            }
        }
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Core.Events.Key.Enter)
            {
                Commit();
                e.Handled = true;
            }
        };
        box.LostFocus += (_, _) => Commit();
        context.ValueChanged += v =>
        {
            if (!box.IsFocused) box.Text = format(v);
        };
        return box;
    }

    // A bindable property as a property-grid descriptor.
    private sealed class BindablePropertyDescriptor(BindableProperty property) : IPropertyDescriptor
    {
        public BindableProperty Property { get; } = property;

        public string Name => Property.IsAttached ? $"{Property.OwnerType.Name}.{Property.Name}" : Property.Name;

        public string DisplayName => Name;

        public string Category => Property.IsAttached ? $"Attached ({Property.OwnerType.Name})" : Property.OwnerType.Name;

        public Type PropertyType => Property.PropertyType;

        public bool IsReadOnly => Property.IsReadOnly;

        public string? Description => $"{Property.OwnerType.Name}.{Property.Name} ({Property.PropertyType.Name})";

        public object? GetValue(object target) => ((ElementInspection)target).Element.GetValueUntyped(Property);

        public void SetValue(object target, object? value)
        {
            if (!Property.IsReadOnly) ((ElementInspection)target).Element.SetValueUntyped(Property, value);
        }
    }
}

/// <summary>A property of the inspected element with its current value and where the value comes from.</summary>
public sealed class PropertyValueRow
{
    internal PropertyValueRow(UIElement element, BindableProperty property)
    {
        Element = element;
        Property = property;
        Refresh();
    }

    /// <summary>Gets the element.</summary>
    public UIElement Element { get; }

    /// <summary>Gets the property.</summary>
    public BindableProperty Property { get; }

    /// <summary>Gets the property's name (Owner.Name for attached properties).</summary>
    public string Name => Property.IsAttached ? $"{Property.OwnerType.Name}.{Property.Name}" : Property.Name;

    /// <summary>Gets the declaring type's name.</summary>
    public string DeclaredBy => Property.OwnerType.Name;

    /// <summary>Gets the value as text.</summary>
    public string Value { get; private set; } = string.Empty;

    /// <summary>Gets where the value comes from: Default, Inherited, Style, Local, ... (see <see cref="ValueSource"/>).</summary>
    public string Source { get; private set; } = string.Empty;

    /// <summary>Gets the value type's name.</summary>
    public string TypeName => Property.PropertyType.Name;

    /// <summary>Reads the value again.</summary>
    public void Refresh()
    {
        Value = ElementInspection.Format(Element.GetValueUntyped(Property));
        Source = Element.GetValueSource(Property).ToString();
    }

    /// <summary>Gets the rows of all properties of <paramref name="element"/>, by name.</summary>
    public static List<PropertyValueRow> For(UIElement element) =>
        BindableProperty.GetPropertiesFor(element.GetType())
            .Select(p => new PropertyValueRow(element, p))
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
