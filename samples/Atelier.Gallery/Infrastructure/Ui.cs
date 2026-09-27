using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Infrastructure;

/// <summary>
/// Building blocks shared by all gallery pages, so every page looks the same and only describes its demos:
/// sections, labeled demos, wrapping rows, adaptive columns, value readouts and code snippets.
/// </summary>
/// <remarks>
/// Controls get their sizes, shapes and colors from the theme's default styles; these helpers only arrange them.
/// </remarks>
public static class Ui
{
    /// <summary>The monospace font used for readouts and code.</summary>
    public const string MonospaceFont = "Consolas";

    /// <summary>
    /// A page section: an outlined card with a title, a description and its demos stacked below.
    /// </summary>
    public static Card Section(string title, string description, params UIElement[] content) =>
        new Card(CardVariant.Outlined)
            .Padding(24)
            .Child(new StackPanel().Spacing(20).Children(
                new StackPanel().Spacing(4).Children(
                    new TextBlock(title).TitleLarge(),
                    new TextBlock(description).BodyMedium().Muted().TextWrapping()),
                new StackPanel().Spacing(20).Children(content)));

    /// <summary>A labeled demo inside a section: a small heading above the demonstrated controls.</summary>
    public static StackPanel Demo(string title, params UIElement[] content) =>
        new StackPanel().Spacing(10).Children(
            new TextBlock(title).TitleSmall().Themed(TextBlock.ForegroundProperty, c => c.Primary))
            .Children(content);

    /// <summary>A row of controls that wraps onto new lines when the page is narrow.</summary>
    public static WrapPanel Row(params UIElement[] items)
    {
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i].VerticalAlignment == VerticalAlignment.Stretch)
            {
                items[i].VerticalAlignment = VerticalAlignment.Center;
            }
        }
        return new WrapPanel().Spacing(12, 12).Children(items);
    }

    /// <summary>A vertical stack of items with the standard gap.</summary>
    public static StackPanel Stack(params UIElement[] items) => new StackPanel().Spacing(12).Children(items);

    /// <summary>
    /// Items in as many equal columns as fit (at least <paramref name="minColumnWidth"/> wide each), for demos that
    /// should sit side by side on wide windows and stack on narrow ones.
    /// </summary>
    public static AdaptiveGrid Columns(float minColumnWidth, params UIElement[] items) =>
        new AdaptiveGrid { MinColumnWidth = minColumnWidth, MaxColumns = Math.Max(1, items.Length) }.Children(items);

    /// <summary>Explanatory text in the muted color, wrapped.</summary>
    public static TextBlock Note(string text) => new TextBlock(text).BodySmall().Muted().TextWrapping();

    /// <summary>
    /// A small chip that shows a live value, e.g. the state a binding wrote to the view model.
    /// </summary>
    public static Border Readout<TSource>(TSource source, Func<TSource, string> text,
        [CallerArgumentExpression(nameof(text))] string? textExpression = null)
        where TSource : class
    {
        var label = new TextBlock()
            .FontFamily(MonospaceFont)
            .FontSize(12)
            .Themed(TextBlock.ForegroundProperty, c => c.OnSecondaryContainer)
            .Bind(TextBlock.TextProperty, source, text, getterExpression: textExpression);
        return new Border()
            .Padding(10, 4)
            .CornerRadius(8)
            .HorizontalAlignment(HorizontalAlignment.Left)
            .VerticalAlignment(VerticalAlignment.Center)
            .Themed(Border.BackgroundProperty, c => c.SecondaryContainer)
            .Child(label);
    }

    /// <summary>A code snippet in a tinted box, showing how a demo is written.</summary>
    public static Border Code(string code) =>
        new Border()
            .Padding(14, 10)
            .CornerRadius(8)
            .Themed(Border.BackgroundProperty, c => c.SurfaceContainerHigh)
            .Child(new TextBlock(code)
                .FontFamily(MonospaceFont)
                .FontSize(12.5f)
                .LineHeight(18)
                .TextWrapping()
                .Themed(TextBlock.ForegroundProperty, c => c.OnSurfaceVariant));

    /// <summary>An icon on a round tonal background, e.g. for page headers.</summary>
    public static Border IconBadge(MaterialIconKind kind, float size = 48) =>
        new Border()
            .Size(size, size)
            .CornerRadius(size / 2)
            .Themed(Border.BackgroundProperty, c => c.PrimaryContainer)
            .Child(new Icon(kind, size * 0.5f)
                .Center()
                .Themed(Control.ForegroundProperty, c => c.OnPrimaryContainer));

    /// <summary>
    /// A labeled slider for playgrounds: the label shows the current value, and the slider edits it two-way.
    /// </summary>
    public static StackPanel SliderSetting<TSource>(string label, TSource source, Func<TSource, float> getter, Action<TSource, float> setter,
        float minimum, float maximum, string format = "0", float step = 0)
        where TSource : class, INotifyPropertyChanged
    {
        var slider = new Slider()
            .Range(minimum, maximum)
            .ValueFormat("{0:" + format + "}")
            .MinWidth(160)
            .BindValue(source, getter, setter);
        if (step > 0)
        {
            slider.SnapTo(step);
        }

        return new StackPanel().Spacing(2).Children(
            new TextBlock().LabelLarge().Bind(TextBlock.TextProperty, source, s => $"{label}: {getter(s).ToString(format)}"),
            slider);
    }

    /// <summary>A caption above a control, for settings and form-like layouts.</summary>
    public static StackPanel Labeled(string label, UIElement control) =>
        new StackPanel().Spacing(6).Children(new TextBlock(label).LabelLarge().Muted(), control);

    /// <summary>A button with an icon before its label.</summary>
    public static Button IconButton(MaterialIconKind icon, string text, ButtonVariant variant = ButtonVariant.Filled) =>
        new Button().Variant(variant).Content(new StackPanel().Orientation(Orientation.Horizontal).Spacing(8).Children(
            new Icon(icon, 18).VerticalAlignment(VerticalAlignment.Center),
            new TextBlock(text).VerticalAlignment(VerticalAlignment.Center)));
}
