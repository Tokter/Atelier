using System;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Markup;

namespace Atelier.Tests;

public partial class TextBoxMD3Tests
{
    [Fact]
    public void TextBox_DefaultProperties_AreCorrect()
    {
        var tb = new TextBox();

        Assert.Equal(TextBoxVariant.Outlined, tb.Variant);
        Assert.Equal(string.Empty, tb.Label);
        Assert.Equal(MaterialIconKind.None, tb.LeadingIconKind);
        Assert.Equal(string.Empty, tb.SupportingText);
        Assert.False(tb.HasLabel);
        Assert.False(tb.HasLeadingIcon);
        Assert.False(tb.HasSupportingText);
        Assert.Equal(0f, tb.LabelAnimationProgress);
        Assert.True(tb.IsEnabled);
    }

    [Fact]
    public void TextBox_WithInitialText_SetsFloatingLabelProgress()
    {
        var tb = new TextBox("user@example.com") { Label = "Email Address" };
        Assert.Equal(1f, tb.LabelAnimationProgress);
    }

    [Fact]
    public void TextBox_Focus_UpdatesFloatingLabelProgress()
    {
        var tb = new TextBox { Label = "Full Name" };
        Assert.Equal(0f, tb.LabelAnimationProgress);

        tb.OnGotFocus();
        Assert.Equal(1f, tb.LabelAnimationProgress);

        tb.OnLostFocus();
        Assert.Equal(0f, tb.LabelAnimationProgress);
    }

    [Fact]
    public void TextBox_TextChange_UpdatesFloatingLabelProgress()
    {
        var tb = new TextBox { Label = "Full Name" };
        Assert.Equal(0f, tb.LabelAnimationProgress);

        tb.Text = "Jane";
        Assert.Equal(1f, tb.LabelAnimationProgress);

        tb.Text = string.Empty;
        Assert.Equal(0f, tb.LabelAnimationProgress);
    }

    [Fact]
    public void TextBox_LeadingIcon_OffsetsTextStartX()
    {
        var tb = new TextBox();
        float startXWithoutIcon = tb.GetTextContentStartX();

        tb.LeadingIconKind = MaterialIconKind.Search;
        float startXWithIcon = tb.GetTextContentStartX();

        Assert.Equal(startXWithoutIcon + 32f, startXWithIcon);
        Assert.True(tb.HasLeadingIcon);
    }

    [Fact]
    public void TextBox_MeasureOverride_IncludesSupportingTextAndLabelHeight()
    {
        var tb = new TextBox { Label = "Username" };
        tb.Measure(new Size(500, 500));
        float hWithoutSupport = tb.DesiredSize.Height;
        Assert.Equal(56f, hWithoutSupport);

        tb.SupportingText = "Between 3 and 16 characters";
        tb.Measure(new Size(500, 500));
        float hWithSupport = tb.DesiredSize.Height;
        Assert.Equal(76f, hWithSupport);
    }

    [Fact]
    public void TextBox_MarkupExtensions_ChainCorrectly()
    {
        var tb = new TextBox()
            .Variant(TextBoxVariant.Filled)
            .Label("Password")
            .LeadingIcon(MaterialIconKind.Lock)
            .SupportingText("Minimum 8 characters")
            .Text("Secret123")
            .IsReadOnly(true);

        Assert.Equal(TextBoxVariant.Filled, tb.Variant);
        Assert.Equal("Password", tb.Label);
        Assert.Equal(MaterialIconKind.Lock, tb.LeadingIconKind);
        Assert.Equal("Minimum 8 characters", tb.SupportingText);
        Assert.Equal("Secret123", tb.Text);
        Assert.True(tb.IsReadOnly);
    }

    [Fact]
    public void Button_PointerClick_ExecutesCommand()
    {
        bool executed = false;
        var btn = new Button("Click Me")
            .Command(new Atelier.Core.Keybinding.AtelierRelayCommand(() => executed = true));
        btn.OnPointerEntered(new Atelier.Core.Events.PointerEventArgs(Point.Zero, Point.Zero));
        btn.OnPointerPressed(new Atelier.Core.Events.PointerEventArgs(Point.Zero, Point.Zero, Atelier.Core.Events.PointerButtons.Left));
        btn.OnPointerReleased(new Atelier.Core.Events.PointerEventArgs(Point.Zero, Point.Zero, Atelier.Core.Events.PointerButtons.Left));
        Assert.True(executed);
    }

