using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Xunit;

namespace Atelier.Tests;

public class DialogBehaviorTests
{
    public enum RemovalPath
    {
        SetDialogNull,
        SetIsOpenFalse,
        ReplaceDialog,
        ScrimClick,
        CloseAllDialogs
    }

    private static UIElement Scrim(DialogHost host)
    {
        // The scrim sits directly below the topmost dialog.
        var children = host.Children;
        for (int i = 0; i < children.Count; i++)
        {
            if (children[i] == host.Dialog)
            {
                return (UIElement)children[i - 1];
            }
        }
        throw new InvalidOperationException("No dialog open.");
    }

    [Theory]
    [InlineData(RemovalPath.SetDialogNull, DialogResult.None)]
    [InlineData(RemovalPath.SetIsOpenFalse, DialogResult.None)]
    [InlineData(RemovalPath.ReplaceDialog, DialogResult.None)]
    [InlineData(RemovalPath.ScrimClick, DialogResult.Cancel)]
    [InlineData(RemovalPath.CloseAllDialogs, DialogResult.None)]
    public async Task RemovingDialog_CompletesTask_AndShowAsyncWorksAgain(RemovalPath path, DialogResult expected)
    {
        var host = new DialogHost { CloseOnClickAway = true, Content = new Border() };
        host.Measure(new Size(800, 600));
        host.Arrange(new Rect(0, 0, 800, 600));

        var dialog = new Dialog("Title", "Message", DialogButtons.OkCancel);
        DialogClosedEventArgs? closedArgs = null;
        dialog.Closed += (s, e) => closedArgs = e;

        var task = dialog.ShowAsync(host);
        Assert.True(dialog.IsOpen);

        var replacement = new Border();
        switch (path)
        {
            case RemovalPath.SetDialogNull: host.Dialog = null; break;
            case RemovalPath.SetIsOpenFalse: host.IsOpen = false; break;
            case RemovalPath.ReplaceDialog: host.Dialog = replacement; break;
            case RemovalPath.ScrimClick:
                Scrim(host).OnPointerPressed(new PointerEventArgs(new Point(5, 5), new Point(5, 5), PointerButtons.Left));
                break;
            case RemovalPath.CloseAllDialogs: host.CloseAllDialogs(); break;
        }

        Assert.True(task.IsCompleted);
        var response = await task;
        Assert.Equal(expected, response.Result);
        Assert.False(dialog.IsOpen);
        Assert.Null(dialog.Parent);
        Assert.NotNull(closedArgs);
        Assert.Equal(expected, closedArgs!.Response.Result);

        if (path == RemovalPath.ReplaceDialog)
        {
            Assert.Same(replacement, host.Dialog);
            host.Dialog = null;
        }

        // The dialog can be shown again, and completes normally.
        var second = dialog.ShowAsync(host);
        Assert.Same(dialog, host.Dialog);
        dialog.Close(DialogResult.Ok);
        Assert.Equal(DialogResult.Ok, (await second).Result);
        Assert.False(host.IsOpen);
        Assert.Single(host.Children); // only the content
    }

