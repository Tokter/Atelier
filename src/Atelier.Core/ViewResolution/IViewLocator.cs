using System;
using Atelier.Core.Tree;

namespace Atelier.Core.ViewResolution;

/// <summary>
/// Defines a contract for resolving and instantiating UIElement views for data objects and ViewModels.
/// </summary>
public interface IViewLocator
{
    /// <summary>
    /// Determines whether this locator can resolve a view for the specified data or ViewModel.
    /// </summary>
    bool CanResolve(object? data);

    /// <summary>
    /// Resolves and instantiates a UIElement view matching the provided data or ViewModel.
    /// Returns null if no matching view could be resolved.
    /// </summary>
    UIElement? ResolveView(object? data);
}
