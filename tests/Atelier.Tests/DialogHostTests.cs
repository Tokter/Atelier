using System;
using System.Threading.Tasks;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Threading;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Rendering;
using SkiaSharp;
using Xunit;

namespace Atelier.Tests;

public class DialogHostTests
{
    [Fact]
    public void DialogHost_DefaultState_RendersContentWithoutScrim()
    {
        var host = new DialogHost();
        var button = new Button("Underlying Button") { Width = 100, Height = 40 };
        host.Content = button;

        Assert.False(host.IsOpen);
        Assert.Null(host.Dialog);
        Assert.Single(host.Children);
        Assert.Same(button, host.Children[0]);

        host.Measure(new Size(800, 600));
        host.Arrange(new Rect(0, 0, 800, 600));

        var hit = host.HitTest(new Point(50, 20));
        Assert.NotNull(hit);
        Assert.True(hit == button || hit.Parent == button);
    }

    [Fact]
    public void DialogHost_DialogAssigned_DarkensAndCentersDialog()
    {
        var host = new DialogHost();
        var button = new Button("Underlying Button") { Width = 100, Height = 40 };
        host.Content = button;

        var dialog = new Dialog("Test Title", "Test Message")
        {
            Width = 400,
            Height = 200
        };

        host.Dialog = dialog;

        Assert.True(host.IsOpen);
        Assert.Same(dialog, host.Dialog);
        Assert.Equal(3, host.Children.Count); // Content, Scrim, Dialog

        host.Measure(new Size(800, 600));
        host.Arrange(new Rect(0, 0, 800, 600));

        // Dialog should be centered horizontally: (800 - 400) / 2 = 200
        // Dialog should be centered vertically: (600 - 200) / 2 = 200
        Assert.Equal(200, dialog.Bounds.X, 1);
        Assert.Equal(200, dialog.Bounds.Y, 1);
        Assert.Equal(400, dialog.Bounds.Width, 1);
        Assert.Equal(200, dialog.Bounds.Height, 1);
        host.Dialog = null;
    }

    [Fact]
    public void DialogHost_HitTest_BlocksInputToUnderlyingContent()
    {
        var host = new DialogHost();
        var button = new Button("Underlying Button")
        {
            Width = 120,
            Height = 40,
            Margin = new Thickness(10)
        };
        host.Content = button;

        var dialog = new Dialog("Confirm", "Proceed with operation?")
        {
            Width = 300,
            Height = 150
        };
        host.Dialog = dialog;

        host.Measure(new Size(800, 600));
        host.Arrange(new Rect(0, 0, 800, 600));

        // Point (15, 15) is directly over the underlying button, but outside the centered dialog (which starts at X=250, Y=225)
        var hitOutsideDialog = host.HitTest(new Point(15, 15));
        Assert.NotNull(hitOutsideDialog);
        Assert.NotSame(button, hitOutsideDialog);
        Assert.False(hitOutsideDialog.IsDescendantOf(button));

        // Pointer event on the scrim element must be swallowed
        var pointerEvent = new PointerEventArgs(new Point(15, 15), new Point(15, 15), PointerButtons.Left);
        hitOutsideDialog.OnPointerPressed(pointerEvent);
        Assert.True(pointerEvent.Handled);

        // Point (400, 300) is inside the dialog
        var hitInsideDialog = host.HitTest(new Point(400, 300));
        Assert.NotNull(hitInsideDialog);
        Assert.True(hitInsideDialog == dialog || hitInsideDialog.IsDescendantOf(dialog));
        host.Dialog = null;
    }

    [Fact]
    public void DialogHost_CloseOnClickAway_DismissesDialog()
    {
        var host = new DialogHost
        {
            CloseOnClickAway = true
        };
        var dialog = new Dialog("Dismissible", "Click outside to close");
        host.Dialog = dialog;

        host.Measure(new Size(800, 600));
        host.Arrange(new Rect(0, 0, 800, 600));

        // Hit scrim outside dialog
        var scrim = host.HitTest(new Point(10, 10));
        Assert.NotNull(scrim);

        var pointerEvent = new PointerEventArgs(new Point(10, 10), new Point(10, 10), PointerButtons.Left);
        scrim.OnPointerPressed(pointerEvent);

        // Dialog should now be closed
        Assert.Null(host.Dialog);
        Assert.False(host.IsOpen);
    }

