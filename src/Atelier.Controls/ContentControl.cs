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
public class ContentControl : Control
{
    public static readonly BindableProperty<object?> ContentProperty =
        BindableProperty.Register<ContentControl, object?>(
            nameof(Content),
            null,
            (s, o, n) => ((ContentControl)s).OnContentChanged(o, n)
        );

    public static readonly BindableProperty<Func<object?, UIElement?>?> ContentTemplateProperty =
        BindableProperty.Register<ContentControl, Func<object?, UIElement?>?>(
            nameof(ContentTemplate),
            null,
            (s, o, n) => ((ContentControl)s).OnContentTemplateChanged(o, n)
        );

    public static readonly BindableProperty<IViewLocator?> ViewLocatorProperty =
        BindableProperty.Register<ContentControl, IViewLocator?>(
            nameof(ViewLocator),
            null,
            (s, o, n) => ((ContentControl)s).OnViewLocatorChanged(o, n)
        );

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
    /// Takes precedence over <see cref="ViewLocator"/> when specified.
    /// </summary>
    public Func<object?, UIElement?>? ContentTemplate
    {
        get => GetValue(ContentTemplateProperty);
        set => SetValue(ContentTemplateProperty, value);
    }

    /// <summary>
    /// Gets or sets the local <see cref="IViewLocator"/> used to resolve views for ViewModel content.
    /// If null, falls back to <see cref="ViewLocator.Current"/>.
    /// </summary>
    public IViewLocator? ViewLocator
    {
        get => GetValue(ViewLocatorProperty);
        set => SetValue(ViewLocatorProperty, value);
    }

    protected UIElement? _currentView;

    /// <summary>
    /// Gets the visual element currently instantiated and mounted to display the content.
    /// </summary>
    public UIElement? CurrentView => _currentView;

    public ContentControl()
    {
    }

    public ContentControl(object? content) : this()
    {
        Content = content;
    }

    protected virtual void OnContentChanged(object? oldContent, object? newContent)
    {
        UpdateContentDisplay();
    }

    protected virtual void OnContentTemplateChanged(Func<object?, UIElement?>? oldTemplate, Func<object?, UIElement?>? newTemplate)
    {
        UpdateContentDisplay();
    }

    protected virtual void OnViewLocatorChanged(IViewLocator? oldLocator, IViewLocator? newLocator)
    {
        UpdateContentDisplay();
    }

    /// <summary>
    /// Resolves or creates a visual element for the given content using the explicit template, direct UIElement casting,
    /// or configured <see cref="IViewLocator"/>.
    /// </summary>
    public virtual UIElement? ResolveContentView(object? content)
    {
        if (content == null) return null;

        UIElement? view = null;

        // 1. Explicit ContentTemplate takes highest precedence
        if (ContentTemplate != null)
        {
            view = ContentTemplate(content);
        }
        // 2. Direct UIElement content
        else if (content is UIElement ui)
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

        if (view != null)
        {
            // If the view represents a data/ViewModel object (and is not the raw element itself),
            // assign its DataContext to the content so child bindings bind to the ViewModel.
            if (!ReferenceEquals(view, content))
            {
                view.DataContext = content;
            }
        }

        return view;
    }

    /// <summary>
    /// Updates the child view matching the current <see cref="Content"/>.
    /// </summary>
    protected virtual void UpdateContentDisplay()
    {
        if (_currentView != null)
        {
            RemoveChild(_currentView);
            _currentView = null;
        }

        var content = Content;
        if (content == null)
        {
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }

        var view = ResolveContentView(content);
        if (view != null)
        {
            _currentView = view;
            AddChild(view);
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>
    /// Creates a fallback view when neither template nor view locator resolves a view for content.
    /// </summary>
    protected virtual UIElement CreateDefaultFallbackView(object content)
    {
        return new TextBlock(content?.ToString() ?? string.Empty);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var padding = Padding;
        if (_currentView != null && _currentView.Visibility != Visibility.Collapsed)
        {
            _currentView.Measure(availableSize.Deflate(padding));
            return _currentView.DesiredSize.Inflate(padding.Horizontal, padding.Vertical);
        }

        return new Size(padding.Horizontal, padding.Vertical);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var padding = Padding;
        if (_currentView != null && _currentView.Visibility != Visibility.Collapsed)
        {
            _currentView.Arrange(new Rect(Point.Zero, finalSize).Deflate(padding));
        }

        return finalSize;
    }
}
