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
        var left = (StackPanel)titleBar.Children[0];
        return (left, (TextBlock)((ContentControl)titleBar.Children[1]).Content!, (ContentControl)left.Children[0]);
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

    // A title bar with a title and 300 px of content, laid out at width.
    private static (TitleBar Bar, ContentControl Title, ContentControl Content, StackPanel Buttons) NarrowBar(float width)
    {
        var titleBar = new TitleBar { Title = "Atelier Gallery", Content = new Border { Width = 300, Height = 30 } };
        titleBar.Measure(new Size(width, 44));
        titleBar.Arrange(new Rect(0, 0, width, 44));
        return (titleBar, (ContentControl)titleBar.Children[1], (ContentControl)titleBar.Children[2], (StackPanel)titleBar.Children[3]);
    }

    [Fact]
    public void CaptionButtons_AlwaysKeepTheirPlaceAtTheRight()
    {
        foreach (float width in new[] { 1000f, 500f, 300f, 200f })
        {
            var (_, _, content, buttons) = NarrowBar(width);
            Assert.Equal(width, buttons.Bounds.Right, 0.5);
            Assert.Equal(3 * 46, buttons.Bounds.Width, 0.5);
            Assert.True(content.Bounds.Right <= buttons.Bounds.Left + 0.5, $"content overlaps the buttons at {width}");
        }
    }

    [Fact]
    public void WhenNarrow_TheTitleGivesWayFirst_ThenTheContent()
    {
        var (_, title, content, _) = NarrowBar(1000);
        Assert.True(title.Bounds.Width > 40);
        Assert.True(content.Bounds.Width >= 300);
        Assert.False(((TextBlock)title.Content!).IsTextTrimmed);
        Assert.True(title.ClipToBounds);

        // Room for the content but hardly for the title: the title disappears.
        (_, title, content, _) = NarrowBar(500);
        Assert.Equal(0, title.Bounds.Width);
        Assert.True(content.Bounds.Width >= 300);

        // No room for the content: it's hidden, and the title gets the space (shortened if needed).
        (_, title, content, _) = NarrowBar(260);
        Assert.Equal(0, content.Bounds.Width);
        Assert.True(title.Bounds.Width > 0);
        Assert.True(content.ClipToBounds);
    }

    [Fact]
    public void ShrinkingContent_NeverOverlapsItself_AtAnyWidth()
    {
        using var theme = ActiveTheme.Use(Atelier.Theming.Material.MaterialTheme.CreateLight());
        // Like the gallery's: a search box in a star column (it shrinks to 140), then two buttons.
        var search = new TextBox { Placeholder = "Search pages (Ctrl+F)" };
        var first = new Button("New window");
        var second = new Button("Light theme");
        var grid = new Grid { ColumnSpacing = 8, Margin = new Thickness(16, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.Add(search);
        Grid.SetColumn(first, 1);
        grid.Add(first);
        Grid.SetColumn(second, 2);
        grid.Add(second);
        var titleBar = new TitleBar { Title = "Atelier Gallery", Content = grid };
        var root = new StackPanel();
        root.Add(titleBar);
        root.AttachToHost();
        root.ApplyStylesToTree();
        try
        {
            bool shownShrunk = false, hidden = false;
            for (float width = 1000; width >= 200; width -= 5)
            {
                root.Measure(new Size(width, 44));
                root.Arrange(new Rect(0, 0, width, 44));
                var content = (ContentControl)titleBar.Children[2];
                if (content.Bounds.Width == 0)
                {
                    hidden = true;
                    continue;
                }
                Assert.True(search.Bounds.Right <= first.Bounds.Left - 7.5f, $"at {width}: search ends at {search.Bounds.Right}, the button starts at {first.Bounds.Left}");
                Assert.True(second.Bounds.Right <= grid.Bounds.Width + 0.5f, $"at {width}: the last button overflows");
                shownShrunk |= search.Bounds.Width < search.DesiredSize.Width + 1 && search.Bounds.Width < 200;
            }
            Assert.True(shownShrunk, "the search box never shrank before the content was hidden");
            Assert.True(hidden);
        }
        finally
        {
            root.DetachFromHost();
        }
    }

    [Fact]
    public void AnElementClippedToNoWidth_DrawsNoneOfItsChildren()
    {
        using var theme = ActiveTheme.Use(Atelier.Theming.Material.MaterialTheme.CreateLight());
        // A canvas lays its children out at their own size, so they overflow the element that clips to no width.
        var overflowing = new Canvas();
        overflowing.Add(new Border { Width = 40, Height = 40, Background = Color.Black });
        var hidden = new Border { ClipToBounds = true, Width = 0, Height = 40, Child = overflowing };
        var root = new Canvas { Width = 60, Height = 60 };
        root.Add(hidden);
        using var bitmap = ThemeRendering.Render(root, 60, 60);
        Assert.True(ThemeRendering.IsClose(bitmap.GetPixel(20, 20), Color.White), $"{bitmap.GetPixel(20, 20)}");
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
