using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Tests;

/// <summary>A command whose CanExecute can be switched.</summary>
internal sealed class ToggleCommand : ICommand
{
    private bool _canExecute = true;
    public int Executions { get; private set; }
    public object? LastParameter { get; private set; }
    public event EventHandler? CanExecuteChanged;

    public bool Enabled
    {
        get => _canExecute;
        set
        {
            _canExecute = value;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool CanExecute(object? parameter) => _canExecute;

    public void Execute(object? parameter)
    {
        Executions++;
        LastParameter = parameter;
    }
}

[Keybinding(name: "ShowAbout", group: "Global", defaultKeybinding: "F12")]
internal sealed class ShowAboutCommand : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) { }
}

internal sealed class MenuTestViewModel
{
    public ToggleCommand Save { get; } = new();
}

[Collection("KeybindingTests")]
public sealed class MenuTests : IDisposable
{
    private readonly TimeSpan _delay = MenuItem.SubmenuShowDelay;

    public MenuTests()
    {
        KeybindingManager.Clear();
        PopupManager.CloseAllPopups();
        MenuItem.SubmenuShowDelay = TimeSpan.Zero;
    }

    public void Dispose()
    {
        KeybindingManager.Clear();
        PopupManager.CloseAllPopups();
        MenuItem.SubmenuShowDelay = _delay;
        MenuManager.Reset();
    }

    private static (StackPanel Root, Menu Menu) Host(Menu menu, params UIElement[] more)
    {
        var root = new StackPanel().Children(new UIElement[] { menu }.Concat(more).ToArray());
        root.AttachToHost();
        Layout(root);
        return (root, menu);
    }

    private static void Layout(UIElement root)
    {
        root.Measure(new Size(800, 600));
        root.Arrange(new Rect(0, 0, 800, 600));
        PopupManager.UpdatePopups(new Size(800, 600), root);
    }

    private static void Click(ButtonBase item)
    {
        var p = new Point(5, 5);
        item.OnPointerEntered(new PointerEventArgs(p, p));
        item.OnPointerPressed(new PointerEventArgs(p, p, PointerButtons.Left));
        item.OnPointerReleased(new PointerEventArgs(p, p, PointerButtons.Left));
    }

    private static void Press(UIElement target, Key key, ModifierKeys modifiers = ModifierKeys.None) =>
        target.DispatchKeyEvent(new KeyEventArgs(key, 0, modifiers, true), static (el, a) => el.OnPreviewKeyDown(a), static (el, a) => el.OnKeyDown(a));

    private static UIElement Focused(UIElement root) => FocusManager.GetFocusedElement(root)!;

    [Fact]
    public void AccessText_RemovesTheMarker()
    {
        Assert.Equal("Save As", AccessText.Parse("Save _As", out int index));
        Assert.Equal(5, index);
        Assert.Equal("A_B", AccessText.Parse("A__B", out index));
        Assert.Equal(-1, index);

        var item = new MenuItem("E_xit");
        Assert.Equal("Exit", item.HeaderTextBlock!.Text);
        Assert.Equal('X', item.AccessKey);
        Assert.Equal('O', new MenuItem("Open").AccessKey); // first letter without a marker
    }

    [Fact]
    public void GestureText_ComesFromTheCommandsKeybinding()
    {
        var vm = new MenuTestViewModel();
        KeybindingManager.RegisterKeybinding(new KeybindingDescriptor("Save", "Editor", "Ctrl+S", vm.Save));
        KeybindingManager.RegisterKeybinding(new KeybindingDescriptor("Chord", "Editor", "Ctrl+K, Ctrl+D1", new ToggleCommand()));

        var save = new MenuItem("_Save", vm.Save);
        var (root, _) = Host(new Menu().Items(new MenuItem("_File").Items(save)));
        Assert.Equal("Ctrl+S", save.GestureText);

        // A manual text wins; rebinding updates the item.
        KeybindingManager.RegisterOrUpdateKeybinding(new KeybindingDescriptor("Save", "Editor", "Ctrl+Shift+S", vm.Save));
        Assert.Equal("Ctrl+Shift+S", save.GestureText);
        save.InputGestureText = "F2";
        Assert.Equal("F2", save.GestureText);
        root.DetachFromHost();
    }

