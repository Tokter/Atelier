using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class RangeControlsView : GalleryPage
{
    private readonly RangeControlsViewModel _vm;

    public RangeControlsView(RangeControlsViewModel viewModel)
        : base(MaterialIconKind.Tune, "Sliders & Progress",
            "Sliders pick a value from a range by dragging or with the keyboard. Progress bars show how far an operation " +
            "has come, or that it is running when its length is unknown.")
    {
        _vm = viewModel;

        Settings(
            new Switch("Controls enabled").ShowThumbIcon().BindIsChecked(_vm, v => v.ControlsEnabled, (v, on) => v.ControlsEnabled = on),
            new Button().Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));

        SectionsPanel.BindIsEnabled(_vm, v => v.ControlsEnabled);

        Sections(SliderSection(), ProgressSection());
    }

    private UIElement SliderSection() => Ui.Section("Sliders",
        "Drag the thumb, click the track, or focus a slider and use the arrow keys (small step), Page Up/Down (large " +
        "step) and Home/End (minimum/maximum). While dragging, a bubble shows the value.",
        Ui.Columns(320,
            Ui.Demo("Basic",
                new Slider().BindValue(_vm, v => v.Volume, (v, value) => v.Volume = value),
                Ui.Readout(_vm, v => $"Value = {v.Volume:0.0}"),
                new Slider().Value(30).IsEnabled(false),
                Ui.Note("The second slider is disabled.")),

            Ui.Demo("Value indicator and format",
                new Slider()
                    .Range(-24, 12)
                    .ValueFormat("{0:+0.0;-0.0;0.0} dB")
                    .BindValue(_vm, v => v.Gain, (v, value) => v.Gain = value)
                    .OnValueChanged(value => _vm.LastValueChanged = $"ValueChanged → {value:0.00}"),
                Ui.Readout(_vm, v => v.LastValueChanged),
                new Slider().Value(70).ShowValueIndicator(false),
                Ui.Note("The first slider formats its bubble as decibels and reports ValueChanged; the second hides the bubble."))),

        Ui.Columns(320,
            Ui.Demo("Snapping to ticks",
                new Slider()
                    .Range(0, 5)
                    .TickFrequency(1)
                    .IsSnapToTickEnabled()
                    .ValueFormat("{0:0} ★")
                    .BindValue(_vm, v => v.Rating, (v, value) => v.Rating = value),
                Ui.Readout(_vm, v => $"Rating = {v.Rating:0} of 5"),
                new Slider()
                    .Range(16, 28)
                    .SnapTo(0.5f)
                    .ValueFormat("{0:0.0} °C")
                    .BindValue(_vm, v => v.Temperature, (v, value) => v.Temperature = value),
                Ui.Readout(_vm, v => $"Temperature = {v.Temperature:0.0} °C (steps of 0.5)")),

            Ui.Demo("Keyboard steps",
                new Slider()
                    .Range(0, 1000)
                    .SmallChange(5)
                    .LargeChange(100)
                    .Value(500),
                Ui.Note("SmallChange is 5 (arrow keys) and LargeChange is 100 (Page Up/Down) on a 0–1000 range. Values " +
                        "set from code or bindings are not snapped."))),

        Ui.Demo("Bound range",
            Ui.Columns(220,
                Ui.SliderSetting("Minimum", _vm, v => v.RangeMinimum, (v, value) => v.RangeMinimum = value, 0, 50),
                Ui.SliderSetting("Maximum", _vm, v => v.RangeMaximum, (v, value) => v.RangeMaximum = value, 50, 200),
                new StackPanel().Spacing(2).Children(
                    new TextBlock().LabelLarge().BindText(_vm, v => $"Value: {v.RangeValue:0} (range {v.RangeMinimum:0}–{v.RangeMaximum:0})"),
                    new Slider()
                        .BindMinimum(_vm, v => v.RangeMinimum)
                        .BindMaximum(_vm, v => v.RangeMaximum)
                        .BindValue(_vm, v => v.RangeValue, (v, value) => v.RangeValue = value))),
            Ui.Note("Minimum and Maximum are bound as well: the value is kept inside the range when it shrinks.")),

        Ui.Demo("Example: media controls",
            Ui.Columns(320,
                IconSlider(
                    new Icon().Size(24).BindKind(_vm, v => v.VolumeIcon),
                    new Slider().ValueFormat("{0:0}%").BindValue(_vm, v => v.Volume, (v, value) => v.Volume = value),
                    new Icon(MaterialIconKind.VolumeUp, 24)),
                IconSlider(
                    new Icon(MaterialIconKind.BrightnessLow, 24),
                    new Slider().Range(0, 1).ValueFormat("{0:P0}").BindValue(_vm, v => v.Brightness, (v, value) => v.Brightness = value),
                    new Icon(MaterialIconKind.BrightnessHigh, 24)))),

        Ui.Code("new Slider()\n" +
                "    .Range(16, 28)\n" +
                "    .SnapTo(0.5f)\n" +
                "    .ValueFormat(\"{0:0.0} °C\")\n" +
                "    .BindValue(vm, v => v.Temperature, (v, value) => v.Temperature = value)"));

    private static Grid IconSlider(Icon leading, Slider slider, Icon trailing) =>
        new Grid()
            .Columns(GridLength.Auto, GridLength.Star, GridLength.Auto)
            .ColumnSpacing(12)
            .Children(
                leading.VerticalAlignment(VerticalAlignment.Center).Themed(Control.ForegroundProperty, c => c.OnSurfaceVariant),
                slider.Column(1).VerticalAlignment(VerticalAlignment.Center),
                trailing.Column(2).VerticalAlignment(VerticalAlignment.Center).Themed(Control.ForegroundProperty, c => c.OnSurfaceVariant));

    private UIElement ProgressSection() => Ui.Section("Progress bars",
        "A determinate bar fills from Minimum to Maximum as the value grows. An indeterminate bar animates while work " +
        "of unknown length is running.",
        Ui.Columns(320,
            Ui.Demo("Determinate, bound to a slider",
                new ProgressBar().BindValue(_vm, v => v.Progress),
                new Slider().BindValue(_vm, v => v.Progress, (v, value) => v.Progress = value),
                Ui.Readout(_vm, v => $"Value = {v.Progress:0}%")),

            Ui.Demo("Custom range",
                new ProgressBar().Range(1, 5).BindValue(_vm, v => v.WizardStep),
                new Slider().Range(1, 5).SnapTo(1).BindValue(_vm, v => v.WizardStep, (v, value) => v.WizardStep = value),
                Ui.Readout(_vm, v => $"Step {v.WizardStep:0} of 5 (Minimum 1, Maximum 5)"))),

        Ui.Columns(280,
            Ui.Demo("Indeterminate",
                new ProgressBar().BindIsIndeterminate(_vm, v => v.IsBusy),
                new Switch("Busy").BindIsChecked(_vm, v => v.IsBusy, (v, on) => v.IsBusy = on),
                Ui.Note("While the length of the work is unknown the bar animates; switch it off to show the value again.")),

            Ui.Demo("Simulated download",
                new ProgressBar().BindValue(_vm, v => v.DownloadProgress),
                Ui.Readout(_vm, v => v.DownloadStatus),
                Ui.Row(
                    new Button().Command(_vm.StartDownloadCommand),
                    new Button().Variant(ButtonVariant.Outlined).Command(_vm.CancelDownloadCommand)),
                Ui.Note("A DispatcherTimer advances the value on the UI thread; the commands enable each other.")),

            Ui.Demo("Disabled",
                new ProgressBar().Value(60).IsEnabled(false),
                Ui.Note("Disabled bars are drawn in the disabled color."))),

        Ui.Code("new ProgressBar().BindValue(vm, v => v.DownloadProgress)\n" +
                "new ProgressBar().BindIsIndeterminate(vm, v => v.IsBusy)"));
}
