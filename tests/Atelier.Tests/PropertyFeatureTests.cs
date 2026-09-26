using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Animation;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Styling;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Tests;

public class ValidatedObject : BindableObject
{
    public static readonly BindableProperty<int> CountProperty =
        BindableProperty.Register<ValidatedObject, int>(nameof(Count), 0, validateValue: v => v >= 0);

    public int Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }
}

public class InvalidDefaultOwner : BindableObject
{
}

public class PillButton : Button
{
}

public class PropertyFeatureTests
{
    #region Metadata flags

    [Fact]
    public void AffectsMeasure_InvalidatesMeasureOnChange()
    {
        var tb = new TextBlock("Measure me");
        tb.Measure(new Size(200, 200));
        Assert.True(tb.IsMeasureValid);

        tb.FontSize = 30f;

        Assert.False(tb.IsMeasureValid);
    }

    [Fact]
    public void AffectsMeasure_AppliesToInheritedChanges()
    {
        var panel = new StackPanel();
        var tb = new TextBlock("Child");
        panel.Add(tb);
        tb.Measure(new Size(200, 200));
        Assert.True(tb.IsMeasureValid);

        panel.SetValue(Control.FontSizeProperty, 30f);

        Assert.False(tb.IsMeasureValid);
    }

    #endregion

    #region Animation layer

    [Fact]
    public void AnimatedValue_OverridesLocalAndRestoresItWhenCleared()
    {
        var button = new Button("Fade") { Opacity = 0.5f };

        button.SetAnimatedValue(UIElement.OpacityProperty, 0.2f);
        Assert.Equal(0.2f, button.Opacity);
        Assert.Equal(ValueSource.Animation, button.GetValueSource(UIElement.OpacityProperty));

        button.ClearAnimatedValue(UIElement.OpacityProperty);
        Assert.Equal(0.5f, button.Opacity);
        Assert.Equal(ValueSource.Local, button.GetValueSource(UIElement.OpacityProperty));
    }

    [Fact]
    public void AnimatedValue_LocalSetDuringAnimation_TakesEffectAfterwards()
    {
        var button = new Button("Fade");
        button.SetAnimatedValue(UIElement.OpacityProperty, 0.2f);

        button.Opacity = 0.7f;
        Assert.Equal(0.2f, button.Opacity);

        button.ClearAnimatedValue(UIElement.OpacityProperty);
        Assert.Equal(0.7f, button.Opacity);
    }

    [Fact]
    public void AnimatedValue_OfInheritableProperty_FlowsToChildren()
    {
        var panel = new StackPanel();
        var tb = new TextBlock("Child");
        panel.Add(tb);

        panel.SetAnimatedValue(Control.FontSizeProperty, 22f);
        Assert.Equal(22f, tb.FontSize);

        panel.ClearAnimatedValue(Control.FontSizeProperty);
        Assert.Equal(14f, tb.FontSize);
    }

    [Fact]
    public void TransitionReset_PreservesElementsOwnValues()
    {
        var from = new Button("From") { Opacity = 0.5f };
        var to = new Button("To") { RenderTransform = Matrix3x2.CreateRotation(0.1f) };
        var transition = new ZoomTransition();

        transition.Apply(from, to, 0.25f, new Size(100, 100));
        Assert.Equal(0.75f, from.Opacity, tolerance: 0.001f);

        transition.Reset(from, to);

        Assert.Equal(0.5f, from.Opacity);
        Assert.Equal(Matrix3x2.CreateRotation(0.1f), to.RenderTransform);
        Assert.Equal(ValueSource.Default, to.GetValueSource(VisualNode.RenderTransformOriginProperty));
        Assert.Equal(ValueSource.Default, to.GetValueSource(UIElement.OpacityProperty));
    }

    #endregion

    #region Read-only properties

    [Fact]
    public void ReadOnlyProperty_CannotBeSetFromOutside()
    {
        var button = new Button("Hover");

        Assert.Throws<InvalidOperationException>(() => button.SetValue(UIElement.IsHoveredProperty, true));
        Assert.Throws<InvalidOperationException>(() => button.SetValueUntyped(UIElement.IsHoveredProperty, true));
        Assert.Throws<InvalidOperationException>(() => button.ClearValue(UIElement.IsHoveredProperty));
        Assert.Throws<InvalidOperationException>(() => new Setter(UIElement.IsHoveredProperty, true));
        Assert.True(UIElement.IsHoveredProperty.IsReadOnly);
    }

