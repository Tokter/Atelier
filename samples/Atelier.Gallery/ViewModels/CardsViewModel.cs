using Atelier.Controls;
using Atelier.Core.Keybinding;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class CardsViewModel : PageViewModel
{
    [ObservableProperty]
    private int _cardClicks;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private string _hoverState = "Pointer outside";

    // Playground
    [ObservableProperty]
    private CardVariant _variant = CardVariant.Elevated;

    [ObservableProperty]
    private float _elevation = 1f;

    [ObservableProperty]
    private float _cornerRadius = 12f;

    [ObservableProperty]
    private float _padding = 16f;

    public CardsViewModel()
    {
        PageIcon = MaterialIconKind.Dashboard;
        PageTitle = "Cards";
        Keywords = "card border elevation shadow outlined filled elevated container surface";
    }

    [RelayCommand]
    [property: Command("Book", "Cards", Description = "Book the cabin")]
    private void CardClick() => CardClicks++;

    [RelayCommand]
    [property: Command("ToggleFavorite", "Cards", Label = "Favorite", Icon = MaterialIcons.Favorite, Description = "Add the cabin to your favorites, or remove it")]
    private void ToggleFavorite() => IsFavorite = !IsFavorite;

    [RelayCommand]
    [property: Command("Reset", "Cards", Icon = MaterialIcons.RestartAlt, Description = "Put the demos of this page back as they were")]
    private void Reset()
    {
        Variant = CardVariant.Elevated;
        Elevation = 1f;
        CornerRadius = 12f;
        Padding = 16f;
        CardClicks = 0;
        IsFavorite = false;
    }
}
