using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Rendering;

namespace Atelier.Nodes;

/// <summary>
/// A layer drawn behind a <see cref="NodeEditor"/>'s links and nodes (see <see cref="NodeEditor.BackgroundLayers"/>),
/// such as a grid. Derive from it and override <see cref="Render"/>; use <see cref="Editor"/> to convert between graph
/// and view coordinates, so the layer moves with the graph.
/// </summary>
/// <remarks>Layers fill the editor and don't take part in hit testing.</remarks>
public abstract class NodeEditorLayer : UIElement
{
    static NodeEditorLayer()
    {
        NodeEditorTheme.Register();
        IsHitTestVisibleProperty.OverrideDefaultValue<NodeEditorLayer>(false);
    }

    /// <summary>Gets the editor the layer is in.</summary>
    public NodeEditor? Editor { get; internal set; }

    /// <summary>Draws the layer in the editor's coordinates, over the editor's whole area.</summary>
    public abstract void Render(ref DrawingContext context);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize) => Size.Zero;

    /// <summary>
    /// Gets the distance in pixels between the lines or dots of a grid with <paramref name="spacing"/> graph units shown at
    /// <paramref name="zoom"/>: while it would be below <paramref name="minimum"/> pixels, only every
    /// <paramref name="every"/>th line stays. <paramref name="fade"/> (0..1) says how visible the finest lines should be, so
    /// they fade out before they thin out.
    /// </summary>
    public static float VisibleStep(float spacing, int every, float zoom, float minimum, out float fade)
    {
        float step = spacing * zoom;
        if (step <= 0 || !float.IsFinite(step))
        {
            fade = 0;
            return 0;
        }
        every = Math.Max(2, every);
        while (step < minimum) step *= every;
        fade = Math.Clamp((step - minimum) / minimum, 0f, 1f);
        return step;
    }
}

/// <summary>A grid of lines that moves and scales with the graph, with a stronger line every few lines.</summary>
/// <remarks>When zoomed out, lines closer than 8 px fade out and only every <see cref="MajorLineEvery"/>th line stays.</remarks>
public class GridLayer : NodeEditorLayer
{
    /// <summary>Identifies the <see cref="Spacing"/> property.</summary>
    public static readonly BindableProperty<float> SpacingProperty =
        BindableProperty.Register<GridLayer, float>(nameof(Spacing), 20f, options: PropertyOptions.AffectsRender, validateValue: v => float.IsFinite(v) && v > 0);

    /// <summary>Identifies the <see cref="MajorLineEvery"/> property.</summary>
    public static readonly BindableProperty<int> MajorLineEveryProperty =
        BindableProperty.Register<GridLayer, int>(nameof(MajorLineEvery), 5, options: PropertyOptions.AffectsRender, validateValue: v => v >= 2);