    [Fact]
    public async Task Dialog_ShowAsync_FindsNearestDialogHostInTree()
    {
        var host = new DialogHost();
        var panel = new StackPanel();
        var border = new Border();
        var button = new Button("Trigger");

        border.Child = button;
        panel.Add(border);
        host.Content = panel;

        var dialog = new Dialog("Found Host", "Found nearest host automatically", DialogButtons.Ok);

        var showTask = dialog.ShowAsync(button);

        Assert.Same(dialog, host.Dialog);
        Assert.True(host.IsOpen);

        dialog.Close(DialogResult.Ok);
        var response = await showTask;

        Assert.Equal(DialogResult.Ok, response.Result);
        Assert.Null(host.Dialog);
        Assert.False(host.IsOpen);
    }

    [Fact]
    public async Task Dialog_ShowAsync_ReturnsPressedButtonInformation()
    {
        var host = new DialogHost();
        var dialog = new Dialog("Delete File", "Are you sure?", DialogButtons.YesNo);

        var task = dialog.ShowAsync(host);

        // YesNo preset creates "No" (index 0) and "Yes" (index 1)
        Assert.Equal(2, dialog.Buttons.Count);
        var yesButton = dialog.Buttons.Find(b => b.Result == DialogResult.Yes);
        Assert.NotNull(yesButton);

        // Simulate clicking Yes
        dialog.Close(yesButton.Result, yesButton);

        var response = await task;
        Assert.Equal(DialogResult.Yes, response.Result);
        Assert.Equal("Yes", response.ButtonText);
        Assert.Same(yesButton, response.Button);

        // Implicit cast to DialogResult
        DialogResult dr = response;
        Assert.Equal(DialogResult.Yes, dr);
    }

    [Fact]
    public void Dialog_PresetButtons_ConfiguresStandardButtons()
    {
        var dialog = new Dialog();

        dialog.ButtonsPreset = DialogButtons.Ok;
        Assert.Single(dialog.Buttons);
        Assert.Equal(DialogResult.Ok, dialog.Buttons[0].Result);
        Assert.True(dialog.Buttons[0].IsDefault);

        dialog.ButtonsPreset = DialogButtons.OkCancel;
        Assert.Equal(2, dialog.Buttons.Count);
        Assert.Equal(DialogResult.Cancel, dialog.Buttons[0].Result);
        Assert.Equal(DialogResult.Ok, dialog.Buttons[1].Result);

        dialog.ButtonsPreset = DialogButtons.YesNo;
        Assert.Equal(2, dialog.Buttons.Count);
        Assert.Equal(DialogResult.No, dialog.Buttons[0].Result);
        Assert.Equal(DialogResult.Yes, dialog.Buttons[1].Result);

        dialog.ButtonsPreset = DialogButtons.YesNoCancel;
        Assert.Equal(3, dialog.Buttons.Count);
        Assert.Equal(DialogResult.Cancel, dialog.Buttons[0].Result);
        Assert.Equal(DialogResult.No, dialog.Buttons[1].Result);
        Assert.Equal(DialogResult.Yes, dialog.Buttons[2].Result);
    }

    [Fact]
    public async Task Dialog_CustomButtons_ReturnsCustomResultAndTag()
    {
        var host = new DialogHost();
        var dialog = new Dialog("Custom Action", "Choose an option");

        dialog.AddButton("Option A", DialogResult.Custom, tag: "PayloadA");
        dialog.AddButton("Option B", DialogResult.Custom, tag: 42);

        var task = dialog.ShowAsync(host);

        var btnB = dialog.Buttons[1];
        dialog.Close(btnB.Result, btnB);

        var response = await task;
        Assert.Equal(DialogResult.Custom, response.Result);
        Assert.Equal("Option B", response.ButtonText);
        Assert.Equal(42, response.Tag);
    }

