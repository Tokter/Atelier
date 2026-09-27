using System;
using System.Linq;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class LayoutView : GalleryPage
{
    private readonly LayoutViewModel _vm;

    public LayoutView(LayoutViewModel viewModel)
        : base(MaterialIconKind.Dashboard, "Layout Panels",
            "Panels arrange their children: in a line, wrapping, docked to edges, in a grid or at fixed positions. Every " +
            "element also has alignment, margin, size limits, visibility, clipping and opacity.")
    {
        _vm = viewModel;

        Settings(new Button("Reset").Variant(ButtonVariant.Tonal).Command(_vm.ResetCommand));

        Sections(
            StackPanelSection(),
            WrapPanelSection(),
            DockPanelSection(),
            GridSection(),
            GridSplitterSection(),
            UniformGridSection(),
            CanvasSection(),
            BorderSection(),
            ScrollViewerSection(),
            AlignmentSection(),
            VisibilitySection(),
            CursorSection());
    }

    private UIElement StackPanelSection()
    {
        var stack = new StackPanel()
            .Bind(StackPanel.OrientationProperty, _vm, v => v.StackVertical ? Orientation.Vertical : Orientation.Horizontal)
            .Bind(StackPanel.SpacingProperty, _vm, v => v.StackSpacing);

        // The items live in the view model; the panel mirrors the collection while it is displayed.
        void Rebuild(object? sender, EventArgs e)
        {
            stack.Clear();
            for (int i = 0; i < _vm.StackItems.Count; i++)
            {
                stack.Add(Block(_vm.StackItems[i], i));
            }
        }

        void OnItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => Rebuild(sender, e);
        Rebuild(null, EventArgs.Empty);
        stack.OnAttachedToVisualTree(() => { _vm.StackItems.CollectionChanged += OnItemsChanged; Rebuild(null, EventArgs.Empty); });
        stack.OnDetachedFromVisualTree(() => _vm.StackItems.CollectionChanged -= OnItemsChanged);

        return Ui.Section("StackPanel",
            "Arranges children in a single line, horizontally or vertically, with an optional gap between them.",
            Ui.Row(
                new Switch("Vertical").BindIsChecked(_vm, v => v.StackVertical, (v, on) => v.StackVertical = on),
                Ui.SliderSetting("Spacing", _vm, v => v.StackSpacing, (v, x) => v.StackSpacing = x, 0, 32),
                new Button("Add item").Variant(ButtonVariant.Outlined).Command(_vm.AddStackItemCommand),
                new Button("Remove item").Variant(ButtonVariant.Outlined).Command(_vm.RemoveStackItemCommand)),
            Stage(stack),
            Ui.Readout(_vm, v => $"Orientation = {(v.StackVertical ? "Vertical" : "Horizontal")}, Spacing = {v.StackSpacing:0}, Children = {v.StackItems.Count}"));
    }

    private UIElement WrapPanelSection()
    {
        var words = new[] { "Layout", "Grid", "Dock", "Canvas", "Wrap", "Stack", "Border", "Scroll", "Align", "Margin", "Padding", "Visibility" };
        var wrap = new WrapPanel()
            .Bind(WrapPanel.OrientationProperty, _vm, v => v.WrapVertical ? Orientation.Vertical : Orientation.Horizontal)
            .Bind(WrapPanel.HorizontalSpacingProperty, _vm, v => v.WrapHorizontalSpacing)
            .Bind(WrapPanel.VerticalSpacingProperty, _vm, v => v.WrapVerticalSpacing)
            .Bind(WrapPanel.ItemWidthProperty, _vm, v => v.WrapUniformItems ? 96f : float.NaN)
            .Bind(WrapPanel.ItemHeightProperty, _vm, v => v.WrapUniformItems ? 40f : float.NaN)
            .Bind(UIElement.WidthProperty, _vm, v => v.WrapWidth)
            .Bind(UIElement.HeightProperty, _vm, v => v.WrapVertical ? 180f : float.NaN)
            .HorizontalAlignment(HorizontalAlignment.Left)
            .Children(words.Select((w, i) => (UIElement)Block(w, i)).ToArray());

        return Ui.Section("WrapPanel",
            "Places children in a line and continues on the next line when there is no more room. With an item width and " +
            "height, all children get the same size, like tiles.",
            Ui.Row(
                new Switch("Vertical").BindIsChecked(_vm, v => v.WrapVertical, (v, on) => v.WrapVertical = on),
                new Switch("Uniform items (96×40)").BindIsChecked(_vm, v => v.WrapUniformItems, (v, on) => v.WrapUniformItems = on)),
            Ui.Row(
                Ui.SliderSetting("Panel width", _vm, v => v.WrapWidth, (v, x) => v.WrapWidth = x, 200, 480),
                Ui.SliderSetting("Horizontal spacing", _vm, v => v.WrapHorizontalSpacing, (v, x) => v.WrapHorizontalSpacing = x, 0, 24),
                Ui.SliderSetting("Vertical spacing", _vm, v => v.WrapVerticalSpacing, (v, x) => v.WrapVerticalSpacing = x, 0, 24)),
            Stage(wrap),
            Ui.Code("new WrapPanel().Spacing(8, 8).ItemWidth(96).ItemHeight(40).Children(...)"));
    }

    private UIElement DockPanelSection()
    {
        var dock = new DockPanel()
            .Height(240)
            .Bind(DockPanel.LastChildFillProperty, _vm, v => v.LastChildFill)
            .Bind(DockPanel.HorizontalSpacingProperty, _vm, v => v.DockSpacing)
            .Bind(DockPanel.VerticalSpacingProperty, _vm, v => v.DockSpacing)
            .Children(
                Block("Top", 0).Dock(Dock.Top),
                Block("Bottom", 0).Dock(Dock.Bottom),
                Block("Left", 1).Width(110).Dock(Dock.Left),
                Block("Right", 1).Width(110).Dock(Dock.Right).BindIsVisible(_vm, v => v.ShowRightPanel),
                Block("Fill (last child)", 2));

        return Ui.Section("DockPanel",
            "Docks children to the edges in the order they are added; each takes the remaining space. The last child " +
            "fills what is left, unless LastChildFill is off.",
            Ui.Row(
                new Switch("LastChildFill").BindIsChecked(_vm, v => v.LastChildFill, (v, on) => v.LastChildFill = on),
                new Switch("Right panel").BindIsChecked(_vm, v => v.ShowRightPanel, (v, on) => v.ShowRightPanel = on),
                Ui.SliderSetting("Spacing", _vm, v => v.DockSpacing, (v, x) => v.DockSpacing = x, 0, 24)),
            Stage(dock),
            Ui.Code("new DockPanel().LastChildFill().Spacing(8).Children(\n    header.Dock(Dock.Top), footer.Dock(Dock.Bottom), nav.Dock(Dock.Left), content)"));
    }

    private UIElement GridSection()
    {
        var shell = new Grid()
            .Columns("140,*,Auto")
            .Rows("Auto,*,Auto")
            .Height(240)
            .Bind(Grid.ColumnSpacingProperty, _vm, v => v.GridColumnSpacing)
            .Bind(Grid.RowSpacingProperty, _vm, v => v.GridRowSpacing)
            .Children(
                Block("Header · Cell(0, 0, columnSpan: 3)", 0).Cell(0, 0, columnSpan: 3),
                Block("Sidebar · 140 px", 1).Cell(1, 0),
                Block("Content · *", 2).Cell(1, 1),
                Block("Inspector · Auto", 1).Cell(1, 2),
                Block("Footer · spans 3 columns", 0).Cell(2, 0, columnSpan: 3));

        var weights = new Grid()
            .Columns("*,2*,*")
            .Bind(Grid.ColumnSpacingProperty, _vm, v => v.GridColumnSpacing)
            .Children(Block("*", 0).Column(0), Block("2*", 1).Column(1), Block("*", 2).Column(2));

        return Ui.Section("Grid",
            "Arranges children in rows and columns. Sizes are Auto (fit the content), pixels, or stars that share the " +
            "remaining space by weight; children can span several cells.",
            Ui.Row(
                Ui.SliderSetting("Column spacing", _vm, v => v.GridColumnSpacing, (v, x) => v.GridColumnSpacing = x, 0, 24),
                Ui.SliderSetting("Row spacing", _vm, v => v.GridRowSpacing, (v, x) => v.GridRowSpacing = x, 0, 24)),
            Ui.Demo("Rows \"Auto,*,Auto\" and columns \"140,*,Auto\"", Stage(shell)),
            Ui.Demo("Star weights \"*,2*,*\"", Stage(weights)),
            Ui.Code("new Grid().Columns(\"140,*,Auto\").Rows(\"Auto,*,Auto\").Spacing(8, 8).Children(\n" +
                    "    header.Cell(0, 0, columnSpan: 3), sidebar.Cell(1, 0), content.Cell(1, 1), ...)"));
    }

    private UIElement GridSplitterSection()
    {
        // An editor-like layout: sidebar | document over an output panel | inspector, all resizable.
        var editor = new Grid()
            .Columns("200,Auto,*,Auto,220")
            .Rows("*,Auto,110")
            .Height(320);
        editor.ColumnDefinitions[0].MinWidth = 120;
        editor.ColumnDefinitions[2].MinWidth = 160;
        editor.ColumnDefinitions[4].MinWidth = 120;
        editor.RowDefinitions[0].MinHeight = 80;
        editor.RowDefinitions[2].MinHeight = 60;

        editor.Children(
            Block("Sidebar · 200 px", 1).Cell(0, 0, rowSpan: 3),
            Splitter().Cell(0, 1, rowSpan: 3),
            Block("Document · *", 0).Cell(0, 2),
            Splitter().Cell(1, 2),
            Block("Output · 110 px", 2).Cell(2, 2),
            Splitter().Cell(0, 3, rowSpan: 3),
            Block("Inspector · 220 px", 1).Cell(0, 4, rowSpan: 3));

        GridSplitter Splitter() =>
            new GridSplitter()
                .Bind(GridSplitter.ShowsPreviewProperty, _vm, v => v.SplitterShowsPreview)
                .OnResized(() => _vm.SplitterSizes = Describe(editor));

        return Ui.Section("GridSplitter",
            "Drag a divider to resize the columns or rows next to it; the arrow keys move a focused divider, Escape " +
            "cancels a drag and a double click restores the original sizes. Star columns keep their proportions, and a " +
            "pixel column next to a star one keeps its new width when the window resizes.",
            Ui.Row(
                new Switch("Show preview, resize on release").ShowThumbIcon()
                    .BindIsChecked(_vm, v => v.SplitterShowsPreview, (v, on) => v.SplitterShowsPreview = on),
                Ui.Readout(_vm, v => v.SplitterSizes)),
            Stage(editor),
            Ui.Code("new Grid().Columns(\"200,Auto,*\").Children(\n" +
                    "    sidebar,\n" +
                    "    new GridSplitter().Column(1),   // in an Auto column: resizes columns 0 and 2\n" +
                    "    content.Column(2))"));
    }

    private static string Describe(Grid grid)
    {
        static string Length(GridLength length) => length.IsStar ? $"{length.Value:0.##}*" : length.IsAuto ? "Auto" : $"{length.Value:0}";

        var columns = string.Join(", ", grid.ColumnDefinitions.Select(c => Length(c.Width)));
        var rows = string.Join(", ", grid.RowDefinitions.Select(r => Length(r.Height)));
        return $"Columns {columns} · Rows {rows}";
    }

    private UIElement CursorSection()
    {
        CursorType[] cursors =
        [
            CursorType.Arrow, CursorType.IBeam, CursorType.Hand, CursorType.Crosshair, CursorType.SizeWestEast,
            CursorType.SizeNorthSouth, CursorType.SizeNorthwestSoutheast, CursorType.SizeNortheastSouthwest,
            CursorType.SizeAll, CursorType.NotAllowed, CursorType.Wait, CursorType.AppStarting,
        ];

        return Ui.Section("Cursors",
            "Any element can set the mouse cursor shown over it; descendants without their own show their parent's. " +
            "Text boxes show the text cursor and splitters the resize arrows by default.",
            new WrapPanel().Spacing(8, 8).Children(
                cursors.Select((cursor, i) => (UIElement)Block(cursor.ToString(), i).Cursor(cursor).MinWidth(140)).ToArray()),
            Ui.Code("new Border().Cursor(CursorType.Hand).OnPointerPressed(...)"));
    }

    private UIElement UniformGridSection()
    {
        var calendar = new UniformGrid()
            .Spacing(6)
            .Bind(UniformGrid.ColumnsProperty, _vm, v => (int)v.UniformColumns)
            .Bind(UniformGrid.FirstColumnProperty, _vm, v => (int)v.UniformFirstColumn)
            .Children(Enumerable.Range(1, 14).Select(i => (UIElement)Block(i.ToString(), i % 3)).ToArray());

        return Ui.Section("UniformGrid",
            "Gives every child a cell of the same size. Set the number of columns (or rows) and the other follows from the " +
            "number of children; FirstColumn leaves cells empty at the start, like the first days of a month.",
            Ui.Row(
                Ui.SliderSetting("Columns", _vm, v => v.UniformColumns, (v, x) => v.UniformColumns = x, 1, 7, step: 1),
                Ui.SliderSetting("First column", _vm, v => v.UniformFirstColumn, (v, x) => v.UniformFirstColumn = x, 0, 6, step: 1)),
            Stage(calendar));
    }

    private UIElement CanvasSection()
    {
        var moving = Block("Canvas.Left / Top", 0)
            .Bind(Canvas.LeftProperty, _vm, v => v.CanvasX)
            .Bind(Canvas.TopProperty, _vm, v => v.CanvasY);

        var canvas = new Canvas()
            .Height(200)
            .ClipToBounds()
            .Children(
                Block("Right = 12, Bottom = 12", 1).CanvasRight(12).CanvasBottom(12),
                Block("Left = 12, Bottom = 12", 2).CanvasLeft(12).CanvasBottom(12),
                moving);

        return Ui.Section("Canvas",
            "Positions children at fixed coordinates, measured from the left or right and top or bottom edge. Children " +
            "don't affect each other, which suits diagrams and free-form editors.",
            Ui.Row(
                Ui.SliderSetting("Left", _vm, v => v.CanvasX, (v, x) => v.CanvasX = x, 0, 300),
                Ui.SliderSetting("Top", _vm, v => v.CanvasY, (v, x) => v.CanvasY = x, 0, 150)),
            Stage(canvas));
    }

    private UIElement BorderSection()
    {
        var border = new Border()
            .HorizontalAlignment(HorizontalAlignment.Left)
            .Themed(Border.BackgroundProperty, c => c.SurfaceContainerLowest)
            .Themed(Border.BorderBrushProperty, c => c.Primary)
            .BindCornerRadius(_vm, v => v.BorderCornerRadius)
            .BindPadding(_vm, v => v.BorderPadding)
            .BindElevation(_vm, v => v.BorderElevation)
            .Bind(Border.BorderThicknessProperty, _vm, v => new Thickness(v.BorderThickness))
            .Child(new StackPanel().Spacing(4).Children(
                new TextBlock("Border").TitleMedium(),
                new TextBlock("Background, outline, corners, padding and shadow").BodySmall().Muted()));

        return Ui.Section("Border",
            "Draws a background, an outline and a shadow around a single child, with rounded corners and padding.",
            Ui.Row(
                Ui.SliderSetting("Corner radius", _vm, v => v.BorderCornerRadius, (v, x) => v.BorderCornerRadius = x, 0, 40),
                Ui.SliderSetting("Thickness", _vm, v => v.BorderThickness, (v, x) => v.BorderThickness = x, 0, 8, step: 1),
                Ui.SliderSetting("Padding", _vm, v => v.BorderPadding, (v, x) => v.BorderPadding = x, 0, 40),
                Ui.SliderSetting("Elevation", _vm, v => v.BorderElevation, (v, x) => v.BorderElevation = x, 0, 12, step: 1)),
            Stage(border).Padding(32));
    }

    private UIElement ScrollViewerSection()
    {
        var tiles = new UniformGrid()
            .Columns(8)
            .Spacing(8)
            .Children(Enumerable.Range(1, 40).Select(i => (UIElement)Block($"Tile {i}", i % 3).Size(110, 64)).ToArray());

        var scroller = new ScrollViewer()
            .Height(200)
            .Content(tiles)
            .Bind(ScrollViewer.HorizontalScrollBarVisibilityProperty, _vm, v => v.HorizontalScrolling)
            .Bind(ScrollViewer.VerticalScrollBarVisibilityProperty, _vm, v => v.VerticalScrolling)
            .OnScrollChanged((_, e) => _vm.ScrollInfo =
                $"Offset {e.HorizontalOffset:0}, {e.VerticalOffset:0} · viewport {e.ViewportWidth:0}×{e.ViewportHeight:0} · extent {e.ExtentWidth:0}×{e.ExtentHeight:0}");

        return Ui.Section("ScrollViewer",
            "Shows a larger content through a viewport. Each axis can scroll with a bar shown when needed (Auto), always " +
            "(Visible), never (Hidden), or not scroll at all (Disabled). The wheel scrolls vertically, Shift+wheel " +
            "horizontally; this scroll area is nested in the page's own.",
            Ui.Columns(360,
                Ui.Labeled("Horizontal", Choice("scroll-h", v => v.HorizontalScrolling, (v, x) => v.HorizontalScrolling = x)),
                Ui.Labeled("Vertical", Choice("scroll-v", v => v.VerticalScrolling, (v, x) => v.VerticalScrolling = x))),
            Stage(scroller),
            Ui.Readout(_vm, v => v.ScrollInfo));
    }

    private UIElement AlignmentSection()
    {
        var child = Block("Aligned child", 0)
            .Bind(UIElement.HorizontalAlignmentProperty, _vm, v => v.ChildHorizontalAlignment)
            .Bind(UIElement.VerticalAlignmentProperty, _vm, v => v.ChildVerticalAlignment)
            .Bind(UIElement.MarginProperty, _vm, v => new Thickness(v.ChildMargin));

        var slot = new Border()
            .Height(160)
            .CornerRadius(8)
            .BorderThickness(1)
            .Themed(Border.BorderBrushProperty, c => c.OutlineVariant)
            .Child(child);

        return Ui.Section("Alignment, margin and size",
            "An element is placed in the slot its parent gives it: stretched to fill it (the default) or aligned to a side " +
            "or the center. The margin keeps space around it; minimum and maximum sizes limit how small or large it gets.",
            Ui.Columns(360,
                Ui.Labeled("HorizontalAlignment", Choice("align-h", v => v.ChildHorizontalAlignment, (v, x) => v.ChildHorizontalAlignment = x)),
                Ui.Labeled("VerticalAlignment", Choice("align-v", v => v.ChildVerticalAlignment, (v, x) => v.ChildVerticalAlignment = x))),
            Ui.Row(Ui.SliderSetting("Margin", _vm, v => v.ChildMargin, (v, x) => v.ChildMargin = x, 0, 40)),
            Stage(slot),
            Ui.Demo("Size limits",
                Ui.Row(
                    Block("120 × 56", 0).Size(120, 56),
                    Block("MinWidth 200", 1).MinWidth(200),
                    new Border()
                        .MaxWidth(200)
                        .Padding(12, 8)
                        .CornerRadius(8)
                        .Themed(Border.BackgroundProperty, c => c.TertiaryContainer)
                        .Child(new TextBlock("MaxWidth 200: longer text wraps instead of growing wider")
                            .TextWrapping()
                            .Themed(TextBlock.ForegroundProperty, c => c.OnTertiaryContainer)))));
    }

    private UIElement VisibilitySection()
    {
        var clipped = new Border()
            .Size(220, 90)
            .HorizontalAlignment(HorizontalAlignment.Left)
            .CornerRadius(12)
            .Bind(UIElement.ClipToBoundsProperty, _vm, v => v.ClipChildren)
            .Themed(Border.BackgroundProperty, c => c.SurfaceContainerHighest)
            .Child(Block("This child is larger than its 220×90 parent", 1).Size(300, 70).Margin(40, 40, 0, 0).HorizontalAlignment(HorizontalAlignment.Left));

        return Ui.Section("Visibility, clipping and opacity",
            "Hidden elements keep their space, collapsed ones don't. ClipToBounds cuts off children that extend beyond " +
            "the element (following rounded corners). Opacity fades an element and its children.",
            Ui.Demo("Visibility of the middle block",
                Choice("visibility", v => v.MiddleVisibility, (v, x) => v.MiddleVisibility = x),
                Stage(Ui.Row(
                    Block("First", 0),
                    Block("Middle", 1).Bind(UIElement.VisibilityProperty, _vm, v => v.MiddleVisibility),
                    Block("Last", 2)))),
            Ui.Columns(320,
                Ui.Demo("ClipToBounds",
                    new Switch("Clip children").BindIsChecked(_vm, v => v.ClipChildren, (v, on) => v.ClipChildren = on),
                    clipped.Margin(0, 0, 0, 40)),
                Ui.Demo("Opacity",
                    Ui.SliderSetting("Opacity", _vm, v => v.BlockOpacity, (v, x) => v.BlockOpacity = x, 0, 1, "0.00"),
                    Block("Faded block", 0).HorizontalAlignment(HorizontalAlignment.Left).BindOpacity(_vm, v => v.BlockOpacity))));
    }

    /// <summary>Radio buttons for every value of an enum, bound to a view model property.</summary>
    private UIElement Choice<TEnum>(string group, Func<LayoutViewModel, TEnum> getter, Action<LayoutViewModel, TEnum> setter)
        where TEnum : struct, Enum =>
        Ui.Row(Enum.GetValues<TEnum>()
            .Select(value => (UIElement)new RadioButton(value.ToString()).GroupName(group).BindIsChecked(_vm, getter, setter, value))
            .ToArray());

    /// <summary>A tinted area that shows a demo panel's bounds.</summary>
    private static Border Stage(UIElement content) =>
        new Border()
            .Padding(12)
            .CornerRadius(12)
            .Themed(Border.BackgroundProperty, c => c.SurfaceContainer)
            .Child(content);

    /// <summary>A colored block with a centered label, in one of three tones.</summary>
    private static Border Block(string text, int tone) =>
        new Border()
            .Padding(12, 8)
            .CornerRadius(8)
            .Themed(Border.BackgroundProperty, c => (tone % 3) switch { 0 => c.PrimaryContainer, 1 => c.SecondaryContainer, _ => c.TertiaryContainer })
            .Child(new TextBlock(text)
                .LabelLarge()
                .Center()
                .Themed(TextBlock.ForegroundProperty, c => (tone % 3) switch { 0 => c.OnPrimaryContainer, 1 => c.OnSecondaryContainer, _ => c.OnTertiaryContainer }));
}
