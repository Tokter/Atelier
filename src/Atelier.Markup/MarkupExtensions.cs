using System;
using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SkiaSharp;
using Atelier.Controls;
using Atelier.Core.Animation;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Styling;
using Atelier.Core.Tree;
using Atelier.Core.ViewResolution;
using Atelier.Layout;

namespace Atelier.Markup;

public static class MarkupExtensions
{
    public static T Width<T>(this T element, float width) where T : UIElement
    {
        element.Width = width;
        return element;
    }

    public static T Height<T>(this T element, float height) where T : UIElement
    {
        element.Height = height;
        return element;
    }

    public static T Size<T>(this T element, float width, float height) where T : UIElement
    {
        element.Width = width;
        element.Height = height;
        return element;
    }

    public static T MinWidth<T>(this T element, float minWidth) where T : UIElement
    {
        element.MinWidth = minWidth;
        return element;
    }

    public static T MaxWidth<T>(this T element, float maxWidth) where T : UIElement
    {
        element.MaxWidth = maxWidth;
        return element;
    }

    public static T MinHeight<T>(this T element, float minHeight) where T : UIElement
    {
        element.MinHeight = minHeight;
        return element;
    }

    public static T MaxHeight<T>(this T element, float maxHeight) where T : UIElement
    {
        element.MaxHeight = maxHeight;
        return element;
    }

    public static T Margin<T>(this T element, float uniform) where T : UIElement
    {
        element.Margin = new Thickness(uniform);
        return element;
    }

    public static T Margin<T>(this T element, float horizontal, float vertical) where T : UIElement
    {
        element.Margin = new Thickness(horizontal, vertical);
        return element;
    }

    public static T Margin<T>(this T element, float left, float top, float right, float bottom) where T : UIElement
    {
        element.Margin = new Thickness(left, top, right, bottom);
        return element;
    }

    public static T Padding<T>(this T control, float uniform) where T : Control
    {
        control.Padding = new Thickness(uniform);
        return control;
    }

    public static T Padding<T>(this T control, float horizontal, float vertical) where T : Control
    {
        control.Padding = new Thickness(horizontal, vertical);
        return control;
    }

    public static T CornerRadius<T>(this T control, float uniform) where T : Control
    {
        control.CornerRadius = new CornerRadius(uniform);
        return control;
    }

    public static T Align<T>(this T element, HorizontalAlignment horizontal, VerticalAlignment vertical) where T : UIElement
    {
        element.HorizontalAlignment = horizontal;
        element.VerticalAlignment = vertical;
        return element;
    }

    public static T HorizontalAlign<T>(this T element, HorizontalAlignment horizontal) where T : UIElement
    {
        element.HorizontalAlignment = horizontal;
        return element;
    }

    public static T VerticalAlign<T>(this T element, VerticalAlignment vertical) where T : UIElement
    {
        element.VerticalAlignment = vertical;
        return element;
    }

    public static T Opacity<T>(this T element, float opacity) where T : UIElement
    {
        element.Opacity = opacity;
        return element;
    }

    public static T IsEnabled<T>(this T element, bool isEnabled) where T : UIElement
    {
        element.IsEnabled = isEnabled;
        return element;
    }

    public static T BindIsEnabled<T, TSource>(
        this T element,
        TSource source,
        Func<TSource, bool> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : UIElement
        where TSource : class
    {
        element.SetBinding(UIElement.IsEnabledProperty, source, getter, getterExpression: getterExpression);
        return element;
    }

    public static T BindIsEnabled<T, TDataContext>(
        this T element,
        Func<TDataContext, bool> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : UIElement
        where TDataContext : class
    {
        element.SetBinding(UIElement.IsEnabledProperty, getter, getterExpression: getterExpression);
        return element;
    }

    public static T ClipToBounds<T>(this T element, bool clip = true) where T : UIElement
    {
        element.ClipToBounds = clip;
        return element;
    }

    public static T Row<T>(this T element, int row) where T : UIElement
    {
        Grid.SetRow(element, row);
        return element;
    }

    public static T Column<T>(this T element, int column) where T : UIElement
    {
        Grid.SetColumn(element, column);
        return element;
    }

    public static T RowSpan<T>(this T element, int span) where T : UIElement
    {
        Grid.SetRowSpan(element, span);
        return element;
    }

    public static T ColumnSpan<T>(this T element, int span) where T : UIElement
    {
        Grid.SetColumnSpan(element, span);
        return element;
    }

    public static T Dock<T>(this T element, Dock dock) where T : UIElement
    {
        DockPanel.SetDock(element, dock);
        return element;
    }

    public static T Children<T>(this T panel, params UIElement[] children) where T : Panel
    {
        for (int i = 0; i < children.Length; i++)
        {
            panel.Add(children[i]);
        }
        return panel;
    }

    public static StackPanel Spacing(this StackPanel panel, float spacing)
    {
        panel.Spacing = spacing;
        return panel;
    }

    public static StackPanel Orientation(this StackPanel panel, Orientation orientation)
    {
        panel.Orientation = orientation;
        return panel;
    }

    public static WrapPanel Orientation(this WrapPanel panel, Orientation orientation)
    {
        panel.Orientation = orientation;
        return panel;
    }

    public static WrapPanel ItemWidth(this WrapPanel panel, float itemWidth)
    {
        panel.ItemWidth = itemWidth;
        return panel;
    }

    public static WrapPanel ItemHeight(this WrapPanel panel, float itemHeight)
    {
        panel.ItemHeight = itemHeight;
        return panel;
    }

    public static WrapPanel HorizontalSpacing(this WrapPanel panel, float spacing)
    {
        panel.HorizontalSpacing = spacing;
        return panel;
    }

    public static WrapPanel VerticalSpacing(this WrapPanel panel, float spacing)
    {
        panel.VerticalSpacing = spacing;
        return panel;
    }

    public static WrapPanel Spacing(this WrapPanel panel, float horizontal, float vertical)
    {
        panel.HorizontalSpacing = horizontal;
        panel.VerticalSpacing = vertical;
        return panel;
    }

    public static WrapPanel Spacing(this WrapPanel panel, float spacing)
    {
        panel.HorizontalSpacing = spacing;
        panel.VerticalSpacing = spacing;
        return panel;
    }

    public static Grid Rows(this Grid grid, params GridLength[] rows)
    {
        for (int i = 0; i < rows.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(rows[i]));
        }
        return grid;
    }

    public static Grid Columns(this Grid grid, params GridLength[] columns)
    {
        for (int i = 0; i < columns.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(columns[i]));
        }
        return grid;
    }

    public static Grid RowSpacing(this Grid grid, float spacing)
    {
        grid.RowSpacing = spacing;
        return grid;
    }

    public static Grid ColumnSpacing(this Grid grid, float spacing)
    {
        grid.ColumnSpacing = spacing;
        return grid;
    }

    public static Grid Spacing(this Grid grid, float rowSpacing, float colSpacing)
    {
        grid.RowSpacing = rowSpacing;
        grid.ColumnSpacing = colSpacing;
        return grid;
    }

    public static Grid Spacing(this Grid grid, float spacing)
    {
        grid.RowSpacing = spacing;
        grid.ColumnSpacing = spacing;
        return grid;
    }

