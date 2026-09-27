using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Gallery.Infrastructure;

/// <summary>
/// The layout every gallery page shares: a header with the page's icon, title, description and optional page-wide
/// settings, followed by the page's sections, all in one scrolling column.
/// </summary>
/// <remarks>
/// Pages derive from this class, call the constructor with their header texts, and add their content with
/// <see cref="Settings"/> and <see cref="Sections"/>, usually built with the <see cref="Ui"/> helpers.
/// </remarks>
public class GalleryPage : ContentControl
{
    private readonly StackPanel _column;
    private readonly WrapPanel _settings;

    /// <summary>Creates a page with the given header.</summary>
    /// <param name="icon">The icon shown next to the title (the same as in the navigation).</param>
    /// <param name="title">The page title.</param>
    /// <param name="description">What the page demonstrates and what can be tried on it.</param>
    public GalleryPage(MaterialIconKind icon, string title, string description)
    {
        _settings = new WrapPanel().Spacing(12, 12).IsVisible(false);

        var header = new Grid()
            .Columns(GridLength.Auto, GridLength.Star)
            .ColumnSpacing(20)
            .Children(
                Ui.IconBadge(icon, 56).VerticalAlignment(VerticalAlignment.Top),
                new StackPanel().Spacing(6).Column(1).Children(
                    new TextBlock(title).HeadlineMedium(),
                    new TextBlock(description).BodyLarge().Muted().TextWrapping(),
                    _settings.Margin(0, 10, 0, 0)));

        _column = new StackPanel().Spacing(20);

        Content = new ScrollViewer().Content(
            new StackPanel()
                .Spacing(28)
                .Margin(32, 24, 32, 32)
                .Children(header, _column));
    }

    /// <summary>
    /// Gets the panel holding the sections (not the header). Bind its <c>IsEnabled</c> to disable all demos at once:
    /// descendants inherit it.
    /// </summary>
    protected StackPanel SectionsPanel => _column;

    /// <summary>
    /// Adds page-wide settings (switches, buttons, ...) to the header, e.g. "enable all controls" or "reset".
    /// </summary>
    protected void Settings(params UIElement[] items)
    {
        for (int i = 0; i < items.Length; i++)
        {
            items[i].VerticalAlignment = VerticalAlignment.Center;
        }
        _settings.Children(items).IsVisible();
    }

    /// <summary>Adds sections (usually <see cref="Ui.Section"/> cards) below the header.</summary>
    protected void Sections(params UIElement[] sections) => _column.Children(sections);
}
