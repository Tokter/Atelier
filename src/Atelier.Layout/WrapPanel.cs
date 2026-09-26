using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;

namespace Atelier.Layout;

/// <summary>
/// Positions children one after another, left to right (<see cref="Orientation.Horizontal"/>, the default) or top to
/// bottom (<see cref="Orientation.Vertical"/>), and starts a new line when the next child doesn't fit.
/// </summary>
/// <remarks>
/// <para>
/// Each line is as tall (or, vertically, as wide) as its largest child, and children are stretched across the line.
/// <see cref="ItemWidth"/> and <see cref="ItemHeight"/> give every child the same slot size instead of its desired size.
/// </para>
/// <para>
/// <see cref="HorizontalSpacing"/> and <see cref="VerticalSpacing"/> separate children within a line and lines from
/// each other, depending on the orientation. Collapsed children take no space. With infinite space along the
/// orientation (for example inside a horizontal <c>ScrollViewer</c>), everything stays on one line.
/// </para>
/// </remarks>
public class WrapPanel : Panel
{
    // Sub-pixel tolerance for "does the child still fit on this line", so rounding differences between measure and
    // arrange (or layout rounding of the children) can't wrap a child that exactly fits.
    private const float FitTolerance = 0.01f;

    /// <summary>Identifies the <see cref="Orientation"/> property.</summary>
    public static readonly BindableProperty<Orientation> OrientationProperty =
        BindableProperty.Register<WrapPanel, Orientation>(nameof(Orientation), Orientation.Horizontal, options: PropertyOptions.AffectsMeasure);

    /// <summary>Identifies the <see cref="ItemWidth"/> property.</summary>
    public static readonly BindableProperty<float> ItemWidthProperty =
        BindableProperty.Register<WrapPanel, float>(nameof(ItemWidth), float.NaN, options: PropertyOptions.AffectsMeasure, validateValue: IsValidItemSize);

    /// <summary>Identifies the <see cref="ItemHeight"/> property.</summary>
    public static readonly BindableProperty<float> ItemHeightProperty =
        BindableProperty.Register<WrapPanel, float>(nameof(ItemHeight), float.NaN, options: PropertyOptions.AffectsMeasure, validateValue: IsValidItemSize);

    /// <summary>Identifies the <see cref="HorizontalSpacing"/> property.</summary>
    public static readonly BindableProperty<float> HorizontalSpacingProperty =
        BindableProperty.Register<WrapPanel, float>(nameof(HorizontalSpacing), 0f, options: PropertyOptions.AffectsMeasure, validateValue: IsValidSpacing);

    /// <summary>Identifies the <see cref="VerticalSpacing"/> property.</summary>
    public static readonly BindableProperty<float> VerticalSpacingProperty =
        BindableProperty.Register<WrapPanel, float>(nameof(VerticalSpacing), 0f, options: PropertyOptions.AffectsMeasure, validateValue: IsValidSpacing);