    [Fact]
    public async Task NestedDialog_FromButtonHandler_StacksAndRestoresParent()
    {
        var host = new DialogHost { Content = new Button("Background") };
        var more = new Button("More");
        var parent = new Dialog("Parent", null, DialogButtons.OkCancel) { Content = more };
        var child = new Dialog("Child", "Are you sure?", DialogButtons.YesNo);

        Task<DialogResponse>? childTask = null;
        more.Click += (s, e) => childTask = child.ShowAsync(more);

        var parentTask = parent.ShowAsync(host);
        FocusManager.SetFocus(more);
        Assert.Same(parent, FocusManager.GetModal(host));

        // Pressing the button inside the parent dialog opens the child on top of it.
        more.OnKeyDown(new KeyEventArgs(Key.Enter, 13, ModifierKeys.None, true));
        Assert.NotNull(childTask);

        Assert.Same(child, host.Dialog);
        Assert.Equal(new UIElement[] { parent, child }, host.OpenDialogs);
        Assert.True(parent.IsOpen);
        Assert.Same(host, parent.Parent);
        Assert.False(parentTask.IsCompleted);
        Assert.Same(child, FocusManager.GetModal(host));
        var focused = FocusManager.GetFocusedElement(host);
        Assert.True(focused == child || focused!.IsDescendantOf(child));

        // Children: content, parent, scrim, child: the scrim covers the parent.
        Assert.Equal(4, host.Children.Count);
        Assert.Same(parent, host.Children[1]);
        Assert.Same(child, host.Children[3]);
        Assert.IsNotType<Dialog>(host.Children[2]);

        child.Close(DialogResult.Yes);
        Assert.Equal(DialogResult.Yes, (await childTask!).Result);

        // The parent is the topmost dialog again, with its modal scope and focus restored.
        Assert.Same(parent, host.Dialog);
        Assert.Equal(new UIElement[] { parent }, host.OpenDialogs);
        Assert.Same(parent, FocusManager.GetModal(host));
        Assert.Same(more, FocusManager.GetFocusedElement(host));
        Assert.Equal(3, host.Children.Count);
        Assert.Same(parent, host.Children[2]);
        Assert.True(host.IsOpen);

        parent.Close(DialogResult.Ok);
        Assert.Equal(DialogResult.Ok, (await parentTask).Result);
        Assert.False(host.IsOpen);
        Assert.Null(FocusManager.GetModal(host));
    }

    [Fact]
    public async Task ClosingParent_AlsoClosesDialogsAboveIt()
    {
        var host = new DialogHost();
        var parent = new Dialog("Parent");
        var child = new Dialog("Child");

        var parentTask = parent.ShowAsync(host);
        var childTask = child.ShowAsync(host);

        parent.Close(DialogResult.Ok);

        Assert.Equal(DialogResult.None, (await childTask).Result);
        Assert.Equal(DialogResult.Ok, (await parentTask).Result);
        Assert.Empty(host.OpenDialogs);
        Assert.Empty(host.Children);
    }

    [Fact]
    public async Task DetachedHost_CompletesOpenDialogs()
    {
        var root = new StackPanel();
        var host = new DialogHost { Content = new Border() };
        root.Add(host);
        root.AttachToHost();
        try
        {
            var first = new Dialog("First");
            var second = new Dialog("Second");
            var firstTask = first.ShowAsync(host);
            var secondTask = second.ShowAsync(host);

            root.RemoveChild(host);

            Assert.Equal(DialogResult.None, (await secondTask).Result);
            Assert.Equal(DialogResult.None, (await firstTask).Result);
            Assert.False(host.IsOpen);
            Assert.Null(host.Dialog);
            Assert.False(first.IsOpen);
        }
        finally
        {
            root.DetachFromHost();
        }
    }

    [Fact]
    public void FindNearestHost_IgnoresHostsNotDisplayed()
    {
        string id = "Unattached-" + Guid.NewGuid();
        var host = new DialogHost { Identifier = id };
        var root = new StackPanel();
        root.Add(host);

        Assert.Null(DialogHost.FindNearestHost(null, id));
        Assert.Throws<InvalidOperationException>(() => { _ = DialogHost.ShowAsync(new Dialog("x"), id); });

        // An ancestor host is always found, displayed or not.
        var inner = new Button("inner");
        host.Content = inner;
        Assert.Same(host, DialogHost.FindNearestHost(inner));

        root.AttachToHost();
        try
        {
            Assert.Same(host, DialogHost.FindNearestHost(null, id));
        }
        finally
        {
            root.DetachFromHost();
        }

        Assert.Null(DialogHost.FindNearestHost(null, id));
    }

