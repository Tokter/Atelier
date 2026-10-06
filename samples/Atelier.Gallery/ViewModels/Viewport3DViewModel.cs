using Atelier.Controls;
using Atelier.Graphics3D;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Atelier.Gallery.ViewModels;

public partial class Viewport3DViewModel : PageViewModel
{
    [ObservableProperty]
    private ViewportShading _shading = ViewportShading.Shaded;

    [ObservableProperty]
    private bool _showGrid = true;

    [ObservableProperty]
    private bool _animate = true;

    [ObservableProperty]
    private bool _showLines = true;

    [ObservableProperty]
    private float _sunAngle = 35;

    [ObservableProperty]
    private string _status = "Middle-drag to orbit, Shift+middle-drag to pan, wheel to zoom (Alt+drag orbits without a middle button)";

    public Viewport3DViewModel()
    {
        PageIcon = MaterialIconKind.ViewInAr;
        PageTitle = "Viewport 3D";
        CommandGroup = Viewport3D.CommandGroup;
        Keywords = "3d viewport opengl gpu mesh scene camera orbit render texture normal map wireframe grid gizmo";
    }
}