    [Fact]
    public async Task Dialog_EscapeKey_CancelsDialog()
    {
        var host = new DialogHost();
        var dialog = new Dialog("Prompt", "Press escape to cancel", DialogButtons.OkCancel);

        var task = dialog.ShowAsync(host);

        var escapeKey = new KeyEventArgs(Key.Escape, 27, ModifierKeys.None, true);
        dialog.OnKeyDown(escapeKey);

        Assert.True(escapeKey.Handled);
        var response = await task;
        Assert.Equal(DialogResult.Cancel, response.Result);
        Assert.Null(host.Dialog);
    }

    [Fact]
    public async Task Dialog_EnterKey_TriggersDefaultButton()
    {
        var host = new DialogHost();
        var dialog = new Dialog("Confirm", "Press enter to accept", DialogButtons.OkCancel);

        var task = dialog.ShowAsync(host);

        var enterKey = new KeyEventArgs(Key.Enter, 13, ModifierKeys.None, true);
        dialog.OnKeyDown(enterKey);

        Assert.True(enterKey.Handled);
        var response = await task;
        Assert.Equal(DialogResult.Ok, response.Result);
        Assert.Null(host.Dialog);
    }

    [Fact]
    public async Task NestedDialogHosts_ResolvesNearestHost()
    {
        var outerHost = new DialogHost { Identifier = "OuterHost" };
        var outerPanel = new StackPanel();

        var innerHost = new DialogHost { Identifier = "InnerHost" };
        var innerButton = new Button("Deep Button");

        innerHost.Content = innerButton;
        outerPanel.Add(innerHost);
        outerHost.Content = outerPanel;

        var dialog = new Dialog("Scoped Dialog", "Belongs to inner host only");

        var task = dialog.ShowAsync(innerButton);

        // Nearest host to innerButton is innerHost!
        Assert.Same(dialog, innerHost.Dialog);
        Assert.True(innerHost.IsOpen);

        // Outer host must remain unaffected
        Assert.Null(outerHost.Dialog);
        Assert.False(outerHost.IsOpen);

        dialog.Close(DialogResult.Ok);
        await task;

        Assert.Null(innerHost.Dialog);
        Assert.False(innerHost.IsOpen);
    }

    [Fact]
    public void DialogHost_OverlayColor_HasDarkeningAlpha()
    {
        var host = new DialogHost();
        host.Content = new Border();
        // Default overlay color must have non-zero alpha (50% black = 128)
        Assert.True(host.OverlayColor.A > 0, "OverlayColor alpha must be > 0 to darken background");
        Assert.Equal(128, host.OverlayColor.A);

        var dialog = new Dialog("Darkening Test");
        host.Dialog = dialog;

        // Scrim element in children (index 1) must have Background with alpha > 0
        var scrim = host.Children[1] as Border;
        Assert.NotNull(scrim);
        Assert.True(scrim.Background.A > 0, "Scrim background must have alpha > 0");
        Assert.Equal(128, scrim.Background.A);
        host.Dialog = null;
    }

    [Fact]
    public void TextBlock_TextWrapping_WrapsLongTextAcrossLines()
    {
        string longText = "This is an alert dialog displayed via DialogHost. The background content is darkened and blocked from input.";
        var unwrappedBlock = new TextBlock(longText) { FontSize = 14f, TextWrapping = TextWrapping.NoWrap };
        unwrappedBlock.Measure(new Size(float.PositiveInfinity, float.PositiveInfinity));
        float singleLineWidth = unwrappedBlock.DesiredSize.Width;
        float singleLineHeight = unwrappedBlock.DesiredSize.Height;

        Assert.True(singleLineWidth > 400f, "Single line width should be long");

        var wrappedBlock = new TextBlock(longText) { FontSize = 14f, TextWrapping = TextWrapping.Wrap };
        wrappedBlock.Measure(new Size(300f, float.PositiveInfinity));

        Assert.True(wrappedBlock.DesiredSize.Width <= 300f, "Wrapped width must be <= available width");
        Assert.True(wrappedBlock.DesiredSize.Height > singleLineHeight, "Wrapped height must be multi-line");
    }

