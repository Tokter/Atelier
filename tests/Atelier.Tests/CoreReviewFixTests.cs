using System;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Styling;
using Atelier.Core.Tree;
using Atelier.Core.ViewResolution;
using Atelier.Layout;

namespace Atelier.Tests;

public class MeasureCountingElement : UIElement
{
    public int MeasureCount { get; private set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        MeasureCount++;
        return new Size(10, 10);
    }
}

public class BaseTestViewModel { }
public class DerivedTestViewModel : BaseTestViewModel { }
public class MostDerivedTestViewModel : DerivedTestViewModel, ITestViewModel { }
public interface ITestViewModel { }
public class TestViewA : UIElement { }
public class TestViewB : UIElement { }
public class TestViewC : UIElement { }

public class CoreReviewFixTests
{
    #region Primitives and layout

    [Fact]
    public void Size_Infinity_EqualsItself()
    {
        var unbounded = new Size(float.PositiveInfinity, float.PositiveInfinity);

        Assert.True(Size.Infinity == unbounded);
        Assert.Equal(Size.Infinity.GetHashCode(), unbounded.GetHashCode());
        Assert.True(Size.Infinity.IsClose(unbounded));
    }

    [Fact]
    public void Primitives_EqualityIsExact_AndIsCloseTolerates()
    {
        var a = new Point(1f, 2f);
        var nearlyA = new Point(1.0000002f, 2f);

        Assert.True(a == new Point(1f, 2f));
        Assert.Equal(a.GetHashCode(), new Point(1f, 2f).GetHashCode());
        Assert.False(a == nearlyA);
        Assert.True(a.IsClose(nearlyA));
        Assert.True(new Size(10, 10).IsClose(new Size(10.000001f, 10)));
    }

    [Fact]
    public void Measure_UnchangedChildWithInfiniteConstraint_IsNotRemeasured()
    {
        var panel = new StackPanel();
        var child = new MeasureCountingElement();
        panel.Add(child);
        panel.Measure(new Size(100, 100));
        int before = child.MeasureCount;

        panel.InvalidateMeasure();
        panel.Measure(new Size(100, 100));

        Assert.Equal(before, child.MeasureCount);
    }

    [Fact]
    public void Layout_MinGreaterThanMax_DoesNotThrow_AndMinWins()
    {
        var element = new MeasureCountingElement { MinWidth = 300, MaxWidth = 200, MinHeight = 50, MaxHeight = 20 };

        element.Measure(new Size(500, 500));
        element.Arrange(new Rect(0, 0, 500, 500));

        Assert.Equal(300, element.DesiredSize.Width);
        Assert.Equal(50, element.DesiredSize.Height);
    }

    [Fact]
    public void Color_Lerp_RoundsAndReachesTarget()
    {
        var black = Color.FromRgb(0, 0, 0);
        var white = Color.FromRgb(255, 255, 255);

        Assert.Equal(white, Color.Lerp(black, white, 1f));
        Assert.Equal(Color.Lerp(black, white, 0.25f).R, 255 - Color.Lerp(white, black, 0.25f).R);
    }

    #endregion

    #region Focus, capture and modal cleanup

    [Fact]
    public void RemovingFocusedElement_ClearsFocus()
    {
        var root = new StackPanel();
        var container = new StackPanel();
        var focusable = new FocusableTestElement();
        container.Add(focusable);
        root.Add(container);

        FocusManager.SetFocus(focusable);
        Assert.Same(focusable, FocusManager.CurrentFocused);

        root.Remove(container);

        Assert.NotSame(focusable, FocusManager.CurrentFocused);
        Assert.False(focusable.IsFocused);
    }

    [Fact]
    public void RemovingCapturingElement_ReleasesCapture()
    {
        var root = new StackPanel();
        var container = new StackPanel();
        var dragged = new FocusableTestElement();
        container.Add(dragged);
        root.Add(container);

        dragged.CapturePointer();
        Assert.True(dragged.IsPointerCaptured);

        root.Remove(container);

        Assert.False(dragged.IsPointerCaptured);
    }

    [Fact]
    public void PopModal_RestoresPreviousFocus()
    {
        var root = new StackPanel();
        var before = new FocusableTestElement();
        var dialog = new StackPanel();
        var inDialog = new FocusableTestElement();
        dialog.Add(inDialog);
        root.Add(before);
        root.Add(dialog);

        FocusManager.SetFocus(before);
        FocusManager.PushModal(dialog);
        Assert.Same(inDialog, FocusManager.CurrentFocused);

        FocusManager.PopModal(dialog);

        Assert.Same(before, FocusManager.CurrentFocused);
    }