    public static Border Child(this Border border, UIElement child)
    {
        border.Child = child;
        return border;
    }

    public static Card Child(this Card card, UIElement child)
    {
        card.Child = child;
        return card;
    }

    public static Card Variant(this Card card, CardVariant variant)
    {
        card.Variant = variant;
        return card;
    }

    public static Card BindVariant<TSource>(
        this Card card,
        TSource source,
        Func<TSource, CardVariant> getter,
        Action<TSource, CardVariant>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        card.SetBinding(Card.VariantProperty, source, getter, setter, getterExpression: getterExpression);
        return card;
    }

    public static Border Elevation(this Border border, float elevation)
    {
        border.Elevation = elevation;
        return border;
    }

    public static Card Elevation(this Card card, float elevation)
    {
        card.Elevation = elevation;
        return card;
    }

    public static Border BindElevation<TSource>(
        this Border border,
        TSource source,
        Func<TSource, float> getter,
        Action<TSource, float>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        border.SetBinding(Border.ElevationProperty, source, getter, setter, getterExpression: getterExpression);
        return border;
    }

    public static Card BindElevation<TSource>(
        this Card card,
        TSource source,
        Func<TSource, float> getter,
        Action<TSource, float>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        card.SetBinding(Border.ElevationProperty, source, getter, setter, getterExpression: getterExpression);
        return card;
    }

    public static Border CornerRadius(this Border border, CornerRadius cornerRadius)
    {
        border.CornerRadius = cornerRadius;
        return border;
    }

    public static Card CornerRadius(this Card card, CornerRadius cornerRadius)
    {
        card.CornerRadius = cornerRadius;
        return card;
    }

    public static Border CornerRadius(this Border border, float uniformRadius)
    {
        border.CornerRadius = new CornerRadius(uniformRadius);
        return border;
    }

    public static Card CornerRadius(this Card card, float uniformRadius)
    {
        card.CornerRadius = new CornerRadius(uniformRadius);
        return card;
    }

    public static Border BindCornerRadius<TSource>(
        this Border border,
        TSource source,
        Func<TSource, float> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        border.SetBinding(Border.CornerRadiusProperty, source, s => new CornerRadius(getter(s)), getterExpression: getterExpression);
        return border;
    }

    public static Card BindCornerRadius<TSource>(
        this Card card,
        TSource source,
        Func<TSource, float> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        card.SetBinding(Border.CornerRadiusProperty, source, s => new CornerRadius(getter(s)), getterExpression: getterExpression);
        return card;
    }

    public static Border Padding(this Border border, Thickness padding)
    {
        border.Padding = padding;
        return border;
    }

    public static Card Padding(this Card card, Thickness padding)
    {
        card.Padding = padding;
        return card;
    }

    public static Border Padding(this Border border, float horizontal, float vertical)
    {
        border.Padding = new Thickness(horizontal, vertical);
        return border;
    }

    public static Card Padding(this Card card, float horizontal, float vertical)
    {
        card.Padding = new Thickness(horizontal, vertical);
        return card;
    }

    public static Border Padding(this Border border, float uniform)
    {
        border.Padding = new Thickness(uniform);
        return border;
    }

    public static Card Padding(this Card card, float uniform)
    {
        card.Padding = new Thickness(uniform);
        return card;
    }

    public static Border BindPadding<TSource>(
        this Border border,
        TSource source,
        Func<TSource, float> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        border.SetBinding(Border.PaddingProperty, source, s => new Thickness(getter(s)), getterExpression: getterExpression);
        return border;
    }

    public static Card BindPadding<TSource>(
        this Card card,
        TSource source,
        Func<TSource, float> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        card.SetBinding(Border.PaddingProperty, source, s => new Thickness(getter(s)), getterExpression: getterExpression);
        return card;
    }

    public static Border Background(this Border border, Color background)
    {
        border.Background = background;
        return border;
    }

    public static Card Background(this Card card, Color background)
    {
        card.Background = background;
        return card;
    }

    public static T Content<T>(this T control, object content) where T : ContentControl
    {
        control.Content = content;
        return control;
    }

    public static Button Variant(this Button button, ButtonVariant variant)
    {
        button.Variant = variant;
        return button;
    }

    public static Button BindVariant<TSource>(
        this Button button,
        TSource source,
        Func<TSource, ButtonVariant> getter,
        Action<TSource, ButtonVariant>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        button.SetBinding(Button.VariantProperty, source, getter, setter, getterExpression: getterExpression);
        return button;
    }

    public static Button OnClick(this Button button, Action action)
    {
        button.Click += (s, e) => action();
        return button;
    }

    public static Button Command(this Button button, ICommand command, object? parameter = null)
    {
        button.Command = command;
        button.CommandParameter = parameter;
        return button;
    }

    public static TextBlock Text(this TextBlock textBlock, string text)
    {
        textBlock.Text = text;
        return textBlock;
    }

    public static TextBlock FontSize(this TextBlock textBlock, float size)
    {
        textBlock.FontSize = size;
        return textBlock;
    }

    public static TextBlock Bold(this TextBlock textBlock, bool bold = true)
    {
        textBlock.Bold = bold;
        return textBlock;
    }

    public static TextBlock TextWrapping(this TextBlock textBlock, Atelier.Controls.TextWrapping wrapping = Atelier.Controls.TextWrapping.Wrap)
    {
        textBlock.TextWrapping = wrapping;
        return textBlock;
    }

    public static TextBlock TextAlignment(this TextBlock textBlock, Atelier.Controls.TextAlignment alignment)
    {
        textBlock.TextAlignment = alignment;
        return textBlock;
    }

    public static TextBlock Foreground(this TextBlock textBlock, Color color)
    {
        textBlock.Foreground = color;
        return textBlock;
    }

    public static TextBlock Muted(this TextBlock textBlock, bool muted = true)
    {
        textBlock.Muted = muted;
        return textBlock;
    }

    public static TextBlock Italic(this TextBlock textBlock, bool italic = true)
    {
        textBlock.Italic = italic;
        return textBlock;
    }

    public static TextBlock FontFamily(this TextBlock textBlock, string fontFamily)
    {
        textBlock.FontFamily = fontFamily;
        return textBlock;
    }

    public static TextBlock BindItalic<TSource>(
        this TextBlock textBlock,
        TSource source,
        Func<TSource, bool> getter,
        Action<TSource, bool>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        textBlock.SetBinding(TextBlock.ItalicProperty, source, getter, setter, getterExpression: getterExpression);
        return textBlock;
    }

    public static TextBlock BindFontFamily<TSource>(
        this TextBlock textBlock,
        TSource source,
        Func<TSource, string?> getter,
        Action<TSource, string?>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        textBlock.SetBinding(TextBlock.FontFamilyProperty, source, getter, setter, getterExpression: getterExpression);
        return textBlock;
    }

    public static TextBlock BindText<TSource>(
        this TextBlock textBlock,
        TSource source,
        Func<TSource, string> getter,
        Action<TSource, string>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        textBlock.SetBinding(TextBlock.TextProperty, source, getter, setter, getterExpression: getterExpression);
        return textBlock;
    }

