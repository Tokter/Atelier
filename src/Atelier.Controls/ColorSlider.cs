using System;
using Atelier.Core.Primitives;

namespace Atelier.Controls;

/// <summary>
/// A <see cref="Slider"/> whose track shows a color gradient, such as the reds a red channel slider can pick. The
/// <see cref="ColorPicker"/> uses one per channel.
/// </summary>
/// <remarks>
/// It behaves like a slider (pointer, keyboard, range); only the look differs. The gradient runs through
/// <see cref="TrackColors"/>, spaced evenly from <see cref="Slider.Minimum"/> to <see cref="Slider.Maximum"/>, and the
/// thumb is filled with <see cref="ThumbColor"/>. The value indicator bubble is off by default.
/// </remarks>
public class ColorSlider : Slider
{
    private Color[] _trackColors = [Color.Black, Color.White];
    private Color _thumbColor = Color.White;
    private bool _showsTransparency;

    static ColorSlider()
    {
        ShowValueIndicatorProperty.OverrideDefaultValue<ColorSlider>(false);
    }

    /// <summary>
    /// Gets or sets the colors of the track's gradient, spaced evenly along it. The default runs from black to white.
    /// </summary>
    /// <exception cref="ArgumentException">The array is empty.</exception>
    public Color[] TrackColors
    {
        get => _trackColors;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Length == 0) throw new ArgumentException("A track needs at least one color.", nameof(value));
            _trackColors = value;
            InvalidateVisual();
        }
    }

    /// <summary>Gets or sets the color filling the thumb, usually the color picked so far. The default is white.</summary>
    public Color ThumbColor
    {
        get => _thumbColor;
        set
        {
            if (_thumbColor == value) return;
            _thumbColor = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Gets or sets whether a checkerboard shows through the track and thumb, for gradients with transparent colors such
    /// as an alpha slider. The default is <c>false</c>.
    /// </summary>
    public bool ShowsTransparency
    {
        get => _showsTransparency;
        set
        {
            if (_showsTransparency == value) return;
            _showsTransparency = value;
            InvalidateVisual();
        }
    }
}
