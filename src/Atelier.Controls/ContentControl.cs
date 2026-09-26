using System;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Core.ViewResolution;

namespace Atelier.Controls;

/// <summary>
/// A control that displays a single content item, automatically resolving and mounting matching
/// views for ViewModels via an <see cref="IViewLocator"/> or <see cref="ContentTemplate"/>.
/// </summary>
/// <remarks>
/// A <see cref="UIElement"/> content is shown directly. Any other content is shown by the view that
/// <see cref="ContentTemplate"/> or the view locator creates for it (with the content as its
/// <see cref="BindableObject.DataContext"/>), or else as text. The view is placed inside <see cref="Control.Padding"/>
/// according to <see cref="HorizontalContentAlignment"/> and <see cref="VerticalContentAlignment"/>.
/// </remarks>
public class ContentControl : Control
{
    /// <summary>Identifies the <see cref="Content"/> property.</summary>
    public static readonly BindableProperty<object?> ContentProperty =
        BindableProperty.Register<ContentControl, object?>(
            nameof(Content),
            null,
            (s, o, n) => ((ContentControl)s).OnContentChanged(o, n)
        );

    /// <summary>Identifies the <see cref="ContentTemplate"/> property.</summary>
    public static readonly BindableProperty<Func<object?, UIElement?>?> ContentTemplateProperty =
        BindableProperty.Register<ContentControl, Func<object?, UIElement?>?>(
            nameof(ContentTemplate),
            null,
            (s, o, n) => ((ContentControl)s).OnContentTemplateChanged(o, n)
        );

    /// <summary>Identifies the <see cref="ViewLocator"/> property.</summary>
    public static readonly BindableProperty<IViewLocator?> ViewLocatorProperty =
        BindableProperty.Register<ContentControl, IViewLocator?>(
            nameof(ViewLocator),
            null,
            (s, o, n) => ((ContentControl)s).OnViewLocatorChanged(o, n)
        );

    /// <summary>Identifies the <see cref="HorizontalContentAlignment"/> property.</summary>
    public static readonly BindableProperty<HorizontalAlignment> HorizontalContentAlignmentProperty =
        BindableProperty.Register<ContentControl, HorizontalAlignment>(
            nameof(HorizontalContentAlignment), HorizontalAlignment.Stretch, options: PropertyOptions.AffectsArrange);

    /// <summary>Identifies the <see cref="VerticalContentAlignment"/> property.</summary>
    public static readonly BindableProperty<VerticalAlignment> VerticalContentAlignmentProperty =
        BindableProperty.Register<ContentControl, VerticalAlignment>(
            nameof(VerticalContentAlignment), VerticalAlignment.Stretch, options: PropertyOptions.AffectsArrange);

    /// <summary>
    /// Gets or sets the data, ViewModel, or UIElement displayed by this control.
    /// </summary>
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Gets or sets an explicit data template delegate used to generate a view for the content.
    /// Takes precedence over <see cref="ViewLocator"/> when specified; when it returns <c>null</c>, the content is
    /// resolved as if no template were set.
    /// </summary>
    public Func<object?, UIElement?>? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the local <see cref="IViewLocator"/> used to resolve views for ViewModel content.
    /// If null, falls back to <see cref="Atelier.Core.ViewResolution.ViewLocator.Current"/>.
    /// </summary>
    public IViewLocator? ViewLocator
    {
        get => GetValue(ViewLocatorProperty);
        set => SetValue(ViewLocatorProperty, value);
    }

    /// <summary>
    /// Gets or sets how the view is placed horizontally inside the padded area. <see cref="HorizontalAlignment.Stretch"/>
    /// (the default) gives it the full width; other values give it its desired width. The view's own
    /// <see cref="UIElement.HorizontalAlignment"/> then applies within that slot.
    /// </summary>
    public HorizontalAlignment HorizontalContentAlignment
    {
        get => GetValue(HorizontalContentAlignmentProperty);
        set => SetValue(HorizontalContentAlignmentProperty, value);
    }

