using Atelier.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Atelier.Gallery.ViewModels;

public partial class ButtonsViewModel : PageViewModel
{
    public ButtonsViewModel()
    {
        PageIcon = MaterialIconKind.SmartButton;
        PageTitle = "Buttons";
        Keywords = "button repeatbutton togglebutton command toolbar icon button";
    }
}
