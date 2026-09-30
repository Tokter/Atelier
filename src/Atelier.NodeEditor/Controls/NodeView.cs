using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Nodes;

/// <summary>
/// Shows a <see cref="NodeViewModel"/>: a title bar in the node's color (with a collapse arrow, the title and the node's
/// <see cref="NodeViewModel.HeaderCommands"/> as icon buttons), then a row per output with its socket on the right
/// edge, the node's <see cref="NodeViewModel.Content"/>, and a row per input with its socket on the left edge.
/// </summary>
/// <remarks>
/// <para>
/// An unconnected input shows the control that edits its value (see <see cref="NodeEditor.InputEditorFactory"/>) in its
/// row, lined up with its socket; while it's connected, only its name is shown, like in Blender. A collapsed node only
/// shows its title bar, with its links meeting at the bar's ends.
/// </para>
/// <para>
/// The view is <see cref="SocketOverhang"/> wider than the node on both sides, so the sockets on its edges can be hit;
/// <see cref="BodyBounds"/> is the node itself. After each layout it updates its sockets'
/// <see cref="SocketViewModel.Anchor"/>s, which the links are drawn between.
/// </para>
/// </remarks>
public class NodeView : Control
{
    /// <summary>Identifies the <see cref="HeaderHeight"/> property.</summary>
    public static readonly BindableProperty<float> HeaderHeightProperty =
        BindableProperty.Register<NodeView, float>(nameof(HeaderHeight), DefaultHeaderHeight, options: PropertyOptions.AffectsMeasure, validateValue: v => float.IsFinite(v) && v > 0);

    /// <summary>Identifies the <see cref="RowHeight"/> property.</summary>
    public static readonly BindableProperty<float> RowHeightProperty =
        BindableProperty.Register<NodeView, float>(nameof(RowHeight), 26f, options: PropertyOptions.AffectsMeasure, validateValue: v => float.IsFinite(v) && v > 0);

