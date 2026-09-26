using System.ComponentModel;
using Xunit;
using Atelier.Controls;
using Atelier.Markup;

namespace Atelier.Tests;

public class ReadCountingViewModel : INotifyPropertyChanged
{
    private string _title = "Title";
    private string _other = "Other";

    public event PropertyChangedEventHandler? PropertyChanged;

    public int TitleReads { get; private set; }

    public string Title
    {
        get { TitleReads++; return _title; }
        set { _title = value; Raise(nameof(Title)); }
    }

    public string Other
    {
        get => _other;
        set { _other = value; Raise(nameof(Other)); }
    }

    public ReadCountingViewModel Self => this;

    public void Raise(string? propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public class BindingSourceFilterTests
{
    [Fact]
    public void SimpleGetter_IgnoresChangesToOtherProperties()
    {
        var vm = new ReadCountingViewModel();
        var tb = new TextBlock();
        tb.SetBinding(TextBlock.TextProperty, vm, x => x.Title);
        int readsAfterBind = vm.TitleReads;

        vm.Other = "changed";

        Assert.Equal(readsAfterBind, vm.TitleReads);
    }

    [Fact]
    public void SimpleGetter_UpdatesOnItsPropertyAndOnEmptyName()
    {
        var vm = new ReadCountingViewModel();
        var tb = new TextBlock();
        tb.SetBinding(TextBlock.TextProperty, vm, x => x.Title);

        vm.Title = "New";
        Assert.Equal("New", tb.Text);

        int reads = vm.TitleReads;
        vm.Raise(string.Empty);
        vm.Raise(null);
        Assert.Equal(reads + 2, vm.TitleReads);
    }

    [Fact]
    public void MemberChain_FiltersOnFirstMember()
    {
        var vm = new ReadCountingViewModel();
        var tb = new TextBlock();
        tb.SetBinding(TextBlock.TextProperty, vm, x => x.Self.Title);
        int reads = vm.TitleReads;

        vm.Other = "changed";
        Assert.Equal(reads, vm.TitleReads);

        vm.Raise(nameof(ReadCountingViewModel.Self));
        Assert.Equal(reads + 1, vm.TitleReads);
    }

    [Fact]
    public void ComplexGetter_UpdatesOnEveryChange()
    {
        var vm = new ReadCountingViewModel();
        var tb = new TextBlock();
        tb.SetBinding(TextBlock.TextProperty, vm, x => x.Title + " / " + x.Other);

        vm.Other = "changed";

        Assert.Equal("Title / changed", tb.Text);
    }

    [Fact]
    public void GetterPassedAsDelegateVariable_UpdatesOnEveryChange()
    {
        var vm = new ReadCountingViewModel();
        var tb = new TextBlock();
        System.Func<ReadCountingViewModel, string> getter = x => x.Title;
        tb.SetBinding(TextBlock.TextProperty, vm, getter);
        int reads = vm.TitleReads;

        vm.Other = "changed";

        Assert.Equal(reads + 1, vm.TitleReads);
    }

    [Fact]
    public void MarkupHelper_ForwardsGetterExpression()
    {
        var vm = new ReadCountingViewModel();
        var tb = new TextBlock().BindText(vm, x => x.Title);
        int reads = vm.TitleReads;

        vm.Other = "changed";
        Assert.Equal(reads, vm.TitleReads);

        vm.Title = "Via markup";
        Assert.Equal("Via markup", tb.Text);
    }

    [Fact]
    public void DataContextBinding_FiltersToo()
    {
        var vm = new ReadCountingViewModel();
        var tb = new TextBlock { DataContext = vm };
        tb.SetBinding<string, ReadCountingViewModel>(TextBlock.TextProperty, x => x.Title);
        int reads = vm.TitleReads;

        vm.Other = "changed";
        Assert.Equal(reads, vm.TitleReads);

        vm.Title = "DC";
        Assert.Equal("DC", tb.Text);
    }
}
