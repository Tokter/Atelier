using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Theming;

namespace Atelier.DevTools;

/// <summary>A window the developer tools can open in: its content (which they wrap) and a few diagnostics.</summary>
public interface IDevToolsHost
{
    /// <summary>Gets or sets the window's root element.</summary>
    UIElement? Content { get; set; }

    /// <summary>Gets or sets whether the window draws its frame-rate overlay.</summary>
    bool ShowFpsOverlay { get; set; }

    /// <summary>Requests a new frame.</summary>
    void InvalidateRender();
}

/// <summary>
/// The F12 developer tools (Debug builds only): a panel to the right of the window's content, behind a
/// <see cref="GridSplitter"/>, with the element tree, a picker, the selected element's properties, its layout (margin,
/// border, padding, spacing, drawn over the window too) and a pixel zoom.
/// </summary>
/// <remarks>
/// The tools' commands (see <see cref="RegisterCommands"/>) are ordinary commands of a window group, so their
/// shortcuts work anywhere in a window: F12 opens and closes the tools, Ctrl+Shift+C starts picking an element (opening
/// the tools if needed). Users can change those shortcuts like any other. While picking, Escape stops picking (see
/// <see cref="HandleKey"/>). Closing puts the window's content back as it was.
/// </remarks>
public static partial class DevToolsManager
{
    private static readonly ConditionalWeakTable<IDevToolsHost, DevToolsSession> s_sessions = new();

    /// <summary>Gets the open tools of <paramref name="host"/>, or <c>null</c>.</summary>
    public static DevToolsSession? GetSession(IDevToolsHost host) => s_sessions.TryGetValue(host, out var session) ? session : null;

    /// <summary>Gets whether the tools are open in <paramref name="host"/>.</summary>
    public static bool IsOpen(IDevToolsHost host) => GetSession(host) != null;

    /// <summary>Opens the tools in <paramref name="host"/> (or returns the open ones); <c>null</c> if the window has no content.</summary>
    public static DevToolsSession? Open(IDevToolsHost host)
    {
        if (GetSession(host) is { } open) return open;
        if (host.Content is not { } content) return null;
        var session = new DevToolsSession(host, content);
        s_sessions.AddOrUpdate(host, session);
        return session;
    }

    /// <summary>Closes the tools of <paramref name="host"/>, restoring its content.</summary>
    public static void Close(IDevToolsHost host)
    {
        if (GetSession(host) is not { } session) return;
        s_sessions.Remove(host);
        session.Detach();
    }

    /// <summary>Opens or closes the tools of <paramref name="host"/>.</summary>
    public static void Toggle(IDevToolsHost host)
    {
        if (IsOpen(host)) Close(host);
        else Open(host);
    }

    /// <summary>
    /// Handles the keys of the picking mode for <paramref name="host"/>, before its content: Escape stops picking. The
    /// tools' shortcuts are commands (see <see cref="RegisterCommands"/>). Returns <c>true</c> if the key was used.
    /// </summary>
    public static bool HandleKey(IDevToolsHost host, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && GetSession(host) is { IsPicking: true } picking)
        {
            picking.IsPicking = false;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Replaces the inspected content while the tools are open (e.g. after hot reload rebuilt the view); returns
    /// <c>false</c> if they aren't open, so the caller sets the window's content itself.
    /// </summary>
    public static bool TryReplaceContent(IDevToolsHost host, UIElement? content)
    {
        if (GetSession(host) is not { } session) return false;
        if (content == null)
        {
            Close(host);
            host.Content = null;
            return true;
        }
        session.ReplaceInspectedRoot(content);
        return true;
    }
}

/// <summary>The developer tools open in one window: the wrapped content, the selection and the modes.</summary>
public sealed class DevToolsSession
{
    /// <summary>The width of the tools panel when it opens.</summary>
    public const float DefaultPanelWidth = 460;

