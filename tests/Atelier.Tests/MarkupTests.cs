using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Tests;

public enum MarkupSize { Small, Medium, Large }

public partial class MarkupTestViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "Ada";

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool? _state;

    [ObservableProperty]
    private MarkupSize _size = MarkupSize.Medium;

    [ObservableProperty]
    private string? _selected;

    [ObservableProperty]
    private float _padding = 4f;

    public ObservableCollection<string> Items { get; } = ["a", "b", "c"];
}

public class MarkupTests
{
    [Fact]
    public void Setters_ReturnTheCallersType()
    {
        // Each call keeps the concrete type, so control-specific methods remain available after general ones.
        Button button = new Button("Save")
            .Margin(4)
            .HorizontalAlignment(HorizontalAlignment.Center)
            .Padding(12, 6)
            .Variant(ButtonVariant.Tonal)
            .IsEnabled(false);

        Assert.Equal(new Thickness(4), button.Margin);
        Assert.Equal(HorizontalAlignment.Center, button.HorizontalAlignment);
        Assert.Equal(new Thickness(12, 6), button.Padding);
        Assert.Equal(ButtonVariant.Tonal, button.Variant);
        Assert.False(button.IsEnabled);

        Card card = new Card().Padding(8).CornerRadius(12).Variant(CardVariant.Outlined).Elevation(2);
        Assert.Equal(new Thickness(8), card.Padding);
        Assert.Equal(new CornerRadius(12), card.CornerRadius);
        Assert.Equal(CardVariant.Outlined, card.Variant);
    }

    [Fact]
    public void AttachedLayoutHelpers_SetTheAttachedProperties()
    {
        var cell = new TextBlock("x").Cell(1, 2, rowSpan: 3, columnSpan: 4);
        Assert.Equal(1, Grid.GetRow(cell));
        Assert.Equal(2, Grid.GetColumn(cell));
        Assert.Equal(3, Grid.GetRowSpan(cell));
        Assert.Equal(4, Grid.GetColumnSpan(cell));

        var positioned = new Border().CanvasPosition(10, 20);
        Assert.Equal(10f, positioned.GetValue(Canvas.LeftProperty));
        Assert.Equal(20f, positioned.GetValue(Canvas.TopProperty));

        var docked = new Border().Dock(Dock.Top);
        Assert.Equal(Dock.Top, docked.GetValue(DockPanel.DockProperty));
    }

    [Fact]
    public void TwoValueSpacing_IsHorizontalThenVertical_ForAllPanels()
    {
        var grid = new Grid().Spacing(10, 20);
        Assert.Equal(10f, grid.ColumnSpacing);
        Assert.Equal(20f, grid.RowSpacing);

        var wrap = new WrapPanel().Spacing(10, 20);
        Assert.Equal(10f, wrap.HorizontalSpacing);
        Assert.Equal(20f, wrap.VerticalSpacing);

        var uniform = new UniformGrid().Rows(2).Columns(3).Spacing(10, 20);
        Assert.Equal(10f, uniform.ColumnSpacing);
        Assert.Equal(20f, uniform.RowSpacing);
        Assert.Equal(3, uniform.Columns);

        var dock = new DockPanel().LastChildFill(false).Spacing(10, 20);
        Assert.Equal(10f, dock.HorizontalSpacing);
        Assert.Equal(20f, dock.VerticalSpacing);
        Assert.False(dock.LastChildFill);
    }

    [Fact]
    public void GridRowsAndColumns_ParseLengthLists()
    {
        var grid = new Grid().Rows("Auto,*,48").Columns(GridLength.Star, GridLength.Pixels(100));

        Assert.Equal(3, grid.RowDefinitions.Count);
        Assert.Equal(GridLength.Auto, grid.RowDefinitions[0].Height);
        Assert.Equal(2, grid.ColumnDefinitions.Count);
        Assert.Equal(GridLength.Pixels(100), grid.ColumnDefinitions[1].Width);
    }

