using System;
using System.Collections.Generic;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>
/// A rectangle of an <see cref="AreaLayout"/> that shows one editor, like an area of a Blender window: a header with the
/// editor button in its top-left corner (which switches the editor type), and the editor's content below.
/// </summary>
/// <remarks>
/// <para>
/// The area creates its editor's content from the layout's <see cref="AreaLayout.Editors"/> when it joins a layout.
/// Switching to another editor and back shows the same content again (with its state), as Blender keeps the data of the
/// editors an area showed; content that implements <see cref="IDisposable"/> is disposed when the area closes.
/// </para>
/// <para>
/// Each corner is an action zone (an <see cref="AreaCorner"/>, with a crosshair cursor): dragging from it into the area
/// splits the area, dragging into a neighbor joins the neighbor into this area, and dragging with Ctrl onto another area
/// swaps the two. A right-click on the header opens the area menu: split, maximize (Ctrl+Space) and close.
/// </para>
/// <para>
/// The area is focusable, so a click anywhere in it gives it the keyboard unless a control in the editor takes it.
/// </para>
/// <para>
/// The header is <see cref="HeaderHeight"/> pixels high; the area's corners are rounded by
/// <see cref="Control.CornerRadius"/> (MD3 small, 8 px, in the Material theme) and its content is clipped to them.
/// </para>
/// </remarks>
public class Area : Control
{
    /// <summary>Identifies the <see cref="EditorId"/> property.</summary>
    public static readonly BindableProperty<string?> EditorIdProperty =
        BindableProperty.Register<Area, string?>(nameof(EditorId), null, (s, o, n) => ((Area)s).OnEditorIdChanged());

    /// <summary>Identifies the <see cref="ShowHeader"/> property.</summary>
    public static readonly BindableProperty<bool> ShowHeaderProperty =
        BindableProperty.Register<Area, bool>(nameof(ShowHeader), true, (s, o, n) => ((Area)s)._header.Visibility = n ? Visibility.Visible : Visibility.Collapsed);

    /// <summary>The height of the header, in pixels.</summary>
    public const float HeaderHeight = 32f;

    /// <summary>The style key of the editor button in the header's top-left corner.</summary>
    public const string EditorButtonStyleKey = "AreaEditorButton";

