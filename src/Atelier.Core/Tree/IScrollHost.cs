using Atelier.Core.Primitives;

namespace Atelier.Core.Tree;

/// <summary>
/// Implemented by elements that scroll their content, such as a scroll viewer, so that
/// <see cref="UIElement.BringIntoView()"/> can make a descendant visible.
/// </summary>
public interface IScrollHost
{
    /// <summary>
    /// Scrolls, if needed, so that <paramref name="rect"/> of <paramref name="element"/> (a descendant, in its own
    /// coordinates) is visible. A rectangle larger than the viewport is aligned to the viewport's top (left) edge.
    /// </summary>
    /// <param name="element">The descendant to show.</param>
    /// <param name="rect">The part of the element to show, in the element's coordinates.</param>
    void MakeVisible(UIElement element, Rect rect);
}