    [Fact]
    public void DataContextBinding_InfersTypesFromTheLambdaParameter()
    {
        var vm = new MarkupTestViewModel();
        var panel = new StackPanel().DataContext(vm);
        var text = new TextBlock().BindText((MarkupTestViewModel m) => m.Name).Margin(2);
        var box = new TextBox().BindText((MarkupTestViewModel m) => m.Name, (m, value) => m.Name = value);
        panel.Children(text, box);

        Assert.Equal("Ada", text.Text);
        vm.Name = "Grace";
        Assert.Equal("Grace", text.Text);

        box.Text = "Linus";
        Assert.Equal("Linus", vm.Name);
        Assert.Equal("Linus", text.Text);
    }

    [Fact]
    public void GenericBind_WorksWithSourceAndDataContext()
    {
        var vm = new MarkupTestViewModel();

        var fromSource = new TextBlock().Bind(TextBlock.TextProperty, vm, m => m.Name);
        var fromContext = new TextBlock().DataContext(vm).Bind(TextBlock.TextProperty, (MarkupTestViewModel m) => m.Name.ToUpperInvariant());

        Assert.Equal("Ada", fromSource.Text);
        Assert.Equal("ADA", fromContext.Text);
    }

    [Fact]
    public void BindIsVisible_CollapsesAndWritesBack()
    {
        var vm = new MarkupTestViewModel();
        var element = new Border().BindIsVisible(vm, m => m.IsVisible, (m, value) => m.IsVisible = value);

        Assert.Equal(Visibility.Visible, element.Visibility);
        vm.IsVisible = false;
        Assert.Equal(Visibility.Collapsed, element.Visibility);

        element.Visibility = Visibility.Visible;
        Assert.True(vm.IsVisible);
    }

    [Fact]
    public void BindIsChecked_SupportsThreeStateValues()
    {
        var vm = new MarkupTestViewModel();
        var checkBox = new CheckBox().IsThreeState().BindIsChecked(vm, m => m.State, (m, value) => m.State = value);

        Assert.Null(checkBox.IsChecked);
        checkBox.IsChecked = true;
        Assert.True(vm.State);
        vm.State = null;
        Assert.Null(checkBox.IsChecked);
    }

    [Fact]
    public void RadioButtonBinding_ChecksTheMatchingOption()
    {
        var vm = new MarkupTestViewModel();
        var small = new RadioButton().GroupName("markup-size").BindIsChecked(vm, m => m.Size, (m, s) => m.Size = s, MarkupSize.Small);
        var medium = new RadioButton().GroupName("markup-size").BindIsChecked(vm, m => m.Size, (m, s) => m.Size = s, MarkupSize.Medium);

        Assert.False(small.IsChecked);
        Assert.True(medium.IsChecked);

        small.IsChecked = true;
        Assert.Equal(MarkupSize.Small, vm.Size);
    }

    [Fact]
    public void BindSelectedItem_IsTypedAndTwoWay()
    {
        var vm = new MarkupTestViewModel();
        var listBox = new ListBox()
            .BindItemsSource(vm, m => m.Items)
            .BindSelectedItem(vm, m => m.Selected, (m, item) => m.Selected = item);

        listBox.SelectedIndex = 1;
        Assert.Equal("b", vm.Selected);

        vm.Selected = "c";
        Assert.Equal("c", listBox.SelectedItem);
    }

    [Fact]
    public void TypedItemTemplate_FallsBackToTextForOtherItems()
    {
        var list = new ItemsControl().WithItemTemplate((string s) => new TextBlock(s.ToUpperInvariant()));

        var typed = Assert.IsType<TextBlock>(list.ItemTemplate!("abc"));
        Assert.Equal("ABC", typed.Text);
        var other = Assert.IsType<TextBlock>(list.ItemTemplate!(42));
        Assert.Equal("42", other.Text);
    }

