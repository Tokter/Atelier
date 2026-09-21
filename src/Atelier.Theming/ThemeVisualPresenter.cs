using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Theming;

public class ThemeVisualPresenter : IElementVisualPresenter
{
    public static readonly ThemeVisualPresenter Instance = new();

    public void Render(UIElement element, ref DrawingContext context)
    {
        if (ThemeManager.HasTheme)
        {
            var renderer = ThemeManager.Current.Renderers.GetRenderer(element.GetType());
            renderer?.Render(element, ref context);
        }
    }

    public void RenderOverlay(UIElement element, ref DrawingContext context)
    {
        if (ThemeManager.HasTheme)
        {
            var renderer = ThemeManager.Current.Renderers.GetRenderer(element.GetType());
            renderer?.RenderOverlay(element, ref context);
        }
    }
}
