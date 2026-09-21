using System;
using System.Collections.Generic;
using System.ComponentModel;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Xunit;

namespace Atelier.Tests;

public class PopupAndComboBoxTests
{
    private class TestTarget : Control
    {
        public TestTarget(float x, float y, float w, float h)
        {
            Arrange(new Rect(x, y, w, h));
        }
    }

    private class TestViewModel : INotifyPropertyChanged
    {
        private string? _selectedCity;
        public string? SelectedCity
        {
            get => _selectedCity;
            set
            {
                if (_selectedCity != value)
                {
                    _selectedCity = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCity)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [Fact]
    public void Popup_DefaultProperties()
    {
        var popup = new Popup();
        Assert.False(popup.IsOpen);
        Assert.Equal(PlacementMode.Bottom, popup.Placement);
        Assert.False(popup.StaysOpen);
        Assert.Equal(6f, popup.Elevation);
        Assert.Equal(Rect.Zero, popup.ActualBounds);
    }

    [Fact]
    public void Popup_OpenAndClose_RegistersWithPopupManager()
    {
        PopupManager.CloseAllPopups();

        var popup = new Popup
        {
            Child = new TextBlock("Popup Content")
        };

        bool openedFired = false;
        bool closedFired = false;
        popup.Opened += (s, e) => openedFired = true;
        popup.Closed += (s, e) => closedFired = true;

        popup.IsOpen = true;
        Assert.True(popup.IsOpen);
        Assert.True(openedFired);
        Assert.Contains(popup, PopupManager.ActivePopups);

        popup.IsOpen = false;
        Assert.False(popup.IsOpen);
        Assert.True(closedFired);
        Assert.DoesNotContain(popup, PopupManager.ActivePopups);
    }

    [Fact]
    public void Popup_SmartPlacement_PositionsBelowTarget()
    {
        var target = new TestTarget(100, 100, 200, 40);
        var child = new Border { Width = 200, Height = 150 };

        var popup = new Popup
        {
            PlacementTarget = target,
            Placement = PlacementMode.Bottom,
            Child = child
        };

        var viewport = new Size(800, 600);
        var placement = popup.ComputeSmartPlacement(viewport);

        // Ideal position: X = 100, Y = 100 + 40 = 140, W = 200, H = 150
        Assert.Equal(100f, placement.X);
        Assert.Equal(140f, placement.Y);
        Assert.Equal(200f, placement.Width);
        Assert.Equal(150f, placement.Height);
    }

    [Fact]
    public void Popup_SmartPlacement_FlipsAboveWhenBottomOverflown()
    {
        // Target near bottom of 800x600 viewport: Y=500, H=40
        // Space below = 600 - (500 + 40) = 60. Space above = 500.
        // Popup height = 150. Overflow below! Flip above to Y = 500 - 150 = 350.
        var target = new TestTarget(100, 500, 200, 40);
        var child = new Border { Width = 200, Height = 150 };

        var popup = new Popup
        {
            PlacementTarget = target,
            Placement = PlacementMode.Bottom,
            Child = child
        };

        var viewport = new Size(800, 600);
        var placement = popup.ComputeSmartPlacement(viewport);

        Assert.Equal(100f, placement.X);
        Assert.Equal(350f, placement.Y);
        Assert.Equal(200f, placement.Width);
        Assert.Equal(150f, placement.Height);
    }

    [Fact]
    public void Popup_SmartPlacement_ClampsInsideViewport()
    {
        // Target near right edge: X=700, W=100. Viewport W=800.
        // Popup width = 200. X+W = 900 > 800. Clamps to X = 600.
        var target = new TestTarget(700, 100, 100, 40);
        var child = new Border { Width = 200, Height = 150 };

        var popup = new Popup
        {
            PlacementTarget = target,
            Placement = PlacementMode.Bottom,
            Child = child
        };

        var viewport = new Size(800, 600);
        var placement = popup.ComputeSmartPlacement(viewport);

        Assert.True(placement.Right <= 800f);
        Assert.Equal(600f, placement.X);
    }

    [Fact]
    public void Popup_HitTest_InsideAndOutside()
    {
        PopupManager.CloseAllPopups();

        var target = new TestTarget(100, 100, 200, 40);
        var innerButton = new Button { Width = 200, Height = 150 };

        var popup = new Popup
        {
            PlacementTarget = target,
            Placement = PlacementMode.Bottom,
            Child = innerButton
        };

        popup.IsOpen = true;
        popup.UpdatePlacement(new Size(800, 600));

        // Point inside popup bounds (100, 140, 200, 150)
        var hitInside = PopupManager.HitTest(new Point(150, 200));
        Assert.NotNull(hitInside);

        // Point outside popup
        var hitOutside = PopupManager.HitTest(new Point(50, 50));
        Assert.Null(hitOutside);

        popup.IsOpen = false;
    }

    [Fact]
    public void Popup_LightDismiss_ClickOutsideClosesPopupAndIntercepts()
    {
        PopupManager.CloseAllPopups();

        var target = new TestTarget(100, 100, 200, 40);
        var child = new Border { Width = 200, Height = 150 };

        var popup = new Popup
        {
            PlacementTarget = target,
            Placement = PlacementMode.Bottom,
            StaysOpen = false,
            Child = child
        };

        popup.IsOpen = true;
        popup.UpdatePlacement(new Size(800, 600));
        Assert.True(popup.IsOpen);

        // Click outside popup: should dismiss popup and return true (swallowing click)
        bool handled = PopupManager.HandleMouseDown(new Point(50, 50), PointerButtons.Left);
        Assert.True(handled);
        Assert.False(popup.IsOpen);
    }

    [Fact]
    public void Popup_EscapeKey_ClosesPopup()
    {
        PopupManager.CloseAllPopups();

        var target = new TestTarget(100, 100, 200, 40);
        var popup = new Popup
        {
            PlacementTarget = target,
            Child = new Border { Width = 100, Height = 100 },
            StaysOpen = false
        };

        popup.IsOpen = true;
        popup.UpdatePlacement(new Size(800, 600));
        Assert.True(popup.IsOpen);

        var keyEvent = new KeyEventArgs(Key.Escape, 0, ModifierKeys.None, true);
        bool handled = PopupManager.HandleKeyDown(keyEvent);

        Assert.True(handled);
        Assert.True(keyEvent.Handled);
        Assert.False(popup.IsOpen);
    }

    [Fact]
    public void ComboBox_ItemsAndSelection()
    {
        var cb = new ComboBox();
        cb.Items.Add("Tokyo");
        cb.Items.Add("Paris");
        cb.Items.Add("New York");

        Assert.Equal(-1, cb.SelectedIndex);
        Assert.Null(cb.SelectedItem);

        cb.SelectedIndex = 1;
        Assert.Equal("Paris", cb.SelectedItem);

        cb.SelectedItem = "New York";
        Assert.Equal(2, cb.SelectedIndex);

        cb.SelectedItem = null;
        Assert.Equal(-1, cb.SelectedIndex);
    }

    [Fact]
    public void ComboBox_OpenDropDown_TogglesInternalPopup()
    {
        var cb = new ComboBox();
        cb.Items.Add("Item 1");
        cb.Items.Add("Item 2");

        bool openedFired = false;
        bool closedFired = false;
        cb.DropDownOpened += (s, e) => openedFired = true;
        cb.DropDownClosed += (s, e) => closedFired = true;

        cb.IsDropDownOpen = true;
        Assert.True(cb.IsDropDownOpen);
        Assert.True(openedFired);

        cb.IsDropDownOpen = false;
        Assert.False(cb.IsDropDownOpen);
        Assert.True(closedFired);
    }

    [Fact]
    public void ComboBox_ItemTemplate_RendersCustomDisplay()
    {
        var cb = new ComboBox
        {
            ItemTemplate = item => new TextBlock($"City: {item}")
        };

        cb.Items.Add("Kyoto");
        cb.SelectedItem = "Kyoto";

        Assert.NotNull(cb.SelectionDisplayElement);
        Assert.IsType<TextBlock>(cb.SelectionDisplayElement);
        Assert.Equal("Kyoto", cb.SelectionDisplayElement.DataContext);
    }

    [Fact]
    public void ComboBox_KeyboardNavigation_WhenClosed()
    {
        var cb = new ComboBox();
        cb.Items.Add("A");
        cb.Items.Add("B");
        cb.Items.Add("C");

        // Down arrow advances selection when closed
        cb.OnKeyDown(new KeyEventArgs(Key.Down, 0, ModifierKeys.None, true));
        Assert.Equal("A", cb.SelectedItem);
        Assert.Equal(0, cb.SelectedIndex);

        cb.OnKeyDown(new KeyEventArgs(Key.Down, 0, ModifierKeys.None, true));
        Assert.Equal("B", cb.SelectedItem);
        Assert.Equal(1, cb.SelectedIndex);

        // Enter opens dropdown
        cb.OnKeyDown(new KeyEventArgs(Key.Enter, 0, ModifierKeys.None, true));
        Assert.True(cb.IsDropDownOpen);

        // Escape closes dropdown
        cb.OnKeyDown(new KeyEventArgs(Key.Escape, 0, ModifierKeys.None, true));
        Assert.False(cb.IsDropDownOpen);
    }

    [Fact]
    public void ComboBox_TwoWayDataBinding()
    {
        var vm = new TestViewModel { SelectedCity = "London" };
        var cb = new ComboBox();
        cb.Items.Add("London");
        cb.Items.Add("Berlin");
        cb.Items.Add("Madrid");

        cb.SetBinding(
            ComboBox.SelectedItemProperty,
            vm,
            m => m.SelectedCity,
            (m, v) => m.SelectedCity = (string?)v
        );

        // Initial bound value
        Assert.Equal("London", cb.SelectedItem);

        // UI change updates VM
        cb.SelectedItem = "Berlin";
        Assert.Equal("Berlin", vm.SelectedCity);

        // VM change updates UI
        vm.SelectedCity = "Madrid";
        Assert.Equal("Madrid", cb.SelectedItem);
        Assert.Equal(2, cb.SelectedIndex);
    }

    private class ElementTrackingPresenter : Atelier.Rendering.IElementVisualPresenter
    {
        public List<UIElement> RenderedElements { get; } = [];

        public void Render(UIElement element, ref Atelier.Rendering.DrawingContext context)
        {
            RenderedElements.Add(element);
        }
    }

    [Fact]
    public void ComboBox_WhenClosed_DoesNotRenderDropdownItemsInVisualTree()
    {
        var cb = new ComboBox();
        cb.Items.Add("Tokyo");
        cb.Items.Add("Paris");
        cb.Items.Add("London");

        var container = new StackPanel();
        container.Add(cb);

        container.Measure(new Size(800, 600));
        container.Arrange(new Rect(0, 0, 800, 600));

        // When closed, popup must be Collapsed and flagged as overlay
        Assert.False(cb.IsDropDownOpen);

        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(800, 600));
        var paintRegistry = new Atelier.Rendering.PaintRegistry();
        var context = new Atelier.Rendering.DrawingContext(surface.Canvas, paintRegistry);
        var presenter = new ElementTrackingPresenter();

        Atelier.Rendering.VisualTreeRenderer.Render(container, ref context, presenter);

        // Verify that only the ComboBox itself (and not the inner ListBox or ListBoxItems) was rendered
        Assert.Contains(cb, presenter.RenderedElements);
        Assert.DoesNotContain(presenter.RenderedElements, el => el is ListBox);
        Assert.DoesNotContain(presenter.RenderedElements, el => el is ListBoxItem);
    }

    [Fact]
    public void ComboBox_Open_MouseInteraction()
    {
        var cb = new ComboBox();
        cb.Items.Add("Tokyo");
        cb.Items.Add("Paris");
        cb.Items.Add("London");

        var container = new StackPanel();
        container.Add(cb);
        container.Measure(new Size(800, 600));
        container.Arrange(new Rect(0, 0, 800, 600));

        // Open dropdown
        cb.IsDropDownOpen = true;
        PopupManager.UpdatePopups(new Size(800, 600));

        var popup = PopupManager.TopmostPopup;
        Assert.NotNull(popup);
        Assert.False(popup.ActualBounds.IsEmpty);

        // Position over the first item
        var itemPoint = new Point(popup.ActualBounds.X + 20, popup.ActualBounds.Y + 15);
        var hit = PopupManager.HitTest(itemPoint);
        Assert.NotNull(hit);

        // Hover test
        UIElement? hovered = null;
        PopupManager.HandleMouseMove(itemPoint, ref hovered);
        Assert.NotNull(hovered);
        Assert.True(hovered.IsHovered, "Hovered element should have IsHovered = true");

        // Mouse Down test
        bool downHandled = PopupManager.HandleMouseDown(itemPoint, PointerButtons.Left);
        Assert.True(downHandled);
        Assert.True(cb.IsDropDownOpen, "ComboBox should remain open on mouse down over an item!");

        // Mouse Up test
        bool upHandled = PopupManager.HandleMouseUp(itemPoint, PointerButtons.Left);
        Assert.True(upHandled);
        Assert.Equal("Tokyo", cb.SelectedItem);
        Assert.False(cb.IsDropDownOpen, "ComboBox should close after item selection on mouse up!");
    }

    [Fact]
    public void ComboBox_WithItemTemplate_MouseInteraction()
    {
        var cb = new ComboBox
        {
            ItemTemplate = item =>
            {
                var dot = new Border { Width = 8, Height = 8 };
                var text = new TextBlock(item?.ToString() ?? string.Empty);
                var panel = new StackPanel { Orientation = Orientation.Horizontal };
                panel.Add(dot);
                panel.Add(text);
                return panel;
            }
        };
        cb.Items.Add("Alpha");
        cb.Items.Add("Beta");

        var container = new StackPanel();
        container.Add(cb);
        container.Measure(new Size(800, 600));
        container.Arrange(new Rect(0, 0, 800, 600));

        cb.IsDropDownOpen = true;
        PopupManager.UpdatePopups(new Size(800, 600));

        var popup = PopupManager.TopmostPopup;
        Assert.NotNull(popup);

        // Click on the second item ("Beta")
        var itemPoint = new Point(popup.ActualBounds.X + 30, popup.ActualBounds.Y + 45);
        var hit = PopupManager.HitTest(itemPoint);
        Assert.NotNull(hit);

        UIElement? hovered = null;
        PopupManager.HandleMouseMove(itemPoint, ref hovered);
        Assert.NotNull(hovered);
        Assert.True(hovered.IsHovered);

        PopupManager.HandleMouseDown(itemPoint, PointerButtons.Left);
        Assert.True(cb.IsDropDownOpen);

        PopupManager.HandleMouseUp(itemPoint, PointerButtons.Left);
        Assert.Equal("Beta", cb.SelectedItem);
        Assert.False(cb.IsDropDownOpen);
    }

    [Fact]
    public void ComboBox_ScrollViewer_ThumbDrag()
    {
        var cb = new ComboBox
        {
            MaxDropDownHeight = 100f
        };
        for (int i = 0; i < 20; i++)
        {
            cb.Items.Add($"Item {i}");
        }

        var container = new StackPanel();
        container.Add(cb);
        container.Measure(new Size(800, 600));
        container.Arrange(new Rect(100, 100, 800, 600));

        cb.IsDropDownOpen = true;
        PopupManager.UpdatePopups(new Size(800, 600));

        var popup = PopupManager.TopmostPopup;
        Assert.NotNull(popup);

        // Find the ListBox's scrollviewer
        var border = popup.Child as Border;
        var listBox = border?.Child as ListBox;
        Assert.NotNull(listBox);
        var scrollViewer = listBox.ScrollViewer;
        Assert.True(scrollViewer.CanScrollVertically);
        Assert.True(scrollViewer.MaxScrollY > 0);

        // Locate vertical thumb in screen coordinates
        var vThumb = scrollViewer.GetVerticalThumbRect();
        var thumbScreenX = popup.ActualBounds.X + vThumb.X + vThumb.Width * 0.5f;
        var thumbScreenY = popup.ActualBounds.Y + vThumb.Y + vThumb.Height * 0.5f;
        var thumbPoint = new Point(thumbScreenX, thumbScreenY);

        // Press down on thumb
        bool downHandled = PopupManager.HandleMouseDown(thumbPoint, PointerButtons.Left);
        Assert.True(downHandled);
        Assert.True(scrollViewer.IsVerticalThumbDragging);
        Assert.Same(scrollViewer, UIElement.CapturedElement);

        // Drag down by 40 pixels
        var dragPoint = new Point(thumbScreenX, thumbScreenY + 40f);
        var moveE = new PointerEventArgs(dragPoint, dragPoint);
        scrollViewer.DispatchBubblePointerEvent(moveE, (el, localE) => el.OnPointerMoved(localE));

        Assert.True(scrollViewer.ScrollOffsetY > 0, "ScrollOffsetY should have increased after dragging thumb down!");

        // Release mouse
        bool upHandled = PopupManager.HandleMouseUp(dragPoint, PointerButtons.Left);
        Assert.True(upHandled);
        Assert.False(scrollViewer.IsVerticalThumbDragging);
        Assert.Null(UIElement.CapturedElement);
    }

    [Fact]
    public void ComboBox_ItemTemplate_AppliesToPopupListBoxItems()
    {
        var cities = new List<string> { "Tokyo", "Berlin", "New York" };
        var cb = new ComboBox
        {
            ItemsSource = cities,
            ItemTemplate = item =>
            {
                var dot = new Border { Width = 8, Height = 8 };
                var text = new TextBlock(item?.ToString() ?? string.Empty);
                var panel = new StackPanel { Orientation = Orientation.Horizontal };
                panel.Add(dot);
                panel.Add(text);
                return panel;
            }
        };

        var container = new StackPanel();
        container.Add(cb);
        container.Measure(new Size(800, 600));
        container.Arrange(new Rect(0, 0, 800, 600));

        cb.IsDropDownOpen = true;
        PopupManager.UpdatePopups(new Size(800, 600));

        var popup = PopupManager.TopmostPopup;
        Assert.NotNull(popup);

        var border = popup.Child as Border;
        var listBox = border?.Child as ListBox;
        Assert.NotNull(listBox);

        // Every ListBoxItem in the popup listbox should have a StackPanel containing the dot border
        var itemPanel = listBox.ScrollViewer.Content as StackPanel;
        Assert.NotNull(itemPanel);
        Assert.Equal(3, itemPanel.Children.Count);

        for (int i = 0; i < itemPanel.Children.Count; i++)
        {
            var itemContainer = itemPanel.Children[i] as ListBoxItem;
            Assert.NotNull(itemContainer);
            Assert.IsType<StackPanel>(itemContainer.Content);
            var templatePanel = (StackPanel)itemContainer.Content;
            Assert.Equal(2, templatePanel.Children.Count);
            Assert.IsType<Border>(templatePanel.Children[0]);
            Assert.IsType<TextBlock>(templatePanel.Children[1]);
        }
    }

    private class ListBoxTestViewModel : INotifyPropertyChanged
    {
        private string? _selectedCity;
        public string? SelectedCity
        {
            get => _selectedCity;
            set
            {
                if (_selectedCity != value)
                {
                    _selectedCity = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedCity)));
                }
            }
        }

        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex != value)
                {
                    _selectedIndex = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedIndex)));
                }
            }
        }

        private List<string> _cities = new() { "Paris", "London", "Rome" };
        public List<string> Cities
        {
            get => _cities;
            set
            {
                _cities = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Cities)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    [Fact]
    public void ListBox_MarkupExtensions_ChainFluently_And_BindProperties()
    {
        var vm = new ListBoxTestViewModel();
        object? selectionChangedItem = null;

        var listBox = new ListBox()
            .ItemsSource(new[] { "Tokyo", "Berlin", "Kyoto" })
            .Items("Sydney")
            .SelectedItem("Berlin")
            .OnSelectionChanged(item => selectionChangedItem = item);

        Assert.Equal(4, listBox.Items.Count);
        Assert.Equal("Berlin", listBox.SelectedItem);
        Assert.Equal(1, listBox.SelectedIndex);

        listBox.SelectedIndex(2);
        Assert.Equal("Kyoto", listBox.SelectedItem);
        Assert.Equal("Kyoto", selectionChangedItem);

        // Test two-way binding
        var boundListBox = new ListBox()
            .BindItemsSource(vm, x => x.Cities)
            .BindSelectedItem(vm, x => x.SelectedCity, (m, val) => m.SelectedCity = val)
            .BindSelectedIndex(vm, x => x.SelectedIndex, (m, val) => m.SelectedIndex = val);

        Assert.Equal(3, boundListBox.Items.Count);

        // VM -> ListBox
        vm.SelectedCity = "London";
        Assert.Equal("London", boundListBox.SelectedItem);
        Assert.Equal(1, boundListBox.SelectedIndex);

        // ListBox -> VM
        boundListBox.SelectedItem = "Rome";
        Assert.Equal("Rome", vm.SelectedCity);
        Assert.Equal(2, vm.SelectedIndex);

        boundListBox.SelectedIndex = 0;
        Assert.Equal("Paris", vm.SelectedCity);
        Assert.Equal(0, vm.SelectedIndex);
    }

    [Fact]
    public void ItemsControl_MarkupExtensions_ChainFluently()
    {
        var vm = new ListBoxTestViewModel();
        var ic = new ItemsControl()
            .BindItemsSource(vm, x => x.Cities)
            .Items("Madrid")
            .ItemTemplate<string>(s => new TextBlock(s.ToUpper()));

        Assert.Equal(4, ic.Items.Count);
        Assert.NotNull(ic.ItemTemplate);
    }

#pragma warning disable CS0067
    private class TestPageItem : INotifyPropertyChanged
    {
        public MaterialIconKind PageIcon { get; set; } = MaterialIconKind.CheckBox;
        public string PageTitle { get; set; } = "Test";
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private class TestMainVM : INotifyPropertyChanged
    {
        public List<TestPageItem> Pages { get; set; } = new() { new TestPageItem() };
        public TestPageItem? CurrentPage { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
#pragma warning restore CS0067

    [Fact]
    public void ListBox_ItemTemplate_WithGenericType_BuildsVisualsCorrectly()
    {
        var viewModel = new TestMainVM();
        viewModel.CurrentPage = viewModel.Pages[0];

        var lb = new ListBox { Width = 200 }
            .BindSelectedItem(viewModel, x => x.CurrentPage, (vm, p) => vm.CurrentPage = p)
            .BindItemsSource(viewModel, x => x.Pages)
            .Dock(Dock.Left)
            .ItemTemplate<TestPageItem>(item =>
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                var icon = new Icon();
                icon.BindKind(item, x => x.PageIcon);
                var tb = new TextBlock();
                tb.BindText(item, x => x.PageTitle);
                sp.Children(icon, tb);
                return sp;
            });

        var sv = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var dockPanel = new DockPanel { LastChildFill = true }
            .Children(lb, sv);

        Assert.NotNull(dockPanel);
        Assert.NotNull(lb.ItemTemplate);
        Assert.Single(lb.Items);
    }

    [Fact]
    public void ListBox_WithItemTemplate_InferredType_BuildsVisualsCorrectly()
    {
        var viewModel = new TestMainVM();
        viewModel.CurrentPage = viewModel.Pages[0];

        var lb = new ListBox { Width = 200 }
            .BindSelectedItem(viewModel, x => x.CurrentPage, (vm, p) => vm.CurrentPage = p)
            .BindItemsSource(viewModel, x => x.Pages)
            .Dock(Dock.Left)
            .WithItemTemplate((TestPageItem item) =>
            {
                return new StackPanel { Orientation = Orientation.Horizontal }
                    .Children(
                        new Icon().BindKind(item, x => x.PageIcon),
                        new TextBlock().BindText(item, x => x.PageTitle)
                    );
            });

        Assert.NotNull(lb.ItemTemplate);
        Assert.Single(lb.Items);
    }

    [Fact]
    public void ListBox_NarrowWidth_ItemContainerDoesNotExceedListBoxWidth()
    {
        var lb = new ListBox { Width = 100 };
        lb.Items.Add("This is a very long string that is much wider than one hundred pixels");
        lb.SelectedIndex = 0;
        lb.Measure(new Size(100, 400));
        lb.Arrange(new Rect(0, 0, 100, 400));

        var itemPanel = (Panel)lb.ScrollViewer.Content!;
        var container = (ListBoxItem)itemPanel.Children[0];

        Assert.Equal(100f, container.Bounds.Width);
        Assert.True(container.IsSelected);
        Assert.True(container.ClipToBounds, "Expected ListBoxItem.ClipToBounds to be true");
    }
}
