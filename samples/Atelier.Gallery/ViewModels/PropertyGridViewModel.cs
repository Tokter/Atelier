using System;
using System.Collections.ObjectModel;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Gallery.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public enum ActivePropertyModel
{
    GraphicElement,
    MicroserviceConfig,
    ParticleEmitter
}

public partial class PropertyGridViewModel : PageViewModel
{
    [ObservableProperty]
    private ActivePropertyModel _activeModel = ActivePropertyModel.GraphicElement;

    [ObservableProperty]
    private PropertySortMode _sortMode = PropertySortMode.Categorized;

    [ObservableProperty]
    private bool _isToolbarVisible = true;

    [ObservableProperty]
    private float _toolbarElevation = 2.0f;

    [ObservableProperty]
    private float _labelWidth = 150.0f;

    [ObservableProperty]
    private string _lastEditedInfo = "Ready. Edit any property in the grid to see real-time inspection updates.";

    public GraphicElementModel GraphicModel { get; private set; } = new();
    public MicroserviceConfigModel MicroserviceModel { get; private set; } = new();
    public ParticleEmitterModel ParticleModel { get; private set; } = new();

    public ObservableCollection<string> ChangeLogs { get; } = new();

    public object CurrentInspectableObject => ActiveModel switch
    {
        ActivePropertyModel.GraphicElement => GraphicModel,
        ActivePropertyModel.MicroserviceConfig => MicroserviceModel,
        ActivePropertyModel.ParticleEmitter => ParticleModel,
        _ => GraphicModel
    };

    public event Action? RequestRebuild;
    public event Action? RequestExpandAll;
    public event Action? RequestCollapseAll;

    public PropertyGridViewModel()
    {
        PageTitle = "Property Grid";
        PageIcon = MaterialIconKind.Tune;
    }

    public void LogChange(string propertyName, object? oldValue, object? newValue)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string message = $"[{timestamp}] '{propertyName}': {oldValue ?? "<null>"} → {newValue ?? "<null>"}";
        LastEditedInfo = message;

        if (ChangeLogs.Count > 40)
        {
            ChangeLogs.RemoveAt(ChangeLogs.Count - 1);
        }
        ChangeLogs.Insert(0, message);
    }

    [RelayCommand]
    public void SelectGraphicModel()
    {
        ActiveModel = ActivePropertyModel.GraphicElement;
        OnPropertyChanged(nameof(CurrentInspectableObject));
        RequestRebuild?.Invoke();
    }

    [RelayCommand]
    public void SelectMicroserviceModel()
    {
        ActiveModel = ActivePropertyModel.MicroserviceConfig;
        OnPropertyChanged(nameof(CurrentInspectableObject));
        RequestRebuild?.Invoke();
    }

    [RelayCommand]
    public void SelectParticleModel()
    {
        ActiveModel = ActivePropertyModel.ParticleEmitter;
        OnPropertyChanged(nameof(CurrentInspectableObject));
        RequestRebuild?.Invoke();
    }

    [RelayCommand]
    public void ToggleSortMode()
    {
        SortMode = SortMode == PropertySortMode.Categorized
            ? PropertySortMode.Alphabetical
            : PropertySortMode.Categorized;
    }

    [RelayCommand]
    public void ExpandAll()
    {
        RequestExpandAll?.Invoke();
    }

    [RelayCommand]
    public void CollapseAll()
    {
        RequestCollapseAll?.Invoke();
    }

    [RelayCommand]
    public void ToggleToolbar()
    {
        IsToolbarVisible = !IsToolbarVisible;
    }

    [RelayCommand]
    public void CycleElevation()
    {
        ToolbarElevation = ToolbarElevation switch
        {
            0f => 2f,
            2f => 4f,
            4f => 8f,
            _ => 0f
        };
    }

    [RelayCommand]
    public void SetLabelWidth(float width)
    {
        LabelWidth = width;
    }

    [RelayCommand]
    public void ResetCurrentModel()
    {
        switch (ActiveModel)
        {
            case ActivePropertyModel.GraphicElement:
                GraphicModel = new GraphicElementModel();
                break;
            case ActivePropertyModel.MicroserviceConfig:
                MicroserviceModel = new MicroserviceConfigModel();
                break;
            case ActivePropertyModel.ParticleEmitter:
                ParticleModel = new ParticleEmitterModel();
                break;
        }

        OnPropertyChanged(nameof(CurrentInspectableObject));
        RequestRebuild?.Invoke();
        LogChange("All Properties", "Previous Values", "Default Preset Restored");
    }

    [RelayCommand]
    public void ClearLog()
    {
        ChangeLogs.Clear();
        LastEditedInfo = "Change audit log cleared.";
    }
}
