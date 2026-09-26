using System;
using System.Collections.Generic;
using System.Linq;
using Atelier.Controls;
using Atelier.Core.Inspection;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Core.Events;
using Atelier.Markup;
using Xunit;

namespace Atelier.Tests;

#region Test Models for PropertyGrid

public enum TestLogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

[Inspectable]
public partial class TestInspectableModel
{
    [InspectableProperty("Server Name", "General")]
    public string ServerName { get; set; } = "WebServer1";

    [InspectableProperty("Enabled", "General")]
    public bool IsEnabled { get; set; } = true;

    [InspectableProperty("Port Number", "Network")]
    public int Port { get; set; } = 8080;

    [InspectableProperty("Timeout Ratio", "Network")]
    public double TimeoutRatio { get; set; } = 2.5;

    [InspectableProperty("Logging Level", "Diagnostics")]
    public TestLogLevel LogLevel { get; set; } = TestLogLevel.Info;

    [InspectableProperty("Theme Color", "Appearance")]
    public Color ThemeColor { get; set; } = Color.FromRgb(255, 0, 0);

    [InspectableProperty("Hardware ID", "Diagnostics", IsReadOnly = true)]
    public string HardwareId { get; } = "HW-999";
}

[Inspectable]
public partial class CustomDataModel
{
    public DateTime Timestamp { get; set; } = new DateTime(2026, 1, 1);
}

public enum ServerLogLevel
{
    Debug,
    Information,
    Warning,
    Error
}

[Inspectable]
public partial class TestServerConfigModel
{
    [InspectableProperty("Server Host", "Network")]
    public string Host { get; set; } = "api.atelier.design";

    [InspectableProperty("Port Number", "Network")]
    public int Port { get; set; } = 443;

    [InspectableProperty("Use TLS / SSL", "Security")]
    public bool EnableSsl { get; set; } = true;

    [InspectableProperty("Timeout (ms)", "Performance")]
    public int TimeoutMs { get; set; } = 5000;

    [InspectableProperty("CPU Threshold (%)", "Performance")]
    public double CpuThreshold { get; set; } = 85.5;

    [InspectableProperty("Log Level", "Diagnostics")]
    public ServerLogLevel LogLevel { get; set; } = ServerLogLevel.Information;

    [InspectableProperty("Status Color", "Appearance")]
    public Color StatusColor { get; set; } = Color.FromHex("#1E88E5");

    [InspectableProperty("Client Version", "Diagnostics", IsReadOnly = true)]
    public string Version { get; } = "v2.5.0-AOT";
}

#endregion

public class PropertyGridTests
{
    [Fact]
    public void PropertyGrid_NullSelectedObject_RendersEmptyPlaceholder()
    {
        var grid = new PropertyGrid { SelectedObject = null };

        // Should not throw and should have children
        Assert.Null(grid.SelectedObject);
        Assert.NotNull(grid.Children);
        Assert.True(grid.Children.Count > 0);
    }

