using System;
using System.Runtime.CompilerServices;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Markup;

/// <summary>Fluent methods for all <see cref="Panel"/>s. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class PanelMarkup
{
    /// <summary>Adds <paramref name="children"/> to the panel, after any children it already has.</summary>
    public static T Children<T>(this T panel, params UIElement[] children) where T : Panel
    {
        for (int i = 0; i < children.Length; i++)
        {
            panel.Add(children[i]);
        }
        return panel;
    }

    /// <summary>Sets the <see cref="Panel.Background"/> drawn behind the children. A transparent background (the default) draws nothing.</summary>
    public static T Background<T>(this T panel, Color background) where T : Panel => panel.Set(Panel.BackgroundProperty, background);
}

/// <summary>Fluent methods for <see cref="StackPanel"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class StackPanelMarkup
{
    /// <summary>Sets whether children are stacked top to bottom (<see cref="Layout.Orientation.Vertical"/>, the default) or left to right.</summary>
    public static T Orientation<T>(this T panel, Orientation orientation) where T : StackPanel => panel.Set(StackPanel.OrientationProperty, orientation);

    /// <summary>Sets the gap between adjacent children in pixels.</summary>
    public static T Spacing<T>(this T panel, float spacing) where T : StackPanel => panel.Set(StackPanel.SpacingProperty, spacing);
}

/// <summary>Fluent methods for <see cref="WrapPanel"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class WrapPanelMarkup
{
    /// <summary>
    /// Sets the flow direction. <see cref="Layout.Orientation.Horizontal"/> (the default) fills rows left to right and
    /// wraps to a new row; <see cref="Layout.Orientation.Vertical"/> fills columns top to bottom.
    /// </summary>
    public static T Orientation<T>(this T panel, Orientation orientation) where T : WrapPanel => panel.Set(WrapPanel.OrientationProperty, orientation);

    /// <summary>Gives every child the same width. <see cref="float.NaN"/> (the default) uses each child's desired width.</summary>
    public static T ItemWidth<T>(this T panel, float itemWidth) where T : WrapPanel => panel.Set(WrapPanel.ItemWidthProperty, itemWidth);

    /// <summary>Gives every child the same height. <see cref="float.NaN"/> (the default) uses each child's desired height.</summary>
    public static T ItemHeight<T>(this T panel, float itemHeight) where T : WrapPanel => panel.Set(WrapPanel.ItemHeightProperty, itemHeight);

    /// <summary>Sets the horizontal gap between children in pixels.</summary>
    public static T HorizontalSpacing<T>(this T panel, float spacing) where T : WrapPanel => panel.Set(WrapPanel.HorizontalSpacingProperty, spacing);

    /// <summary>Sets the vertical gap between children in pixels.</summary>
    public static T VerticalSpacing<T>(this T panel, float spacing) where T : WrapPanel => panel.Set(WrapPanel.VerticalSpacingProperty, spacing);

    /// <summary>Sets the same horizontal and vertical gap between children.</summary>
    public static T Spacing<T>(this T panel, float spacing) where T : WrapPanel => panel.Spacing(spacing, spacing);

    /// <summary>Sets the horizontal and vertical gap between children.</summary>
    public static T Spacing<T>(this T panel, float horizontal, float vertical) where T : WrapPanel =>
        panel.HorizontalSpacing(horizontal).VerticalSpacing(vertical);
}

/// <summary>Fluent methods for <see cref="DockPanel"/>. Children choose their edge with <c>.Dock(...)</c>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class DockPanelMarkup
{
    /// <summary>Sets whether the last child fills the remaining space (the default) instead of docking to its edge.</summary>
    public static T LastChildFill<T>(this T panel, bool lastChildFill = true) where T : DockPanel => panel.Set(DockPanel.LastChildFillProperty, lastChildFill);

    /// <summary>Sets the gap after children docked to the left or right, in pixels.</summary>
    public static T HorizontalSpacing<T>(this T panel, float spacing) where T : DockPanel => panel.Set(DockPanel.HorizontalSpacingProperty, spacing);

    /// <summary>Sets the gap after children docked to the top or bottom, in pixels.</summary>
    public static T VerticalSpacing<T>(this T panel, float spacing) where T : DockPanel => panel.Set(DockPanel.VerticalSpacingProperty, spacing);

    /// <summary>Sets the same gap after all docked children.</summary>
    public static T Spacing<T>(this T panel, float spacing) where T : DockPanel => panel.Spacing(spacing, spacing);

    /// <summary>Sets the gap after horizontally and vertically docked children.</summary>
    public static T Spacing<T>(this T panel, float horizontal, float vertical) where T : DockPanel =>
        panel.HorizontalSpacing(horizontal).VerticalSpacing(vertical);
}

