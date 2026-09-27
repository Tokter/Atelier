using System;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Styling;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Markup;

/// <summary>
/// Fluent extension methods for building element trees in C#. Every method sets a property (or attaches a handler or
/// binding) and returns the element, so calls chain:
/// <c>new TextBlock("Hello").Margin(8).HorizontalAlignment(HorizontalAlignment.Center)</c>.
/// </summary>
/// <remarks>
/// <para>
/// This class holds the methods that apply to every element: size, margin, alignment, visibility, styles, attached
/// layout properties (grid cell, dock edge, canvas position), transforms, input events and bindings. Control-specific
/// methods live in one class per control (<see cref="ButtonMarkup"/>, <see cref="TextBoxMarkup"/>, ...) in this
/// namespace.
/// </para>
/// <para>Naming conventions shared by all markup classes:</para>
/// <list type="bullet">
/// <item>A method that sets a property has the property's name: <c>.Padding(8)</c>, <c>.IsChecked()</c>. Boolean
/// methods default to <c>true</c>.</item>
/// <item>Properties holding a delegate (templates, selectors, easing) use a <c>With</c> prefix
/// (<c>.WithItemTemplate(...)</c>): C# would otherwise try to call the delegate instead of the method.</item>
/// <item><c>Bind{Property}(source, s =&gt; s.Value)</c> binds to a value of a source object.
/// <c>Bind{Property}((MyViewModel vm) =&gt; vm.Value)</c>, with the lambda parameter's type written out, binds to the
/// element's <see cref="BindableObject.DataContext"/> of that type. An optional setter makes the binding two-way,
/// which is what the properties the user changes (text, value, checked state, selection) usually need.</item>
/// <item><c>On{Event}</c> attaches a handler to the event. Events without data also accept an <see cref="Action"/>.</item>
/// <item>Every method returns the type it was called on, so derived controls keep their type through the chain.</item>
/// </list>
/// </remarks>
public static class MarkupExtensions
{
    #region General

    /// <summary>
    /// Sets any bindable property, including attached ones: <c>.Set(Grid.RowProperty, 2)</c>. Use it for properties
    /// without a dedicated method.
    /// </summary>
    /// <param name="element">The element to change.</param>
    /// <param name="property">The property to set.</param>
    /// <param name="value">The new local value.</param>
    public static T Set<T, TValue>(this T element, BindableProperty<TValue> property, TValue value) where T : BindableObject
    {
        element.SetValue(property, value);
        return element;
    }

