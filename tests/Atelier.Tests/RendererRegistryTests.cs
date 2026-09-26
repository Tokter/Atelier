using System;
using Xunit;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Theming;

namespace Atelier.Tests;

public class RendererRegistryTests
{
    private sealed class PanelRenderer : ControlRenderer<Panel> { }

    private sealed class StackPanelRenderer : ControlRenderer<StackPanel> { }

    private sealed class DerivedStackPanel : StackPanel { }

    [Fact]
    public void Lookup_UsesTheClosestRegisteredBaseType()
    {
        var registry = new RendererRegistry();
        var panelRenderer = new PanelRenderer();
        var stackRenderer = new StackPanelRenderer();
        registry.Register(panelRenderer);
        registry.Register(stackRenderer);

        Assert.Same(stackRenderer, registry.GetRenderer(typeof(DerivedStackPanel)));
        Assert.Same(stackRenderer, registry.GetRenderer(typeof(DerivedStackPanel))); // cached
        Assert.Same(panelRenderer, registry.GetRenderer(typeof(Grid)));
        Assert.Null(registry.GetRenderer(typeof(Border)));
        Assert.Same(stackRenderer, registry.GetRenderer<StackPanel>());
    }

    [Fact]
    public void Registering_ReplacesCachedResults_IncludingCachedMisses()
    {
        var registry = new RendererRegistry();
        Assert.Null(registry.GetRenderer(typeof(DerivedStackPanel)));

        var panelRenderer = new PanelRenderer();
        registry.Register(panelRenderer);
        Assert.Same(panelRenderer, registry.GetRenderer(typeof(DerivedStackPanel)));

        var stackRenderer = new StackPanelRenderer();
        registry.Register(stackRenderer);
        Assert.Same(stackRenderer, registry.GetRenderer(typeof(DerivedStackPanel)));
    }

    [Fact]
    public void RepeatedLookups_DoNotAllocate()
    {
        var registry = new RendererRegistry();
        registry.Register(new PanelRenderer());
        Type found = typeof(DerivedStackPanel), missing = typeof(Border);
        registry.GetRenderer(found);
        registry.GetRenderer(missing);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
        {
            registry.GetRenderer(found);
            registry.GetRenderer(missing);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
