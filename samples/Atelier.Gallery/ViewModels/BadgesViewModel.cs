using System;
using Atelier.Controls;
using Atelier.Core.Keybinding;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class BadgesViewModel : PageViewModel
{
    /// <summary>The keybinding group of this page's commands.</summary>
    public const string Group = "Badges";

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
        CommandGroup = Group;
        Keywords = "badge count notification dot indicator unread";
    }

    [RelayCommand]
    [property: Command("Increment", Group, Label = "Add a message", Icon = MaterialIcons.Add, Description = "One more unread message")]
    private void Increment() => Unread = Unread < 5 ? Unread + 1 : Unread * 10;

    [RelayCommand]
    [property: Command("Decrement", Group, Label = "Read a message", Icon = MaterialIcons.Remove, Description = "One unread message less")]
    private void Decrement() => Unread = Math.Max(0, Unread > 5 ? Unread / 10 : Unread - 1);

    [RelayCommand]
    [property: Command("Reset", Group, Icon = MaterialIcons.RestartAlt, Description = "Put the demos of this page back as they were")]
    private void Reset()
    {
        Unread = 3;
        ShowZero = false;
        BadgesVisible = true;
    }
}
