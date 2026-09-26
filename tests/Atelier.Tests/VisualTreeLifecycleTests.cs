using System;
using System.Collections.Generic;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Styling;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Tests;

public class VisualTreeLifecycleTests
{
    private static List<string> Record(params (string Name, VisualNode Node)[] nodes)
    {
        var log = new List<string>();
        foreach (var (name, node) in nodes)
        {
            node.AttachedToVisualTree += (s, e) => log.Add("+" + name);
            node.DetachedFromVisualTree += (s, e) => log.Add("-" + name);
        }
        return log;
    }

    [Fact]
    public void AttachToHost_RaisesAttachedParentFirst_AndDetachChildrenFirst()
    {
        var root = new StackPanel();
        var child = new StackPanel();
        var leaf = new TextBlock("leaf");
        child.Add(leaf);
        root.Add(child);
        var log = Record(("root", root), ("child", child), ("leaf", leaf));

        root.AttachToHost();
        Assert.True(leaf.IsAttachedToVisualTree);

        root.DetachFromHost();
        Assert.False(leaf.IsAttachedToVisualTree);

        Assert.Equal(new[] { "+root", "+child", "+leaf", "-leaf", "-child", "-root" }, log);
    }

    [Fact]
    public void UnhostedTree_IsNotAttached()
    {
        var root = new StackPanel();
        var leaf = new TextBlock("leaf");
        var log = Record(("leaf", leaf));

        root.Add(leaf);

        Assert.False(leaf.IsAttachedToVisualTree);
        Assert.Empty(log);
    }

    [Fact]
    public void AddingAndRemovingUnderAttachedParent_RaisesEventsForWholeSubtree()
    {
        var root = new StackPanel();
        root.AttachToHost();
        var subtree = new StackPanel();
        var leaf = new TextBlock("leaf");
        subtree.Add(leaf);
        var log = Record(("subtree", subtree), ("leaf", leaf));

        root.Add(subtree);
        root.Remove(subtree);

        Assert.Equal(new[] { "+subtree", "+leaf", "-leaf", "-subtree" }, log);
    }

    [Fact]
    public void MovingBetweenAttachedParents_RaisesNoEvents()
    {
        var root = new StackPanel();
        var left = new StackPanel();
        var right = new StackPanel();
        var moving = new TextBlock("moving");
        root.Add(left);
        root.Add(right);
        left.Add(moving);
        root.AttachToHost();
        var log = Record(("moving", moving));

        right.Add(moving);

        Assert.True(moving.IsAttachedToVisualTree);
        Assert.Empty(log);
    }

    [Fact]
    public void AttachedEvent_SeesStyledValues()
    {
        var root = new StackPanel();
        root.Styles.Add(new Style(typeof(TextBlock)).Set(TextBlock.FontSizeProperty, 26f));
        root.AttachToHost();
        var tb = new TextBlock("styled");
        float seen = 0;
        tb.AttachedToVisualTree += (s, e) => seen = tb.FontSize;

        root.Add(tb);

        Assert.Equal(26f, seen);
    }

    [Fact]
    public void AttachToHost_OnNodeWithParent_Throws()
    {
        var root = new StackPanel();
        var child = new StackPanel();
        root.Add(child);

        Assert.Throws<InvalidOperationException>(() => child.AttachToHost());
    }

    [Fact]
    public void AddingHostedRoot_OrAncestor_AsChild_Throws()
    {
        var hosted = new StackPanel();
        hosted.AttachToHost();
        Assert.Throws<InvalidOperationException>(() => new StackPanel().Add(hosted));

        var parent = new StackPanel();
        var child = new StackPanel();
        parent.Add(child);
        Assert.Throws<InvalidOperationException>(() => child.Add(parent));
        Assert.Throws<InvalidOperationException>(() => child.AddChild(child));
    }

    [Fact]
    public void StyleCollection_AddRange_RaisesStylesChangedOnce()
    {
        var styles = new StyleCollection();
        int raised = 0;
        styles.StylesChanged += () => raised++;

        styles.AddRange(new[] { new Style(typeof(TextBlock)), new Style(typeof(Button)), new Style(typeof(StackPanel)) });
        styles.AddRange(Array.Empty<Style>());

        Assert.Equal(1, raised);
        Assert.Equal(3, styles.Count);
    }
}
