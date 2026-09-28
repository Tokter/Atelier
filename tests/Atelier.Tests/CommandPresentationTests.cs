using System.Linq;
using Atelier.Controls;
using Atelier.Core.Keybinding;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Theming.Material;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using Xunit;

namespace Atelier.Tests;

public class IconSourceTests
{
    private const string OutlineSvg =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"24\" height=\"24\" viewBox=\"0 0 24 24\" fill=\"none\" " +
        "stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\">" +
        "<path d=\"M5 12h14\"/><path d=\"M12 5v14\"/></svg>";

    [Theory]
    [InlineData("DarkMode", MaterialIconKind.DarkMode)]
    [InlineData("darkmode", MaterialIconKind.DarkMode)]
    [InlineData("  Save ", MaterialIconKind.Save)]
    public void MaterialIconNames_ResolveToTheGlyph(string source, MaterialIconKind kind)
    {
        Assert.True(IconSource.TryResolve(source, out var resolved, out var path, out var viewBox));
        Assert.Equal(kind, resolved);
        Assert.Null(path);
        Assert.Null(viewBox);
    }

    [Fact]
    public void MaterialIcons_HasAConstantForEveryIcon_WithItsName()
    {
        Assert.Equal("DarkMode", MaterialIcons.DarkMode);
        var constants = typeof(MaterialIcons).GetFields()
            .Where(f => f.IsLiteral)
            .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!);
        var names = System.Enum.GetNames<MaterialIconKind>().Where(n => n != nameof(MaterialIconKind.None)).ToArray();
        Assert.Equal(names.Length, constants.Count);
        foreach (var name in names)
        {
            Assert.Equal(name, constants[name]);
            Assert.True(IconSource.TryResolve(constants[name], out var kind, out _, out _));
            Assert.Equal(System.Enum.Parse<MaterialIconKind>(name), kind);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NoSuchIcon")]
    [InlineData("5")] // a number is not an enum name
    [InlineData("<svg><broken")]
    [InlineData("<div>not svg</div>")]
    [InlineData("<svg viewBox=\"0 0 24 24\"></svg>")] // nothing to draw
    public void InvalidSources_DontResolve(string? source)
    {
        Assert.False(IconSource.TryResolve(source, out var kind, out var path, out _));
        Assert.Equal(MaterialIconKind.None, kind);
        Assert.Null(path);
    }

    [Fact]
    public void PathData_ResolvesToGeometryWithoutViewBox()
    {
        Assert.True(IconSource.TryResolve("M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z", out var kind, out var path, out var viewBox));
        Assert.Equal(MaterialIconKind.None, kind);
        Assert.NotNull(path);
        Assert.Null(viewBox);
        Assert.Equal(2, path!.Bounds.Left, 0.01);
        Assert.Equal(22, path.Bounds.Right, 0.01);
    }

    [Fact]
    public void SvgDocument_KeepsItsViewBox_AndStrokesBecomeRoundOutlines()
    {
        Assert.True(IconSource.TryResolve(OutlineSvg, out _, out var path, out var viewBox));
        Assert.Equal(new Rect(0, 0, 24, 24), viewBox);

        // A plus of two 14-unit lines, 2 units wide with round caps: 1 unit beyond each end, 1 unit to each side.
        var bounds = path!.Bounds;
        Assert.Equal(4, bounds.Left, 0.05);
        Assert.Equal(20, bounds.Right, 0.05);
        Assert.Equal(4, bounds.Top, 0.05);
        Assert.Equal(20, bounds.Bottom, 0.05);
        Assert.True(path.Contains(12, 12));
        Assert.True(path.Contains(12, 6));
        Assert.False(path.Contains(6, 6));
    }

    [Fact]
    public void SvgDocument_ReadsShapesGroupsTransformsAndFillRules()
    {
        const string svg =
            "<svg viewBox=\"0 0 32 32\">" +
            "<g transform=\"translate(10 0)\"><rect x=\"0\" y=\"0\" width=\"4\" height=\"4\"/></g>" +
            "<circle cx=\"4\" cy=\"20\" r=\"2\"/>" +
            "<polygon points=\"20,20 30,20 30,30\"/>" +
            "<path fill-rule=\"evenodd\" d=\"M0 26h8v6H0z M2 28h4v2H2z\"/>" +
            "<rect x=\"20\" y=\"0\" width=\"4\" height=\"4\" style=\"fill:none\"/>" +
            "<defs><rect x=\"0\" y=\"0\" width=\"32\" height=\"32\"/></defs>" +
            "</svg>";
        Assert.True(IconSource.TryResolve(svg, out _, out var path, out var viewBox));
        Assert.Equal(new Rect(0, 0, 32, 32), viewBox);
        Assert.True(path!.Contains(12, 2));   // the translated rect
        Assert.False(path.Contains(2, 2));    // ...not at its untranslated place
        Assert.True(path.Contains(4, 20));    // the circle
        Assert.True(path.Contains(29, 25));   // inside the triangle
        Assert.False(path.Contains(21, 29));  // outside it
        Assert.True(path.Contains(1, 27));    // the frame of the even-odd path
        Assert.False(path.Contains(4, 29));   // its hole
        Assert.False(path.Contains(22, 2));   // fill:none, no stroke
    }

    [Fact]
    public void SvgDocument_WithoutViewBox_UsesWidthAndHeight()
    {
        Assert.True(IconSource.TryResolve("<svg width=\"16px\" height=\"16\"><rect x=\"4\" y=\"4\" width=\"8\" height=\"8\"/></svg>", out _, out _, out var viewBox));
        Assert.Equal(new Rect(0, 0, 16, 16), viewBox);
    }

    [Fact]
    public void IconSourceProperty_SetsTheGlyphOrTheGeometry_AndClearingRemovesThem()
    {
        var icon = new Icon { Source = OutlineSvg };
        Assert.NotNull(icon.Data);
        Assert.Equal(new Rect(0, 0, 24, 24), icon.ViewBox);
        Assert.Equal(MaterialIconKind.None, icon.Kind);

        icon.Source = "Settings";
        Assert.Equal(MaterialIconKind.Settings, icon.Kind);
        Assert.Null(icon.Data);
        Assert.Null(icon.ViewBox);

        icon.Source = "not an icon";
        Assert.Equal(MaterialIconKind.None, icon.Kind);
        Assert.Null(icon.Data);
    }

    [Fact]
    public void SvgIcon_IsDrawnInItsViewBox_KeepingThePadding()
    {
        using var theme = ActiveTheme.Use(MaterialTheme.CreateLight());
        // A 4×4 square in the middle of a 24×24 view box, drawn 48 px large: the square covers 20..28 px.
        var icon = new Icon { Source = "<svg viewBox=\"0 0 24 24\"><rect x=\"10\" y=\"10\" width=\"4\" height=\"4\"/></svg>", Size = 48, Foreground = Color.Black };
        var root = new Canvas { Width = 48, Height = 48 };
        root.Add(icon);
        using var bitmap = ThemeRendering.Render(root, 48, 48);

        Assert.True(ThemeRendering.IsClose(bitmap.GetPixel(24, 24), Color.Black), $"center: {bitmap.GetPixel(24, 24)}");
        Assert.True(ThemeRendering.IsClose(bitmap.GetPixel(21, 21), Color.Black), $"inside: {bitmap.GetPixel(21, 21)}");
        Assert.True(ThemeRendering.IsClose(bitmap.GetPixel(18, 24), Color.White), $"padding: {bitmap.GetPixel(18, 24)}");
        Assert.True(ThemeRendering.IsClose(bitmap.GetPixel(4, 4), Color.White), $"corner: {bitmap.GetPixel(4, 4)}");
    }
}

[Collection("KeybindingTests")]
public class CommandPresentationTests
{
    public CommandPresentationTests() => Atelier.Generated.GeneratedKeybindings.RegisterKeybindings();

