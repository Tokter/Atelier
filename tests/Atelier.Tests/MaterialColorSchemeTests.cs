using System;
using Atelier.Core.Primitives;
using Atelier.Theming.Material;
using Xunit;

namespace Atelier.Tests;

public class MaterialColorSchemeTests
{
    private static readonly string[] Seeds = ["#6750A4", "#0061A4", "#386A20", "#B3261E", "#FFB300", "#00BCD4", "#E91E63", "#808080", "#000000", "#FFFFFF"];

    // WCAG contrast ratio of two opaque colors.
    private static double Contrast(Color a, Color b)
    {
        static double Luminance(Color c)
        {
            static double Linear(byte v) => v / 255.0 <= 0.04045 ? v / 255.0 / 12.92 : Math.Pow((v / 255.0 + 0.055) / 1.055, 2.4);
            return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
        }
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    [Fact]
    public void TonalPalette_TonesHaveTheirLightness_AndTheEndsAreBlackAndWhite()
    {
        var palette = new MaterialTonalPalette(250, 60);
        Assert.Equal(Color.Black, palette.Tone(0));
        Assert.Equal(Color.White, palette.Tone(100));
        foreach (double tone in new[] { 10.0, 25, 40, 60, 80, 95 })
        {
            Assert.InRange(MaterialTonalPalette.ToneOf(palette.Tone(tone)), tone - 0.6, tone + 0.6);
        }
    }

    [Fact]
    public void TonalPalette_KeepsTheHue_AndFitsTooMuchChromaIntoSrgb()
    {
        var palette = new MaterialTonalPalette(140, 500); // far more chroma than any green has
        var color = palette.Tone(50);
        MaterialTonalPalette.ToLch(color, out double lightness, out double chroma, out double hue);
        Assert.InRange(lightness, 49.4, 50.6);
        Assert.InRange(hue, 137, 143);
        Assert.True(chroma > 40);
    }

    [Fact]
    public void FromSeed_TheBaselineSeedGivesTheBaselinePrimary()
    {
        MaterialTonalPalette.ToLch(MaterialColorScheme.FromSeed(Color.FromHex("#6750A4"), isDark: false).Primary, out double l, out double c, out double h);
        MaterialTonalPalette.ToLch(MaterialColorScheme.Light().Primary, out double bl, out double bc, out double bh);
        Assert.InRange(l, bl - 1, bl + 1);
        Assert.InRange(h, bh - 2, bh + 2);
        Assert.InRange(c, bc - 6, bc + 6);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FromSeed_OnColorsContrastWithTheirRoles(bool isDark)
    {
        foreach (var hex in Seeds)
        {
            foreach (var variant in Enum.GetValues<MaterialSchemeVariant>())
            {
                var s = MaterialColorScheme.FromSeed(Color.FromHex(hex), isDark, variant);
                (Color Role, Color On)[] pairs =
                [
                    (s.Primary, s.OnPrimary), (s.PrimaryContainer, s.OnPrimaryContainer),
                    (s.Secondary, s.OnSecondary), (s.SecondaryContainer, s.OnSecondaryContainer),
                    (s.Tertiary, s.OnTertiary), (s.TertiaryContainer, s.OnTertiaryContainer),
                    (s.Surface, s.OnSurface), (s.SurfaceContainerHighest, s.OnSurfaceVariant),
                    (s.Background, s.OnBackground), (s.Error, s.OnError),
                    (s.InverseSurface, s.InverseOnSurface), (s.Surface, s.Primary),
                ];
                foreach (var (role, on) in pairs)
                {
                    Assert.True(Contrast(role, on) >= 4.5, $"{hex} {variant} dark={isDark}: {on} on {role} is {Contrast(role, on):0.00}:1");
                }
            }
        }
    }

    [Fact]
    public void FromSeed_AccentsFollowTheSeedsHue_AndLightSchemesAreLight()
    {
        var light = MaterialColorScheme.FromSeed(Color.FromHex("#0061A4"), isDark: false);
        var dark = MaterialColorScheme.FromSeed(Color.FromHex("#0061A4"), isDark: true);
        MaterialTonalPalette.ToLch(Color.FromHex("#0061A4"), out _, out _, out double seedHue);
        MaterialTonalPalette.ToLch(light.Primary, out _, out _, out double hue);
        Assert.InRange(hue, seedHue - 3, seedHue + 3);
        Assert.True(MaterialTonalPalette.ToneOf(light.Surface) > 95);
        Assert.True(MaterialTonalPalette.ToneOf(dark.Surface) < 10);
        Assert.True(MaterialTonalPalette.ToneOf(dark.Primary) > MaterialTonalPalette.ToneOf(light.Primary));
    }

    [Fact]
    public void FromSeed_MonochromeIsGray()
    {
        var scheme = MaterialColorScheme.FromSeed(Color.FromHex("#E91E63"), isDark: false, MaterialSchemeVariant.Monochrome);
        foreach (var color in new[] { scheme.Primary, scheme.Surface, scheme.Tertiary })
        {
            Assert.InRange(Math.Abs(color.R - color.G) + Math.Abs(color.G - color.B), 0, 2);
        }
    }

    [Fact]
    public void Clone_CopiesEveryRole()
    {
        var scheme = MaterialColorScheme.Dark();
        var copy = scheme.Clone();
        Assert.NotSame(scheme, copy);
        foreach (var property in typeof(MaterialColorScheme).GetProperties())
        {
            Assert.Equal(property.GetValue(scheme), property.GetValue(copy));
        }
    }
}
