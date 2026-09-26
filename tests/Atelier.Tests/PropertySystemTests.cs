using System;
using System.Collections.Generic;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Properties;
using Atelier.Core.Styling;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Tests;

public class CountingPanel : StackPanel
{
    public int DataContextChangedCount { get; private set; }

    protected override void OnDataContextChanged(object? oldValue, object? newValue)
    {
        base.OnDataContextChanged(oldValue, newValue);
        DataContextChangedCount++;
    }
}

public class RangeObject : BindableObject
{
    public static readonly BindableProperty<float> ValueProperty =
        BindableProperty.Register<RangeObject, float>(
            nameof(Value),
            0f,
            coerceValue: (s, v) => Math.Min(v, ((RangeObject)s).Maximum));

    public static readonly BindableProperty<float> MaximumProperty =
        BindableProperty.Register<RangeObject, float>(
            nameof(Maximum),
            100f,
            (s, o, n) => s.CoerceValue(ValueProperty));

    public float Maximum { get => GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public float Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
}

public class ClampedTextBlock : TextBlock
{
    public static readonly BindableProperty<int> LevelProperty =
        BindableProperty.Register<ClampedTextBlock, int>(nameof(Level), 0, coerceValue: (s, v) => Math.Min(v, 10));

    public int Level { get => GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
}

public class DuplicateRegistrationOwner : BindableObject
{
}

public class PropertySystemTests
{
    #region Reparenting

    [Fact]
    public void Reparent_GrandchildDataContextBindingFollowsNewAncestor()
    {
        var vm1 = new BindingTestViewModel { Title = "First" };
        var vm2 = new BindingTestViewModel { Title = "Second" };

        var root1 = new StackPanel { DataContext = vm1 };
        var root2 = new StackPanel { DataContext = vm2 };

        var middle = new StackPanel();
        var leaf = new TextBox();
        leaf.BindText<BindingTestViewModel>(x => x.Title, (m, v) => m.Title = v);
        middle.Add(leaf);
        root1.Add(middle);
        Assert.Equal("First", leaf.Text);

        root2.Add(middle);

        Assert.Equal("Second", leaf.Text);
    }

    [Fact]
    public void Reparent_RaisesOnDataContextChangedOnce()
    {
        var root = new StackPanel { DataContext = new object() };
        var child = new CountingPanel();

        root.Add(child);

        Assert.Equal(1, child.DataContextChangedCount);
    }

    [Fact]
    public void Reparent_NotifiesEveryInheritableProperty()
    {
        var root = new StackPanel();
        root.IsEnabled = false;

        var child = new TextBlock("Child");
        var changed = new List<string?>();
        child.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

        root.Add(child);

        Assert.False(child.IsEnabled);
        Assert.Contains(nameof(child.IsEnabled), changed);
    }

    [Fact]
    public void Reparent_AliasedPropertyCallbacksOnlyRunOnMatchingTypes()
    {
        // TextBlock.FontSizeProperty's callback casts the sender to TextBlock; it must not run for a Button.
        // Previously threw InvalidCastException once the aliased value came from a grandparent.
        var root = new StackPanel();
        root.SetValue(Control.FontSizeProperty, 30f);
        var middle = new StackPanel();
        root.Add(middle);

        var button = new Button("Click");
        middle.Add(button);

        Assert.Equal(30f, button.FontSize);
    }

    [Fact]
    public void Reparent_MoveBetweenParents_RaisesSingleNotificationWithRealOldValue()
    {
        var from = new StackPanel();
        from.SetValue(Control.FontSizeProperty, 20f);
        var to = new StackPanel();
        to.SetValue(Control.FontSizeProperty, 40f);

        var child = new TextBlock("Moving");
        from.Add(child);

        int notifications = 0;
        child.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TextBlock.FontSize)) notifications++;
        };

        to.Add(child);