    private static (Button Button, DocumentViewModel Vm) SaveButton()
    {
        var vm = new DocumentViewModel();
        var button = new Button { DataContext = vm };
        button.Command = vm.SaveCommand;
        return (button, vm);
    }

    private static UIElement? Shown(ButtonBase button) => button.Children.OfType<UIElement>().FirstOrDefault();

    [Fact]
    public void Button_WithoutContent_ShowsTheCommandsIconAndLabel()
    {
        var (button, _) = SaveButton();
        Assert.NotNull(button.CommandInfo);
        var row = Assert.IsType<StackPanel>(Shown(button));
        var parts = row.Children.OfType<UIElement>().ToArray();
        Assert.Equal(MaterialIconKind.Save, Assert.IsType<Icon>(parts[0]).Kind);
        Assert.Equal("Save", Assert.IsType<TextBlock>(parts[1]).Text);
        Assert.Null(button.Content); // generated, not set
    }

    [Fact]
    public void CommandDisplay_ChoosesIconOrLabel()
    {
        var (button, _) = SaveButton();
        button.CommandDisplay = CommandDisplay.Icon;
        Assert.Equal(MaterialIconKind.Save, Assert.IsType<Icon>(Shown(button)).Kind);

        button.CommandDisplay = CommandDisplay.Label;
        Assert.Equal("Save", Assert.IsType<TextBlock>(Shown(button)).Text);
    }

    [Fact]
    public void CommandWithoutIcon_ShowsItsLabel_EvenWhenOnlyTheIconIsAsked()
    {
        var vm = new DocumentViewModel();
        var button = new Button { DataContext = vm, CommandDisplay = CommandDisplay.Icon };
        button.Command = vm.CloseCommand;
        Assert.Equal("Close document", Assert.IsType<TextBlock>(Shown(button)).Text);
    }

