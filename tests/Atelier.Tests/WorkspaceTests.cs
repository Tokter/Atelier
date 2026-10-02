using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Theming.Material;

namespace Atelier.Tests;

public class WorkspaceTests
{
    private const float Width = 1000, Height = 600;

    // An editor's content that records being disposed.
    private sealed class TestEditor(string id) : Border, IDisposable
    {
        public string Id { get; } = id;
        public bool IsDisposed { get; private set; }
        public void Dispose() => IsDisposed = true;
    }

    private sealed class Editors
    {
        public readonly AreaEditorRegistry Registry = new();
        public readonly List<TestEditor> Created = [];

        public Editors()
        {
            Add("a", "Alpha", null);
            Add("b", "Beta", null);
            Add("c", "Gamma", "Data");
            Add("d", "Delta", "Data");
        }

        private void Add(string id, string title, string? category) =>
            Registry.Register(id, title, MaterialIconKind.Dashboard, _ =>
            {
                var editor = new TestEditor(id);
                Created.Add(editor);
                return editor;
            }, category);
    }

    private static AreaLayout Layout(AreaLayout layout)
    {
        layout.Measure(new Size(Width, Height));
        layout.Arrange(new Rect(0, 0, Width, Height));
        return layout;
    }

    private static AreaLayout Create(AreaDefinition definition, out Editors editors)
    {
        editors = new Editors();
        return Layout(new AreaLayout(editors.Registry, definition));
    }

    private static string Ids(AreaLayout layout) => string.Join(",", layout.Areas.Select(a => a.EditorId));

    private static Area AreaOf(AreaLayout layout, string editorId) => layout.Areas.First(a => a.EditorId == editorId);

    private static Point Screen(UIElement element, float x, float y) => element.PointToScreen(new Point(x, y));

    private static PointerEventArgs At(Point screen, PointerButtons button = PointerButtons.None, ModifierKeys modifiers = ModifierKeys.None, int clicks = 0) =>
        new(screen, screen, button, 0, modifiers, clicks);

    // Drags the corner to `to` (in window coordinates) through `via`, and releases it.
    private static void DragCorner(AreaCorner corner, Point to, ModifierKeys modifiers = ModifierKeys.None, Point? via = null)
    {
        var start = Screen(corner, AreaCorner.Size / 2, AreaCorner.Size / 2);
        corner.OnPointerPressed(At(start, PointerButtons.Left, clicks: 1));
        if (via is { } middle) corner.OnPointerMoved(At(middle, modifiers: modifiers));
        corner.OnPointerMoved(At(to, modifiers: modifiers));
        corner.OnPointerReleased(At(to, PointerButtons.Left, modifiers));
    }

    private static void Click(ButtonBase item)
    {
        var p = new Point(5, 5);
        item.OnPointerEntered(new PointerEventArgs(p, p));
        item.OnPointerPressed(new PointerEventArgs(p, p, PointerButtons.Left));
        item.OnPointerReleased(new PointerEventArgs(p, p, PointerButtons.Left));
    }

    private static MenuItem Item(ContextMenu menu, string header) => menu.Items.OfType<MenuItem>().First(i => (string?)i.Header == header);

    #region Layout and editors

    [Fact]
    public void Definitions_BuildTheTree_AndShareTheSpaceByWeight()
    {
        var layout = Create(AreaDefinition.Row(
            AreaDefinition.Editor("a", 3),
            AreaDefinition.Column(AreaDefinition.Editor("b"), AreaDefinition.Editor("c"))), out _);

        var root = Assert.IsType<AreaSplit>(layout.Root);
        Assert.Equal(Orientation.Horizontal, root.Orientation);
        Assert.Equal("a,b,c", Ids(layout));

        // 4 px around the areas and between them.
        var a = AreaOf(layout, "a");
        var b = AreaOf(layout, "b");
        float inner = Width - 2 * AreaLayout.DefaultSpacing - AreaLayout.DefaultSpacing;
        Assert.Equal(inner * 0.75f, a.Bounds.Width, 0.5f);
        Assert.Equal(inner * 0.25f, b.Bounds.Width, 0.5f);
        Assert.Equal(AreaLayout.DefaultSpacing, layout.GetBoundsInLayout(a).X);
        Assert.Equal((Height - 3 * AreaLayout.DefaultSpacing) / 2, b.Bounds.Height, 0.5f);
        Assert.Single(root.Borders);
    }

    [Fact]
    public void Splits_OfOneOrientation_JoinTheirParent()
    {
        var layout = Create(AreaDefinition.Row(
            AreaDefinition.Editor("a"),
            AreaDefinition.Row(AreaDefinition.Editor("b"), AreaDefinition.Editor("c")).WithWeight(2)), out _);

        var root = Assert.IsType<AreaSplit>(layout.Root);
        Assert.Equal(3, root.Nodes.Count);
        Assert.Equal(new[] { 1f, 1f, 1f }, Enumerable.Range(0, 3).Select(root.GetWeight));
    }