    [Fact]
    public void Dialog_AlertMessage_DoesNotExceedDialogBounds()
    {
        var host = new DialogHost();
        var dialog = new Dialog(
            "Important Notice",
            "This is an alert dialog displayed via DialogHost. The background content is darkened and blocked from input.",
            DialogButtons.Ok);

        host.Dialog = dialog;
        host.Measure(new Size(1280, 800));
        host.Arrange(new Rect(0, 0, 1280, 800));

        // Dialog must not exceed its MaxWidth (560)
        Assert.True(dialog.Bounds.Width <= dialog.MaxWidth);
        Assert.True(dialog.Bounds.Width >= dialog.MinWidth);

        // Check internal root stack
        var rootStack = dialog.Children[0] as UIElement;
        Assert.NotNull(rootStack);
        Assert.True(rootStack.Bounds.Width <= dialog.Bounds.Width);

        // All internal children of rootStack must fit within rootStack
        foreach (var childNode in rootStack.Children)
        {
            if (childNode is UIElement child && child.Visibility == Visibility.Visible)
            {
                Assert.True(child.Bounds.Width <= rootStack.Bounds.Width,
                    $"Child {child.GetType().Name} width ({child.Bounds.Width}) exceeds container width ({rootStack.Bounds.Width})");
            }
        }
        host.Dialog = null;
    }

    [Fact]
    public void DialogHost_TabFocus_CannotEscapeDialog()
    {
        var host = new DialogHost();
        var backgroundPanel = new StackPanel();
        var backgroundBtn1 = new Button("Background 1");
        var backgroundBtn2 = new Button("Background 2");
        backgroundPanel.Add(backgroundBtn1);
        backgroundPanel.Add(backgroundBtn2);
        host.Content = backgroundPanel;

        // Focus background button initially
        FocusManager.SetFocus(backgroundBtn1);
        Assert.Same(backgroundBtn1, FocusManager.CurrentFocused);

        var dialog = new Dialog("Modal Test", "Testing focus trap", DialogButtons.OkCancel);
        host.Dialog = dialog;

        // On open, focus should have entered the dialog (buttons are Cancel at 0, OK at 1)
        Assert.NotNull(FocusManager.CurrentFocused);
        Assert.True(FocusManager.CurrentFocused == dialog || FocusManager.CurrentFocused.IsDescendantOf(dialog));

        var firstFocused = FocusManager.CurrentFocused;

        // Tab to next element (Cancel button)
        FocusManager.FocusNext(host);
        var secondFocused = FocusManager.CurrentFocused;
        Assert.True(secondFocused!.IsDescendantOf(dialog));
        Assert.NotSame(firstFocused, secondFocused);

        // Tab to next element (OK button)
        FocusManager.FocusNext(host);
        var thirdFocused = FocusManager.CurrentFocused;
        Assert.True(thirdFocused!.IsDescendantOf(dialog));
        Assert.NotSame(secondFocused, thirdFocused);

        // Tab again: must wrap around to firstFocused within dialog, NEVER escaping to backgroundBtn1 or backgroundBtn2!
        FocusManager.FocusNext(host);
        var fourthFocused = FocusManager.CurrentFocused;
        Assert.Same(firstFocused, fourthFocused);

        // Even after many Tab presses, focus remains strictly within the dialog
        for (int i = 0; i < 10; i++)
        {
            FocusManager.FocusNext(host);
            Assert.True(FocusManager.CurrentFocused != null && (FocusManager.CurrentFocused == dialog || FocusManager.CurrentFocused.IsDescendantOf(dialog)));
            Assert.NotSame(backgroundBtn1, FocusManager.CurrentFocused);
            Assert.NotSame(backgroundBtn2, FocusManager.CurrentFocused);
        }

        // Attempting to directly focus background elements while dialog is open must be rejected
        FocusManager.SetFocus(backgroundBtn1);
        Assert.NotSame(backgroundBtn1, FocusManager.CurrentFocused);
        Assert.True(FocusManager.CurrentFocused == dialog || FocusManager.CurrentFocused!.IsDescendantOf(dialog));

        // When dialog closes, focus returns and background buttons can be focused again
        dialog.Close(DialogResult.Ok);
        Assert.Null(host.Dialog);

        FocusManager.SetFocus(backgroundBtn2);
        Assert.Same(backgroundBtn2, FocusManager.CurrentFocused);
    }

