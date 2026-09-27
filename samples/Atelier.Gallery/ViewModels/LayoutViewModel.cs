using System.Collections.ObjectModel;
using Atelier.Controls;
using Atelier.Core.Primitives;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class LayoutViewModel : PageViewModel
{
    // StackPanel
    [ObservableProperty]
    private bool _stackVertical;

    [ObservableProperty]
    private float _stackSpacing = 12;

    public ObservableCollection<string> StackItems { get; } = ["Item 1", "Item 2", "Item 3"];

    // WrapPanel
    [ObservableProperty]
    private bool _wrapVertical;

    [ObservableProperty]
    private bool _wrapUniformItems;

    [ObservableProperty]
    private float _wrapHorizontalSpacing = 8;

    [ObservableProperty]
    private float _wrapVerticalSpacing = 8;

    [ObservableProperty]
    private float _wrapWidth = 360;

    // DockPanel
    [ObservableProperty]
    private bool _lastChildFill = true;

    [ObservableProperty]
    private bool _showRightPanel = true;

    [ObservableProperty]
    private float _dockSpacing = 8;

    // Grid
    [ObservableProperty]
    private float _gridColumnSpacing = 8;

    [ObservableProperty]
    private float _gridRowSpacing = 8;

    // GridSplitter
    [ObservableProperty]
    private bool _splitterShowsPreview;

    [ObservableProperty]
    private string _splitterSizes = "Drag a divider, or double-click it to restore the sizes";

    // UniformGrid
    [ObservableProperty]
    private float _uniformColumns = 4;

    [ObservableProperty]
    private float _uniformFirstColumn;

    // Canvas
    [ObservableProperty]
    private float _canvasX = 40;

    [ObservableProperty]
    private float _canvasY = 30;

    // Border
    [ObservableProperty]
    private float _borderCornerRadius = 16;

    [ObservableProperty]
    private float _borderThickness = 2;

    [ObservableProperty]
    private float _borderElevation = 3;

    [ObservableProperty]
    private float _borderPadding = 16;

    // ScrollViewer
    [ObservableProperty]
    private ScrollBarVisibility _horizontalScrolling = ScrollBarVisibility.Auto;

    [ObservableProperty]
    private ScrollBarVisibility _verticalScrolling = ScrollBarVisibility.Auto;

    [ObservableProperty]
    private string _scrollInfo = "Scroll the area to see ScrollChanged";

    // Alignment and sizing
    [ObservableProperty]
    private HorizontalAlignment _childHorizontalAlignment = HorizontalAlignment.Center;

    [ObservableProperty]
    private VerticalAlignment _childVerticalAlignment = VerticalAlignment.Center;

    [ObservableProperty]
    private float _childMargin = 8;

    // Visibility and rendering
    [ObservableProperty]
    private Visibility _middleVisibility = Visibility.Visible;

    [ObservableProperty]
    private bool _clipChildren = true;

    [ObservableProperty]
    private float _blockOpacity = 0.6f;

    public LayoutViewModel()
    {
        PageTitle = "Layout Panels";
        PageIcon = MaterialIconKind.Dashboard;
        Keywords = "stackpanel wrappanel dockpanel grid uniformgrid canvas border scrollviewer alignment margin visibility clip opacity layout";
    }

    [RelayCommand]
    private void AddStackItem()
    {
        if (StackItems.Count < 8)
        {
            StackItems.Add($"Item {StackItems.Count + 1}");
        }
    }

    [RelayCommand]
    private void RemoveStackItem()
    {
        if (StackItems.Count > 1)
        {
            StackItems.RemoveAt(StackItems.Count - 1);
        }
    }

    [RelayCommand]
    private void Reset()
    {
        SplitterShowsPreview = false;
        StackVertical = false;
        StackSpacing = 12;
        while (StackItems.Count > 3) StackItems.RemoveAt(StackItems.Count - 1);
        while (StackItems.Count < 3) StackItems.Add($"Item {StackItems.Count + 1}");
        WrapVertical = false;
        WrapUniformItems = false;
        WrapHorizontalSpacing = 8;
        WrapVerticalSpacing = 8;
        WrapWidth = 360;
        LastChildFill = true;
        ShowRightPanel = true;
        DockSpacing = 8;
        GridColumnSpacing = 8;
        GridRowSpacing = 8;
        UniformColumns = 4;
        UniformFirstColumn = 0;
        CanvasX = 40;
        CanvasY = 30;
        BorderCornerRadius = 16;
        BorderThickness = 2;
        BorderElevation = 3;
        BorderPadding = 16;
        HorizontalScrolling = ScrollBarVisibility.Auto;
        VerticalScrolling = ScrollBarVisibility.Auto;
        ChildHorizontalAlignment = HorizontalAlignment.Center;
        ChildVerticalAlignment = VerticalAlignment.Center;
        ChildMargin = 8;
        MiddleVisibility = Visibility.Visible;
        ClipChildren = true;
        BlockOpacity = 0.6f;
    }
}
