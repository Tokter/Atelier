using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Atelier.Core.Inspection;
using Atelier.Core.Primitives;

namespace Atelier.Gallery.Models;

public enum ShapeKind
{
    Rectangle,
    Rounded,
    Pill
}

public enum CloudRegion
{
    UsEast,
    UsWest,
    EuCentral,
    AsiaPacific
}

public enum EnvironmentType
{
    Development,
    Staging,
    Production
}

public enum LogSeverity
{
    Debug,
    Information,
    Warning,
    Error
}

[Flags]
public enum ServiceFeatures
{
    None = 0,
    Caching = 1,
    Compression = 2,
    Metrics = 4,
    Tracing = 8
}

public enum CollisionMode
{
    None,
    Bounce,
    Stick
}

/// <summary>
/// A shape shown next to the property grid. It raises <see cref="INotifyPropertyChanged.PropertyChanged"/>, so the
/// grid and the preview follow changes made in code as well as in the grid.
/// </summary>
[Inspectable]
public partial class ShapeModel : INotifyPropertyChanged
{
    private string _name = "Preview shape";
    private bool _isVisible = true;
    private int _width = 220;
    private int _height = 120;
    private ShapeKind _kind = ShapeKind.Rounded;
    private Color _fill = Color.FromHex("#6750A4");
    private Color _stroke = Color.FromHex("#21005D");
    private float _strokeWidth = 2;
    private float _opacity = 1;

    public event PropertyChangedEventHandler? PropertyChanged;

    [InspectableProperty("Name", "General", Description = "A label for the shape.")]
    public string Name { get => _name; set => Set(ref _name, value); }

    [InspectableProperty("Visible", "General", Description = "Hides the shape without removing it.")]
    public bool IsVisible { get => _isVisible; set => Set(ref _isVisible, value); }

    [InspectableProperty("Width", "Size", Description = "Width in pixels, 40 to 400.")]
    public int Width { get => _width; set => Set(ref _width, Math.Clamp(value, 40, 400)); }

    [InspectableProperty("Height", "Size", Description = "Height in pixels, 40 to 240.")]
    public int Height { get => _height; set => Set(ref _height, Math.Clamp(value, 40, 240)); }

    [InspectableProperty("Kind", "Appearance", Description = "The corner style: square, rounded or fully rounded.")]
    public ShapeKind Kind { get => _kind; set => Set(ref _kind, value); }

    [InspectableProperty("Fill", "Appearance", Description = "The fill color.")]
    public Color Fill { get => _fill; set => Set(ref _fill, value); }

    [InspectableProperty("Stroke", "Appearance", Description = "The outline color.")]
    public Color Stroke { get => _stroke; set => Set(ref _stroke, value); }

    [InspectableProperty("Stroke width", "Appearance", Description = "The outline width in pixels.")]
    public float StrokeWidth { get => _strokeWidth; set => Set(ref _strokeWidth, Math.Clamp(value, 0, 12)); }

    [InspectableProperty("Opacity", "Appearance", Description = "From 0 (invisible) to 1 (opaque).")]
    public float Opacity { get => _opacity; set => Set(ref _opacity, Math.Clamp(value, 0, 1)); }

    [InspectableProperty("Area", "Size", IsReadOnly = true, Description = "Width × height, computed.")]
    public int Area => Width * Height;

    public void Reset()
    {
        Name = "Preview shape";
        IsVisible = true;
        Width = 220;
        Height = 120;
        Kind = ShapeKind.Rounded;
        Fill = Color.FromHex("#6750A4");
        Stroke = Color.FromHex("#21005D");
        StrokeWidth = 2;
        Opacity = 1;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (name is nameof(Width) or nameof(Height))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Area)));
        }
    }
}

/// <summary>A service configuration: strings, numbers, enums, a flags enum and a nullable value.</summary>
[Inspectable]
public partial class ServiceConfigModel
{
    [InspectableProperty("Service name", "Service", Description = "Must not be empty: the grid's PropertyValueChanging handler rejects empty names.")]
    public string ServiceName { get; set; } = "auth-gateway";

    [InspectableProperty("Region", "Service")]
    public CloudRegion Region { get; set; } = CloudRegion.EuCentral;

    [InspectableProperty("Environment", "Service")]
    public EnvironmentType Environment { get; set; } = EnvironmentType.Production;

    [InspectableProperty("Features", "Service", Description = "A [Flags] enum is edited with one check box per flag.")]
    public ServiceFeatures Features { get; set; } = ServiceFeatures.Caching | ServiceFeatures.Metrics;

    [InspectableProperty("Host", "Network")]
    public string Host { get; set; } = "auth.internal.example.com";

    [InspectableProperty("Port", "Network", Description = "1 to 65535. Other values are rejected by the PropertyValueChanging handler.")]
    public int Port { get; set; } = 8443;

    [InspectableProperty("Max connections", "Network", Description = "Nullable: clear the value for no limit.")]
    public int? MaxConnections { get; set; } = 10_000;

    [InspectableProperty("Use TLS", "Security")]
    public bool UseTls { get; set; } = true;

    [InspectableProperty("Timeout (s)", "Performance")]
    public double TimeoutSeconds { get; set; } = 3.5;

    [InspectableProperty("Log level", "Diagnostics")]
    public LogSeverity LogLevel { get; set; } = LogSeverity.Information;

    [InspectableProperty("Runtime", "Diagnostics", IsReadOnly = true)]
    public string Runtime { get; } = ".NET 10";
}

/// <summary>A particle emitter whose setters validate by throwing, to show how the grid reports errors.</summary>
[Inspectable]
public partial class EmitterModel
{
    private int _rate = 250;

    [InspectableProperty("Name", "Emitter")]
    public string Name { get; set; } = "Sparks";

    [InspectableProperty("Active", "Emitter")]
    public bool IsActive { get; set; } = true;

    [InspectableProperty("Rate (per second)", "Simulation", Description = "The setter throws above 5000; the grid shows the message on the row.")]
    public int Rate
    {
        get => _rate;
        set => _rate = value <= 5000 ? value : throw new ArgumentOutOfRangeException(nameof(value), "The rate can be at most 5000 per second.");
    }

    [InspectableProperty("Gravity", "Simulation")]
    public float Gravity { get; set; } = 9.81f;

    [InspectableProperty("Bounciness", "Simulation")]
    public double Bounciness { get; set; } = 0.75;

    [InspectableProperty("Collision", "Simulation")]
    public CollisionMode Collision { get; set; } = CollisionMode.Bounce;

    [InspectableProperty("Color", "Appearance")]
    public Color Color { get; set; } = Color.FromHex("#FF9800");
}

/// <summary>Settings edited with custom editors: a slider for every float and a star rating for "Rating".</summary>
[Inspectable]
public partial class PreferencesModel
{
    [InspectableProperty("Display name", "Profile")]
    public string DisplayName { get; set; } = "Ada";

    [InspectableProperty("Rating", "Profile", Description = "Edited with a custom editor chosen by a predicate (the property name).")]
    public int Rating { get; set; } = 4;

    [InspectableProperty("Volume", "Audio", Description = "Every float in this grid uses a custom slider editor registered for the type.")]
    public float Volume { get; set; } = 0.7f;

    [InspectableProperty("Balance", "Audio")]
    public float Balance { get; set; } = 0.5f;

    [InspectableProperty("Accent color", "Appearance")]
    public Color Accent { get; set; } = Color.FromHex("#006A6A");
}
