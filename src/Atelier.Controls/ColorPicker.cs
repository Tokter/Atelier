using System;
using System.Globalization;
using Atelier.Core.Events;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Layout;

namespace Atelier.Controls;

/// <summary>The color model whose channels a <see cref="ColorPicker"/> shows as sliders.</summary>
public enum ColorPickerMode
{
    /// <summary>Red, green and blue, each from 0 to 255.</summary>
    Rgb,

    /// <summary>Hue (0–360°), saturation and lightness (0–100%).</summary>
    Hsl,

    /// <summary>Hue (0–360°), saturation and brightness (0–100%), also called HSV.</summary>
    Hsb,
}

/// <summary>
/// Picks a color with a color wheel, channel sliders in RGB, HSL or HSB, an alpha slider and a hex text box.
/// </summary>
/// <remarks>
/// <para>
/// The wheel picks hue and saturation; the sliders of the current <see cref="Mode"/> each show the gradient of colors
/// they can reach from the current color, and the number box next to each slider accepts typed values (applied on Enter
/// or when the box loses focus). The hex box accepts <c>#RGB</c>, <c>#RRGGBB</c> and <c>#AARRGGBB</c>; complete
/// six- or eight-digit input applies while typing. The hex text omits the alpha while the color is opaque.
/// </para>
/// <para>
/// The picker keeps hue and saturation while they have no effect, so for example dragging the brightness to black and
/// back returns to the same hue. <see cref="ColorChanged"/> is raised for every change, including during drags.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// new ColorPicker().Color(Color.FromHex("#6750A4")).Mode(ColorPickerMode.Hsl).OnColorChanged(c => preview.Background = c);
/// </code>
/// </example>
public class ColorPicker : Control
{
    /// <summary>Identifies the <see cref="Color"/> property.</summary>
    public static readonly BindableProperty<Color> ColorProperty =
        BindableProperty.Register<ColorPicker, Color>(nameof(Color), Color.White, (s, o, n) => ((ColorPicker)s).OnColorChanged(n));

    /// <summary>Identifies the <see cref="Mode"/> property.</summary>
    public static readonly BindableProperty<ColorPickerMode> ModeProperty =
        BindableProperty.Register<ColorPicker, ColorPickerMode>(nameof(Mode), ColorPickerMode.Rgb, (s, o, n) => ((ColorPicker)s).OnModeChanged());

    /// <summary>Identifies the <see cref="IsAlphaEnabled"/> property.</summary>
    public static readonly BindableProperty<bool> IsAlphaEnabledProperty =
        BindableProperty.Register<ColorPicker, bool>(nameof(IsAlphaEnabled), true, (s, o, n) => ((ColorPicker)s).OnIsAlphaEnabledChanged(n));

    /// <summary>The width the picker takes when nothing constrains it.</summary>
    public const float DefaultWidth = 272f;

    // The picked color as HSV and alpha, all from 0 to 1 except the hue (0 to 360); the source of truth, so hue and
    // saturation survive while they don't show in the color (grays, black).
    private float _hue, _saturation, _value = 1f, _alpha = 1f;

    private readonly ColorWheel _wheel = new();
    private readonly ColorSwatch _preview = new() { Height = 56, CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _hexBox;
    private readonly ToggleButton[] _modeButtons = new ToggleButton[3];
    private readonly Channel[] _channels = new Channel[4]; // three of the mode, then alpha
    private bool _isSyncing;
    private bool _isSettingColor;

    private sealed class Channel
    {
        public required TextBlock Label { get; init; }
        public required ColorSlider Slider { get; init; }
        public required TextBox Box { get; init; }
        public UIElement[] Row => [Label, Slider, Box];
    }

    /// <summary>Initializes a picker showing white.</summary>
    public ColorPicker()
    {
        _wheel.VerticalAlignment = VerticalAlignment.Top;
        _wheel.ColorChanged += (_, _) => OnWheelChanged();

        _hexBox = CreateBox();
        _hexBox.MaxLength = 9;
        _hexBox.TextChanged += (_, text) => OnHexTyped(text);
        _hexBox.LostFocus += (_, _) => CommitHex();
        _hexBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) CommitHex();
        };

