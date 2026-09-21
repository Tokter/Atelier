using System;
using Atelier.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class CardsViewModel : PageViewModel
{
    // Interactive card state
    [ObservableProperty]
    private int _interactiveCardClickCount = 0;

    [ObservableProperty]
    private bool _favoriteSelected = false;

    [ObservableProperty]
    private bool _notificationsCardEnabled = true;

    // Live Card Playground properties
    [ObservableProperty]
    private CardVariant _playgroundVariant = CardVariant.Elevated;

    [ObservableProperty]
    private float _playgroundElevation = 2f;

    [ObservableProperty]
    private float _playgroundCornerRadius = 16f;

    [ObservableProperty]
    private float _playgroundPadding = 20f;

    public CardsViewModel()
    {
        PageIcon = MaterialIconKind.Dashboard;
        PageTitle = "Cards";
    }

    [RelayCommand]
    private void CardClick()
    {
        InteractiveCardClickCount++;
    }

    [RelayCommand]
    private void ToggleFavorite()
    {
        FavoriteSelected = !FavoriteSelected;
    }

    [RelayCommand]
    private void ResetPlayground()
    {
        PlaygroundVariant = CardVariant.Elevated;
        PlaygroundElevation = 2f;
        PlaygroundCornerRadius = 16f;
        PlaygroundPadding = 20f;
        InteractiveCardClickCount = 0;
        FavoriteSelected = false;
        NotificationsCardEnabled = true;
    }

    [RelayCommand]
    private void ClearInteractions()
    {
        InteractiveCardClickCount = 0;
        FavoriteSelected = false;
    }
}
