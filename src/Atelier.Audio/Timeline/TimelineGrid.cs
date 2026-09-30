namespace Atelier.Audio;

/// <summary>What a timeline ruler or grid counts in.</summary>
public enum TimelineRulerMode
{
    /// <summary>Bars and beats: <c>5</c>, <c>5.2</c>, <c>5.2.3</c>, following the tempo map's time signatures.</summary>
    Beats,

    /// <summary>Minutes and seconds: <c>01:05</c>, with milliseconds (<c>01:05.250</c>) when zoomed in.</summary>
    Time,

    /// <summary>Sample counts at the context's sample rate.</summary>
    Samples,
}

/// <summary>A tick of a <see cref="TimelineGrid"/>.</summary>
/// <param name="Time">The song time in seconds.</param>
/// <param name="X">The x in the control the grid was computed for.</param>
/// <param name="Level">
/// <see cref="TimelineGrid.LabelLevel"/> for labeled ticks, <see cref="TimelineGrid.MediumLevel"/> for the unlabeled
/// ticks between them, <see cref="TimelineGrid.MinorLevel"/> for the finest ticks.
/// </param>
/// <param name="IsEmphasized">
/// Whether the tick is on a coarser natural boundary than the labels: a bar line while beats are labeled, a whole second
/// while milliseconds are, a whole minute while seconds are, or an hour while minutes are.
/// </param>
/// <param name="Label">The label text of a labeled tick; <c>null</c> for the others.</param>
public readonly record struct TimelineTick(double Time, float X, int Level, bool IsEmphasized, string? Label);

/// <summary>
/// Computes the ticks and labels of a timeline ruler for the visible range, adapting their spacing to the zoom: labels
/// are as frequent as their width allows, with up to two finer levels of unlabeled ticks between them.
/// </summary>
/// <remarks>
/// <para>
/// In <see cref="TimelineRulerMode.Time"/> the label step is one of 10, 20, 50 µs … 100, 200, 500 ms, 1, 2, 5, 10, 15,
/// 30 s, 1, 2, 5, 10, 15, 30 min, 1, 2, 5, 10 h; labels show milliseconds (or finer digits) only when the step is below a
/// second. <see cref="TimelineRulerMode.Samples"/> steps through 1, 2, 5 × 10ⁿ samples. In
/// <see cref="TimelineRulerMode.Beats"/> the steps are an eighth, a quarter and half a beat, a beat, a bar and 2, 4, 8 …
/// bars, per time signature; bar groups start at bar 1 (1, 5, 9, …), and labels read <c>bar</c>, <c>bar.beat</c> or
/// <c>bar.beat.sixteenth</c> as the step needs.
/// </para>
/// <para>
/// A label step is used when its spacing is at least the label's width (its character count × <see cref="CharWidth"/>)
/// plus <see cref="LabelPadding"/>; finer ticks when their spacing is at least <see cref="MinTickSpacing"/>. The finest
/// ticks are the adaptive snap grid (<see cref="Snap"/>).
/// </para>
/// <para>
/// Updating reuses the tick list and caches the label strings, so scrolling doesn't allocate once the labels in view
/// have been seen.
/// </para>
/// </remarks>
public sealed class TimelineGrid
{
    /// <summary>The <see cref="TimelineTick.Level"/> of labeled ticks.</summary>
    public const int LabelLevel = 0;

    /// <summary>The <see cref="TimelineTick.Level"/> of the unlabeled ticks halfway (or so) between labels.</summary>
    public const int MediumLevel = 1;

    /// <summary>The <see cref="TimelineTick.Level"/> of the finest ticks.</summary>
    public const int MinorLevel = 2;

    private const int MaxTicks = 20000;
    private const int MaxCachedLabels = 4096;
    private const double Epsilon = 1e-9;
    private const double NanosecondsPerSecond = 1e9;
    private const long Second = 1_000_000_000;

