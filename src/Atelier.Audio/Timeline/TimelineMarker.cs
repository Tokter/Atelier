using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Atelier.Core.Primitives;

namespace Atelier.Audio;

/// <summary>
/// A colored flag at a point of the timeline, like a cue marker in a DAW: a <see cref="Position"/> (pinned to a time or
/// to the beat grid), a <see cref="Label"/> and a <see cref="Color"/>.
/// </summary>
/// <remarks>
/// Markers in <see cref="TimelineContext.Markers"/> are shared: the ruler shows them as flags and every connected control
/// draws them as lines. Markers in a control's own <see cref="TimelineControl.Markers"/> show on that control only.
/// </remarks>
public sealed class TimelineMarker : INotifyPropertyChanged
{
    private TimelinePosition _position;
    private string _label;
    private Color _color;

    /// <summary>Creates a marker.</summary>
    /// <param name="position">Where the marker is.</param>
    /// <param name="label">The text on its flag.</param>
    /// <param name="color">Its color; transparent (the default) uses the control's <see cref="TimelineControl.MarkerColor"/>.</param>
    public TimelineMarker(TimelinePosition position, string label = "", Color color = default)
    {
        _position = position;
        _label = label ?? "";
        _color = color;
    }

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Gets the named colors the ruler offers for markers, and gives new markers in turn.</summary>
    public static IReadOnlyList<(string Name, Color Color)> Palette { get; } =
    [
        ("Red", Color.FromRgb(0xE5, 0x39, 0x35)),
        ("Orange", Color.FromRgb(0xFB, 0x8C, 0x00)),
        ("Yellow", Color.FromRgb(0xFD, 0xD8, 0x35)),
        ("Green", Color.FromRgb(0x43, 0xA0, 0x47)),
        ("Teal", Color.FromRgb(0x00, 0x89, 0x7B)),
        ("Blue", Color.FromRgb(0x1E, 0x88, 0xE5)),
        ("Purple", Color.FromRgb(0x8E, 0x24, 0xAA)),
        ("Pink", Color.FromRgb(0xD8, 0x1B, 0x60)),
    ];

    /// <summary>Gets or sets where the marker is, in seconds or beats.</summary>
    public TimelinePosition Position { get => _position; set => Set(ref _position, value); }

    /// <summary>Gets or sets the text on the marker's flag.</summary>
    public string Label { get => _label; set => Set(ref _label, value ?? ""); }

    /// <summary>Gets or sets the marker's color; transparent uses the control's <see cref="TimelineControl.MarkerColor"/>.</summary>
    public Color Color { get => _color; set => Set(ref _color, value); }

    /// <summary>Returns the label and position.</summary>
    public override string ToString() => $"{Label} at {Position}";

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>
/// An observable collection of markers that also reports changes to its markers' properties (see <see cref="Changed"/>),
/// so controls redraw when a marker moves.
/// </summary>
public sealed class TimelineMarkerCollection : ObservableCollection<TimelineMarker>
{
    /// <summary>Occurs when a marker is added, removed or replaced, or a marker's property changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the marker at a position in seconds (within <paramref name="tolerance"/> seconds), or <c>null</c>.</summary>
    public TimelineMarker? FindAt(double seconds, TempoMap tempoMap, double tolerance = 1e-6)
    {
        ArgumentNullException.ThrowIfNull(tempoMap);
        foreach (var marker in this)
        {
            if (Math.Abs(marker.Position.ToSeconds(tempoMap) - seconds) <= tolerance) return marker;
        }
        return null;
    }

    /// <inheritdoc/>
    protected override void InsertItem(int index, TimelineMarker item)
    {
        ArgumentNullException.ThrowIfNull(item);
        base.InsertItem(index, item);
        item.PropertyChanged += OnMarkerChanged;
    }

    /// <inheritdoc/>
    protected override void SetItem(int index, TimelineMarker item)
    {
        ArgumentNullException.ThrowIfNull(item);
        this[index].PropertyChanged -= OnMarkerChanged;
        base.SetItem(index, item);
        item.PropertyChanged += OnMarkerChanged;
    }

    /// <inheritdoc/>
    protected override void RemoveItem(int index)
    {
        this[index].PropertyChanged -= OnMarkerChanged;
        base.RemoveItem(index);
    }

    /// <inheritdoc/>
    protected override void ClearItems()
    {
        foreach (var marker in this) marker.PropertyChanged -= OnMarkerChanged;
        base.ClearItems();
    }

    /// <inheritdoc/>
    protected override void OnCollectionChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        base.OnCollectionChanged(e);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnMarkerChanged(object? sender, PropertyChangedEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>A section of the timeline from <paramref name="Start"/> to <paramref name="End"/>, each pinned to a time or to the beat grid, such as the loop.</summary>
/// <param name="Start">Where the section starts.</param>
/// <param name="End">Where the section ends.</param>
public readonly record struct TimelineRange(TimelinePosition Start, TimelinePosition End)
{
    /// <summary>Gets the start in seconds.</summary>
    public double GetStartSeconds(TempoMap tempoMap) => Start.ToSeconds(tempoMap);

    /// <summary>Gets the end in seconds.</summary>
    public double GetEndSeconds(TempoMap tempoMap) => End.ToSeconds(tempoMap);

    /// <summary>Gets whether the section is empty: it doesn't end after it starts.</summary>
    public bool IsEmpty(TempoMap tempoMap) => !(GetEndSeconds(tempoMap) > GetStartSeconds(tempoMap));

    /// <summary>Gets whether a time in seconds is within the section (start included, end excluded).</summary>
    public bool Contains(double seconds, TempoMap tempoMap) => seconds >= GetStartSeconds(tempoMap) && seconds < GetEndSeconds(tempoMap);
}