/// <summary>
/// Fluent methods for <see cref="Grid"/>. Children choose their cell with <c>.Row(...)</c>, <c>.Column(...)</c> or
/// <c>.Cell(...)</c>. See <see cref="MarkupExtensions"/> for the conventions.
/// </summary>
public static class GridMarkup
{
    /// <summary>Adds rows with the given heights: <c>.Rows(GridLength.Auto, GridLength.Star)</c>.</summary>
    public static T Rows<T>(this T grid, params GridLength[] rows) where T : Grid
    {
        for (int i = 0; i < rows.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(rows[i]));
        }
        return grid;
    }

    /// <summary>Adds columns with the given widths: <c>.Columns(GridLength.Pixels(200), GridLength.Star)</c>.</summary>
    public static T Columns<T>(this T grid, params GridLength[] columns) where T : Grid
    {
        for (int i = 0; i < columns.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(columns[i]));
        }
        return grid;
    }

    /// <summary>
    /// Adds rows from a comma-separated list of heights: <c>.Rows("Auto,*,2*,48")</c>. <c>Auto</c> sizes to the content,
    /// <c>*</c> and <c>2*</c> share the remaining space by weight, and a number is a fixed size in pixels
    /// (see <see cref="GridLength.Parse"/>).
    /// </summary>
    /// <exception cref="FormatException">An entry isn't a valid length.</exception>
    public static T Rows<T>(this T grid, string rows) where T : Grid
    {
        foreach (var part in rows.Split(',', StringSplitOptions.TrimEntries))
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Parse(part)));
        }
        return grid;
    }

    /// <summary>
    /// Adds columns from a comma-separated list of widths: <c>.Columns("200,*,Auto")</c>. <c>Auto</c> sizes to the
    /// content, <c>*</c> and <c>2*</c> share the remaining space by weight, and a number is a fixed size in pixels
    /// (see <see cref="GridLength.Parse"/>).
    /// </summary>
    /// <exception cref="FormatException">An entry isn't a valid length.</exception>
    public static T Columns<T>(this T grid, string columns) where T : Grid
    {
        foreach (var part in columns.Split(',', StringSplitOptions.TrimEntries))
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Parse(part)));
        }
        return grid;
    }

    /// <summary>Sets the gap between rows in pixels.</summary>
    public static T RowSpacing<T>(this T grid, float spacing) where T : Grid => grid.Set(Grid.RowSpacingProperty, spacing);

    /// <summary>Sets the gap between columns in pixels.</summary>
    public static T ColumnSpacing<T>(this T grid, float spacing) where T : Grid => grid.Set(Grid.ColumnSpacingProperty, spacing);

    /// <summary>Sets the same gap between rows and between columns.</summary>
    public static T Spacing<T>(this T grid, float spacing) where T : Grid => grid.Spacing(spacing, spacing);

    /// <summary>
    /// Sets the gap between columns (<paramref name="horizontal"/>) and between rows (<paramref name="vertical"/>), in
    /// the same order as the other two-value spacing and margin methods.
    /// </summary>
    public static T Spacing<T>(this T grid, float horizontal, float vertical) where T : Grid =>
        grid.ColumnSpacing(horizontal).RowSpacing(vertical);
}

/// <summary>Fluent methods for <see cref="UniformGrid"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class UniformGridMarkup
{
    /// <summary>Sets the number of rows. 0 (the default) derives it from the number of children and columns.</summary>
    public static T Rows<T>(this T grid, int rows) where T : UniformGrid => grid.Set(UniformGrid.RowsProperty, rows);

    /// <summary>Sets the number of columns. 0 (the default) derives it from the number of children and rows.</summary>
    public static T Columns<T>(this T grid, int columns) where T : UniformGrid => grid.Set(UniformGrid.ColumnsProperty, columns);

    /// <summary>Sets the number of empty cells before the first child in the first row.</summary>
    public static T FirstColumn<T>(this T grid, int firstColumn) where T : UniformGrid => grid.Set(UniformGrid.FirstColumnProperty, firstColumn);

    /// <summary>Sets the gap between rows in pixels.</summary>
    public static T RowSpacing<T>(this T grid, float spacing) where T : UniformGrid => grid.Set(UniformGrid.RowSpacingProperty, spacing);

    /// <summary>Sets the gap between columns in pixels.</summary>
    public static T ColumnSpacing<T>(this T grid, float spacing) where T : UniformGrid => grid.Set(UniformGrid.ColumnSpacingProperty, spacing);

    /// <summary>Sets the same gap between rows and between columns.</summary>
    public static T Spacing<T>(this T grid, float spacing) where T : UniformGrid => grid.Spacing(spacing, spacing);

    /// <summary>Sets the gap between columns (<paramref name="horizontal"/>) and between rows (<paramref name="vertical"/>).</summary>
    public static T Spacing<T>(this T grid, float horizontal, float vertical) where T : UniformGrid =>
        grid.ColumnSpacing(horizontal).RowSpacing(vertical);
}

