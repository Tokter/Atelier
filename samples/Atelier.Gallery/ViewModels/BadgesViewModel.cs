using System;
using Atelier.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class BadgesViewModel : PageViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountText))]
    private int _unread = 3;

    [ObservableProperty]
    private bool _showZero;

    [ObservableProperty]
    private bool _badgesVisible = true;

    public string CountText => $"Count = {Unread}";

    public BadgesViewModel()
    {
        PageIcon = MaterialIconKind.NotificationsActive;
        PageTitle = "Badges";
        Keywords = "badge count notification dot indicator unread";
    }

    [RelayCommand]
    private void Increment() => Unread = Unread < 5 ? Unread + 1 : Unread * 10;

    [RelayCommand]
    private void Decrement() => Unread = Math.Max(0, Unread > 5 ? Unread / 10 : Unread - 1);

    [RelayCommand]
    private void Reset()
    {
        Unread = 3;
        ShowZero = false;
        BadgesVisible = true;
    }
}
