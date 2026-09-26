using System;
using SkiaSharp;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

using PrimitiveSize = Atelier.Core.Primitives.Size;

namespace Atelier.Controls;

/// <summary>
/// A high-performance icon control that renders Google Material Symbols Rounded
/// using its variable font capabilities across 4 axes: FILL, wght (weight), GRAD (grade), and opsz (optical size),
/// or custom vector geometry from an <see cref="SKPath"/> or SVG path data string.
/// </summary>
/// <remarks>
/// The axis properties are clamped to the font's ranges by coercion, so values from bindings and styles are clamped too.
/// While <see cref="Foreground"/> is not set anywhere the theme's icon color is used.
/// </remarks>
public class Icon : Control
{
    /// <summary>The smallest optical size the Material Symbols font supports.</summary>
    private const float MinOpticalSize = 20f;

    /// <summary>The largest optical size the Material Symbols font supports.</summary>
    private const float MaxOpticalSize = 48f;

    #region Bindable Properties

    /// <summary>Identifies the <see cref="Kind"/> property.</summary>
    public static readonly BindableProperty<MaterialIconKind> KindProperty =
        BindableProperty.Register<Icon, MaterialIconKind>(
            nameof(Kind),
            MaterialIconKind.None,
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="Data"/> property.</summary>
    public static readonly BindableProperty<SKPath?> DataProperty =
        BindableProperty.Register<Icon, SKPath?>(
            nameof(Data),
            null,
            (s, o, n) => ((Icon)s).OnDataChanged(o, n),
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="PathData"/> property.</summary>
    public static readonly BindableProperty<string?> PathDataProperty =
        BindableProperty.Register<Icon, string?>(
            nameof(PathData),
            null,
            (s, o, n) => ((Icon)s).OnPathDataChanged(n)
        );

    /// <summary>Identifies the <see cref="StrokeWidth"/> property.</summary>
    public static readonly BindableProperty<float> StrokeWidthProperty =
        BindableProperty.Register<Icon, float>(
            nameof(StrokeWidth),
            0f,
            coerceValue: static (_, value) => float.IsNaN(value) ? 0f : Math.Max(0f, value),
            options: PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="Size"/> property.</summary>
    public static readonly BindableProperty<float> SizeProperty =
        BindableProperty.Register<Icon, float>(
            nameof(Size),
            24f,
            options: PropertyOptions.AffectsMeasure | PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="Fill"/> property.</summary>
    public static readonly BindableProperty<float> FillProperty =
        BindableProperty.Register<Icon, float>(
            nameof(Fill),
            0f,
            coerceValue: static (_, value) => ClampAxis(value, 0f, 1f, 0f),
            options: PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="Weight"/> property.</summary>
    public static readonly BindableProperty<float> WeightProperty =
        BindableProperty.Register<Icon, float>(
            nameof(Weight),
            400f,
            coerceValue: static (_, value) => ClampAxis(value, 100f, 700f, 400f),
            options: PropertyOptions.AffectsRender
        );

    /// <summary>Identifies the <see cref="Grade"/> property.</summary>
    public static readonly BindableProperty<float> GradeProperty =
        BindableProperty.Register<Icon, float>(
            nameof(Grade),
            0f,
            coerceValue: static (_, value) => ClampAxis(value, -25f, 200f, 0f),
            options: PropertyOptions.AffectsRender
        );

    /// <summary>
    /// Identifies the <see cref="OpticalSize"/> property. Its stored default is 24; while it is not set, the
    /// <see cref="OpticalSize"/> accessor follows <see cref="Size"/>.
    /// </summary>
    public static readonly BindableProperty<float> OpticalSizeProperty =
        BindableProperty.Register<Icon, float>(
            nameof(OpticalSize),
            24f,
            coerceValue: static (_, value) => ClampAxis(value, MinOpticalSize, MaxOpticalSize, 24f),
            options: PropertyOptions.AffectsRender
        );

    #endregion

    #region Property Accessors

    /// <summary>
    /// Gets or sets the icon glyph to display from the <see cref="MaterialIconKind"/> catalog.
    /// </summary>
    public MaterialIconKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>
    /// Gets or sets the display size in device-independent pixels (width and height). Defaults to 24dp.
    /// </summary>
    public float Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the variable font FILL axis (0.0 = Outlined, 1.0 = Filled), clamped to 0..1.
    /// </summary>
    public float Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <summary>
    /// Gets or sets a boolean indicating whether the icon is filled (FILL = 1.0) or outlined (FILL = 0.0).
    /// </summary>
    public bool IsFilled
    {
        get => Fill >= 0.5f;
        set => Fill = value ? 1f : 0f;
    }

    /// <summary>
    /// Gets or sets the variable font stroke weight axis, clamped to 100..700. Defaults to 400 (Regular).
    /// </summary>
    public float Weight
    {
        get => GetValue(WeightProperty);
        set => SetValue(WeightProperty, value);
    }

    /// <summary>
    /// Gets or sets the variable font grade/contrast axis, clamped to -25..200. Defaults to 0.
    /// </summary>
    public float Grade
    {
        get => GetValue(GradeProperty);
        set => SetValue(GradeProperty, value);
    }

    /// <summary>
    /// Gets or sets the variable font optical size axis, clamped to 20..48. While it is not set (locally, by a style
    /// or a binding) it follows <see cref="Size"/>, clamped to that range; so the default for a 24dp icon is 24.
    /// </summary>
    public float OpticalSize
    {
        get => GetValueSource(OpticalSizeProperty) == ValueSource.Default
            ? ClampAxis(Size, MinOpticalSize, MaxOpticalSize, 24f)
            : GetValue(OpticalSizeProperty);
        set => SetValue(OpticalSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the custom vector geometry as an <see cref="SKPath"/>.
    /// When set, this path is rendered instead of a Material font glyph. The icon doesn't dispose paths assigned here.
    /// </summary>
    public SKPath? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>
    /// Gets or sets the custom vector geometry as an SVG path data string (e.g. "M10 20v-6h4v6...").
    /// Automatically parsed into <see cref="Data"/>; invalid data gives no geometry. The parsed path is owned by the
    /// icon and disposed when it is replaced.
    /// </summary>
    public string? PathData
    {
        get => GetValue(PathDataProperty);
        set => SetValue(PathDataProperty, value);
    }

    /// <summary>
    /// Gets or sets the stroke width when rendering custom vector geometry (not negative).
    /// When 0 (default), the path is filled. When > 0, the path outline is stroked.
    /// </summary>
    public float StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    #endregion

    // The path parsed from PathData, which this icon created and therefore disposes when it is replaced.
    private SKPath? _ownedPath;

    /// <summary>Initializes an empty icon (no glyph, 24dp).</summary>
    public Icon()
    {
    }

    /// <summary>Initializes an icon showing a Material Symbols glyph.</summary>
    /// <param name="kind">The glyph.</param>
    /// <param name="size">The size in dp.</param>
    /// <param name="isFilled">Whether to use the filled variant.</param>
    /// <param name="foreground">The icon color, or <c>null</c> for the theme color.</param>
    public Icon(MaterialIconKind kind, float size = 24f, bool isFilled = false, Color? foreground = null) : this()
    {
        Kind = kind;
        Size = size;
        IsFilled = isFilled;
        if (foreground.HasValue)
        {
            Foreground = foreground.Value;
        }
    }

    /// <summary>Initializes an icon drawing custom geometry, scaled uniformly to <paramref name="size"/>.</summary>
    /// <param name="path">The geometry; not disposed by the icon.</param>
    /// <param name="size">The size in dp.</param>
    /// <param name="foreground">The icon color, or <c>null</c> for the theme color.</param>
    public Icon(SKPath path, float size = 24f, Color? foreground = null) : this()
    {
        Data = path;
        Size = size;
        if (foreground.HasValue)
        {
            Foreground = foreground.Value;
        }
    }

    /// <summary>Initializes an icon drawing SVG path data, scaled uniformly to <paramref name="size"/>.</summary>
    /// <param name="svgPathData">The SVG path data (see <see cref="PathData"/>).</param>
    /// <param name="size">The size in dp.</param>
    /// <param name="foreground">The icon color, or <c>null</c> for the theme color.</param>
    public Icon(string svgPathData, float size = 24f, Color? foreground = null) : this()
    {
        PathData = svgPathData;
        Size = size;
        if (foreground.HasValue)
        {
            Foreground = foreground.Value;
        }
    }

    private static float ClampAxis(float value, float min, float max, float fallback) =>
        float.IsNaN(value) ? fallback : Math.Clamp(value, min, max);

    private void OnDataChanged(SKPath? oldVal, SKPath? newVal)
    {
        if (oldVal != null && ReferenceEquals(oldVal, _ownedPath) && !ReferenceEquals(oldVal, newVal))
        {
            _ownedPath = null;
            oldVal.Dispose();
        }
    }

    private void OnPathDataChanged(string? newVal)
    {
        SKPath? path = null;
        if (!string.IsNullOrWhiteSpace(newVal))
        {
            try
            {
                path = SKPath.ParseSvgPathData(newVal);
            }
            catch
            {
                path = null;
            }
        }

        Data = path;
        _ownedPath = path;
    }

    /// <inheritdoc/>
    protected override PrimitiveSize MeasureOverride(PrimitiveSize availableSize)
    {
        if (Size <= 0)
        {
            return PrimitiveSize.Zero;
        }

        return new PrimitiveSize(Size, Size);
    }

    /// <inheritdoc/>
    protected override PrimitiveSize ArrangeOverride(PrimitiveSize finalSize)
    {
        return finalSize;
    }
}
