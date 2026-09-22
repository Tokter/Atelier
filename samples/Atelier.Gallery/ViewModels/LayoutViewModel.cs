using System;
using Atelier.Controls;
using Atelier.Layout;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public enum LayoutTab
{
    All,
    StackPanel,
    DockPanel,
    Grid,
    WrapPanel,
    Canvas,
    Border
}

public enum GridPreset
{
    Uniform3x3,
    AppShell,
    SpanningMatrix
}

public partial class LayoutViewModel : PageViewModel
{
    [ObservableProperty]
    private LayoutTab _activeTab = LayoutTab.All;

    #region StackPanel State

    [ObservableProperty]
    private Orientation _stackOrientation = Orientation.Horizontal;

    [ObservableProperty]
    private float _stackSpacing = 12f;

    [ObservableProperty]
    private int _stackItemCount = 4;

    #endregion

    #region DockPanel State

    [ObservableProperty]
    private bool _lastChildFill = true;

    [ObservableProperty]
    private bool _showRightDock = true;

    #endregion

    #region Grid State

    [ObservableProperty]
    private GridPreset _selectedGridPreset = GridPreset.Uniform3x3;

    [ObservableProperty]
    private float _gridRowSpacing = 8f;

    [ObservableProperty]
    private float _gridColumnSpacing = 8f;

    #endregion

    #region WrapPanel State

    [ObservableProperty]
    private Orientation _wrapOrientation = Orientation.Horizontal;

    [ObservableProperty]
    private float _wrapContainerWidth = 420f;

    [ObservableProperty]
    private float _wrapSpacing = 8f;

    [ObservableProperty]
    private bool _wrapUniformItems = false;

    #endregion

    #region Canvas State

    [ObservableProperty]
    private float _canvasItemX = 60f;

    [ObservableProperty]
    private float _canvasItemY = 40f;

    #endregion

    #region Border State

    [ObservableProperty]
    private float _borderCornerRadius = 16f;

    [ObservableProperty]
    private float _borderThickness = 2f;

    [ObservableProperty]
    private float _borderElevation = 6f;

    [ObservableProperty]
    private float _borderPadding = 16f;

    #endregion

    public event Action? RequestLayoutRefresh;

    public LayoutViewModel()
    {
        PageTitle = "Layout Panels";
        PageIcon = MaterialIconKind.Dashboard;
    }

    [RelayCommand]
    public void SetTab(LayoutTab tab)
    {
        ActiveTab = tab;
        RequestLayoutRefresh?.Invoke();
    }

    [RelayCommand]
    public void ToggleStackOrientation()
    {
        StackOrientation = StackOrientation == Orientation.Horizontal
            ? Orientation.Vertical
            : Orientation.Horizontal;
        RequestLayoutRefresh?.Invoke();
    }

    [RelayCommand]
    public void AddStackItem()
    {
        if (StackItemCount < 8)
        {
            StackItemCount++;
            RequestLayoutRefresh?.Invoke();
        }
    }

    [RelayCommand]
    public void RemoveStackItem()
    {
        if (StackItemCount > 1)
        {
            StackItemCount--;
            RequestLayoutRefresh?.Invoke();
        }
    }

    [RelayCommand]
    public void ToggleLastChildFill()
    {
        LastChildFill = !LastChildFill;
        RequestLayoutRefresh?.Invoke();
    }

    [RelayCommand]
    public void ToggleRightDock()
    {
        ShowRightDock = !ShowRightDock;
        RequestLayoutRefresh?.Invoke();
    }

    [RelayCommand]
    public void SetGridPreset(GridPreset preset)
    {
        SelectedGridPreset = preset;
        RequestLayoutRefresh?.Invoke();
    }

    [RelayCommand]
    public void ToggleWrapOrientation()
    {
        WrapOrientation = WrapOrientation == Orientation.Horizontal
            ? Orientation.Vertical
            : Orientation.Horizontal;
        RequestLayoutRefresh?.Invoke();
    }

    [RelayCommand]
    public void ToggleWrapUniform()
    {
        WrapUniformItems = !WrapUniformItems;
        RequestLayoutRefresh?.Invoke();
    }

    [RelayCommand]
    public void ResetCanvasPosition()
    {
        CanvasItemX = 60f;
        CanvasItemY = 40f;
        RequestLayoutRefresh?.Invoke();
    }
}
