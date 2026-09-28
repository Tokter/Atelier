using System;
using Xunit;
using Atelier.Controls;
using Atelier.Core.Events;
using Atelier.Core.Inspection;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
using Atelier.Layout;
using Atelier.Markup;

namespace Atelier.Tests;

public class ColorConversionTests
{
    [Theory]
    [InlineData("#FF0000", 0f, 1f, 1f)]
    [InlineData("#00FF00", 120f, 1f, 1f)]
    [InlineData("#0000FF", 240f, 1f, 1f)]
    [InlineData("#808080", 0f, 0f, 0.502f)]
    [InlineData("#6750A4", 256.4f, 0.512f, 0.643f)]
    public void ToHsv_MatchesKnownValues(string hex, float hue, float saturation, float value)
    {
        Color.FromHex(hex).ToHsv(out float h, out float s, out float v);
        Assert.Equal(hue, h, 0.1f);
        Assert.Equal(saturation, s, 0.005f);
        Assert.Equal(value, v, 0.005f);
    }

    [Fact]
    public void Hsv_AndHsl_RoundTripEveryFewColors()
    {
        for (int r = 0; r < 256; r += 51)
        for (int g = 0; g < 256; g += 51)
        for (int b = 0; b < 256; b += 51)
        {
            var color = Color.FromArgb(200, (byte)r, (byte)g, (byte)b);
            color.ToHsv(out float h, out float s, out float v);
            Assert.Equal(color, Color.FromHsv(h, s, v, color.Af));
            color.ToHsl(out h, out s, out float l);
            Assert.Equal(color, Color.FromHsl(h, s, l, color.Af));
        }
    }

    [Fact]
    public void FromHsl_KnownValues_AndHueWraps()
    {
        Assert.Equal(Color.FromRgb(255, 0, 0), Color.FromHsl(0, 1, 0.5f));
        Assert.Equal(Color.White, Color.FromHsl(200, 1, 1));
        Assert.Equal(Color.Black, Color.FromHsl(200, 1, 0));
        Assert.Equal(Color.FromHsv(30, 1, 1), Color.FromHsv(390, 1, 1));
        Assert.Equal(Color.FromHsv(330, 1, 1), Color.FromHsv(-30, 1, 1));
    }
}

public class ColorPickerTests
{
    private static ColorPicker CreatePicker(Color color)
    {
        var picker = new ColorPicker { Color = color };
        picker.AttachToHost();
        picker.Measure(new Size(float.PositiveInfinity, float.PositiveInfinity));
        picker.Arrange(new Rect(0, 0, picker.DesiredSize.Width, picker.DesiredSize.Height));
        return picker;
    }

    [Fact]
    public void Measure_UsesTheDefaultWidth_AndShrinksWhenConstrained()
    {
        var picker = CreatePicker(Color.White);
        Assert.Equal(ColorPicker.DefaultWidth, picker.DesiredSize.Width);
        Assert.True(picker.DesiredSize.Height > 200);

        picker.Measure(new Size(200, 800));
        Assert.Equal(200, picker.DesiredSize.Width);
        picker.DetachFromHost();
    }

    [Fact]
    public void SettingTheColor_UpdatesEveryPart()
    {
        var picker = CreatePicker(Color.FromHex("#336699"));

        Assert.Equal("#336699", picker.HexBox.Text);
        Assert.Equal(0x33, picker.GetChannelSlider(0).Value);
        Assert.Equal(0x66, picker.GetChannelSlider(1).Value);
        Assert.Equal(0x99, picker.GetChannelSlider(2).Value);
        Assert.Equal(100, picker.GetChannelSlider(3).Value);
        Assert.Equal("153", picker.GetChannelBox(2).Text);
        Assert.Equal(210f, picker.Wheel.Hue, 0.5f);

        // The red slider's gradient runs from no red to full red at the current green and blue.
        Assert.Equal([Color.FromRgb(0, 0x66, 0x99), Color.FromRgb(255, 0x66, 0x99)], picker.GetChannelSlider(0).TrackColors);
        picker.DetachFromHost();
    }

    [Fact]
    public void Sliders_ChangeTheColor_InEachMode()
    {
        var picker = CreatePicker(Color.FromHex("#336699"));
        Color? reported = null;
        picker.ColorChanged += (_, c) => reported = c;

        picker.GetChannelSlider(0).Value = 255;
        Assert.Equal(Color.FromHex("#FF6699"), picker.Color);
        Assert.Equal(picker.Color, reported);

        picker.Mode = ColorPickerMode.Hsl;
        picker.GetChannelSlider(2).Value = 100; // lightness 100% is white
        Assert.Equal(Color.White, picker.Color);

        picker.Mode = ColorPickerMode.Hsb;
        picker.GetChannelSlider(0).Value = 120;
        picker.GetChannelSlider(1).Value = 100;
        picker.GetChannelSlider(2).Value = 100;
        Assert.Equal(Color.FromRgb(0, 255, 0), picker.Color);

        picker.GetChannelSlider(3).Value = 50;
        Assert.Equal(128, picker.Color.A);
        Assert.Equal("#8000FF00", picker.HexBox.Text);
        picker.DetachFromHost();
    }

