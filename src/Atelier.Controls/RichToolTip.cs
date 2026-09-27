using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// The content of a Material-style rich tooltip: an optional title, supporting text and a row of actions (usually
/// text buttons). Set it as an element's tooltip to get a rich tooltip whose actions can be clicked.
/// </summary>
/// <example>
/// <code>
/// icon.ToolTip(new RichToolTip()
///     .Title("Sync paused")
///     .Text("Changes are kept on this device until you reconnect.")
///     .Actions(new Button("Retry").Variant(ButtonVariant.Text)));
/// </code>
/// </example>
public class RichToolTip : Control
{
    /// <summary>Identifies the <see cref="Title"/> property.</summary>
    public static readonly BindableProperty<string?> TitleProperty =
        BindableProperty.Register<RichToolTip, string?>(nameof(Title), null, (s, o, n) => ((RichToolTip)s).UpdateTitle(n));

    /// <summary>Identifies the <see cref="Text"/> property.</summary>
    public static readonly BindableProperty<string?> TextProperty =
        BindableProperty.Register<RichToolTip, string?>(nameof(Text), null, (s, o, n) => ((RichToolTip)s).UpdateText(n));

    private readonly TextBlock _title = new() { FontWeight = Core.Primitives.FontWeight.Medium, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    private readonly TextBlock _text = new() { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    private readonly StackPanel _actions = new() { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(-12, 4, 0, 0), Visibility = Visibility.Collapsed };

    /// <summary>Initializes an empty rich tooltip.</summary>
    public RichToolTip()
    {
        Actions.CollectionChanged += OnActionsChanged;
        AddChild(new StackPanel { Spacing = 4 }.WithChildren(_title, _text, _actions));
    }

    /// <summary>Gets or sets the heading, shown in medium weight above the text. <c>null</c> or empty hides it.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the supporting text, shown below the title. <c>null</c> or empty hides it.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Gets the actions shown in a row at the bottom, usually text buttons. The row is hidden while empty.</summary>
    public ObservableCollection<UIElement> Actions { get; } = [];

    private void UpdateTitle(string? title)
    {
        _title.Text = title ?? string.Empty;
        _title.Visibility = string.IsNullOrEmpty(title) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateText(string? text)
    {
        _text.Text = text ?? string.Empty;
        _text.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnActionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _actions.Clear();
        foreach (var action in Actions)
        {
            _actions.Add(action);
        }
        _actions.Visibility = Actions.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}

internal static class PanelBuildExtensions
{
    // Controls can't use Atelier.Markup (it depends on this assembly), so a tiny local helper.
    public static StackPanel WithChildren(this StackPanel panel, params UIElement[] children)
    {
        foreach (var child in children)
        {
            panel.Add(child);
        }
        return panel;
    }
}