    [Fact]
    public async Task Closing_CanCancel_AndEventsFireOnce()
    {
        var host = new DialogHost();
        var dialog = new Dialog("Save?", null, DialogButtons.OkCancel);
        int opened = 0, closed = 0, hostOpened = 0, hostClosed = 0;
        var closingResults = new List<DialogResult>();
        bool veto = true;

        dialog.Opened += (s, e) => opened++;
        dialog.Closed += (s, e) => closed++;
        dialog.Closing += (s, e) =>
        {
            closingResults.Add(e.Response.Result);
            e.Cancel = veto;
        };
        host.DialogOpened += (s, e) => { Assert.Same(dialog, e.Dialog); Assert.Null(e.Response); hostOpened++; };
        host.DialogClosed += (s, e) => { Assert.Equal(DialogResult.Ok, e.Response!.Result); hostClosed++; };

        var task = dialog.ShowAsync(host);
        Assert.Equal(1, opened);
        Assert.Equal(1, hostOpened);

        dialog.Close(DialogResult.Ok);
        Assert.False(task.IsCompleted);
        Assert.Same(dialog, host.Dialog);
        Assert.Equal(0, closed);

        veto = false;
        dialog.Close(DialogResult.Ok);
        Assert.Equal(DialogResult.Ok, (await task).Result);
        Assert.Equal(new[] { DialogResult.Ok, DialogResult.Ok }, closingResults);
        Assert.Equal(1, closed);
        Assert.Equal(1, hostClosed);
    }

    [Fact]
    public void Escape_OnNonDialogContent_ClosesOnlyWithCloseOnClickAway()
    {
        var host = new DialogHost { Content = new Border() };
        var panel = new Border { Width = 100, Height = 50 };
        host.Dialog = panel;

        var esc = new KeyEventArgs(Key.Escape, 27, ModifierKeys.None, true);
        FocusManager.DispatchKeyDown(esc, host);
        Assert.Same(panel, host.Dialog);

        host.CloseOnClickAway = true;
        esc = new KeyEventArgs(Key.Escape, 27, ModifierKeys.None, true);
        Assert.True(FocusManager.DispatchKeyDown(esc, host));
        Assert.Null(host.Dialog);
        Assert.False(host.IsOpen);

        // The handler was removed with the content.
        host.Dialog = new Border();
        host.CloseOnClickAway = false;
        panel.OnKeyDown(new KeyEventArgs(Key.Escape, 27, ModifierKeys.None, true));
        Assert.NotNull(host.Dialog);
        host.Dialog = null;
    }

    [Fact]
    public void ScrimClick_OnNonDialogContent_RemovesIt()
    {
        var host = new DialogHost { CloseOnClickAway = true, Content = new Border() };
        var panel = new Border();
        UIElement? closedDialog = null;
        host.DialogClosed += (s, e) => closedDialog = e.Dialog;
        host.Dialog = panel;

        Scrim(host).OnPointerPressed(new PointerEventArgs(new Point(1, 1), new Point(1, 1), PointerButtons.Left));

        Assert.Null(host.Dialog);
        Assert.Same(panel, closedDialog);
    }

    [Fact]
    public void IsOpen_SetTrueWithoutDialog_HasNoEffect()
    {
        var host = new DialogHost();
        host.IsOpen = true;
        Assert.False(host.IsOpen);
        Assert.Empty(host.Children);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateGarbageHosts(int count)
    {
        for (int i = 0; i < count; i++)
        {
            _ = new DialogHost();
        }
    }

    [Fact]
    public void HostRegistry_PrunesCollectedHosts()
    {
        var field = typeof(DialogHost).GetField("_hostRefs", BindingFlags.NonPublic | BindingFlags.Static)!;
        var refs = (List<WeakReference<DialogHost>>)field.GetValue(null)!;

        for (int round = 0; round < 20; round++)
        {
            CreateGarbageHosts(200);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        // 4000 hosts were created; without pruning on registration they would all still be listed.
        Assert.True(refs.Count < 1000, $"Registry holds {refs.Count} entries.");
    }
}
