using System;
using System.Collections.Generic;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Platform;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Tests;

public class KeyRecordingElement : FocusableTestElement
{
    public List<Key> Keys { get; } = [];

    public override void OnKeyDown(KeyEventArgs e)
    {
        Keys.Add(e.Key);
        base.OnKeyDown(e);
    }
}

public class FakeHostWindow : IHostWindow
{
    public int CloseCount { get; private set; }
    public bool IsMaximized { get; private set; }
    public void Minimize() { }
    public void ToggleMaximize() => IsMaximized = !IsMaximized;
    public void Close() => CloseCount++;
    public void DragMove() { }
}

public class MultiWindowFocusTests
{
    private static (StackPanel Root, KeyRecordingElement Field) CreateWindowTree()
    {
        var root = new StackPanel();
        var field = new KeyRecordingElement();
        root.Add(field);
        return (root, field);
    }

    [Fact]
    public void EachTree_KeepsItsOwnFocus()
    {
        var (rootA, fieldA) = CreateWindowTree();
        var (rootB, fieldB) = CreateWindowTree();

        FocusManager.SetFocus(fieldA);
        FocusManager.SetFocus(fieldB);

        Assert.True(fieldA.IsFocused);
        Assert.True(fieldB.IsFocused);
        Assert.Same(fieldA, FocusManager.GetFocusedElement(rootA));
        Assert.Same(fieldB, FocusManager.GetFocusedElement(rootB));
    }

    [Fact]
    public void KeyInput_GoesToTheFocusedElementOfTheGivenTree()
    {
        var (rootA, fieldA) = CreateWindowTree();
        var (rootB, fieldB) = CreateWindowTree();
        FocusManager.SetFocus(fieldA);
        FocusManager.SetFocus(fieldB);

        FocusManager.DispatchKeyDown(new KeyEventArgs(Key.A), rootA);
        FocusManager.DispatchKeyDown(new KeyEventArgs(Key.B), rootB);

        Assert.Equal(new[] { Key.A }, fieldA.Keys);
        Assert.Equal(new[] { Key.B }, fieldB.Keys);
    }

    [Fact]
    public void ActivateRoot_SelectsWhichTreeCurrentFocusedReports()
    {
        var (rootA, fieldA) = CreateWindowTree();
        var (rootB, fieldB) = CreateWindowTree();
        FocusManager.SetFocus(fieldA);
        FocusManager.SetFocus(fieldB);
        try
        {
            FocusManager.ActivateRoot(rootA);
            Assert.Same(fieldA, FocusManager.CurrentFocused);

            // Once a host activated a tree, focusing in another tree doesn't switch the active tree.
            FocusManager.SetFocus(fieldB);
            Assert.Same(fieldA, FocusManager.CurrentFocused);

            FocusManager.ActivateRoot(rootB);
            Assert.Same(fieldB, FocusManager.CurrentFocused);
        }
        finally
        {
            FocusManager.ActivateRoot(null);
        }
    }

    [Fact]
    public void ModalScope_OnlyAffectsItsOwnTree()
    {
        var (rootA, fieldA) = CreateWindowTree();
        var (rootB, fieldB) = CreateWindowTree();
        var dialog = new StackPanel();
        var dialogField = new FocusableTestElement();
        dialog.Add(dialogField);
        rootA.Add(dialog);

        FocusManager.PushModal(dialog);
        FocusManager.SetFocus(fieldA); // outside the modal scope of tree A: rejected
        FocusManager.SetFocus(fieldB); // tree B has no modal scope: allowed

        Assert.Same(dialogField, FocusManager.GetFocusedElement(rootA));
        Assert.Same(fieldB, FocusManager.GetFocusedElement(rootB));
        Assert.Same(dialog, FocusManager.GetModal(rootA));
        Assert.Null(FocusManager.GetModal(rootB));

        FocusManager.PopModal(dialog);
    }

    [Fact]
    public void MovingFocusedElementToAnotherTree_ClearsFocusInTheOldTree()
    {
        var (rootA, fieldA) = CreateWindowTree();
        var (rootB, _) = CreateWindowTree();
        FocusManager.SetFocus(fieldA);

        rootB.Add(fieldA);

        Assert.Null(FocusManager.GetFocusedElement(rootA));
        Assert.False(fieldA.IsFocused);
    }

    [Fact]
    public void Popups_AreFilteredByTree()
    {
        var (rootA, _) = CreateWindowTree();
        var (rootB, _) = CreateWindowTree();
        var popup = new Popup { Child = new TextBlock("menu") };
        rootA.Add(popup);
        popup.IsOpen = true;
        try
        {
            Assert.True(PopupManager.HasActivePopupsIn(rootA));
            Assert.False(PopupManager.HasActivePopupsIn(rootB));

            // Escape in window B must not close window A's popup.
            Assert.False(PopupManager.HandleKeyDown(new KeyEventArgs(Key.Escape), rootB));
            Assert.True(popup.IsOpen);

            Assert.True(PopupManager.HandleKeyDown(new KeyEventArgs(Key.Escape), rootA));
            Assert.False(popup.IsOpen);
        }
        finally
        {
            PopupManager.CloseAllPopups();
        }
    }