    [Fact]
    public void Hue_SurvivesBlack_AndGray()
    {
        var picker = CreatePicker(Color.FromHex("#6750A4"));
        picker.Mode = ColorPickerMode.Hsb;
        float hue = picker.GetChannelSlider(0).Value;

        picker.GetChannelSlider(2).Value = 0;
        Assert.Equal(Color.Black, picker.Color);
        picker.GetChannelSlider(2).Value = 64;
        Assert.Equal(hue, picker.GetChannelSlider(0).Value);
        Assert.Equal(Color.FromHsv(hue, picker.GetChannelSlider(1).Value / 100f, 0.64f), picker.Color);

        picker.GetChannelSlider(1).Value = 0;
        Assert.Equal(hue, picker.Wheel.Hue, 0.01f);
        picker.DetachFromHost();
    }

    [Fact]
    public void HexBox_AppliesCompleteInputWhileTyping_AndNormalizesOnEnter()
    {
        var picker = CreatePicker(Color.White);

        picker.HexBox.Text = "#12";
        Assert.Equal(Color.White, picker.Color); // incomplete
        picker.HexBox.Text = "#123456";
        Assert.Equal(Color.FromHex("#123456"), picker.Color);
        Assert.Equal("#123456", picker.HexBox.Text); // the typed text is kept

        picker.HexBox.Text = "abc";
        picker.HexBox.OnKeyDown(new KeyEventArgs(Key.Enter, 0, ModifierKeys.None, true));
        Assert.Equal(Color.FromHex("#AABBCC"), picker.Color);
        Assert.Equal("#AABBCC", picker.HexBox.Text);

        picker.HexBox.Text = "nonsense";
        picker.HexBox.OnKeyDown(new KeyEventArgs(Key.Enter, 0, ModifierKeys.None, true));
        Assert.Equal("#AABBCC", picker.HexBox.Text);
        picker.DetachFromHost();
    }

    [Fact]
    public void ChannelBox_CommitsAClampedValue()
    {
        var picker = CreatePicker(Color.Black);
        var box = picker.GetChannelBox(1);

        box.Text = "300";
        box.OnKeyDown(new KeyEventArgs(Key.Enter, 0, ModifierKeys.None, true));
        Assert.Equal(Color.FromRgb(0, 255, 0), picker.Color);
        Assert.Equal("255", box.Text);

        box.Text = "x";
        box.OnKeyDown(new KeyEventArgs(Key.Enter, 0, ModifierKeys.None, true));
        Assert.Equal("255", box.Text);
        picker.DetachFromHost();
    }

    [Fact]
    public void Wheel_PicksHueAndSaturation_FromThePointer()
    {
        var picker = CreatePicker(Color.Black);
        var wheel = picker.Wheel;
        var center = wheel.WheelCenter;

        // Straight up at the edge is hue 90 (between yellow and green) at full saturation; on black the brightness
        // jumps to full so the pick is visible.
        wheel.PickAt(new Point(center.X, center.Y - wheel.WheelRadius - 20));
        Assert.Equal(90f, wheel.Hue, 0.01f);
        Assert.Equal(1f, wheel.Saturation);
        Assert.Equal(Color.FromHsv(90, 1, 1), picker.Color);

        // The thumb sits where the pick was (clamped to the edge).
        var thumb = wheel.ThumbPosition;
        Assert.Equal(center.X, thumb.X, 0.01f);
        Assert.Equal(center.Y - wheel.WheelRadius, thumb.Y, 0.01f);
        picker.DetachFromHost();
    }

    [Fact]
    public void Mode_ButtonsSelectTheMode_AndAlphaCanBeHidden()
    {
        var picker = new ColorPicker().Mode(ColorPickerMode.Hsl).IsAlphaEnabled(false);
        Assert.Equal(360, picker.GetChannelSlider(0).Maximum);
        Assert.Equal(Visibility.Collapsed, picker.GetChannelSlider(3).Visibility);

        picker.Mode = ColorPickerMode.Rgb;
        Assert.Equal(255, picker.GetChannelSlider(0).Maximum);
    }
}