    [Fact]
    public void Areas_CreateTheirEditors_AndKeepThemWhenSwitchingBack()
    {
        var layout = Create(AreaDefinition.Editor("a"), out var editors);
        var area = Assert.IsType<Area>(layout.Root);
        var first = Assert.IsType<TestEditor>(area.EditorContent);
        Assert.Equal("Alpha", area.EditorType!.Title);

        area.EditorId = "b";
        Assert.Equal("b", Assert.IsType<TestEditor>(area.EditorContent).Id);
        area.EditorId = "a";
        Assert.Same(first, area.EditorContent);
        Assert.Equal(2, editors.Created.Count);
    }

    [Fact]
    public void UnknownEditors_ShowAPlaceholder()
    {
        var layout = Create(AreaDefinition.Editor("missing"), out _);
        var area = (Area)layout.Root!;
        Assert.Null(area.EditorType);
        Assert.Contains("missing", Assert.IsType<TextBlock>(area.EditorContent).Text);
    }

    [Fact]
    public void EditorMenu_ListsTheEditorsByCategory_AndSwitches()
    {
        var layout = Create(AreaDefinition.Editor("a"), out _);
        layout.AttachToHost();
        var area = (Area)layout.Root!;

        var menu = area.ShowEditorMenu()!;
        Assert.NotNull(menu);
        Assert.Equal(new object?[] { "Alpha", "Beta", null, "Gamma", "Delta" },
            menu.Items.Select(i => i is MenuItem item ? item.Header : null));
        Assert.True(Item(menu, "Alpha").IsChecked);
        Assert.Equal(PlacementMode.Bottom, menu.Placement);

        Click(Item(menu, "Delta"));
        Assert.Equal("d", area.EditorId);
        Assert.False(menu.IsOpen);
        layout.DetachFromHost();
    }

    [Fact]
    public void Registry_GroupsUncategorizedEditorsFirst_AndReplacesById()
    {
        var registry = new AreaEditorRegistry()
            .Register("x", "X", MaterialIconKind.Add, _ => new Border(), category: "One")
            .Register("y", "Y", MaterialIconKind.Add, _ => new Border())
            .Register("x", "X2", MaterialIconKind.Add, _ => new Border(), category: "One");

        Assert.Equal(new[] { "X2", "Y" }, registry.Editors.Select(e => e.Title));
        var groups = registry.GetGroups();
        Assert.Equal(new string?[] { null, "One" }, groups.Select(g => g.Category));
        Assert.True(registry.Unregister("y"));
        Assert.Null(registry.Find("y"));
    }

    #endregion

    #region Operations

    [Fact]
    public void Split_ReplacesTheRootArea_WithASplitOfTwoAreas()
    {
        var layout = Create(AreaDefinition.Editor("a"), out var editors);
        var area = (Area)layout.Root!;

        var created = layout.Split(area, Orientation.Vertical, 0.25f);
        Layout(layout);

        var root = Assert.IsType<AreaSplit>(layout.Root);
        Assert.Equal(Orientation.Vertical, root.Orientation);
        Assert.Equal(new UIElement[] { area, created }, root.Nodes);
        Assert.Equal("a", created.EditorId);
        Assert.NotSame(area.EditorContent, created.EditorContent);
        Assert.Equal(2, editors.Created.Count);
        Assert.Equal(0.25f, root.GetWeight(0) / (root.GetWeight(0) + root.GetWeight(1)), 3);
    }

    [Fact]
    public void Split_InsertsIntoAParentOfTheSameOrientation_AndNestsOtherwise()
    {
        var layout = Create(AreaDefinition.Row(AreaDefinition.Editor("a"), AreaDefinition.Editor("b")), out _);
        var a = AreaOf(layout, "a");
        var root = (AreaSplit)layout.Root!;

        var left = layout.Split(a, Orientation.Horizontal, 0.5f, newAreaFirst: true, editorId: "c");
        Assert.Same(root, layout.Root);
        Assert.Equal("c,a,b", Ids(layout));
        Assert.Equal(1f, root.GetWeight(0) + root.GetWeight(1), 3); // a's share was divided

        var below = layout.Split(a, Orientation.Vertical, editorId: "d");
        var nested = Assert.IsType<AreaSplit>(root.Nodes[1]);
        Assert.Equal(Orientation.Vertical, nested.Orientation);
        Assert.Equal(new UIElement[] { a, below }, nested.Nodes);
        Assert.Equal("c,a,d,b", Ids(layout));
        Assert.Same(layout, left.Layout);
    }

    [Fact]
    public void Join_OnlyNeighborsInOneSplit_AndCollapsesTheSplit()
    {
        var layout = Create(AreaDefinition.Row(
            AreaDefinition.Editor("a"),
            AreaDefinition.Column(AreaDefinition.Editor("b"), AreaDefinition.Editor("c"))), out _);
        var a = AreaOf(layout, "a");
        var b = AreaOf(layout, "b");
        var c = AreaOf(layout, "c");
        var cContent = (TestEditor)c.EditorContent!;

        Assert.False(layout.CanJoin(a, b)); // a's edge is longer than b's
        Assert.True(layout.CanJoin(b, c));

        bool changed = false;
        layout.LayoutChanged += (_, _) => changed = true;
        Assert.True(layout.Join(b, c));
        Assert.True(changed);
        Assert.True(cContent.IsDisposed);
        Assert.Null(c.Layout);

        var root = Assert.IsType<AreaSplit>(layout.Root);
        Assert.Equal(new UIElement[] { a, b }, root.Nodes);
        Assert.True(layout.Join(a, b));
        Assert.Same(a, layout.Root);
    }

