using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

public enum CardVariant
{
    Outlined,
    Elevated,
    Filled
}

public class Card : Border
{
    public static readonly BindableProperty<CardVariant> VariantProperty =
        BindableProperty.Register<Card, CardVariant>(
            nameof(Variant),
            CardVariant.Outlined,
            (s, o, n) => ((Card)s).InvalidateVisual());

    public CardVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public Card()
    {
        CornerRadius = new CornerRadius(12f);
        Padding = new Thickness(16f);
    }

    public Card(CardVariant variant) : this()
    {
        Variant = variant;
    }

    public Card(UIElement child) : this()
    {
        Child = child;
    }

    public Card(CardVariant variant, UIElement child) : this(variant)
    {
        Child = child;
    }
}
