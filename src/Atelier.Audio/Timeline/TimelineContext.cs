using System.ComponentModel;

namespace Atelier.Audio;

/// <summary>
/// The state that connected timeline controls share: which part of the song is in view (<see cref="Start"/> and
/// <see cref="PixelsPerSecond"/>); the song's <see cref="TempoMap"/> and <see cref="SampleRate"/>; and the transport
/// and arrangement shown across the controls: <see cref="PlayStart"/>, <see cref="Playhead"/>, the <see cref="Loop"/>
/// and the shared <see cref="Markers"/>.
/// </summary>
/// <remarks>
/// <para>
/// Give the same context to a ruler and the tracks below it, and zooming or scrolling any of them moves them all. Song
/// time <c>t</c> is at <c>x = (t − Start) × PixelsPerSecond</c> in a control that scrolls with the view, whatever its
/// position: controls don't need to share a left edge, and a control pinned to a later time (like a clip on a track)
/// uses the same scale from its own start.
/// </para>
/// <para>
/// Times are <see cref="double"/> seconds, precise to well below a sample for hours of audio. <see cref="Start"/> stays
/// between 0 and <see cref="Duration"/>, and <see cref="PixelsPerSecond"/> between <see cref="MinPixelsPerSecond"/> and
/// <see cref="MaxPixelsPerSecond"/> (<see cref="MaxPixelsPerSample"/> pixels for each sample).
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var song = new TimelineContext { TempoMap = TempoMap.Constant(128), PixelsPerSecond = 50 };
/// song.ZoomAt(2, anchorX: 300);   // zoom in around the pointer, keeping the time under it in place
/// </code>
/// </example>
public sealed class TimelineContext : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs s_startArgs = new(nameof(Start));
    private static readonly PropertyChangedEventArgs s_pixelsPerSecondArgs = new(nameof(PixelsPerSecond));
    private static readonly PropertyChangedEventArgs s_minPixelsPerSecondArgs = new(nameof(MinPixelsPerSecond));
    private static readonly PropertyChangedEventArgs s_maxPixelsPerSampleArgs = new(nameof(MaxPixelsPerSample));
    private static readonly PropertyChangedEventArgs s_maxPixelsPerSecondArgs = new(nameof(MaxPixelsPerSecond));
    private static readonly PropertyChangedEventArgs s_durationArgs = new(nameof(Duration));
    private static readonly PropertyChangedEventArgs s_sampleRateArgs = new(nameof(SampleRate));
    private static readonly PropertyChangedEventArgs s_tempoMapArgs = new(nameof(TempoMap));
    private static readonly PropertyChangedEventArgs s_playStartArgs = new(nameof(PlayStart));
    private static readonly PropertyChangedEventArgs s_playheadArgs = new(nameof(Playhead));
    private static readonly PropertyChangedEventArgs s_loopArgs = new(nameof(Loop));
    private static readonly PropertyChangedEventArgs s_isLoopEnabledArgs = new(nameof(IsLoopEnabled));

    private TimelinePosition _playStart;
    private double? _playhead;
    private TimelineRange _loop;
    private bool _isLoopEnabled;
    private double _start;
    private double _pixelsPerSecond = 100;
    private double _minPixelsPerSecond = 0.05;
    private double _maxPixelsPerSample = 32;
    private double _duration = double.PositiveInfinity;
    private double _sampleRate = 48000;
    private TempoMap _tempoMap = TempoMap.Default;

    /// <inheritdoc/>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Occurs once after <see cref="Start"/> or <see cref="PixelsPerSecond"/> (or both) changed, for controls that only
    /// redraw; <see cref="PropertyChanged"/> reports each property.
    /// </summary>
    public event EventHandler? ViewChanged;

    /// <summary>Gets or sets the time at the left edge of the view, in seconds; kept between 0 and <see cref="Duration"/>. The default is 0.</summary>
    public double Start
    {
        get => _start;
        set => SetView(value, _pixelsPerSecond);
    }

    /// <summary>
    /// Gets or sets the zoom: how many pixels a second takes, kept between <see cref="MinPixelsPerSecond"/> and
    /// <see cref="MaxPixelsPerSecond"/>. The default is 100.
    /// </summary>
    public double PixelsPerSecond
    {
        get => _pixelsPerSecond;
        set => SetView(_start, value);
    }

    /// <summary>Gets or sets the smallest <see cref="PixelsPerSecond"/>, the furthest zoomed out. The default is 0.05 (an hour in 180 pixels).</summary>
    public double MinPixelsPerSecond
    {
        get => _minPixelsPerSecond;
        set
        {
            ThrowIfNotPositive(value);
            if (_minPixelsPerSecond == value) return;
            _minPixelsPerSecond = value;
            PropertyChanged?.Invoke(this, s_minPixelsPerSecondArgs);
            SetView(_start, _pixelsPerSecond);
        }
    }

    /// <summary>Gets or sets how many pixels a sample may take at most, the furthest zoomed in. The default is 32.</summary>
    public double MaxPixelsPerSample
    {
        get => _maxPixelsPerSample;
        set
        {
            ThrowIfNotPositive(value);
            if (_maxPixelsPerSample == value) return;
            _maxPixelsPerSample = value;
            PropertyChanged?.Invoke(this, s_maxPixelsPerSampleArgs);
            PropertyChanged?.Invoke(this, s_maxPixelsPerSecondArgs);
            SetView(_start, _pixelsPerSecond);
        }
    }

    /// <summary>Gets the largest <see cref="PixelsPerSecond"/>: <see cref="SampleRate"/> × <see cref="MaxPixelsPerSample"/>, at least <see cref="MinPixelsPerSecond"/>.</summary>
    public double MaxPixelsPerSecond => Math.Max(_minPixelsPerSecond, _sampleRate * _maxPixelsPerSample);

    /// <summary>
    /// Gets or sets the length of the song in seconds: how far <see cref="Start"/> can scroll, and what
    /// <see cref="ZoomToFit(double)"/> shows. The default is <see cref="double.PositiveInfinity"/>, no end.
    /// </summary>
    public double Duration
    {
        get => _duration;
        set
        {
            if (double.IsNaN(value) || value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, "The duration must be zero or more.");
            if (_duration == value) return;
            _duration = value;
            PropertyChanged?.Invoke(this, s_durationArgs);
            SetView(_start, _pixelsPerSecond);
        }
    }

    /// <summary>Gets or sets the sample rate in samples per second, for the samples ruler and the zoom limit. The default is 48000.</summary>
    public double SampleRate
    {
        get => _sampleRate;
        set
        {
            ThrowIfNotPositive(value);
            if (_sampleRate == value) return;
            _sampleRate = value;
            PropertyChanged?.Invoke(this, s_sampleRateArgs);
            PropertyChanged?.Invoke(this, s_maxPixelsPerSecondArgs);
            SetView(_start, _pixelsPerSecond);
        }
    }

    /// <summary>Gets or sets the song's tempo and time signatures. The default is <see cref="TempoMap.Default"/> (120 BPM, 4/4).</summary>
    public TempoMap TempoMap
    {
        get => _tempoMap;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (ReferenceEquals(_tempoMap, value)) return;
            _tempoMap = value;
            PropertyChanged?.Invoke(this, s_tempoMapArgs);
        }
    }

    /// <summary>
    /// Gets or sets where playback starts (the play start marker): the ruler sets it when clicked. The default is the
    /// start of the song.
    /// </summary>
    public TimelinePosition PlayStart
    {
        get => _playStart;
        set
        {
            if (_playStart == value) return;
            _playStart = value;
            PropertyChanged?.Invoke(this, s_playStartArgs);
        }
    }

    /// <summary>
    /// Gets or sets the playback position in seconds, drawn as a line across the connected controls while the app plays;
    /// <c>null</c> (the default) hides it.
    /// </summary>
    public double? Playhead
    {
        get => _playhead;
        set
        {
            if (_playhead == value) return;
            _playhead = value;
            PropertyChanged?.Invoke(this, s_playheadArgs);
        }
    }

    /// <summary>
    /// Gets or sets the loop: the section of time the ruler's loop bar shows and edits. Its ends keep their unit when
    /// edited. The default is empty, at the start.
    /// </summary>
    public TimelineRange Loop
    {
        get => _loop;
        set
        {
            if (_loop == value) return;
            _loop = value;
            PropertyChanged?.Invoke(this, s_loopArgs);
        }
    }

    /// <summary>Gets or sets whether the <see cref="Loop"/> is on: drawn in full color and shaded across the lanes. The default is <c>false</c>.</summary>
    public bool IsLoopEnabled
    {
        get => _isLoopEnabled;
        set
        {
            if (_isLoopEnabled == value) return;
            _isLoopEnabled = value;
            PropertyChanged?.Invoke(this, s_isLoopEnabledArgs);
        }
    }

    /// <summary>
    /// Gets the markers shared by the connected controls: the ruler shows them as flags and the other controls as lines.
    /// Markers a single control shows go in its own <see cref="TimelineControl.Markers"/>.
    /// </summary>
    public TimelineMarkerCollection Markers { get; } = [];

    /// <summary>Creates a position for a time in seconds, in <paramref name="unit"/> (for example beats for a beat ruler).</summary>
    public TimelinePosition CreatePosition(double seconds, TimelineUnit unit) =>
        unit == TimelineUnit.Beats ? TimelinePosition.Beats(_tempoMap.SecondsToBeats(seconds)) : TimelinePosition.Seconds(seconds);

    /// <summary>Gets <paramref name="position"/> moved to <paramref name="seconds"/>, keeping its unit.</summary>
    public TimelinePosition MovePosition(TimelinePosition position, double seconds) => CreatePosition(seconds, position.Unit);

    /// <summary>Gets the x of song time <paramref name="time"/> (seconds) in a control that scrolls with the view.</summary>
    public double TimeToX(double time) => (time - _start) * _pixelsPerSecond;

    /// <summary>Gets the song time in seconds at <paramref name="x"/> in a control that scrolls with the view.</summary>
    public double XToTime(double x) => _start + x / _pixelsPerSecond;

    /// <summary>Gets a length in seconds as pixels at the current zoom.</summary>
    public double SecondsToPixels(double seconds) => seconds * _pixelsPerSecond;

    /// <summary>Converts seconds to samples at <see cref="SampleRate"/>.</summary>
    public double SecondsToSamples(double seconds) => seconds * _sampleRate;

    /// <summary>Converts samples to seconds at <see cref="SampleRate"/>.</summary>
    public double SamplesToSeconds(double samples) => samples / _sampleRate;

    /// <summary>Multiplies the zoom by <paramref name="factor"/>, keeping the time at <paramref name="anchorX"/> in place (as far as the limits allow).</summary>
    public void ZoomAt(double factor, double anchorX)
    {
        if (!double.IsFinite(factor) || factor <= 0) throw new ArgumentOutOfRangeException(nameof(factor), factor, "The factor must be finite and positive.");
        ZoomTo(_pixelsPerSecond * factor, anchorX);
    }

    /// <summary>Sets the zoom to <paramref name="pixelsPerSecond"/>, keeping the time at <paramref name="anchorX"/> in place (as far as the limits allow).</summary>
    public void ZoomTo(double pixelsPerSecond, double anchorX)
    {
        double anchor = XToTime(anchorX);
        double zoom = ClampZoom(pixelsPerSecond);
        SetView(anchor - anchorX / zoom, zoom);
    }

    /// <summary>Scrolls by <paramref name="pixels"/> (positive to later times).</summary>
    public void ScrollBy(double pixels) => SetView(_start + pixels / _pixelsPerSecond, _pixelsPerSecond);

    /// <summary>Zooms and scrolls so <paramref name="startTime"/> to <paramref name="endTime"/> fills <paramref name="width"/> pixels.</summary>
    public void ZoomToFit(double startTime, double endTime, double width)
    {
        if (!(endTime > startTime) || !double.IsFinite(endTime - startTime) || !(width > 0)) return;
        double zoom = ClampZoom(width / (endTime - startTime));
        // Center the range when the limits don't let it fill the width exactly.
        SetView((startTime + endTime) * 0.5 - width * 0.5 / zoom, zoom);
    }

    /// <summary>Shows the whole song (0 to <see cref="Duration"/>) in <paramref name="width"/> pixels; does nothing without a finite duration.</summary>
    public void ZoomToFit(double width) => ZoomToFit(0, _duration, width);

    private double ClampZoom(double pixelsPerSecond) =>
        double.IsNaN(pixelsPerSecond) ? _pixelsPerSecond : Math.Clamp(pixelsPerSecond, _minPixelsPerSecond, MaxPixelsPerSecond);

    private void SetView(double start, double pixelsPerSecond)
    {
        if (double.IsNaN(start)) throw new ArgumentOutOfRangeException(nameof(start), start, "The start must be a number.");
        double zoom = ClampZoom(pixelsPerSecond);
        start = Math.Clamp(start, 0, _duration);
        bool startChanged = start != _start;
        bool zoomChanged = zoom != _pixelsPerSecond;
        if (!startChanged && !zoomChanged) return;
        _start = start;
        _pixelsPerSecond = zoom;
        if (startChanged) PropertyChanged?.Invoke(this, s_startArgs);
        if (zoomChanged) PropertyChanged?.Invoke(this, s_pixelsPerSecondArgs);
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void ThrowIfNotPositive(double value)
    {
        if (!double.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value), value, "The value must be finite and positive.");
    }
}
