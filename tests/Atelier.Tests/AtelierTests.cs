using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Rendering;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Atelier.Tests;

public partial class TestViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Initial Title";

    [ObservableProperty]
    private int _counter = 0;

    [RelayCommand]
    private void Increment() => Counter++;
}

public class AtelierCoreTests
{
    [Fact]
    public void Color_HexParsingAndLerp_WorksCorrectly()
    {
        var red = Color.FromHex("#FF0000");
        Assert.Equal(255, red.R);
        Assert.Equal(0, red.G);
        Assert.Equal(0, red.B);
        Assert.Equal(255, red.A);

        var blue = Color.FromHex("#0000FF");
        var mid = Color.Lerp(red, blue, 0.5f);
        Assert.Equal(128, mid.R); // 127.5 rounds to 128
        Assert.Equal(0, mid.G);
        Assert.Equal(128, mid.B);
    }

    [Fact]
    public void BindableProperty_And_CommunityToolkitMvvmBinding_Works()
    {
        var vm = new TestViewModel();
        var textBlock = new TextBlock();

        textBlock.BindText(vm, x => x.Title);

        Assert.Equal("Initial Title", textBlock.Text);

        vm.Title = "Updated Title";
        Assert.Equal("Updated Title", textBlock.Text);
    }

    [Fact]
    public void CommandBinding_And_RelayCommand_ExecutesOnClick()
    {
        var vm = new TestViewModel();
        var button = new Button()
            .Command(vm.IncrementCommand);

        Assert.Equal(0, vm.Counter);

        // Simulate click
        button.OnPointerPressed(new Core.Events.PointerEventArgs(Point.Zero, Core.Events.PointerButtons.Left));
        button.OnPointerReleased(new Core.Events.PointerEventArgs(Point.Zero, Core.Events.PointerButtons.Left));

        // Trigger hover then click
        button.OnPointerEntered(new Core.Events.PointerEventArgs(Point.Zero));
        button.OnPointerPressed(new Core.Events.PointerEventArgs(Point.Zero, Core.Events.PointerButtons.Left));
        button.OnPointerReleased(new Core.Events.PointerEventArgs(Point.Zero, Core.Events.PointerButtons.Left));

        Assert.Equal(1, vm.Counter);
    }