    [Fact]
    public void Close_GivesTheSpaceToTheNeighbor_AndFlattensSplits()
    {
        var layout = Create(AreaDefinition.Row(
            AreaDefinition.Editor("a", 2),
            AreaDefinition.Column(
                AreaDefinition.Row(AreaDefinition.Editor("b"), AreaDefinition.Editor("c")),
                AreaDefinition.Editor("d")).WithWeight(2)), out _);

        Assert.True(layout.Close(AreaOf(layout, "d")));
        var root = Assert.IsType<AreaSplit>(layout.Root);
        Assert.Equal(Orientation.Horizontal, root.Orientation);
        Assert.Equal(3, root.Nodes.Count); // b and c joined the root row
        Assert.Equal(new[] { 2f, 1f, 1f }, Enumerable.Range(0, 3).Select(root.GetWeight));

        Assert.True(layout.Close(AreaOf(layout, "a")));
        Assert.Equal(3f, ((AreaSplit)layout.Root!).GetWeight(0));
        Assert.True(layout.Close(AreaOf(layout, "b")));
        var last = AreaOf(layout, "c");
        Assert.Same(last, layout.Root);
        Assert.False(layout.CanClose(last));
        Assert.False(layout.Close(last));
    }

    [Fact]
    public void Swap_ExchangesPlaces_InOneSplitAndAcrossSplits()
    {
        var layout = Create(AreaDefinition.Row(
            AreaDefinition.Editor("a", 3),
            AreaDefinition.Column(AreaDefinition.Editor("b"), AreaDefinition.Editor("c"))), out _);
        var root = (AreaSplit)layout.Root!;

        Assert.True(layout.Swap(AreaOf(layout, "b"), AreaOf(layout, "c")));
        Assert.Equal("a,c,b", Ids(layout));

        Assert.True(layout.Swap(AreaOf(layout, "a"), AreaOf(layout, "b")));
        Assert.Equal("b,c,a", Ids(layout));
        Assert.Same(AreaOf(layout, "b"), root.Nodes[0]);
        Assert.Equal(3f, root.GetWeight(0)); // sizes stay with the places
        Assert.False(layout.Swap(AreaOf(layout, "a"), AreaOf(layout, "a")));
    }

    [Fact]
    public void Maximize_FillsTheLayout_AndRestoreBringsTheOthersBack()
    {
        var layout = Create(AreaDefinition.Row(AreaDefinition.Editor("a"), AreaDefinition.Editor("b")), out _);
        var definition = layout.ToDefinition();
        var b = AreaOf(layout, "b");

        layout.Maximize(b);
        Layout(layout);
        Assert.Same(b, layout.MaximizedArea);
        Assert.True(b.IsMaximized);
        Assert.Equal(Width - 2 * AreaLayout.DefaultSpacing, b.Bounds.Width);
        Assert.Equal("a,b", Ids(layout));
        Assert.Equal(definition, layout.ToDefinition(), DefinitionComparer.Instance);
        Assert.All(b.Corners, c => Assert.False(c.IsHitTestVisible));

        layout.RestoreMaximized();
        Layout(layout);
        Assert.Null(layout.MaximizedArea);
        Assert.Same(b, ((AreaSplit)layout.Root!).Nodes[1]);
        Assert.True(b.Bounds.Width < Width / 2);
        Assert.All(b.Corners, c => Assert.True(c.IsHitTestVisible));
    }

    [Fact]
    public void CtrlSpace_MaximizesTheHoveredArea_AndRestores()
    {
        var layout = Create(AreaDefinition.Row(AreaDefinition.Editor("a"), AreaDefinition.Editor("b")), out _);
        var a = AreaOf(layout, "a");
        layout.OnPreviewPointerMoved(At(Screen(a, 50, 50)));
        Assert.Same(a, layout.HoveredArea);

        var key = new KeyEventArgs(Key.Space, 0, ModifierKeys.Control, true);
        layout.OnKeyDown(key);
        Assert.True(key.Handled);
        Assert.Same(a, layout.MaximizedArea);
        layout.OnKeyDown(new KeyEventArgs(Key.Space, 0, ModifierKeys.Control, true));
        Assert.Null(layout.MaximizedArea);
    }

    [Fact]
    public void Splitting_RestoresAMaximizedLayoutFirst()
    {
        var layout = Create(AreaDefinition.Row(AreaDefinition.Editor("a"), AreaDefinition.Editor("b")), out _);
        var a = AreaOf(layout, "a");
        layout.Maximize(a);
        layout.Split(a, Orientation.Vertical);
        Assert.Null(layout.MaximizedArea);
        Assert.Equal("a,a,b", Ids(layout));
    }

