using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;

namespace Atelier.Charts;

/// <summary>
/// The chart's zoom and pan commands, registered in the <see cref="XYChart.CommandGroup"/> keybinding group with the
/// chart as their target: users can rebind them in the keybinding editor, and the command palette lists them while the
/// focus is in a chart.
/// </summary>
public static class XYChartCommands
{
    /// <summary>How much one step of the zoom commands zooms.</summary>
    public const double ZoomStep = 1.25;

    /// <summary>Gets the command that moves the view by dragging (left drag).</summary>
    public static XYChartPanCommand Pan { get; } = new();

    /// <summary>Gets the command that zooms into the box dragged (right drag).</summary>
    public static XYChartZoomBoxCommand ZoomBox { get; } = new();

    /// <summary>Gets the command that zooms in at the pointer (wheel up, +).</summary>
    public static XYChartZoomCommand ZoomIn { get; } = new(1 / ZoomStep, 1 / ZoomStep);

    /// <summary>Gets the command that zooms out at the pointer (wheel down, −).</summary>
    public static XYChartZoomCommand ZoomOut { get; } = new(ZoomStep, ZoomStep);

    /// <summary>Gets the command that zooms in horizontally only (Ctrl+wheel up).</summary>
    public static XYChartZoomCommand ZoomInX { get; } = new(1 / ZoomStep, 1);

    /// <summary>Gets the command that zooms out horizontally only (Ctrl+wheel down).</summary>
    public static XYChartZoomCommand ZoomOutX { get; } = new(ZoomStep, 1);

    /// <summary>Gets the command that zooms in vertically only (Shift+wheel up).</summary>
    public static XYChartZoomCommand ZoomInY { get; } = new(1, 1 / ZoomStep);

    /// <summary>Gets the command that zooms out vertically only (Shift+wheel down).</summary>
    public static XYChartZoomCommand ZoomOutY { get; } = new(1, ZoomStep);

    /// <summary>Gets the command that fits the view to the data again (Home, double click).</summary>
    public static XYChartResetCommand ResetView { get; } = new();

    /// <summary>
    /// Registers the commands that aren't registered yet (with the users' changes applied). Charts call it when they're
    /// created.
    /// </summary>
    public static void Register()
    {
        Add("Pan", "LeftDrag", Pan, "Pan", "Move the view by dragging", MaterialIcons.PanTool);
        Add("ZoomBox", "RightDrag", ZoomBox, "Zoom to box", "Zoom into the box dragged; Escape cancels", MaterialIcons.ZoomInMap);
        Add("ZoomIn", "WheelUp", ZoomIn, "Zoom in", "Zoom in at the pointer", MaterialIcons.ZoomIn);
        Add("ZoomOut", "WheelDown", ZoomOut, "Zoom out", "Zoom out at the pointer", MaterialIcons.ZoomOut);
        Add("ZoomInKey", "Equal", ZoomIn, "Zoom in", "Zoom in at the pointer, or the center", MaterialIcons.ZoomIn);
        Add("ZoomOutKey", "Minus", ZoomOut, "Zoom out", "Zoom out at the pointer, or the center", MaterialIcons.ZoomOut);
        Add("ZoomInNumPad", "NumPadAdd", ZoomIn, "Zoom in (numpad)", "Zoom in at the pointer, or the center", MaterialIcons.ZoomIn);
        Add("ZoomOutNumPad", "NumPadSubtract", ZoomOut, "Zoom out (numpad)", "Zoom out at the pointer, or the center", MaterialIcons.ZoomOut);
        Add("ZoomInX", "Ctrl+WheelUp", ZoomInX, "Zoom in horizontally", "Zoom in along the horizontal axis only", MaterialIcons.ZoomIn);
        Add("ZoomOutX", "Ctrl+WheelDown", ZoomOutX, "Zoom out horizontally", "Zoom out along the horizontal axis only", MaterialIcons.ZoomOut);
        Add("ZoomInY", "Shift+WheelUp", ZoomInY, "Zoom in vertically", "Zoom in along the vertical axis only", MaterialIcons.ZoomIn);
        Add("ZoomOutY", "Shift+WheelDown", ZoomOutY, "Zoom out vertically", "Zoom out along the vertical axis only", MaterialIcons.ZoomOut);
        Add("ResetView", "Home", ResetView, "_Reset view", "Fit the view to the data", MaterialIcons.FitScreen);
        Add("ResetViewClick", "DoubleClick", ResetView, "Reset view (double click)", "Fit the view to the data", MaterialIcons.FitScreen);
    }

    private static void Add(string name, string keybinding, AtelierCommand command, string label, string description, string icon)
    {
        foreach (var registered in KeybindingManager.GetKeybindings(XYChart.CommandGroup))
        {
            if (registered.Name == name) return;
        }
        KeybindingManager.RegisterKeybinding(new KeybindingDescriptor(name, XYChart.CommandGroup, keybinding, command,
            label: label, description: description, icon: icon));
    }
}

/// <summary>Fits an <see cref="XYChart"/>'s view to its data (<see cref="XYChart.ResetView"/>).</summary>
public sealed class XYChartResetCommand : AtelierCommand
{
    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is XYChart chart && (chart.IsZoomEnabled || chart.IsPanEnabled);

    /// <inheritdoc/>
    public override void Execute(object? parameter)
    {
        if (CanExecute(parameter)) ((XYChart)parameter!).ResetView();
    }
}

