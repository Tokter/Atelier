using System;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Styling;
using Atelier.Layout;

namespace Atelier.Tests;

public class AddOwnerSource : BindableObject
{
    public static readonly BindableProperty<int> SharedProperty =
        BindableProperty.Register<AddOwnerSource, int>("Shared", 1);
}

public class AddOwnerTarget : BindableObject
{
    public static readonly BindableProperty<int> SharedProperty = AddOwnerSource.SharedProperty.AddOwner<AddOwnerTarget>(5);

    public static readonly BindableProperty<int> OtherProperty =
        BindableProperty.Register<AddOwnerTarget, int>("Other", 0);
}

public class AddOwnerTests
{
    [Fact]
    public void TextBlockAndControl_ShareTheSameProperties()
    {
        Assert.Same(Control.FontSizeProperty, TextBlock.FontSizeProperty);
        Assert.Same(Control.ForegroundProperty, TextBlock.ForegroundProperty);
        Assert.Same(Control.FontFamilyProperty, TextBlock.FontFamilyProperty);
    }

    [Fact]
    public void StyleSetterThroughControlField_AppliesToTextBlock()
    {
        var tb = new TextBlock("x")
        {
            Style = new Style(typeof(TextBlock)).Set(Control.FontSizeProperty, 22f).Set(Control.ForegroundProperty, Color.White)
        };

        Assert.Equal(22f, tb.FontSize);
        Assert.Equal(Color.White, tb.Foreground);
    }

    [Fact]
    public void StyleSetterThroughTextBlockField_AppliesToButton()
    {
        var button = new Button("x") { Style = new Style(typeof(Button)).Set(TextBlock.FontSizeProperty, 18f) };

        Assert.Equal(18f, button.FontSize);
    }

    [Fact]
    public void LocalValueThroughEitherField_IsTheSameValue()
    {
        var tb = new TextBlock("x");
        tb.SetValue(Control.FontFamilyProperty, "Serif");

        Assert.Equal("Serif", tb.FontFamily);
        Assert.Equal(ValueSource.Local, tb.GetValueSource(TextBlock.FontFamilyProperty));
    }

    [Fact]
    public void InheritedChange_NotifiesBothOwnerTypes()
    {
        var root = new StackPanel();
        var tb = new TextBlock("x");
        var button = new Button("y");
        root.Add(tb);
        root.Add(button);
        int tbChanges = 0, buttonChanges = 0;
        tb.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(TextBlock.FontSize)) tbChanges++; };
        button.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(Control.FontSize)) buttonChanges++; };

        root.SetValue(TextBlock.FontSizeProperty, 30f);

        Assert.Equal(30f, tb.FontSize);
        Assert.Equal(30f, button.FontSize);
        Assert.Equal(1, tbChanges);
        Assert.Equal(1, buttonChanges);
    }

    [Fact]
    public void FindByName_FindsTheSharedPropertyThroughEveryOwner()
    {
        Assert.Same(Control.FontSizeProperty, BindableProperty.FindByName(typeof(TextBlock), "FontSize"));
        Assert.Same(Control.FontSizeProperty, BindableProperty.FindByName(typeof(Button), "FontSize"));
        Assert.Contains(typeof(TextBlock), Control.FontSizeProperty.OwnerTypes);
        Assert.True(Control.FontSizeProperty.AppliesTo(typeof(TextBlock)));
        Assert.False(Control.FontSizeProperty.AppliesTo(typeof(StackPanel)));
    }

    [Fact]
    public void AddOwnerWithDefault_OverridesTheDefaultForTheNewOwnerOnly()
    {
        Assert.Equal(1, new AddOwnerSource().GetValue(AddOwnerSource.SharedProperty));
        Assert.Equal(5, new AddOwnerTarget().GetValue(AddOwnerTarget.SharedProperty));
    }

    [Fact]
    public void AddOwner_IsIdempotent_ButRejectsANameTakenByAnotherProperty()
    {
        Assert.Same(AddOwnerSource.SharedProperty, AddOwnerSource.SharedProperty.AddOwner<AddOwnerTarget>());

        _ = AddOwnerTarget.OtherProperty; // make sure AddOwnerTarget's "Other" is registered
        var clash = BindableProperty.Register<AddOwnerSource, int>("Other", 0);

        Assert.Throws<InvalidOperationException>(() => clash.AddOwner<AddOwnerTarget>());
    }
}