    #endregion

    #region Borders

    [Fact]
    public void DraggingABorder_ResizesTheNeighbors_WithinTheirMinimumSizes()
    {
        var layout = Create(AreaDefinition.Row(AreaDefinition.Editor("a"), AreaDefinition.Editor("b")), out _);
        var root = (AreaSplit)layout.Root!;
        var border = root.Borders[0];
        var a = AreaOf(layout, "a");
        float before = a.Bounds.Width;

        var start = Screen(border, border.Bounds.Width / 2, 100);
        border.OnPointerPressed(At(start, PointerButtons.Left, clicks: 1));
        Assert.True(border.IsDragging);
        border.OnPointerMoved(At(start.Offset(100, 40)));
        Layout(layout);
        Assert.Equal(before + 100, a.Bounds.Width, 0.5f);

        border.OnPointerMoved(At(start.Offset(-2000, 0)));
        Layout(layout);
        Assert.Equal(AreaLayout.MinAreaWidth, a.Bounds.Width, 0.5f);

        // Escape puts it back.
        layout.OnPreviewKeyDown(new KeyEventArgs(Key.Escape, 0, ModifierKeys.None, true));
        Layout(layout);
        Assert.False(border.IsDragging);
        Assert.Equal(before, a.Bounds.Width, 0.5f);
    }

    [Fact]
    public void Border_ShowsAResizeCursor_ForItsDirection()
    {
        var layout = Create(AreaDefinition.Row(
            AreaDefinition.Editor("a"),
            AreaDefinition.Column(AreaDefinition.Editor("b"), AreaDefinition.Editor("c"))), out _);
        var root = (AreaSplit)layout.Root!;
        Assert.Equal(CursorType.SizeWestEast, UIElement.GetEffectiveCursor(root.Borders[0]));
        Assert.Equal(CursorType.SizeNorthSouth, UIElement.GetEffectiveCursor(((AreaSplit)root.Nodes[1]).Borders[0]));
        Assert.Equal(CursorType.Crosshair, UIElement.GetEffectiveCursor(AreaOf(layout, "a").Corners[0]));
    }

    [Fact]
    public void AreaOptions_OfferSplitJoinAndSwap_ForTheAreasAtTheBorder()
    {
        var layout = Create(AreaDefinition.Row(
            AreaDefinition.Editor("a"),
            AreaDefinition.Column(AreaDefinition.Editor("b"), AreaDefinition.Editor("c"))), out _);
        layout.AttachToHost();
        var root = (AreaSplit)layout.Root!;

        var outer = layout.ShowAreaOptions(root.Borders[0])!;
        Assert.Equal(new object?[] { "Vertical Split", "Horizontal Split", null, "Join Areas", "Swap Areas" },
            outer.Items.Select(i => i is MenuItem item ? item.Header : null));
        Assert.False(Item(outer, "Join Areas").IsEnabled); // the right side is a split
        outer.IsOpen = false;

        var inner = layout.ShowAreaOptions(((AreaSplit)root.Nodes[1]).Borders[0])!;
        Assert.True(Item(inner, "Join Areas").IsEnabled);
        Click(Item(inner, "Swap Areas"));
        Assert.Equal("a,c,b", Ids(layout));
        layout.DetachFromHost();
    }

    [Fact]
    public void RightReleaseOnABorder_OpensTheAreaOptions()
    {
        var layout = Create(AreaDefinition.Row(AreaDefinition.Editor("a"), AreaDefinition.Editor("b")), out _);
        layout.AttachToHost();
        var border = ((AreaSplit)layout.Root!).Borders[0];
        var p = Screen(border, 2, 50);

        var release = At(p, PointerButtons.Right);
        border.OnPointerReleased(release);
        Assert.True(release.Handled);
        Assert.Contains(PopupManager.ActivePopups, popup => popup is ContextMenu { IsOpen: true });
        foreach (var popup in PopupManager.ActivePopups.ToList()) popup.IsOpen = false;
        layout.DetachFromHost();
    }

    #endregion

    #region Corners

    [Fact]
    public void DraggingACornerIntoTheArea_SplitsIt_TowardsTheCorner()
    {
        var layout = Create(AreaDefinition.Editor("a"), out _);
        var area = (Area)layout.Root!;
        var topRight = area.Corners[1];

        // A leftward drag from the top-right corner: side by side, the new area on the right, the line at the pointer.
        var to = Screen(area, 700, 200);
        var start = Screen(topRight, AreaCorner.Size / 2, AreaCorner.Size / 2);
        topRight.OnPointerPressed(At(start, PointerButtons.Left, clicks: 1));
        topRight.OnPointerMoved(At(to));
        Assert.Equal(AreaPreviewKind.Split, layout.PreviewKind);
        Assert.Equal(Orientation.Horizontal, layout.PreviewOrientation);
        Assert.False(layout.PreviewNewAreaFirst);
        Assert.Equal(layout.PointToClient(to).X, layout.PreviewSplitLine.X + 1, 1f);
        topRight.OnPointerReleased(At(to, PointerButtons.Left));
        Layout(layout);

        Assert.Equal(AreaPreviewKind.None, layout.PreviewKind);
        var root = Assert.IsType<AreaSplit>(layout.Root);
        Assert.Same(area, root.Nodes[0]);
        Assert.Equal(700 - AreaLayout.DefaultSpacing / 2, area.Bounds.Width, 1.5f);
    }