    [Fact]
    public void GestureLookup_ResolvesPropertyKeybindings_AndCommandClasses_AndPrefersTheGroup()
    {
        var vm = new MenuTestViewModel();
        KeybindingManager.RegisterKeybinding(new KeybindingDescriptor("Save", "Editor",
            "Ctrl+S", new PropertyKeybindingCommand<MenuTestViewModel>("Save", x => x.Save)));
        KeybindingManager.RegisterKeybinding(new KeybindingDescriptor("SaveGlobal", "Global", "Ctrl+Alt+S", vm.Save));
        KeybindingManager.RegisterKeybinding(new KeybindingDescriptor("ShowAbout", "Global", "F12", new ShowAboutCommand()));

        // The wrapper matches through a target; the group decides between two bindings.
        Assert.Equal("Ctrl+Alt+S", KeybindingManager.GetGestureText(vm.Save));
        Assert.Equal("Ctrl+S", KeybindingManager.GetGestureText(vm.Save, "Editor", [vm]));

        // Another instance of a [Keybinding] command class matches by type; general commands never do.
        Assert.Equal("F12", KeybindingManager.GetGestureText(new ShowAboutCommand()));
        Assert.Null(KeybindingManager.GetGestureText(new ToggleCommand()));

        // In a menu, the DataContext chain supplies the target.
        var item = new MenuItem("_Save", vm.Save);
        var menu = new Menu { DataContext = vm, KeybindingGroup = "Editor" }.Items(new MenuItem("_File").Items(item));
        var (root, _) = Host(menu);
        Assert.Equal("Ctrl+S", item.GestureText);
        root.DetachFromHost();
    }

    [Fact]
    public void DisplayText_UsesKeyLabels()
    {
        Assert.Equal("Ctrl+1", KeybindingGesture.FormatForDisplay("Ctrl+D1"));
        Assert.Equal("Ctrl+K, Ctrl+,", KeybindingGesture.FormatForDisplay("Ctrl+K, Ctrl+Comma"));
        Assert.Equal("Del", KeybindingGesture.FormatForDisplay("Delete"));
    }

    [Fact]
    public void ClickingAHeader_OpensItsMenu_AndHoverSwitchesMenus()
    {
        var file = new MenuItem("_File").Items(new MenuItem("_New"));
        var edit = new MenuItem("_Edit").Items(new MenuItem("_Undo"));
        var (root, menu) = Host(new Menu().Items(file, edit));
        Assert.True(file.IsTopLevel);

        file.OnPointerPressed(new PointerEventArgs(new Point(5, 5), new Point(5, 5), PointerButtons.Left)); // headers open on press
        Assert.True(file.IsSubmenuOpen);
        Assert.Same(file, menu.OpenItem);

        edit.OnPointerEntered(new PointerEventArgs(Point.Zero, Point.Zero));
        Assert.True(edit.IsSubmenuOpen);
        Assert.False(file.IsSubmenuOpen);
        Assert.Same(edit, menu.OpenItem);
        root.DetachFromHost();
    }

    [Fact]
    public void ClickingAnItem_RunsTheCommand_AndClosesTheMenu()
    {
        var command = new ToggleCommand();
        var item = new MenuItem("_New", command) { CommandParameter = 42 };
        var file = new MenuItem("_File").Items(item);
        var (root, menu) = Host(new Menu().Items(file));

        file.OpenSubmenu();
        Layout(root);
        Click(item);
        Assert.Equal(1, command.Executions);
        Assert.Equal(42, command.LastParameter);
        Assert.False(file.IsSubmenuOpen);
        Assert.Null(menu.OpenItem);
        root.DetachFromHost();
    }

    [Fact]
    public void CanExecute_DisablesItems_AndHideWhenDisabledHidesThem()
    {
        var command = new ToggleCommand();
        var paste = new MenuItem("_Paste", command);
        var delete = new MenuItem("_Delete", command).HideWhenDisabled();
        var (root, _) = Host(new Menu().Items(new MenuItem("_Edit").Items(paste, delete)));

        command.Enabled = false;
        Assert.False(paste.IsEnabled);
        Assert.Equal(Visibility.Visible, paste.Visibility);
        Assert.Equal(Visibility.Collapsed, delete.Visibility);

        command.Enabled = true;
        Assert.Equal(Visibility.Visible, delete.Visibility);
        root.DetachFromHost();
    }

