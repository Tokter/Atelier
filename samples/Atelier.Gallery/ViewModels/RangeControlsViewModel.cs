using System;
using Atelier.Controls;
using Atelier.Core.Threading;
using Atelier.Core.Keybinding;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class RangeControlsViewModel : PageViewModel
{
    /// <summary>The keybinding group of this page's commands.</summary>
    public const string Group = "RangeControls";

    private readonly DispatcherTimer _downloadTimer;

    [ObservableProperty]
    private bool _controlsEnabled = true;

    // Sliders
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumeIcon))]
    private float _volume = 60;

    [ObservableProperty]
    private float _brightness = 0.7f;

    [ObservableProperty]
    private float _temperature = 21.5f;

    [ObservableProperty]
    private float _rating = 3;

    [ObservableProperty]
    private float _gain;

    [ObservableProperty]
    private float _rangeMinimum;

    [ObservableProperty]
    private float _rangeMaximum = 100;

    [ObservableProperty]
    private float _rangeValue = 40;

    [ObservableProperty]
    private string _lastValueChanged = "Drag a slider or use the arrow keys";

    // Progress
    [ObservableProperty]
    private float _progress = 45;

    [ObservableProperty]
    private float _wizardStep = 2;

    [ObservableProperty]
    private bool _isBusy = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartDownloadCommand), nameof(CancelDownloadCommand))]
    private bool _isDownloading;

    [ObservableProperty]
    private float _downloadProgress;

    [ObservableProperty]
    private string _downloadStatus = "Ready to download 250 MB";

    public MaterialIconKind VolumeIcon => Volume <= 0 ? MaterialIconKind.VolumeOff : Volume < 50 ? MaterialIconKind.VolumeDown : MaterialIconKind.VolumeUp;

    public RangeControlsViewModel()
    {
        PageIcon = MaterialIconKind.Tune;
        PageTitle = "Sliders & Progress";
        CommandGroup = Group;
        Keywords = "slider progressbar progress range value indeterminate download";

        _downloadTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(50), (_, _) => AdvanceDownload());
    }

    [RelayCommand(CanExecute = nameof(CanStartDownload))]
    [property: Command("StartDownload", Group, Label = "Download", Icon = MaterialIcons.Download, Description = "Start the simulated download")]
    private void StartDownload()
    {
        DownloadProgress = 0;
        IsDownloading = true;
        DownloadStatus = "Downloading…";
        _downloadTimer.Start();
    }

    private bool CanStartDownload() => !IsDownloading;

    [RelayCommand(CanExecute = nameof(CanCancelDownload))]
    [property: Command("CancelDownload", Group, Label = "Cancel", Icon = MaterialIcons.Cancel, Description = "Stop the download")]
    private void CancelDownload()
    {
        _downloadTimer.Stop();
        IsDownloading = false;
        DownloadStatus = $"Canceled at {DownloadProgress:0}%";
    }

    private bool CanCancelDownload() => IsDownloading;

    private void AdvanceDownload()
    {
        DownloadProgress = Math.Min(100, DownloadProgress + Random.Shared.NextSingle() * 2.5f);
        DownloadStatus = $"Downloading… {DownloadProgress * 2.5f:0} of 250 MB";
        if (DownloadProgress >= 100)
        {
            _downloadTimer.Stop();
            IsDownloading = false;
            DownloadStatus = "Download complete";
        }
    }

    [RelayCommand]
    [property: Command("Reset", Group, Icon = MaterialIcons.RestartAlt, Description = "Put the demos of this page back as they were")]
    private void Reset()
    {
        ControlsEnabled = true;
        Volume = 60;
        Brightness = 0.7f;
        Temperature = 21.5f;
        Rating = 3;
        Gain = 0;
        RangeMinimum = 0;
        RangeMaximum = 100;
        RangeValue = 40;
        Progress = 45;
        WizardStep = 2;
        IsBusy = true;
        if (IsDownloading)
        {
            CancelDownload();
        }
        DownloadProgress = 0;
        DownloadStatus = "Ready to download 250 MB";
    }
}
