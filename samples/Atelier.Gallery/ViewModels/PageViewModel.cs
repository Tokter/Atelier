using Atelier.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atelier.Gallery.ViewModels
{
    public partial class PageViewModel : ObservableObject
    {
        [ObservableProperty]
        private MaterialIconKind _pageIcon = MaterialIconKind.DeviceUnknown;

        [ObservableProperty]
        private string _pageTitle = "Untitled Page";
    }
}