    [Fact]
    public void CheckableItems_Toggle_RadioItemsCheckOne_AndStaysOpenOnClick()
    {
        var wrap = new MenuItem("_Wrap").IsCheckable().StaysOpenOnClick();
        var left = new MenuItem("_Left").IsCheckable().GroupName("a").IsChecked();
        var right = new MenuItem("_Right").IsCheckable().GroupName("a");
        var view = new MenuItem("_View").Items(wrap, new Separator(), left, right);
        var (root, _) = Host(new Menu().Items(view));
        view.OpenSubmenu();
        Layout(root);

        Click(wrap);
        Assert.True(wrap.IsChecked);
        Assert.True(view.IsSubmenuOpen);
        Assert.True(view.Submenu.HasIconColumn); // checkable items reserve the icon column for all

        Click(right);
        Assert.True(right.IsChecked);
        Assert.False(left.IsChecked);
        Assert.False(view.IsSubmenuOpen);

        view.OpenSubmenu();
        Click(right); // a checked radio item stays checked
        Assert.True(right.IsChecked);
        root.DetachFromHost();
    }

    [Fact]
    public void Keyboard_NavigatesSubmenus_AndMovesBetweenMenus()
    {
        var recent = new MenuItem("_Recent").Items(new MenuItem("a.txt"), new MenuItem("b.txt"));
        var file = new MenuItem("_File").Items(new MenuItem("_New"), new MenuItem("Dis_abled") { IsEnabled = false }, recent);
        var edit = new MenuItem("_Edit").Items(new MenuItem("_Undo"));
        var (root, menu) = Host(new Menu().Items(file, edit));

        menu.EnterKeyboardMode();
        Assert.True(file.IsFocused);
        Press(file, Key.Down);
        Assert.True(file.IsSubmenuOpen);
        Assert.Equal("_New", ((MenuItem)Focused(root)).Header);

        Press(Focused(root), Key.Down); // skips the disabled item
        Assert.Same(recent, Focused(root));
        Press(recent, Key.Right);
        Assert.True(recent.IsSubmenuOpen);
        Assert.Equal("a.txt", ((MenuItem)Focused(root)).Header);

        Press(Focused(root), Key.Left);
        Assert.False(recent.IsSubmenuOpen);
        Assert.Same(recent, Focused(root));

        Press(recent, Key.Left); // at the top level of a menu: the neighboring menu (wrapping)
        Assert.True(edit.IsSubmenuOpen);
        Assert.False(file.IsSubmenuOpen);

        Press(Focused(root), Key.F); // no item with F in Edit
        Press(Focused(root), Key.U); // U picks Undo, which closes the menu
        Assert.Null(menu.OpenItem);
        root.DetachFromHost();
    }

    [Fact]
    public void AltAndAltLetter_ReachTheMainMenu()
    {
        var textBox = new TextBox();
        var file = new MenuItem("_File").Items(new MenuItem("_New"));
        var view = new MenuItem("_View").Items(new MenuItem("_Zoom"));
        var (root, menu) = Host(new Menu().Items(file, view), textBox);
        textBox.Focus();

        // Alt pressed and released alone: the first header gets the focus; access keys show while it has the keyboard.
        MenuManager.HandleKeyDown(new KeyEventArgs(Key.LeftAlt, 0, ModifierKeys.Alt, true), root);
        Assert.True(MenuManager.AccessKeysVisible);
        MenuManager.HandleKeyUp(new KeyEventArgs(Key.LeftAlt, 0, ModifierKeys.None, false), root);
        Assert.True(menu.IsKeyboardActive);
        Assert.True(file.IsFocused);

        // Escape leaves the menu bar and returns the focus.
        Press(file, Key.Escape);
        Assert.False(menu.IsKeyboardActive);
        Assert.True(textBox.IsFocused);
        Assert.False(MenuManager.AccessKeysVisible);

        // Alt+V opens View.
        var altV = new KeyEventArgs(Key.V, 0, ModifierKeys.Alt, true);
        Assert.True(MenuManager.HandleKeyDown(altV, root));
        Assert.True(view.IsSubmenuOpen);
        Assert.Equal("_Zoom", ((MenuItem)Focused(root)).Header);

        // F10 toggles the keyboard mode off (closing the menu).
        MenuManager.HandleKeyDown(new KeyEventArgs(Key.F10, 0, ModifierKeys.None, true), root);
        Assert.False(view.IsSubmenuOpen);
        Assert.False(menu.IsKeyboardActive);
        root.DetachFromHost();
    }