    [Fact]
    public void RemovingModalSubtree_DropsModalScope()
    {
        var root = new StackPanel();
        var dialog = new StackPanel();
        dialog.Add(new FocusableTestElement());
        root.Add(dialog);

        FocusManager.PushModal(dialog);
        Assert.Same(dialog, FocusManager.CurrentModal);

        root.Remove(dialog);

        Assert.NotSame(dialog, FocusManager.CurrentModal);
    }

    #endregion

    #region Allocations

    private static long AllocatedBy(Action action)
    {
        action(); // warm up: JIT, lazily created caches and scratch buffers
        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void SetValue_WithUnchangedValueTypeValue_DoesNotAllocate()
    {
        var tb = new TextBlock { FontSize = 20f };
        Assert.Equal(0, AllocatedBy(() => tb.FontSize = 20f));
    }

    [Fact]
    public void GetValueUntyped_OfValueTypeDefault_DoesNotAllocate()
    {
        var tb = new TextBlock();
        Assert.Equal(0, AllocatedBy(() => tb.GetValueUntyped(UIElement.OpacityProperty)));
    }

    [Fact]
    public void ReapplyingStyles_DoesNotAllocate()
    {
        var baseStyle = new Style(typeof(TextBlock)).Set(TextBlock.FontSizeProperty, 18f);
        var style = new Style("Heading", typeof(TextBlock), baseStyle).Set(UIElement.OpacityProperty, 0.8f);
        var tb = new TextBlock { Style = style };

        Assert.Equal(0, AllocatedBy(tb.ApplyStyles));
    }

    [Fact]
    public void PointerBubbling_DoesNotAllocatePerLevel()
    {
        UIElement leaf = new MeasureCountingElement();
        UIElement current = leaf;
        for (int i = 0; i < 6; i++)
        {
            var panel = new StackPanel();
            panel.Add(current);
            current = panel;
        }

        var e = new PointerEventArgs(new Point(5, 5), new Point(5, 5));
        Assert.Equal(0, AllocatedBy(() => leaf.DispatchBubblePointerEvent(e, static (el, args) => el.OnPointerMoved(args))));
    }

    #endregion

    #region View locator

    [Fact]
    public void ViewLocator_PrefersMostDerivedRegistration_ThenBaseClassOverInterface()
    {
        var locator = new ViewLocator { EnableConventionLookup = false };
        locator.Register<ITestViewModel, TestViewC>();
        locator.Register<BaseTestViewModel, TestViewA>();
        locator.Register<DerivedTestViewModel, TestViewB>();

        Assert.IsType<TestViewB>(locator.ResolveView(new MostDerivedTestViewModel()));
        Assert.IsType<TestViewA>(locator.ResolveView(new BaseTestViewModel()));
    }

    [Fact]
    public void ViewLocator_NewRegistration_InvalidatesCache()
    {
        var locator = new ViewLocator { EnableConventionLookup = false };
        locator.Register<BaseTestViewModel, TestViewA>();
        Assert.IsType<TestViewA>(locator.ResolveView(new DerivedTestViewModel()));

        locator.Register<DerivedTestViewModel, TestViewB>();

        Assert.IsType<TestViewB>(locator.ResolveView(new DerivedTestViewModel()));
    }

    #endregion
}

[Collection("KeybindingTests")]
public class KeybindingReviewFixTests
{
    [Fact]
    public void Gesture_NumericTokens_AreRejected()
    {
        Assert.False(KeybindingGesture.TryParse("Ctrl+42", out _));
        Assert.True(KeybindingGesture.TryParse("Ctrl+5", out var digit));
        Assert.Equal(Key.D5, digit.Key);
    }

    [Fact]
    public void UnregisterKeybinding_RemovesDescriptorAndEmptyGroup()
    {
        var descriptor = new KeybindingDescriptor("Temp", "ReviewFixGroup", "Ctrl+F9", new AtelierRelayCommand(() => { }));
        KeybindingManager.RegisterKeybinding(descriptor);
        Assert.NotNull(KeybindingManager.FindKeybinding("ReviewFixGroup", Key.F9, ModifierKeys.Control));

        Assert.True(KeybindingManager.UnregisterKeybinding(descriptor));

        Assert.Null(KeybindingManager.FindKeybinding("ReviewFixGroup", Key.F9, ModifierKeys.Control));
        Assert.False(KeybindingManager.RegisteredKeybindings.ContainsKey("ReviewFixGroup"));
        Assert.False(KeybindingManager.UnregisterKeybinding("ReviewFixGroup", "Temp"));
    }
}