    [Fact]
    public void DraggingACornerDown_StacksTheAreas()
    {
        var layout = Create(AreaDefinition.Editor("a"), out _);
        var area = (Area)layout.Root!;
        DragCorner(area.Corners[0], Screen(area, 20, 300)); // top left, downwards: the new area on top

        var root = Assert.IsType<AreaSplit>(layout.Root);
        Assert.Equal(Orientation.Vertical, root.Orientation);
        Assert.Same(area, root.Nodes[1]);
    }

    [Fact]
    public void SmallMovements_DoNotSplit()
    {
        var layout = Create(AreaDefinition.Editor("a"), out _);
        var area = (Area)layout.Root!;
        DragCorner(area.Corners[0], Screen(area.Corners[0], 10, 9));
        Assert.Same(area, layout.Root);
    }

    [Fact]
    public void DraggingACornerIntoANeighbor_JoinsTheNeighbor()
    {
        var layout = Create(AreaDefinition.Row(AreaDefinition.Editor("a"), AreaDefinition.Editor("b")), out _);
        var a = AreaOf(layout, "a");
        var b = AreaOf(layout, "b");
        var corner = a.Corners[1]; // top right
        var start = Screen(corner, 7, 7);
        corner.OnPointerPressed(At(start, PointerButtons.Left, clicks: 1));
        corner.OnPointerMoved(At(Screen(b, 100, 100)));

        Assert.Equal(AreaPreviewKind.Join, layout.PreviewKind);
        Assert.Same(a, layout.PreviewArea);
        Assert.Same(b, layout.PreviewTarget);
        Assert.Equal(Dock.Right, layout.PreviewDirection);
        corner.OnPointerReleased(At(Screen(b, 100, 100), PointerButtons.Left));

        Assert.Same(a, layout.Root);
        Assert.Null(b.Layout);
    }

    [Fact]
    public void DraggingACornerIntoAnAreaWithoutASharedEdge_DoesNothing()
    {
        var layout = Create(AreaDefinition.Row(
            AreaDefinition.Editor("a"),
            AreaDefinition.Column(AreaDefinition.Editor("b"), AreaDefinition.Editor("c"))), out _);
        var a = AreaOf(layout, "a");
        var b = AreaOf(layout, "b");
        var corner = a.Corners[1];
        corner.OnPointerPressed(At(Screen(corner, 7, 7), PointerButtons.Left, clicks: 1));
        corner.OnPointerMoved(At(Screen(b, 50, 50)));
        Assert.Equal(AreaPreviewKind.None, layout.PreviewKind);
        corner.OnPointerReleased(At(Screen(b, 50, 50), PointerButtons.Left));
        Assert.Equal("a,b,c", Ids(layout));
    }

    [Fact]
    public void CtrlDraggingACorner_SwapsAreas()
    {
        var layout = Create(AreaDefinition.Row(
            AreaDefinition.Editor("a"),
            AreaDefinition.Column(AreaDefinition.Editor("b"), AreaDefinition.Editor("c"))), out _);
        var a = AreaOf(layout, "a");
        var c = AreaOf(layout, "c");
        DragCorner(a.Corners[0], Screen(c, 50, 50), ModifierKeys.Control);
        Assert.Equal("c,b,a", Ids(layout));
    }

    [Fact]
    public void Escape_CancelsACornerDrag()
    {
        var layout = Create(AreaDefinition.Editor("a"), out _);
        var area = (Area)layout.Root!;
        var corner = area.Corners[0];
        corner.OnPointerPressed(At(Screen(corner, 7, 7), PointerButtons.Left, clicks: 1));
        corner.OnPointerMoved(At(Screen(area, 400, 30)));
        Assert.Equal(AreaPreviewKind.Split, layout.PreviewKind);

        layout.OnPreviewKeyDown(new KeyEventArgs(Key.Escape, 0, ModifierKeys.None, true));
        Assert.False(layout.IsInteracting);
        Assert.False(corner.IsPointerCaptured);
        corner.OnPointerReleased(At(Screen(area, 400, 30), PointerButtons.Left));
        Assert.Same(area, layout.Root);
    }

    #endregion

    #region Interactive split and join

    [Fact]
    public void InteractiveSplit_FollowsThePointer_AndSplitsOnClick()
    {
        var layout = Create(AreaDefinition.Row(AreaDefinition.Editor("a"), AreaDefinition.Editor("b")), out _);
        var b = AreaOf(layout, "b");

        layout.BeginInteractiveSplit(Orientation.Vertical);
        Assert.True(layout.IsInteracting);
        var point = Screen(b, 100, 250);
        layout.OnPreviewPointerMoved(At(point));
        Assert.Equal(AreaPreviewKind.Split, layout.PreviewKind);
        Assert.Same(b, layout.PreviewArea);

        var press = At(point, PointerButtons.Left, clicks: 1);
        layout.OnPreviewPointerPressed(press);
        Assert.True(press.Handled);
        Assert.False(layout.IsInteracting);
        Layout(layout);
        Assert.Equal("a,b,b", Ids(layout));
        Assert.Equal(250 - AreaLayout.DefaultSpacing / 2, b.Bounds.Height, 1.5f);
    }