    public static TextBlock BindForeground<TSource>(
        this TextBlock textBlock,
        TSource source,
        Func<TSource, Color> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        textBlock.SetBinding(TextBlock.ForegroundProperty, source, getter, getterExpression: getterExpression);
        return textBlock;
    }

    public static TextBlock BindFontSize<TSource>(
        this TextBlock textBlock,
        TSource source,
        Func<TSource, float> getter,
        Action<TSource, float>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        textBlock.SetBinding(TextBlock.FontSizeProperty, source, getter, setter, getterExpression: getterExpression);
        return textBlock;
    }

    public static TextBlock BindBold<TSource>(
        this TextBlock textBlock,
        TSource source,
        Func<TSource, bool> getter,
        Action<TSource, bool>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        textBlock.SetBinding(TextBlock.BoldProperty, source, getter, setter, getterExpression: getterExpression);
        return textBlock;
    }

    public static TextBlock BindMuted<TSource>(
        this TextBlock textBlock,
        TSource source,
        Func<TSource, bool> getter,
        Action<TSource, bool>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        textBlock.SetBinding(TextBlock.MutedProperty, source, getter, setter, getterExpression: getterExpression);
        return textBlock;
    }

    public static TextBlock BindTextAlignment<TSource>(
        this TextBlock textBlock,
        TSource source,
        Func<TSource, TextAlignment> getter,
        Action<TSource, TextAlignment>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        textBlock.SetBinding(TextBlock.TextAlignmentProperty, source, getter, setter, getterExpression: getterExpression);
        return textBlock;
    }

    public static TextBlock BindTextWrapping<TSource>(
        this TextBlock textBlock,
        TSource source,
        Func<TSource, TextWrapping> getter,
        Action<TSource, TextWrapping>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        textBlock.SetBinding(TextBlock.TextWrappingProperty, source, getter, setter, getterExpression: getterExpression);
        return textBlock;
    }

    #region Material Typography Extensions

    public static TextBlock DisplayLarge(this TextBlock textBlock) => textBlock.StyleKey("DisplayLarge");
    public static TextBlock DisplayMedium(this TextBlock textBlock) => textBlock.StyleKey("DisplayMedium");
    public static TextBlock DisplaySmall(this TextBlock textBlock) => textBlock.StyleKey("DisplaySmall");

    public static TextBlock HeadlineLarge(this TextBlock textBlock) => textBlock.StyleKey("HeadlineLarge");
    public static TextBlock HeadlineMedium(this TextBlock textBlock) => textBlock.StyleKey("HeadlineMedium");
    public static TextBlock HeadlineSmall(this TextBlock textBlock) => textBlock.StyleKey("HeadlineSmall");

    public static TextBlock TitleLarge(this TextBlock textBlock) => textBlock.StyleKey("TitleLarge");
    public static TextBlock TitleMedium(this TextBlock textBlock) => textBlock.StyleKey("TitleMedium");
    public static TextBlock TitleSmall(this TextBlock textBlock) => textBlock.StyleKey("TitleSmall");

    public static TextBlock BodyLarge(this TextBlock textBlock) => textBlock.StyleKey("BodyLarge");
    public static TextBlock BodyMedium(this TextBlock textBlock) => textBlock.StyleKey("BodyMedium");
    public static TextBlock BodySmall(this TextBlock textBlock) => textBlock.StyleKey("BodySmall");

    public static TextBlock LabelLarge(this TextBlock textBlock) => textBlock.StyleKey("LabelLarge");
    public static TextBlock LabelMedium(this TextBlock textBlock) => textBlock.StyleKey("LabelMedium");
    public static TextBlock LabelSmall(this TextBlock textBlock) => textBlock.StyleKey("LabelSmall");

    // Common Aliases
    public static TextBlock Heading1(this TextBlock textBlock) => textBlock.StyleKey("Heading1");
    public static TextBlock Heading2(this TextBlock textBlock) => textBlock.StyleKey("Heading2");
    public static TextBlock Heading3(this TextBlock textBlock) => textBlock.StyleKey("Heading3");
    public static TextBlock NormalText(this TextBlock textBlock) => textBlock.StyleKey("NormalText");
    public static TextBlock Subtext(this TextBlock textBlock) => textBlock.StyleKey("Subtext");
    public static TextBlock Caption(this TextBlock textBlock) => textBlock.StyleKey("Caption");

    #endregion

    public static T Style<T>(this T element, Style style) where T : UIElement
    {
        element.Style = style;
        return element;
    }

    public static T StyleKey<T>(this T element, string styleKey) where T : UIElement
    {
        element.StyleKey = styleKey;
        return element;
    }