/// <summary>Fluent methods for <see cref="Border"/> and <see cref="Card"/>. See <see cref="MarkupExtensions"/> for the conventions.</summary>
public static class BorderMarkup
{
    /// <summary>Sets the single <see cref="Border.Child"/> the border decorates.</summary>
    public static T Child<T>(this T border, UIElement? child) where T : Border
    {
        border.Child = child;
        return border;
    }

    /// <summary>Sets the fill drawn inside the border.</summary>
    public static T Background<T>(this T border, Color background) where T : Border => border.Set(Border.BackgroundProperty, background);

    /// <summary>Sets the color of the outline. It is drawn only where <c>BorderThickness</c> is non-zero.</summary>
    public static T BorderBrush<T>(this T border, Color color) where T : Border => border.Set(Border.BorderBrushProperty, color);

    /// <summary>Sets the same outline width on all sides.</summary>
    public static T BorderThickness<T>(this T border, float uniform) where T : Border => border.Set(Border.BorderThicknessProperty, new Thickness(uniform));

    /// <summary>Sets the outline width of each side.</summary>
    public static T BorderThickness<T>(this T border, Thickness thickness) where T : Border => border.Set(Border.BorderThicknessProperty, thickness);

    /// <summary>Sets the outline color and width together.</summary>
    public static T Stroke<T>(this T border, Color color, float thickness = 1f) where T : Border => border.BorderBrush(color).BorderThickness(thickness);

    /// <summary>Rounds all four corners by <paramref name="uniform"/> pixels. Children are clipped to the shape with <c>.ClipToBounds()</c>.</summary>
    public static T CornerRadius<T>(this T border, float uniform) where T : Border => border.Set(Border.CornerRadiusProperty, new CornerRadius(uniform));

    /// <summary>Rounds each corner by its own radius, in pixels.</summary>
    public static T CornerRadius<T>(this T border, float topLeft, float topRight, float bottomRight, float bottomLeft) where T : Border =>
        border.Set(Border.CornerRadiusProperty, new CornerRadius(topLeft, topRight, bottomRight, bottomLeft));

    /// <summary>Sets the corner radii.</summary>
    public static T CornerRadius<T>(this T border, CornerRadius cornerRadius) where T : Border => border.Set(Border.CornerRadiusProperty, cornerRadius);

    /// <summary>Sets the same <see cref="Border.Padding"/> (space between the outline and the child) on all sides.</summary>
    public static T Padding<T>(this T border, float uniform) where T : Border => border.Set(Border.PaddingProperty, new Thickness(uniform));

    /// <summary>Sets the <see cref="Border.Padding"/>: <paramref name="horizontal"/> on the left and right, <paramref name="vertical"/> on the top and bottom.</summary>
    public static T Padding<T>(this T border, float horizontal, float vertical) where T : Border =>
        border.Set(Border.PaddingProperty, new Thickness(horizontal, vertical));

    /// <summary>Sets the <see cref="Border.Padding"/> of each side.</summary>
    public static T Padding<T>(this T border, float left, float top, float right, float bottom) where T : Border =>
        border.Set(Border.PaddingProperty, new Thickness(left, top, right, bottom));

    /// <summary>Sets the <see cref="Border.Padding"/>.</summary>
    public static T Padding<T>(this T border, Thickness padding) where T : Border => border.Set(Border.PaddingProperty, padding);

    /// <summary>Sets the elevation in dp, drawn as a drop shadow. 0 draws no shadow.</summary>
    public static T Elevation<T>(this T border, float elevation) where T : Border => border.Set(Border.ElevationProperty, elevation);

    /// <summary>Sets the Material card style: elevated, filled or outlined.</summary>
    public static T Variant<T>(this T card, CardVariant variant) where T : Card => card.Set(Card.VariantProperty, variant);