    [Fact]
    public void UniformBorderBindings_ConvertFloats()
    {
        var vm = new MarkupTestViewModel();
        var border = new Border().BindPadding(vm, m => m.Padding).BindCornerRadius(vm, m => m.Padding);

        Assert.Equal(new Thickness(4), border.Padding);
        vm.Padding = 9;
        Assert.Equal(new Thickness(9), border.Padding);
        Assert.Equal(new CornerRadius(9), border.CornerRadius);
    }

    [Fact]
    public void EventHelpers_AcceptActions()
    {
        int clicks = 0;
        bool? lastState = false;
        var button = new Button("x").OnClick(() => clicks++);
        var toggle = new CheckBox().OnCheckedChanged(state => lastState = state);

        ButtonInput.Click(button);
        toggle.IsChecked = true;

        Assert.Equal(1, clicks);
        Assert.True(lastState);
    }

    [Fact]
    public void TreeHelpers_BuildItemTrees()
    {
        var root = new TreeViewItem("root").IsExpanded().ChildrenItems(new TreeViewItem("a"), new TreeViewItem("b"));
        var tree = new TreeView().RootItems(root);

        Assert.Same(root, Assert.Single(tree.RootItems));
        Assert.Equal(2, root.ChildrenItems.Count);
        Assert.True(root.IsExpanded);
    }

    [Fact]
    public void EveryPublicMarkupMethod_ReturnsItsReceiverType()
    {
        var methods = MarkupMethods().Where(m => m.Name != nameof(IconMarkup.ToIcon));

        foreach (var method in methods)
        {
            var receiver = method.GetParameters()[0].ParameterType;
            Assert.True(method.ReturnType == receiver, $"{method.DeclaringType!.Name}.{method.Name} returns {method.ReturnType.Name}, not its receiver type.");
        }
    }

    /// <summary>
    /// Guards the "no missing features" goal: every settable bindable property of a public element type has a markup
    /// method named after it (delegate-typed properties with a "With" prefix).
    /// </summary>
    [Fact]
    public void EverySettableBindableProperty_HasAMarkupMethod()
    {
        // Properties deliberately without a method: runtime state that the control or the host window owns.
        var skipped = new HashSet<string>
        {
            "ScrollViewer.ScrollOffsetX", "ScrollViewer.ScrollOffsetY",
            "TitleBar.IsMaximized", "TitleBarButton.IsCloseButton",
        };

        var methods = MarkupMethods().ToLookup(m => m.Name);
        var missing = new List<string>();

        foreach (var type in ElementTypes())
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (field.GetValue(null) is not BindableProperty property || property.IsReadOnly || property.IsAttached)
                {
                    continue;
                }

                var key = $"{type.Name}.{property.Name}";
                if (skipped.Contains(key))
                {
                    continue;
                }

                bool found = methods[property.Name].Concat(methods["With" + property.Name]).Any(m => AcceptsReceiver(m, type));
                if (!found)
                {
                    missing.Add(key);
                }
            }
        }

        Assert.True(missing.Count == 0, "Missing markup methods: " + string.Join(", ", missing));
    }

    private static IEnumerable<MethodInfo> MarkupMethods() =>
        typeof(MarkupExtensions).Assembly.GetExportedTypes()
            .Where(t => t.Namespace == "Atelier.Markup" && t.IsAbstract && t.IsSealed)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false));

    private static IEnumerable<Type> ElementTypes() =>
        new[] { typeof(UIElement).Assembly, typeof(Panel).Assembly, typeof(Control).Assembly }
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => typeof(VisualNode).IsAssignableFrom(t));

    private static bool AcceptsReceiver(MethodInfo method, Type type)
    {
        var receiver = method.GetParameters()[0].ParameterType;
        if (!receiver.IsGenericParameter)
        {
            return receiver.IsAssignableFrom(type);
        }

        return receiver.GetGenericParameterConstraints().All(c => c.IsAssignableFrom(type));
    }
}
