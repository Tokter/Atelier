using Atelier.Controls;
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
    private void CardClick() => CardClicks++;

    [RelayCommand]
    private void ToggleFavorite() => IsFavorite = !IsFavorite;

    [RelayCommand]
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
