#if DEBUG
using System;
using System.Linq;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.DevTools;
using Atelier.Layout;
using Atelier.Theming;
using Atelier.Theming.Material;
using Xunit;

namespace Atelier.Tests;

public class DevToolsTests
{
    // A window for the tools: the content is attached like a window's root.
    private sealed class FakeHost : IDevToolsHost
    {
        private UIElement? _content;

        public UIElement? Content
        {
            get => _content;
            set
            {
                _content?.DetachFromHost();
                _content = value;
                _content?.AttachToHost();
            }
        }

        public bool ShowFpsOverlay { get; set; }

        public int RenderRequests { get; private set; }

        public void InvalidateRender() => RenderRequests++;

        public void Layout(float width = 1400, float height = 800)
        {
            _content!.Measure(new Size(width, height));
            _content.Arrange(new Rect(0, 0, width, height));
        }
    }

    private static (FakeHost Host, StackPanel Root, Border Card, Button Button, TextBlock Text) Window()
    {
        var text = new TextBlock { Text = "Hello" };
        var button = new Button("Save") { Margin = new Thickness(10, 4) };
        var card = new Border { Padding = new Thickness(16), BorderThickness = new Thickness(2), Margin = new Thickness(8), Child = new StackPanel { Spacing = 12 } };
        ((StackPanel)card.Child!).Add(text);
        ((StackPanel)card.Child!).Add(button);
        var root = new StackPanel { Spacing = 20 };
        root.Add(new TextBlock { Text = "Title" });
        root.Add(card);
        var host = new FakeHost { Content = root };
        host.Layout();
        return (host, root, card, button, text);
    }

    private static Point Center(DevToolsSession session, UIElement element)
    {
        var rect = session.RectOf(element);
        return new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
    }

    [Fact]
    public void F12_WrapsTheContentBesideThePanel_AndClosingRestoresIt()
    {
        var (host, root, _, _, _) = Window();
        Assert.True(DevToolsManager.HandleKey(host, new KeyEventArgs(Key.F12)));
        var session = DevToolsManager.GetSession(host)!;
        Assert.Same(session.Layout, host.Content);
        Assert.Same(session.Layout, root.Parent);
        Assert.Contains(session.Layout.Children, c => c is GridSplitter);
        Assert.Contains(session.Layout.Children, c => c == session.Panel);
        Assert.Same(root, session.InspectedRoot);
        Assert.Same(root, session.Panel.Roots[0].Element);

        Assert.True(DevToolsManager.HandleKey(host, new KeyEventArgs(Key.F12)));
        Assert.False(DevToolsManager.IsOpen(host));
        Assert.Same(root, host.Content);
        Assert.Null(root.Parent);
        Assert.False(DevToolsManager.HandleKey(host, new KeyEventArgs(Key.F11)));
    }

    [Fact]
    public void Picking_HighlightsTheElementUnderThePointer_AndAClickSelectsIt()
    {
        var (host, _, card, button, _) = Window();
        Assert.True(DevToolsManager.HandleKey(host, new KeyEventArgs(Key.C, 0, ModifierKeys.Control | ModifierKeys.Shift)));
        var session = DevToolsManager.GetSession(host)!;
        host.Layout();
        Assert.True(session.IsPicking);

        // The overlay takes the pointer only while picking.
        var point = Center(session, button);
        Assert.Same(session.Overlay, session.Overlay.HitTest(point));
        Assert.Same(button.Children.OfType<UIElement>().FirstOrDefault() ?? button, session.ElementAt(point) is TextBlock t && t.Parent == button ? t : session.ElementAt(point));

        session.Overlay.OnPointerMoved(new PointerEventArgs(point, point));
        Assert.NotNull(session.Overlay.HoveredElement);
        session.Overlay.OnPointerPressed(new PointerEventArgs(point, point, PointerButtons.Left));
        Assert.False(session.IsPicking);
        Assert.NotNull(session.SelectedElement);
        Assert.True(IsSelfOrInside(session.SelectedElement!, button));
        Assert.Null(session.Overlay.HitTest(point));

        // Escape stops picking.
        session.IsPicking = true;
        Assert.True(DevToolsManager.HandleKey(host, new KeyEventArgs(Key.Escape)));
        Assert.False(session.IsPicking);

        // A point in the card's padding is the card.
        var cardRect = session.RectOf(card);
        Assert.Same(card, session.ElementAt(new Point(cardRect.X + 5, cardRect.Y + 5)));
    }

    private static bool IsSelfOrInside(UIElement element, UIElement ancestor)
    {
        for (VisualNode? node = element; node != null; node = node.Parent)
        {
            if (node == ancestor) return true;
        }
        return false;
    }

    [Fact]
    public void Selecting_RevealsTheElementInTheTree_AndShowsItsProperties()
    {
        var (host, _, _, button, _) = Window();
        var session = DevToolsManager.Open(host)!;
        host.Layout();

        session.Select(button);
        var panel = session.Panel;
        Assert.Same(button, ((ElementNode)panel.Tree.SelectedItem!).Element);
        var inspection = Assert.IsType<ElementInspection>(panel.Properties.SelectedObject);
        Assert.Same(button, inspection.Element);
        Assert.Contains(inspection.GetProperties(), p => p.Name == "Margin");
        Assert.Contains(panel.Values.View.Cast<PropertyValueRow>(), r => r.Name == "Margin" && r.Value == "10,4" && r.Source == "Local");

        // Editing through the grid's descriptor changes the element, and the values table follows.
        var padding = inspection.GetProperties().Single(p => p.Name == "Padding");
        padding.SetValue(inspection, new Thickness(3));
        Assert.Equal(new Thickness(3), button.Padding);
        Assert.Equal("3", panel.Values.View.Cast<PropertyValueRow>().Single(r => r.Name == "Padding").Value);

        // Choosing a node in the tree selects its element.
        var root = panel.Roots[0];
        panel.Tree.SelectedItem = root;
        Assert.Same(session.InspectedRoot, session.SelectedElement);
    }