    /// <summary>
    /// Runs <paramref name="configure"/> on the element and returns it, to do things that have no fluent method (such
    /// as calling a method or keeping a reference) without breaking the chain.
    /// </summary>
    /// <example><c>new TextBox().Configure(t =&gt; _nameBox = t)</c></example>
    public static T Configure<T>(this T element, Action<T> configure) where T : BindableObject
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(element);
        return element;
    }

    /// <summary>
    /// Sets the <see cref="BindableObject.DataContext"/>: the object that bindings without an explicit source read
    /// from. Descendants inherit it.
    /// </summary>
    public static T DataContext<T>(this T element, object? dataContext) where T : BindableObject =>
        element.Set(BindableObject.DataContextProperty, dataContext);

    #endregion

    #region Size

    /// <summary>Sets an explicit <see cref="UIElement.Width"/> in pixels. <see cref="float.NaN"/> (the default) sizes to the content.</summary>
    public static T Width<T>(this T element, float width) where T : UIElement => element.Set(UIElement.WidthProperty, width);

    /// <summary>Sets an explicit <see cref="UIElement.Height"/> in pixels. <see cref="float.NaN"/> (the default) sizes to the content.</summary>
    public static T Height<T>(this T element, float height) where T : UIElement => element.Set(UIElement.HeightProperty, height);

    /// <summary>Sets an explicit <see cref="UIElement.Width"/> and <see cref="UIElement.Height"/> in pixels.</summary>
    public static T Size<T>(this T element, float width, float height) where T : UIElement =>
        element.Set(UIElement.WidthProperty, width).Set(UIElement.HeightProperty, height);

    /// <summary>Sets the <see cref="UIElement.MinWidth"/>. The element is never narrower, even when its content asks for less.</summary>
    public static T MinWidth<T>(this T element, float minWidth) where T : UIElement => element.Set(UIElement.MinWidthProperty, minWidth);

    /// <summary>Sets the <see cref="UIElement.MaxWidth"/>. The element is never wider, even when stretched.</summary>
    public static T MaxWidth<T>(this T element, float maxWidth) where T : UIElement => element.Set(UIElement.MaxWidthProperty, maxWidth);

    /// <summary>Sets the <see cref="UIElement.MinHeight"/>. The element is never shorter, even when its content asks for less.</summary>
    public static T MinHeight<T>(this T element, float minHeight) where T : UIElement => element.Set(UIElement.MinHeightProperty, minHeight);

    /// <summary>Sets the <see cref="UIElement.MaxHeight"/>. The element is never taller, even when stretched.</summary>
    public static T MaxHeight<T>(this T element, float maxHeight) where T : UIElement => element.Set(UIElement.MaxHeightProperty, maxHeight);

    #endregion

    #region Margin and alignment

    /// <summary>Sets the same <see cref="UIElement.Margin"/> (outer spacing) on all four sides.</summary>
    public static T Margin<T>(this T element, float uniform) where T : UIElement => element.Set(UIElement.MarginProperty, new Thickness(uniform));

    /// <summary>Sets the <see cref="UIElement.Margin"/>: <paramref name="horizontal"/> on the left and right, <paramref name="vertical"/> on the top and bottom.</summary>
    public static T Margin<T>(this T element, float horizontal, float vertical) where T : UIElement =>
        element.Set(UIElement.MarginProperty, new Thickness(horizontal, vertical));

    /// <summary>Sets the <see cref="UIElement.Margin"/> of each side.</summary>
    public static T Margin<T>(this T element, float left, float top, float right, float bottom) where T : UIElement =>
        element.Set(UIElement.MarginProperty, new Thickness(left, top, right, bottom));

    /// <summary>Sets the <see cref="UIElement.Margin"/>.</summary>
    public static T Margin<T>(this T element, Thickness margin) where T : UIElement => element.Set(UIElement.MarginProperty, margin);

    /// <summary>
    /// Sets where the element sits horizontally in the space its parent gives it. The default,
    /// <see cref="Core.Primitives.HorizontalAlignment.Stretch"/>, fills that space.
    /// </summary>
    public static T HorizontalAlignment<T>(this T element, HorizontalAlignment alignment) where T : UIElement =>
        element.Set(UIElement.HorizontalAlignmentProperty, alignment);

    /// <summary>
    /// Sets where the element sits vertically in the space its parent gives it. The default,
    /// <see cref="Core.Primitives.VerticalAlignment.Stretch"/>, fills that space.
    /// </summary>
    public static T VerticalAlignment<T>(this T element, VerticalAlignment alignment) where T : UIElement =>
        element.Set(UIElement.VerticalAlignmentProperty, alignment);

    /// <summary>Sets the horizontal and vertical alignment together.</summary>
    public static T Align<T>(this T element, HorizontalAlignment horizontal, VerticalAlignment vertical) where T : UIElement =>
        element.HorizontalAlignment(horizontal).VerticalAlignment(vertical);

    /// <summary>Centers the element horizontally and vertically in the space its parent gives it, at its desired size.</summary>
    public static T Center<T>(this T element) where T : UIElement =>
        element.Align(Core.Primitives.HorizontalAlignment.Center, Core.Primitives.VerticalAlignment.Center);

    #endregion

    #region Visibility and interaction

    /// <summary>
    /// Sets the <see cref="UIElement.Visibility"/>. <see cref="Core.Primitives.Visibility.Hidden"/> keeps the element's
    /// space in the layout; <see cref="Core.Primitives.Visibility.Collapsed"/> removes it.
    /// </summary>
    public static T Visibility<T>(this T element, Visibility visibility) where T : UIElement => element.Set(UIElement.VisibilityProperty, visibility);

    /// <summary>Shows the element, or collapses it when <paramref name="isVisible"/> is <c>false</c> (it then takes no space).</summary>
    public static T IsVisible<T>(this T element, bool isVisible = true) where T : UIElement => element.Visibility(ToVisibility(isVisible));

    /// <summary>Sets the <see cref="UIElement.Opacity"/>, from 0 (invisible, still interactive) to 1 (opaque).</summary>
    public static T Opacity<T>(this T element, float opacity) where T : UIElement => element.Set(UIElement.OpacityProperty, opacity);

    /// <summary>
    /// Enables or disables the element. Disabled elements are drawn dimmed and ignore input; descendants inherit the
    /// setting.
    /// </summary>
    public static T IsEnabled<T>(this T element, bool isEnabled = true) where T : UIElement => element.Set(UIElement.IsEnabledProperty, isEnabled);

    /// <summary>Sets whether the element can receive keyboard focus, by clicking it or with Tab.</summary>
    public static T IsFocusable<T>(this T element, bool isFocusable = true) where T : UIElement => element.Set(UIElement.IsFocusableProperty, isFocusable);

    /// <summary>
    /// Sets whether the element receives pointer input. With <c>false</c>, clicks go to whatever is behind it, which
    /// suits decorative overlays.
    /// </summary>
    public static T IsHitTestVisible<T>(this T element, bool isHitTestVisible = true) where T : UIElement =>
        element.Set(UIElement.IsHitTestVisibleProperty, isHitTestVisible);

    /// <summary>Clips the element's children to its bounds, including the rounded corners of a <see cref="Border"/>.</summary>
    public static T ClipToBounds<T>(this T element, bool clip = true) where T : UIElement => element.Set(UIElement.ClipToBoundsProperty, clip);

    /// <summary>
    /// Sets whether the layout of this element and its descendants snaps to whole pixels for crisp edges. Windows turn
    /// it on for their content by default.
    /// </summary>
    public static T UseLayoutRounding<T>(this T element, bool useLayoutRounding = true) where T : UIElement =>
        element.Set(UIElement.UseLayoutRoundingProperty, useLayoutRounding);

    #endregion

    #region Styles

    /// <summary>Applies an explicit style. It takes precedence over keyed and implicit styles.</summary>
    public static T Style<T>(this T element, Style? style) where T : UIElement
    {
        element.Style = style;
        return element;
    }

    /// <summary>
    /// Applies the style registered under <paramref name="styleKey"/>, such as a typography key. The style is looked
    /// up in the element's and its ancestors' styles, then the global styles, then the theme.
    /// </summary>
    public static T StyleKey<T>(this T element, string? styleKey) where T : UIElement => element.Set(UIElement.StyleKeyProperty, styleKey);

    /// <summary>Adds styles to <see cref="UIElement.Styles"/>. They apply to the element and its descendants.</summary>
    public static T Styles<T>(this T element, params Style[] styles) where T : UIElement
    {
        element.Styles.AddRange(styles);
        return element;
    }

    #endregion

    #region Attached layout properties

    /// <summary>Places the element in <paramref name="row"/> (0-based) of its parent <see cref="Grid"/>.</summary>
    public static T Row<T>(this T element, int row) where T : UIElement => element.Set(Grid.RowProperty, row);

    /// <summary>Places the element in <paramref name="column"/> (0-based) of its parent <see cref="Grid"/>.</summary>
    public static T Column<T>(this T element, int column) where T : UIElement => element.Set(Grid.ColumnProperty, column);

    /// <summary>Makes the element span <paramref name="span"/> rows of its parent <see cref="Grid"/>.</summary>
    public static T RowSpan<T>(this T element, int span) where T : UIElement => element.Set(Grid.RowSpanProperty, span);

    /// <summary>Makes the element span <paramref name="span"/> columns of its parent <see cref="Grid"/>.</summary>
    public static T ColumnSpan<T>(this T element, int span) where T : UIElement => element.Set(Grid.ColumnSpanProperty, span);

    /// <summary>Places the element in a cell of its parent <see cref="Grid"/>, optionally spanning several rows and columns.</summary>
    public static T Cell<T>(this T element, int row, int column, int rowSpan = 1, int columnSpan = 1) where T : UIElement =>
        element.Row(row).Column(column).RowSpan(rowSpan).ColumnSpan(columnSpan);

    /// <summary>Docks the element to an edge of its parent <see cref="DockPanel"/>.</summary>
    public static T Dock<T>(this T element, Dock dock) where T : UIElement => element.Set(DockPanel.DockProperty, dock);

    /// <summary>Sets the distance between the left edges of the element and its parent <see cref="Canvas"/>.</summary>
    public static T CanvasLeft<T>(this T element, float left) where T : UIElement => element.Set(Canvas.LeftProperty, left);

    /// <summary>Sets the distance between the top edges of the element and its parent <see cref="Canvas"/>.</summary>
    public static T CanvasTop<T>(this T element, float top) where T : UIElement => element.Set(Canvas.TopProperty, top);

    /// <summary>Sets the distance between the right edges of the element and its parent <see cref="Canvas"/>. It is used when no left is set.</summary>
    public static T CanvasRight<T>(this T element, float right) where T : UIElement => element.Set(Canvas.RightProperty, right);

    /// <summary>Sets the distance between the bottom edges of the element and its parent <see cref="Canvas"/>. It is used when no top is set.</summary>
    public static T CanvasBottom<T>(this T element, float bottom) where T : UIElement => element.Set(Canvas.BottomProperty, bottom);

    /// <summary>Positions the element at (<paramref name="left"/>, <paramref name="top"/>) in its parent <see cref="Canvas"/>.</summary>
    public static T CanvasPosition<T>(this T element, float left, float top) where T : UIElement => element.CanvasLeft(left).CanvasTop(top);

    #endregion

    #region Transforms

    /// <summary>
    /// Sets the layout <see cref="VisualNode.Transform"/>. Layout takes it into account: the element gets the space of
    /// its transformed bounds. Replaces any previous transform.
    /// </summary>
    public static T Transform<T>(this T node, Matrix3x2 transform) where T : VisualNode => node.Set(VisualNode.TransformProperty, transform);

    /// <summary>Sets the origin of <see cref="VisualNode.Transform"/>, relative to the element's size; (0.5, 0.5) is the center.</summary>
    public static T TransformOrigin<T>(this T node, float x, float y) where T : VisualNode => node.Set(VisualNode.TransformOriginProperty, new Point(x, y));

    /// <summary>Sets the origin of <see cref="VisualNode.Transform"/>, relative to the element's size; (0.5, 0.5) is the center.</summary>
    public static T TransformOrigin<T>(this T node, Point origin) where T : VisualNode => node.Set(VisualNode.TransformOriginProperty, origin);

    /// <summary>Sets the layout transform to a uniform scale, replacing any previous transform.</summary>
    public static T Scale<T>(this T node, float scale) where T : VisualNode => node.Transform(Matrix3x2.CreateScale(scale));

    /// <summary>Sets the layout transform to a scale, replacing any previous transform.</summary>
    public static T Scale<T>(this T node, float scaleX, float scaleY) where T : VisualNode => node.Transform(Matrix3x2.CreateScale(scaleX, scaleY));

    /// <summary>Sets the layout transform to a rotation by <paramref name="degrees"/> (clockwise) around the transform origin.</summary>
    public static T Rotate<T>(this T node, float degrees) where T : VisualNode => node.Transform(Matrix3x2.CreateRotation(ToRadians(degrees)));

    /// <summary>Sets the layout transform to a rotation by <paramref name="degrees"/> (clockwise) around the element's center.</summary>
    public static T RotateCenter<T>(this T node, float degrees) where T : VisualNode => node.TransformOrigin(0.5f, 0.5f).Rotate(degrees);

    /// <summary>
    /// Sets the <see cref="VisualNode.RenderTransform"/>, which applies only when drawing, after layout. Neighbors don't
    /// move, which makes it the right choice for animations. Replaces any previous render transform.
    /// </summary>
    public static T RenderTransform<T>(this T node, Matrix3x2 transform) where T : VisualNode => node.Set(VisualNode.RenderTransformProperty, transform);

    /// <summary>Sets the origin of <see cref="VisualNode.RenderTransform"/>, relative to the element's size. The default (0.5, 0.5) is the center.</summary>
    public static T RenderTransformOrigin<T>(this T node, float x, float y) where T : VisualNode =>
        node.Set(VisualNode.RenderTransformOriginProperty, new Point(x, y));

    /// <summary>Sets the origin of <see cref="VisualNode.RenderTransform"/>, relative to the element's size. The default (0.5, 0.5) is the center.</summary>
    public static T RenderTransformOrigin<T>(this T node, Point origin) where T : VisualNode => node.Set(VisualNode.RenderTransformOriginProperty, origin);

    /// <summary>Sets the render transform to a uniform scale around the render transform origin.</summary>
    public static T RenderScale<T>(this T node, float scale) where T : VisualNode => node.RenderTransform(Matrix3x2.CreateScale(scale));

    /// <summary>Sets the render transform to a scale around the render transform origin.</summary>
    public static T RenderScale<T>(this T node, float scaleX, float scaleY) where T : VisualNode => node.RenderTransform(Matrix3x2.CreateScale(scaleX, scaleY));

    /// <summary>Sets the render transform to a rotation by <paramref name="degrees"/> (clockwise) around the render transform origin.</summary>
    public static T RenderRotate<T>(this T node, float degrees) where T : VisualNode => node.RenderTransform(Matrix3x2.CreateRotation(ToRadians(degrees)));

    /// <summary>Sets the render transform to a skew by the given angles in degrees.</summary>
    public static T RenderSkew<T>(this T node, float skewXDegrees, float skewYDegrees) where T : VisualNode =>
        node.RenderTransform(Matrix3x2.CreateSkew(ToRadians(skewXDegrees), ToRadians(skewYDegrees)));

    /// <summary>Sets the render transform to a translation, an offset in pixels.</summary>
    public static T RenderTranslate<T>(this T node, float x, float y) where T : VisualNode => node.RenderTransform(Matrix3x2.CreateTranslation(x, y));

    private static float ToRadians(float degrees) => degrees * (MathF.PI / 180f);

    #endregion

    #region Events

    /// <summary>Handles <see cref="UIElement.PointerPressed"/>, raised when a mouse button is pressed over the element.</summary>
    public static T OnPointerPressed<T>(this T element, EventHandler<PointerEventArgs> handler) where T : UIElement
    {
        element.PointerPressed += handler;
        return element;
    }

    /// <summary>Handles <see cref="UIElement.PointerReleased"/>, raised when a mouse button is released over the element.</summary>
    public static T OnPointerReleased<T>(this T element, EventHandler<PointerEventArgs> handler) where T : UIElement
    {
        element.PointerReleased += handler;
        return element;
    }

    /// <summary>Handles <see cref="UIElement.PointerMoved"/>, raised when the pointer moves over the element.</summary>
    public static T OnPointerMoved<T>(this T element, EventHandler<PointerEventArgs> handler) where T : UIElement
    {
        element.PointerMoved += handler;
        return element;
    }

    /// <summary>Handles <see cref="UIElement.PointerEntered"/>, raised when the pointer moves onto the element.</summary>
    public static T OnPointerEntered<T>(this T element, EventHandler<PointerEventArgs> handler) where T : UIElement
    {
        element.PointerEntered += handler;
        return element;
    }

    /// <summary>Handles <see cref="UIElement.PointerExited"/>, raised when the pointer leaves the element.</summary>
    public static T OnPointerExited<T>(this T element, EventHandler<PointerEventArgs> handler) where T : UIElement
    {
        element.PointerExited += handler;
        return element;
    }

    /// <summary>Handles <see cref="UIElement.PointerWheel"/>, raised when the mouse wheel turns over the element.</summary>
    public static T OnPointerWheel<T>(this T element, EventHandler<PointerWheelEventArgs> handler) where T : UIElement
    {
        element.PointerWheel += handler;
        return element;
    }

    /// <summary>Handles <see cref="UIElement.KeyDown"/>, raised when a key is pressed while the element or a descendant has focus.</summary>
    public static T OnKeyDown<T>(this T element, EventHandler<KeyEventArgs> handler) where T : UIElement
    {
        element.KeyDown += handler;
        return element;
    }

    /// <summary>Handles <see cref="UIElement.KeyUp"/>, raised when a key is released while the element or a descendant has focus.</summary>
    public static T OnKeyUp<T>(this T element, EventHandler<KeyEventArgs> handler) where T : UIElement
    {
        element.KeyUp += handler;
        return element;
    }

    /// <summary>Handles <see cref="UIElement.TextInput"/>, raised when text is typed while the element or a descendant has focus.</summary>
    public static T OnTextInput<T>(this T element, EventHandler<TextInputEventArgs> handler) where T : UIElement
    {
        element.TextInput += handler;
        return element;
    }

    /// <summary>Handles <see cref="UIElement.GotFocus"/>, raised when the element receives keyboard focus.</summary>
    public static T OnGotFocus<T>(this T element, EventHandler handler) where T : UIElement
    {
        element.GotFocus += handler;
        return element;
    }

    /// <summary>Runs <paramref name="action"/> when the element receives keyboard focus (<see cref="UIElement.GotFocus"/>).</summary>
    public static T OnGotFocus<T>(this T element, Action action) where T : UIElement => element.OnGotFocus(ToHandler(action));

    /// <summary>Handles <see cref="UIElement.LostFocus"/>, raised when the element loses keyboard focus.</summary>
    public static T OnLostFocus<T>(this T element, EventHandler handler) where T : UIElement
    {
        element.LostFocus += handler;
        return element;
    }

    /// <summary>Runs <paramref name="action"/> when the element loses keyboard focus (<see cref="UIElement.LostFocus"/>).</summary>
    public static T OnLostFocus<T>(this T element, Action action) where T : UIElement => element.OnLostFocus(ToHandler(action));

    /// <summary>
    /// Handles <see cref="VisualNode.AttachedToVisualTree"/>, raised when the element becomes part of a displayed tree
    /// (a window's content). Use it to start work that needs the element on screen.
    /// </summary>
    public static T OnAttachedToVisualTree<T>(this T node, EventHandler handler) where T : VisualNode
    {
        node.AttachedToVisualTree += handler;
        return node;
    }

    /// <summary>Runs <paramref name="action"/> when the element becomes part of a displayed tree (<see cref="VisualNode.AttachedToVisualTree"/>).</summary>
    public static T OnAttachedToVisualTree<T>(this T node, Action action) where T : VisualNode => node.OnAttachedToVisualTree(ToHandler(action));

    /// <summary>
    /// Handles <see cref="VisualNode.DetachedFromVisualTree"/>, raised when the element is removed from the displayed
    /// tree. Use it to stop work started when it was attached.
    /// </summary>
    public static T OnDetachedFromVisualTree<T>(this T node, EventHandler handler) where T : VisualNode
    {
        node.DetachedFromVisualTree += handler;
        return node;
    }

    /// <summary>Runs <paramref name="action"/> when the element is removed from the displayed tree (<see cref="VisualNode.DetachedFromVisualTree"/>).</summary>
    public static T OnDetachedFromVisualTree<T>(this T node, Action action) where T : VisualNode => node.OnDetachedFromVisualTree(ToHandler(action));

    /// <summary>
    /// Calls <paramref name="handler"/> with the old and new value whenever <paramref name="property"/> of the element
    /// changes: <c>.OnPropertyChanged(UIElement.IsHoveredProperty, (s, was, isNow) =&gt; ...)</c>.
    /// </summary>
    public static T OnPropertyChanged<T, TValue>(this T element, BindableProperty<TValue> property, PropertyChangedCallback<TValue> handler)
        where T : BindableObject
    {
        element.Subscribe(property, handler);
        return element;
    }

    /// <summary>Adapts an <see cref="Action"/> to an <see cref="EventHandler"/>, for the <c>On{Event}(Action)</c> overloads.</summary>
    internal static EventHandler ToHandler(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return (_, _) => action();
    }

    /// <summary>Adapts an <see cref="Action{T}"/> to an <see cref="EventHandler{TEventArgs}"/>, for the <c>On{Event}(Action&lt;T&gt;)</c> overloads.</summary>
    internal static EventHandler<TArgs> ToHandler<TArgs>(Action<TArgs> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return (_, args) => action(args);
    }

    #endregion

    #region Bindings

    /// <summary>
    /// Binds <paramref name="property"/> to a value computed from <paramref name="source"/>. With a
    /// <paramref name="setter"/>, the binding is two-way: changes of the property are written back.
    /// </summary>
    /// <remarks>
    /// For a simple getter such as <c>vm =&gt; vm.Title</c>, the binding updates when <paramref name="source"/> raises
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for that property. It replaces any existing binding of the
    /// property.
    /// </remarks>
    /// <param name="target">The element to bind.</param>
    /// <param name="property">The property to bind.</param>
    /// <param name="source">The object providing the value, usually a view model.</param>
    /// <param name="getter">Computes the property value from the source.</param>
    /// <param name="setter">Writes changes of the property back to the source. <c>null</c> makes the binding one-way.</param>
    /// <param name="updateSourceTrigger">When changes are written back to the source.</param>
    /// <param name="options">An optional binding mode, fallback value and null value.</param>
    /// <param name="getterExpression">Filled in by the compiler; don't pass it.</param>
    public static T Bind<T, TValue, TSource>(
        this T target,
        BindableProperty<TValue> property,
        TSource source,
        Func<TSource, TValue> getter,
        Action<TSource, TValue>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        BindingOptions<TValue>? options = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : BindableObject
        where TSource : class
    {
        target.SetBinding(property, source, getter, setter, updateSourceTrigger, options, getterExpression);
        return target;
    }

    /// <summary>
    /// Binds <paramref name="property"/> two-way to <paramref name="source"/>. It reads with <paramref name="getter"/>
    /// and writes changes back with <paramref name="setter"/>. It is the same as <c>Bind</c> with a setter.
    /// </summary>
    /// <param name="target">The element to bind.</param>
    /// <param name="property">The property to bind.</param>
    /// <param name="source">The object providing the value, usually a view model.</param>
    /// <param name="getter">Computes the property value from the source.</param>
    /// <param name="setter">Writes changes of the property back to the source.</param>
    /// <param name="updateSourceTrigger">When changes are written back to the source.</param>
    /// <param name="options">An optional binding mode, fallback value and null value.</param>
    /// <param name="getterExpression">Filled in by the compiler; don't pass it.</param>
    public static T BindTwoWay<T, TValue, TSource>(
        this T target,
        BindableProperty<TValue> property,
        TSource source,
        Func<TSource, TValue> getter,
        Action<TSource, TValue> setter,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        BindingOptions<TValue>? options = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : BindableObject
        where TSource : class
    {
        ArgumentNullException.ThrowIfNull(setter);
        target.SetBinding(property, source, getter, setter, updateSourceTrigger, options, getterExpression);
        return target;
    }

    /// <summary>
    /// Binds <paramref name="property"/> to a value computed from the element's
    /// <see cref="BindableObject.DataContext"/>, which is usually inherited from an ancestor. The binding follows
    /// changes of the DataContext, and a <paramref name="setter"/> makes it two-way. Write out the type of the lambda
    /// parameter so the data context type can be inferred:
    /// <c>.Bind(TextBlock.TextProperty, (MainViewModel vm) =&gt; vm.Title)</c>.
    /// </summary>
    /// <param name="target">The element to bind.</param>
    /// <param name="property">The property to bind.</param>
    /// <param name="getter">Computes the property value from the data context.</param>
    /// <param name="setter">Writes changes of the property back to the data context. <c>null</c> makes the binding one-way.</param>
    /// <param name="updateSourceTrigger">When changes are written back to the data context.</param>
    /// <param name="options">An optional binding mode, fallback value (used while there is no data context) and null value.</param>
    /// <param name="getterExpression">Filled in by the compiler; don't pass it.</param>
    public static T Bind<T, TValue, TDataContext>(
        this T target,
        BindableProperty<TValue> property,
        Func<TDataContext, TValue> getter,
        Action<TDataContext, TValue>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        BindingOptions<TValue>? options = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : BindableObject
        where TDataContext : class
    {
        target.SetBinding(property, getter, setter, updateSourceTrigger, options, getterExpression);
        return target;
    }

    /// <summary>
    /// Binds <paramref name="property"/> two-way to the element's <see cref="BindableObject.DataContext"/>. It is the
    /// same as <c>Bind</c> with a setter:
    /// <c>.BindTwoWay(TextBox.TextProperty, (MainViewModel vm) =&gt; vm.Name, (vm, value) =&gt; vm.Name = value)</c>.
    /// </summary>
    /// <param name="target">The element to bind.</param>
    /// <param name="property">The property to bind.</param>
    /// <param name="getter">Computes the property value from the data context.</param>
    /// <param name="setter">Writes changes of the property back to the data context.</param>
    /// <param name="updateSourceTrigger">When changes are written back to the data context.</param>
    /// <param name="options">An optional binding mode, fallback value and null value.</param>
    /// <param name="getterExpression">Filled in by the compiler; don't pass it.</param>
    public static T BindTwoWay<T, TValue, TDataContext>(
        this T target,
        BindableProperty<TValue> property,
        Func<TDataContext, TValue> getter,
        Action<TDataContext, TValue> setter,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        BindingOptions<TValue>? options = null,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : BindableObject
        where TDataContext : class
    {
        ArgumentNullException.ThrowIfNull(setter);
        target.SetBinding(property, getter, setter, updateSourceTrigger, options, getterExpression);
        return target;
    }

    /// <summary>
    /// Binds <paramref name="property"/> one-way to a value computed from several sources. It is re-evaluated whenever
    /// any of them raises <see cref="INotifyPropertyChanged.PropertyChanged"/>:
    /// <c>.BindMulti(TextBlock.TextProperty, () =&gt; $"{person.Name} ({settings.Unit})", person, settings)</c>.
    /// </summary>
    public static T BindMulti<T, TValue>(this T target, BindableProperty<TValue> property, Func<TValue> getter, params INotifyPropertyChanged[] sources)
        where T : BindableObject
    {
        target.SetMultiBinding(property, getter, sources);
        return target;
    }

    /// <summary>Binds <see cref="UIElement.IsEnabled"/> to a value of <paramref name="source"/>.</summary>
    public static T BindIsEnabled<T, TSource>(this T element, TSource source, Func<TSource, bool> getter, Action<TSource, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : UIElement where TSource : class =>
        element.BindToSource(UIElement.IsEnabledProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds <see cref="UIElement.IsEnabled"/> to a value of the <see cref="BindableObject.DataContext"/>.</summary>
    public static T BindIsEnabled<T, TDataContext>(this T element, Func<TDataContext, bool> getter, Action<TDataContext, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : UIElement where TDataContext : class =>
        element.BindToDataContext(UIElement.IsEnabledProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Shows the element while a value of <paramref name="source"/> is <c>true</c>, and collapses it otherwise.</summary>
    public static T BindIsVisible<T, TSource>(this T element, TSource source, Func<TSource, bool> getter, Action<TSource, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : UIElement where TSource : class =>
        element.BindToSource(UIElement.VisibilityProperty, source, s => ToVisibility(getter(s)), FromVisibility(setter), updateSourceTrigger, getterExpression);

    /// <summary>Shows the element while a value of the <see cref="BindableObject.DataContext"/> is <c>true</c>, and collapses it otherwise.</summary>
    public static T BindIsVisible<T, TDataContext>(this T element, Func<TDataContext, bool> getter, Action<TDataContext, bool>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : UIElement where TDataContext : class =>
        element.BindToDataContext(UIElement.VisibilityProperty, (TDataContext s) => ToVisibility(getter(s)), FromVisibility(setter), updateSourceTrigger, getterExpression);

    /// <summary>Binds <see cref="UIElement.Opacity"/> to a value of <paramref name="source"/>.</summary>
    public static T BindOpacity<T, TSource>(this T element, TSource source, Func<TSource, float> getter, Action<TSource, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : UIElement where TSource : class =>
        element.BindToSource(UIElement.OpacityProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds <see cref="UIElement.Opacity"/> to a value of the <see cref="BindableObject.DataContext"/>.</summary>
    public static T BindOpacity<T, TDataContext>(this T element, Func<TDataContext, float> getter, Action<TDataContext, float>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : UIElement where TDataContext : class =>
        element.BindToDataContext(UIElement.OpacityProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds <see cref="UIElement.StyleKey"/> to a value of <paramref name="source"/>, for example to switch typography.</summary>
    public static T BindStyleKey<T, TSource>(this T element, TSource source, Func<TSource, string?> getter, Action<TSource, string?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : UIElement where TSource : class =>
        element.BindToSource(UIElement.StyleKeyProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds <see cref="UIElement.StyleKey"/> to a value of the <see cref="BindableObject.DataContext"/>.</summary>
    public static T BindStyleKey<T, TDataContext>(this T element, Func<TDataContext, string?> getter, Action<TDataContext, string?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : UIElement where TDataContext : class =>
        element.BindToDataContext(UIElement.StyleKeyProperty, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>
    /// Binds the <see cref="BindableObject.DataContext"/> to a value of <paramref name="source"/>, such as a child view
    /// model, so the DataContext bindings of the element and its descendants follow it.
    /// </summary>
    public static T BindDataContext<T, TSource>(this T element, TSource source, Func<TSource, object?> getter, Action<TSource, object?>? setter = null,
        UpdateSourceTrigger updateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        [CallerArgumentExpression(nameof(getter))] string? getterExpression = null)
        where T : BindableObject where TSource : class =>
        element.BindToSource(BindableObject.DataContextProperty, source, getter, setter, updateSourceTrigger, getterExpression);

    /// <summary>Binds <paramref name="property"/> to <paramref name="source"/> and returns the target (the implementation of the <c>Bind{Property}</c> methods).</summary>
    internal static T BindToSource<T, TValue, TSource>(
        this T target,
        BindableProperty<TValue> property,
        TSource source,
        Func<TSource, TValue> getter,
        Action<TSource, TValue>? setter,
        UpdateSourceTrigger updateSourceTrigger,
        string? getterExpression)
        where T : BindableObject
        where TSource : class
    {
        target.SetBinding(property, source, getter, setter, updateSourceTrigger, null, getterExpression);
        return target;
    }

    /// <summary>Binds <paramref name="property"/> to the DataContext and returns the target (the implementation of the <c>Bind{Property}&lt;TDataContext&gt;</c> methods).</summary>
    internal static T BindToDataContext<T, TValue, TDataContext>(
        this T target,
        BindableProperty<TValue> property,
        Func<TDataContext, TValue> getter,
        Action<TDataContext, TValue>? setter,
        UpdateSourceTrigger updateSourceTrigger,
        string? getterExpression)
        where T : BindableObject
        where TDataContext : class
    {
        target.SetBinding(property, getter, setter, updateSourceTrigger, null, getterExpression);
        return target;
    }

    private static Visibility ToVisibility(bool visible) =>
        visible ? Core.Primitives.Visibility.Visible : Core.Primitives.Visibility.Collapsed;

    // Adapts a bool setter to the Visibility property: only Visible counts as visible.
    private static Action<TSource, Visibility>? FromVisibility<TSource>(Action<TSource, bool>? setter) =>
        setter == null ? null : (s, visibility) => setter(s, visibility == Core.Primitives.Visibility.Visible);

    #endregion
}