    [Fact]
    public void RightClick_CancelsAnInteractiveSplit_WithoutOpeningAMenu()
    {
        var layout = Create(AreaDefinition.Editor("a"), out _);
        layout.BeginInteractiveSplit(Orientation.Horizontal);
        var point = Screen(layout.Root!, 100, 100);

        var press = At(point, PointerButtons.Right, clicks: 1);
        layout.OnPreviewPointerPressed(press);
        var release = At(point, PointerButtons.Right, clicks: 1);
        layout.OnPreviewPointerReleased(release);

        Assert.True(press.Handled && release.Handled);
        Assert.False(layout.IsInteracting);
        Assert.IsType<Area>(layout.Root);
    }

    [Fact]
    public void InteractiveJoin_KeepsTheAreaUnderThePointer()
    {
        var layout = Create(AreaDefinition.Row(AreaDefinition.Editor("a"), AreaDefinition.Editor("b")), out _);
        var a = AreaOf(layout, "a");
        var b = AreaOf(layout, "b");

        Assert.True(layout.BeginInteractiveJoin(a, b));
        layout.OnPreviewPointerMoved(At(Screen(b, 30, 30)));
        Assert.Equal(AreaPreviewKind.Join, layout.PreviewKind);
        Assert.Same(b, layout.PreviewArea);
        Assert.Equal(Dock.Left, layout.PreviewDirection);

        layout.OnPreviewPointerPressed(At(Screen(b, 30, 30), PointerButtons.Left, clicks: 1));
        Assert.Same(b, layout.Root);
    }

    [Fact]
    public void AreaMenu_SplitsMaximizesAndCloses()
    {
        var layout = Create(AreaDefinition.Row(AreaDefinition.Editor("a"), AreaDefinition.Editor("b")), out _);
        var a = AreaOf(layout, "a");
        var items = layout.CreateAreaMenuItems(a).OfType<MenuItem>().ToList();
        Assert.Equal(new object?[] { "Vertical Split", "Horizontal Split", "Maximize Area", "Close Area" }, items.Select(i => i.Header));
        Assert.Equal("Ctrl+Space", items[2].InputGestureText);

        layout.Maximize(a);
        var maximized = layout.CreateAreaMenuItems(a).OfType<MenuItem>().ToList();
        Assert.Equal("Back to Previous", maximized[2].Header);
        Assert.False(maximized[3].IsEnabled);
        layout.RestoreMaximized();

        Assert.True(layout.CanClose(a));
        Assert.True(layout.CreateAreaMenuItems(AreaOf(layout, "b")).OfType<MenuItem>().Last().IsEnabled);
    }

    #endregion

    #region Definitions

    private sealed class DefinitionComparer : IEqualityComparer<AreaDefinition>
    {
        public static readonly DefinitionComparer Instance = new();

        public bool Equals(AreaDefinition? x, AreaDefinition? y) => (x, y) switch
        {
            (EditorAreaDefinition a, EditorAreaDefinition b) => a.EditorId == b.EditorId && Math.Abs(a.Weight - b.Weight) < 1e-4,
            (SplitAreaDefinition a, SplitAreaDefinition b) => a.Orientation == b.Orientation && Math.Abs(a.Weight - b.Weight) < 1e-4 &&
                a.Children.Count == b.Children.Count && a.Children.Zip(b.Children).All(p => Equals(p.First, p.Second)),
            _ => false,
        };

        public int GetHashCode(AreaDefinition obj) => 0;
    }

    [Fact]
    public void ToDefinition_DescribesTheArrangement_AndSurvivesJson()
    {
        var definition = AreaDefinition.Row(
            AreaDefinition.Editor("a", 3),
            AreaDefinition.Column(AreaDefinition.Editor("b"), AreaDefinition.Editor("c", 2)));
        var layout = Create(definition, out var editors);
        Assert.Equal(definition, layout.ToDefinition(), DefinitionComparer.Instance);

        var workspaces = new WorkspacesDefinition([new WorkspaceDefinition("Layout", layout.ToDefinition())], 0);
        string json = workspaces.ToJson();
        Assert.Contains("\"editorId\": \"a\"", json);
        Assert.Contains("\"orientation\": \"Vertical\"", json);
        var read = WorkspacesDefinition.FromJson(json);
        Assert.Equal("Layout", read.Workspaces[0].Name);
        Assert.Equal(definition, read.Workspaces[0].Root, DefinitionComparer.Instance);

        var copy = Layout(new AreaLayout(editors.Registry, read.Workspaces[0].Root));
        Assert.Equal("a,b,c", Ids(copy));
        Assert.Equal(new[] { "a", "b", "c" }, definition.GetEditorIds());
    }

    #endregion