    /// <summary>Identifies the <see cref="HeaderBackground"/> property.</summary>
    public static readonly BindableProperty<Color> HeaderBackgroundProperty =
        BindableProperty.Register<NodeView, Color>(nameof(HeaderBackground), Color.FromRgb(0x4A, 0x4A, 0x4A), (s, o, n) => ((NodeView)s).UpdateHeaderForeground(), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="BorderColor"/> property.</summary>
    public static readonly BindableProperty<Color> BorderColorProperty =
        BindableProperty.Register<NodeView, Color>(nameof(BorderColor), Color.FromArgb(0x66, 0, 0, 0), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="ShadowColor"/> property.</summary>
    public static readonly BindableProperty<Color> ShadowColorProperty =
        BindableProperty.Register<NodeView, Color>(nameof(ShadowColor), Color.Black, options: PropertyOptions.AffectsRender);

    /// <summary>How far the view reaches past the node's body on each side, for the sockets on its edges.</summary>
    public const float SocketOverhang = 10f;

    /// <summary>The default <see cref="HeaderHeight"/>.</summary>
    public const float DefaultHeaderHeight = 28f;

    private const float RowGap = 4f;
    private const float BottomPadding = 6f;
    private const float ContentInset = 8f;
    private const float HeaderButtonSize = 20f;

    private static readonly Color DarkHeaderText = Color.FromRgb(0x1C, 0x1B, 0x1F);
    private static readonly Color LightHeaderText = Color.FromRgb(0xF4, 0xF4, 0xF4);

    private readonly NodeEditor? _editor;
    private readonly Grid _header;
    private readonly Button _collapseButton;
    private readonly Icon _collapseIcon;
    private readonly TextBlock _title;
    private readonly StackPanel _commands;
    private readonly ContentControl _content;
    private readonly List<SocketRow> _outputRows = [];
    private readonly List<SocketRow> _inputRows = [];
    private bool _isSubscribed;

    static NodeView()
    {
        NodeEditorTheme.Register();
        CornerRadiusProperty.OverrideDefaultValue<NodeView>(new CornerRadius(6));
        BackgroundProperty.OverrideDefaultValue<NodeView>(Color.FromRgb(0x30, 0x30, 0x30));
        FontSizeProperty.OverrideDefaultValue<NodeView>(12f);
    }

    /// <summary>Initializes the view of <paramref name="node"/>, shown in <paramref name="editor"/> (which supplies the input controls, selection and error colors).</summary>
    public NodeView(NodeViewModel node, NodeEditor? editor = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        Node = node;
        _editor = editor;
        RenderTransformOrigin = Point.Zero;

        _collapseIcon = new Icon(MaterialIconKind.ExpandMore, 18);
        _collapseButton = CreateHeaderButton();
        _collapseButton.Content = _collapseIcon;
        _collapseButton.Click += (_, _) => Node.IsCollapsed = !Node.IsCollapsed;
        _title = new TextBlock { FontWeight = FontWeight.Medium, FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        _commands = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _header = new Grid { Margin = new Thickness(4, 0) };
        _header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        _header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        _header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        _header.Add(_collapseButton);
        Grid.SetColumn(_title, 1);
        _header.Add(_title);
        Grid.SetColumn(_commands, 2);
        _header.Add(_commands);
        AddChild(_header);
        if (IsReroute) _header.Visibility = Visibility.Collapsed;

        _content = new ContentControl();
        AddChild(_content);

        UpdateAll();
    }

    /// <summary>Gets the node shown.</summary>
    public NodeViewModel Node { get; }

    /// <summary>Gets the editor the view is in, or <c>null</c>.</summary>
    public NodeEditor? Editor => _editor;

    /// <summary>Gets or sets the height of the title bar. The default is 28.</summary>
    public float HeaderHeight { get => GetValue(HeaderHeightProperty); set => SetValue(HeaderHeightProperty, value); }

    /// <summary>Gets or sets the smallest height of a socket's row; rows with taller controls grow. The default is 26.</summary>
    public float RowHeight { get => GetValue(RowHeightProperty); set => SetValue(RowHeightProperty, value); }

    /// <summary>Gets or sets the title bar's color for nodes without a <see cref="NodeViewModel.HeaderColor"/>.</summary>
    public Color HeaderBackground { get => GetValue(HeaderBackgroundProperty); set => SetValue(HeaderBackgroundProperty, value); }

    /// <summary>Gets or sets the color of the node's outline (when it isn't selected and has no error).</summary>
    public Color BorderColor { get => GetValue(BorderColorProperty); set => SetValue(BorderColorProperty, value); }

    /// <summary>Gets or sets the color of the node's shadow.</summary>
    public Color ShadowColor { get => GetValue(ShadowColorProperty); set => SetValue(ShadowColorProperty, value); }

    /// <summary>Gets the title bar's color: the node's <see cref="NodeViewModel.HeaderColor"/> or <see cref="HeaderBackground"/>, grayed out while muted.</summary>
    public Color EffectiveHeaderColor
    {
        get
        {
            var color = Node.HeaderColor ?? HeaderBackground;
            if (!Node.IsMuted) return color;
            byte gray = (byte)Math.Round(Luminance(color) * 255);
            return Color.Lerp(color, Color.FromArgb(color.A, gray, gray, gray), 0.8f);
        }
    }

    /// <summary>Gets whether the node is a reroute point, drawn as a dot (see <see cref="RerouteNodeViewModel"/>).</summary>
    public bool IsReroute => Node is RerouteNodeViewModel;

    /// <summary>Gets the node's own area within the view, without the <see cref="SocketOverhang"/> on either side.</summary>
    public Rect BodyBounds => new(SocketOverhang, 0, Math.Max(0, Bounds.Width - 2 * SocketOverhang), Bounds.Height);

    /// <summary>Gets the view of <paramref name="socket"/>, or <c>null</c> if it isn't one of the node's.</summary>
    public SocketView? GetSocketView(SocketViewModel socket) =>
        _inputRows.Concat(_outputRows).FirstOrDefault(r => r.Socket == socket)?.SocketView;

    /// <summary>Gets the control that edits <paramref name="input"/>'s value, or <c>null</c>.</summary>
    public UIElement? GetInputEditor(InputSocketViewModel input) => _inputRows.FirstOrDefault(r => r.Socket == input)?.Editor;

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();
        if (_isSubscribed) return;
        _isSubscribed = true;
        Node.PropertyChanged += OnNodePropertyChanged;
        ((INotifyCollectionChanged)Node.Inputs).CollectionChanged += OnSocketsChanged;
        ((INotifyCollectionChanged)Node.Outputs).CollectionChanged += OnSocketsChanged;
        Node.HeaderCommands.CollectionChanged += OnHeaderCommandsChanged;
        foreach (var input in Node.Inputs) input.PropertyChanged += OnInputPropertyChanged;
        UpdateAll();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree()
    {
        base.OnDetachedFromVisualTree();
        if (!_isSubscribed) return;
        _isSubscribed = false;
        Node.PropertyChanged -= OnNodePropertyChanged;
        ((INotifyCollectionChanged)Node.Inputs).CollectionChanged -= OnSocketsChanged;
        ((INotifyCollectionChanged)Node.Outputs).CollectionChanged -= OnSocketsChanged;
        Node.HeaderCommands.CollectionChanged -= OnHeaderCommandsChanged;
        foreach (var input in Node.Inputs) input.PropertyChanged -= OnInputPropertyChanged;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        if (IsReroute) return new Size(RerouteNodeViewModel.Size + 2 * SocketOverhang, RerouteNodeViewModel.Size);
        float width = Node.Width;
        float fullWidth = width + 2 * SocketOverhang;
        float height = HeaderHeight;
        _header.Measure(new Size(width, HeaderHeight));
        if (!Node.IsCollapsed)
        {
            height += RowGap;
            foreach (var row in _outputRows) height += MeasureRow(row, fullWidth);
            if (_content.Visibility != Visibility.Collapsed)
            {
                _content.Measure(new Size(Math.Max(0, width - 2 * ContentInset), float.PositiveInfinity));
                height += _content.DesiredSize.Height + RowGap;
            }
            foreach (var row in _inputRows) height += MeasureRow(row, fullWidth);
            height += BottomPadding;
        }
        return new Size(fullWidth, height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (IsReroute)
        {
            UpdateAnchors(0, 0);
            return finalSize;
        }
        float width = Math.Max(0, finalSize.Width - 2 * SocketOverhang);
        float headerHeight = HeaderHeight;
        _header.Arrange(new Rect(SocketOverhang, 0, width, headerHeight));
        float y = headerHeight;
        if (!Node.IsCollapsed)
        {
            y += RowGap;
            foreach (var row in _outputRows) y += ArrangeRow(row, y, finalSize.Width);
            if (_content.Visibility != Visibility.Collapsed)
            {
                _content.Arrange(new Rect(SocketOverhang + ContentInset, y, Math.Max(0, width - 2 * ContentInset), _content.DesiredSize.Height));
                y += _content.DesiredSize.Height + RowGap;
            }
            foreach (var row in _inputRows) y += ArrangeRow(row, y, finalSize.Width);
        }
        UpdateAnchors(width, headerHeight);
        return finalSize;
    }

    private float MeasureRow(SocketRow row, float fullWidth)
    {
        row.MinRowHeight = RowHeight;
        row.Measure(new Size(fullWidth, float.PositiveInfinity));
        return row.DesiredSize.Height;
    }

    private static float ArrangeRow(SocketRow row, float y, float fullWidth)
    {
        row.Arrange(new Rect(0, y, fullWidth, row.DesiredSize.Height));
        return row.DesiredSize.Height;
    }

    // Anchors are in graph coordinates: the node's position plus their offset from its body's top-left corner.
    private void UpdateAnchors(float width, float headerHeight)
    {
        var position = Node.Position;
        if (Node is RerouteNodeViewModel reroute)
        {
            foreach (var socket in Node.Inputs.Cast<SocketViewModel>().Concat(Node.Outputs)) socket.Anchor = reroute.Center;
            _editor?.InvalidateLinks();
            return;
        }
        bool collapsed = Node.IsCollapsed;
        foreach (var row in _inputRows)
        {
            float y = collapsed ? headerHeight * 0.5f : row.Bounds.Y + row.Bounds.Height * 0.5f;
            row.Socket.Anchor = new Point(position.X, position.Y + y);
        }
        foreach (var row in _outputRows)
        {
            float y = collapsed ? headerHeight * 0.5f : row.Bounds.Y + row.Bounds.Height * 0.5f;
            row.Socket.Anchor = new Point(position.X + width, position.Y + y);
        }
        _editor?.InvalidateLinks();
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(NodeViewModel.Title):
                _title.Text = Node.Title;
                break;
            case nameof(NodeViewModel.HeaderColor):
            case nameof(NodeViewModel.IsMuted):
                UpdateHeaderForeground();
                InvalidateVisual();
                _editor?.InvalidateLinks();
                break;
            case nameof(NodeViewModel.Position):
                UpdateAnchors(Node.Width, HeaderHeight);
                _editor?.InvalidateNodePlacement();
                break;
            case nameof(NodeViewModel.Width):
                InvalidateMeasure();
                _editor?.InvalidateNodePlacement();
                break;
            case nameof(NodeViewModel.IsCollapsed):
                UpdateCollapsed();
                _editor?.InvalidateNodePlacement();
                break;
            case nameof(NodeViewModel.Content):
                UpdateContent();
                break;
            case nameof(NodeViewModel.IsSelected):
            case nameof(NodeViewModel.Error):
                InvalidateVisual();
                break;
        }
    }

    private void OnInputPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(InputSocketViewModel.IsConnected)) return;
        _inputRows.FirstOrDefault(r => r.Socket == sender)?.UpdateEditorVisibility();
    }

    private void OnSocketsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var input in (e.OldItems ?? Array.Empty<object>()).OfType<InputSocketViewModel>()) input.PropertyChanged -= OnInputPropertyChanged;
        foreach (var input in (e.NewItems ?? Array.Empty<object>()).OfType<InputSocketViewModel>()) input.PropertyChanged += OnInputPropertyChanged;
        RebuildRows();
    }

    private void OnHeaderCommandsChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildHeaderCommands();

    private void UpdateAll()
    {
        _title.Text = Node.Title;
        UpdateHeaderForeground();
        RebuildHeaderCommands();
        if (!_outputRows.Select(r => r.Socket).SequenceEqual(Node.Outputs) || !_inputRows.Select(r => r.Socket).SequenceEqual(Node.Inputs))
        {
            RebuildRows();
        }
        foreach (var row in _inputRows) row.UpdateEditorVisibility();
        UpdateContent();
        UpdateCollapsed();
    }

    private void RebuildRows()
    {
        foreach (var row in _outputRows.Concat(_inputRows)) RemoveChild(row);
        _outputRows.Clear();
        _inputRows.Clear();
        if (IsReroute) return;
        foreach (var output in Node.Outputs)
        {
            var row = new SocketRow(output, null);
            _outputRows.Add(row);
            AddChild(row);
        }
        var factory = _editor?.InputEditorFactory ?? InputEditors.Create;
        foreach (var input in Node.Inputs)
        {
            var row = new SocketRow(input, factory(input));
            _inputRows.Add(row);
            AddChild(row);
        }
        UpdateCollapsed();
        InvalidateMeasure();
    }

    private void RebuildHeaderCommands()
    {
        _commands.ClearChildren();
        foreach (var command in Node.HeaderCommands)
        {
            var button = CreateHeaderButton();
            button.Command = command;
            button.CommandDisplay = CommandDisplay.Icon;
            _commands.Add(button);
        }
        UpdateHeaderForeground();
    }

    private void UpdateContent()
    {
        _content.Content = Node.Content;
        _content.Visibility = Node.Content is null || Node.IsCollapsed ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateCollapsed()
    {
        bool collapsed = Node.IsCollapsed;
        _collapseIcon.Kind = collapsed ? MaterialIconKind.ChevronRight : MaterialIconKind.ExpandMore;
        var visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        foreach (var row in _outputRows.Concat(_inputRows)) row.Visibility = visibility;
        _content.Visibility = Node.Content is null || collapsed ? Visibility.Collapsed : Visibility.Visible;
        InvalidateMeasure();
    }

    // Light or dark text, whichever reads better on the title bar.
    private void UpdateHeaderForeground()
    {
        var header = EffectiveHeaderColor;
        var text = Luminance(header) > 0.45 ? DarkHeaderText : LightHeaderText;
        _title.Foreground = Node.IsMuted ? text.WithAlpha(0.6f) : text;
        _collapseButton.Foreground = text;
        foreach (var button in _commands.Children.OfType<Button>()) button.Foreground = text;
    }

    private static Button CreateHeaderButton() => new()
    {
        Variant = ButtonVariant.Text,
        Width = HeaderButtonSize,
        Height = HeaderButtonSize,
        MinWidth = 0,
        MinHeight = 0,
        Padding = Thickness.Zero,
        IsFocusable = false,
        VerticalAlignment = VerticalAlignment.Center,
    };

    // The relative luminance (0 black .. 1 white) of a color, from its sRGB components.
    private static double Luminance(Color color)
    {
        static double Channel(float c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        return 0.2126 * Channel(color.Rf) + 0.7152 * Channel(color.Gf) + 0.0722 * Channel(color.Bf);
    }
}

/// <summary>
/// A socket's row in a <see cref="NodeView"/>: the socket on the node's edge and its name, plus, for an unconnected
/// input, the control that edits its value.
/// </summary>
internal sealed class SocketRow : UIElement
{
    private const float Gap = 6f;
    private const float EdgePadding = 8f;
    private const float EditorPadding = 2f;
    private const float LabelShare = 0.45f;

    private readonly TextBlock _label;

    public SocketRow(SocketViewModel socket, UIElement? editor)
    {
        Socket = socket;
        SocketView = new SocketView(socket);
        _label = new TextBlock
        {
            Text = socket.Name,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = socket is OutputSocketViewModel ? TextAlignment.Right : TextAlignment.Left,
        };
        Editor = editor;
        AddChild(_label);
        if (editor != null) AddChild(editor);
        AddChild(SocketView); // last, so the socket is hit first
        UpdateEditorVisibility();
    }

    public SocketViewModel Socket { get; }

    public SocketView SocketView { get; }

    public UIElement? Editor { get; }

    public float MinRowHeight { get; set; } = 26f;

    private bool IsInput => Socket is InputSocketViewModel;

    private bool ShowsEditor => Editor != null && !Socket.IsConnected;

    // Text boxes and check boxes show the input's name themselves.
    private bool EditorShowsName => Editor is TextBox or ToggleButton;

    // Controls with a set width (like a knob) keep their size at the row's end; others take the room next to the name.
    private bool EditorStretches => float.IsNaN(Editor!.Width);

    public void UpdateEditorVisibility()
    {
        if (Editor != null) Editor.Visibility = ShowsEditor ? Visibility.Visible : Visibility.Collapsed;
        _label.Visibility = ShowsEditor && EditorShowsName ? Visibility.Collapsed : Visibility.Visible;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        float content = ContentWidth(availableSize.Width);
        SocketView.Measure(Size.Infinity);
        _label.Measure(new Size(content, float.PositiveInfinity));
        float height = Math.Max(MinRowHeight, _label.DesiredSize.Height);
        if (ShowsEditor)
        {
            Editor!.Measure(new Size(content, float.PositiveInfinity));
            height = Math.Max(height, Editor.DesiredSize.Height + 2 * EditorPadding);
        }
        return new Size(availableSize.Width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        float overhang = NodeView.SocketOverhang;
        float socketX = IsInput ? overhang : finalSize.Width - overhang;
        var socketSize = SocketView.DesiredSize;
        SocketView.Arrange(new Rect(socketX - socketSize.Width * 0.5f, (finalSize.Height - socketSize.Height) * 0.5f, socketSize.Width, socketSize.Height));

        float left = overhang + EdgePadding;
        float content = ContentWidth(finalSize.Width);
        float labelHeight = _label.DesiredSize.Height;
        float labelY = (finalSize.Height - labelHeight) * 0.5f;
        if (!ShowsEditor)
        {
            _label.Arrange(new Rect(left, labelY, content, labelHeight));
            return finalSize;
        }

        var editor = Editor!;
        float editorHeight = Math.Min(editor.DesiredSize.Height, finalSize.Height);
        float editorY = (finalSize.Height - editorHeight) * 0.5f;
        if (EditorShowsName)
        {
            editor.Arrange(new Rect(left, editorY, content, editorHeight));
            return finalSize;
        }

        float editorWidth = EditorStretches
            ? Math.Max(0, content - Gap - Math.Min(_label.DesiredSize.Width, content * LabelShare))
            : Math.Min(editor.DesiredSize.Width, content);
        float labelWidth = Math.Max(0, content - editorWidth - Gap);
        _label.Arrange(new Rect(left, labelY, labelWidth, labelHeight));
        editor.Arrange(new Rect(left + content - editorWidth, editorY, editorWidth, editorHeight));
        return finalSize;
    }

    private static float ContentWidth(float fullWidth) =>
        Math.Max(0, fullWidth - 2 * (NodeView.SocketOverhang + EdgePadding));
}