    private readonly Border _frame = new() { ClipToBounds = true };
    private readonly AreaHeader _header;
    private readonly Button _editorButton;
    private readonly Icon _editorIcon = new(MaterialIconKind.Dashboard, 18) { VerticalAlignment = VerticalAlignment.Center };
    private readonly ContentControl _headerContent = new() { VerticalAlignment = VerticalAlignment.Center, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly ContentControl _content = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
    private readonly AreaCorner[] _corners;
    private readonly Dictionary<string, (UIElement Content, UIElement? Header)> _editors = new(StringComparer.Ordinal);
    private AreaLayout? _layout;

    static Area()
    {
        // A click anywhere in the area gives it the keyboard (unless a control in it takes it), so the layout's keys
        // (Ctrl+Space, Escape) work over editors without focusable content.
        IsFocusableProperty.OverrideDefaultValue<Area>(true);
    }

    /// <summary>Initializes an area without an editor; it shows one once <see cref="EditorId"/> is set and it is in a layout.</summary>
    public Area()
    {
        var buttonContent = new StackPanel { Orientation = Orientation.Horizontal };
        buttonContent.Add(_editorIcon);
        buttonContent.Add(new Icon(MaterialIconKind.ArrowDropDown, 18) { VerticalAlignment = VerticalAlignment.Center });
        _editorButton = new Button
        {
            Variant = ButtonVariant.Text,
            Content = buttonContent,
            StyleKey = EditorButtonStyleKey,
            Height = 26,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(6, 0, 2, 0),
            IsFocusable = false,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _editorButton.Click += (_, _) => ShowEditorMenu();

        _header = new AreaHeader(this, _editorButton, _headerContent);
        var menu = new ContextMenu();
        menu.Opening += (_, _) => FillAreaMenu(menu);
        ContextMenuService.SetContextMenu(_header, menu);

        var body = new DockPanel();
        DockPanel.SetDock(_header, Dock.Top);
        body.Add(_header);
        body.Add(_content);
        _frame.Child = body;
        AddChild(_frame);

        _corners =
        [
            new AreaCorner(this, AreaCornerPosition.TopLeft),
            new AreaCorner(this, AreaCornerPosition.TopRight),
            new AreaCorner(this, AreaCornerPosition.BottomLeft),
            new AreaCorner(this, AreaCornerPosition.BottomRight),
        ];
        foreach (var corner in _corners)
        {
            AddChild(corner);
        }
        UpdateEditorButton();
    }

    /// <summary>Initializes an area that shows the editor with <paramref name="editorId"/>.</summary>
    public Area(string? editorId) : this()
    {
        EditorId = editorId;
    }

    /// <summary>Gets or sets the id of the editor type the area shows (see <see cref="AreaEditorType.Id"/>).</summary>
    public string? EditorId { get => GetValue(EditorIdProperty); set => SetValue(EditorIdProperty, value); }

    /// <summary>Gets or sets whether the header (with the editor button) is shown. The default is <c>true</c>.</summary>
    public bool ShowHeader { get => GetValue(ShowHeaderProperty); set => SetValue(ShowHeaderProperty, value); }

    /// <summary>Gets the editor type the area shows, or <c>null</c> when <see cref="EditorId"/> isn't registered.</summary>
    public AreaEditorType? EditorType => _layout?.Editors.Find(EditorId);

    /// <summary>Gets the content of the shown editor, or <c>null</c>.</summary>
    public UIElement? EditorContent { get; private set; }

    /// <summary>Gets the header content of the shown editor (see <see cref="AreaEditorType.CreateHeader"/>), or <c>null</c>.</summary>
    public UIElement? EditorHeaderContent { get; private set; }

    /// <summary>Gets the layout the area belongs to, or <c>null</c>.</summary>
    public AreaLayout? Layout => _layout;

    /// <summary>Gets the split the area is in, or <c>null</c> for the only area of a layout.</summary>
    public AreaSplit? ParentSplit => Parent as AreaSplit;

    /// <summary>Gets whether the area fills its layout (see <see cref="AreaLayout.Maximize"/>).</summary>
    public bool IsMaximized => _layout?.MaximizedArea == this;

    /// <summary>Gets the header: the editor button and the editor's header content.</summary>
    public AreaHeader Header => _header;

    /// <summary>Gets the editor button in the header's top-left corner.</summary>
    public Button EditorButton => _editorButton;

    /// <summary>Gets the corner action zones: top left, top right, bottom left, bottom right.</summary>
    public IReadOnlyList<AreaCorner> Corners => _corners;

    /// <summary>Gets the bounds of the header in the area's coordinates; empty while it is hidden.</summary>
    public Rect HeaderBounds => ShowHeader ? new Rect(0, 0, Bounds.Width, Math.Min(HeaderHeight, Bounds.Height)) : Rect.Zero;

    /// <summary>Occurs after the area switched to another editor type.</summary>
    public event EventHandler? EditorChanged;

    /// <inheritdoc/>
    protected override void OnPropertyValueChanged<T>(BindableProperty<T> property, T oldValue, T newValue)
    {
        base.OnPropertyValueChanged(property, oldValue, newValue);
        if (ReferenceEquals(property, CornerRadiusProperty))
        {
            _frame.CornerRadius = CornerRadius;
        }
    }

    internal void SetLayout(AreaLayout? layout)
    {
        if (_layout == layout) return;
        _layout = layout;
        ShowEditor();
    }

    private void OnEditorIdChanged()
    {
        ShowEditor();
        EditorChanged?.Invoke(this, EventArgs.Empty);
        _layout?.OnAreaChanged();
    }

    // Shows the editor of EditorId: the content made for it before, or new content from the layout's registry.
    private void ShowEditor()
    {
        UIElement? content = null, header = null;
        var id = EditorId;
        if (_layout != null && id != null)
        {
            if (_editors.TryGetValue(id, out var shown))
            {
                (content, header) = shown;
            }
            else if (_layout.Editors.Find(id) is { } type)
            {
                content = type.CreateContent(this);
                EditorContent = content; // the header factory may use it
                header = type.CreateHeader?.Invoke(this);
                _editors[id] = (content, header);
            }
            else
            {
                content = new TextBlock
                {
                    Text = $"Unknown editor “{id}”",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                };
            }
        }

        EditorContent = content;
        EditorHeaderContent = header;
        _content.Content = content;
        _headerContent.Content = header;
        UpdateEditorButton();
    }

    private void UpdateEditorButton()
    {
        var type = EditorType;
        _editorIcon.Kind = type?.Icon ?? MaterialIconKind.Dashboard;
        ToolTipService.SetToolTip(_editorButton, type?.Title ?? "Editor type");
    }

    // Disposes the content of the editors the area showed; called when the area leaves its layout for good.
    internal void ReleaseEditors()
    {
        foreach (var (content, header) in _editors.Values)
        {
            (content as IDisposable)?.Dispose();
            (header as IDisposable)?.Dispose();
        }
        _editors.Clear();
        _content.Content = null;
        _headerContent.Content = null;
        EditorContent = null;
        EditorHeaderContent = null;
    }

    /// <summary>
    /// Opens the editor menu under the editor button, listing the layout's editor types by category; picking one switches
    /// the area to it. Returns the menu, or <c>null</c> when it couldn't open.
    /// </summary>
    public ContextMenu? ShowEditorMenu()
    {
        var menu = CreateEditorMenu();
        if (!menu.Open(_editorButton)) return null;
        menu.Placement = PlacementMode.Bottom;
        return menu;
    }

    /// <summary>Creates the editor menu (see <see cref="ShowEditorMenu"/>). Override it to change the menu.</summary>
    public virtual ContextMenu CreateEditorMenu()
    {
        var menu = new ContextMenu();
        if (_layout == null) return menu;
        foreach (var (_, editors) in _layout.Editors.GetGroups())
        {
            if (menu.Items.Count > 0) menu.Items.Add(new Separator());
            foreach (var editor in editors)
            {
                var item = new MenuItem(editor.Title)
                {
                    Icon = editor.Icon,
                    IsCheckable = true,
                    GroupName = "AreaEditor",
                    IsChecked = editor.Id == EditorId,
                };
                if (editor.Description != null) ToolTipService.SetToolTip(item, editor.Description);
                item.Click += (_, _) => EditorId = editor.Id;
                menu.Items.Add(item);
            }
        }
        return menu;
    }

    // The area menu of the header: split, maximize and close.
    private void FillAreaMenu(ContextMenu menu)
    {
        menu.Items.Clear();
        if (_layout is not { } layout) return;
        foreach (var item in layout.CreateAreaMenuItems(this))
        {
            menu.Items.Add(item);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        _frame.Measure(availableSize);
        foreach (var corner in _corners)
        {
            corner.Measure(new Size(AreaCorner.Size, AreaCorner.Size));
        }
        return _frame.DesiredSize;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _frame.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
        float size = AreaCorner.Size;
        float right = Math.Max(0, finalSize.Width - size), bottom = Math.Max(0, finalSize.Height - size);
        foreach (var corner in _corners)
        {
            var position = corner.Position;
            float x = position is AreaCornerPosition.TopLeft or AreaCornerPosition.BottomLeft ? 0 : right;
            float y = position is AreaCornerPosition.TopLeft or AreaCornerPosition.TopRight ? 0 : bottom;
            corner.Arrange(new Rect(x, y, size, size));
        }
        return finalSize;
    }

    /// <inheritdoc/>
    public override string ToString() => $"Area({EditorId})";
}

/// <summary>
/// The header of an <see cref="Area"/>: the editor button in the top-left corner and the editor's own header content
/// after it. A right-click opens the area menu.
/// </summary>
public class AreaHeader : Control
{
    private const float Padding4 = 4f;
    private readonly UIElement _button;
    private readonly UIElement _content;

    internal AreaHeader(Area area, UIElement button, UIElement content)
    {
        Area = area;
        _button = button;
        _content = content;
        Height = Controls.Area.HeaderHeight;
        AddChild(button);
        AddChild(content);
    }

    /// <summary>Gets the area the header belongs to.</summary>
    public Area Area { get; }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        float height = Controls.Area.HeaderHeight;
        var inner = new Size(Math.Max(0, availableSize.Width - 2 * Padding4), height);
        _button.Measure(inner);
        _content.Measure(new Size(Math.Max(0, inner.Width - _button.DesiredSize.Width - Padding4), height));
        float width = _button.DesiredSize.Width + Padding4 + _content.DesiredSize.Width + 2 * Padding4;
        return new Size(float.IsInfinity(availableSize.Width) ? width : Math.Min(width, availableSize.Width), height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var button = _button.DesiredSize;
        float buttonHeight = Math.Min(button.Height, finalSize.Height);
        _button.Arrange(new Rect(Padding4, (finalSize.Height - buttonHeight) * 0.5f, button.Width, buttonHeight));
        float x = Padding4 + button.Width + Padding4;
        _content.Arrange(new Rect(x, 0, Math.Max(0, finalSize.Width - x - Padding4), finalSize.Height));
        return finalSize;
    }
}

/// <summary>Identifies a corner of an <see cref="Area"/>.</summary>
public enum AreaCornerPosition
{
    /// <summary>The top-left corner.</summary>
    TopLeft,

    /// <summary>The top-right corner.</summary>
    TopRight,

    /// <summary>The bottom-left corner.</summary>
    BottomLeft,

    /// <summary>The bottom-right corner.</summary>
    BottomRight,
}

/// <summary>
/// An action zone in a corner of an <see cref="Area"/>, as in Blender: dragging from it into the area splits the area
/// (the direction of the first movement decides between side by side and stacked), dragging into a neighbor joins the
/// neighbor into the area, and dragging with Ctrl onto another area swaps the two. Escape or a right-click cancels.
/// </summary>
/// <remarks>The zone is <see cref="Size"/> pixels square, shows a crosshair cursor, and a grip while hovered.</remarks>
public class AreaCorner : Control
{
    /// <summary>The width and height of the action zone, in pixels.</summary>
    public const float Size = 14f;

    static AreaCorner()
    {
        CursorProperty.OverrideDefaultValue<AreaCorner>(CursorType.Crosshair);
    }

    internal AreaCorner(Area area, AreaCornerPosition position)
    {
        Area = area;
        Position = position;
        ZIndex = 1;
    }

    /// <summary>Gets the area the corner belongs to.</summary>
    public Area Area { get; }

    /// <summary>Gets which corner of the area this is.</summary>
    public AreaCornerPosition Position { get; }

    /// <summary>Gets whether the corner is being dragged.</summary>
    public bool IsDragging { get; internal set; }

    /// <inheritdoc/>
    public override void OnPointerPressed(PointerEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || e.Button != PointerButtons.Left || Area.Layout is not { } layout) return;
        if (layout.BeginCornerDrag(this, e.ScreenPosition))
        {
            e.Handled = true;
            CapturePointer();
        }
    }

    /// <inheritdoc/>
    public override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (IsPointerCaptured && IsDragging) Area.Layout?.UpdateCornerDrag(e.ScreenPosition, e.Modifiers);
    }

    /// <inheritdoc/>
    public override void OnPointerReleased(PointerEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!IsPointerCaptured || e.Button != PointerButtons.Left) return;
        e.Handled = true;
        if (IsDragging) Area.Layout?.EndCornerDrag(e.ScreenPosition, e.Modifiers, commit: true);
        ReleasePointerCapture();
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture()
    {
        base.OnLostPointerCapture();
        if (IsDragging) Area.Layout?.CancelInteraction();
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);
}
