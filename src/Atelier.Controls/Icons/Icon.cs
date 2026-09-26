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
public class Icon : Control
{
    #region Bindable Properties

    public static readonly BindableProperty<MaterialIconKind> KindProperty =
        BindableProperty.Register<Icon, MaterialIconKind>(
            nameof(Kind),
            MaterialIconKind.None,
            (s, o, n) => ((Icon)s).OnKindChanged(o, n)
        );

    public static readonly BindableProperty<SKPath?> DataProperty =
        BindableProperty.Register<Icon, SKPath?>(
            nameof(Data),
            null,
            (s, o, n) => ((Icon)s).OnDataChanged(o, n)
        );

    public static readonly BindableProperty<string?> PathDataProperty =
        BindableProperty.Register<Icon, string?>(
            nameof(PathData),
            null,
            (s, o, n) => ((Icon)s).OnPathDataChanged(o, n)
        );

    public static readonly BindableProperty<float> StrokeWidthProperty =
        BindableProperty.Register<Icon, float>(
            nameof(StrokeWidth),
            0f,
            options: PropertyOptions.AffectsRender
        );

    public static readonly BindableProperty<float> SizeProperty =
        BindableProperty.Register<Icon, float>(
            nameof(Size),
            24f,
            (s, o, n) => ((Icon)s).OnSizeChanged(o, n)
        );

    public static readonly BindableProperty<float> FillProperty =
        BindableProperty.Register<Icon, float>(
            nameof(Fill),
            0f,
            options: PropertyOptions.AffectsRender
        );

    public static readonly BindableProperty<float> WeightProperty =
        BindableProperty.Register<Icon, float>(
            nameof(Weight),
            400f,
            options: PropertyOptions.AffectsRender
        );

    public static readonly BindableProperty<float> GradeProperty =
        BindableProperty.Register<Icon, float>(
            nameof(Grade),
            0f,
            options: PropertyOptions.AffectsRender
        );

    public static readonly BindableProperty<float> OpticalSizeProperty =
        BindableProperty.Register<Icon, float>(
            nameof(OpticalSize),
            24f,
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
    /// Gets or sets the variable font FILL axis (0.0 = Outlined, 1.0 = Filled).
    /// </summary>
    public float Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, Math.Clamp(value, 0f, 1f));
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
    /// Gets or sets the variable font stroke weight axis (100 to 700). Defaults to 400 (Regular).
    /// </summary>
    public float Weight
    {
        get => GetValue(WeightProperty);
        set => SetValue(WeightProperty, Math.Clamp(value, 100f, 700f));
    }

    /// <summary>
    /// Gets or sets the variable font grade/contrast axis (-25 to 200). Defaults to 0.
    /// </summary>
    public float Grade
    {
        get => GetValue(GradeProperty);
        set => SetValue(GradeProperty, Math.Clamp(value, -25f, 200f));
    }

    /// <summary>
    /// Gets or sets the variable font optical size axis (20 to 48). Defaults to 24.
    /// </summary>
    public float OpticalSize
    {
        get => GetValue(OpticalSizeProperty);
        set => SetValue(OpticalSizeProperty, Math.Clamp(value, 20f, 48f));
    }

    /// <summary>
    /// Gets or sets the custom vector geometry as an <see cref="SKPath"/>.
    /// When set, this path is rendered instead of a Material font glyph.
    /// </summary>
    public SKPath? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    /// <summary>
    /// Gets or sets the custom vector geometry as an SVG path data string (e.g. "M10 20v-6h4v6...").
    /// Automatically parsed into <see cref="Data"/>.
    /// </summary>
    public string? PathData
    {
        get => GetValue(PathDataProperty);
        set => SetValue(PathDataProperty, value);
    }

    /// <summary>
    /// Gets or sets the stroke width when rendering custom vector geometry.
    /// When 0 (default), the path is filled. When > 0, the path outline is stroked.
    /// </summary>
    public float StrokeWidth
    {
        get => GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, Math.Max(0f, value));
    }

    #endregion

    public Icon()
    {
    }

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

    public Icon(SKPath path, float size = 24f, Color? foreground = null) : this()
    {
        Data = path;
        Size = size;
        if (foreground.HasValue)
        {
            Foreground = foreground.Value;
        }
    }

    public Icon(string svgPathData, float size = 24f, Color? foreground = null) : this()
    {
        PathData = svgPathData;
        Size = size;
        if (foreground.HasValue)
        {
            Foreground = foreground.Value;
        }
    }

    private void OnKindChanged(MaterialIconKind oldVal, MaterialIconKind newVal)
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnDataChanged(SKPath? oldVal, SKPath? newVal)
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnPathDataChanged(string? oldVal, string? newVal)
    {
        if (string.IsNullOrWhiteSpace(newVal))
        {
            Data = null;
        }
        else
        {
            try
            {
                Data = SKPath.ParseSvgPathData(newVal);
            }
            catch
            {
                Data = null;
            }
        }
    }

    private void OnSizeChanged(float oldVal, float newVal)
    {
        // Automatically sync OpticalSize to Size if OpticalSize was kept at default (24) or matched oldVal
        if (MathF.Abs(OpticalSize - oldVal) < 0.01f || MathF.Abs(OpticalSize - 24f) < 0.01f)
        {
            OpticalSize = Math.Clamp(newVal, 20f, 48f);
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override PrimitiveSize MeasureOverride(PrimitiveSize availableSize)
    {
        if (Size <= 0)
        {
            return PrimitiveSize.Zero;
        }

        return new PrimitiveSize(Size, Size);
    }

    protected override PrimitiveSize ArrangeOverride(PrimitiveSize finalSize)
    {
        return finalSize;
    }
}