/// <summary>
/// Zooms an <see cref="XYChart"/> by a factor per axis around the point under the pointer (or the plot's center when the
/// pointer isn't over it), which stays put.
/// </summary>
/// <param name="factorX">The horizontal factor of the visible range (below 1 zooms in).</param>
/// <param name="factorY">The vertical factor of the visible range (below 1 zooms in).</param>
public sealed class XYChartZoomCommand(double factorX, double factorY) : AtelierCommand
{
    /// <summary>Gets the horizontal factor of the visible range.</summary>
    public double FactorX { get; } = factorX;

    /// <summary>Gets the vertical factor of the visible range.</summary>
    public double FactorY { get; } = factorY;

    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is XYChart { IsZoomEnabled: true };

    /// <inheritdoc/>
    public override void Execute(object? parameter)
    {
        if (parameter is not XYChart { IsZoomEnabled: true } chart) return;
        var plot = chart.PlotArea;
        var at = chart.PointerPosition is { } pointer && plot.Contains(pointer) ? pointer : plot.Center;
        chart.ZoomAt(chart.LocalToData(at), FactorX, FactorY);
    }
}

/// <summary>Pans an <see cref="XYChart"/> by dragging: the point under the pointer follows it; Escape moves back.</summary>
public sealed class XYChartPanCommand : DragCommand
{
    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is XYChart { IsPanEnabled: true };

    /// <inheritdoc/>
    public override IDragOperation? BeginDrag(DragStart start) =>
        start.Target is XYChart { IsPanEnabled: true } chart ? new Operation(chart, start.ScreenPosition) : null;

    private sealed class Operation : IDragOperation
    {
        private readonly XYChart _chart;
        private readonly Point _start;
        private readonly bool _wasAuto;
        private readonly (double X0, double X1, double Y0, double Y1) _view;
        private readonly double _unitsPerPixelX, _unitsPerPixelY;

        public Operation(XYChart chart, Point start)
        {
            _chart = chart;
            _start = start;
            _wasAuto = chart.IsAutoRange;
            _view = (chart.VisibleXMin, chart.VisibleXMax, chart.VisibleYMin, chart.VisibleYMax);
            var plot = chart.PlotArea;
            _unitsPerPixelX = (_view.X1 - _view.X0) / Math.Max(1, plot.Width);
            _unitsPerPixelY = (_view.Y1 - _view.Y0) / Math.Max(1, plot.Height);
        }

        public void Update(Point screenPosition, ModifierKeys modifiers)
        {
            var delta = screenPosition - _start;
            // Dragging right shows smaller values (the data moves with the pointer); dragging down shows larger ones.
            double dx = -delta.X * _unitsPerPixelX, dy = delta.Y * _unitsPerPixelY;
            _chart.ZoomTo(_view.X0 + dx, _view.X1 + dx, _view.Y0 + dy, _view.Y1 + dy);
        }

        public void Complete(Point screenPosition, ModifierKeys modifiers) => Update(screenPosition, modifiers);

        public void Cancel()
        {
            if (_wasAuto) _chart.ResetView();
            else _chart.ZoomTo(_view.X0, _view.X1, _view.Y0, _view.Y1);
        }
    }
}

/// <summary>Zooms an <see cref="XYChart"/> into the box dragged over its plot; Escape cancels.</summary>
public sealed class XYChartZoomBoxCommand : DragCommand
{
    /// <summary>The smallest box (in pixels, each side) that zooms; smaller ones are taken for a click.</summary>
    public const float MinimumSize = 4;

    /// <inheritdoc/>
    public override bool CanExecute(object? parameter) => parameter is XYChart { IsZoomEnabled: true };

    /// <inheritdoc/>
    public override IDragOperation? BeginDrag(DragStart start)
    {
        if (start.Target is not XYChart { IsZoomEnabled: true } chart) return null;
        var position = chart.PointToClient(start.ScreenPosition);
        return chart.PlotArea.Contains(position) ? new Operation(chart, start.ScreenPosition, position) : null;
    }

    private sealed class Operation(XYChart chart, Point screenStart, Point start) : IDragOperation
    {
        private Rect Box(Point screenPosition)
        {
            var plot = chart.PlotArea;
            var end = start + (screenPosition - screenStart);
            float x0 = Math.Clamp(Math.Min(start.X, end.X), plot.Left, plot.Right);
            float x1 = Math.Clamp(Math.Max(start.X, end.X), plot.Left, plot.Right);
            float y0 = Math.Clamp(Math.Min(start.Y, end.Y), plot.Top, plot.Bottom);
            float y1 = Math.Clamp(Math.Max(start.Y, end.Y), plot.Top, plot.Bottom);
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        public void Update(Point screenPosition, ModifierKeys modifiers) => chart.SetZoomBox(Box(screenPosition));

        public void Complete(Point screenPosition, ModifierKeys modifiers)
        {
            var box = Box(screenPosition);
            chart.SetZoomBox(null);
            if (box.Width < MinimumSize || box.Height < MinimumSize) return;
            var a = chart.LocalToData(new Point(box.Left, box.Bottom));
            var b = chart.LocalToData(new Point(box.Right, box.Top));
            chart.ZoomTo(a.X, b.X, a.Y, b.Y);
        }

        public void Cancel() => chart.SetZoomBox(null);
    }
}
