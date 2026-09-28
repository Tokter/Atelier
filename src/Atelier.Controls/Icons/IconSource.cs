using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Atelier.Core.Primitives;
using SkiaSharp;

namespace Atelier.Controls;

/// <summary>
/// Turns icon names into icons: a <see cref="MaterialIconKind"/> name (<c>"DarkMode"</c>), SVG path data
/// (<c>"M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z"</c>) or a whole SVG document (<c>"&lt;svg viewBox="0 0 24 24"&gt;…&lt;/svg&gt;"</c>),
/// such as the icon of a command (see <c>CommandAttribute.Icon</c>).
/// </summary>
/// <remarks>
/// <para>
/// SVG documents keep their proportions: the icon scales the <c>viewBox</c> (or the width and height) into its size, so
/// the padding of a 24×24 icon is kept. The shapes <c>path</c>, <c>circle</c>, <c>ellipse</c>, <c>rect</c>, <c>line</c>,
/// <c>polyline</c> and <c>polygon</c> are read, inside groups too, with their <c>transform</c>, <c>fill</c>,
/// <c>fill-rule</c> and <c>stroke</c> attributes (also in <c>style</c>). Stroked shapes, as in outline icon sets, become
/// filled outlines with their <c>stroke-width</c>, <c>stroke-linecap</c> and <c>stroke-linejoin</c>. Icons are drawn in
/// one color, so the colors themselves are ignored.
/// </para>
/// <para>Parsed geometry is cached per string and shared; don't dispose it.</para>
/// </remarks>
public static class IconSource
{
    private static readonly ConcurrentDictionary<string, (SKPath? Path, Rect? ViewBox)> s_geometry = new(StringComparer.Ordinal);

    /// <summary>
    /// Resolves <paramref name="source"/> into a glyph or geometry.
    /// </summary>
    /// <param name="source">A <see cref="MaterialIconKind"/> name (an exact match first, else ignoring case), SVG path data or an SVG document.</param>
    /// <param name="kind">The glyph, or <see cref="MaterialIconKind.None"/> for geometry.</param>
    /// <param name="path">The geometry (shared, not to be disposed), or <c>null</c> for a glyph.</param>
    /// <param name="viewBox">The area of <paramref name="path"/>'s coordinates the icon shows, or <c>null</c> to fit the path's bounds.</param>
    /// <returns><c>false</c> if <paramref name="source"/> is empty or not a valid icon.</returns>
    public static bool TryResolve(string? source, out MaterialIconKind kind, out SKPath? path, out Rect? viewBox)
    {
        kind = MaterialIconKind.None;
        path = null;
        viewBox = null;
        if (string.IsNullOrWhiteSpace(source)) return false;

        string text = source.Trim();
        char first = text[0];
        // An exact name first: a few icons differ only in case ("Addchart" and "AddChart").
        if (first != '<' && !char.IsDigit(first) && first != '-' && first != '+'
            && (Enum.TryParse(text, ignoreCase: false, out MaterialIconKind parsed) || Enum.TryParse(text, ignoreCase: true, out parsed))
            && parsed != MaterialIconKind.None)
        {
            kind = parsed;
            return true;
        }

        (path, viewBox) = s_geometry.GetOrAdd(text, static t => t[0] == '<' ? ParseDocument(t) : (ParsePathData(t), null));
        if (path == null)
        {
            Debug.WriteLine($"[Icon] Not a Material icon name, SVG path data or SVG document: '{(text.Length > 60 ? text[..60] + "…" : text)}'");
        }
        return path != null;
    }

    /// <summary>Gets whether <paramref name="source"/> resolves to an icon (see <see cref="TryResolve"/>).</summary>
    public static bool IsValid(string? source) => TryResolve(source, out _, out _, out _);

    private static SKPath? ParsePathData(string data)
    {
        if (data[0] is not ('M' or 'm')) return null;
        try
        {
            var path = SKPath.ParseSvgPathData(data);
            return path is { IsEmpty: false } ? path : null;
        }
        catch
        {
            return null;
        }
    }

    #region SVG documents

    private sealed record Style(bool Fill, SKPathFillType FillRule, bool Stroke, float StrokeWidth, SKStrokeCap Cap, SKStrokeJoin Join);

    private static (SKPath? Path, Rect? ViewBox) ParseDocument(string svg)
    {
        XElement root;
        try
        {
            root = XElement.Parse(svg, LoadOptions.None);
        }
        catch (XmlException)
        {
            return (null, null);
        }
        if (root.Name.LocalName != "svg") return (null, null);

        SKPath? result = null;
        var style = new Style(true, SKPathFillType.Winding, false, 1, SKStrokeCap.Butt, SKStrokeJoin.Miter);
        AddElement(root, style, SKMatrix.Identity, ref result);
        if (result is not { IsEmpty: false })
        {
            result?.Dispose();
            return (null, null);
        }
        return (result, ReadViewBox(root));
    }

