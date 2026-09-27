using System;
using Atelier.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class ToolTipsViewModel : PageViewModel
{
    private static readonly string[] Statuses = ["Online", "Away", "In a meeting", "Offline"];
    private int _statusIndex;

    [ObservableProperty]
    private string _status = Statuses[0];

    [ObservableProperty]
    private float _showDelayMs = 500;

    [ObservableProperty]
    private string _lastEvent = "Hover an element to see ToolTipOpened and ToolTipClosed";

    [ObservableProperty]
    private string _lastAction = "No action clicked yet";

    [ObservableProperty]
    private bool _canPublish;

    public ToolTipsViewModel()
    {
        PageIcon = MaterialIconKind.Comment;
        PageTitle = "Tooltips";
        Keywords = "tooltip tool tip hint hover rich tooltip placement delay";
    }

    /// <summary>Whether focusing an element with Tab shows its tooltip; a global setting of the tooltip service.</summary>
    public bool ShowOnKeyboardFocus
    {
        get => ToolTipService.ShowOnKeyboardFocus;
        set
        {
            if (ToolTipService.ShowOnKeyboardFocus != value)
            {
                ToolTipService.ShowOnKeyboardFocus = value;
                OnPropertyChanged();
            }
        }
    }

    [RelayCommand]
    private void NextStatus()
    {
        _statusIndex = (_statusIndex + 1) % Statuses.Length;
        Status = Statuses[_statusIndex];
    }

    [RelayCommand]
    private void RunAction(string action) => LastAction = $"{action} clicked at {DateTime.Now:HH:mm:ss}";

    [RelayCommand]
    private void Reset()
    {
        ShowDelayMs = 500;
        ShowOnKeyboardFocus = false;
        CanPublish = false;
        _statusIndex = 0;
        Status = Statuses[0];
    }
}