    [Fact]
    public void PropertyGrid_InspectableObject_PopulatesProperties()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };

        var descriptors = ObjectInspector.GetProperties(model);
        Assert.Equal(7, descriptors.Count);
    }

    [Fact]
    public void PropertyGrid_CategorizedMode_GroupsCategories()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid
        {
            SortMode = PropertySortMode.Categorized,
            SelectedObject = model
        };

        var descriptors = ObjectInspector.GetProperties(model);
        var categories = descriptors.Select(p => p.Category).Distinct().ToList();

        // 4 distinct categories: General, Network, Diagnostics, Appearance
        Assert.Equal(4, categories.Count);
        Assert.Contains("General", categories);
        Assert.Contains("Network", categories);
        Assert.Contains("Diagnostics", categories);
        Assert.Contains("Appearance", categories);
    }

    [Fact]
    public void PropertyGrid_AlphabeticalMode_SortsProperties()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid
        {
            SortMode = PropertySortMode.Alphabetical,
            SelectedObject = model
        };

        Assert.Equal(PropertySortMode.Alphabetical, grid.SortMode);
    }

    [Fact]
    public void PropertyGrid_FilterText_FiltersProperties()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };

        grid.FilterText = "Port";
        Assert.Equal("Port", grid.FilterText);

        grid.FilterText = "General";
        Assert.Equal("General", grid.FilterText);

        grid.FilterText = string.Empty;
        Assert.Equal(string.Empty, grid.FilterText);
    }

    [Fact]
    public void PropertyGrid_StringEditor_UpdatesModelAndRaisesEvent()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };

        PropertyValueChangedEventArgs? raisedArgs = null;
        grid.PropertyValueChanged += (s, e) => raisedArgs = e;

        var desc = ObjectInspector.GetProperty(model, nameof(TestInspectableModel.ServerName));
        Assert.NotNull(desc);

        var context = new PropertyEditorContext(grid, model, desc);
        var editor = PropertyEditorRegistry.Default.CreateEditor(context) as TextBox;
        Assert.NotNull(editor);
        Assert.Equal("WebServer1", editor.Text);

        // Edit text
        editor.Text = "ProductionServer99";

        Assert.Equal("ProductionServer99", model.ServerName);
        Assert.NotNull(raisedArgs);
        Assert.Equal(nameof(TestInspectableModel.ServerName), raisedArgs.Property.Name);
        Assert.Equal("WebServer1", raisedArgs.OldValue);
        Assert.Equal("ProductionServer99", raisedArgs.NewValue);
    }

    [Fact]
    public void PropertyGrid_BoolEditor_UpdatesModel()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };

        PropertyValueChangedEventArgs? raisedArgs = null;
        grid.PropertyValueChanged += (s, e) => raisedArgs = e;

        var desc = ObjectInspector.GetProperty(model, nameof(TestInspectableModel.IsEnabled));
        Assert.NotNull(desc);

        var context = new PropertyEditorContext(grid, model, desc);
        var editor = PropertyEditorRegistry.Default.CreateEditor(context) as CheckBox;
        Assert.NotNull(editor);
        Assert.True(editor.IsChecked);

        // Toggle checkbox
        editor.IsChecked = false;

        Assert.False(model.IsEnabled);
        Assert.NotNull(raisedArgs);
        Assert.Equal(true, raisedArgs.OldValue);
        Assert.Equal(false, raisedArgs.NewValue);
    }

    [Fact]
    public void PropertyGrid_NumericEditor_ValidatesAndUpdates()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };

        var desc = ObjectInspector.GetProperty(model, nameof(TestInspectableModel.Port));
        Assert.NotNull(desc);

        var context = new PropertyEditorContext(grid, model, desc);
        var editor = PropertyEditorRegistry.Default.CreateEditor(context) as TextBox;
        Assert.NotNull(editor);
        Assert.Equal("8080", editor.Text);

        // Update with valid integer
        editor.Text = "9090";
        Assert.Equal(8080, model.Port); // numeric editors commit on Enter / LostFocus, not per keystroke
        editor.OnKeyDown(new KeyEventArgs(Key.Enter));
        Assert.Equal(9090, model.Port);

        // Invalid text should not corrupt model
        editor.Text = "invalid_number";
        editor.OnLostFocus(); // triggers commit revert
        Assert.Equal("9090", editor.Text);
        Assert.Equal(9090, model.Port);
    }

    [Fact]
    public void PropertyGrid_FloatingPointEditor_UpdatesDouble()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };

        var desc = ObjectInspector.GetProperty(model, nameof(TestInspectableModel.TimeoutRatio));
        Assert.NotNull(desc);

        var context = new PropertyEditorContext(grid, model, desc);
        var editor = PropertyEditorRegistry.Default.CreateEditor(context) as TextBox;
        Assert.NotNull(editor);

        editor.Text = "4.75";
        editor.OnLostFocus();
        Assert.Equal(4.75, model.TimeoutRatio, 2);
    }

    [Fact]
    public void PropertyGrid_EnumEditor_UpdatesModel()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };

        var desc = ObjectInspector.GetProperty(model, nameof(TestInspectableModel.LogLevel));
        Assert.NotNull(desc);

        var context = new PropertyEditorContext(grid, model, desc);
        var editor = PropertyEditorRegistry.Default.CreateEditor(context) as ComboBox;
        Assert.NotNull(editor);
        Assert.Equal("Info", editor.SelectedItem);

        // Change enum selection
        editor.SelectedItem = "Error";
        Assert.Equal(TestLogLevel.Error, model.LogLevel);
    }

    [Fact]
    public void PropertyGrid_ColorEditor_UpdatesModel()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };

        var desc = ObjectInspector.GetProperty(model, nameof(TestInspectableModel.ThemeColor));
        Assert.NotNull(desc);

        var context = new PropertyEditorContext(grid, model, desc);
        var container = PropertyEditorRegistry.Default.CreateEditor(context) as StackPanel;
        Assert.NotNull(container);

        var hexBox = container.Children.OfType<TextBox>().FirstOrDefault();
        Assert.NotNull(hexBox);

        // Set green color via hex
        hexBox.Text = "#00FF00";
        hexBox.OnLostFocus();
        Assert.Equal(Color.FromRgb(0, 255, 0), model.ThemeColor);
    }

    [Fact]
    public void PropertyGrid_ReadOnlyProperty_ProtectsFromEditing()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };

        var desc = ObjectInspector.GetProperty(model, nameof(TestInspectableModel.HardwareId));
        Assert.NotNull(desc);
        Assert.True(desc.IsReadOnly);

        var context = new PropertyEditorContext(grid, model, desc);
        var editor = PropertyEditorRegistry.Default.CreateEditor(context) as TextBox;
        Assert.NotNull(editor);
        Assert.True(editor.IsReadOnly);

        // Trying to update read-only property via context should be a no-op
        context.UpdateValue("MODIFIED-ID");
        Assert.Equal("HW-999", model.HardwareId);
    }

    [Fact]
    public void PropertyGrid_CustomEditor_RegisteredAndUsed()
    {
        var model = new CustomDataModel();
        var grid = new PropertyGrid { SelectedObject = model };

        // Register custom editor for DateTime
        grid.RegisterEditor<DateTime>(ctx =>
        {
            var tb = new TextBox("CUSTOM_DATE_EDITOR: " + ((DateTime?)ctx.Value)?.ToShortDateString());
            return tb;
        });

        var desc = ObjectInspector.GetProperty(model, nameof(CustomDataModel.Timestamp));
        Assert.NotNull(desc);

        var context = new PropertyEditorContext(grid, model, desc);
        var editor = grid.EditorRegistry.CreateEditor(context) as TextBox;
        Assert.NotNull(editor);
        Assert.StartsWith("CUSTOM_DATE_EDITOR", editor.Text);
    }

    [Fact]
    public void PropertyGrid_ExpandAndCollapseAll()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };

        grid.CollapseAll();
        grid.ExpandAll();
        grid.SetCategoryExpanded("Network", false);
    }

    [Fact]
    public void PropertyGrid_MarkupExtensions_ChainFluently()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid()
            .Inspect(model)
            .WithSortMode(PropertySortMode.Alphabetical)
            .WithFilter("Port")
            .WithLabelWidth(180f)
            .ShowToolbar(true);

        Assert.Same(model, grid.SelectedObject);
        Assert.Equal(PropertySortMode.Alphabetical, grid.SortMode);
        Assert.Equal("Port", grid.FilterText);
        Assert.Equal(180f, grid.LabelWidth);
        Assert.True(grid.IsToolbarVisible);
    }

    [Fact]
    public void PropertyGrid_MeasureAndArrange_CompletesSuccessfully()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };

        grid.Measure(new Size(500, 600));
        Assert.True(grid.DesiredSize.Width > 0);
        Assert.True(grid.DesiredSize.Height > 0);

        grid.Arrange(new Rect(0, 0, 500, 600));
        Assert.Equal(500, grid.Bounds.Width);
        Assert.Equal(600, grid.Bounds.Height);
    }

    [Fact]
    public void PropertyGrid_Layout_ToolbarAtTop_ScrollViewerAllocatedRemainingHeight()
    {
        var model = new TestServerConfigModel();
        var propertyGrid = new PropertyGrid()
            .Inspect(model)
            .WithSortMode(PropertySortMode.Categorized)
            .WithLabelWidth(140f);
        propertyGrid.Height = 360f;

        propertyGrid.Measure(new Size(600, 360));
        propertyGrid.Arrange(new Rect(0, 0, 600, 360));

        // Find internal visual tree components via children traversal
        var rootBorder = propertyGrid.Children[0] as Border;
        Assert.NotNull(rootBorder);
        var rootLayout = rootBorder.Child as Grid;
        Assert.NotNull(rootLayout);

        var toolbar = propertyGrid.Toolbar;
        Assert.NotNull(toolbar);
        var scrollViewer = rootLayout.Children[0] as ScrollViewer;
        Assert.NotNull(scrollViewer);

        // Toolbar must be at the top with compact height (< 60px)
        Assert.Equal(0, toolbar.Bounds.Y);
        Assert.InRange(toolbar.Bounds.Height, 35f, 60f);

        // ScrollViewer must start immediately below toolbar and take the remaining height
        Assert.Equal(toolbar.Bounds.Bottom, scrollViewer.Bounds.Y);
        Assert.InRange(scrollViewer.Bounds.Height, 280f, 325f);

        // Verify content panel inside ScrollViewer has children arranged sequentially
        var contentPanel = scrollViewer.Content as StackPanel;
        Assert.NotNull(contentPanel);
        Assert.True(contentPanel.Children.Count > 0);

        float lastY = -1;
        for (int i = 0; i < contentPanel.Children.Count; i++)
        {
            if (contentPanel.Children[i] is UIElement child && child.Visibility == Visibility.Visible)
            {
                Assert.True(child.Bounds.Y >= lastY, $"Child {i} ({child.GetType().Name}) at Y={child.Bounds.Y} is not >= lastY={lastY}");
                lastY = child.Bounds.Y;
            }
        }
    }

    [Fact]
    public void PropertyGrid_SwitchToAlphabetical_And_FilterText_UpdatesLayoutImmediately()
    {
        var model = new TestServerConfigModel();
        var propertyGrid = new PropertyGrid()
            .Inspect(model)
            .WithSortMode(PropertySortMode.Categorized)
            .WithLabelWidth(140f);
        propertyGrid.Height = 360f;

        propertyGrid.Measure(new Size(600, 360));
        propertyGrid.Arrange(new Rect(0, 0, 600, 360));

        var rootBorder = (Border)propertyGrid.Children[0];
        var rootLayout = (Grid)rootBorder.Child!;
        var scrollViewer = (ScrollViewer)rootLayout.Children[0];
        var toolbar = propertyGrid.Toolbar;
        var contentPanel = (StackPanel)scrollViewer.Content!;
        var toolbarGrid = (Grid)toolbar.Content!;
        var buttonGroup = (StackPanel)toolbarGrid.Children[0];
        var alphaBtn = (Button)buttonGroup.Children[1];
        var filterBox = (TextBox)toolbarGrid.Children[1];

        // 1. Switch to Alphabetical
        propertyGrid.SortMode = PropertySortMode.Alphabetical;
        Assert.Equal(PropertySortMode.Alphabetical, propertyGrid.SortMode);

        // Frame update
        propertyGrid.Measure(new Size(600, 360));
        propertyGrid.Arrange(new Rect(0, 0, 600, 360));

        Assert.Equal(8, contentPanel.Children.Count);
        for (int i = 0; i < contentPanel.Children.Count; i++)
        {
            var child = (UIElement)contentPanel.Children[i];
            Assert.True(child.Bounds.Height > 0, $"Alphabetical row {i} height is {child.Bounds.Height}, expected > 0");
            Assert.True(child.Bounds.Width > 0, $"Alphabetical row {i} width is {child.Bounds.Width}, expected > 0");
        }

        // 2. Type in Filter Box
        filterBox.Focus();
        filterBox.Text = "Port";

        Assert.Equal("Port", propertyGrid.FilterText);

        // Frame update
        propertyGrid.Measure(new Size(600, 360));
        propertyGrid.Arrange(new Rect(0, 0, 600, 360));

        // Should only match 1 property row: Port Number
        // Filtering hides non-matching rows instead of recreating them.
        var visibleRows = contentPanel.Children.OfType<UIElement>().Where(c => c.Visibility == Visibility.Visible).ToList();
        Assert.Single(visibleRows);
        var filteredRow = visibleRows[0];
        Assert.True(filteredRow.Bounds.Height > 0, $"Filtered row height is {filteredRow.Bounds.Height}, expected > 0");
    }

    [Fact]
    public void PropertyGrid_FilterBox_HitTestAndTyping()
    {
        var model = new TestServerConfigModel();
        var propertyGrid = new PropertyGrid()
            .Inspect(model)
            .WithSortMode(PropertySortMode.Categorized)
            .WithLabelWidth(140f);
        propertyGrid.Height = 360f;

        var card = new Border { Padding = new Thickness(16), Child = propertyGrid };
        var mainStack = new StackPanel { Orientation = Orientation.Vertical };
        mainStack.Add(card);
        var scrollViewer = new ScrollViewer { Content = mainStack };

        scrollViewer.Measure(new Size(1000, 800));
        scrollViewer.Arrange(new Rect(0, 0, 1000, 800));

        var rootBorder = (Border)propertyGrid.Children[0];
        var rootLayout = (Grid)rootBorder.Child!;
        var toolbar = propertyGrid.Toolbar;
        var toolbarGrid = (Grid)toolbar.Content!;
        var filterBox = (TextBox)toolbarGrid.Children[1];

        // Screen center of filter box
        var screenCenter = filterBox.TransformRectToScreen(new Rect(Point.Zero, filterBox.Bounds.Size)).Center;
        var hit = scrollViewer.HitTest(screenCenter);
        Assert.Same(filterBox, hit);

        // Simulate user clicking and typing "Port"
        filterBox.Focus();
        Assert.True(filterBox.IsFocused);

        filterBox.OnTextInput(new TextInputEventArgs("P"));
        filterBox.OnTextInput(new TextInputEventArgs("o"));
        filterBox.OnTextInput(new TextInputEventArgs("r"));
        filterBox.OnTextInput(new TextInputEventArgs("t"));

        Assert.Equal("Port", filterBox.Text);
        Assert.Equal("Port", propertyGrid.FilterText);

        scrollViewer.Measure(new Size(1000, 800));
        scrollViewer.Arrange(new Rect(0, 0, 1000, 800));

        var scrollViewerInner = (ScrollViewer)rootLayout.Children[0];
        var contentPanel = (StackPanel)scrollViewerInner.Content!;

        // Should filter down to 1 category (Network) and 1 item (Port Number)
        // Categorized view has category header + children panel
        // Filtering hides non-matching categories instead of recreating them.
        var visible = contentPanel.Children.OfType<UIElement>().Where(c => c.Visibility == Visibility.Visible).ToList();
        Assert.Equal(2, visible.Count);
        var header = visible[0];
        var panel = visible[1];
        Assert.True(header.Bounds.Height > 0);
        Assert.True(panel.Bounds.Height > 0);
    }

    [Fact]
    public void PropertyGrid_ToggleToolbar_CollapsesToolbarAndExpandsScrollViewer()
    {
        var model = new TestInspectableModel();
        var propertyGrid = new PropertyGrid { SelectedObject = model, Width = 400, Height = 500 };

        // Initial layout with toolbar visible
        propertyGrid.Measure(new Size(400, 500));
        propertyGrid.Arrange(new Rect(0, 0, 400, 500));

        var rootBorder = (Border)propertyGrid.Children[0];
        var rootLayout = (Grid)rootBorder.Child!;
        var scrollViewer = (ScrollViewer)rootLayout.Children[0];
        var toolbar = propertyGrid.Toolbar;

        // Verify initial state: toolbar is visible, occupies top row, scrollViewer starts below it
        Assert.True(propertyGrid.IsToolbarVisible);
        Assert.True(toolbar.Bounds.Height > 30, $"Toolbar height should be > 30, was {toolbar.Bounds.Height}");
        Assert.Equal(0f, toolbar.Bounds.Y);
        float initialToolbarHeight = toolbar.Bounds.Height;
        Assert.Equal(initialToolbarHeight, scrollViewer.Bounds.Y);
        float expectedScrollViewerHeight = rootLayout.Bounds.Height - initialToolbarHeight;
        Assert.Equal(expectedScrollViewerHeight, scrollViewer.Bounds.Height);

        // Toggle toolbar to hidden
        propertyGrid.IsToolbarVisible = false;
        propertyGrid.Measure(new Size(400, 500));
        propertyGrid.Arrange(new Rect(0, 0, 400, 500));

        // Toolbar must be collapsed to 0 height
        Assert.Equal(0f, toolbar.Bounds.Height);
        // ScrollViewer must expand to occupy the entire grid height starting at Y = 0 (no empty space!)
        Assert.Equal(0f, scrollViewer.Bounds.Y);
        Assert.Equal(rootLayout.Bounds.Height, scrollViewer.Bounds.Height);

        // Toggle toolbar back to visible
        propertyGrid.IsToolbarVisible = true;
        propertyGrid.Measure(new Size(400, 500));
        propertyGrid.Arrange(new Rect(0, 0, 400, 500));

        // Toolbar must be restored at the top, scrollViewer positioned below it
        Assert.Equal(initialToolbarHeight, toolbar.Bounds.Height);
        Assert.Equal(0f, toolbar.Bounds.Y);
        Assert.Equal(initialToolbarHeight, scrollViewer.Bounds.Y);
        Assert.Equal(expectedScrollViewerHeight, scrollViewer.Bounds.Height);
    }

    [Fact]
    public void Grid_CollapsedChild_ZeroDesiredSizeAndZeroRowHeight()
    {
        var grid = new Grid { Width = 300, Height = 200 };
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

        var item1 = new Border { Height = 50 };
        var item2 = new Border();

        Grid.SetRow(item1, 0);
        Grid.SetRow(item2, 1);

        grid.Add(item1);
        grid.Add(item2);

        grid.Measure(new Size(300, 200));
        grid.Arrange(new Rect(0, 0, 300, 200));

        Assert.Equal(50f, item1.Bounds.Height);
        Assert.Equal(0f, item1.Bounds.Y);
        Assert.Equal(50f, item2.Bounds.Y);
        Assert.Equal(150f, item2.Bounds.Height);

        // Now collapse item1
        item1.Visibility = Visibility.Collapsed;
        grid.InvalidateMeasure();

        grid.Measure(new Size(300, 200));
        grid.Arrange(new Rect(0, 0, 300, 200));

        Assert.Equal(0f, item1.Bounds.Height);
        Assert.Equal(0f, item2.Bounds.Y);
        Assert.Equal(200f, item2.Bounds.Height);
    }

    [Fact]
    public void Border_Elevation_SetsAndBindsProperly()
    {
        var border = new Border { Elevation = 4f };
        Assert.Equal(4f, border.Elevation);

        border.Elevation = 0f;
        Assert.Equal(0f, border.Elevation);
    }

    [Fact]
    public void Toolbar_CreationAndContentLayout_Works()
    {
        var content = new Button("Action") { Height = 32 };
        var toolbar = new Toolbar
        {
            Elevation = 3f,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = Color.FromHex("#CCCCCC"),
            Padding = new Thickness(12, 8),
            Content = content
        };

        Assert.Equal(3f, toolbar.Elevation);
        Assert.Equal(Color.FromHex("#CCCCCC"), toolbar.BorderBrush);
        Assert.Equal(new Thickness(0, 0, 0, 1), toolbar.BorderThickness);

        toolbar.Measure(new Size(400, 100));
        toolbar.Arrange(new Rect(0, 0, 400, 100));

        Assert.True(toolbar.DesiredSize.Height >= 32f + 16f + 1f); // Button height + V-padding + bottom border
    }

    [Fact]
    public void PropertyGrid_ToolbarElevation_UpdatesToolbarAndVisuals()
    {
        var model = new TestInspectableModel();
        var propertyGrid = new PropertyGrid { SelectedObject = model, ToolbarElevation = 4f };

        Assert.Equal(4f, propertyGrid.ToolbarElevation);
        Assert.Equal(4f, propertyGrid.Toolbar.Elevation);

        propertyGrid.ToolbarElevation = 0f;
        Assert.Equal(0f, propertyGrid.ToolbarElevation);
        Assert.Equal(0f, propertyGrid.Toolbar.Elevation);
    }

    [Fact]
    public void PropertyGrid_ToolbarVisualOrder_RendersAboveScrollViewer()
    {
        var model = new TestInspectableModel();
        var propertyGrid = new PropertyGrid { SelectedObject = model };

        var rootBorder = (Border)propertyGrid.Children[0];
        var rootLayout = (Grid)rootBorder.Child!;

        // ScrollViewer is child 0, Toolbar is child 1 so Toolbar renders on top (casting shadow over ScrollViewer)
        Assert.IsType<ScrollViewer>(rootLayout.Children[0]);
        Assert.IsType<Toolbar>(rootLayout.Children[1]);
        Assert.Same(propertyGrid.Toolbar, rootLayout.Children[1]);
    }
}