    #region Workspaces

    private static WorkspaceView CreateView(out Editors editors)
    {
        editors = new Editors();
        var view = new WorkspaceView(editors.Registry);
        view.AddWorkspace("Layout", AreaDefinition.Row(AreaDefinition.Editor("a"), AreaDefinition.Editor("b")));
        view.AddWorkspace("Shading", AreaDefinition.Editor("c"));
        LayoutView(view);
        return view;
    }

    private static void LayoutView(WorkspaceView view)
    {
        view.Measure(new Size(Width, Height));
        view.Arrange(new Rect(0, 0, Width, Height));
    }

    [Fact]
    public void WorkspaceView_ShowsTheSelectedWorkspacesAreas()
    {
        var view = CreateView(out _);
        Assert.Equal(0, view.SelectedIndex);
        var layout = view.CurrentLayout!;
        Assert.Same(view.Workspaces[0].Layout, layout);
        Assert.Same(view, layout.Parent);
        Assert.True(layout.Bounds.Y >= 40 && layout.Bounds.Bottom == Height);

        Workspace? shown = null;
        view.SelectionChanged += (_, w) => shown = w;
        view.SelectedIndex = 1;
        Assert.Same(view.Workspaces[1], shown);
        Assert.Same(view.Workspaces[1].Layout, view.CurrentLayout);
        Assert.Null(layout.Parent);

        // Switching back finds the same areas.
        var area = layout.Areas[0];
        view.SelectedWorkspace = view.Workspaces[0];
        Assert.Same(area, view.CurrentLayout!.Areas[0]);
    }

    [Fact]
    public void CtrlPageDown_SwitchesWorkspaces_FromInsideTheAreas()
    {
        var view = CreateView(out _);
        var key = new KeyEventArgs(Key.PageDown, 0, ModifierKeys.Control, true);
        view.OnKeyDown(key);
        Assert.True(key.Handled);
        Assert.Equal(1, view.SelectedIndex);
        view.OnKeyDown(new KeyEventArgs(Key.PageDown, 0, ModifierKeys.Control, true));
        Assert.Equal(0, view.SelectedIndex);
        view.OnKeyDown(new KeyEventArgs(Key.PageUp, 0, ModifierKeys.Control, true));
        Assert.Equal(1, view.SelectedIndex);
    }

    [Fact]
    public void Duplicate_CopiesTheArrangement_WithABlenderStyleName()
    {
        var view = CreateView(out var editors);
        var original = view.Workspaces[0];
        original.Layout.Areas[1].EditorId = "d";

        var copy = view.Duplicate(original);
        Assert.Equal("Layout.001", copy.Name);
        Assert.Equal(1, view.Workspaces.IndexOf(copy));
        Assert.Same(copy, view.SelectedWorkspace);
        Assert.Equal("a,d", Ids(copy.Layout));
        Assert.NotSame(original.Layout.Areas[0].EditorContent, copy.Layout.Areas[0].EditorContent);

        Assert.Equal("Layout.002", view.Duplicate(copy).Name);
        Assert.Equal("Shading.001", view.GetUniqueName("Shading"));
        Assert.Equal("New", view.GetUniqueName("New"));
    }

    [Fact]
    public void Delete_KeepsTheLastWorkspace_AndReleasesItsEditors()
    {
        var view = CreateView(out _);
        var shading = view.Workspaces[1];
        var content = (TestEditor)shading.Layout.Areas[0].EditorContent!;

        Assert.True(view.Delete(shading));
        Assert.True(content.IsDisposed);
        Assert.Single(view.Workspaces);
        Assert.False(view.CanDelete(view.Workspaces[0]));
        Assert.False(view.Delete(view.Workspaces[0]));

        view.AddWorkspace("One");
        view.AddWorkspace("Two");
        view.DeleteOthers(view.Workspaces[2]);
        Assert.Equal(new[] { "Two" }, view.Workspaces.Select(w => w.Name));
        Assert.Same(view.Workspaces[0].Layout, view.CurrentLayout);
    }

    [Fact]
    public void Rename_TrimsAndKeepsNamesUnique()
    {
        var view = CreateView(out _);
        var shading = view.Workspaces[1];
        Assert.True(view.Rename(shading, "  Layout "));
        Assert.Equal("Layout.001", shading.Name);
        Assert.False(view.Rename(shading, "   "));
        Assert.True(view.Rename(shading, "Sculpting"));
        Assert.Equal("Sculpting", shading.Name);
    }

    [Fact]
    public void TabHeaders_RenameInPlace_WithEnterOrEscape()
    {
        using var theme = ActiveTheme.Use(MaterialTheme.CreateLight());
        var view = CreateView(out _);
        view.AttachToHost();
        LayoutView(view);
        var tab = view.TabStrip.GetTab(1)!;
        var header = FindDescendant<WorkspaceTabHeader>(tab)!;

        // A double click starts renaming.
        tab.OnPointerPressed(At(Screen(tab, 10, 10), PointerButtons.Left, clicks: 2));
        Assert.True(header.IsRenaming);
        header.RenameBox!.Text = "Animation";
        header.RenameBox.OnPreviewKeyDown(new KeyEventArgs(Key.Enter, 0, ModifierKeys.None, true));
        Assert.False(header.IsRenaming);
        Assert.Equal("Animation", view.Workspaces[1].Name);

        view.BeginRename(view.Workspaces[1]);
        header.RenameBox!.Text = "Nope";
        header.RenameBox.OnPreviewKeyDown(new KeyEventArgs(Key.Escape, 0, ModifierKeys.None, true));
        Assert.Equal("Animation", view.Workspaces[1].Name);
        view.DetachFromHost();
    }

