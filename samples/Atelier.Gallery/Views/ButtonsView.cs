using Atelier.Controls;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class ButtonsView : GalleryPage
{
    private readonly ButtonsViewModel _vm;

    public ButtonsView(ButtonsViewModel viewModel)
        : base(MaterialIconKind.SmartButton, "Buttons", "Coming soon.")
    {
        _vm = viewModel;
    }
}