    [Fact]
    public void DataBound_UsesChildrenSelector_ItemSetup_AndSeparators()
    {
        var data = new ObservableCollection<object>
        {
            new Node("File", [new Node("Open", null), MenuSeparator.Instance, new Node("Close", null)]),
            new Node("Help", null),
        };
        var clicked = new List<string>();
        var menu = new Menu
        {
            ChildrenSelector = n => (n as Node)?.Children,
            ItemSetup = (item, n) =>
            {
                var node = (Node)n;
                item.Header = "_" + node.Title;
                item.Click += (_, _) => clicked.Add(node.Title);
            },
            ItemsSource = data,
        };
        var (root, _) = Host(menu);

        var file = menu.TopLevelItems.First();
        Assert.Equal("_File", file.Header);
        Assert.Same(data[0], file.DataItem);
        var children = file.Submenu.Items;
        Assert.Equal(3, children.Count);
        Assert.IsType<Separator>(file.Submenu.ContainerFromIndex(1));

        file.OpenSubmenu();
        Layout(root);
        Click(file.Submenu.MenuItems.Last());
        Assert.Equal(["Close"], clicked);

        // A new top-level node appears.
        data.Add(new Node("Tools", null));
        Assert.Equal(3, menu.TopLevelItems.Count());
        root.DetachFromHost();
    }

    private sealed record Node(string Title, IReadOnlyList<object>? Children);

    [Fact]
    public void ContextMenu_OpensOnRightClick_WithTheTargetsDataContext_AndCloses()
    {
        var vm = new MenuTestViewModel();
        var copy = new MenuItem("_Copy", vm.Save);
        var menu = new ContextMenu().Items(copy, new Separator(), new MenuItem("_Other"));
        var target = new Border { DataContext = vm, Width = 100, Height = 50, IsFocusable = true }.ContextMenu(menu);
        var (root, _) = Host(new Menu(), target);
        target.Focus();

        ContextMenuOpeningEventArgs? opening = null;
        menu.Opening += (_, e) => opening = e;
        Assert.False(ContextMenuService.OnPointerReleased(target, PointerButtons.Left, handled: false));
        Assert.True(ContextMenuService.OnPointerReleased(target, PointerButtons.Right, handled: false));
        Assert.True(menu.IsOpen);
        Assert.Same(target, opening!.Target);
        Assert.Same(vm, menu.DataContext);

        Layout(root);
        Click(copy);
        Assert.Equal(1, vm.Save.Executions);
        Assert.False(menu.IsOpen);
        Assert.True(target.IsFocused);

        // The menu key opens it with the first item focused; Opening can cancel.
        Assert.True(MenuManager.HandleKeyDown(new KeyEventArgs(Key.Menu, 0, ModifierKeys.None, true), root));
        Assert.True(copy.IsFocused);
        menu.IsOpen = false;
        menu.Opening += (_, e) => e.Cancel = true;
        Assert.False(ContextMenuService.TryOpen(target, byKeyboard: false));
        root.DetachFromHost();
    }

    [Fact]
    public void ContextMenuSubmenus_BelongToTheWindow()
    {
        var sort = new MenuItem("_Sort").Items(new MenuItem("_Name"), new MenuItem("_Date"));
        var menu = new ContextMenu().Items(sort);
        var target = new Border { Width = 100, Height = 50 }.ContextMenu(menu);
        var (root, _) = Host(new Menu(), target);

        ContextMenuService.TryOpen(target, byKeyboard: true);
        Layout(root);
        Press(Focused(root), Key.Right);
        Assert.True(sort.IsSubmenuOpen);
        Assert.Contains(sort.SubmenuPopup, PopupManager.ActivePopups);
        Assert.True(PopupManager.HasActivePopupsIn(root));
        Assert.Equal("_Name", ((MenuItem)Focused(root)).Header);
        menu.IsOpen = false;
        root.DetachFromHost();
    }

    [Fact]
    public void TitleBar_HostsTheMenuAfterTheIcon()
    {
        var menu = new Menu().Items(new MenuItem("_File"));
        var titleBar = new TitleBar { Title = "App" }.Menu(menu);
        Assert.Same(titleBar, menu.Parent!.Parent!.Parent);
        titleBar.Menu = null;
        Assert.Null(menu.Parent);
    }
}