    /// <summary>Binds the background to a value of <paramref name="source"/>.</summary>
    public static T BindBackground<T, TSource>(this T border, TSource source, Func<TSource, Color> getter, Action<TSource, Color>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Border where TSource : class =>
        border.BindToSource(Border.BackgroundProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the background to a value of the DataContext.</summary>
    public static T BindBackground<T, TDataContext>(this T border, Func<TDataContext, Color> getter, Action<TDataContext, Color>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Border where TDataContext : class =>
        border.BindToDataContext(Border.BackgroundProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the outline color to a value of <paramref name="source"/>.</summary>
    public static T BindBorderBrush<T, TSource>(this T border, TSource source, Func<TSource, Color> getter, Action<TSource, Color>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Border where TSource : class =>
        border.BindToSource(Border.BorderBrushProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the outline color to a value of the DataContext.</summary>
    public static T BindBorderBrush<T, TDataContext>(this T border, Func<TDataContext, Color> getter, Action<TDataContext, Color>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Border where TDataContext : class =>
        border.BindToDataContext(Border.BorderBrushProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the corner radius to a value of <paramref name="source"/>.</summary>
    public static T BindCornerRadius<T, TSource>(this T border, TSource source, Func<TSource, CornerRadius> getter, Action<TSource, CornerRadius>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Border where TSource : class =>
        border.BindToSource(Border.CornerRadiusProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the corner radius to a value of the DataContext.</summary>
    public static T BindCornerRadius<T, TDataContext>(this T border, Func<TDataContext, CornerRadius> getter, Action<TDataContext, CornerRadius>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Border where TDataContext : class =>
        border.BindToDataContext(Border.CornerRadiusProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds a uniform corner radius (the same for all corners) to a value of <paramref name="source"/>.</summary>
    public static T BindCornerRadius<T, TSource>(this T border, TSource source, Func<TSource, float> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Border where TSource : class =>
        border.BindToSource(Border.CornerRadiusProperty, source, s => new CornerRadius(getter(s)), null, default, getterExpression);

    /// <summary>Binds a uniform corner radius (the same for all corners) to a value of the DataContext.</summary>
    public static T BindCornerRadius<T, TDataContext>(this T border, Func<TDataContext, float> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Border where TDataContext : class =>
        border.BindToDataContext(Border.CornerRadiusProperty, (TDataContext s) => new CornerRadius(getter(s)), null, default, getterExpression);

    /// <summary>Binds the padding to a value of <paramref name="source"/>.</summary>
    public static T BindPadding<T, TSource>(this T border, TSource source, Func<TSource, Thickness> getter, Action<TSource, Thickness>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Border where TSource : class =>
        border.BindToSource(Border.PaddingProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the padding to a value of the DataContext.</summary>
    public static T BindPadding<T, TDataContext>(this T border, Func<TDataContext, Thickness> getter, Action<TDataContext, Thickness>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Border where TDataContext : class =>
        border.BindToDataContext(Border.PaddingProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds a uniform padding (the same on all sides) to a value of <paramref name="source"/>.</summary>
    public static T BindPadding<T, TSource>(this T border, TSource source, Func<TSource, float> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Border where TSource : class =>
        border.BindToSource(Border.PaddingProperty, source, s => new Thickness(getter(s)), null, default, getterExpression);

    /// <summary>Binds a uniform padding (the same on all sides) to a value of the DataContext.</summary>
    public static T BindPadding<T, TDataContext>(this T border, Func<TDataContext, float> getter,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Border where TDataContext : class =>
        border.BindToDataContext(Border.PaddingProperty, (TDataContext s) => new Thickness(getter(s)), null, default, getterExpression);

    /// <summary>Binds the elevation to a value of <paramref name="source"/>.</summary>
    public static T BindElevation<T, TSource>(this T border, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Border where TSource : class =>
        border.BindToSource(Border.ElevationProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the elevation to a value of the DataContext.</summary>
    public static T BindElevation<T, TDataContext>(this T border, Func<TDataContext, float> getter, Action<TDataContext, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Border where TDataContext : class =>
        border.BindToDataContext(Border.ElevationProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the card variant to a value of <paramref name="source"/>.</summary>
    public static T BindVariant<T, TSource>(this T card, TSource source, Func<TSource, CardVariant> getter, Action<TSource, CardVariant>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Card where TSource : class =>
        card.BindToSource(Card.VariantProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds the card variant to a value of the DataContext.</summary>
    public static T BindVariant<T, TDataContext>(this T card, Func<TDataContext, CardVariant> getter, Action<TDataContext, CardVariant>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : Card where TDataContext : class =>
        card.BindToDataContext(Card.VariantProperty, getter, setter, updateSourceTrigger, getterExpression);
}