    private static readonly long[] s_timeSteps =
    [
        10_000, 20_000, 50_000, 100_000, 200_000, 500_000,
        1_000_000, 2_000_000, 5_000_000, 10_000_000, 20_000_000, 50_000_000, 100_000_000, 200_000_000, 500_000_000,
        Second, 2 * Second, 5 * Second, 10 * Second, 15 * Second, 30 * Second,
        60 * Second, 120 * Second, 300 * Second, 600 * Second, 900 * Second, 1800 * Second,
        3600 * Second, 7200 * Second, 18000 * Second, 36000 * Second,
    ];

    private static readonly long[] s_sampleSteps = CreateSampleSteps();

    // Sub-bar steps in eighths of a beat, then whole bars.
    private static readonly BeatStep[] s_beatSteps =
    [
        new(1, 0), new(2, 0), new(4, 0), new(8, 0),
        new(0, 1), new(0, 2), new(0, 4), new(0, 8), new(0, 16), new(0, 32), new(0, 64), new(0, 128), new(0, 256), new(0, 512), new(0, 1024),
    ];

    private const int FirstLabeledBeatStep = 1; // a quarter of a beat (a sixteenth in x/4)
    private const int BeatStepIndex = 3;
    private const int BarStepIndex = 4;

    private readonly List<TimelineTick> _ticks = [];
    private readonly Dictionary<long, string> _labels = [];
    private readonly List<(int Meter, BeatStep Step)> _beatSnaps = [];
    private TimelineRulerMode _mode = TimelineRulerMode.Beats;
    private TimelineRulerMode _snapMode;
    private long _snapStep;
    private double _snapUnitsPerSecond;
    private TempoMap? _snapTempoMap;
    private bool _abbreviateBarLabels = true;