    [Fact]
    public void OwnContent_Wins_AndClearingItShowsTheCommandAgain()
    {
        var (button, _) = SaveButton();
        button.Content = "Store";
        Assert.Equal("Store", Assert.IsType<TextBlock>(Shown(button)).Text);

        button.Content = null;
        Assert.IsType<StackPanel>(Shown(button));
    }

    [Fact]
    public void ToolTip_ShowsTheDescriptionAndShortcut_OrTheLabelWhenOnlyTheIconShows()
    {
        var (button, _) = SaveButton();
        Assert.Equal("Save the document (Ctrl+S)", ToolTipService.GetEffectiveToolTip(button));

        button.CommandDisplay = CommandDisplay.Icon;
        Assert.Equal("Save (Ctrl+S)\nSave the document", ToolTipService.GetEffectiveToolTip(button));

        ToolTipService.SetToolTip(button, "Mine");
        Assert.Equal("Mine", ToolTipService.GetEffectiveToolTip(button));
    }

    [Fact]
    public void ToolTip_WithoutDescription_ShowsTheShortcut_AndNothingWithoutEither()
    {
        var vm = new DocumentViewModel();
        var close = new Button { DataContext = vm };
        close.Command = vm.CloseCommand;
        Assert.Equal("Ctrl+W", ToolTipService.GetEffectiveToolTip(close));

        var plain = new Button { Command = new RelayCommand(() => { }) };
        Assert.Null(plain.CommandInfo);
        Assert.Null(ToolTipService.GetEffectiveToolTip(plain));
        Assert.Null(Shown(plain));
    }

    [Fact]
    public void ViewModelCommand_IsFoundThroughTheDataContextAroundTheButton()
    {
        var vm = new DocumentViewModel();
        var button = new Button { Command = vm.SaveCommand };
        Assert.Null(button.CommandInfo); // no DataContext: the view model's command can't be matched

        var panel = new StackPanel { DataContext = vm };
        panel.Add(button);
        button.DataContext = vm; // DataContext changes look the command up again
        Assert.Equal("Save", button.CommandInfo?.Name);
    }

    [Fact]
    public void CommandClass_IsFoundWithoutDataContext()
    {
        var button = new Button { Command = new ShowLicenseCommand() };
        Assert.Equal("License", button.CommandInfo?.Label);
        Assert.Equal("Show the version and the license", button.CommandToolTip);
    }

    [Fact]
    public void RegistrationChanges_UpdateDisplayedButtons()
    {
        var command = new RelayCommand(() => { });
        var root = new StackPanel();
        var button = new Button { Command = command };
        root.Add(button);
        root.AttachToHost();
        try
        {
            Assert.Null(Shown(button));

            var descriptor = new KeybindingDescriptor("Refresh", "Testing", "F5", command, label: "Reload", icon: "Refresh");
            KeybindingManager.RegisterOrUpdateKeybinding(descriptor);
            var row = Assert.IsType<StackPanel>(Shown(button));
            Assert.Equal("Reload", row.Children.OfType<TextBlock>().Single().Text);

            KeybindingManager.RegisterOrUpdateKeybinding(new KeybindingDescriptor("Refresh", "Testing", "F5", command, label: "Refresh all"));
            Assert.Equal("Refresh all", Assert.IsType<TextBlock>(Shown(button)).Text);

            KeybindingManager.UnregisterKeybinding("Testing", "Refresh");
            Assert.Null(button.CommandInfo);
            Assert.Null(Shown(button));
        }
        finally
        {
            KeybindingManager.UnregisterKeybinding("Testing", "Refresh");
            root.DetachFromHost();
        }
    }

    [Fact]
    public void MenuItem_WithoutHeaderOrIcon_ShowsTheCommands()
    {
        var item = new MenuItem { Command = new ShowLicenseCommand() };
        Assert.Equal("License", item.HeaderTextBlock?.Text);
        Assert.True(item.HasIcon);

        item.Header = "_About";
        Assert.Equal("About", item.HeaderTextBlock?.Text);

        item.Icon = "<svg viewBox=\"0 0 24 24\"><circle cx=\"12\" cy=\"12\" r=\"6\"/></svg>";
        Assert.True(item.HasIcon);
        Assert.Null(item.CommandToolTip); // menus show the shortcut themselves
    }

    [Fact]
    public void MenuItem_ShowsTheShortcutOfAViewModelCommand()
    {
        var vm = new DocumentViewModel();
        var item = new MenuItem { DataContext = vm };
        item.Command = vm.SaveCommand;
        Assert.Equal("Save", item.HeaderTextBlock?.Text);
        Assert.True(item.HasIcon);
        Assert.Equal("Ctrl+S", item.Children.OfType<TextBlock>().Select(t => t.Text).FirstOrDefault(t => t == "Ctrl+S"));
    }
}
