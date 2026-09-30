using Atelier.Controls;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;

namespace Atelier.Audio;

/// <summary>
/// A beat and time ruler, like the one above the tracks of a DAW: ticks and labels for bars and beats
/// (<c>5</c>, <c>5.2</c>, <c>5.2.3</c>), time (<c>01:05.250</c>) or samples that adapt to the zoom, with an optional
/// second row in another unit.
/// </summary>
/// <remarks>
/// <para>
/// The ruler shows its <see cref="TimelineControl.Timeline"/> context; give the same context to the tracks below it and
/// they zoom and scroll together. Labels are as frequent as their width allows: dragging the ruler up zooms in from bars
/// to beats to sixteenths (or from minutes to seconds to milliseconds), with finer unlabeled ticks between labels.
/// Bar lines are drawn stronger while beats are labeled.
/// </para>
/// <para>
/// Like Bitwig's ruler, dragging zooms (up to zoom in, down to zoom out) around the time where the drag started and
/// scrolls sideways with the pointer; the wheel zooms, double-clicking shows the whole song, and right-clicking opens a
/// menu to choose the units (see <see cref="TimelineCommands.RulerGroup"/>). The commands of every timeline control
/// work too (see <see cref="TimelineCommands.Group"/>).
/// </para>
/// <para>
/// The ruler is <see cref="RowHeight"/> pixels high, plus <see cref="SecondaryRowHeight"/> with a
/// <see cref="SecondaryMode"/>. Labels use <see cref="Control.Foreground"/> and the second row
/// <see cref="SecondaryForeground"/>, both at <see cref="Control.FontSize"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var song = new TimelineContext { TempoMap = TempoMap.Constant(128) };
/// new StackPanel().Children(
///     new TimelineRuler().Timeline(song).SecondaryMode(TimelineRulerMode.Time),
///     new TimelineLane().Timeline(song).Height(64),
///     new TimelineLane().Timeline(song).Height(64));
/// </code>
/// </example>
public class TimelineRuler : TimelineControl
{
    /// <summary>Identifies the <see cref="SecondaryMode"/> property.</summary>
    public static readonly BindableProperty<TimelineRulerMode?> SecondaryModeProperty =
        BindableProperty.Register<TimelineRuler, TimelineRulerMode?>(nameof(SecondaryMode), null, options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="SecondaryForeground"/> property.</summary>
    public static readonly BindableProperty<Color> SecondaryForegroundProperty =
        BindableProperty.Register<TimelineRuler, Color>(nameof(SecondaryForeground), Color.FromRgb(0x80, 0x80, 0x80), options: PropertyOptions.AffectsRender);

    /// <summary>The height of the main row of ticks and labels, in pixels.</summary>
    public const float RowHeight = 26f;

    /// <summary>The height of the second row (with a <see cref="SecondaryMode"/>), in pixels.</summary>
    public const float SecondaryRowHeight = 16f;

    private readonly TimelineGrid _secondaryGrid = new();

    /// <summary>Initializes a ruler, running the <see cref="TimelineCommands.RulerGroup"/> commands on itself as well.</summary>
    public TimelineRuler()
    {
        AdditionalScopes.Add(new KeybindingScope(TimelineCommands.RulerGroup, this));
    }

    /// <summary>Gets or sets what the second row of labels counts in, or <c>null</c> (the default) for no second row.</summary>
    public TimelineRulerMode? SecondaryMode { get => GetValue(SecondaryModeProperty); set => SetValue(SecondaryModeProperty, value); }

    /// <summary>Gets or sets the color of the second row's labels.</summary>
    public Color SecondaryForeground { get => GetValue(SecondaryForegroundProperty); set => SetValue(SecondaryForegroundProperty, value); }

    /// <summary>Gets the second row's grid as last computed by <see cref="UpdateSecondaryGrid"/>.</summary>
    public TimelineGrid SecondaryGrid => _secondaryGrid;

    /// <summary>Computes <see cref="SecondaryGrid"/> like <see cref="TimelineControl.UpdateGrid"/>; returns <c>null</c> without a <see cref="SecondaryMode"/>.</summary>
    public TimelineGrid? UpdateSecondaryGrid(float charWidth)
    {
        if (SecondaryMode is not { } mode) return null;
        _secondaryGrid.Mode = mode;
        _secondaryGrid.CharWidth = charWidth;
        _secondaryGrid.Update(CurrentTimeline, Bounds.Width);
        return _secondaryGrid;
    }

    /// <summary>Opens the ruler's menu (see <see cref="CreateContextMenu"/>); returns it, or <c>null</c> when it couldn't open.</summary>
    public ContextMenu? ShowContextMenu()
    {
        var menu = CreateContextMenu();
        return menu.Open(this) ? menu : null;
    }

    /// <summary>
    /// Creates the ruler's menu: the unit of the main row (Beats, Time, Samples), a "Second row" submenu (None or a unit)
    /// and "Zoom to fit". Override it to change the menu.
    /// </summary>
    public virtual ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();
        foreach (var mode in Enum.GetValues<TimelineRulerMode>())
        {
            var item = new MenuItem(mode.ToString()) { IsCheckable = true, GroupName = "TimelineRulerMode", IsChecked = Mode == mode };
            item.Click += (_, _) => Mode = mode;
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());

        var secondary = new MenuItem("Second row");
        var none = new MenuItem("None") { IsCheckable = true, GroupName = "TimelineRulerSecondaryMode", IsChecked = SecondaryMode == null };
        none.Click += (_, _) => SecondaryMode = null;
        secondary.Items.Add(none);
        foreach (var mode in Enum.GetValues<TimelineRulerMode>())
        {
            var item = new MenuItem(mode.ToString()) { IsCheckable = true, GroupName = "TimelineRulerSecondaryMode", IsChecked = SecondaryMode == mode };
            item.Click += (_, _) => SecondaryMode = mode;
            secondary.Items.Add(item);
        }
        menu.Items.Add(secondary);
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem(null, TimelineCommands.ZoomToFit) { CommandParameter = this });
        return menu;
    }

    /// <inheritdoc/>
    /// <remarks><see cref="RowHeight"/> high, plus <see cref="SecondaryRowHeight"/> with a <see cref="SecondaryMode"/>; as wide as it's given.</remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(availableSize);
        return new Size(0, RowHeight + (SecondaryMode != null ? SecondaryRowHeight : 0));
    }
}