    /// <summary>Gets or sets what the grid counts in. The default is <see cref="TimelineRulerMode.Beats"/>.</summary>
    public TimelineRulerMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value) return;
            _mode = value;
            _labels.Clear();
        }
    }

    /// <summary>Gets or sets the width of a label character in pixels, to space labels. The default is 7 (a digit of a 12 px font).</summary>
    public float CharWidth { get; set; } = 7f;

    /// <summary>Gets or sets the space between labels in pixels, beyond their width. The default is 14.</summary>
    public float LabelPadding { get; set; } = 14f;

    /// <summary>Gets or sets the smallest spacing of unlabeled ticks in pixels. The default is 6.</summary>
    public float MinTickSpacing { get; set; } = 6f;

    /// <summary>
    /// Gets or sets whether bar lines are labeled with the bar alone (<c>2</c>, like Bitwig) instead of <c>2.1</c> while
    /// beats are labeled. The default is <c>true</c>.
    /// </summary>
    public bool AbbreviateBarLabels
    {
        get => _abbreviateBarLabels;
        set
        {
            if (_abbreviateBarLabels == value) return;
            _abbreviateBarLabels = value;
            _labels.Clear();
        }
    }

    /// <summary>Gets the ticks from the last update, left to right.</summary>
    public IReadOnlyList<TimelineTick> Ticks => _ticks;

    /// <summary>Computes the ticks for a control of <paramref name="width"/> pixels that scrolls with <paramref name="context"/>.</summary>
    public void Update(TimelineContext context, float width)
    {
        ArgumentNullException.ThrowIfNull(context);
        Update(context, context.Start, width);
    }

    /// <summary>
    /// Computes the ticks for a control of <paramref name="width"/> pixels whose left edge is at song time
    /// <paramref name="leftTime"/>, at the zoom of <paramref name="context"/>. Ticks before time 0 are left out.
    /// </summary>
    public void Update(TimelineContext context, double leftTime, float width)
    {
        ArgumentNullException.ThrowIfNull(context);
        _ticks.Clear();
        _beatSnaps.Clear();
        _snapStep = 0;
        _snapTempoMap = context.TempoMap;
        _snapMode = _mode;
        if (!(width > 0) || !double.IsFinite(leftTime)) return;

        double pixelsPerSecond = context.PixelsPerSecond;
        double end = leftTime + width / pixelsPerSecond;
        if (end < 0) return;
        switch (_mode)
        {
            case TimelineRulerMode.Time:
                UpdateLinear(s_timeSteps, NanosecondsPerSecond, leftTime, end, pixelsPerSecond, samples: false);
                break;
            case TimelineRulerMode.Samples:
                UpdateLinear(s_sampleSteps, context.SampleRate, leftTime, end, pixelsPerSecond, samples: true);
                break;
            default:
                UpdateBeats(context.TempoMap, leftTime, end, pixelsPerSecond);
                break;
        }
    }

    /// <summary>
    /// Snaps a song time (seconds) to the nearest of the finest ticks of the last update, the adaptive grid: the grid
    /// gets finer as the view zooms in. Returns <paramref name="time"/> unchanged before the first update.
    /// </summary>
    public double Snap(double time)
    {
        if (_snapTempoMap == null || !double.IsFinite(time)) return time;
        if (_snapMode != TimelineRulerMode.Beats)
        {
            if (_snapStep <= 0) return time;
            double units = Math.Round(time * _snapUnitsPerSecond / _snapStep) * _snapStep;
            return units / _snapUnitsPerSecond;
        }

        var map = _snapTempoMap;
        double beats = map.SecondsToBeats(time);
        int meter = map.MeterIndexAt(beats);
        var step = new BeatStep(8, 0);
        foreach (var snap in _beatSnaps)
        {
            if (snap.Meter == meter)
            {
                step = snap.Step;
                break;
            }
        }
        var (bar, signature, start, _) = map.GetMeterSegment(meter);
        double snapped;
        if (step.Units > 0)
        {
            double unit = signature.BeatLength / 8 * step.Units;
            snapped = start + Math.Round((beats - start) / unit) * unit;
        }
        else
        {
            double barNumber = bar + (beats - start) / signature.BarLength;
            double snappedBar = Math.Round((barNumber - 1) / step.Bars) * step.Bars + 1;
            snapped = start + (snappedBar - bar) * signature.BarLength;
        }
        return map.BeatsToSeconds(snapped);
    }

    private void UpdateLinear(long[] ladder, double unitsPerSecond, double leftTime, double end, double pixelsPerSecond, bool samples)
    {
        double unitPixels = pixelsPerSecond / unitsPerSecond;
        bool hours = !samples && end >= 3600;
        long maxUnits = (long)Math.Min(end * unitsPerSecond, long.MaxValue / 4);

        int label = ladder.Length - 1;
        for (int i = 0; i < ladder.Length; i++)
        {
            int chars = samples ? TimelineFormat.Digits(maxUnits) : TimeLabelChars(ladder[i], hours);
            if (ladder[i] * unitPixels >= chars * CharWidth + LabelPadding)
            {
                label = i;
                break;
            }
        }
        int minor = label;
        for (int i = 0; i < label; i++)
        {
            if (ladder[label] % ladder[i] == 0 && ladder[i] * unitPixels >= MinTickSpacing)
            {
                minor = i;
                break;
            }
        }
        long medium = 0;
        for (int i = label - 1; i > minor; i--)
        {
            if (ladder[label] % ladder[i] == 0 && ladder[i] % ladder[minor] == 0)
            {
                medium = ladder[i];
                break;
            }
        }

        long labelStep = ladder[label];
        long minorStep = ladder[minor];
        long emphasis = samples ? 0 : labelStep < Second ? Second : labelStep < 60 * Second ? 60 * Second : labelStep < 3600 * Second ? 3600 * Second : 0;
        int decimals = samples ? 0 : TimeDecimals(labelStep);
        _snapStep = minorStep;
        _snapUnitsPerSecond = unitsPerSecond;

        long first = (long)Math.Ceiling(Math.Max(0, leftTime) * unitsPerSecond / minorStep - Epsilon);
        long last = (long)Math.Floor(end * unitsPerSecond / minorStep + Epsilon);
        for (long n = first; n <= last && _ticks.Count < MaxTicks; n++)
        {
            long units = n * minorStep;
            int level = units % labelStep == 0 ? LabelLevel : medium > 0 && units % medium == 0 ? MediumLevel : MinorLevel;
            double time = units / unitsPerSecond;
            string? text = level != LabelLevel ? null
                : samples ? GetLabel(units, static u => TimelineFormat.Samples(u))
                : GetTimeLabel(units, decimals, hours);
            _ticks.Add(new TimelineTick(time, (float)((time - leftTime) * pixelsPerSecond), level, emphasis > 0 && units % emphasis == 0, text));
        }
    }

    private void UpdateBeats(TempoMap map, double leftTime, double end, double pixelsPerSecond)
    {
        double from = map.SecondsToBeats(Math.Max(0, leftTime));
        double to = map.SecondsToBeats(end);
        for (int meter = map.MeterIndexAt(from); meter < map.MeterCount && _ticks.Count < MaxTicks; meter++)
        {
            var (bar, signature, start, segmentEnd) = map.GetMeterSegment(meter);
            if (start > to + Epsilon) break;
            double a = Math.Max(from, start);
            double b = Math.Min(to, segmentEnd);
            double unit = signature.BeatLength / 8;
            int barUnits = signature.Numerator * 8;
            // The fastest tempo in view packs ticks tightest; spacing them for it keeps labels apart everywhere.
            double unitPixels = unit * 60 / map.MaxTempo(a, b) * pixelsPerSecond;
            int barDigits = TimelineFormat.Digits(bar + (long)((b - start) / signature.BarLength) + 1);
            int beatDigits = TimelineFormat.Digits(signature.Numerator);

            int label = s_beatSteps.Length - 1;
            for (int i = FirstLabeledBeatStep; i < s_beatSteps.Length; i++)
            {
                var step = s_beatSteps[i];
                int chars = step.Bars > 0 ? barDigits : step.Units == 8 ? barDigits + 1 + beatDigits : barDigits + beatDigits + 3;
                if (step.Length(barUnits) * unitPixels >= chars * CharWidth + LabelPadding)
                {
                    label = i;
                    break;
                }
            }
            int minor = label;
            for (int i = 0; i < label; i++)
            {
                if (s_beatSteps[i].Length(barUnits) * unitPixels >= MinTickSpacing)
                {
                    minor = i;
                    break;
                }
            }
            int medium = minor < BarStepIndex && BarStepIndex < label ? BarStepIndex
                : minor < BeatStepIndex && BeatStepIndex < label ? BeatStepIndex
                : label - 1 > minor ? label - 1 : -1;

            var labelStep = s_beatSteps[label];
            var mediumStep = medium >= 0 ? s_beatSteps[medium] : default;
            var minorStep = s_beatSteps[minor];
            _beatSnaps.Add((meter, minorStep));

            void Add(long k)
            {
                double beats = start + k * unit;
                if (beats >= segmentEnd - Epsilon) return; // the next segment's first tick
                long relativeBar = k / barUnits;
                int inBar = (int)(k % barUnits);
                long absoluteBar = bar + relativeBar;
                int level = labelStep.Contains(inBar, absoluteBar) ? LabelLevel
                    : medium >= 0 && mediumStep.Contains(inBar, absoluteBar) ? MediumLevel
                    : MinorLevel;
                double time = map.BeatsToSeconds(beats);
                string? text = level == LabelLevel ? GetBeatLabel(absoluteBar, inBar, labelStep.Bars > 0) : null;
                bool emphasized = inBar == 0 && labelStep.Bars == 0;
                _ticks.Add(new TimelineTick(time, (float)((time - leftTime) * pixelsPerSecond), level, emphasized, text));
            }

            if (minorStep.Units > 0)
            {
                long u = minorStep.Units;
                long first = (long)Math.Ceiling((a - start) / unit / u - Epsilon) * u;
                long last = (long)Math.Floor((b - start) / unit / u + Epsilon) * u;
                for (long k = Math.Max(0, first); k <= last && _ticks.Count < MaxTicks; k += u) Add(k);
            }
            else
            {
                long bars = minorStep.Bars;
                long firstBar = bar + Math.Max(0, (long)Math.Ceiling((a - start) / signature.BarLength - Epsilon));
                long lastBar = bar + (long)Math.Floor((b - start) / signature.BarLength + Epsilon);
                long offset = (firstBar - 1) % bars;
                if (offset > 0) firstBar += bars - offset;
                for (long absolute = firstBar; absolute <= lastBar && _ticks.Count < MaxTicks; absolute += bars) Add((absolute - bar) * barUnits);
            }
        }
    }

    private string GetTimeLabel(long nanoseconds, int decimals, bool hours)
    {
        long key = nanoseconds * 32 + decimals * 2 + (hours ? 1 : 0);
        if (_labels.TryGetValue(key, out var text)) return text;
        text = TimelineFormat.Time(nanoseconds, decimals, hours);
        Cache(key, text);
        return text;
    }

    private string GetBeatLabel(long bar, int inBar, bool barsOnly)
    {
        int beat = inBar / 8 + 1;
        int sixteenth = inBar % 8 / 2 + 1;
        int form = barsOnly || (inBar == 0 && AbbreviateBarLabels) ? 0 : inBar % 8 == 0 ? 1 : 2;
        long key = (bar << 16) | ((long)beat << 8) | ((long)sixteenth << 2) | (long)form;
        if (_labels.TryGetValue(key, out var text)) return text;
        text = form switch
        {
            0 => TimelineFormat.Bar((int)bar),
            1 => TimelineFormat.Bar((int)bar, beat),
            _ => TimelineFormat.Bar((int)bar, beat, sixteenth),
        };
        Cache(key, text);
        return text;
    }

    private string GetLabel(long key, Func<long, string> format)
    {
        if (_labels.TryGetValue(key, out var text)) return text;
        text = format(key);
        Cache(key, text);
        return text;
    }

    private void Cache(long key, string text)
    {
        if (_labels.Count >= MaxCachedLabels) _labels.Clear();
        _labels[key] = text;
    }

    // The characters of a time label for a step: mm:ss or h:mm:ss, plus the decimals the step needs.
    private static int TimeLabelChars(long step, bool hours)
    {
        int decimals = TimeDecimals(step);
        return (hours ? 7 : 5) + (decimals > 0 ? decimals + 1 : 0);
    }

    // Whole seconds need no decimals; below that, milliseconds, or more digits for finer steps.
    private static int TimeDecimals(long step)
    {
        if (step % Second == 0) return 0;
        int decimals = 3;
        while (decimals < 9 && step % TimelineFormat.Pow10(9 - decimals) != 0) decimals++;
        return decimals;
    }

    private static long[] CreateSampleSteps()
    {
        var steps = new List<long>();
        for (long power = 1; power <= 1_000_000_000_000; power *= 10)
        {
            steps.Add(power);
            steps.Add(power * 2);
            steps.Add(power * 5);
        }
        return [.. steps];
    }

    // A beat grid step: Units eighths of a beat, or (when Units is 0) Bars bars.
    private readonly record struct BeatStep(int Units, int Bars)
    {
        public long Length(int barUnits) => Units > 0 ? Units : (long)Bars * barUnits;

        public bool Contains(int inBar, long absoluteBar) =>
            Units > 0 ? inBar % Units == 0 : inBar == 0 && (absoluteBar - 1) % Bars == 0;
    }
}