    /// <summary>Identifies the <see cref="LineColor"/> property.</summary>
    public static readonly BindableProperty<Color> LineColorProperty =
        BindableProperty.Register<GridLayer, Color>(nameof(LineColor), Color.FromArgb(0x40, 0x80, 0x80, 0x80), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="MajorLineColor"/> property.</summary>
    public static readonly BindableProperty<Color> MajorLineColorProperty =
        BindableProperty.Register<GridLayer, Color>(nameof(MajorLineColor), Color.FromArgb(0x80, 0x80, 0x80, 0x80), options: PropertyOptions.AffectsRender);

    private const float MinimumStep = 8f;

    /// <summary>Gets or sets the distance between lines in graph units. The default is 20.</summary>
    public float Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>Gets or sets how many lines apart the stronger lines are. The default is 5.</summary>
    public int MajorLineEvery { get => GetValue(MajorLineEveryProperty); set => SetValue(MajorLineEveryProperty, value); }

    /// <summary>Gets or sets the color of the lines.</summary>
    public Color LineColor { get => GetValue(LineColorProperty); set => SetValue(LineColorProperty, value); }

    /// <summary>Gets or sets the color of every <see cref="MajorLineEvery"/>th line.</summary>
    public Color MajorLineColor { get => GetValue(MajorLineColorProperty); set => SetValue(MajorLineColorProperty, value); }

    /// <inheritdoc/>
    public override void Render(ref DrawingContext context)
    {
        if (Editor is not { } editor || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        int every = MajorLineEvery;
        float step = VisibleStep(Spacing, every, editor.Zoom, MinimumStep, out float fade);
        if (step <= 0) return;

        var minor = LineColor.WithAlpha(LineColor.Af * fade);
        var major = MajorLineColor;
        var offset = editor.Offset;

        long first = (long)MathF.Floor(-offset.X / step);
        for (long i = first; ; i++)
        {
            float x = offset.X + i * step;
            if (x > Bounds.Width) break;
            bool isMajor = i % every == 0;
            context.DrawPixelRect(new Rect(x, 0, 1, Bounds.Height), isMajor ? major : minor);
        }
        first = (long)MathF.Floor(-offset.Y / step);
        for (long i = first; ; i++)
        {
            float y = offset.Y + i * step;
            if (y > Bounds.Height) break;
            bool isMajor = i % every == 0;
            context.DrawPixelRect(new Rect(0, y, Bounds.Width, 1), isMajor ? major : minor);
        }
    }
}

/// <summary>A grid of dots that moves and scales with the graph.</summary>
/// <remarks>When zoomed out, dots closer than 12 px fade out and only every fifth dot stays.</remarks>
public class DotGridLayer : NodeEditorLayer
{
    /// <summary>Identifies the <see cref="Spacing"/> property.</summary>
    public static readonly BindableProperty<float> SpacingProperty =
        BindableProperty.Register<DotGridLayer, float>(nameof(Spacing), 20f, options: PropertyOptions.AffectsRender, validateValue: v => float.IsFinite(v) && v > 0);

    /// <summary>Identifies the <see cref="DotColor"/> property.</summary>
    public static readonly BindableProperty<Color> DotColorProperty =
        BindableProperty.Register<DotGridLayer, Color>(nameof(DotColor), Color.FromArgb(0x80, 0x80, 0x80, 0x80), options: PropertyOptions.AffectsRender);

    /// <summary>Identifies the <see cref="DotRadius"/> property.</summary>
    public static readonly BindableProperty<float> DotRadiusProperty =
        BindableProperty.Register<DotGridLayer, float>(nameof(DotRadius), 1.25f, options: PropertyOptions.AffectsRender, validateValue: v => float.IsFinite(v) && v > 0);

    private const float MinimumStep = 12f;
    private const int Every = 5;

    /// <summary>Gets or sets the distance between dots in graph units. The default is 20.</summary>
    public float Spacing { get => GetValue(SpacingProperty); set => SetValue(SpacingProperty, value); }

    /// <summary>Gets or sets the color of the dots.</summary>
    public Color DotColor { get => GetValue(DotColorProperty); set => SetValue(DotColorProperty, value); }

    /// <summary>Gets or sets the radius of the dots in pixels (they don't scale with the zoom). The default is 1.25.</summary>
    public float DotRadius { get => GetValue(DotRadiusProperty); set => SetValue(DotRadiusProperty, value); }

    /// <inheritdoc/>
    public override void Render(ref DrawingContext context)
    {
        if (Editor is not { } editor || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        float step = VisibleStep(Spacing, Every, editor.Zoom, MinimumStep, out float fade);
        if (step <= 0) return;

        var color = DotColor.WithAlpha(DotColor.Af * (0.4f + 0.6f * fade));
        var offset = editor.Offset;
        float radius = DotRadius;
        long firstX = (long)MathF.Floor(-offset.X / step), firstY = (long)MathF.Floor(-offset.Y / step);
        for (long j = firstY; ; j++)
        {
            float y = offset.Y + j * step;
            if (y > Bounds.Height + radius) break;
            for (long i = firstX; ; i++)
            {
                float x = offset.X + i * step;
                if (x > Bounds.Width + radius) break;
                context.DrawCircle(new Point(x, y), radius, color);
            }
        }
    }
}