    /// <summary>
    /// Gets or sets how the view is placed vertically inside the padded area. <see cref="VerticalAlignment.Stretch"/>
    /// (the default) gives it the full height; other values give it its desired height.
    /// </summary>
    public VerticalAlignment VerticalContentAlignment
    {
        get => GetValue(VerticalContentAlignmentProperty);
        set => SetValue(VerticalContentAlignmentProperty, value);
    }

    /// <summary>
    /// Gets the visual element currently instantiated and mounted to display the content.
    /// </summary>
    public UIElement? CurrentView { get; private set; }

    /// <summary>Gets the content <see cref="CurrentView"/> was resolved for.</summary>
    protected object? CurrentViewContent { get; private set; }

    /// <summary>
    /// Records <paramref name="view"/> as <see cref="CurrentView"/>, resolved for <paramref name="content"/>. Derived
    /// classes that call this are responsible for adding the view as a child (and releasing the previous one).
    /// </summary>
    /// <param name="view">The view, or <c>null</c>.</param>
    /// <param name="content">The content the view shows.</param>
    protected void SetCurrentView(UIElement? view, object? content)
    {
        CurrentView = view;
        CurrentViewContent = view != null ? content : null;
    }

    /// <summary>Initializes a new, empty <see cref="ContentControl"/>.</summary>
    public ContentControl()
    {
    }

    /// <summary>Initializes a new <see cref="ContentControl"/> showing <paramref name="content"/>.</summary>
    /// <param name="content">The content to show.</param>
    public ContentControl(object? content) : this()
    {
        Content = content;
    }

    /// <summary>Called when <see cref="Content"/> changes. The base implementation calls <see cref="UpdateContentDisplay"/>.</summary>
    /// <param name="oldContent">The previous content.</param>
    /// <param name="newContent">The new content.</param>
    protected virtual void OnContentChanged(object? oldContent, object? newContent)
    {
        UpdateContentDisplay();
    }

    /// <summary>Called when <see cref="ContentTemplate"/> changes. The base implementation calls <see cref="UpdateContentDisplay"/>.</summary>
    /// <param name="oldTemplate">The previous template.</param>
    /// <param name="newTemplate">The new template.</param>
    protected virtual void OnContentTemplateChanged(Func<object?, UIElement?>? oldTemplate, Func<object?, UIElement?>? newTemplate)
    {
        UpdateContentDisplay();
    }

    /// <summary>Called when <see cref="ViewLocator"/> changes. The base implementation calls <see cref="UpdateContentDisplay"/>.</summary>
    /// <param name="oldLocator">The previous locator.</param>
    /// <param name="newLocator">The new locator.</param>
    protected virtual void OnViewLocatorChanged(IViewLocator? oldLocator, IViewLocator? newLocator)
    {
        UpdateContentDisplay();
    }

    /// <summary>
    /// Resolves or creates a visual element for the given content using the explicit template, direct UIElement casting,
    /// or configured <see cref="IViewLocator"/>, falling back to <see cref="CreateDefaultFallbackView"/>.
    /// </summary>
    /// <remarks>A resolved view other than the content itself gets the content as its local <see cref="BindableObject.DataContext"/>.</remarks>
    /// <param name="content">The content to resolve a view for.</param>
    /// <returns>The view, or <c>null</c> for <c>null</c> content.</returns>
    public virtual UIElement? ResolveContentView(object? content)
    {
        if (content == null) return null;

        // 1. Explicit ContentTemplate takes highest precedence (a null result falls through to the steps below)
        UIElement? view = ContentTemplate?.Invoke(content);

        if (view == null)
        {
            // 2. Direct UIElement content
            if (content is UIElement ui)
            {
                view = ui;
            }
            // 3. View resolution via local or global ViewLocator
            else
            {
                var locator = ViewLocator ?? Atelier.Core.ViewResolution.ViewLocator.Current;
                view = locator?.ResolveView(content);

                // 4. Fallback if no matching view found
                view ??= CreateDefaultFallbackView(content);
            }
        }

        // If the view represents a data/ViewModel object (and is not the raw element itself),
        // assign its DataContext to the content so child bindings bind to the ViewModel.
        if (view != null && !ReferenceEquals(view, content))
        {
            view.DataContext = content;
        }

        return view;
    }