    [Fact]
    public void FindNext_SearchesTheTreeByTypeAndText()
    {
        var (host, _, _, _, text) = Window();
        var session = DevToolsManager.Open(host)!;
        Assert.True(session.Panel.FindNext("Hello"));
        Assert.Same(text, session.SelectedElement);
        Assert.True(session.Panel.FindNext("TextBlock"));
        Assert.NotSame(text, session.SelectedElement); // the next TextBlock
        Assert.False(session.Panel.FindNext("NoSuchThing"));
    }

    [Fact]
    public void SelectFocused_SelectsTheContentsFocusedElement_EvenAfterTheToolsTookTheFocus()
    {
        var (host, _, _, button, _) = Window();
        var session = DevToolsManager.Open(host)!;
        Assert.False(session.Panel.SelectFocused());

        button.Focus();
        session.Panel.SearchBox.Focus(); // like clicking the tools' button, which focuses it
        Assert.True(session.Panel.SearchBox.IsFocused);
        Assert.Same(button, session.FocusedElement);
        Assert.True(session.Panel.SelectFocused());
        Assert.Same(button, session.SelectedElement);

        // Clearing the focus forgets it.
        button.Focus();
        button.Unfocus();
        Assert.Null(session.FocusedElement);
        DevToolsManager.Close(host);
    }

    [Fact]
    public void OpeningAndClosing_KeepTheContentsFocus()
    {
        var (host, _, _, button, _) = Window();
        button.Focus();
        var session = DevToolsManager.Open(host)!;
        Assert.True(button.IsFocused);
        Assert.Same(button, session.FocusedElement);

        session.Panel.SearchBox.Focus();
        DevToolsManager.Close(host);
        Assert.True(button.IsFocused);
    }

    [Fact]
    public void BoxModel_HasMarginBorderPaddingContentAndSpacing()
    {
        var (host, root, card, _, _) = Window();
        var session = DevToolsManager.Open(host)!;
        host.Layout();

        var box = BoxModel.Compute(card, session.RectOf);
        var bounds = session.RectOf(card);
        Assert.Equal(bounds.X - 8, box.Margin.X, 1);
        Assert.Equal(bounds.X + 2, box.Padding.X, 1);
        Assert.Equal(bounds.X + 18, box.Content.X, 1);
        Assert.Equal(new Thickness(16), box.PaddingThickness);

        var stack = (StackPanel)card.Child!;
        var stackBox = BoxModel.Compute(stack, session.RectOf);
        Assert.Equal("12", stackBox.Spacing);
        var gap = Assert.Single(stackBox.Gaps);
        Assert.Equal(12, gap.Height, 1);

        Assert.Single(BoxModel.Compute(root, session.RectOf).Gaps);
    }

    [Fact]
    public void HotReload_ReplacesTheInspectedContent()
    {
        var (host, _, _, _, _) = Window();
        var session = DevToolsManager.Open(host)!;
        var fresh = new TextBlock { Text = "Reloaded" };
        Assert.True(DevToolsManager.TryReplaceContent(host, fresh));
        Assert.Same(fresh, session.InspectedRoot);
        Assert.Same(session.Layout, fresh.Parent);
        Assert.Same(fresh, session.Panel.Roots[0].Element);

        DevToolsManager.Close(host);
        Assert.Same(fresh, host.Content);
        Assert.False(DevToolsManager.TryReplaceContent(host, fresh));
    }

    [Fact]
    public void Properties_ListAttachedOnes_AndSidesParse()
    {
        _ = Grid.RowProperty; // attached properties register when their declaring type is used
        var properties = BindableProperty.GetPropertiesFor(typeof(Button));
        Assert.Contains(properties, p => p.Name == "Margin");
        Assert.Contains(properties, p => p.Name == "Padding");
        Assert.Contains(properties, p => p.IsAttached && p.Name == "Row");
        Assert.DoesNotContain(properties, p => p.Name == "Spacing");

        Assert.True(ElementInspection.TryParseSides("8", out var a, out var b, out var c, out var d));
        Assert.Equal((8f, 8f, 8f, 8f), (a, b, c, d));
        Assert.True(ElementInspection.TryParseSides("8,4", out a, out b, out c, out d));
        Assert.Equal((8f, 4f, 8f, 4f), (a, b, c, d));
        Assert.True(ElementInspection.TryParseSides("1 2 3 4", out a, out b, out c, out d));
        Assert.Equal((1f, 2f, 3f, 4f), (a, b, c, d));
        Assert.False(ElementInspection.TryParseSides("1,2,3", out _, out _, out _, out _));
        Assert.Equal("10,4", ElementInspection.Format(new Thickness(10, 4)));
    }

    [Fact]
    public void Toggles_ReachTheHost()
    {
        var (host, _, _, _, _) = Window();
        var session = DevToolsManager.Open(host)!;
        session.ShowAllBounds = true;
        session.ShowBoxModel = false;
        Assert.True(host.RenderRequests > 0);
        host.ShowFpsOverlay = false;
        DevToolsManager.Close(host);
        Assert.Null(DevToolsManager.GetSession(host));
    }
}
#endif