    [Fact]
    public void StackPanel_MeasureAndArrange_PositionsChildrenSequentially()
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 10 }
            .Children(
                new TextBlock("Child 1").Height(30).Width(100),
                new TextBlock("Child 2").Height(40).Width(100)
            );

        panel.Measure(new Size(500, 500));
        Assert.Equal(100, panel.DesiredSize.Width);
        Assert.Equal(30 + 40 + 10, panel.DesiredSize.Height);

        panel.Arrange(new Rect(0, 0, 500, 500));
        var child1 = (UIElement)panel.Children[0];
        var child2 = (UIElement)panel.Children[1];

        Assert.Equal(0, child1.Bounds.Y);
        Assert.Equal(30, child1.Bounds.Height);

        Assert.Equal(40, child2.Bounds.Y); // 30 + 10
        Assert.Equal(40, child2.Bounds.Height);
    }

    [Fact]
    public void Grid_StarAndPixelSizing_CalculatesAccurateBounds()
    {
        var grid = new Grid()
            .Columns(GridLength.Pixels(100), GridLength.Star)
            .Rows(GridLength.Pixels(50), GridLength.Star);

        var cell1 = new TextBlock().Column(0).Row(0);
        var cell2 = new TextBlock().Column(1).Row(1);

        grid.Children(cell1, cell2);

        grid.Measure(new Size(300, 200));
        grid.Arrange(new Rect(0, 0, 300, 200));

        Assert.Equal(0, cell1.Bounds.X);
        Assert.Equal(100, cell1.Bounds.Width);
        Assert.Equal(50, cell1.Bounds.Height);

        Assert.Equal(100, cell2.Bounds.X);
        Assert.Equal(200, cell2.Bounds.Width); // 300 - 100
        Assert.Equal(50, cell2.Bounds.Y);
        Assert.Equal(150, cell2.Bounds.Height); // 200 - 50
    }

    [Fact]
    public void VisualTree_HitTesting_FindsTopmostElement()
    {
        var button = new Button("Click me").Size(100, 40);
        var canvas = new Canvas().Children(button);

        Canvas.SetLeft(button, 20);
        Canvas.SetTop(button, 30);

        canvas.Measure(new Size(500, 500));
        canvas.Arrange(new Rect(0, 0, 500, 500));

        // Point inside button
        var hit = canvas.HitTest(new Point(50, 50));
        Assert.NotNull(hit);

        // Point outside button
        var miss = canvas.HitTest(new Point(5, 5));
        Assert.Equal(canvas, miss);
    }

    [Fact]
    public void EventBubbling_TextBlockInsideButton_ClicksButtonSuccessfully()
    {
        bool clicked = false;
        var textBlock = new TextBlock("Clickable Text");
        var button = new Button { Content = textBlock };
        button.Click += (s, e) => clicked = true;

        var root = new Canvas().Children(button);
        button.Size(120, 40);
        root.Measure(new Size(500, 500));
        root.Arrange(new Rect(0, 0, 500, 500));

        // When hit testing at center of button, TextBlock has IsHitTestVisible = false so hit is Button
        var hit = root.HitTest(new Point(60, 20));
        Assert.Equal(button, hit);

        // If an event is raised from child or button directly, it bubbles and triggers click
        button.OnPointerEntered(new Core.Events.PointerEventArgs(new Point(60, 20)));
        var pressE = new Core.Events.PointerEventArgs(new Point(60, 20), Core.Events.PointerButtons.Left);
        var releaseE = new Core.Events.PointerEventArgs(new Point(60, 20), Core.Events.PointerButtons.Left);

        button.DispatchBubblePointerEvent(pressE, (el, e) => el.OnPointerPressed(e));
        button.DispatchBubblePointerEvent(releaseE, (el, e) => el.OnPointerReleased(e));

        Assert.True(clicked);
    }

    [Fact]
    public void EventBubbling_TextBlockInsideListBoxItem_SelectsItem()
    {
        var listBox = new ListBox();
        listBox.Items.Add("Tokyo");
        listBox.Items.Add("Paris");

        listBox.Measure(new Size(300, 200));
        listBox.Arrange(new Rect(0, 0, 300, 200));

        // Click first item (at y=15)
        var hit = listBox.HitTest(new Point(50, 15));
        Assert.NotNull(hit);
        Assert.IsAssignableFrom<ListBoxItem>(hit);

        var pt = new Point(50, 15);
        hit.DispatchBubblePointerEvent(new Core.Events.PointerEventArgs(pt), (el, e) => el.OnPointerEntered(e));
        hit.DispatchBubblePointerEvent(new Core.Events.PointerEventArgs(pt, Core.Events.PointerButtons.Left), (el, e) => el.OnPointerPressed(e));
        hit.DispatchBubblePointerEvent(new Core.Events.PointerEventArgs(pt, Core.Events.PointerButtons.Left), (el, e) => el.OnPointerReleased(e));

        Assert.Equal("Tokyo", listBox.SelectedItem);
    }

    [Fact]
    public void LocalCoordinateTransform_PointToClient_AccuratelyConvertsPositions()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 0 }.Size(200, 30);
        var panel = new Canvas().Children(slider);
        Canvas.SetLeft(slider, 100);
        Canvas.SetTop(slider, 50);

        panel.Measure(new Size(500, 500));
        panel.Arrange(new Rect(0, 0, 500, 500));

        // Window coordinate (200, 65) is exactly at the middle (local X=100) of the slider
        var screenPoint = new Point(200, 65);
        var localPoint = slider.PointToClient(screenPoint);

        Assert.Equal(100, localPoint.X);
        Assert.Equal(15, localPoint.Y);

        // Pointer pressed with local coordinates sets slider to 50%
        var pressE = new Core.Events.PointerEventArgs(localPoint, screenPoint, Core.Events.PointerButtons.Left);
        slider.OnPointerPressed(pressE);

        Assert.Equal(50, slider.Value);
        slider.OnPointerReleased(pressE);
    }

    [Fact]
    public void TextBox_KeyboardNavigation_CaretAndTextEditing()
    {
        var textBox = new TextBox("Hello");
        textBox.Focus();

        Assert.Equal(5, textBox.CaretIndex);

        // Arrow Left
        textBox.OnKeyDown(new Core.Events.KeyEventArgs(Core.Events.Key.Left, 0, Core.Events.ModifierKeys.None, true));
        Assert.Equal(4, textBox.CaretIndex);

        // Backspace: deletes character at index 3 ('l') -> "Helo"
        textBox.OnKeyDown(new Core.Events.KeyEventArgs(Core.Events.Key.Backspace, 0, Core.Events.ModifierKeys.None, true));
        Assert.Equal("Helo", textBox.Text);
        Assert.Equal(3, textBox.CaretIndex);

        // Home: moves caret to 0
        textBox.OnKeyDown(new Core.Events.KeyEventArgs(Core.Events.Key.Home, 0, Core.Events.ModifierKeys.None, true));
        Assert.Equal(0, textBox.CaretIndex);

        // Delete: deletes character at 0 ('H') -> "elo"
        textBox.OnKeyDown(new Core.Events.KeyEventArgs(Core.Events.Key.Delete, 0, Core.Events.ModifierKeys.None, true));
        Assert.Equal("elo", textBox.Text);
        Assert.Equal(0, textBox.CaretIndex);

        // End: moves caret to 3
        textBox.OnKeyDown(new Core.Events.KeyEventArgs(Core.Events.Key.End, 0, Core.Events.ModifierKeys.None, true));
        Assert.Equal(3, textBox.CaretIndex);
    }

    [Fact]
    public void FocusManager_TabCycle_MovesFocusSequentially()
    {
        var btn = new Button("B1");
        var chk = new CheckBox("C1");
        var txt = new TextBox("T1");
        var sld = new Slider();

        var root = new StackPanel().Children(btn, chk, txt, sld);

        FocusManager.SetFocus(null);
        Assert.Null(FocusManager.CurrentFocused);

        // Focus next -> btn
        FocusManager.FocusNext(root);
        Assert.Equal(btn, FocusManager.CurrentFocused);
        Assert.True(btn.IsFocused);

        // Focus next -> chk
        FocusManager.FocusNext(root);
        Assert.Equal(chk, FocusManager.CurrentFocused);
        Assert.True(chk.IsFocused);
        Assert.False(btn.IsFocused);

        // Focus next -> txt
        FocusManager.FocusNext(root);
        Assert.Equal(txt, FocusManager.CurrentFocused);

        // Focus next -> sld
        FocusManager.FocusNext(root);
        Assert.Equal(sld, FocusManager.CurrentFocused);

        // Focus next -> wraps to btn
        FocusManager.FocusNext(root);
        Assert.Equal(btn, FocusManager.CurrentFocused);

        // Focus previous -> wraps to sld
        FocusManager.FocusPrevious(root);
        Assert.Equal(sld, FocusManager.CurrentFocused);
    }

    [Fact]
    public void RadioButtonGroup_ImmediateDeselection()
    {
        var rb1 = new RadioButton { GroupName = "testGroup", IsChecked = true };
        var rb2 = new RadioButton { GroupName = "testGroup", IsChecked = false };

        Assert.True(rb1.IsChecked);
        Assert.False(rb2.IsChecked);

        // Checking rb2 must immediately deselect rb1 on the first change
        rb2.IsChecked = true;

        Assert.False(rb1.IsChecked);
        Assert.True(rb2.IsChecked);

        // Checking rb1 again deselects rb2
        rb1.IsChecked = true;

        Assert.True(rb1.IsChecked);
        Assert.False(rb2.IsChecked);
    }

    [Fact]
    public void PointerWheel_BubblingThroughHierarchy_PreservesTypeAndDelta()
    {
        var scrollViewer = new ScrollViewer { Width = 300, Height = 200 };
        var innerContent = new StackPanel { Spacing = 10 }
            .Children(
                new Button("Item 1").Height(100),
                new Button("Item 2").Height(100),
                new Button("Item 3").Height(100)
            );
        scrollViewer.Content = innerContent;

        var root = new Canvas().Children(scrollViewer);
        root.Measure(new Size(500, 500));
        root.Arrange(new Rect(0, 0, 500, 500));

        // Find child element under cursor
        var hit = root.HitTest(new Point(50, 50));
        Assert.NotNull(hit);

        // Dispatch scroll wheel event on child - should bubble up to ScrollViewer without InvalidCastException
        var wheelE = new Core.Events.PointerWheelEventArgs(new Point(50, 50), 0, -1.0f);
        hit.DispatchBubblePointerEvent(wheelE, (el, e) => el.OnPointerWheel(e));

        Assert.True(wheelE.Handled);
        Assert.True(scrollViewer.ScrollOffsetY > 0);
    }

    [Fact]
    public void Slider_PointerCapture_MaintainsDraggingOutsideBounds()
    {
        UIElement.ReleaseCurrentPointerCapture();

        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 0 }.Size(200, 48);
        var root = new Canvas().Children(slider);
        root.Measure(new Size(500, 500));
        root.Arrange(new Rect(0, 0, 500, 500));

        Assert.False(slider.IsPointerCaptured);
        Assert.Null(UIElement.CapturedElement);

        // 1. Left click on the slider
        var pressE = new Core.Events.PointerEventArgs(new Point(100, 24), Core.Events.PointerButtons.Left);
        slider.OnPointerPressed(pressE);

        Assert.True(slider.IsPointerCaptured);
        Assert.Equal(slider, UIElement.CapturedElement);
        Assert.Equal(50f, slider.Value, tolerance: 1f);

        // 2. Drag pointer far outside bounds to the right (x=350, y=-100)
        var dragRight = new Core.Events.PointerEventArgs(new Point(350, -100));
        slider.OnPointerMoved(dragRight);
        Assert.Equal(100f, slider.Value);

        // 3. Drag pointer far outside bounds to the left (x=-50, y=800)
        var dragLeft = new Core.Events.PointerEventArgs(new Point(-50, 800));
        slider.OnPointerMoved(dragLeft);
        Assert.Equal(0f, slider.Value);

        // 4. Release mouse button anywhere
        var releaseE = new Core.Events.PointerEventArgs(new Point(-50, 800), Core.Events.PointerButtons.Left);
        slider.OnPointerReleased(releaseE);

        Assert.False(slider.IsPointerCaptured);
        Assert.Null(UIElement.CapturedElement);

        // 5. Subsequent pointer moves without press must not manipulate slider
        var moveWithoutPress = new Core.Events.PointerEventArgs(new Point(100, 24));
        slider.OnPointerMoved(moveWithoutPress);
        Assert.Equal(0f, slider.Value);
    }

    [Fact]
    public void Slider_DragTracking_StableWhenParentOrWindowIsScaledDuringDrag()
    {
        UIElement.ReleaseCurrentPointerCapture();

        var slider = new Slider { Minimum = 80, Maximum = 200, Value = 100 }.Size(240, 48);
        var root = new Canvas().Children(slider);
        root.Measure(new Size(800, 800));
        root.Arrange(new Rect(0, 0, 800, 800));

        // Hook ValueChanged to dynamically rescale root during the drag
        // (reproducing exactly what happens when "Scale Entire App Window" is checked)
        slider.ValueChanged += (s, v) =>
        {
            root.Transform = System.Numerics.Matrix3x2.CreateScale(v / 100f);
        };

        // 1. Initial click at X = 120 (mid-point of slider)
        var pressE = new Core.Events.PointerEventArgs(new Point(120, 24), Core.Events.PointerButtons.Left);
        slider.OnPointerPressed(pressE);

        float initialVal = slider.Value;
        Assert.InRange(initialVal, 130f, 150f);

        // 2. Drag right by +20 screen pixels
        // Even though root.Transform changes to ~1.4x scale, the drag must remain monotonic and smooth
        var drag1 = new Core.Events.PointerEventArgs(new Point(140, 24));
        slider.OnPointerMoved(drag1);
        float val1 = slider.Value;
        Assert.True(val1 > initialVal, $"Expected val1 ({val1}) > initialVal ({initialVal})");

        // 3. Drag right another +20 screen pixels
        var drag2 = new Core.Events.PointerEventArgs(new Point(160, 24));
        slider.OnPointerMoved(drag2);
        float val2 = slider.Value;
        Assert.True(val2 > val1, $"Expected val2 ({val2}) > val1 ({val1})");

        // 4. Drag back left by 10 screen pixels (X = 150)
        var drag3 = new Core.Events.PointerEventArgs(new Point(150, 24));
        slider.OnPointerMoved(drag3);
        float val3 = slider.Value;
        Assert.True(val3 < val2, $"Expected val3 ({val3}) < val2 ({val2})");
        Assert.True(val3 > val1, $"Expected val3 ({val3}) > val1 ({val1})");

        // 5. Release pointer capture
        var releaseE = new Core.Events.PointerEventArgs(new Point(150, 24), Core.Events.PointerButtons.Left);
        slider.OnPointerReleased(releaseE);
        Assert.False(slider.IsPointerCaptured);
    }

    [Fact]
    public void Slider_ValueIndicator_FadesOnPressAndRelease()
    {
        var clock = new Core.Animation.AnimationClock();
        Slider.SetGlobalAnimationClock(clock);

        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 50, ShowValueIndicator = true }.Size(200, 48);
        var root = new Canvas().Children(slider);
        root.Measure(new Size(500, 500));
        root.Arrange(new Rect(0, 0, 500, 500));

        Assert.Equal(0f, slider.ValueIndicatorOpacity);

        // 1. Press left mouse button
        var pressE = new Core.Events.PointerEventArgs(new Point(100, 24), Core.Events.PointerButtons.Left);
        slider.OnPointerPressed(pressE);

        // Advance animation clock to mid-way
        clock.Update(0.075); // 75ms
        Assert.True(slider.ValueIndicatorOpacity > 0f);
        Assert.True(slider.ValueIndicatorOpacity < 1f);

        // Advance past 150ms duration to completion
        clock.Update(0.150);
        Assert.Equal(1f, slider.ValueIndicatorOpacity);

        // 2. Release left mouse button
        var releaseE = new Core.Events.PointerEventArgs(new Point(100, 24), Core.Events.PointerButtons.Left);
        slider.OnPointerReleased(releaseE);

        // Advance animation clock to mid-way of fade-out (200ms duration)
        clock.Update(0.100);
        Assert.True(slider.ValueIndicatorOpacity > 0f);
        Assert.True(slider.ValueIndicatorOpacity < 1f);

        // Advance to completion
        clock.Update(0.150);
        Assert.Equal(0f, slider.ValueIndicatorOpacity);
    }

    [Fact]
    public void Slider_ShowValueIndicator_Configuration_And_Layout()
    {
        var sliderDefault = new Slider();
        Assert.True(sliderDefault.ShowValueIndicator);
        Assert.Equal("{0:0}", sliderDefault.ValueFormat);

        sliderDefault.Measure(new Size(500, 500));
        Assert.Equal(32f, sliderDefault.DesiredSize.Height);

        var sliderNoIndicator = new Slider { ShowValueIndicator = false };
        sliderNoIndicator.Measure(new Size(500, 500));
        Assert.Equal(32f, sliderNoIndicator.DesiredSize.Height);

        // When ShowValueIndicator is false, pressing should not trigger indicator fade
        var pressE = new Core.Events.PointerEventArgs(new Point(50, 16), Core.Events.PointerButtons.Left);
        sliderNoIndicator.OnPointerPressed(pressE);
        Assert.Equal(0f, sliderNoIndicator.ValueIndicatorOpacity);
        sliderNoIndicator.OnPointerReleased(pressE);
    }

    [Fact]
    public void HotReload_TriggerAndRebuild_ExecutesViewFactory()
    {
        int factoryInvocations = 0;
        string buttonTitle = "Version 1";

        Func<UIElement> viewFactory = () =>
        {
            factoryInvocations++;
            return new Button(buttonTitle);
        };

        // 1. Initial visual tree construction
        var currentContent = viewFactory();
        Assert.Equal(1, factoryInvocations);
        Assert.IsType<Button>(currentContent);
        Assert.Equal("Version 1", ((TextBlock)((Button)currentContent).Content!).Text);

        // 2. Subscribe to HotReload
        void OnReload()
        {
            currentContent = viewFactory();
        }

        Atelier.Core.HotReload.HotReloadManager.HotReloadTriggered += OnReload;

        try
        {
            // 3. User edits code (e.g. changes button label to Version 2)
            buttonTitle = "Version 2";

            // 4. Runtime hot reload fires
            Atelier.Core.HotReload.HotReloadManager.TriggerHotReload();

            Assert.Equal(2, factoryInvocations);
            Assert.Equal("Version 2", ((TextBlock)((Button)currentContent).Content!).Text);
        }
        finally
        {
            Atelier.Core.HotReload.HotReloadManager.HotReloadTriggered -= OnReload;
        }
    }

    [Fact]
    public void UIElement_MaxHeight_ConstrainsArrangeAndBounds()
    {
        var panel = new StackPanel().Height(100).MaxHeight(80);
        panel.Measure(new Size(500, 500));
        panel.Arrange(new Rect(0, 0, 500, 500));

        Assert.Equal(80, panel.Bounds.Height);
    }

    [Fact]
    public void ListBox_WithMaxHeight_EnablesScrollingAndLimitsHeight()
    {
        var listBox = new ListBox();
        for (int i = 0; i < 10; i++)
        {
            listBox.Items.Add($"City {i}");
        }

        listBox.MaxHeight = 150;

        listBox.Measure(new Size(300, 1000));
        listBox.Arrange(new Rect(0, 0, 300, 1000));

        // 1. Height must be strictly constrained to 150
        Assert.Equal(150, listBox.Bounds.Height);
        Assert.Equal(150, listBox.ScrollViewer.Bounds.Height);

        // 2. Extent must exceed Viewport and MaxScrollY must be positive
        Assert.True(listBox.ScrollViewer.Extent.Height > 150);
        Assert.True(listBox.ScrollViewer.MaxScrollY > 0);

        // 3. ScrollBar thumb must exist
        var thumb = listBox.ScrollViewer.GetThumbRect();
        Assert.True(thumb.Height > 0);
        Assert.True(thumb.Width > 0);

        // 4. Mouse wheel on the list box must scroll the content and update child bounds
        var wheelE = new Core.Events.PointerWheelEventArgs(new Point(50, 50), 0, -1.0f);
        listBox.ScrollViewer.DispatchBubblePointerEvent(wheelE, (el, e) => el.OnPointerWheel(e));

        Assert.True(wheelE.Handled);
        Assert.True(listBox.ScrollViewer.ScrollOffsetY > 0);
        var itemPanel = (UIElement)listBox.ScrollViewer.Children[0];
        Assert.Equal(-listBox.ScrollViewer.ScrollOffsetY, itemPanel.Bounds.Y);
    }

    [Fact]
    public void ScrollViewer_PointerDrag_ScrollsThumbAndContent()
    {
        var scrollViewer = new ScrollViewer { Width = 200, Height = 100 };
        var content = new StackPanel().Height(400); // 400px tall content
        scrollViewer.Content = content;

        scrollViewer.Measure(new Size(200, 100));
        scrollViewer.Arrange(new Rect(0, 0, 200, 100));

        Assert.Equal(100, scrollViewer.Bounds.Height);
        Assert.Equal(400, scrollViewer.Extent.Height);
        Assert.Equal(300, scrollViewer.MaxScrollY);

        var thumb = scrollViewer.GetThumbRect();
        Assert.True(thumb.Height > 0);

        // Click thumb and drag down
        var pressE = new Core.Events.PointerEventArgs(new Point(thumb.X + 2, thumb.Y + 2), Core.Events.PointerButtons.Left);
        scrollViewer.OnPointerPressed(pressE);
        Assert.True(scrollViewer.IsThumbDragging);
        Assert.True(scrollViewer.IsVerticalThumbDragging);

        // Drag down by 30px
        var dragE = new Core.Events.PointerEventArgs(new Point(thumb.X + 2, thumb.Y + 32));
        scrollViewer.OnPointerMoved(dragE);
        Assert.True(scrollViewer.ScrollOffsetY > 0);

        // Content Bounds.Y MUST update immediately to match the scroll offset!
        Assert.Equal(-scrollViewer.ScrollOffsetY, content.Bounds.Y);

        // Release
        var releaseE = new Core.Events.PointerEventArgs(new Point(thumb.X + 2, thumb.Y + 32), Core.Events.PointerButtons.Left);
        scrollViewer.OnPointerReleased(releaseE);
        Assert.False(scrollViewer.IsThumbDragging);
    }

    [Fact]
    public void ScrollViewer_HorizontalAndVerticalScrolling_WorksTogether()
    {
        var scrollViewer = new ScrollViewer
        {
            Width = 200,
            Height = 100,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        var content = new StackPanel().Width(600).Height(500);
        scrollViewer.Content = content;

        scrollViewer.Measure(new Size(200, 100));
        scrollViewer.Arrange(new Rect(0, 0, 200, 100));

        Assert.Equal(400, scrollViewer.MaxScrollX); // 600 - 200
        Assert.Equal(400, scrollViewer.MaxScrollY); // 500 - 100
        Assert.True(scrollViewer.CanScrollHorizontally);
        Assert.True(scrollViewer.CanScrollVertically);

        var hTrack = scrollViewer.GetHorizontalTrackRect();
        var hThumb = scrollViewer.GetHorizontalThumbRect();
        Assert.True(hTrack.Width > 0);
        Assert.True(hThumb.Width > 0);

        // Drag horizontal thumb to the right
        var pressH = new Core.Events.PointerEventArgs(new Point(hThumb.X + 2, hThumb.Y + 2), Core.Events.PointerButtons.Left);
        scrollViewer.OnPointerPressed(pressH);
        Assert.True(scrollViewer.IsHorizontalThumbDragging);

        var moveH = new Core.Events.PointerEventArgs(new Point(hThumb.X + 32, hThumb.Y + 2));
        scrollViewer.OnPointerMoved(moveH);
        Assert.True(scrollViewer.ScrollOffsetX > 0);
        Assert.Equal(-scrollViewer.ScrollOffsetX, content.Bounds.X);

        scrollViewer.OnPointerReleased(moveH);
        Assert.False(scrollViewer.IsHorizontalThumbDragging);

        // Wheel scroll horizontally via DeltaX
        var wheelH = new Core.Events.PointerWheelEventArgs(new Point(50, 50), -1.0f, 0);
        scrollViewer.OnPointerWheel(wheelH);
        Assert.True(wheelH.Handled);
        Assert.Equal(-scrollViewer.ScrollOffsetX, content.Bounds.X);
    }

    [Fact]
    public void Grid_WithStarDefinitions_MeasuresInScrollViewerWithoutZeroSize()
    {
        var grid = new Grid()
            .Columns(GridLength.Pixels(100), GridLength.Star)
            .Rows(GridLength.Pixels(50), GridLength.Star);

        var col0Child = new Border().Width(100).Height(50).Column(0).Row(0);
        var starChild = new Border().Width(300).Height(200).Column(1).Row(1);
        grid.Children(col0Child, starChild);

        var scrollViewer = new ScrollViewer
        {
            Width = 250,
            Height = 150,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = grid
        };

        scrollViewer.Measure(new Size(250, 150));
        scrollViewer.Arrange(new Rect(0, 0, 250, 150));

        // In infinite measure inside ScrollViewer, Star definitions must measure children correctly
        Assert.True(scrollViewer.Extent.Width >= 400); // 100 + 300
        Assert.True(scrollViewer.Extent.Height >= 250); // 50 + 200
        Assert.True(scrollViewer.MaxScrollX > 0);
        Assert.True(scrollViewer.MaxScrollY > 0);
    }

    [Fact]
    public void UIElement_InvalidateArrange_BubblesToAncestors()
    {
        var panel = new StackPanel();
        var child = new Border();
        panel.Add(child);

        // Initial layout pass
        panel.Measure(new Size(200, 200));
        panel.Arrange(new Rect(0, 0, 200, 200));

        Assert.True(panel.IsArrangeValid);
        Assert.True(child.IsArrangeValid);

        // Invalidate child arrange
        child.InvalidateArrange();

        Assert.False(child.IsArrangeValid);
        Assert.False(panel.IsArrangeValid); // Must have bubbled to parent!
    }

    [Fact]
    public void ListBox_TabCycling_IsSingleTabStop()
    {
        var btn1 = new Button("First");
        var listBox = new ListBox();
        for (int i = 0; i < 5; i++)
        {
            listBox.Items.Add($"Item {i}");
        }
        var btn2 = new Button("Second");

        var root = new StackPanel().Children(btn1, listBox, btn2);
        root.Measure(new Size(500, 500));
        root.Arrange(new Rect(0, 0, 500, 500));

        FocusManager.SetFocus(btn1);
        Assert.Equal(btn1, FocusManager.CurrentFocused);

        // Pressing Tab from btn1 must focus ListBox (not ListBoxItem 0)
        FocusManager.FocusNext(root);
        Assert.Equal(listBox, FocusManager.CurrentFocused);
        Assert.Equal(0, listBox.SelectedIndex);

        // Pressing Tab from ListBox must move away to btn2 (NOT item 1!)
        FocusManager.FocusNext(root);
        Assert.Equal(btn2, FocusManager.CurrentFocused);

        // Pressing Tab again wraps to btn1
        FocusManager.FocusNext(root);
        Assert.Equal(btn1, FocusManager.CurrentFocused);
    }

    [Fact]
    public void ListBox_ArrowKeys_NavigatesAndSelectsItems()
    {
        var listBox = new ListBox();
        for (int i = 0; i < 6; i++)
        {
            listBox.Items.Add($"City {i}");
        }
        listBox.MaxHeight = 100;

        listBox.Measure(new Size(300, 500));
        listBox.Arrange(new Rect(0, 0, 300, 500));

        // Focus list box
        FocusManager.SetFocus(listBox);
        Assert.Equal(0, listBox.SelectedIndex);

        // Down arrow: moves to 1
        listBox.OnKeyDown(new Core.Events.KeyEventArgs(Core.Events.Key.Down, 0, Core.Events.ModifierKeys.None, true));
        Assert.Equal(1, listBox.SelectedIndex);
        Assert.Equal("City 1", listBox.SelectedItem);

        // Down arrow: moves to 2
        listBox.OnKeyDown(new Core.Events.KeyEventArgs(Core.Events.Key.Down, 0, Core.Events.ModifierKeys.None, true));
        Assert.Equal(2, listBox.SelectedIndex);

        // Up arrow: moves back to 1
        listBox.OnKeyDown(new Core.Events.KeyEventArgs(Core.Events.Key.Up, 0, Core.Events.ModifierKeys.None, true));
        Assert.Equal(1, listBox.SelectedIndex);

        // End: moves to last item (5) and scrolls
        listBox.OnKeyDown(new Core.Events.KeyEventArgs(Core.Events.Key.End, 0, Core.Events.ModifierKeys.None, true));
        Assert.Equal(5, listBox.SelectedIndex);
        Assert.Equal("City 5", listBox.SelectedItem);
        Assert.True(listBox.ScrollViewer.ScrollOffsetY > 0);

        // Home: moves back to first item (0)
        listBox.OnKeyDown(new Core.Events.KeyEventArgs(Core.Events.Key.Home, 0, Core.Events.ModifierKeys.None, true));
        Assert.Equal(0, listBox.SelectedIndex);
        Assert.Equal(0, listBox.ScrollViewer.ScrollOffsetY);
    }

    [Fact]
    public void TitleBar_InitializesWithDefaults_AndSupportsCustomContent()
    {
        var searchBox = new TextBox { Placeholder = "Search..." };
        var titleBar = new TitleBar
        {
            Title = "My Custom App",
            Content = searchBox
        };

        Assert.Equal("My Custom App", titleBar.Title);
        Assert.Equal(searchBox, titleBar.Content);
        Assert.True(titleBar.ShowMinimizeButton);
        Assert.True(titleBar.ShowMaximizeButton);
        Assert.True(titleBar.ShowCloseButton);

        titleBar.Measure(new Size(800, 44));
        titleBar.Arrange(new Rect(0, 0, 800, 44));

        Assert.Equal(800, titleBar.Bounds.Width);
        Assert.Equal(44, titleBar.Bounds.Height);
    }

    [Fact]
    public void TitleBar_ButtonActions_And_DoubleClicks_TriggerCallbacks()
    {
        bool minimized = false;
        bool maximized = false;
        bool closed = false;
        bool dragged = false;

        var titleBar = new TitleBar
        {
            Title = "Test Window",
            OnMinimize = () => minimized = true,
            OnMaximize = () => maximized = true,
            OnClose = () => closed = true,
            OnDragMove = () => dragged = true
        };

        // 1. Single click triggers drag move
        var point = new Point(100, 20);
        titleBar.OnPointerPressed(new Core.Events.PointerEventArgs(point, point, Core.Events.PointerButtons.Left, clickCount: 1));
        Assert.True(dragged);

        // 2. The second click of a double click (as counted by the platform) toggles maximize
        titleBar.OnPointerPressed(new Core.Events.PointerEventArgs(point, point, Core.Events.PointerButtons.Left, clickCount: 2));
        Assert.True(maximized);

        // 3. Button callbacks
        titleBar.OnMinimize?.Invoke();
        Assert.True(minimized);

        titleBar.OnClose?.Invoke();
        Assert.True(closed);
    }

    [Fact]
    public void TextBox_CornerRadius_AffectsBottomLineCalculation()
    {
        var textBox = new TextBox { Width = 200, Height = 32, CornerRadius = new CornerRadius(16) };
        textBox.Measure(new Size(200, 32));
        textBox.Arrange(new Rect(0, 0, 200, 32));

        Assert.Equal(16, textBox.CornerRadius.BottomLeft);
        Assert.Equal(16, textBox.CornerRadius.BottomRight);

        float startX = 0 + textBox.CornerRadius.BottomLeft;
        float endX = 200 - textBox.CornerRadius.BottomRight;

        Assert.Equal(16, startX);
        Assert.Equal(184, endX);
        Assert.True(endX > startX);
    }

    [Fact]
    public void DrawingContext_DrawShadow_ExecutesDropShadowFilterSuccessfully()
    {
        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(200, 100));
        var paintRegistry = new PaintRegistry();
        var context = new DrawingContext(surface.Canvas, paintRegistry);

        var rect = new Rect(20, 20, 160, 40);
        context.DrawShadow(rect, new CornerRadius(20), 2f, Color.Black);

        surface.Canvas.Flush();
        Assert.NotNull(surface);
    }

    [Fact]
    public void Font_Inspection_LoadsMaterialSymbolsVariableFont()
    {
        string fontPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "MaterialSymbolsRounded-VariableFont_FILL,GRAD,opsz,wght.ttf");
        if (!System.IO.File.Exists(fontPath))
        {
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "Assets", "Fonts", "MaterialSymbolsRounded-VariableFont_FILL,GRAD,opsz,wght.ttf")))
            {
                dir = dir.Parent;
            }
            if (dir != null)
            {
                fontPath = System.IO.Path.Combine(dir.FullName, "Assets", "Fonts", "MaterialSymbolsRounded-VariableFont_FILL,GRAD,opsz,wght.ttf");
            }
        }
        Assert.True(System.IO.File.Exists(fontPath));

        using var tf = SkiaSharp.SKTypeface.FromFile(fontPath);
        Assert.NotNull(tf);
        Assert.Equal("Material Symbols Rounded", tf.FamilyName);

        // Test measuring search icon (codepoint 0xE8B6 = 59574)
        string searchGlyph = char.ConvertFromUtf32(0xE8B6);
        using var font = new SkiaSharp.SKFont(tf, 24f);
        float width = font.MeasureText(searchGlyph);
        Assert.True(width > 0, $"Measured width: {width}");

        // Test all 4 variable font axes with SKFontArguments
        var coords = new[]
        {
            new SkiaSharp.SKFontVariationPositionCoordinate { Axis = new SkiaSharp.SKFourByteTag('F', 'I', 'L', 'L'), Value = 1f },
            new SkiaSharp.SKFontVariationPositionCoordinate { Axis = new SkiaSharp.SKFourByteTag('w', 'g', 'h', 't'), Value = 600f },
            new SkiaSharp.SKFontVariationPositionCoordinate { Axis = new SkiaSharp.SKFourByteTag('G', 'R', 'A', 'D'), Value = 25f },
            new SkiaSharp.SKFontVariationPositionCoordinate { Axis = new SkiaSharp.SKFourByteTag('o', 'p', 's', 'z'), Value = 48f }
        };
        var args = new SkiaSharp.SKFontArguments { VariationDesignPosition = coords };
        using var customizedTf = tf.Clone(args);
        Assert.NotNull(customizedTf);

        using var font2 = new SkiaSharp.SKFont(customizedTf, 24f);
        float width2 = font2.MeasureText(searchGlyph);
        Assert.True(width2 > 0);
    }

    [Fact]
    public void ScrollViewer_ScrollBarWidth_AnimatesOnHoverAndExit()
    {
        var clock = new Core.Animation.AnimationClock();
        ScrollViewer.SetGlobalAnimationClock(clock);
        try
        {
            var scrollViewer = new ScrollViewer { Width = 200, Height = 100 };
            var content = new StackPanel().Height(400);
            scrollViewer.Content = content;

            scrollViewer.Measure(new Size(200, 100));
            scrollViewer.Arrange(new Rect(0, 0, 200, 100));

            // Initially normal width
            Assert.Equal(ScrollViewer.NormalScrollBarWidth, scrollViewer.CurrentVerticalScrollBarWidth);

            // Move mouse into vertical scrollbar hit zone (X >= 200 - 28 = 172)
            scrollViewer.OnPointerMoved(new Core.Events.PointerEventArgs(new Point(190, 50)));
            Assert.True(scrollViewer.IsVerticalScrollBarHovered);

            // Step clock 50ms - should have started expanding
            clock.Update(0.05);
            Assert.True(scrollViewer.CurrentVerticalScrollBarWidth > ScrollViewer.NormalScrollBarWidth);
            Assert.True(scrollViewer.CurrentVerticalScrollBarWidth < ScrollViewer.HoveredScrollBarWidth);

            // Step clock 250ms - animation completes (duration is 180ms)
            clock.Update(0.25);
            Assert.Equal(ScrollViewer.HoveredScrollBarWidth, scrollViewer.CurrentVerticalScrollBarWidth);

            // Move mouse out
            scrollViewer.OnPointerExited(new Core.Events.PointerEventArgs(new Point(100, 50)));
            Assert.False(scrollViewer.IsVerticalScrollBarHovered);

            // Step clock 50ms - shrinking
            clock.Update(0.05);
            Assert.True(scrollViewer.CurrentVerticalScrollBarWidth < ScrollViewer.HoveredScrollBarWidth);

            // Step clock 300ms - fully shrunk to NormalScrollBarWidth (duration is 250ms)
            clock.Update(0.3);
            Assert.Equal(ScrollViewer.NormalScrollBarWidth, scrollViewer.CurrentVerticalScrollBarWidth);
        }
        finally
        {
            ScrollViewer.SetGlobalAnimationClock(null);
        }
    }

    [Fact]
    public void ScrollViewer_ScrollBarWidth_RemainsExpandedDuringDrag()
    {
        var clock = new Core.Animation.AnimationClock();
        ScrollViewer.SetGlobalAnimationClock(clock);
        try
        {
            var scrollViewer = new ScrollViewer { Width = 200, Height = 100 };
            var content = new StackPanel().Height(400);
            scrollViewer.Content = content;

            scrollViewer.Measure(new Size(200, 100));
            scrollViewer.Arrange(new Rect(0, 0, 200, 100));

            var thumb = scrollViewer.GetVerticalThumbRect();

            // Press thumb to start drag
            scrollViewer.OnPointerPressed(new Core.Events.PointerEventArgs(new Point(thumb.X + 1, thumb.Y + 1), Core.Events.PointerButtons.Left));
            Assert.True(scrollViewer.IsVerticalThumbDragging);

            // Complete expand animation
            clock.Update(0.3);
            Assert.Equal(ScrollViewer.HoveredScrollBarWidth, scrollViewer.CurrentVerticalScrollBarWidth);

            // Move pointer far away from the hit zone (e.g. x=50) while dragging
            scrollViewer.OnPointerMoved(new Core.Events.PointerEventArgs(new Point(50, 50)));
            clock.Update(0.3);

            // Still fully expanded because drag is active!
            Assert.Equal(ScrollViewer.HoveredScrollBarWidth, scrollViewer.CurrentVerticalScrollBarWidth);

            // Release drag outside the hit zone
            scrollViewer.OnPointerReleased(new Core.Events.PointerEventArgs(new Point(50, 50), Core.Events.PointerButtons.Left));
            Assert.False(scrollViewer.IsThumbDragging);

            // Now it should collapse back to NormalScrollBarWidth
            clock.Update(0.3);
            Assert.Equal(ScrollViewer.NormalScrollBarWidth, scrollViewer.CurrentVerticalScrollBarWidth);
        }
        finally
        {
            ScrollViewer.SetGlobalAnimationClock(null);
        }
    }
}

