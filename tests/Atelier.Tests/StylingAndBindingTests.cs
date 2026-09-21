using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Styling;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Tests;

public partial class BindingTestViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Initial Title";

    [ObservableProperty]
    private float _sliderValue = 50f;

    [ObservableProperty]
    private bool _isChecked = false;
}

public class StylingAndBindingTests
{
    #region Property Inheritance Tests

    [Fact]
    public void Inheritance_FontSizeFlowsDownVisualTree()
    {
        var panel = new StackPanel();
        panel.SetValue(Control.FontSizeProperty, 28f);

        var textBlock = new TextBlock("Child Text");
        panel.Add(textBlock);

        Assert.Equal(28f, textBlock.FontSize);
    }

    [Fact]
    public void Inheritance_GrandchildInheritsFromRoot()
    {
        var root = new StackPanel();
        root.SetValue(Control.FontSizeProperty, 32f);

        var innerPanel = new StackPanel();
        root.Add(innerPanel);

        var grandchild = new TextBlock("Grandchild");
        innerPanel.Add(grandchild);

        Assert.Equal(32f, grandchild.FontSize);
    }

    [Fact]
    public void Inheritance_LocalOverrideTakesPrecedence()
    {
        var root = new StackPanel();
        root.SetValue(Control.FontSizeProperty, 32f);

        var child = new TextBlock("Overridden")
        {
            FontSize = 18f
        };
        root.Add(child);

        Assert.Equal(18f, child.FontSize);
    }

    [Fact]
    public void Inheritance_ClearingLocalValueRevertsToInherited()
    {
        var root = new StackPanel();
        root.SetValue(Control.FontSizeProperty, 26f);

        var child = new TextBlock("Revertable")
        {
            FontSize = 18f
        };
        root.Add(child);
        Assert.Equal(18f, child.FontSize);

        child.ClearValue(TextBlock.FontSizeProperty);
        Assert.Equal(26f, child.FontSize);
    }