    /// <summary>
    /// Replaces the child view with one matching the current <see cref="Content"/>.
    /// </summary>
    protected virtual void UpdateContentDisplay()
    {
        var oldView = CurrentView;
        if (oldView != null)
        {
            var oldContent = CurrentViewContent;
            SetCurrentView(null, null);
            ReleaseView(oldView, oldContent);
        }

        var content = Content;
        if (content != null)
        {
            var view = ResolveContentView(content);
            if (view != null)
            {
                SetCurrentView(view, content);
                AddChild(view);
            }
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>
    /// Removes a view this control displayed and, if the control set its <see cref="BindableObject.DataContext"/>
    /// (see <see cref="ResolveContentView"/>), clears it so a cached view does not keep the old content alive.
    /// </summary>
    /// <param name="view">The view to remove.</param>
    /// <param name="content">The content the view was resolved for.</param>
    protected void ReleaseView(UIElement view, object? content)
    {
        RemoveChild(view);

        if (content != null && !ReferenceEquals(view, content) && view.HasLocalValue(DataContextProperty)
            && ReferenceEquals(view.DataContext, content))
        {
            view.ClearValue(DataContextProperty);
        }
    }

    /// <summary>
    /// Creates a fallback view when neither template nor view locator resolves a view for content.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <returns>A <see cref="TextBlock"/> showing the content's <see cref="object.ToString"/>.</returns>
    protected virtual UIElement CreateDefaultFallbackView(object content)
    {
        return new TextBlock(content?.ToString() ?? string.Empty);
    }

    /// <summary>
    /// Computes the rectangle a view gets inside <paramref name="contentArea"/> per <see cref="HorizontalContentAlignment"/>
    /// and <see cref="VerticalContentAlignment"/>.
    /// </summary>
    /// <param name="view">The measured view.</param>
    /// <param name="contentArea">The padded content area.</param>
    /// <returns>The slot to arrange the view in.</returns>
    protected Rect GetContentSlot(UIElement view, Rect contentArea)
    {
        var desired = view.DesiredSize;
        float x = contentArea.X, y = contentArea.Y, w = contentArea.Width, h = contentArea.Height;

        var horizontal = HorizontalContentAlignment;
        if (horizontal != HorizontalAlignment.Stretch)
        {
            w = Math.Min(desired.Width, contentArea.Width);
            if (horizontal == HorizontalAlignment.Center) x += (contentArea.Width - w) * 0.5f;
            else if (horizontal == HorizontalAlignment.Right) x += contentArea.Width - w;
        }

        var vertical = VerticalContentAlignment;
        if (vertical != VerticalAlignment.Stretch)
        {
            h = Math.Min(desired.Height, contentArea.Height);
            if (vertical == VerticalAlignment.Center) y += (contentArea.Height - h) * 0.5f;
            else if (vertical == VerticalAlignment.Bottom) y += contentArea.Height - h;
        }

        return new Rect(x, y, w, h);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        var view = CurrentView;
        if (view != null && view.Visibility != Visibility.Collapsed)
        {
            view.Measure(availableSize.Deflate(padding));
            return view.DesiredSize.Inflate(padding.Horizontal, padding.Vertical);
        }

        return new Size(padding.Horizontal, padding.Vertical);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var view = CurrentView;
        if (view != null && view.Visibility != Visibility.Collapsed)
        {
            view.Arrange(GetContentSlot(view, new Rect(Point.Zero, finalSize).Deflate(Padding)));
        }

        return finalSize;
    }
}