    [Fact]
    public async Task DialogHost_EscapeKey_WhenButtonFocused_BubblesAndCancelsDialog()
    {
        var host = new DialogHost();
        var dialog = new Dialog("Prompt", "Press escape while button has focus", DialogButtons.OkCancel);
        var task = dialog.ShowAsync(host);

        // Tab from dialog into the Cancel button
        FocusManager.FocusNext(host);
        var focused = FocusManager.CurrentFocused;
        Assert.NotNull(focused);
        Assert.IsType<Button>(focused);
        Assert.True(focused.IsDescendantOf(dialog));

        // Press Escape while the button is focused
        var escapeKey = new KeyEventArgs(Key.Escape, 27, ModifierKeys.None, true);
        bool handled = FocusManager.DispatchKeyDown(escapeKey, host);

        Assert.True(handled);
        Assert.True(escapeKey.Handled);
        var response = await task;
        Assert.Equal(DialogResult.Cancel, response.Result);
        Assert.Null(host.Dialog);
    }

    [Fact]
    public async Task DialogHost_EscapeKey_WhenTextBoxFocused_BubblesAndCancelsDialog()
    {
        var host = new DialogHost();
        var textBox = new TextBox { Text = "User input" };
        var dialog = new Dialog("Form", "Enter text", DialogButtons.OkCancel)
        {
            Content = textBox
        };
        var task = dialog.ShowAsync(host);

        // Focus the text box
        FocusManager.SetFocus(textBox);
        Assert.Same(textBox, FocusManager.CurrentFocused);

        // Press Escape while text box is focused
        var escapeKey = new KeyEventArgs(Key.Escape, 27, ModifierKeys.None, true);
        bool handled = FocusManager.DispatchKeyDown(escapeKey, host);

        Assert.True(handled);
        Assert.True(escapeKey.Handled);
        var response = await task;
        Assert.Equal(DialogResult.Cancel, response.Result);
        Assert.Null(host.Dialog);
    }

    [Fact]
    public async Task DialogHost_EscapeKey_WhenFocusIsNull_StillCancelsModal()
    {
        var host = new DialogHost();
        var dialog = new Dialog("Alert", "Click away test", DialogButtons.OkCancel);
        var task = dialog.ShowAsync(host);

        // Simulate clicking outside focusable elements (clearing focus)
        FocusManager.SetFocus(null);
        Assert.Null(FocusManager.CurrentFocused);

        // Press Escape
        var escapeKey = new KeyEventArgs(Key.Escape, 27, ModifierKeys.None, true);
        bool handled = FocusManager.DispatchKeyDown(escapeKey, host);

        Assert.True(handled);
        Assert.True(escapeKey.Handled);
        var response = await task;
        Assert.Equal(DialogResult.Cancel, response.Result);
        Assert.Null(host.Dialog);
    }