    public static T BindStyleKey<T, TSource>(
        this T element,
        TSource source,
        Func<TSource, string?> getter,
        Action<TSource, string?>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : UIElement
        where TSource : class
    {
        element.SetBinding(UIElement.StyleKeyProperty, source, getter, setter, getterExpression: getterExpression);
        return element;
    }

    public static T Styles<T>(this T element, params Style[] styles) where T : UIElement
    {
        element.Styles.AddRange(styles);
        return element;
    }

    public static T Bind<T, TProp, TSource>(
        this T target,
        BindableProperty<TProp> property,
        TSource source,
        Func<TSource, TProp> getter,
        Action<TSource, TProp>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : BindableObject
        where TSource : class
    {
        target.SetBinding(property, source, getter, setter, getterExpression: getterExpression);
        return target;
    }

    public static T BindTwoWay<T, TProp, TSource>(
        this T target,
        BindableProperty<TProp> property,
        TSource source,
        Func<TSource, TProp> getter,
        Action<TSource, TProp> setter,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : BindableObject
        where TSource : class
    {
        target.SetBinding(property, source, getter, setter, updateSourceTrigger, getterExpression: getterExpression);
        return target;
    }

    public static T Bind<T, TProp, TDataContext>(
        this T target,
        BindableProperty<TProp> property,
        Func<TDataContext, TProp> getter)
        where T : BindableObject
        where TDataContext : class
    {
        target.SetBinding<TProp, TDataContext>(property, getter);
        return target;
    }

    public static T BindTwoWay<T, TProp, TDataContext>(
        this T target,
        BindableProperty<TProp> property,
        Func<TDataContext, TProp> getter,
        Action<TDataContext, TProp> setter,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : BindableObject
        where TDataContext : class
    {
        target.SetBinding(property, getter, setter, updateSourceTrigger, getterExpression: getterExpression);
        return target;
    }

    public static TextBlock BindText<TDataContext>(
        this TextBlock textBlock,
        Func<TDataContext, string> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TDataContext : class
    {
        textBlock.SetBinding(TextBlock.TextProperty, getter, getterExpression: getterExpression);
        return textBlock;
    }

    public static TextBox BindText<TSource>(
        this TextBox textBox,
        TSource source,
        Func<TSource, string> getter,
        Action<TSource, string>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        textBox.SetBinding(TextBox.TextProperty, source, getter, setter, updateSourceTrigger, getterExpression: getterExpression);
        return textBox;
    }

    public static TextBox BindText<TDataContext>(
        this TextBox textBox,
        Func<TDataContext, string> getter,
        Action<TDataContext, string>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TDataContext : class
    {
        textBox.SetBinding(TextBox.TextProperty, getter, setter, updateSourceTrigger, getterExpression: getterExpression);
        return textBox;
    }

    public static Slider BindValue<TSource>(
        this Slider slider,
        TSource source,
        Func<TSource, float> getter,
        Action<TSource, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        slider.SetBinding(Slider.ValueProperty, source, getter, setter, updateSourceTrigger, getterExpression: getterExpression);
        return slider;
    }

    public static Slider BindValue<TDataContext>(
        this Slider slider,
        Func<TDataContext, float> getter,
        Action<TDataContext, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TDataContext : class
    {
        slider.SetBinding(Slider.ValueProperty, getter, setter, updateSourceTrigger, getterExpression: getterExpression);
        return slider;
    }

    public static CheckBox IsChecked(this CheckBox checkBox, bool isChecked)
    {
        checkBox.IsChecked = isChecked;
        return checkBox;
    }

    public static CheckBox Content(this CheckBox checkBox, object? content)
    {
        checkBox.Content = content;
        return checkBox;
    }

    public static CheckBox BindIsChecked<TSource>(
        this CheckBox checkBox,
        TSource source,
        Func<TSource, bool> getter,
        Action<TSource, bool>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        checkBox.SetBinding(CheckBox.IsCheckedProperty, source, getter, setter, getterExpression: getterExpression);
        return checkBox;
    }

    public static CheckBox BindIsChecked<TDataContext>(
        this CheckBox checkBox,
        Func<TDataContext, bool> getter,
        Action<TDataContext, bool>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TDataContext : class
    {
        checkBox.SetBinding(CheckBox.IsCheckedProperty, getter, setter, getterExpression: getterExpression);
        return checkBox;
    }

    public static RadioButton GroupName(this RadioButton radioButton, string? groupName)
    {
        radioButton.GroupName = groupName;
        return radioButton;
    }

    public static RadioButton IsChecked(this RadioButton radioButton, bool isChecked)
    {
        radioButton.IsChecked = isChecked;
        return radioButton;
    }

    public static RadioButton BindIsChecked<TSource, TValue>(
        this RadioButton radioButton,
        TSource source,
        Func<TSource, TValue> getter,
        Action<TSource, TValue> setter,
        TValue valueToMatch,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        radioButton.SetBinding(
            CheckBox.IsCheckedProperty,
            source,
            s => EqualityComparer<TValue>.Default.Equals(getter(s), valueToMatch),
            (s, isChecked) =>
            {
                if (isChecked) setter(s, valueToMatch);
            },
            getterExpression: getterExpression);
        return radioButton;
    }

    public static RadioButton BindIsChecked<TDataContext, TValue>(
        this RadioButton radioButton,
        Func<TDataContext, TValue> getter,
        Action<TDataContext, TValue> setter,
        TValue valueToMatch,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TDataContext : class
    {
        radioButton.SetBinding(
            CheckBox.IsCheckedProperty,
            (TDataContext s) => EqualityComparer<TValue>.Default.Equals(getter(s), valueToMatch),
            (TDataContext s, bool isChecked) =>
            {
                if (isChecked) setter(s, valueToMatch);
            },
            getterExpression: getterExpression);
        return radioButton;
    }

    public static Switch IsChecked(this Switch switchControl, bool isChecked)
    {
        switchControl.IsChecked = isChecked;
        return switchControl;
    }

    public static Switch ShowThumbIcon(this Switch switchControl, bool show = true)
    {
        switchControl.ShowThumbIcon = show;
        return switchControl;
    }

    public static Switch Content(this Switch switchControl, object? content)
    {
        switchControl.Content = content;
        return switchControl;
    }

    public static Switch BindIsChecked<TSource>(
        this Switch switchControl,
        TSource source,
        Func<TSource, bool> getter,
        Action<TSource, bool>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        switchControl.SetBinding(Switch.IsCheckedProperty, source, getter, setter, getterExpression: getterExpression);
        return switchControl;
    }

    public static Switch BindIsChecked<TDataContext>(
        this Switch switchControl,
        Func<TDataContext, bool> getter,
        Action<TDataContext, bool>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TDataContext : class
    {
        switchControl.SetBinding(Switch.IsCheckedProperty, getter, setter, getterExpression: getterExpression);
        return switchControl;
    }

    // TextBox Extensions
    public static TextBox Text(this TextBox textBox, string text)
    {
        textBox.Text = text;
        return textBox;
    }

    public static TextBox Placeholder(this TextBox textBox, string placeholder)
    {
        textBox.Placeholder = placeholder;
        return textBox;
    }

    public static TextBox Variant(this TextBox textBox, TextBoxVariant variant)
    {
        textBox.Variant = variant;
        return textBox;
    }

    public static TextBox Label(this TextBox textBox, string label)
    {
        textBox.Label = label;
        return textBox;
    }

    public static TextBox LeadingIcon(this TextBox textBox, MaterialIconKind iconKind)
    {
        textBox.LeadingIconKind = iconKind;
        return textBox;
    }

    public static TextBox SupportingText(this TextBox textBox, string supportingText)
    {
        textBox.SupportingText = supportingText;
        return textBox;
    }

    public static TextBox IsReadOnly(this TextBox textBox, bool isReadOnly = true)
    {
        textBox.IsReadOnly = isReadOnly;
        return textBox;
    }

    public static TextBox BindLabel<TSource>(
        this TextBox textBox,
        TSource source,
        Func<TSource, string> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        textBox.SetBinding(TextBox.LabelProperty, source, getter, getterExpression: getterExpression);
        return textBox;
    }

    public static TextBox BindSupportingText<TSource>(
        this TextBox textBox,
        TSource source,
        Func<TSource, string> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        textBox.SetBinding(TextBox.SupportingTextProperty, source, getter, getterExpression: getterExpression);
        return textBox;
    }

    public static Slider Minimum(this Slider slider, float min)
    {
        slider.Minimum = min;
        return slider;
    }

    public static Slider Maximum(this Slider slider, float max)
    {
        slider.Maximum = max;
        return slider;
    }

    public static Slider Value(this Slider slider, float val)
    {
        slider.Value = val;
        return slider;
    }

    public static Slider ShowValueIndicator(this Slider slider, bool show = true)
    {
        slider.ShowValueIndicator = show;
        return slider;
    }

    public static Slider ValueFormat(this Slider slider, string format)
    {
        slider.ValueFormat = format;
        return slider;
    }

    // ComboBox Extensions
    public static ComboBox ItemsSource(this ComboBox comboBox, System.Collections.IEnumerable? source)
    {
        comboBox.ItemsSource = source;
        return comboBox;
    }

    public static ComboBox Items(this ComboBox comboBox, params object[] items)
    {
        foreach (var item in items) comboBox.Items.Add(item);
        return comboBox;
    }

    public static ComboBox ItemTemplate(this ComboBox comboBox, Func<object, UIElement> template)
    {
        comboBox.ItemTemplate = template;
        return comboBox;
    }

    public static ComboBox ItemTemplate<T>(this ComboBox comboBox, Func<T, UIElement> template)
    {
        comboBox.ItemTemplate = obj => obj is T typed ? template(typed) : new TextBlock(obj?.ToString() ?? string.Empty);
        return comboBox;
    }

    public static ComboBox WithItemTemplate(this ComboBox comboBox, Func<object, UIElement> template)
    {
        comboBox.ItemTemplate = template;
        return comboBox;
    }

    public static ComboBox WithItemTemplate<T>(this ComboBox comboBox, Func<T, UIElement> template)
    {
        comboBox.ItemTemplate = obj => obj is T typed ? template(typed) : new TextBlock(obj?.ToString() ?? string.Empty);
        return comboBox;
    }

    public static ComboBox SelectedItem(this ComboBox comboBox, object? item)
    {
        comboBox.SelectedItem = item;
        return comboBox;
    }

    public static ComboBox SelectedIndex(this ComboBox comboBox, int index)
    {
        comboBox.SelectedIndex = index;
        return comboBox;
    }

    public static ComboBox Placeholder(this ComboBox comboBox, string placeholder)
    {
        comboBox.Placeholder = placeholder;
        return comboBox;
    }

    public static ComboBox MaxDropDownHeight(this ComboBox comboBox, float height)
    {
        comboBox.MaxDropDownHeight = height;
        return comboBox;
    }

    public static ComboBox BindSelectedItem<TSource>(
        this ComboBox comboBox,
        TSource source,
        Func<TSource, object?> getter,
        Action<TSource, object?>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        comboBox.SetBinding(ComboBox.SelectedItemProperty, source, getter, setter, getterExpression: getterExpression);
        return comboBox;
    }

    public static ComboBox BindSelectedIndex<TSource>(
        this ComboBox comboBox,
        TSource source,
        Func<TSource, int> getter,
        Action<TSource, int>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        comboBox.SetBinding(ComboBox.SelectedIndexProperty, source, getter, setter, getterExpression: getterExpression);
        return comboBox;
    }

    #region ListBox Extensions

    /// <summary>
    /// Sets the items source collection for the <see cref="ListBox"/>.
    /// </summary>
    public static ListBox ItemsSource(this ListBox listBox, System.Collections.IEnumerable? source)
    {
        listBox.ItemsSource = source;
        return listBox;
    }

    /// <summary>
    /// Adds items to the <see cref="ListBox"/>.
    /// </summary>
    public static ListBox Items(this ListBox listBox, params object[] items)
    {
        foreach (var item in items) listBox.Items.Add(item);
        return listBox;
    }

    /// <summary>
    /// Sets the item template delegate for the <see cref="ListBox"/>.
    /// </summary>
    public static ListBox ItemTemplate(this ListBox listBox, Func<object, UIElement> template)
    {
        listBox.ItemTemplate = template;
        return listBox;
    }

    /// <summary>
    /// Sets a strongly-typed item template delegate for the <see cref="ListBox"/>.
    /// </summary>
    public static ListBox ItemTemplate<T>(this ListBox listBox, Func<T, UIElement> template)
    {
        listBox.ItemTemplate = obj => obj is T typed ? template(typed) : new TextBlock(obj?.ToString() ?? string.Empty);
        return listBox;
    }

    /// <summary>
    /// Sets the item template delegate for the <see cref="ListBox"/>.
    /// </summary>
    public static ListBox WithItemTemplate(this ListBox listBox, Func<object, UIElement> template)
    {
        listBox.ItemTemplate = template;
        return listBox;
    }

    /// <summary>
    /// Sets a strongly-typed item template delegate for the <see cref="ListBox"/>.
    /// </summary>
    public static ListBox WithItemTemplate<T>(this ListBox listBox, Func<T, UIElement> template)
    {
        listBox.ItemTemplate = obj => obj is T typed ? template(typed) : new TextBlock(obj?.ToString() ?? string.Empty);
        return listBox;
    }

    /// <summary>
    /// Sets the currently selected item of the <see cref="ListBox"/>.
    /// </summary>
    public static ListBox SelectedItem(this ListBox listBox, object? item)
    {
        listBox.SelectedItem = item;
        return listBox;
    }

    /// <summary>
    /// Sets the currently selected index of the <see cref="ListBox"/>.
    /// </summary>
    public static ListBox SelectedIndex(this ListBox listBox, int index)
    {
        listBox.SelectedIndex = index;
        return listBox;
    }

    /// <summary>
    /// Adds an action handler for the <see cref="ListBox.SelectionChanged"/> event.
    /// </summary>
    public static ListBox OnSelectionChanged(this ListBox listBox, Action<object?> handler)
    {
        listBox.SelectionChanged += (s, item) => handler(item);
        return listBox;
    }

    /// <summary>
    /// Adds an event handler for the <see cref="ListBox.SelectionChanged"/> event.
    /// </summary>
    public static ListBox OnSelectionChanged(this ListBox listBox, EventHandler<object?> handler)
    {
        listBox.SelectionChanged += handler;
        return listBox;
    }

    /// <summary>
    /// Binds the <see cref="ListBox.SelectedItemProperty"/> to a source object property.
    /// </summary>
    public static ListBox BindSelectedItem<TSource, TProperty>(
        this ListBox listBox,
        TSource source,
        Func<TSource, TProperty> getter,
        Action<TSource, TProperty>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged)
        where TSource : class
    {
        Action<TSource, object?>? targetSetter = setter != null
            ? (src, val) => setter(src, (TProperty)val!)
            : null;

        listBox.SetBinding<object?, TSource>(
            ListBox.SelectedItemProperty,
            source,
            src => getter(src),
            targetSetter,
            updateSourceTrigger);
        return listBox;
    }

    /// <summary>
    /// Binds the <see cref="ListBox.SelectedItemProperty"/> to a property on the current <see cref="BindableObject.DataContext"/>.
    /// </summary>
    public static ListBox BindSelectedItem<TDataContext, TProperty>(
        this ListBox listBox,
        Func<TDataContext, TProperty> getter,
        Action<TDataContext, TProperty>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged)
        where TDataContext : class
    {
        Action<TDataContext, object?>? targetSetter = setter != null
            ? (dc, val) => setter(dc, (TProperty)val!)
            : null;

        listBox.SetBinding<object?, TDataContext>(
            ListBox.SelectedItemProperty,
            dc => getter(dc),
            targetSetter,
            updateSourceTrigger);
        return listBox;
    }

    /// <summary>
    /// Binds the <see cref="ListBox.SelectedIndexProperty"/> to a source object property.
    /// </summary>
    public static ListBox BindSelectedIndex<TSource>(
        this ListBox listBox,
        TSource source,
        Func<TSource, int> getter,
        Action<TSource, int>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        listBox.SetBinding(ListBox.SelectedIndexProperty, source, getter, setter, updateSourceTrigger, getterExpression: getterExpression);
        return listBox;
    }

    /// <summary>
    /// Binds the <see cref="ListBox.SelectedIndexProperty"/> to a property on the current <see cref="BindableObject.DataContext"/>.
    /// </summary>
    public static ListBox BindSelectedIndex<TDataContext>(
        this ListBox listBox,
        Func<TDataContext, int> getter,
        Action<TDataContext, int>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TDataContext : class
    {
        listBox.SetBinding(ListBox.SelectedIndexProperty, getter, setter, updateSourceTrigger, getterExpression: getterExpression);
        return listBox;
    }

    /// <summary>
    /// Binds the <see cref="ItemsControl.ItemsSourceProperty"/> of the <see cref="ListBox"/> to a source object property.
    /// </summary>
    public static ListBox BindItemsSource<TSource>(
        this ListBox listBox,
        TSource source,
        Func<TSource, System.Collections.IEnumerable?> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        listBox.SetBinding(ItemsControl.ItemsSourceProperty, source, getter, getterExpression: getterExpression);
        return listBox;
    }

    /// <summary>
    /// Binds the <see cref="ItemsControl.ItemsSourceProperty"/> of the <see cref="ListBox"/> to a property on the current <see cref="BindableObject.DataContext"/>.
    /// </summary>
    public static ListBox BindItemsSource<TDataContext>(
        this ListBox listBox,
        Func<TDataContext, System.Collections.IEnumerable?> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TDataContext : class
    {
        listBox.SetBinding(ItemsControl.ItemsSourceProperty, getter, getterExpression: getterExpression);
        return listBox;
    }

    #endregion

    #region ItemsControl Extensions

    /// <summary>
    /// Sets the items source collection for the <see cref="ItemsControl"/>.
    /// </summary>
    public static ItemsControl ItemsSource(this ItemsControl itemsControl, System.Collections.IEnumerable? source)
    {
        itemsControl.ItemsSource = source;
        return itemsControl;
    }

    /// <summary>
    /// Adds items to the <see cref="ItemsControl"/>.
    /// </summary>
    public static ItemsControl Items(this ItemsControl itemsControl, params object[] items)
    {
        foreach (var item in items) itemsControl.Items.Add(item);
        return itemsControl;
    }

    /// <summary>
    /// Sets the item template delegate for the <see cref="ItemsControl"/>.
    /// </summary>
    public static ItemsControl ItemTemplate(this ItemsControl itemsControl, Func<object, UIElement> template)
    {
        itemsControl.ItemTemplate = template;
        return itemsControl;
    }

    /// <summary>
    /// Sets a strongly-typed item template delegate for the <see cref="ItemsControl"/>.
    /// </summary>
    public static ItemsControl ItemTemplate<T>(this ItemsControl itemsControl, Func<T, UIElement> template)
    {
        itemsControl.ItemTemplate = obj => obj is T typed ? template(typed) : new TextBlock(obj?.ToString() ?? string.Empty);
        return itemsControl;
    }

    /// <summary>
    /// Sets the item template delegate for the <see cref="ItemsControl"/>.
    /// </summary>
    public static ItemsControl WithItemTemplate(this ItemsControl itemsControl, Func<object, UIElement> template)
    {
        itemsControl.ItemTemplate = template;
        return itemsControl;
    }

    /// <summary>
    /// Sets a strongly-typed item template delegate for the <see cref="ItemsControl"/>.
    /// </summary>
    public static ItemsControl WithItemTemplate<T>(this ItemsControl itemsControl, Func<T, UIElement> template)
    {
        itemsControl.ItemTemplate = obj => obj is T typed ? template(typed) : new TextBlock(obj?.ToString() ?? string.Empty);
        return itemsControl;
    }

    /// <summary>
    /// Binds the <see cref="ItemsControl.ItemsSourceProperty"/> to a source object property.
    /// </summary>
    public static ItemsControl BindItemsSource<TSource>(
        this ItemsControl itemsControl,
        TSource source,
        Func<TSource, System.Collections.IEnumerable?> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        itemsControl.SetBinding(ItemsControl.ItemsSourceProperty, source, getter, getterExpression: getterExpression);
        return itemsControl;
    }

    /// <summary>
    /// Binds the <see cref="ItemsControl.ItemsSourceProperty"/> to a property on the current <see cref="BindableObject.DataContext"/>.
    /// </summary>
    public static ItemsControl BindItemsSource<TDataContext>(
        this ItemsControl itemsControl,
        Func<TDataContext, System.Collections.IEnumerable?> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TDataContext : class
    {
        itemsControl.SetBinding(ItemsControl.ItemsSourceProperty, getter, getterExpression: getterExpression);
        return itemsControl;
    }

    #endregion

    // Popup Extensions
    public static Popup Child(this Popup popup, UIElement child)
    {
        popup.Child = child;
        return popup;
    }

    public static Popup PlacementTarget(this Popup popup, UIElement target)
    {
        popup.PlacementTarget = target;
        return popup;
    }

    public static Popup Placement(this Popup popup, PlacementMode mode)
    {
        popup.Placement = mode;
        return popup;
    }

    public static Popup StaysOpen(this Popup popup, bool staysOpen)
    {
        popup.StaysOpen = staysOpen;
        return popup;
    }

    public static Popup IsOpen(this Popup popup, bool isOpen)
    {
        popup.IsOpen = isOpen;
        return popup;
    }

    #region ContentControl Extensions
    public static ContentControl Content(this ContentControl control, object? content)
    {
        control.Content = content;
        return control;
    }

    public static ContentControl BindContent<TSource>(
        this ContentControl control,
        TSource source,
        Func<TSource, object?> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        control.SetBinding(ContentControl.ContentProperty, source, getter, getterExpression: getterExpression);
        return control;
    }

    public static ContentControl BindContent<TDataContext>(
        this ContentControl control,
        Func<TDataContext, object?> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TDataContext : class
    {
        control.SetBinding(ContentControl.ContentProperty, getter, getterExpression: getterExpression);
        return control;
    }

    public static ContentControl ViewLocator(this ContentControl control, IViewLocator locator)
    {
        control.ViewLocator = locator;
        return control;
    }

    public static ContentControl ContentTemplate(this ContentControl control, Func<object?, UIElement?> template)
    {
        control.ContentTemplate = template;
        return control;
    }

    #endregion

    #region DialogHost Extensions

    public static DialogHost Content(this DialogHost host, UIElement? content)
    {
        host.Content = content;
        return host;
    }

    public static DialogHost Dialog(this DialogHost host, UIElement? dialog)
    {
        host.Dialog = dialog;
        return host;
    }

    public static DialogHost CloseOnClickAway(this DialogHost host, bool closeOnClickAway = true)
    {
        host.CloseOnClickAway = closeOnClickAway;
        return host;
    }

    public static DialogHost OverlayColor(this DialogHost host, Color color)
    {
        host.OverlayColor = color;
        return host;
    }

    public static DialogHost Identifier(this DialogHost host, string identifier)
    {
        host.Identifier = identifier;
        return host;
    }

    #endregion

    #region Dialog Extensions

    public static Dialog Title(this Dialog dialog, string title)
    {
        dialog.Title = title;
        return dialog;
    }

    public static Dialog Message(this Dialog dialog, string message)
    {
        dialog.Message = message;
        return dialog;
    }

    public static Dialog Content(this Dialog dialog, UIElement? content)
    {
        dialog.Content = content;
        return dialog;
    }

    public static Dialog Buttons(this Dialog dialog, DialogButtons preset)
    {
        dialog.ButtonsPreset = preset;
        return dialog;
    }

    public static Dialog CloseOnEscape(this Dialog dialog, bool closeOnEscape = true)
    {
        dialog.CloseOnEscape = closeOnEscape;
        return dialog;
    }

    #endregion

    #region Icon Extensions

    public static Icon Kind(this Icon icon, MaterialIconKind kind)
    {
        icon.Kind = kind;
        return icon;
    }

    public static Icon Data(this Icon icon, SKPath path)
    {
        icon.Data = path;
        return icon;
    }

    public static Icon PathData(this Icon icon, string svgPathData)
    {
        icon.PathData = svgPathData;
        return icon;
    }

    public static Icon StrokeWidth(this Icon icon, float strokeWidth)
    {
        icon.StrokeWidth = strokeWidth;
        return icon;
    }

    public static Icon Size(this Icon icon, float size)
    {
        icon.Size = size;
        return icon;
    }

    public static Icon Fill(this Icon icon, float fill)
    {
        icon.Fill = fill;
        return icon;
    }

    public static Icon IsFilled(this Icon icon, bool isFilled = true)
    {
        icon.IsFilled = isFilled;
        return icon;
    }

    public static Icon Filled(this Icon icon)
    {
        icon.IsFilled = true;
        return icon;
    }

    public static Icon Weight(this Icon icon, float weight)
    {
        icon.Weight = weight;
        return icon;
    }

    public static Icon Grade(this Icon icon, float grade)
    {
        icon.Grade = grade;
        return icon;
    }

    public static Icon OpticalSize(this Icon icon, float opticalSize)
    {
        icon.OpticalSize = opticalSize;
        return icon;
    }

    public static Icon Foreground(this Icon icon, Color color)
    {
        icon.Foreground = color;
        return icon;
    }

    public static Icon ToIcon(this MaterialIconKind kind, float size = 24f, bool isFilled = false, Color? foreground = null)
    {
        return new Icon(kind, size, isFilled, foreground);
    }

    public static Icon ToIcon(this SKPath path, float size = 24f, Color? foreground = null)
    {
        return new Icon(path, size, foreground);
    }

    public static Icon ToIcon(this string svgPathData, float size = 24f, Color? foreground = null)
    {
        return new Icon(svgPathData, size, foreground);
    }

    public static Icon BindKind<TSource>(this Icon icon, TSource source, Func<TSource, MaterialIconKind> getter, Action<TSource, MaterialIconKind>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        icon.SetBinding(Icon.KindProperty, source, getter, setter, getterExpression: getterExpression);
        return icon;
    }

    public static Icon BindForeground<TSource>(this Icon icon, TSource source, Func<TSource, Color> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        icon.SetBinding(Icon.ForegroundProperty, source, getter, getterExpression: getterExpression);
        return icon;
    }

    public static Icon BindFill<TSource>(this Icon icon, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        icon.SetBinding(Icon.FillProperty, source, getter, setter, getterExpression: getterExpression);
        return icon;
    }

    public static Icon BindWeight<TSource>(this Icon icon, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        icon.SetBinding(Icon.WeightProperty, source, getter, setter, getterExpression: getterExpression);
        return icon;
    }

    public static Icon BindGrade<TSource>(this Icon icon, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        icon.SetBinding(Icon.GradeProperty, source, getter, setter, getterExpression: getterExpression);
        return icon;
    }

    public static Icon BindOpticalSize<TSource>(this Icon icon, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        icon.SetBinding(Icon.OpticalSizeProperty, source, getter, setter, getterExpression: getterExpression);
        return icon;
    }

    public static Icon BindSize<TSource>(this Icon icon, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        icon.SetBinding(Icon.SizeProperty, source, getter, setter, getterExpression: getterExpression);
        return icon;
    }

    public static Icon BindStrokeWidth<TSource>(this Icon icon, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        icon.SetBinding(Icon.StrokeWidthProperty, source, getter, setter, getterExpression: getterExpression);
        return icon;
    }

    public static Icon BindPathData<TSource>(this Icon icon, TSource source, Func<TSource, string?> getter, Action<TSource, string?>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        icon.SetBinding(Icon.PathDataProperty, source, getter, setter, getterExpression: getterExpression);
        return icon;
    }

    #endregion

    #region TreeView Extensions

    public static TreeView ItemsSource(this TreeView treeView, System.Collections.IEnumerable itemsSource)
    {
        treeView.ItemsSource = itemsSource;
        return treeView;
    }

    public static TreeView ChildrenSelector(this TreeView treeView, Func<object, System.Collections.IEnumerable?> selector)
    {
        treeView.ChildrenSelector = selector;
        return treeView;
    }

    public static TreeView ChildrenSelector<T>(this TreeView treeView, Func<T, System.Collections.IEnumerable?> selector)
    {
        treeView.ChildrenSelector = obj => obj is T typed ? selector(typed) : null;
        return treeView;
    }

    public static TreeView ItemTemplate(this TreeView treeView, Func<object, UIElement> template)
    {
        treeView.ItemTemplate = template;
        return treeView;
    }

    public static TreeView ItemTemplate<T>(this TreeView treeView, Func<T, UIElement> template)
    {
        treeView.ItemTemplate = obj => obj is T typed ? template(typed) : new TextBlock(obj?.ToString() ?? string.Empty);
        return treeView;
    }

    public static TreeView WithItemTemplate(this TreeView treeView, Func<object, UIElement> template)
    {
        treeView.ItemTemplate = template;
        return treeView;
    }

    public static TreeView WithItemTemplate<T>(this TreeView treeView, Func<T, UIElement> template)
    {
        treeView.ItemTemplate = obj => obj is T typed ? template(typed) : new TextBlock(obj?.ToString() ?? string.Empty);
        return treeView;
    }

    public static TreeView SelectedItem(this TreeView treeView, object? selectedItem)
    {
        treeView.SelectedItem = selectedItem;
        return treeView;
    }

    public static TreeView BindSelectedItem<TSource>(
        this TreeView treeView,
        TSource source,
        Func<TSource, object?> getter,
        Action<TSource, object?>? setter = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where TSource : class
    {
        treeView.SetBinding(TreeView.SelectedItemProperty, source, getter, setter, getterExpression: getterExpression);
        return treeView;
    }

    public static TreeView IndentSize(this TreeView treeView, float indentSize)
    {
        treeView.IndentSize = indentSize;
        return treeView;
    }

    public static TreeView ExpandIcon(this TreeView treeView, MaterialIconKind icon)
    {
        treeView.ExpandIcon = icon;
        return treeView;
    }

    public static TreeView CollapseIcon(this TreeView treeView, MaterialIconKind icon)
    {
        treeView.CollapseIcon = icon;
        return treeView;
    }

    public static TreeView OnSelectionChanged(this TreeView treeView, Action<object?> handler)
    {
        treeView.SelectionChanged += (s, item) => handler(item);
        return treeView;
    }

    public static TreeViewItem IsExpanded(this TreeViewItem item, bool isExpanded = true)
    {
        item.IsExpanded = isExpanded;
        return item;
    }

    public static TreeViewItem IsSelected(this TreeViewItem item, bool isSelected = true)
    {
        item.IsSelected = isSelected;
        return item;
    }

    public static TreeViewItem AddChild(this TreeViewItem parent, TreeViewItem child)
    {
        parent.AddChildItem(child);
        return parent;
    }

    #endregion

    #region VisualNode Transformations

    public static T Transform<T>(this T node, Matrix3x2 transform) where T : VisualNode
    {
        node.Transform = transform;
        return node;
    }

    public static T TransformOrigin<T>(this T node, float x, float y) where T : VisualNode
    {
        node.TransformOrigin = new Point(x, y);
        return node;
    }

    public static T TransformOrigin<T>(this T node, Point origin) where T : VisualNode
    {
        node.TransformOrigin = origin;
        return node;
    }

    public static T Scale<T>(this T node, float scale) where T : VisualNode
    {
        node.Transform = Matrix3x2.CreateScale(scale);
        return node;
    }

    public static T Scale<T>(this T node, float scaleX, float scaleY) where T : VisualNode
    {
        node.Transform = Matrix3x2.CreateScale(scaleX, scaleY);
        return node;
    }

    public static T Rotate<T>(this T node, float degrees) where T : VisualNode
    {
        float radians = degrees * (MathF.PI / 180f);
        node.Transform = Matrix3x2.CreateRotation(radians);
        return node;
    }

    public static T RotateCenter<T>(this T node, float degrees) where T : VisualNode
    {
        node.TransformOrigin = new Point(0.5f, 0.5f);
        float radians = degrees * (MathF.PI / 180f);
        node.Transform = Matrix3x2.CreateRotation(radians);
        return node;
    }

    public static T RenderTransform<T>(this T node, Matrix3x2 transform) where T : VisualNode
    {
        node.RenderTransform = transform;
        return node;
    }

    public static T RenderTransformOrigin<T>(this T node, float x, float y) where T : VisualNode
    {
        node.RenderTransformOrigin = new Point(x, y);
        return node;
    }

    public static T RenderTransformOrigin<T>(this T node, Point origin) where T : VisualNode
    {
        node.RenderTransformOrigin = origin;
        return node;
    }

    public static T RenderScale<T>(this T node, float scale) where T : VisualNode
    {
        node.RenderTransform = Matrix3x2.CreateScale(scale);
        return node;
    }

    public static T RenderScale<T>(this T node, float scaleX, float scaleY) where T : VisualNode
    {
        node.RenderTransform = Matrix3x2.CreateScale(scaleX, scaleY);
        return node;
    }

    public static T RenderRotate<T>(this T node, float degrees) where T : VisualNode
    {
        float radians = degrees * (MathF.PI / 180f);
        node.RenderTransform = Matrix3x2.CreateRotation(radians);
        return node;
    }

    public static T RenderRotateCenter<T>(this T node, float degrees) where T : VisualNode
    {
        node.RenderTransformOrigin = new Point(0.5f, 0.5f);
        float radians = degrees * (MathF.PI / 180f);
        node.RenderTransform = Matrix3x2.CreateRotation(radians);
        return node;
    }

    public static T RenderSkew<T>(this T node, float skewXDegrees, float skewYDegrees) where T : VisualNode
    {
        float skewXRad = skewXDegrees * (MathF.PI / 180f);
        float skewYRad = skewYDegrees * (MathF.PI / 180f);
        node.RenderTransform = Matrix3x2.CreateSkew(skewXRad, skewYRad);
        return node;
    }

    #endregion

    #region Image Extensions

    public static Image Source(this Image image, SKImage source)
    {
        image.Source = source;
        return image;
    }

    public static Image Source(this Image image, string pathOrResource)
    {
        image.Source = Image.LoadImage(pathOrResource);
        return image;
    }

    public static Image Stretch(this Image image, Stretch stretch)
    {
        image.Stretch = stretch;
        return image;
    }

    #endregion

    #region PropertyGrid Extensions

    public static PropertyGrid Inspect(this PropertyGrid grid, object? target)
    {
        grid.SelectedObject = target;
        return grid;
    }

    public static PropertyGrid WithSortMode(this PropertyGrid grid, PropertySortMode mode)
    {
        grid.SortMode = mode;
        return grid;
    }

    public static PropertyGrid WithFilter(this PropertyGrid grid, string filter)
    {
        grid.FilterText = filter;
        return grid;
    }

    public static PropertyGrid ShowToolbar(this PropertyGrid grid, bool show = true)
    {
        grid.IsToolbarVisible = show;
        return grid;
    }

    public static PropertyGrid WithLabelWidth(this PropertyGrid grid, float width)
    {
        grid.LabelWidth = width;
        return grid;
    }

    public static PropertyGrid RegisterCustomEditor<T>(this PropertyGrid grid, Func<PropertyEditorContext, UIElement> factory)
    {
        grid.RegisterEditor<T>(factory);
        return grid;
    }

    public static PropertyGrid RegisterCustomEditor(this PropertyGrid grid, Type type, Func<PropertyEditorContext, UIElement> factory)
    {
        grid.RegisterEditor(type, factory);
        return grid;
    }

    public static PropertyGrid OnPropertyValueChanged(this PropertyGrid grid, EventHandler<PropertyValueChangedEventArgs> handler)
    {
        grid.PropertyValueChanged += handler;
        return grid;
    }

    public static PropertyGrid WithToolbarElevation(this PropertyGrid grid, float elevation)
    {
        grid.ToolbarElevation = elevation;
        return grid;
    }

    #endregion

    #region Border & Toolbar Extensions

    public static Border BorderBrush(this Border border, Color color)
    {
        border.BorderBrush = color;
        return border;
    }

    public static Border BorderThickness(this Border border, Thickness thickness)
    {
        border.BorderThickness = thickness;
        return border;
    }

    public static Border BorderThickness(this Border border, float uniform)
    {
        border.BorderThickness = new Thickness(uniform);
        return border;
    }

    public static Toolbar Elevation(this Toolbar toolbar, float elevation)
    {
        toolbar.Elevation = elevation;
        return toolbar;
    }

    public static Toolbar BorderBrush(this Toolbar toolbar, Color color)
    {
        toolbar.BorderBrush = color;
        return toolbar;
    }

    public static Toolbar BorderThickness(this Toolbar toolbar, Thickness thickness)
    {
        toolbar.BorderThickness = thickness;
        return toolbar;
    }

    public static Toolbar BorderThickness(this Toolbar toolbar, float uniform)
    {
        toolbar.BorderThickness = new Thickness(uniform);
        return toolbar;
    }

    #endregion

    #region KeybindingHandler Extensions

    public static KeybindingHandler KeybindingGroup(this KeybindingHandler handler, string group)
    {
        handler.Group = group;
        return handler;
    }

    #endregion

    #region TransitioningContentControl Extensions

    public static T Transition<T>(this T control, ITransition? transition) where T : TransitioningContentControl
    {
        control.Transition = transition;
        return control;
    }

    public static T TransitionDuration<T>(this T control, TimeSpan duration) where T : TransitioningContentControl
    {
        control.Duration = duration;
        return control;
    }

    #endregion
}