public sealed class ColorPickerToolTipTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly TimeProvider _originalTime = ToolTipService.TimeProvider;

    public ColorPickerToolTipTests()
    {
        ToolTipService.Close();
        PopupManager.CloseAllPopups();
        ToolTipService.TimeProvider = _time;
    }

    public void Dispose()
    {
        ToolTipService.Close();
        PopupManager.CloseAllPopups();
        ToolTipService.TimeProvider = _originalTime;
    }

    [Fact]
    public void PropertyGridColorEditor_HasAPickerToolTip_ThatEditsLive()
    {
        var model = new TestInspectableModel();
        var grid = new PropertyGrid { SelectedObject = model };
        var desc = ObjectInspector.GetProperty(model, nameof(TestInspectableModel.ThemeColor));
        Assert.NotNull(desc);
        var context = new PropertyEditorContext(grid, model, desc);
        var editor = PropertyEditorRegistry.Default.CreateEditor(context);

        var picker = Assert.IsType<ColorPicker>(ToolTipService.GetToolTip(editor));
        Assert.Equal(Color.FromRgb(255, 0, 0), picker.Color);

        picker.GetChannelSlider(2).Value = 255;
        Assert.Equal(Color.FromRgb(255, 0, 255), model.ThemeColor);
        var hexBox = Assert.IsType<TextBox>(((StackPanel)editor).Children[1]);
        Assert.Equal("#FFFF00FF", hexBox.Text);

        // Typing in the editor's own box updates the picker.
        hexBox.Text = "#FF00FF00";
        hexBox.OnKeyDown(new KeyEventArgs(Key.Enter, 0, ModifierKeys.None, true));
        Assert.Equal(Color.FromRgb(0, 255, 0), picker.Color);
    }

    [Fact]
    public void FocusInsideAnInteractiveToolTip_GetsKeys_AndKeepsItOpen()
    {
        var picker = new ColorPicker();
        var field = new Button("Field").ToolTip(picker);
        var other = new Button("Other");
        var root = new StackPanel().Children(field, other);
        root.AttachToHost();
        root.Measure(new Size(800, 600));
        root.Arrange(new Rect(0, 0, 800, 600));
        other.Focus();

        ToolTipService.OnPointerOver(field);
        _time.AdvanceMs(500);
        Assert.Same(field, ToolTipService.CurrentOwner);

        // Clicking into the hex box focuses it in the window's focus scope, so keys reach it.
        picker.HexBox.Focus();
        Assert.True(picker.HexBox.IsFocused);
        Assert.False(other.IsFocused);
        Assert.Same(picker.HexBox, FocusManager.GetEffectiveKeyTarget(root));

        // Leaving with the pointer doesn't close it while it has the focus.
        ToolTipService.OnPointerOver(null);
        _time.AdvanceMs(2000);
        Assert.Same(field, ToolTipService.CurrentOwner);

        // Closing it returns the focus to where it was.
        ToolTipService.Close();
        Assert.True(other.IsFocused);
        Assert.False(picker.HexBox.IsFocused);
        root.DetachFromHost();
    }

    [Fact]
    public void Tab_InsideAnOwnedPopup_CyclesWithinIt()
    {
        var picker = new ColorPicker();
        var field = new Button("Field").ToolTip(picker);
        var root = new StackPanel().Children(field, new Button("Other"));
        root.AttachToHost();
        root.Measure(new Size(800, 600));
        root.Arrange(new Rect(0, 0, 800, 600));

        ToolTipService.Show(field);
        picker.HexBox.Focus();
        for (int i = 0; i < 20; i++)
        {
            FocusManager.FocusNext(root);
            Assert.True(FocusManager.GetFocusedElement(root)!.IsDescendantOf(picker));
        }
        root.DetachFromHost();
    }

    [Fact]
    public void DraggingASliderInAPopup_FollowsThePointerOutsideIt()
    {
        var picker = new ColorPicker { Color = Color.Black };
        var field = new Button("Field").ToolTip(picker);
        var root = new StackPanel().Children(field);
        root.AttachToHost();
        root.Measure(new Size(800, 600));
        root.Arrange(new Rect(0, 0, 800, 600));
        ToolTipService.Show(field);
        var toolTip = ToolTipService.CurrentToolTip!;
        PopupManager.UpdatePopups(new Size(800, 600), root);

        var red = picker.GetChannelSlider(0);
        var start = red.PointToScreen(new Point(10, red.Bounds.Height / 2));
        Assert.True(PopupManager.HandleMouseDown(start, PointerButtons.Left, root: root));
        Assert.True(red.IsPointerCaptured);

        // Far to the right of the popup: still routed to the slider, which goes to its maximum.
        UIElement? hovered = null;
        Assert.True(PopupManager.HandleMouseMove(new Point(start.X + 2000, start.Y + 300), ref hovered, root: root));
        Assert.Equal(255, red.Value);

        PopupManager.HandleMouseUp(new Point(start.X + 2000, start.Y + 300), PointerButtons.Left, root: root);
        Assert.False(red.IsPointerCaptured);
        Assert.True(toolTip.IsOpen);
        root.DetachFromHost();
    }
}