        Assert.Equal(1, notifications);
        Assert.Equal(40f, child.FontSize);
    }

    [Fact]
    public void AddChild_ExistingChildOfSameParent_MovesToEnd()
    {
        var panel = new StackPanel();
        var a = new TextBlock("A");
        var b = new TextBlock("B");
        panel.Add(a);
        panel.Add(b);

        panel.Add(a);

        Assert.Equal(new[] { b, a }, panel.Children);
    }

    #endregion

    #region Inheritance

    [Fact]
    public void Inheritance_ChangeDeepInTree_NotifiesGreatGrandchild()
    {
        var root = new StackPanel();
        var level1 = new StackPanel();
        var level2 = new StackPanel();
        var leaf = new TextBlock("Leaf");
        root.Add(level1);
        level1.Add(level2);
        level2.Add(leaf);

        bool notified = false;
        leaf.PropertyChanged += (s, e) => notified |= e.PropertyName == nameof(TextBlock.FontSize);

        root.SetValue(Control.FontSizeProperty, 50f);

        Assert.True(notified);
        Assert.Equal(50f, leaf.FontSize);
    }

    [Fact]
    public void Inheritance_ShadowedByIntermediateValue_DoesNotNotifyLeaf()
    {
        var root = new StackPanel();
        var middle = new StackPanel();
        middle.SetValue(Control.FontSizeProperty, 18f);
        var leaf = new TextBlock("Leaf");
        root.Add(middle);
        middle.Add(leaf);

        bool notified = false;
        leaf.PropertyChanged += (s, e) => notified |= e.PropertyName == nameof(TextBlock.FontSize);

        root.SetValue(Control.FontSizeProperty, 50f);

        Assert.False(notified);
        Assert.Equal(18f, leaf.FontSize);
    }

    #endregion

    #region Styles

    [Fact]
    public void Style_ReapplyingSameStyle_RaisesNoChange()
    {
        var tb = new TextBlock("Styled");
        tb.Style = new Style(typeof(TextBlock)).Set(TextBlock.FontSizeProperty, 20f);

        var changed = new List<string?>();
        tb.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

        tb.ApplyStyles();

        Assert.Empty(changed);
        Assert.Equal(20f, tb.FontSize);
    }

    [Fact]
    public void Style_Removed_RevertsAndNotifies()
    {
        var tb = new TextBlock("Styled");
        tb.Style = new Style(typeof(TextBlock)).Set(TextBlock.FontSizeProperty, 20f);

        bool notified = false;
        tb.PropertyChanged += (s, e) => notified |= e.PropertyName == nameof(TextBlock.FontSize);

        tb.Style = null;

        Assert.True(notified);
        Assert.Equal(14f, tb.FontSize);
    }

    [Fact]
    public void Style_RemovedFromParent_NotifiesInheritingChild()
    {
        var panel = new StackPanel();
        var child = new TextBlock("Child");
        panel.Add(child);
        panel.Style = new Style(typeof(StackPanel)).Set(Control.FontSizeProperty, 24f);
        Assert.Equal(24f, child.FontSize);

        bool notified = false;
        child.PropertyChanged += (s, e) => notified |= e.PropertyName == nameof(TextBlock.FontSize);

        panel.Style = null;

        Assert.True(notified);
        Assert.Equal(14f, child.FontSize);
    }

    [Fact]
    public void Setter_WithWrongValueType_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Setter(TextBlock.FontSizeProperty, "large"));
        Assert.Throws<ArgumentException>(() => new Setter(TextBlock.FontSizeProperty, null));
    }

    #endregion

    #region Untyped access and coercion

    [Fact]
    public void SetValueUntyped_WithWrongValueType_ThrowsAtCallSite()
    {
        var tb = new TextBlock();

        Assert.Throws<ArgumentException>(() => tb.SetValueUntyped(TextBlock.FontSizeProperty, "large"));
        Assert.Throws<ArgumentException>(() => tb.SetValueUntyped(TextBlock.FontSizeProperty, null));
        Assert.Equal(14f, tb.FontSize);
    }

    [Fact]
    public void SetValueUntyped_NullForReferenceType_IsAllowed()
    {
        var tb = new TextBlock();
        tb.SetValueUntyped(TextBlock.FontFamilyProperty, null);
        Assert.Null(tb.GetValue(TextBlock.FontFamilyProperty));
    }

    [Fact]
    public void SetValueUntyped_AppliesCoercion()
    {
        var range = new RangeObject { Maximum = 10f };
        range.SetValueUntyped(RangeObject.ValueProperty, 25f);
        Assert.Equal(10f, range.Value);
    }

    [Fact]
    public void CoerceValue_RestoresRequestedValueWhenConstraintRelaxes()
    {
        var range = new RangeObject { Value = 80f };

        range.Maximum = 50f;
        Assert.Equal(50f, range.Value);

        range.Maximum = 100f;
        Assert.Equal(80f, range.Value);
    }

    [Fact]
    public void Coercion_AppliesToStyledValues()
    {
        var box = new ClampedTextBlock();
        box.Style = new Style(typeof(ClampedTextBlock)).Set(ClampedTextBlock.LevelProperty, 99);
        Assert.Equal(10, box.Level);
    }

    #endregion

    #region Registration

    [Fact]
    public void Register_DuplicateNameOnSameOwner_Throws()
    {
        BindableProperty.Register<DuplicateRegistrationOwner, int>("Duplicate");
        Assert.Throws<InvalidOperationException>(() => BindableProperty.Register<DuplicateRegistrationOwner, int>("Duplicate"));
    }

    [Fact]
    public void AttachedProperties_AreOwnedByDeclaringPanel()
    {
        Assert.True(Grid.RowProperty.IsAttached);
        Assert.Equal(typeof(Grid), Grid.RowProperty.OwnerType);
        Assert.Same(Grid.RowProperty, BindableProperty.FindByName(typeof(Grid), "Row"));
        Assert.Null(BindableProperty.FindByName(typeof(Button), "Row"));
    }

    #endregion

    #region Bindings

    [Fact]
    public void DataContextBinding_DataContextBecomesNull_ClearsBoundValue()
    {
        var vm = new BindingTestViewModel { Title = "Bound" };
        var tb = new TextBox { DataContext = vm };
        tb.BindText<BindingTestViewModel>(x => x.Title);
        Assert.Equal("Bound", tb.Text);

        tb.DataContext = null;

        Assert.Equal(string.Empty, tb.Text);
    }

    [Fact]
    public void Binding_LostFocusTriggerOnNonUIElement_Throws()
    {
        var target = new BindableObject();
        var source = new BindingTestViewModel();

        Assert.Throws<ArgumentException>(() => target.SetBinding<object?, BindingTestViewModel>(
            BindableObject.DataContextProperty, source, s => s.Title, (s, v) => { }, UpdateSourceTrigger.LostFocus));
    }

    [Fact]
    public void TwoWayBinding_CoercedTargetValue_IsWrittenBackToSource()
    {
        var vm = new BindingTestViewModel { SliderValue = 500f };
        var range = new RangeObject { Maximum = 100f };

        range.SetBinding(RangeObject.ValueProperty, vm, x => x.SliderValue, (x, v) => x.SliderValue = v);

        Assert.Equal(100f, range.Value);
        Assert.Equal(100f, vm.SliderValue);
    }

    #endregion
}
