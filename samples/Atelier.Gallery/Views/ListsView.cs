using Atelier.Controls;
using Atelier.Gallery.Infrastructure;
using Atelier.Gallery.ViewModels;
using Atelier.Markup;

namespace Atelier.Gallery.Views;

public class ListsView : GalleryPage
{
    private readonly ListsViewModel _vm;

    public ListsView(ListsViewModel viewModel)
        : base(MaterialIconKind.ViewList, "Lists", "Coming soon.")
    {
        _vm = viewModel;
    }
}