    [Fact]
    public void DialogHost_Measure_ExpandsToAccommodateDialog()
    {
        var host = new DialogHost();
        // Small content (80x50)
        var smallContent = new Border { Width = 80f, Height = 50f };
        host.Content = smallContent;

        host.Measure(new Size(1000f, 1000f));
        Assert.Equal(80f, host.DesiredSize.Width);
        Assert.Equal(50f, host.DesiredSize.Height);

        // Assign a standard dialog that needs ~180px height and ~300px width
        var dialog = new Dialog("Expansion Test", "Checking auto-expansion", DialogButtons.Ok);
        host.Dialog = dialog;

        host.Measure(new Size(1000f, 1000f));
        // Desired size must now expand to fit the dialog!
        Assert.True(host.DesiredSize.Height >= dialog.DesiredSize.Height);
        Assert.True(host.DesiredSize.Height > 50f, "Host desired height must expand beyond 50px");
        Assert.True(host.DesiredSize.Width >= dialog.DesiredSize.Width);
        Assert.True(host.DesiredSize.Width > 80f, "Host desired width must expand beyond 80px");

        // When arranged with the desired size, dialog fits 100% inside host
        host.Arrange(new Rect(0, 0, host.DesiredSize.Width, host.DesiredSize.Height));
        Assert.True(dialog.Bounds.Height <= host.Bounds.Height);
        Assert.True(dialog.Bounds.Width <= host.Bounds.Width);

        // When dialog is removed, desired size shrinks back to small content
        host.Dialog = null;
        host.Measure(new Size(1000f, 1000f));
        Assert.Equal(80f, host.DesiredSize.Width);
        Assert.Equal(50f, host.DesiredSize.Height);
    }

    [Fact]
    public async Task DialogHost_GlobalAndLocalHosts_CanCoexistAndRouteByIdentifier()
    {
        var rootHost = new DialogHost { Identifier = "RootHost" };
        var localHost = new DialogHost { Identifier = "LocalGalleryHost" };

        var localContent = new Button("Local Action");
        localHost.Content = localContent;

        var pageLayout = new StackPanel();
        pageLayout.Add(localHost);
        rootHost.Content = pageLayout;

        // 1. Show a dialog targeting the Local host
        var localDialog = new Dialog("Local Alert", "Confined to local card", DialogButtons.Ok);
        var localTask = localDialog.ShowAsync(localHost);

        Assert.True(localHost.IsOpen);
        Assert.Same(localDialog, localHost.Dialog);
        Assert.False(rootHost.IsOpen);
        Assert.Null(rootHost.Dialog);

        localDialog.Close(DialogResult.Ok);
        await localTask;
        Assert.False(localHost.IsOpen);

        // 2. Show a dialog targeting the Global host
        var globalDialog = new Dialog("Global Alert", "Covers entire application", DialogButtons.Ok);
        var globalTask = DialogHost.ShowAsync(globalDialog, "RootHost");

        Assert.True(rootHost.IsOpen);
        Assert.Same(globalDialog, rootHost.Dialog);
        Assert.False(localHost.IsOpen);
        Assert.Null(localHost.Dialog);

        globalDialog.Close(DialogResult.Ok);
        await globalTask;
        Assert.False(rootHost.IsOpen);
    }

    [Fact]
    public async Task DialogHost_CloseOnClickAway_Behavior()
    {
        var host = new DialogHost { CloseOnClickAway = true, Content = new Border() };
        var dialog = new Dialog("Modal Test", "Click away test", DialogButtons.OkCancel);
        var task = dialog.ShowAsync(host);

        Assert.True(host.IsOpen);
        Assert.Equal(3, host.Children.Count);

        // Scrim is at index 1
        var scrim = host.Children[1] as UIElement;
        Assert.NotNull(scrim);

        // 1. When CloseOnClickAway = true, clicking scrim dismisses with Cancel
        var clickEvent = new PointerEventArgs(new Point(10, 10), new Point(10, 10), PointerButtons.Left);
        scrim.OnPointerPressed(clickEvent);

        Assert.True(clickEvent.Handled);
        var response = await task;
        Assert.Equal(DialogResult.Cancel, response.Result);
        Assert.Null(response.Button); // Dismissed via scrim, not a button!
        Assert.False(host.IsOpen);

        // 2. When CloseOnClickAway = false, clicking scrim does NOT dismiss
        host.CloseOnClickAway = false;
        var dialog2 = new Dialog("Strict Modal", "Backdrop clicks should do nothing", DialogButtons.OkCancel);
        var task2 = dialog2.ShowAsync(host);

        Assert.True(host.IsOpen);
        var scrim2 = host.Children[1] as UIElement;
        Assert.NotNull(scrim2);

        var clickEvent2 = new PointerEventArgs(new Point(10, 10), new Point(10, 10), PointerButtons.Left);
        scrim2.OnPointerPressed(clickEvent2);

        Assert.True(clickEvent2.Handled);
        Assert.True(host.IsOpen); // Remains open!
        Assert.Same(dialog2, host.Dialog);

        // Explicitly closing button works
        dialog2.Close(DialogResult.Ok);
        var response2 = await task2;
        Assert.Equal(DialogResult.Ok, response2.Result);
        Assert.False(host.IsOpen);
    }

