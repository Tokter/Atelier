using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// The Material Design 3 card styles.
/// </summary>
public enum CardVariant
{
    /// <summary>A surface with an outline and no shadow.</summary>
    Outlined,

    /// <summary>A low surface container with a shadow (at least elevation 1).</summary>
    Elevated,

    /// <summary>A highest surface container fill without outline; shadowed only when an elevation is set.</summary>
    Filled
}

/// <summary>
/// A Material Design 3 card: a <see cref="Border"/> that groups related content on a styled surface.
/// </summary>
/// <remarks>
/// Defaults differ from <see cref="Border"/>: <see cref="Border.CornerRadius"/> is 12, <see cref="Border.Padding"/> is 16
/// and <see cref="UIElement.ClipToBounds"/> is <c>true</c>, so content follows the rounded corners.
/// </remarks>
public class Card : Border
{
    /// <summary>Identifies the <see cref="Variant"/> property.</summary>
    public static readonly BindableProperty<CardVariant> VariantProperty =
        BindableProperty.Register<Card, CardVariant>(
            nameof(Variant),
            CardVariant.Outlined,
            options: PropertyOptions.AffectsRender);

    /// <summary>Gets or sets the visual style. The default is <see cref="CardVariant.Outlined"/>.</summary>
    public CardVariant Variant
    {
        get => GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    static Card()
    {
        CornerRadiusProperty.OverrideDefaultValue<Card>(new CornerRadius(12f));
        PaddingProperty.OverrideDefaultValue<Card>(new Thickness(16f));
        ClipToBoundsProperty.OverrideDefaultValue<Card>(true);
    }

    /// <summary>Initializes a new, empty outlined card.</summary>
    public Card()
    {
    }

    /// <summary>Initializes a new, empty card of the given <paramref name="variant"/>.</summary>
    public Card(CardVariant variant) : this()
    {
        Variant = variant;
    }

    /// <summary>Initializes a new outlined card containing <paramref name="child"/>.</summary>
    public Card(UIElement child) : this()
    {
        Child = child;
    }

    /// <summary>Initializes a new card of the given <paramref name="variant"/> containing <paramref name="child"/>.</summary>
    public Card(CardVariant variant, UIElement child) : this(variant)
    {
        Child = child;
    }
}