    [Fact]
    public void TabMenu_RenamesDuplicatesDeletesAndReorders()
    {
        var view = CreateView(out _);
        view.AttachToHost();
        LayoutView(view);
        var layout = view.Workspaces[0];

        var menu = view.ShowTabMenu(layout)!;
        Assert.NotNull(menu);
        Assert.Equal(new object?[] { "Rename…", "Duplicate", null, "Delete", "Delete Other Workspaces", null, "Reorder to Front", "Reorder to Back" },
            menu.Items.Select(i => i is MenuItem item ? item.Header : null));
        Assert.False(Item(menu, "Reorder to Front").IsEnabled);
        Click(Item(menu, "Reorder to Back"));
        Assert.Equal(new[] { "Shading", "Layout" }, view.Workspaces.Select(w => w.Name));
        Assert.Same(layout, view.SelectedWorkspace);

        menu = view.ShowTabMenu(layout)!;
        Click(Item(menu, "Duplicate"));
        Assert.Equal(new[] { "Shading", "Layout", "Layout.001" }, view.Workspaces.Select(w => w.Name));
        view.DetachFromHost();
    }

    [Fact]
    public void AddButton_AddsAWorkspace_OrOffersTheTemplates()
    {
        var view = CreateView(out _);
        view.AttachToHost();
        view.TabStrip.RequestAddTab();
        Assert.Equal("Workspace", view.Workspaces[^1].Name);
        Assert.Same(view.Workspaces[^1], view.SelectedWorkspace);
        Assert.Equal("a", view.Workspaces[^1].Layout.Areas.Single().EditorId);

        view.Templates.Add(new WorkspaceDefinition("Scripting", AreaDefinition.Row(AreaDefinition.Editor("c"), AreaDefinition.Editor("d"))));
        var menu = view.ShowAddMenu()!;
        Assert.Equal(new object?[] { "Scripting", null, "Duplicate Current" }, menu.Items.Select(i => i is MenuItem item ? item.Header : null));
        Click(Item(menu, "Scripting"));
        Assert.Equal("Scripting", view.SelectedWorkspace!.Name);
        Assert.Equal("c,d", Ids(view.SelectedWorkspace.Layout));
        view.DetachFromHost();
    }

    [Fact]
    public void LayoutChanged_ReportsChangesToWorkspacesAndTheirAreas()
    {
        var view = CreateView(out _);
        int changes = 0;
        view.LayoutChanged += (_, _) => changes++;

        view.Workspaces[0].Layout.Areas[0].EditorId = "c";
        Assert.Equal(1, changes);
        view.Rename(view.Workspaces[0], "Main");
        view.AddWorkspace("More");
        Assert.Equal(3, changes);

        // A deleted workspace no longer reports.
        var more = view.Workspaces[2];
        view.Delete(more);
        changes = 0;
        more.Layout.Load(AreaDefinition.Editor("a"));
        Assert.Equal(0, changes);
    }

    [Fact]
    public void ToDefinitionAndLoad_RestoreAllWorkspaces()
    {
        var view = CreateView(out var editors);
        view.SelectedIndex = 1;
        string json = view.ToDefinition().ToJson();

        var restored = new WorkspaceView(editors.Registry);
        restored.Load(WorkspacesDefinition.FromJson(json));
        Assert.Equal(new[] { "Layout", "Shading" }, restored.Workspaces.Select(w => w.Name));
        Assert.Equal(1, restored.SelectedIndex);
        Assert.Equal("a,b", Ids(restored.Workspaces[0].Layout));

        // Loading lets go of the old workspaces: their editors are released and they no longer report changes.
        var old = restored.Workspaces[0];
        var content = (TestEditor)old.Layout.Areas[0].EditorContent!;
        int changes = 0;
        restored.LayoutChanged += (_, _) => changes++;
        restored.Load(new WorkspacesDefinition([new WorkspaceDefinition("Fresh", AreaDefinition.Editor("c"))]));
        Assert.True(content.IsDisposed);
        changes = 0;
        old.Layout.Load(AreaDefinition.Editor("a"));
        old.Layout.Areas[0].EditorId = "b";
        Assert.Equal(0, changes);
        Assert.Equal("Fresh", restored.SelectedWorkspace!.Name);
    }

    private static T? FindDescendant<T>(VisualNode node) where T : class
    {
        foreach (var child in node.Children)
        {
            if (child is T found) return found;
            if (FindDescendant<T>(child) is { } deeper) return deeper;
        }
        return null;
    }

    #endregion
}
