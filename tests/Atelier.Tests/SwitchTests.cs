using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Tests;

public enum TestMode
{
    Standard,
    Pro,
    Enterprise
}

public partial class SwitchTestViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _notificationsEnabled = false;

    [ObservableProperty]
    private bool _canInteract = true;

    [ObservableProperty]
    private TestMode _selectedMode = TestMode.Standard;
}

public class SwitchTests
{
    private static void SimulateClick(UIElement element)
    {
        var pos = new Point(10, 10);
        element.OnPointerEntered(new PointerEventArgs(pos));
        element.OnPointerPressed(new PointerEventArgs(pos, PointerButtons.Left));
        element.OnPointerReleased(new PointerEventArgs(pos, PointerButtons.Left));
    }

    [Fact]
    public void Switch_DefaultValues_AreCorrect()
    {
        var sw = new Switch();

        Assert.False(sw.IsChecked);
        Assert.False(sw.ShowThumbIcon);
        Assert.Equal(0f, sw.ThumbAnimationProgress);
        Assert.Null(sw.Content);
        Assert.True(sw.IsEnabled);
    }

    [Fact]
    public void Switch_ToggleIsChecked_FiresCheckedChanged()
    {
        var sw = new Switch();
        bool eventFired = false;
        bool? receivedNew = null;

        sw.CheckedChanged += (sender, isChecked) =>
        {
            eventFired = true;
            receivedNew = isChecked;
        };

        sw.IsChecked = true;

        Assert.True(eventFired);
        Assert.True(receivedNew);
        Assert.True(sw.IsChecked);
    }

    [Fact]
    public void Switch_PointerClick_TogglesState()
    {
        var sw = new Switch();
        Assert.False(sw.IsChecked);

        SimulateClick(sw);
        Assert.True(sw.IsChecked);

        SimulateClick(sw);
        Assert.False(sw.IsChecked);
    }

    [Fact]
    public void Switch_WhenDisabled_PointerClickDoesNotToggle()
    {
        var sw = new Switch();
        sw.IsEnabled = false;

        SimulateClick(sw);
        Assert.False(sw.IsChecked);
    }

    [Fact]
    public void Switch_KeyDown_SpaceOrEnter_TogglesState()
    {
        var sw = new Switch();

        // Space toggles on release, like WPF
        var spaceArgs = new KeyEventArgs(Key.Space, 0, ModifierKeys.None, true);
        sw.OnKeyDown(spaceArgs);
        Assert.False(sw.IsChecked);
        Assert.True(spaceArgs.Handled);
        var spaceUpArgs = new KeyEventArgs(Key.Space, 0, ModifierKeys.None, false);
        sw.OnKeyUp(spaceUpArgs);
        Assert.True(sw.IsChecked);
        Assert.True(spaceUpArgs.Handled);

        // Enter
        var enterArgs = new KeyEventArgs(Key.Enter, 0, ModifierKeys.None, true);
        sw.OnKeyDown(enterArgs);
        Assert.False(sw.IsChecked);
        Assert.True(enterArgs.Handled);

        // Other key does not toggle
        var tabArgs = new KeyEventArgs(Key.Tab, 0, ModifierKeys.None, true);
        sw.OnKeyDown(tabArgs);
        Assert.False(sw.IsChecked);
        Assert.False(tabArgs.Handled);
    }

    [Fact]
    public void Switch_WhenDisabled_KeyDownDoesNotToggle()
    {
        var sw = new Switch();
        sw.IsEnabled = false;

        var spaceArgs = new KeyEventArgs(Key.Space, 0, ModifierKeys.None, true);
        sw.OnKeyDown(spaceArgs);

        Assert.False(sw.IsChecked);
        Assert.False(spaceArgs.Handled);
    }

    [Fact]
    public void Switch_Measure_AccountsForTrackAndContent()
    {
        var sw = new Switch("Enable Dark Mode");
        sw.Measure(new Size(500, 500));

        // Compact desktop switch track is 40x22, with content and 10 spacing, desired width should be > 40 and height >= 22
        Assert.True(sw.DesiredSize.Width > 40);
        Assert.True(sw.DesiredSize.Height >= 22);
    }

    [Fact]
    public void Switch_MarkupExtensions_ChainProperly()
    {
        var contentBlock = new TextBlock("Custom Child");
        var sw = new Switch()
            .IsChecked(true)
            .ShowThumbIcon(true)
            .Content(contentBlock);

        Assert.True(sw.IsChecked);
        Assert.True(sw.ShowThumbIcon);
        Assert.Same(contentBlock, sw.Content);
    }

    [Fact]
    public void Switch_TwoWayDataBinding_SynchronizesWithViewModel()
    {
        var vm = new SwitchTestViewModel { NotificationsEnabled = false };
        var sw = new Switch()
            .BindIsChecked(vm, x => x.NotificationsEnabled, (vmodel, val) => vmodel.NotificationsEnabled = val);

        Assert.False(sw.IsChecked);

        // Changing ViewModel updates Switch
        vm.NotificationsEnabled = true;
        Assert.True(sw.IsChecked);

        // Toggling Switch updates ViewModel
        SimulateClick(sw);
        Assert.False(vm.NotificationsEnabled);
        Assert.False(sw.IsChecked);
    }