    /// <summary>Gets or sets the flow direction. The default is <see cref="Orientation.Horizontal"/>.</summary>
    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Gets or sets the width of every child's slot. The default, <see cref="float.NaN"/>, uses each child's desired width.
    /// </summary>
    public float ItemWidth
    {
        get => GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the height of every child's slot. The default, <see cref="float.NaN"/>, uses each child's desired height.
    /// </summary>
    public float ItemHeight
    {
        get => GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    /// <summary>Gets or sets the horizontal gap between children (horizontal flow) or between lines (vertical flow).</summary>
    public float HorizontalSpacing
    {
        get => GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    /// <summary>Gets or sets the vertical gap between lines (horizontal flow) or between children (vertical flow).</summary>
    public float VerticalSpacing
    {
        get => GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    private static bool IsValidItemSize(float value) => float.IsNaN(value) || (float.IsFinite(value) && value > 0);

    /// <summary>The settings shared by measure and arrange, read once per pass.</summary>
    private readonly struct FlowSettings
    {
        public FlowSettings(WrapPanel panel, float lineLength)
        {
            IsHorizontal = panel.Orientation == Orientation.Horizontal;
            ItemWidth = panel.ItemWidth;
            ItemHeight = panel.ItemHeight;
            float horizontal = panel.HorizontalSpacing;
            float vertical = panel.VerticalSpacing;
            ItemSpacing = IsHorizontal ? horizontal : vertical;
            LineSpacing = IsHorizontal ? vertical : horizontal;
            LineLength = lineLength;
        }

        public bool IsHorizontal { get; }
        public float ItemWidth { get; }   // NaN: the child's desired width
        public float ItemHeight { get; }  // NaN: the child's desired height
        public float ItemSpacing { get; }
        public float LineSpacing { get; }
        public float LineLength { get; }  // space along the flow direction

        public float SlotWidth(UIElement child) => float.IsNaN(ItemWidth) ? child.DesiredSize.Width : ItemWidth;
        public float SlotHeight(UIElement child) => float.IsNaN(ItemHeight) ? child.DesiredSize.Height : ItemHeight;
        public float Along(UIElement child) => IsHorizontal ? SlotWidth(child) : SlotHeight(child);
        public float Across(UIElement child) => IsHorizontal ? SlotHeight(child) : SlotWidth(child);

        public bool Fits(float lineUsed, int itemsOnLine, float childAlong) =>
            itemsOnLine == 0 || lineUsed + ItemSpacing + childAlong <= LineLength + FitTolerance;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var flow = new FlowSettings(this, Orientation == Orientation.Horizontal ? availableSize.Width : availableSize.Height);
        var childConstraint = new Size(
            float.IsNaN(flow.ItemWidth) ? availableSize.Width : flow.ItemWidth,
            float.IsNaN(flow.ItemHeight) ? availableSize.Height : flow.ItemHeight);

        float lineAlong = 0, lineAcross = 0;
        int itemsOnLine = 0;
        float maxAlong = 0, totalAcross = 0;
        int lineCount = 0;

        var children = Children;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not UIElement child)
            {
                continue;
            }

            child.Measure(childConstraint);
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            float along = flow.Along(child);
            float across = flow.Across(child);
            if (!flow.Fits(lineAlong, itemsOnLine, along))
            {
                EndLine(lineAlong, lineAcross, ref maxAlong, ref totalAcross, ref lineCount, flow.LineSpacing);
                lineAlong = 0;
                lineAcross = 0;
                itemsOnLine = 0;
            }

            lineAlong += (itemsOnLine > 0 ? flow.ItemSpacing : 0) + along;
            lineAcross = Math.Max(lineAcross, across);
            itemsOnLine++;
        }

        if (itemsOnLine > 0)
        {
            EndLine(lineAlong, lineAcross, ref maxAlong, ref totalAcross, ref lineCount, flow.LineSpacing);
        }

        return flow.IsHorizontal ? new Size(maxAlong, totalAcross) : new Size(totalAcross, maxAlong);
    }

    private static void EndLine(float lineAlong, float lineAcross, ref float maxAlong, ref float totalAcross, ref int lineCount, float lineSpacing)
    {
        maxAlong = Math.Max(maxAlong, lineAlong);
        totalAcross += (lineCount > 0 ? lineSpacing : 0) + lineAcross;
        lineCount++;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var flow = new FlowSettings(this, Orientation == Orientation.Horizontal ? finalSize.Width : finalSize.Height);

        float lineAlong = 0, lineAcross = 0, lineOffset = 0;
        int itemsOnLine = 0;
        int lineStart = 0;

        var children = Children;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] is not UIElement child)
            {
                continue;
            }

            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(Rect.Zero);
                continue;
            }

            float along = flow.Along(child);
            if (!flow.Fits(lineAlong, itemsOnLine, along))
            {
                ArrangeLine(flow, lineStart, i, lineOffset, lineAcross);
                lineOffset += lineAcross + flow.LineSpacing;
                lineAlong = 0;
                lineAcross = 0;
                itemsOnLine = 0;
                lineStart = i;
            }

            lineAlong += (itemsOnLine > 0 ? flow.ItemSpacing : 0) + along;
            lineAcross = Math.Max(lineAcross, flow.Across(child));
            itemsOnLine++;
        }

        if (itemsOnLine > 0)
        {
            ArrangeLine(flow, lineStart, children.Count, lineOffset, lineAcross);
        }

        return finalSize;
    }

    // Arranges the visible children in [start, end) as one line at lineOffset across the flow.
    private void ArrangeLine(in FlowSettings flow, int start, int end, float lineOffset, float lineAcross)
    {
        var children = Children;
        float offset = 0;
        for (int i = start; i < end; i++)
        {
            if (children[i] is not UIElement child || child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            // Along the flow a child gets its slot size; across it, the fixed item size or the full line.
            if (flow.IsHorizontal)
            {
                float width = flow.SlotWidth(child);
                float height = float.IsNaN(flow.ItemHeight) ? lineAcross : flow.ItemHeight;
                child.Arrange(new Rect(offset, lineOffset, width, height));
                offset += width + flow.ItemSpacing;
            }
            else
            {
                float width = float.IsNaN(flow.ItemWidth) ? lineAcross : flow.ItemWidth;
                float height = flow.SlotHeight(child);
                child.Arrange(new Rect(lineOffset, offset, width, height));
                offset += height + flow.ItemSpacing;
            }
        }
    }
}