    [Fact]
    public void Inheritance_ParentChangeNotifiesDescendants()
    {
        var panel = new StackPanel();
        panel.SetValue(Control.FontSizeProperty, 20f);

        var child = new TextBlock("Dynamic Child");
        panel.Add(child);
        Assert.Equal(20f, child.FontSize);

        bool notified = false;
        child.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TextBlock.FontSize))
            {
                notified = true;
            }
        };

        panel.SetValue(Control.FontSizeProperty, 30f);

        Assert.True(notified);
        Assert.Equal(30f, child.FontSize);
    }

    [Fact]
    public void Inheritance_Reparenting_InheritsNewParentValue()
    {
        var child = new TextBlock("Orphan");
        Assert.Equal(14f, child.FontSize); // default

        var panel = new StackPanel();
        panel.SetValue(Control.FontSizeProperty, 22f);
        panel.Add(child);

        Assert.Equal(22f, child.FontSize);

        panel.Remove(child);
        Assert.Equal(14f, child.FontSize);
    }

    [Fact]
    public void Inheritance_DataContextFlowsDownAutomatically()
    {
        var root = new StackPanel();
        var vm1 = new BindingTestViewModel { Title = "Root VM" };
        root.DataContext = vm1;

        var childPanel = new StackPanel();
        root.Add(childPanel);

        var grandchild = new TextBlock();
        childPanel.Add(grandchild);

        Assert.Same(vm1, childPanel.DataContext);
        Assert.Same(vm1, grandchild.DataContext);

        var vm2 = new BindingTestViewModel { Title = "Swapped VM" };
        root.DataContext = vm2;

        Assert.Same(vm2, childPanel.DataContext);
        Assert.Same(vm2, grandchild.DataContext);
    }

    #endregion

    #region Data Binding Tests

    [Fact]
    public void DataBinding_OneWay_SourceUpdatesTarget()
    {
        var vm = new BindingTestViewModel { Title = "Start" };
        var textBlock = new TextBlock();
        textBlock.Bind(TextBlock.TextProperty, vm, x => x.Title);

        Assert.Equal("Start", textBlock.Text);

        vm.Title = "Next";
        Assert.Equal("Next", textBlock.Text);
    }

    [Fact]
    public void DataBinding_TwoWay_TargetUpdatesSource()
    {
        var vm = new BindingTestViewModel { Title = "Original" };
        var textBox = new TextBox();
        textBox.BindTwoWay(TextBox.TextProperty, vm, x => x.Title, (m, v) => m.Title = v);

        Assert.Equal("Original", textBox.Text);

        textBox.Text = "Modified By User";
        Assert.Equal("Modified By User", vm.Title);
    }

    [Fact]
    public void DataBinding_TwoWay_SourceUpdatesTarget_WithoutInfiniteLoop()
    {
        var vm = new BindingTestViewModel { Title = "Initial" };
        var textBox = new TextBox();
        textBox.BindTwoWay(TextBox.TextProperty, vm, x => x.Title, (m, v) => m.Title = v);

        vm.Title = "VM Update";
        Assert.Equal("VM Update", textBox.Text);

        textBox.Text = "Control Update";
        Assert.Equal("Control Update", vm.Title);
    }

    [Fact]
    public void DataBinding_DataContext_OneWay()
    {
        var panel = new StackPanel();
        var vm = new BindingTestViewModel { Title = "DC Title" };
        panel.DataContext = vm;

        var textBlock = new TextBlock();
        textBlock.BindText<BindingTestViewModel>(x => x.Title);
        panel.Add(textBlock);

        Assert.Equal("DC Title", textBlock.Text);

        vm.Title = "DC Title 2";
        Assert.Equal("DC Title 2", textBlock.Text);
    }

    [Fact]
    public void DataBinding_DataContext_TwoWay()
    {
        var panel = new StackPanel();
        var vm = new BindingTestViewModel { Title = "DC TwoWay Initial" };
        panel.DataContext = vm;

        var textBox = new TextBox();
        textBox.BindText<BindingTestViewModel>(x => x.Title, (m, v) => m.Title = v);
        panel.Add(textBox);

        Assert.Equal("DC TwoWay Initial", textBox.Text);

        textBox.Text = "DC User Edited";
        Assert.Equal("DC User Edited", vm.Title);
    }

    [Fact]
    public void DataBinding_DataContext_SwitchViewModel()
    {
        var panel = new StackPanel();
        var vm1 = new BindingTestViewModel { Title = "First VM" };
        panel.DataContext = vm1;

        var textBox = new TextBox();
        textBox.BindText<BindingTestViewModel>(x => x.Title, (m, v) => m.Title = v);
        panel.Add(textBox);

        Assert.Equal("First VM", textBox.Text);

        var vm2 = new BindingTestViewModel { Title = "Second VM" };
        panel.DataContext = vm2;

        Assert.Equal("Second VM", textBox.Text);

        textBox.Text = "Updated On Second VM";
        Assert.Equal("Updated On Second VM", vm2.Title);
        Assert.Equal("First VM", vm1.Title); // First VM unchanged
    }

    #endregion

    #region CSS-like Style System Tests

    [Fact]
    public void Style_TargetType_DefaultStyleApplies()
    {
        var panel = new StackPanel();
        var defaultTbStyle = new Style(typeof(TextBlock))
            .Set(TextBlock.FontSizeProperty, 21f);
        panel.Styles.Add(defaultTbStyle);

        var tb = new TextBlock("Default Styled");
        panel.Add(tb);

        Assert.Equal(21f, tb.FontSize);
    }

    [Fact]
    public void Style_StyleKey_AppliesSpecificHeadingStyles()
    {
        var panel = new StackPanel();

        var h1 = new Style("Heading1", typeof(TextBlock))
            .Set(TextBlock.FontSizeProperty, 28f)
            .Set(TextBlock.BoldProperty, true);

        var h2 = new Style("Heading2", typeof(TextBlock))
            .Set(TextBlock.FontSizeProperty, 22f)
            .Set(TextBlock.BoldProperty, true);

        var h3 = new Style("Heading3", typeof(TextBlock))
            .Set(TextBlock.FontSizeProperty, 18f);

        panel.Styles.Add(h1);
        panel.Styles.Add(h2);
        panel.Styles.Add(h3);

        var tb1 = new TextBlock("Title").StyleKey("Heading1");
        var tb2 = new TextBlock("Subtitle").StyleKey("Heading2");
        var tb3 = new TextBlock("Section").StyleKey("Heading3");
        var tbNormal = new TextBlock("Normal text");

        panel.Add(tb1);
        panel.Add(tb2);
        panel.Add(tb3);
        panel.Add(tbNormal);

        Assert.Equal(28f, tb1.FontSize);
        Assert.True(tb1.Bold);

        Assert.Equal(22f, tb2.FontSize);
        Assert.True(tb2.Bold);

        Assert.Equal(18f, tb3.FontSize);
        Assert.False(tb3.Bold);

        Assert.Equal(14f, tbNormal.FontSize); // Default
    }

    [Fact]
    public void Style_BasedOn_CascadesSetters()
    {
        var panel = new StackPanel();

        var baseStyle = new Style("HeadingBase", typeof(TextBlock))
            .Set(TextBlock.ForegroundProperty, Color.Blue)
            .Set(TextBlock.BoldProperty, true);

        var h1 = new Style("Heading1", typeof(TextBlock), basedOn: baseStyle)
            .Set(TextBlock.FontSizeProperty, 30f);

        var h2 = new Style("Heading2", typeof(TextBlock), basedOn: baseStyle)
            .Set(TextBlock.FontSizeProperty, 24f);

        panel.Styles.Add(baseStyle);
        panel.Styles.Add(h1);
        panel.Styles.Add(h2);

        var tb1 = new TextBlock("H1").StyleKey("Heading1");
        var tb2 = new TextBlock("H2").StyleKey("Heading2");

        panel.Add(tb1);
        panel.Add(tb2);

        Assert.Equal(30f, tb1.FontSize);
        Assert.Equal(Color.Blue, tb1.Foreground);
        Assert.True(tb1.Bold);

        Assert.Equal(24f, tb2.FontSize);
        Assert.Equal(Color.Blue, tb2.Foreground);
        Assert.True(tb2.Bold);
    }

    [Fact]
    public void Style_LocalValue_BeatsStyleValue()
    {
        var panel = new StackPanel();
        var style = new Style("H1", typeof(TextBlock))
            .Set(TextBlock.FontSizeProperty, 28f);
        panel.Styles.Add(style);

        var tb = new TextBlock("Explicit").StyleKey("H1");
        tb.FontSize = 44f; // Local value
        panel.Add(tb);

        Assert.Equal(44f, tb.FontSize);

        tb.ClearValue(TextBlock.FontSizeProperty);
        Assert.Equal(28f, tb.FontSize); // Falls back to styled value
    }

    [Fact]
    public void Style_StyleValue_BeatsInheritedValue()
    {
        var panel = new StackPanel();
        panel.SetValue(Control.FontSizeProperty, 16f); // Inherited from panel

        var style = new Style("BigHeading", typeof(TextBlock))
            .Set(TextBlock.FontSizeProperty, 36f);
        panel.Styles.Add(style);

        var tb = new TextBlock("Styled Child").StyleKey("BigHeading");
        panel.Add(tb);

        Assert.Equal(36f, tb.FontSize); // Style beats inherited!
    }

    [Fact]
    public void Style_DirectStyleAssignment()
    {
        var customStyle = new Style(typeof(TextBlock))
            .Set(TextBlock.FontSizeProperty, 33f)
            .Set(TextBlock.ForegroundProperty, Color.Red);

        var tb = new TextBlock("Direct").Style(customStyle);

        Assert.Equal(33f, tb.FontSize);
        Assert.Equal(Color.Red, tb.Foreground);
    }

    [Fact]
    public void Style_GlobalStyles()
    {
        StyleManager.GlobalStyles.Add(new Style("GlobalBadge", typeof(TextBlock))
            .Set(TextBlock.FontSizeProperty, 11f)
            .Set(TextBlock.BoldProperty, true));

        var tb = new TextBlock("Badge").StyleKey("GlobalBadge");
        var panel = new StackPanel();
        panel.Add(tb);

        Assert.Equal(11f, tb.FontSize);
        Assert.True(tb.Bold);
    }

    [Fact]
    public void Style_RuntimeStyleKeyChange_UpdatesProperties()
    {
        var panel = new StackPanel();
        panel.Styles.Add(new Style("H1", typeof(TextBlock)).Set(TextBlock.FontSizeProperty, 28f));
        panel.Styles.Add(new Style("H2", typeof(TextBlock)).Set(TextBlock.FontSizeProperty, 20f));

        var tb = new TextBlock("Dynamic").StyleKey("H1");
        panel.Add(tb);
        Assert.Equal(28f, tb.FontSize);

        tb.StyleKey = "H2";
        Assert.Equal(20f, tb.FontSize);

        tb.StyleKey = null;
        Assert.Equal(14f, tb.FontSize); // Back to default
    }

    [Fact]
    public void Style_NestedContainerScoping_InnerOverridesOuter()
    {
        var outerPanel = new StackPanel();
        outerPanel.Styles.Add(new Style("AccentText", typeof(TextBlock)).Set(TextBlock.ForegroundProperty, Color.Blue));

        var innerPanel = new StackPanel();
        innerPanel.Styles.Add(new Style("AccentText", typeof(TextBlock)).Set(TextBlock.ForegroundProperty, Color.FromRgb(255, 0, 0))); // Red

        outerPanel.Add(innerPanel);

        var outerText = new TextBlock("Outer").StyleKey("AccentText");
        outerPanel.Add(outerText);

        var innerText = new TextBlock("Inner").StyleKey("AccentText");
        innerPanel.Add(innerText);

        Assert.Equal(Color.Blue, outerText.Foreground);
        Assert.Equal(Color.FromRgb(255, 0, 0), innerText.Foreground);
    }

    [Fact]
    public void Style_ControlLevelSetters_PaddingAndCornerRadius()
    {
        var panel = new StackPanel();
        var cardStyle = new Style("Card", typeof(Border))
            .Set(Border.PaddingProperty, new Thickness(24))
            .Set(Border.CornerRadiusProperty, new CornerRadius(16));
        panel.Styles.Add(cardStyle);

        var border = new Border().StyleKey("Card");
        panel.Add(border);

        Assert.Equal(new Thickness(24), border.Padding);
        Assert.Equal(new CornerRadius(16), border.CornerRadius);
    }

    [Fact]
    public void DataBinding_DataContext_TwoWay_SliderAndCheckBox()
    {
        var vm = new BindingTestViewModel { SliderValue = 75f, IsChecked = true };
        var panel = new StackPanel { DataContext = vm };

        var slider = new Slider().BindValue<BindingTestViewModel>(x => x.SliderValue, (m, v) => m.SliderValue = v);
        var checkBox = new CheckBox().BindIsChecked<BindingTestViewModel>(x => x.IsChecked, (m, v) => m.IsChecked = v);

        panel.Add(slider);
        panel.Add(checkBox);

        Assert.Equal(75f, slider.Value);
        Assert.True(checkBox.IsChecked);

        // Modify control values
        slider.Value = 30f;
        checkBox.IsChecked = false;

        Assert.Equal(30f, vm.SliderValue);
        Assert.False(vm.IsChecked);

        // Modify ViewModel values
        vm.SliderValue = 90f;
        vm.IsChecked = true;

        Assert.Equal(90f, slider.Value);
        Assert.True(checkBox.IsChecked);
    }

    [Fact]
    public void DataBinding_UpdateSourceTrigger_LostFocus_SourceUpdatedOnlyWhenFocusLost()
    {
        var vm = new BindingTestViewModel { Title = "Initial Text" };
        var textBox = new TextBox();
        textBox.BindText(vm, x => x.Title, (m, v) => m.Title = v, UpdateSourceTrigger.LostFocus);

        var otherBox = new TextBox();

        var panel = new StackPanel();
        panel.Add(textBox);
        panel.Add(otherBox);

        // Focus textBox
        FocusManager.SetFocus(textBox);
        Assert.Same(textBox, FocusManager.CurrentFocused);

        // Simulate typing
        textBox.Text = "Typing in progress...";

        // Source VM must NOT be updated yet!
        Assert.Equal("Initial Text", vm.Title);

        // Move focus away to otherBox
        FocusManager.SetFocus(otherBox);
        Assert.Same(otherBox, FocusManager.CurrentFocused);

        // Source VM must NOW be updated!
        Assert.Equal("Typing in progress...", vm.Title);
    }

    [Fact]
    public void DataBinding_UpdateSourceTrigger_LostFocus_WithDataContext()
    {
        var vm = new BindingTestViewModel { Title = "DC Initial" };
        var panel = new StackPanel { DataContext = vm };

        var textBox = new TextBox().BindText<BindingTestViewModel>(x => x.Title, (m, v) => m.Title = v, UpdateSourceTrigger.LostFocus);
        var button = new Button();
        panel.Add(textBox);
        panel.Add(button);

        // Focus textBox
        FocusManager.SetFocus(textBox);
        Assert.Equal("DC Initial", textBox.Text);

        // Type new content
        textBox.Text = "Modified but not committed";
        Assert.Equal("DC Initial", vm.Title); // Still uncommitted

        // Move focus to button
        FocusManager.SetFocus(button);

        // Committed now!
        Assert.Equal("Modified but not committed", vm.Title);
    }

    #endregion
}