    [Fact]
    public void RadioButton_EnumDataBinding_SynchronizesBothWays()
    {
        var vm = new SwitchTestViewModel { SelectedMode = TestMode.Pro };

        var rbStandard = new RadioButton("Standard")
            .BindIsChecked(vm, x => x.SelectedMode, (vmodel, val) => vmodel.SelectedMode = val, TestMode.Standard);

        var rbPro = new RadioButton("Pro")
            .BindIsChecked(vm, x => x.SelectedMode, (vmodel, val) => vmodel.SelectedMode = val, TestMode.Pro);

        var rbEnterprise = new RadioButton("Enterprise")
            .BindIsChecked(vm, x => x.SelectedMode, (vmodel, val) => vmodel.SelectedMode = val, TestMode.Enterprise);

        // Initially Pro should be selected
        Assert.False(rbStandard.IsChecked);
        Assert.True(rbPro.IsChecked);
        Assert.False(rbEnterprise.IsChecked);

        // Select Enterprise via RadioButton
        SimulateClick(rbEnterprise);

        Assert.Equal(TestMode.Enterprise, vm.SelectedMode);
        Assert.False(rbStandard.IsChecked);
        Assert.False(rbPro.IsChecked);
        Assert.True(rbEnterprise.IsChecked);

        // Change ViewModel to Standard
        vm.SelectedMode = TestMode.Standard;
        Assert.True(rbStandard.IsChecked);
        Assert.False(rbPro.IsChecked);
        Assert.False(rbEnterprise.IsChecked);
    }

    [Fact]
    public void UIElement_BindIsEnabled_Synchronizes()
    {
        var vm = new SwitchTestViewModel { CanInteract = true };
        var sw = new Switch().BindIsEnabled(vm, x => x.CanInteract);
        var cb = new CheckBox().BindIsEnabled(vm, x => x.CanInteract);

        Assert.True(sw.IsEnabled);
        Assert.True(cb.IsEnabled);

        vm.CanInteract = false;
        Assert.False(sw.IsEnabled);
        Assert.False(cb.IsEnabled);
    }

    [Fact]
    public void Card_DefaultProperties_AndVariant_AreCorrect()
    {
        var card = new Card(CardVariant.Outlined);
        Assert.Equal(CardVariant.Outlined, card.Variant);
        Assert.Equal(12f, card.CornerRadius.TopLeft);
        Assert.Equal(16f, card.Padding.Left);

        card.Variant(CardVariant.Filled);
        Assert.Equal(CardVariant.Filled, card.Variant);

        card.Variant(CardVariant.Elevated);
        Assert.Equal(CardVariant.Elevated, card.Variant);
    }

    [Fact]
    public void TextBlock_Muted_SetsProperty()
    {
        var tb = new TextBlock("Sample text").Muted();
        Assert.True(tb.Muted);

        tb.Muted(false);
        Assert.False(tb.Muted);
    }

    [Fact]
    public void UIElement_IsEnabled_InheritsDownVisualTree()
    {
        var parentPanel = new StackPanel();
        var childBlock = new TextBlock("Child text");
        parentPanel.Add(childBlock);

        Assert.True(parentPanel.IsEnabled);
        Assert.True(childBlock.IsEnabled);

        parentPanel.IsEnabled = false;
        Assert.False(parentPanel.IsEnabled);
        Assert.False(childBlock.IsEnabled);

        parentPanel.IsEnabled = true;
        Assert.True(parentPanel.IsEnabled);
        Assert.True(childBlock.IsEnabled);
    }

    [Fact]
    public void Grid_RowAndColumnSpacing_AffectsMeasureAndArrange()
    {
        var grid = new Grid()
            .Columns(GridLength.Pixels(100), GridLength.Pixels(100))
            .Rows(GridLength.Pixels(50), GridLength.Pixels(50))
            .RowSpacing(10)
            .ColumnSpacing(20);

        var c00 = new Border().Row(0).Column(0);
        var c01 = new Border().Row(0).Column(1);
        var c10 = new Border().Row(1).Column(0);
        var c11 = new Border().Row(1).Column(1);

        grid.Children(c00, c01, c10, c11);

        grid.Measure(new Size(500, 500));
        Assert.Equal(220f, grid.DesiredSize.Width);
        Assert.Equal(110f, grid.DesiredSize.Height);

        grid.Arrange(new Rect(0, 0, 220, 110));

        Assert.Equal(new Rect(0, 0, 100, 50), c00.Bounds);
        Assert.Equal(new Rect(120, 0, 100, 50), c01.Bounds);
        Assert.Equal(new Rect(0, 60, 100, 50), c10.Bounds);
        Assert.Equal(new Rect(120, 60, 100, 50), c11.Bounds);
    }

    [Fact]
    public void Switch_Padding_AffectsMeasureAndArrange()
    {
        var sw = new Switch();
        sw.Padding = new Thickness(5, 8);

        sw.Measure(new Size(500, 500));
        Assert.Equal(50f, sw.DesiredSize.Width);
        Assert.Equal(38f, sw.DesiredSize.Height);
    }
}