    [Fact]
    public void Host_IsTheWindowOfTheElementsOwnTree()
    {
        var hostA = new FakeHostWindow();
        var hostB = new FakeHostWindow();
        var (rootA, fieldA) = CreateWindowTree();
        var (rootB, fieldB) = CreateWindowTree();
        rootA.AttachToHost(hostA);
        rootB.AttachToHost(hostB);

        Assert.Same(hostA, fieldA.Host);
        Assert.Same(hostB, fieldB.Host);

        rootA.DetachFromHost();
        Assert.Null(fieldA.Host);
    }

    [Fact]
    public void TitleBar_ClosesItsOwnWindow()
    {
        var hostA = new FakeHostWindow();
        var hostB = new FakeHostWindow();
        var rootA = new StackPanel();
        var rootB = new StackPanel();
        var titleBar = new TitleBar();
        rootB.Add(titleBar);
        rootA.AttachToHost(hostA);
        rootB.AttachToHost(hostB);

        var closeButton = FindCloseButton(titleBar);
        closeButton.OnPointerEntered(new PointerEventArgs(default));
        closeButton.OnPointerPressed(new PointerEventArgs(default, PointerButtons.Left));
        closeButton.OnPointerReleased(new PointerEventArgs(default, PointerButtons.Left));

        Assert.Equal(0, hostA.CloseCount);
        Assert.Equal(1, hostB.CloseCount);
    }

    private static Button FindCloseButton(UIElement root)
    {
        Button? last = null;
        void Walk(VisualNode node)
        {
            if (node is Button b) last = b;
            for (int i = 0; i < node.Children.Count; i++) Walk(node.Children[i]);
        }
        Walk(root);
        return last!; // the close button is the last button in the title bar
    }
}

public class RadioGroupScopeTests
{
    [Fact]
    public void SameGroupName_InDifferentWindows_DoesNotInteract()
    {
        var windowA = new StackPanel();
        var windowB = new StackPanel();
        var a = new RadioButton { GroupName = "Shipping" };
        var b = new RadioButton { GroupName = "Shipping" };
        windowA.Add(a);
        windowB.Add(b);
        windowA.AttachToHost();
        windowB.AttachToHost();

        a.IsChecked = true;
        b.IsChecked = true;

        Assert.True(a.IsChecked);
        Assert.True(b.IsChecked);
    }

    [Fact]
    public void SameGroupName_InOneWindow_StillDeselects()
    {
        var window = new StackPanel();
        var left = new StackPanel();
        var right = new StackPanel();
        var a = new RadioButton { GroupName = "Shipping" };
        var b = new RadioButton { GroupName = "Shipping" };
        left.Add(a);
        right.Add(b);
        window.Add(left);
        window.Add(right);
        window.AttachToHost();

        a.IsChecked = true;
        b.IsChecked = true;

        Assert.False(a.IsChecked);
        Assert.True(b.IsChecked);
    }

    [Fact]
    public void UnnamedGroups_AreScopedToTheirParent()
    {
        var window = new StackPanel();
        var group1 = new StackPanel();
        var group2 = new StackPanel();
        var a1 = new RadioButton();
        var a2 = new RadioButton();
        var b1 = new RadioButton();
        group1.Add(a1);
        group1.Add(a2);
        group2.Add(b1);
        window.Add(group1);
        window.Add(group2);

        a1.IsChecked = true;
        b1.IsChecked = true;   // other parent: a1 stays checked
        a2.IsChecked = true;   // same parent: a1 is deselected

        Assert.False(a1.IsChecked);
        Assert.True(a2.IsChecked);
        Assert.True(b1.IsChecked);
    }
}

public class DialogHostMultiWindowTests
{
    [Fact]
    public void FindNearestHost_ByIdentifier_PrefersTheActiveWindow()
    {
        var hostA = new DialogHost { Identifier = "RootHost" };
        var hostB = new DialogHost { Identifier = "RootHost" }; // created last
        hostA.AttachToHost();
        hostB.AttachToHost();
        var previous = DialogHost.RootVisualProvider;
        try
        {
            DialogHost.RootVisualProvider = () => hostA;   // window A is active

            Assert.Same(hostA, DialogHost.FindNearestHost(null, "RootHost"));
        }
        finally
        {
            DialogHost.RootVisualProvider = previous;

            // Detached hosts ("closed windows") must not be preferred by other tests' lookups.
            hostA.DetachFromHost();
            hostB.DetachFromHost();
        }
    }

    [Fact]
    public void UnreferencedHost_CanBeCollected()
    {
        var weak = CreateHost();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(weak.TryGetTarget(out _));
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference<DialogHost> CreateHost() => new(new DialogHost { Identifier = "Temporary" });
}