    [Fact]
    public void ReadOnlyProperty_IsObservable()
    {
        var button = new Button("Hover");
        var changes = new List<bool>();
        using var subscription = button.Subscribe(UIElement.IsHoveredProperty, (s, o, n) => changes.Add(n));

        button.OnPointerEntered(new PointerEventArgs(new Point(1, 1), new Point(1, 1), PointerButtons.None));
        button.OnPointerExited(new PointerEventArgs(new Point(1, 1), new Point(1, 1), PointerButtons.None));

        Assert.Equal(new[] { true, false }, changes);
    }

    #endregion

    #region Validation

    [Fact]
    public void ValidateValueCallback_RejectsInvalidValues()
    {
        var obj = new ValidatedObject();

        Assert.Throws<ArgumentException>(() => obj.Count = -1);
        Assert.Throws<ArgumentException>(() => obj.SetValueUntyped(ValidatedObject.CountProperty, -5));
        Assert.Throws<ArgumentException>(() => new Setter(ValidatedObject.CountProperty, -2));
        Assert.Equal(0, obj.Count);

        obj.Count = 3;
        Assert.Equal(3, obj.Count);
    }

    [Fact]
    public void ValidateValueCallback_RejectsInvalidDefaultAtRegistration()
    {
        Assert.Throws<ArgumentException>(() =>
            BindableProperty.Register<InvalidDefaultOwner, int>("Negative", -1, validateValue: v => v >= 0));
    }

    #endregion

    #region Per-type defaults

    [Fact]
    public void OverriddenDefault_AppliesToTypeAndSubclasses_Only()
    {
        Assert.Equal(new Thickness(16, 6), new Button().Padding);
        Assert.Equal(new Thickness(16, 6), new PillButton().Padding);
        Assert.Equal(Thickness.Zero, new Slider().Padding);
        Assert.Equal(ValueSource.Default, new Button().GetValueSource(Control.PaddingProperty));
    }

    [Fact]
    public void OverriddenDefault_CanBeReplacedByStyle()
    {
        // Previously the constructor set Padding as a local value, which beat every style.
        var button = new Button("Styled");
        button.Style = new Style(typeof(Button)).Set(Control.PaddingProperty, new Thickness(4));

        Assert.Equal(new Thickness(4), button.Padding);
    }

    [Fact]
    public void OverrideDefaultValue_ForUnrelatedType_Throws()
    {
        Assert.Throws<ArgumentException>(() => Control.PaddingProperty.OverrideDefaultValue<StackPanel>(new Thickness(1)));
    }

    #endregion

    #region Value source and subscriptions

    [Fact]
    public void GetValueSource_ReportsEachLayer()
    {
        var panel = new StackPanel();
        panel.SetValue(Control.FontSizeProperty, 20f);
        var tb = new TextBlock("x");

        Assert.Equal(ValueSource.Default, tb.GetValueSource(TextBlock.FontSizeProperty));

        panel.Add(tb);
        Assert.Equal(ValueSource.Inherited, tb.GetValueSource(TextBlock.FontSizeProperty));

        tb.Style = new Style(typeof(TextBlock)).Set(TextBlock.FontSizeProperty, 18f);
        Assert.Equal(ValueSource.Style, tb.GetValueSource(TextBlock.FontSizeProperty));

        tb.FontSize = 12f;
        Assert.Equal(ValueSource.Local, tb.GetValueSource(TextBlock.FontSizeProperty));
    }

    [Fact]
    public void Subscribe_ReceivesTypedValues_UntilDisposed()
    {
        var tb = new TextBlock("x");
        var changes = new List<(float Old, float New)>();
        var subscription = tb.Subscribe(TextBlock.FontSizeProperty, (s, o, n) => changes.Add((o, n)));

        tb.FontSize = 20f;
        subscription.Dispose();
        tb.FontSize = 30f;

        Assert.Equal(new[] { (14f, 20f) }, changes);
    }

    [Fact]
    public void ElementToElementBinding_WorksWithExplicitSource()
    {
        var slider = new Slider();
        var label = new TextBlock();
        label.SetBinding(TextBlock.TextProperty, slider, s => s.Value.ToString("0"));

        slider.Value = 42f;

        Assert.Equal("42", label.Text);
    }

    #endregion
}
