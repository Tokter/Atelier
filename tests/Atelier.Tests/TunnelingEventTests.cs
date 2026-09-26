using System;
using System.Collections.Generic;
using Xunit;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Tests;

public class RecordingPanel : StackPanel
{
    public RecordingPanel(string name, List<string> log, bool interceptPreview = false)
    {
        Name = name;
        Log = log;
        InterceptPreview = interceptPreview;
    }

    public string Name { get; }
    public List<string> Log { get; }
    public bool InterceptPreview { get; }

    public override void OnPreviewPointerPressed(PointerEventArgs e)
    {
        Log.Add("preview:" + Name);
        e.Handled |= InterceptPreview;
        base.OnPreviewPointerPressed(e);
    }

    public override void OnPointerPressed(PointerEventArgs e)
    {
        Log.Add("bubble:" + Name);
        base.OnPointerPressed(e);
    }

    public override void OnPreviewKeyDown(KeyEventArgs e)
    {
        Log.Add("preview:" + Name);
        e.Handled |= InterceptPreview;
        base.OnPreviewKeyDown(e);
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        Log.Add("bubble:" + Name);
        base.OnKeyDown(e);
    }
}

public class TunnelingEventTests
{
    private static (RecordingPanel Outer, RecordingPanel Inner, List<string> Log) Build(bool outerIntercepts = false)
    {
        var log = new List<string>();
        var outer = new RecordingPanel("outer", log, outerIntercepts);
        var inner = new RecordingPanel("inner", log);
        outer.Add(inner);
        return (outer, inner, log);
    }

    [Fact]
    public void PointerEvent_TunnelsThenBubbles()
    {
        var (_, inner, log) = Build();

        inner.DispatchPointerEvent(new PointerEventArgs(Point.Zero), static (el, a) => el.OnPreviewPointerPressed(a), static (el, a) => el.OnPointerPressed(a));

        Assert.Equal(new[] { "preview:outer", "preview:inner", "bubble:inner", "bubble:outer" }, log);
    }

    [Fact]
    public void HandledPreview_StopsEventBeforeChildren()
    {
        var (_, inner, log) = Build(outerIntercepts: true);

        inner.DispatchPointerEvent(new PointerEventArgs(Point.Zero), static (el, a) => el.OnPreviewPointerPressed(a), static (el, a) => el.OnPointerPressed(a));

        Assert.Equal(new[] { "preview:outer" }, log);
    }

    [Fact]
    public void KeyEvent_TunnelsThenBubbles_ThroughFocusManager()
    {
        var (_, inner, log) = Build();
        var focusable = new FocusableTestElement();
        inner.Add(focusable);
        FocusManager.SetFocus(focusable);

        FocusManager.DispatchKeyDown(new KeyEventArgs(Key.A));

        Assert.Equal(new[] { "preview:outer", "preview:inner", "bubble:inner", "bubble:outer" }, log);
        FocusManager.SetFocus(null);
    }

    [Fact]
    public void PointerTunneling_DoesNotAllocate()
    {
        var (_, inner, _) = Build();
        var leaf = new MeasureCountingElement();
        inner.Add(leaf);
        var e = new PointerEventArgs(new Point(1, 1));
        Action dispatch = () => leaf.DispatchPointerEvent(e, static (el, a) => el.OnPreviewPointerMoved(a), static (el, a) => el.OnPointerMoved(a));

        dispatch();
        long before = GC.GetAllocatedBytesForCurrentThread();
        dispatch();

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