        var side = new StackPanel { Spacing = 8, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        side.WithChildren(_preview, new TextBlock { Text = "Hex" }, _hexBox);

        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        top.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        Grid.SetColumn(side, 1);
        top.Add(_wheel);
        top.Add(side);

        var modes = new UniformGrid { Columns = 3 };
        string[] names = ["RGB", "HSL", "HSB"];
        for (int i = 0; i < 3; i++)
        {
            var mode = (ColorPickerMode)i;
            var button = new ToggleButton(names[i])
            {
                Padding = new Thickness(8, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius = i switch
                {
                    0 => new CornerRadius(16, 0, 0, 16),
                    2 => new CornerRadius(0, 16, 16, 0),
                    _ => CornerRadius.Zero
                },
            };
            button.Click += (_, _) =>
            {
                Mode = mode;
                SyncModeButtons(); // clicking the selected mode unchecked it
            };
            _modeButtons[i] = button;
            modes.Add(button);
        }

        var channelGrid = new Grid { RowSpacing = 4, ColumnSpacing = 8 };
        channelGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Pixels(14)));
        channelGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        channelGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Pixels(52)));
        for (int i = 0; i < _channels.Length; i++)
        {
            int index = i;
            var channel = new Channel
            {
                Label = new TextBlock { VerticalAlignment = VerticalAlignment.Center },
                Slider = new ColorSlider { HorizontalAlignment = HorizontalAlignment.Stretch, IsSnapToTickEnabled = true, TickFrequency = 1f },
                Box = CreateBox(),
            };
            channel.Slider.ValueChanged += (_, value) => OnSliderChanged(index, value);
            channel.Box.LostFocus += (_, _) => CommitBox(index);
            channel.Box.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) CommitBox(index);
            };

            channelGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (int column = 0; column < 3; column++)
            {
                var element = channel.Row[column];
                Grid.SetRow(element, i);
                Grid.SetColumn(element, column);
                channelGrid.Add(element);
            }
            _channels[i] = channel;
        }

        var alpha = _channels[3];
        alpha.Label.Text = "A";
        alpha.Slider.Minimum = 0;
        alpha.Slider.Maximum = 100;
        alpha.Slider.ShowsTransparency = true;

        AddChild(new StackPanel { Spacing = 12 }.WithChildren(top, modes, channelGrid));

        OnModeChanged();
        UpdateControls(source: null);
    }

    /// <summary>Gets or sets the picked color. The default is white.</summary>
    public Color Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    /// <summary>Gets or sets the color model of the channel sliders. The default is <see cref="ColorPickerMode.Rgb"/>.</summary>
    public ColorPickerMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }

    /// <summary>
    /// Gets or sets whether the alpha slider is shown. Without it the alpha of <see cref="Color"/> can still be set from
    /// code or with the hex box. The default is <c>true</c>.
    /// </summary>
    public bool IsAlphaEnabled { get => GetValue(IsAlphaEnabledProperty); set => SetValue(IsAlphaEnabledProperty, value); }

    /// <summary>Occurs when <see cref="Color"/> changes, with the new color.</summary>
    public event EventHandler<Color>? ColorChanged;

    /// <summary>Gets the wheel that picks hue and saturation.</summary>
    public ColorWheel Wheel => _wheel;

    /// <summary>Gets the hex text box.</summary>
    public TextBox HexBox => _hexBox;

    /// <summary>
    /// Gets the slider of channel <paramref name="index"/>: 0 to 2 are the channels of <see cref="Mode"/> (for example
    /// red, green, blue), 3 is alpha.
    /// </summary>
    public ColorSlider GetChannelSlider(int index) => _channels[index].Slider;

    /// <summary>Gets the number box next to the slider of channel <paramref name="index"/> (see <see cref="GetChannelSlider"/>).</summary>
    public TextBox GetChannelBox(int index) => _channels[index].Box;

    /// <summary>Formats a color as hex: <c>#RRGGBB</c> when opaque, otherwise <c>#AARRGGBB</c>.</summary>
    public static string ToHex(Color color) =>
        color.A == 255 ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : color.ToString();

    private static TextBox CreateBox() => new()
    {
        Height = 32,
        CornerRadius = new CornerRadius(4),
        Padding = new Thickness(8, 4),
        VerticalAlignment = VerticalAlignment.Center,
    };

    #region State

    private Color StateColor => Color.FromHsv(_hue, _saturation, _value, _alpha);

    private void OnColorChanged(Color color)
    {
        if (!_isSettingColor)
        {
            // Set from outside: take it over, keeping hue and saturation where the color doesn't define them.
            color.ToHsv(out float h, out float s, out float v);
            if (v > 0)
            {
                if (s > 0) _hue = h;
                _saturation = s;
            }
            _value = v;
            _alpha = color.Af;
            UpdateControls(source: null);
        }
        ColorChanged?.Invoke(this, color);
    }

    // Applies the state changed by one of the controls to Color and the other controls.
    private void Apply(object? source)
    {
        _isSettingColor = true;
        try
        {
            Color = StateColor;
        }
        finally
        {
            _isSettingColor = false;
        }
        UpdateControls(source);
    }

    private void OnWheelChanged()
    {
        if (_isSyncing) return;
        _hue = _wheel.Hue;
        _saturation = _wheel.Saturation;
        if (_value <= 0f)
        {
            _value = 1f; // on black the wheel would pick nothing visible
        }
        Apply(_wheel);
    }

    private void OnSliderChanged(int index, float value)
    {
        if (_isSyncing) return;

        if (index == 3)
        {
            _alpha = value / 100f;
        }
        else
        {
            switch (Mode)
            {
                case ColorPickerMode.Rgb:
                    var rgb = GetRgb();
                    rgb[index] = value / 255f;
                    SetFromRgb(rgb[0], rgb[1], rgb[2]);
                    break;
                case ColorPickerMode.Hsl:
                    Color.HsvToHsl(_saturation, _value, out float hslS, out float lightness);
                    if (index == 0) _hue = value;
                    else if (index == 1) hslS = value / 100f;
                    else lightness = value / 100f;
                    Color.HslToHsv(hslS, lightness, out float s, out float v);
                    if (v > 0) _saturation = s; // black keeps its saturation, as in SetFromRgb
                    _value = v;
                    break;
                default:
                    if (index == 0) _hue = value;
                    else if (index == 1) _saturation = value / 100f;
                    else _value = value / 100f;
                    break;
            }
        }
        Apply(_channels[index].Slider);
    }

    private float[] GetRgb()
    {
        var c = Color.FromHsv(_hue, _saturation, _value);
        return [c.Rf, c.Gf, c.Bf];
    }

    private void SetFromRgb(float r, float g, float b)
    {
        Color.FromRgba(r, g, b).ToHsv(out float h, out float s, out float v);
        if (v > 0)
        {
            if (s > 0) _hue = h;
            _saturation = s;
        }
        _value = v;
    }

    private void OnHexTyped(string text)
    {
        if (_isSyncing) return;
        var digits = text.AsSpan().Trim().TrimStart('#');
        if ((digits.Length == 6 || digits.Length == 8) && Color.TryParseHex(digits, out var color))
        {
            SetFromOutside(color, _hexBox);
        }
    }

    private void CommitHex()
    {
        if (Color.TryParseHex(_hexBox.Text.AsSpan(), out var color))
        {
            SetFromOutside(color, source: null);
        }
        else
        {
            UpdateControls(source: null); // restore the text
        }
    }

    // Takes over a complete color typed by the user, like a Color set from code but updating the other controls only.
    private void SetFromOutside(Color color, object? source)
    {
        color.ToHsv(out float h, out float s, out float v);
        if (v > 0)
        {
            if (s > 0) _hue = h;
            _saturation = s;
        }
        _value = v;
        _alpha = color.Af;
        Apply(source);
    }

    private void CommitBox(int index)
    {
        var channel = _channels[index];
        string text = channel.Box.Text.Trim().TrimEnd('%', '°');
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out float value) ||
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            float clamped = Math.Clamp(value, channel.Slider.Minimum, channel.Slider.Maximum);
            if (clamped != channel.Slider.Value)
            {
                channel.Slider.Value = clamped; // applies through OnSliderChanged
                return;
            }
        }
        ShowText(channel.Box, FormatChannel(channel.Slider.Value));
    }

    #endregion

    #region Mode and display

    private void OnModeChanged()
    {
        string[] labels;
        float[] maxima;
        switch (Mode)
        {
            case ColorPickerMode.Rgb:
                labels = ["R", "G", "B"];
                maxima = [255, 255, 255];
                break;
            case ColorPickerMode.Hsl:
                labels = ["H", "S", "L"];
                maxima = [360, 100, 100];
                break;
            default:
                labels = ["H", "S", "B"];
                maxima = [360, 100, 100];
                break;
        }

        _isSyncing = true;
        try
        {
            for (int i = 0; i < 3; i++)
            {
                _channels[i].Label.Text = labels[i];
                _channels[i].Slider.Maximum = maxima[i];
                _channels[i].Slider.LargeChange = maxima[i] / 10f;
            }
        }
        finally
        {
            _isSyncing = false;
        }

        SyncModeButtons();
        UpdateControls(source: null);
    }

    private void SyncModeButtons()
    {
        for (int i = 0; i < _modeButtons.Length; i++)
        {
            _modeButtons[i].IsChecked = (int)Mode == i;
        }
    }

    private void OnIsAlphaEnabledChanged(bool isEnabled)
    {
        var visibility = isEnabled ? Visibility.Visible : Visibility.Collapsed;
        foreach (var element in _channels[3].Row)
        {
            element.Visibility = visibility;
        }
    }

    // Shows the state in every control except the source of the change (a text box being typed in keeps its text).
    private void UpdateControls(object? source)
    {
        _isSyncing = true;
        try
        {
            Color color = StateColor;
            Color opaque = color.WithAlpha((byte)255);

            if (source != _wheel)
            {
                _wheel.Hue = _hue;
                _wheel.Saturation = _saturation;
            }
            _wheel.Brightness = _value;
            _preview.Color = color;

            if (source != _hexBox)
            {
                ShowText(_hexBox, ToHex(color));
            }

            UpdateChannelSliders(opaque);

            var alpha = _channels[3];
            alpha.Slider.Value = _alpha * 100f;
            alpha.Slider.TrackColors = [opaque.WithAlpha((byte)0), opaque];
            alpha.Slider.ThumbColor = color;

            foreach (var channel in _channels)
            {
                ShowText(channel.Box, FormatChannel(channel.Slider.Value));
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void UpdateChannelSliders(Color opaque)
    {
        var (s0, s1, s2) = (_channels[0].Slider, _channels[1].Slider, _channels[2].Slider);
        switch (Mode)
        {
            case ColorPickerMode.Rgb:
                s0.Value = opaque.R;
                s1.Value = opaque.G;
                s2.Value = opaque.B;
                s0.TrackColors = [Color.FromRgb(0, opaque.G, opaque.B), Color.FromRgb(255, opaque.G, opaque.B)];
                s1.TrackColors = [Color.FromRgb(opaque.R, 0, opaque.B), Color.FromRgb(opaque.R, 255, opaque.B)];
                s2.TrackColors = [Color.FromRgb(opaque.R, opaque.G, 0), Color.FromRgb(opaque.R, opaque.G, 255)];
                break;

            case ColorPickerMode.Hsl:
                Color.HsvToHsl(_saturation, _value, out float saturation, out float lightness);
                s0.Value = _hue;
                s1.Value = saturation * 100f;
                s2.Value = lightness * 100f;
                s0.TrackColors = HueSteps(h => Color.FromHsl(h, saturation, lightness));
                s1.TrackColors = [Color.FromHsl(_hue, 0, lightness), Color.FromHsl(_hue, 1, lightness)];
                s2.TrackColors = [Color.Black, Color.FromHsl(_hue, saturation, 0.5f), Color.White];
                break;

            default:
                s0.Value = _hue;
                s1.Value = _saturation * 100f;
                s2.Value = _value * 100f;
                s0.TrackColors = HueSteps(h => Color.FromHsv(h, _saturation, _value));
                s1.TrackColors = [Color.FromHsv(_hue, 0, _value), Color.FromHsv(_hue, 1, _value)];
                s2.TrackColors = [Color.Black, Color.FromHsv(_hue, _saturation, 1)];
                break;
        }

        s0.ThumbColor = s1.ThumbColor = s2.ThumbColor = opaque;
    }

    // The hue gradient in 30° steps, from red around to red.
    private static Color[] HueSteps(Func<float, Color> colorAt)
    {
        var steps = new Color[13];
        for (int i = 0; i < steps.Length; i++)
        {
            steps[i] = colorAt(i * 30f);
        }
        return steps;
    }

    // Replaces a box's text; a focused box keeps its caret at the end, where the user was typing.
    private static void ShowText(TextBox box, string text)
    {
        if (box.Text == text) return;
        box.Text = text;
        if (box.IsFocused)
        {
            box.CaretIndex = text.Length;
        }
    }

    private static string FormatChannel(float value) => MathF.Round(value).ToString(CultureInfo.CurrentCulture);

    #endregion

    /// <inheritdoc/>
    /// <remarks><see cref="DefaultWidth"/> wide, or narrower when less space is available.</remarks>
    protected override Size MeasureOverride(Size availableSize)
    {
        float width = Math.Min(DefaultWidth, availableSize.Width);
        var size = base.MeasureOverride(new Size(width, availableSize.Height));
        return new Size(width, size.Height);
    }
}
