using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;

namespace Atelier.Nodes;

/// <summary>
/// The connection point of a socket on the edge of a <see cref="NodeView"/>: a circle, diamond or square in the socket
/// type's color (see <see cref="SocketType.Shape"/>), slightly larger while the pointer is over it.
/// </summary>
/// <remarks>Its area is larger than the shape, so the socket is easy to hit.</remarks>
public class SocketView : Control
{
    /// <summary>Identifies the <see cref="OutlineColor"/> property.</summary>
    public static readonly BindableProperty<Color> OutlineColorProperty =
        BindableProperty.Register<SocketView, Color>(nameof(OutlineColor), Color.FromArgb(0x99, 0, 0, 0), options: PropertyOptions.AffectsRender);

    /// <summary>The radius of the shape.</summary>
    public const float Radius = 5f;

    /// <summary>The radius of the shape while the pointer is over it.</summary>
    public const float HoverRadius = 6.5f;

    static SocketView()
    {
        NodeEditorTheme.Register();
    }

    /// <summary>Initializes the view of <paramref name="socket"/>.</summary>
    public SocketView(SocketViewModel socket)
    {
        ArgumentNullException.ThrowIfNull(socket);
        Socket = socket;
    }

    /// <summary>Gets the socket shown.</summary>
    public SocketViewModel Socket { get; }

    /// <summary>Gets or sets the color of the thin outline around the shape.</summary>
    public Color OutlineColor { get => GetValue(OutlineColorProperty); set => SetValue(OutlineColorProperty, value); }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize) => new(NodeView.SocketOverhang * 2, NodeView.SocketOverhang * 2);
}
