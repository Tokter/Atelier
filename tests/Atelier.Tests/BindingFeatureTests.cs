using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Properties;

namespace Atelier.Tests;

public class Customer
{
    public string Name { get; set; } = "Ada";
}

public class OrderViewModel : INotifyPropertyChanged, INotifyDataErrorInfo
{
    private string? _email = "ada@example.com";
    private Customer? _customer = new();
    private readonly Dictionary<string, List<string>> _errors = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    public string? Email
    {
        get => _email;
        set { _email = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Email))); }
    }

    public Customer? Customer
    {
        get => _customer;
        set { _customer = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Customer))); }
    }

    public bool HasErrors => _errors.Count > 0;

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName != null && _errors.TryGetValue(propertyName, out var list) ? list : Array.Empty<string>();

    public void SetErrors(string propertyName, params string[] errors)
    {
        if (errors.Length == 0) _errors.Remove(propertyName);
        else _errors[propertyName] = new List<string>(errors);
        ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
    }
}

public class BindingFeatureTests
{
    [Fact]
    public void OneTime_IgnoresLaterSourceChanges()
    {
        var vm = new OrderViewModel();
        var tb = new TextBlock();
        tb.SetBinding(TextBlock.TextProperty, vm, x => x.Email!, options: new BindingOptions<string> { Mode = BindingMode.OneTime });

        vm.Email = "changed@example.com";

        Assert.Equal("ada@example.com", tb.Text);
    }

    [Fact]
    public void OneTime_DataContextBinding_RereadsWhenDataContextChanges()
    {
        var tb = new TextBlock { DataContext = new OrderViewModel() };
        tb.SetBinding<string, OrderViewModel>(TextBlock.TextProperty, x => x.Email!, options: new BindingOptions<string> { Mode = BindingMode.OneTime });

        tb.DataContext = new OrderViewModel { Email = "second@example.com" };

        Assert.Equal("second@example.com", tb.Text);
    }

    [Fact]
    public void OneWay_WithSetter_DoesNotWriteBack()
    {
        var vm = new OrderViewModel();
        var box = new TextBox();
        box.SetBinding(TextBox.TextProperty, vm, x => x.Email!, (x, v) => x.Email = v,
            options: new BindingOptions<string> { Mode = BindingMode.OneWay });

        box.Text = "typed";

        Assert.Equal("ada@example.com", vm.Email);
    }

    [Fact]
    public void TwoWay_WithoutSetter_Throws()
    {
        var vm = new OrderViewModel();
        Assert.Throws<ArgumentException>(() => new TextBox().SetBinding(TextBox.TextProperty, vm, x => x.Email!,
            options: new BindingOptions<string> { Mode = BindingMode.TwoWay }));
    }

    [Fact]
    public void OneWayToSource_PushesTargetValue_AndIgnoresSource()
    {
        var vm = new OrderViewModel();
        var box = new TextBox { Text = "initial" };
        box.SetBinding(TextBox.TextProperty, vm, x => x.Email!, (x, v) => x.Email = v,
            options: new BindingOptions<string> { Mode = BindingMode.OneWayToSource });
        Assert.Equal("initial", vm.Email);

        vm.Email = "from source";
        Assert.Equal("initial", box.Text);

        box.Text = "typed";
        Assert.Equal("typed", vm.Email);
    }

    [Fact]
    public void FallbackValue_UsedWhenGetterThrows()
    {
        var vm = new OrderViewModel();
        var tb = new TextBlock();
        tb.SetBinding(TextBlock.TextProperty, vm, x => x.Customer!.Name, options: new BindingOptions<string> { FallbackValue = "(no customer)" });
        Assert.Equal("Ada", tb.Text);

        vm.Customer = null;

        Assert.Equal("(no customer)", tb.Text);
    }

    [Fact]
    public void GetterThrowing_WithoutFallback_StillPropagates()
    {
        var vm = new OrderViewModel { Customer = null };
        Assert.Throws<NullReferenceException>(() => new TextBlock().SetBinding(TextBlock.TextProperty, vm, x => x.Customer!.Name));
    }

    [Fact]
    public void FallbackValue_UsedWhenDataContextMissing()
    {
        var tb = new TextBlock();
        tb.SetBinding<string, OrderViewModel>(TextBlock.TextProperty, x => x.Email!, options: new BindingOptions<string> { FallbackValue = "—" });

        Assert.Equal("—", tb.Text);
    }

    [Fact]
    public void TargetNullValue_IsShownForNull_AndWrittenBackAsNull()
    {
        var vm = new OrderViewModel { Email = null };
        var box = new TextBox();
        box.SetBinding(TextBox.TextProperty, vm, x => x.Email!, (x, v) => x.Email = v,
            options: new BindingOptions<string> { TargetNullValue = "(none)" });
        Assert.Equal("(none)", box.Text);

        box.Text = "set@example.com";
        box.Text = "(none)";

        Assert.Null(vm.Email);
    }

    [Fact]
    public void ClearBinding_KeepsLastValue()
    {
        var vm = new OrderViewModel();
        var tb = new TextBlock();
        tb.SetBinding(TextBlock.TextProperty, vm, x => x.Email!);

        tb.ClearBinding(TextBlock.TextProperty);
        vm.Email = "after clear";

        Assert.Equal("ada@example.com", tb.Text);
    }

    [Fact]
    public void ValidationErrors_AppearOnTarget_AndInTextBox()
    {
        var vm = new OrderViewModel();
        var box = new TextBox { SupportingText = "We never share it" };
        box.SetBinding(TextBox.TextProperty, vm, x => x.Email!, (x, v) => x.Email = v);
        Assert.False(Validation.GetHasError(box));
        Assert.Equal("We never share it", box.DisplayedSupportingText);

        vm.SetErrors(nameof(OrderViewModel.Email), "Invalid email address");

        Assert.True(Validation.GetHasError(box));
        Assert.Equal(new object[] { "Invalid email address" }, Validation.GetErrors(box));
        Assert.True(box.HasValidationError);
        Assert.Equal("Invalid email address", box.DisplayedSupportingText);

        vm.SetErrors(nameof(OrderViewModel.Email));

        Assert.False(Validation.GetHasError(box));
        Assert.Equal("We never share it", box.DisplayedSupportingText);
    }

    [Fact]
    public void ValidationErrors_ForOtherProperties_AreIgnored_AndClearedWithBinding()
    {
        var vm = new OrderViewModel();
        var box = new TextBox();
        box.SetBinding(TextBox.TextProperty, vm, x => x.Email!, (x, v) => x.Email = v);

        vm.SetErrors(nameof(OrderViewModel.Customer), "Required");
        Assert.False(Validation.GetHasError(box));

        vm.SetErrors(nameof(OrderViewModel.Email), "Invalid");
        box.ClearBinding(TextBox.TextProperty);
        Assert.False(Validation.GetHasError(box));
    }

    [Fact]
    public void MultiBinding_UpdatesWhenAnySourceChanges()
    {
        var order = new OrderViewModel();
        var other = new BindingTestViewModel { Title = "VIP" };
        var tb = new TextBlock();
        tb.SetMultiBinding(TextBlock.TextProperty, () => $"{order.Email} ({other.Title})", order, other);
        Assert.Equal("ada@example.com (VIP)", tb.Text);

        order.Email = "b@example.com";
        Assert.Equal("b@example.com (VIP)", tb.Text);

        other.Title = "Regular";
        Assert.Equal("b@example.com (Regular)", tb.Text);
    }
}
