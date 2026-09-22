using System;
using Atelier.Core.Inspection;
using Atelier.Core.Primitives;

namespace Atelier.Gallery.Models;

#region Enums

public enum ShapeKind
{
    Rectangle,
    RoundedRect,
    Circle,
    Pill
}

public enum BlendMode
{
    Normal,
    Additive,
    Multiply,
    Screen
}

public enum LogSeverity
{
    Verbose,
    Debug,
    Information,
    Warning,
    Error,
    Fatal
}

public enum CloudRegion
{
    UsEast,
    UsWest,
    EuCentral,
    ApSouth,
    SaEast
}

public enum EnvironmentType
{
    Development,
    Staging,
    Production
}

public enum ParticleShape
{
    Spark,
    Orb,
    Star,
    Smoke
}

public enum CollisionMode
{
    None,
    Bounce,
    Dissolve,
    Stick
}

#endregion

#region Inspectable Models

/// <summary>
/// Model representing an interactive 2D graphic shape with geometric, appearance, and rendering properties.
/// Changes directly update the live shape preview in real-time.
/// </summary>
[Inspectable]
public partial class GraphicElementModel
{
    [InspectableProperty("Element Name", "General")]
    public string Name { get; set; } = "Hero Card Background";

    [InspectableProperty("Visible", "General")]
    public bool Visible { get; set; } = true;

    [InspectableProperty("Width (px)", "Geometry")]
    public int Width { get; set; } = 280;

    [InspectableProperty("Height (px)", "Geometry")]
    public int Height { get; set; } = 160;

    [InspectableProperty("Corner Radius", "Geometry")]
    public int CornerRadius { get; set; } = 16;

    [InspectableProperty("Shape Type", "Geometry")]
    public ShapeKind ShapeType { get; set; } = ShapeKind.RoundedRect;

    [InspectableProperty("Fill Color", "Appearance")]
    public Color FillColor { get; set; } = Color.FromHex("#1E88E5");

    [InspectableProperty("Border / Stroke", "Appearance")]
    public Color StrokeColor { get; set; } = Color.FromHex("#90CAF9");

    [InspectableProperty("Opacity (0 - 1)", "Appearance")]
    public float Opacity { get; set; } = 1.0f;

    [InspectableProperty("Shadow Elevation", "Appearance")]
    public float ShadowElevation { get; set; } = 6.0f;

    [InspectableProperty("Blend Mode", "Rendering")]
    public BlendMode BlendMode { get; set; } = BlendMode.Normal;

    [InspectableProperty("Anti-Aliasing", "Rendering")]
    public bool AntiAliasing { get; set; } = true;

    [InspectableProperty("Hardware GPU ID", "System", IsReadOnly = true)]
    public string HardwareId { get; } = "GPU-VK-DX12-0042";

    [InspectableProperty("Created At", "System", IsReadOnly = true)]
    public string CreatedTimestamp { get; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
}

/// <summary>
/// Model representing a backend microservice configuration with network, security, and performance metrics.
/// </summary>
[Inspectable]
public partial class MicroserviceConfigModel
{
    [InspectableProperty("Service Name", "Cluster")]
    public string ServiceName { get; set; } = "atelier-auth-gateway";

    [InspectableProperty("Cloud Region", "Cluster")]
    public CloudRegion Region { get; set; } = CloudRegion.EuCentral;

    [InspectableProperty("Environment", "Cluster")]
    public EnvironmentType Environment { get; set; } = EnvironmentType.Production;

    [InspectableProperty("Host Address", "Network")]
    public string HostAddress { get; set; } = "auth.internal.atelier.io";

    [InspectableProperty("Port Number", "Network")]
    public int Port { get; set; } = 8443;

    [InspectableProperty("Max Connections", "Network")]
    public int MaxConnections { get; set; } = 10000;

    [InspectableProperty("Enable TLS 1.3", "Security")]
    public bool EnableTls { get; set; } = true;

    [InspectableProperty("API Token Key", "Security")]
    public string ApiKey { get; set; } = "ak_live_99fa8c12b07d";

    [InspectableProperty("Timeout (seconds)", "Performance")]
    public double TimeoutSeconds { get; set; } = 3.5;

    [InspectableProperty("Target Cache Ratio", "Performance")]
    public double TargetCacheRatio { get; set; } = 0.95;

    [InspectableProperty("Log Severity", "Diagnostics")]
    public LogSeverity LogLevel { get; set; } = LogSeverity.Information;

    [InspectableProperty("Status Indicator", "Appearance")]
    public Color StatusColor { get; set; } = Color.FromHex("#10B981");

    [InspectableProperty("Runtime Build", "Diagnostics", IsReadOnly = true)]
    public string RuntimeVersion { get; } = ".NET 9.0 Native AOT";

    [InspectableProperty("Service Uptime", "Diagnostics", IsReadOnly = true)]
    public string Uptime { get; } = "99.98% (42 days 14h)";
}

/// <summary>
/// Model representing particle physics simulation parameters.
/// </summary>
[Inspectable]
public partial class ParticleEmitterModel
{
    [InspectableProperty("Emitter Identifier", "Emitter")]
    public string EmitterName { get; set; } = "Cosmic Spark Emitter";

    [InspectableProperty("Is Active", "Emitter")]
    public bool IsActive { get; set; } = true;

    [InspectableProperty("Emission Rate (p/s)", "Simulation")]
    public int EmissionRate { get; set; } = 250;

    [InspectableProperty("Target FPS", "Simulation")]
    public int TargetFps { get; set; } = 120;

    [InspectableProperty("Gravity Force", "Physics")]
    public float GravityForce { get; set; } = 9.81f;

    [InspectableProperty("Restitution / Bounce", "Physics")]
    public double Elasticity { get; set; } = 0.75;

    [InspectableProperty("Collision Mode", "Physics")]
    public CollisionMode CollisionMode { get; set; } = CollisionMode.Bounce;

    [InspectableProperty("Primary Particle Color", "Visuals")]
    public Color ParticleColor { get; set; } = Color.FromHex("#FF9800");

    [InspectableProperty("Secondary Trail Color", "Visuals")]
    public Color TrailColor { get; set; } = Color.FromHex("#E91E63");

    [InspectableProperty("Particle Geometry", "Visuals")]
    public ParticleShape Shape { get; set; } = ParticleShape.Star;

    [InspectableProperty("Compute Engine", "Hardware", IsReadOnly = true)]
    public string ComputeEngine { get; } = "DirectCompute Shader 6.0";
}

#endregion
