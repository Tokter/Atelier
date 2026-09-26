using System;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Platform;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Tests;

/// <summary>A host window whose state can change behind the title bar's back, like Win+Up or snapping.</summary>
public class StateReportingHostWindow : IHostWindow
{
    private EventHandler? _stateChanged;

    public bool IsMaximized { get; private set; }
    public int ToggleCount { get; private set; }
    public int DragCount { get; private set; }
    public int SubscriberCount => _stateChanged?.GetInvocationList().Length ?? 0;

    public event EventHandler? WindowStateChanged
    {
        add => _stateChanged += value;
        remove => _stateChanged -= value;
    }

    public void SetMaximizedExternally(bool value)
    {
        IsMaximized = value;
        _stateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Minimize() { }
    public void ToggleMaximize() { ToggleCount++; IsMaximized = !IsMaximized; }
    public void Close() { }
    public void DragMove() => DragCount++;
}

public class TitleBarTests
{
    private static PointerEventArgs Press(int clickCount) =>
        new(new Point(100, 20), new Point(100, 20), PointerButtons.Left, clickCount: clickCount);

    private static (StackPanel Left, TextBlock Title, ContentControl IconContainer) GetLeftParts(TitleBar titleBar)
    {
        var dock = (DockPanel)titleBar.Children[0];
        var left = (StackPanel)dock.Children[0];
        return (left, (TextBlock)left.Children[1], (ContentControl)left.Children[0]);
    }

    [Fact]
    public void TripleClick_TogglesMaximizeOnlyOnce()
    {
        int toggles = 0, drags = 0;
        var titleBar = new TitleBar { OnMaximize = () => toggles++, OnDragMove = () => drags++ };

        titleBar.OnPointerPressed(Press(1));
        titleBar.OnPointerPressed(Press(2));
        titleBar.OnPointerPressed(Press(3));

        Assert.Equal(1, toggles);
        Assert.Equal(1, drags);
    }

    [Fact]
    public void RightClick_NeitherDragsNorMaximizes()
    {
        int toggles = 0, drags = 0;
        var titleBar = new TitleBar { OnMaximize = () => toggles++, OnDragMove = () => drags++ };

        titleBar.OnPointerPressed(new PointerEventArgs(new Point(100, 20), new Point(100, 20), PointerButtons.Right, clickCount: 1));

        Assert.Equal(0, toggles + drags);
    }

    [Fact]
    public void IsMaximized_FollowsTheHostWindow_WhileAttached()
    {
        var host = new StateReportingHostWindow();
        host.SetMaximizedExternally(true);
        var root = new StackPanel();
        var titleBar = new TitleBar();
        root.Add(titleBar);
        root.AttachToHost(host);

        Assert.True(titleBar.IsMaximized); // synced on attach
        Assert.Equal(MaterialIconKind.FilterNone, titleBar.MaximizeIcon.Kind);

        host.SetMaximizedExternally(false);
        Assert.False(titleBar.IsMaximized);
        Assert.Equal(MaterialIconKind.CropSquare, titleBar.MaximizeIcon.Kind);

        // Double click on the bar toggles the host
        titleBar.OnPointerPressed(Press(2));
        Assert.Equal(1, host.ToggleCount);
        Assert.True(titleBar.IsMaximized);

        root.Remove(titleBar);
        Assert.Equal(0, host.SubscriberCount); // no leak through the window's event
        root.DetachFromHost();
    }

    [Fact]
    public void MaximizeButton_TogglesTheHostWindow()
    {
        var host = new StateReportingHostWindow();
        var root = new StackPanel();
        var titleBar = new TitleBar();
        root.Add(titleBar);
        root.AttachToHost(host);

        var button = titleBar.MaximizeButton;
        button.OnPointerEntered(new PointerEventArgs(default));
        button.OnPointerPressed(new PointerEventArgs(default, PointerButtons.Left));
        button.OnPointerReleased(new PointerEventArgs(default, PointerButtons.Left));

        Assert.Equal(1, host.ToggleCount);
        Assert.True(titleBar.IsMaximized);
        root.DetachFromHost();
    }

    [Fact]
    public void IconAndTitle_AreCollapsedUntilSet()
    {
        var titleBar = new TitleBar();
        var (_, title, icon) = GetLeftParts(titleBar);

        Assert.Equal(Visibility.Collapsed, title.Visibility);
        Assert.Equal(Visibility.Collapsed, icon.Visibility);

        titleBar.Title = "App";
        titleBar.Icon = new Border();
        Assert.Equal(Visibility.Visible, title.Visibility);
        Assert.Equal(Visibility.Visible, icon.Visibility);
    }

    [Fact]
    public void TitleFont_IsInheritedFromTheTitleBar()
    {
        var titleBar = new TitleBar { Title = "App" };
        var (_, title, _) = GetLeftParts(titleBar);

        titleBar.FontSize = 20;
        titleBar.FontFamily = "Consolas";

        Assert.Equal(20, title.FontSize);
        Assert.Equal("Consolas", title.FontFamily);
    }

    [Fact]
    public void CaptionButtonAndTitleBarSizes_AreDefaultsThatStylesCanOverride()
    {
        var titleBar = new TitleBar();
        var button = titleBar.CloseButton;

        Assert.Equal(46f, button.Width);
        Assert.Equal(44f, button.Height);
        Assert.Equal(ButtonVariant.Text, button.Variant);
        Assert.False(button.IsFocusable);
        Assert.Equal(ValueSource.Default, button.GetValueSource(UIElement.WidthProperty));
        Assert.Equal(ValueSource.Default, button.GetValueSource(Button.VariantProperty));
        Assert.Equal(ValueSource.Default, button.GetValueSource(Control.CornerRadiusProperty));
        Assert.Equal(44f, titleBar.Height);
        Assert.Equal(ValueSource.Default, titleBar.GetValueSource(UIElement.HeightProperty));
    }
}

public class ToolbarLayoutTests
{
    [Fact]
    public void Measure_AddsBorderAndPadding_AndCollapsedContentTakesNoSpace()
    {
        var content = new Border { Width = 100, Height = 20 };
        var toolbar = new Toolbar(content) { Padding = new Thickness(10, 8), BorderThickness = new Thickness(1) };

        toolbar.Measure(new Size(500, 500));
        Assert.Equal(new Size(122, 38), toolbar.DesiredSize);

        toolbar.Arrange(new Rect(0, 0, 122, 38));
        Assert.Equal(new Rect(11, 9, 100, 20), content.Bounds);

        content.Visibility = Visibility.Collapsed;
        toolbar.Measure(new Size(500, 500));
        Assert.Equal(new Size(22, 18), toolbar.DesiredSize);
    }
}
