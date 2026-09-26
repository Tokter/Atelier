using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Tests;

public class SlotTestObject : BindableObject
{
    public static readonly BindableProperty<float> ValueProperty =
        BindableProperty.Register<SlotTestObject, float>("Value", 0f);

    public float Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
}

// Two inheritable properties named "SlotAlias": a float one and an object one. The object one can inherit the float
// value (object is assignable from float), which exercises reading a typed slot through a differently-typed alias.
public class SlotAliasFloatOwner : StackPanel
{
    public static readonly BindableProperty<float> SlotAliasProperty =
        BindableProperty.Register<SlotAliasFloatOwner, float>("SlotAlias", 0f, inherits: true);
}

public class SlotAliasObjectOwner : StackPanel
{
    public static readonly BindableProperty<object?> SlotAliasProperty =
        BindableProperty.Register<SlotAliasObjectOwner, object?>("SlotAlias", null, inherits: true);
}

public class TypedValueStorageTests
{
    private static long AllocatedBy(Action action)
    {
        action();
        action(); // second warm-up: the first real change creates the slot
        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void AnimatingValueTypes_DoesNotAllocatePerFrame()
    {
        var tb = new TextBlock("x");
        float t = 0f;

        Assert.Equal(0, AllocatedBy(() => tb.SetAnimatedValue(UIElement.OpacityProperty, (t += 0.01f) % 1f)));
        Assert.Equal(0, AllocatedBy(() => tb.SetAnimatedValue(VisualNode.RenderTransformProperty, Matrix3x2.CreateTranslation(t += 1f, 0))));
    }

    [Fact]
    public void ChangingValueTypeLocalValue_DoesNotAllocate()
    {
        var tb = new TextBlock("x");
        float size = 10f;

        Assert.Equal(0, AllocatedBy(() => tb.FontSize = size += 1f));
    }

    [Fact]
    public void OldValuesAreNotAliasedWithTheReusedSlot()
    {
        var obj = new SlotTestObject { Value = 1f };
        var changes = new List<(float Old, float New)>();
        using var subscription = obj.Subscribe(SlotTestObject.ValueProperty, (s, o, n) => changes.Add((o, n)));

        object? boxedBefore = obj.GetValueUntyped(SlotTestObject.ValueProperty);
        obj.Value = 2f;
        obj.Value = 3f;

        Assert.Equal(new[] { (1f, 2f), (2f, 3f) }, changes);
        Assert.Equal(1f, boxedBefore); // an untyped read is a copy, not a view of the slot
        Assert.Equal(3f, obj.GetValueUntyped(SlotTestObject.ValueProperty));
    }

    [Fact]
    public void UntypedAndTypedSets_Interoperate()
    {
        var obj = new SlotTestObject();

        obj.Value = 1f;                                               // typed: creates a slot
        obj.SetValueUntyped(SlotTestObject.ValueProperty, 2f);        // untyped: replaces it with a box
        Assert.Equal(2f, obj.Value);
        obj.Value = 3f;                                               // typed again
        Assert.Equal(3f, obj.GetValueUntyped(SlotTestObject.ValueProperty));
        Assert.False(obj.SetValueUntyped(SlotTestObject.ValueProperty, 3f)); // equal value is detected through the slot
    }

    [Fact]
    public void SlotValue_InheritedThroughDifferentlyTypedAlias_IsReadCorrectly()
    {
        var parent = new SlotAliasFloatOwner();
        parent.SetValue(SlotAliasFloatOwner.SlotAliasProperty, 4.5f); // no children yet: stored in a typed slot
        var child = new SlotAliasObjectOwner();
        parent.Add(child);

        Assert.Equal(4.5f, child.GetValue(SlotAliasObjectOwner.SlotAliasProperty));
    }

    [Fact]
    public void InheritedChangeWithChildren_StillNotifiesDescendants()
    {
        var panel = new StackPanel();
        var child = new TextBlock("child");
        panel.Add(child);
        var seen = new List<float>();
        using var subscription = child.Subscribe(TextBlock.FontSizeProperty, (s, o, n) => seen.Add(n));

        panel.SetValue(Control.FontSizeProperty, 20f);
        panel.SetValue(Control.FontSizeProperty, 22f);

        Assert.Equal(new[] { 20f, 22f }, seen);
    }

    [Fact]
    public void TwoWayBinding_TypedTargetChange_UpdatesSource()
    {
        var vm = new BindingTestViewModel { SliderValue = 10f };
        var slider = new Slider();
        slider.SetBinding(Slider.ValueProperty, vm, x => x.SliderValue, (x, v) => x.SliderValue = v);

        slider.Value = 42f;

        Assert.Equal(42f, vm.SliderValue);
    }
}