    [Fact]
    public void Button_PointerClickOnChildTextBlock_ExecutesCommand()
    {
        bool executed = false;
        var btn = new Button("Click Me")
            .Command(new Atelier.Core.Keybinding.AtelierRelayCommand(() => executed = true));
        var textBlock = (TextBlock)btn.Content!;

        // SilkWindow hover dispatch
        textBlock.DispatchBubblePointerEvent(new Atelier.Core.Events.PointerEventArgs(Point.Zero, Point.Zero), (el, e) => el.OnPointerEntered(e));

        // SilkWindow mouse down dispatch
        textBlock.DispatchBubblePointerEvent(new Atelier.Core.Events.PointerEventArgs(Point.Zero, Point.Zero, Atelier.Core.Events.PointerButtons.Left), (el, e) => el.OnPointerPressed(e));

        // SilkWindow mouse up dispatch
        textBlock.DispatchBubblePointerEvent(new Atelier.Core.Events.PointerEventArgs(Point.Zero, Point.Zero, Atelier.Core.Events.PointerButtons.Left), (el, e) => el.OnPointerReleased(e));

        Assert.True(executed);
    }

    public partial class TestBindingVM : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string _username = "initial";

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void ResetDefaults()
        {
            Username = "reset_value";
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void Clear()
        {
            Username = "";
        }
    }

    [Fact]
    public void TextBox_BoundToViewModel_UpdatesOnCommand()
    {
        var vm = new TestBindingVM();
        var tb = new TextBox().BindText(vm, x => x.Username, (m, v) => m.Username = v);
        Assert.Equal("initial", tb.Text);

        tb.Text = "user_typed";
        Assert.Equal("user_typed", vm.Username);

        vm.ResetDefaultsCommand.Execute(null);
        Assert.Equal("reset_value", vm.Username);
        Assert.Equal("reset_value", tb.Text);

        vm.ClearCommand.Execute(null);
        Assert.Equal("", vm.Username);
        Assert.Equal("", tb.Text);
    }

    public partial class TestControlBindingVM : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private bool _isChecked = true;

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private string _option = "Option A";

        [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
        private bool _switchState = false;

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void ResetDefaults()
        {
            IsChecked = true;
            Option = "Option A";
            SwitchState = true;
        }

        [CommunityToolkit.Mvvm.Input.RelayCommand]
        private void Clear()
        {
            IsChecked = false;
            Option = "";
            SwitchState = false;
        }
    }

    [Fact]
    public void Controls_BoundToViewModel_UpdateOnCommands()
    {
        var vm = new TestControlBindingVM();
        var cb = new CheckBox("Test").BindIsChecked(vm, x => x.IsChecked, (m, v) => m.IsChecked = v);
        var rbA = new RadioButton("A").BindIsChecked(vm, x => x.Option, (m, v) => m.Option = v, "Option A");
        var rbB = new RadioButton("B").BindIsChecked(vm, x => x.Option, (m, v) => m.Option = v, "Option B");
        var sw = new Switch("SW").BindIsChecked(vm, x => x.SwitchState, (m, v) => m.SwitchState = v);

        Assert.True(cb.IsChecked);
        Assert.True(rbA.IsChecked);
        Assert.False(rbB.IsChecked);
        Assert.False(sw.IsChecked);

        // User interacts with controls
        cb.IsChecked = false;
        rbB.IsChecked = true;
        sw.IsChecked = true;

        Assert.False(vm.IsChecked);
        Assert.Equal("Option B", vm.Option);
        Assert.True(vm.SwitchState);

        // Execute ResetDefaultsCommand
        vm.ResetDefaultsCommand.Execute(null);

        Assert.True(vm.IsChecked);
        Assert.True(cb.IsChecked);
        Assert.Equal("Option A", vm.Option);
        Assert.True(rbA.IsChecked);
        Assert.False(rbB.IsChecked);
        Assert.True(vm.SwitchState);
        Assert.True(sw.IsChecked);

        // Execute ClearCommand
        vm.ClearCommand.Execute(null);

        Assert.False(vm.IsChecked);
        Assert.False(cb.IsChecked);
        Assert.Equal("", vm.Option);
        Assert.False(rbA.IsChecked);
        Assert.False(rbB.IsChecked);
        Assert.False(vm.SwitchState);
        Assert.False(sw.IsChecked);
    }
}
