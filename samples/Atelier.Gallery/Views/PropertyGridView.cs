using System;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.Models;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class PropertyGridView : GalleryPage
{
    private readonly PropertyGridViewModel _vm;

    public PropertyGridView(PropertyGridViewModel viewModel)
        : base(MaterialIconKind.Tune, "Property Grid",
            "The PropertyGrid lists and edits the properties of an object, grouped by category. Objects marked " +
            "[Inspectable] describe their properties at compile time, so no reflection is needed.")
    {
        _vm = viewModel;

        Settings(new Button("Reset").Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));

        Sections(InspectorSection(), CustomEditorSection());
    }

    private UIElement InspectorSection()
    {
        var grid = new PropertyGrid()
            .Height(560)
            .BindSelectedObject(_vm, v => v.SelectedObject)
            .BindTwoWay(PropertyGrid.SortModeProperty, _vm, v => v.SortMode, (v, mode) => v.SortMode = mode)
            .BindTwoWay(PropertyGrid.FilterTextProperty, _vm, v => v.FilterText, (v, text) => v.FilterText = text)
            .Bind(PropertyGrid.IsToolbarVisibleProperty, _vm, v => v.IsToolbarVisible)
            .Bind(PropertyGrid.IsDescriptionVisibleProperty, _vm, v => v.IsDescriptionVisible)
            .Bind(PropertyGrid.ToolbarElevationProperty, _vm, v => v.ToolbarElevation)
            .Bind(PropertyGrid.LabelWidthProperty, _vm, v => v.LabelWidth)
            .OnPropertyValueChanging((_, e) => Validate(e))
            .OnPropertyValueChanged((_, e) => _vm.Log($"Changed {e.Property.Name}: {e.OldValue ?? "null"} → {e.NewValue ?? "null"}"))
            .OnPropertyValueError((_, e) => _vm.Log($"Error in {e.Property.Name}: {e.Exception.Message}"))
            .OnSelectedObjectChanged((_, target) => _vm.Log($"Inspecting {target?.GetType().Name ?? "nothing"}"))
            .OnSortModeChanged((_, mode) => _vm.Log($"Sort mode: {mode}"));

        return Ui.Section("Inspecting objects",
            "Pick an object to inspect. Each property type gets a matching editor: text, numbers, switches, colors, " +
            "drop-downs for enums, check boxes for [Flags] enums and nullable values. Edits are validated and logged.",
            Ui.Columns(300,
                Ui.Labeled("Object", Ui.Row(
                    ObjectOption("Shape", InspectedObject.Shape),
                    ObjectOption("Service", InspectedObject.Service),
                    ObjectOption("Emitter", InspectedObject.Emitter))),
                Ui.Labeled("Sort mode", Ui.Row(
                    new RadioButton("Categorized").GroupName("pg-sort")
                        .BindIsChecked(_vm, v => v.SortMode, (v, m) => v.SortMode = m, PropertySortMode.Categorized),
                    new RadioButton("Alphabetical").GroupName("pg-sort")
                        .BindIsChecked(_vm, v => v.SortMode, (v, m) => v.SortMode = m, PropertySortMode.Alphabetical))),
                new TextBox()
                    .Label("Filter")
                    .LeadingIconKind(MaterialIconKind.FilterList)
                    .BindText(_vm, v => v.FilterText, (v, text) => v.FilterText = text)),
            Ui.Columns(220,
                Ui.SliderSetting("Label width", _vm, v => v.LabelWidth, (v, w) => v.LabelWidth = w, 100, 260, step: 10),
                Ui.SliderSetting("Toolbar elevation", _vm, v => v.ToolbarElevation, (v, e) => v.ToolbarElevation = e, 0, 8, step: 1),
                Ui.Stack(
                    new Switch("Toolbar").BindIsChecked(_vm, v => v.IsToolbarVisible, (v, on) => v.IsToolbarVisible = on),
                    new Switch("Description panel").BindIsChecked(_vm, v => v.IsDescriptionVisible, (v, on) => v.IsDescriptionVisible = on))),
            Ui.Row(
                new Button("Expand all").Variant(ButtonVariant.Outlined).OnClick(grid.ExpandAll),
                new Button("Collapse all").Variant(ButtonVariant.Outlined).OnClick(grid.CollapseAll),
                Ui.IconButton(MaterialIconKind.Shuffle, "Change shape in code", ButtonVariant.Tonal).Command(_vm.RandomizeShapeCommand)),
            Ui.Columns(360,
                grid,
                Ui.Stack(ShapePreview(), EventLog())));
    }

    private RadioButton ObjectOption(string text, InspectedObject value) =>
        new RadioButton(text).GroupName("pg-object").BindIsChecked(_vm, v => v.Inspected, (v, o) => v.Inspected = o, value);

    // PropertyValueChanging can reject an edit before it reaches the object; the editor then shows the old value again.
    private void Validate(PropertyValueChangingEventArgs e)
    {
        string? error = e.Property.Name switch
        {
            nameof(ServiceConfigModel.ServiceName) when string.IsNullOrWhiteSpace(e.NewValue as string) => "the name can't be empty",
            nameof(ServiceConfigModel.Port) when e.NewValue is int port && (port < 1 || port > 65535) => "the port must be 1 to 65535",
            _ => null,
        };

        if (error != null)
        {
            e.Cancel = true;
            _vm.Log($"Rejected {e.Property.Name}: {error}");
        }
    }

    private UIElement ShapePreview()
    {
        var shape = _vm.Shape;
        var preview = new Border()
            .Center()
            .Bind(UIElement.WidthProperty, shape, s => (float)s.Width)
            .Bind(UIElement.HeightProperty, shape, s => (float)s.Height)
            .Bind(Border.CornerRadiusProperty, shape, s => new CornerRadius(s.Kind switch
            {
                ShapeKind.Rectangle => 0,
                ShapeKind.Rounded => 16,
                _ => MathF.Min(s.Width, s.Height) / 2,
            }))
            .Bind(Border.BackgroundProperty, shape, s => s.Fill)
            .Bind(Border.BorderBrushProperty, shape, s => s.Stroke)
            .Bind(Border.BorderThicknessProperty, shape, s => new Thickness(s.StrokeWidth))
            .BindOpacity(shape, s => s.Opacity)
            .BindIsVisible(shape, s => s.IsVisible)
            .Child(new TextBlock().TitleSmall().Center().Foreground(Color.White).BindText(shape, s => s.Name));

        return Ui.Demo("Shape preview",
            new Border()
                .Height(280)
                .CornerRadius(12)
                .ClipToBounds()
                .Themed(Border.BackgroundProperty, c => c.SurfaceContainerLow)
                .Child(preview),
            Ui.Note("The shape implements INotifyPropertyChanged: the grid and the preview follow edits and code changes."));
    }

    private UIElement EventLog() =>
        Ui.Demo("Events",
            new Border()
                .Padding(12, 8)
                .MinHeight(120)
                .CornerRadius(8)
                .Themed(Border.BackgroundProperty, c => c.SurfaceContainerHigh)
                .Child(new ItemsControl()
                    .BindItemsSource(_vm, v => v.Events)
                    .WithItemTemplate((string entry) => new TextBlock(entry)
                        .FontFamily(Ui.MonospaceFont)
                        .FontSize(12)
                        .LineHeight(18)
                        .TextWrapping()
                        .Themed(TextBlock.ForegroundProperty, c => c.OnSurfaceVariant))),
            Ui.Note("Try an empty service name, port 0, or an emitter rate above 5000."));

    private UIElement CustomEditorSection() => Ui.Section("Custom editors",
        "RegisterCustomEditor replaces the editor for a property type, or for the properties a predicate selects. " +
        "Here every float is edited with a slider and the \"Rating\" property with stars.",
        Ui.Columns(360,
            new PropertyGrid()
                .Height(340)
                .IsToolbarVisible(false)
                .SelectedObject(_vm.Preferences)
                .RegisterCustomEditor<PropertyGrid, float>(SliderEditor)
                .RegisterCustomEditor(context => context.Descriptor.Name == nameof(PreferencesModel.Rating), RatingEditor)
                .OnPropertyValueChanged((_, e) => _vm.Log($"Preferences.{e.Property.Name} = {e.NewValue}")),
            Ui.Code(
                "grid.RegisterCustomEditor<PropertyGrid, float>(ctx =>\n" +
                "{\n" +
                "    var slider = new Slider()\n" +
                "        .Range(0, 1)\n" +
                "        .Value(ctx.GetValue<float>());\n" +
                "    slider.OnValueChanged(v => ctx.UpdateValue(v));\n" +
                "    ctx.ValueChanged += v => slider.Value = (float)v!;\n" +
                "    return slider;\n" +
                "});\n\n" +
                "grid.RegisterCustomEditor(\n" +
                "    ctx => ctx.Descriptor.Name == \"Rating\",\n" +
                "    RatingEditor);")));

    private static UIElement SliderEditor(PropertyEditorContext context)
    {
        var slider = new Slider()
            .Range(0, 1)
            .ValueFormat("{0:0.00}")
            .Value(context.GetValue<float>())
            .IsEnabled(!context.IsReadOnly);
        slider.OnValueChanged(value => context.UpdateValue(value));
        context.ValueChanged += value =>
        {
            if (value is float f)
            {
                slider.Value = f;
            }
        };
        return slider;
    }

    private static UIElement RatingEditor(PropertyEditorContext context)
    {
        var stars = new Icon[5];
        var row = new StackPanel().Orientation(Orientation.Horizontal).Spacing(2).VerticalAlignment(VerticalAlignment.Center);

        void Show(int rating)
        {
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].IsFilled(i < rating);
            }
        }

        for (int i = 0; i < stars.Length; i++)
        {
            int value = i + 1;
            stars[i] = new Icon(MaterialIconKind.Star, 22)
                .Themed(Control.ForegroundProperty, c => c.Tertiary)
                .OnPointerPressed((_, e) =>
                {
                    context.UpdateValue(value);
                    e.Handled = true;
                });
            row.Add(stars[i]);
        }

        Show(context.GetValue<int>());
        context.ValueChanged += value => Show(value is int rating ? rating : 0);
        return row;
    }
}
