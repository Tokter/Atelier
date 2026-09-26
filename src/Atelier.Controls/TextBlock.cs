using System;
using Atelier.Core;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Controls;

public enum TextAlignment
{
    Left,
    Center,
    Right
}

public enum TextWrapping
{
    NoWrap = 0,
    Wrap
}

public class TextBlock : UIElement
{
    public static readonly BindableProperty<string> TextProperty =
        BindableProperty.Register<TextBlock, string>(
            nameof(Text),
            string.Empty,
            options: PropertyOptions.AffectsMeasure
        );

    public static readonly BindableProperty<float> FontSizeProperty =
        BindableProperty.Register<TextBlock, float>(
            nameof(FontSize),
            14f,
            options: PropertyOptions.AffectsMeasure,
            inherits: true
        );

    public static readonly BindableProperty<Color> ForegroundProperty =
        BindableProperty.Register<TextBlock, Color>(
            nameof(Foreground),
            Color.Black,
            options: PropertyOptions.AffectsRender,
            inherits: true
        );

    public static readonly BindableProperty<string?> FontFamilyProperty =
        BindableProperty.Register<TextBlock, string?>(
            nameof(FontFamily),
            null,
            options: PropertyOptions.AffectsMeasure,
            inherits: true
        );

    public static readonly BindableProperty<TextAlignment> TextAlignmentProperty =
        BindableProperty.Register<TextBlock, TextAlignment>(
            nameof(TextAlignment),
            TextAlignment.Left,
            options: PropertyOptions.AffectsRender
        );

    public static readonly BindableProperty<TextWrapping> TextWrappingProperty =
        BindableProperty.Register<TextBlock, TextWrapping>(
            nameof(TextWrapping),
            TextWrapping.NoWrap,
            options: PropertyOptions.AffectsMeasure
        );

    public static readonly BindableProperty<bool> BoldProperty =
        BindableProperty.Register<TextBlock, bool>(
            nameof(Bold),
            false,
            options: PropertyOptions.AffectsMeasure
        );

    public static readonly BindableProperty<bool> ItalicProperty =
        BindableProperty.Register<TextBlock, bool>(
            nameof(Italic),
            false,
            options: PropertyOptions.AffectsMeasure
        );

    public static readonly BindableProperty<bool> MutedProperty =
        BindableProperty.Register<TextBlock, bool>(
            nameof(Muted),
            false,
            options: PropertyOptions.AffectsRender
        );

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public float FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public Color Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public string? FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public TextAlignment TextAlignment { get => GetValue(TextAlignmentProperty); set => SetValue(TextAlignmentProperty, value); }
    public TextWrapping TextWrapping { get => GetValue(TextWrappingProperty); set => SetValue(TextWrappingProperty, value); }
    public bool Bold { get => GetValue(BoldProperty); set => SetValue(BoldProperty, value); }
    public bool Italic { get => GetValue(ItalicProperty); set => SetValue(ItalicProperty, value); }
    public bool Muted { get => GetValue(MutedProperty); set => SetValue(MutedProperty, value); }

    public TextBlock()
    {
        IsHitTestVisible = false;
    }

    public TextBlock(string text) : this()
    {
        Text = text;

        if (Text.ContainsEmoji())
        {
            FontFamily = "Segoe UI Emoji";
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (string.IsNullOrEmpty(Text)) return Size.Zero;

        if (TextWrapping == TextWrapping.Wrap && !float.IsPositiveInfinity(availableSize.Width) && availableSize.Width > 0)
        {
            var (size, _) = TextMeasurer.MeasureWrapped(Text, availableSize.Width, FontSize, FontFamily, Bold, Italic);
            return size;
        }

        return TextMeasurer.Measure(Text, FontSize, FontFamily, Bold, Italic);
    }
}