    [Fact]
    public async Task Dialog_CloseFromBackgroundThread_CompletesSuccessfully()
    {
        var host = new DialogHost();
        var dialog = new Dialog("Async Modal", "Testing close from thread pool", DialogButtons.OkCancel);
        var showTask = dialog.ShowAsync(host);

        Assert.True(host.IsOpen);
        Assert.Same(dialog, host.Dialog);

        // Close from ThreadPool background thread
        await Task.Run(() =>
        {
            dialog.Close(DialogResult.Ok);
        });

        var response = await showTask;
        Assert.Equal(DialogResult.Ok, response.Result);
        Assert.Null(host.Dialog);
        Assert.False(host.IsOpen);
    }

    [Fact]
    public async Task DialogHost_BackgroundThreadDialogAssignment_MarshalsProperly()
    {
        var host = new DialogHost();
        var dialog = new Dialog("Async Assignment", "Testing dialog assignment from background thread");

        await Task.Run(() =>
        {
            host.Dialog = dialog;
        });

        Assert.True(host.IsOpen);
        Assert.Same(dialog, host.Dialog);

        await Task.Run(() =>
        {
            host.Dialog = null;
        });

        Assert.False(host.IsOpen);
        Assert.Null(host.Dialog);
    }

    [Fact]
    public async Task Dispatcher_UIThread_DefaultAndCustomExecution()
    {
        Assert.True(Dispatcher.CheckAccess());

        bool posted = false;
        Dispatcher.Post(() => posted = true);
        Assert.True(posted);

        bool sent = false;
        Dispatcher.Send(() => sent = true);
        Assert.True(sent);

        int result = await Dispatcher.InvokeAsync(() => 42);
        Assert.Equal(42, result);
    }

    [Fact]
    public void VisualTreeRenderer_ConcurrentChildRemoval_DoesNotThrowArgumentOutOfRangeException()
    {
        var parent = new StackPanel();
        var child1 = new Border { Width = 50, Height = 50 };
        var child2 = new Border { Width = 50, Height = 50 };
        var child3 = new Border { Width = 50, Height = 50 };

        parent.Add(child1);
        parent.Add(child2);
        parent.Add(child3);

        Assert.Equal(3, parent.Children.Count);

        using var bitmap = new SKBitmap(100, 100);
        using var canvas = new SKCanvas(bitmap);
        using var paintRegistry = new PaintRegistry();
        var dc = new DrawingContext(canvas, paintRegistry);

        var mutatingPresenter = new MutatingTestPresenter(parent);

        // Rendering should complete safely without throwing ArgumentOutOfRangeException
        VisualTreeRenderer.Render(parent, ref dc, mutatingPresenter);
        Assert.Single(parent.Children);
    }

    private sealed class MutatingTestPresenter(StackPanel parent) : IElementVisualPresenter
    {
        public void Render(UIElement element, ref DrawingContext context)
        {
            if (parent.Children.Count > 0 && element == parent.Children[0])
            {
                while (parent.Children.Count > 1)
                {
                    parent.RemoveChild(parent.Children[^1]);
                }
            }
        }
    }
}