    private readonly Grid _layout = new();
    private UIElement? _selected;
    private UIElement? _lastFocused;
    private bool _isPicking;
    private bool _showBoxModel = true;
    private bool _showAllBounds;

    internal DevToolsSession(IDevToolsHost host, UIElement content)
    {
        Host = host;
        InspectedRoot = content;
        DevToolsRenderers.EnsureRegistered();

        Overlay = new DevToolsOverlay(this);
        Panel = new DevToolsPanel(this);

        _layout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        _layout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        _layout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Pixels(DefaultPanelWidth)) { MinWidth = 280 });
        var splitter = new GridSplitter();
        Grid.SetColumn(splitter, 1);
        Grid.SetColumn(Panel, 2);

        // The content leaves the window, joins the layout, and the layout becomes the window's content. Its focused
        // element gets the focus back in the new tree.
        var focused = FocusManager.GetFocusedElement(content);
        host.Content = null;
        _layout.Add(content);
        _layout.Add(Overlay);
        _layout.Add(splitter);
        _layout.Add(Panel);
        host.Content = _layout;
        FocusManager.FocusChanged += OnFocusChanged;
        focused?.Focus();
        Panel.RebuildTree();
    }

    /// <summary>Gets the window.</summary>
    public IDevToolsHost Host { get; }

    /// <summary>Gets the window's own content, which the tools inspect.</summary>
    public UIElement InspectedRoot { get; private set; }

    /// <summary>Gets the tools panel.</summary>
    public DevToolsPanel Panel { get; }

    /// <summary>Gets the layer over the content that draws the highlights and picks elements.</summary>
    public DevToolsOverlay Overlay { get; }

    /// <summary>Gets the grid that holds the content, the splitter and the panel while the tools are open.</summary>
    public Grid Layout => _layout;

    /// <summary>Gets the selected element, or <c>null</c>.</summary>
    public UIElement? SelectedElement => _selected;

    /// <summary>
    /// Gets the element of the inspected content (or an open popup) that has keyboard focus, or had it last before the
    /// focus moved into the tools (clicking their buttons takes it); <c>null</c> if none, or the focus was cleared.
    /// </summary>
    public UIElement? FocusedElement => _lastFocused is { } focused && IsInspectable(focused) ? focused : null;

    /// <summary>Occurs when <see cref="SelectedElement"/> changed.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Occurs when a mode (<see cref="IsPicking"/>, <see cref="ShowBoxModel"/>, <see cref="ShowAllBounds"/>) changed.</summary>
    public event EventHandler? ModesChanged;

    /// <summary>
    /// Gets or sets whether the pointer picks elements: moving over the window highlights the element under it, a
    /// click selects it (and stops picking). The window can't be used meanwhile.
    /// </summary>
    public bool IsPicking
    {
        get => _isPicking;
        set
        {
            if (_isPicking == value) return;
            _isPicking = value;
            if (!value) Overlay.HoveredElement = null;
            OnModesChanged();
        }
    }

    /// <summary>Gets or sets whether the selected element's margin, border, padding, content and spacing are drawn over the window.</summary>
    public bool ShowBoxModel
    {
        get => _showBoxModel;
        set
        {
            if (_showBoxModel == value) return;
            _showBoxModel = value;
            OnModesChanged();
        }
    }

    /// <summary>Gets or sets whether the bounds of every element are outlined.</summary>
    public bool ShowAllBounds
    {
        get => _showAllBounds;
        set
        {
            if (_showAllBounds == value) return;
            _showAllBounds = value;
            OnModesChanged();
        }
    }

    private void OnModesChanged()
    {
        Overlay.InvalidateVisual();
        Host.InvalidateRender();
        ModesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Selects <paramref name="element"/> (in the tree, the properties and the highlight).</summary>
    public void Select(UIElement? element)
    {
        if (element == _selected) return;
        _selected = element;
        Overlay.InvalidateVisual();
        Host.InvalidateRender();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets the element at <paramref name="point"/> (in the window's coordinates): the innermost visible element of the
    /// open popups or the content there, whether or not it takes pointer input.
    /// </summary>
    public UIElement? ElementAt(Point point)
    {
        for (int i = PopupManager.ActivePopups.Count - 1; i >= 0; i--)
        {
            var popup = PopupManager.ActivePopups[i];
            if (popup.IsOpen && popup.Child is { } child && Pick(child, point) is { } inPopup) return inPopup;
        }
        return Pick(InspectedRoot, point);
    }

    private UIElement? Pick(UIElement root, Point point)
    {
        UIElement? found = null;
        Visit(root);
        return found;

        void Visit(UIElement element)
        {
            if (element.Visibility != Visibility.Visible || element is Popup) return;
            var rect = RectOf(element);
            bool inside = rect.Contains(point);
            if (inside && rect.Width > 0 && rect.Height > 0) found = element;
            if (!inside && element.ClipToBounds) return; // clipped children can't show outside
            foreach (var child in element.Children)
            {
                if (child is UIElement ui) Visit(ui);
            }
        }
    }

    /// <summary>Gets the rectangle of <paramref name="element"/> in the window's coordinates (its layout bounds, without render transforms' scaling).</summary>
    public Rect RectOf(UIElement element)
    {
        var origin = element.PointToScreen(Point.Zero);
        var windowOrigin = _layout.PointToScreen(Point.Zero);
        return new Rect(origin.X - windowOrigin.X, origin.Y - windowOrigin.Y, element.Bounds.Width, element.Bounds.Height);
    }

    /// <summary>Gets whether <paramref name="element"/> belongs to the inspected content or an open popup (not to the tools).</summary>
    public bool IsInspectable(UIElement element)
    {
        for (VisualNode? node = element; node != null; node = node.Parent)
        {
            if (node == InspectedRoot) return true;
            if (node == Panel || node == Overlay) return false;
            if (node is Popup popup && PopupManager.ActivePopups.Contains(popup)) return true;
        }
        return false;
    }

    // Focus moving into the tools keeps the content's last focused element; clearing the focus forgets it.
    private void OnFocusChanged(UIElement? oldFocus, UIElement? newFocus)
    {
        if (newFocus == null)
        {
            if (oldFocus == _lastFocused) _lastFocused = null;
        }
        else if (IsInspectable(newFocus))
        {
            _lastFocused = newFocus;
        }
    }

    internal void ReplaceInspectedRoot(UIElement content)
    {
        int index = _layout.Children.ToList().IndexOf(InspectedRoot);
        _layout.Remove(InspectedRoot);
        InspectedRoot = content;
        Grid.SetColumn(content, 0);
        _layout.InsertChild(Math.Max(0, index), content);
        Select(null);
        Panel.RebuildTree();
    }

    // Puts the window's content back.
    internal void Detach()
    {
        var focused = FocusedElement;
        FocusManager.FocusChanged -= OnFocusChanged;
        _lastFocused = null;
        IsPicking = false;
        Panel.OnDetaching();
        Host.Content = null;
        _layout.Remove(InspectedRoot);
        Host.Content = InspectedRoot;
        focused?.Focus();
        Host.InvalidateRender();
    }
}

// Registers the renderers of the tools' own elements in the current theme (again when the theme changes).
internal static class DevToolsRenderers
{
    private static bool s_subscribed;

    public static void EnsureRegistered()
    {
        if (!s_subscribed)
        {
            s_subscribed = true;
            ThemeManager.ThemeChanged += _ => Register();
        }
        Register();
    }

    private static void Register()
    {
        if (!ThemeManager.HasTheme) return;
        var renderers = ThemeManager.Current.Renderers;
        renderers.Register(new DevToolsOverlayRenderer());
        renderers.Register(new PixelZoomViewRenderer());
    }
}