    private static Rect? ReadViewBox(XElement root)
    {
        if (Attribute(root, "viewBox") is { } viewBox)
        {
            var numbers = Numbers(viewBox);
            if (numbers.Count == 4 && numbers[2] > 0 && numbers[3] > 0)
            {
                return new Rect(numbers[0], numbers[1], numbers[2], numbers[3]);
            }
        }
        float width = Length(Attribute(root, "width")), height = Length(Attribute(root, "height"));
        return width > 0 && height > 0 ? new Rect(0, 0, width, height) : null;
    }

    private static void AddElement(XElement element, Style inherited, SKMatrix inheritedTransform, ref SKPath? result)
    {
        string name = element.Name.LocalName;
        if (name is "defs" or "clipPath" or "mask" or "symbol" or "title" or "desc" or "metadata" or "style" or "linearGradient" or "radialGradient" or "pattern")
        {
            return;
        }
        if (Attribute(element, "display") == "none" || Attribute(element, "visibility") == "hidden") return;

        var style = ReadStyle(element, inherited);
        var transform = Attribute(element, "transform") is { } t ? inheritedTransform.PreConcat(ParseTransform(t)) : inheritedTransform;

        if (name is "svg" or "g" or "a")
        {
            foreach (var child in element.Elements()) AddElement(child, style, transform, ref result);
            return;
        }

        using var untransformed = CreateShape(element);
        if (untransformed is not { IsEmpty: false }) return;
        using var builder = new SKPathBuilder { FillType = style.FillRule };
        builder.AddPath(untransformed, transform);
        using var shape = builder.Detach();

        if (style.Fill && name is not ("line" or "polyline"))
        {
            Union(ref result, shape);
        }
        if (style.Stroke && style.StrokeWidth > 0)
        {
            // The stroke width scales with the transform, as in SVG.
            float scale = MathF.Sqrt(MathF.Abs(transform.ScaleX * transform.ScaleY - transform.SkewX * transform.SkewY));
            using var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = style.StrokeWidth * (scale > 0 ? scale : 1),
                StrokeCap = style.Cap,
                StrokeJoin = style.Join,
            };
            using var outline = new SKPathBuilder();
            if (paint.GetFillPath(shape, outline))
            {
                using var outlinePath = outline.Detach();
                Union(ref result, outlinePath);
            }
        }
    }

    // Adds a shape so that overlaps stay filled whatever the fill rules of the parts.
    private static void Union(ref SKPath? result, SKPath shape)
    {
        var union = result == null ? Copy(shape) : result.Op(shape, SKPathOp.Union);
        if (union == null)
        {
            // The boolean operation failed: keep both shapes as they are.
            using var both = new SKPathBuilder();
            both.AddPath(result!);
            both.AddPath(shape);
            union = both.Detach();
        }
        result?.Dispose();
        result = union;
    }

    private static SKPath Copy(SKPath path)
    {
        using var builder = new SKPathBuilder { FillType = path.FillType };
        builder.AddPath(path);
        return builder.Detach();
    }

    private static SKPath? CreateShape(XElement element)
    {
        float F(string attribute) => Length(Attribute(element, attribute));
        switch (element.Name.LocalName)
        {
            case "path":
                return Attribute(element, "d") is { } d ? ParsePathOrNull(d) : null;
            case "circle":
            {
                float r = F("r");
                if (r <= 0) return null;
                using var path = new SKPathBuilder();
                path.AddCircle(F("cx"), F("cy"), r);
                return path.Detach();
            }
            case "ellipse":
            {
                float rx = F("rx"), ry = F("ry");
                if (rx <= 0 || ry <= 0) return null;
                using var path = new SKPathBuilder();
                path.AddOval(SKRect.Create(F("cx") - rx, F("cy") - ry, rx * 2, ry * 2));
                return path.Detach();
            }
            case "rect":
            {
                float width = F("width"), height = F("height");
                if (width <= 0 || height <= 0) return null;
                float rx = F("rx"), ry = F("ry");
                if (rx <= 0) rx = ry;
                if (ry <= 0) ry = rx;
                using var path = new SKPathBuilder();
                var rect = SKRect.Create(F("x"), F("y"), width, height);
                if (rx > 0) path.AddRoundRect(rect, Math.Min(rx, width / 2), Math.Min(ry, height / 2));
                else path.AddRect(rect);
                return path.Detach();
            }
            case "line":
            {
                using var path = new SKPathBuilder();
                path.MoveTo(F("x1"), F("y1"));
                path.LineTo(F("x2"), F("y2"));
                return path.Detach();
            }
            case "polyline" or "polygon":
            {
                var numbers = Numbers(Attribute(element, "points") ?? string.Empty);
                if (numbers.Count < 4) return null;
                using var path = new SKPathBuilder();
                path.MoveTo(numbers[0], numbers[1]);
                for (int i = 2; i + 1 < numbers.Count; i += 2) path.LineTo(numbers[i], numbers[i + 1]);
                if (element.Name.LocalName == "polygon") path.Close();
                return path.Detach();
            }
            default:
                return null;
        }
    }

    private static SKPath? ParsePathOrNull(string data)
    {
        try
        {
            return SKPath.ParseSvgPathData(data);
        }
        catch
        {
            return null;
        }
    }

    private static Style ReadStyle(XElement element, Style inherited)
    {
        var style = inherited;
        string? Get(string name) => StyleProperty(element, name) ?? Attribute(element, name);

        if (Get("fill") is { } fill) style = style with { Fill = fill != "none" && fill != "transparent" };
        if (Get("fill-rule") is { } rule) style = style with { FillRule = rule == "evenodd" ? SKPathFillType.EvenOdd : SKPathFillType.Winding };
        if (Get("stroke") is { } stroke) style = style with { Stroke = stroke != "none" && stroke != "transparent" };
        if (Get("stroke-width") is { } width) style = style with { StrokeWidth = Length(width) };
        if (Get("stroke-linecap") is { } cap)
        {
            style = style with { Cap = cap switch { "round" => SKStrokeCap.Round, "square" => SKStrokeCap.Square, _ => SKStrokeCap.Butt } };
        }
        if (Get("stroke-linejoin") is { } join)
        {
            style = style with { Join = join switch { "round" => SKStrokeJoin.Round, "bevel" => SKStrokeJoin.Bevel, _ => SKStrokeJoin.Miter } };
        }
        return style;
    }

    private static string? StyleProperty(XElement element, string name)
    {
        if (Attribute(element, "style") is not { } style) return null;
        foreach (var declaration in style.Split(';'))
        {
            int colon = declaration.IndexOf(':');
            if (colon > 0 && declaration[..colon].Trim() == name) return declaration[(colon + 1)..].Trim();
        }
        return null;
    }

    private static string? Attribute(XElement element, string name) => element.Attribute(name)?.Value.Trim();

    // A length in user units; units such as px are ignored, percentages aren't supported.
    private static float Length(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        int end = 0;
        while (end < text.Length && (char.IsDigit(text[end]) || text[end] is '.' or '-' or '+' or 'e' or 'E')) end++;
        return float.TryParse(text.AsSpan(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : 0;
    }

    private static List<float> Numbers(string text)
    {
        var numbers = new List<float>();
        foreach (var part in text.Split([' ', ',', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (float.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) numbers.Add(value);
        }
        return numbers;
    }

    // translate, scale, rotate, skewX, skewY and matrix, applied left to right as in SVG.
    private static SKMatrix ParseTransform(string text)
    {
        var result = SKMatrix.Identity;
        int index = 0;
        while (index < text.Length)
        {
            int open = text.IndexOf('(', index);
            int close = open < 0 ? -1 : text.IndexOf(')', open);
            if (open < 0 || close < 0) break;
            string function = text[index..open].Trim(' ', ',', '\t', '\n', '\r');
            var a = Numbers(text[(open + 1)..close]);
            float A(int i, float fallback = 0) => i < a.Count ? a[i] : fallback;
            var matrix = function switch
            {
                "translate" => SKMatrix.CreateTranslation(A(0), A(1)),
                "scale" => SKMatrix.CreateScale(A(0, 1), A(1, A(0, 1))),
                "rotate" => SKMatrix.CreateRotationDegrees(A(0), A(1), A(2)),
                "skewX" => SKMatrix.CreateSkew(MathF.Tan(A(0) * MathF.PI / 180), 0),
                "skewY" => SKMatrix.CreateSkew(0, MathF.Tan(A(0) * MathF.PI / 180)),
                "matrix" when a.Count == 6 => new SKMatrix(a[0], a[2], a[4], a[1], a[3], a[5], 0, 0, 1),
                _ => SKMatrix.Identity,
            };
            result = result.PreConcat(matrix);
            index = close + 1;
        }
        return result;
    }

    #endregion
}
