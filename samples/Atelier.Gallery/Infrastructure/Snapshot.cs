using System;
using System.IO;
using Atelier.Core.Primitives;
using Atelier.Gallery.ViewModels;
using Atelier.Gallery.Views;
using Atelier.Rendering;
using Atelier.Theming;
using Atelier.Theming.Material;
using SkiaSharp;

namespace Atelier.Gallery.Infrastructure;

/// <summary>
/// Renders gallery pages offscreen into PNG files, without opening a window: a quick way to review every page in both
/// themes (and a fallback where no GPU window can be shown).
/// </summary>
/// <remarks>
/// Enabled with <c>ATELIER_GALLERY_SNAPSHOT=&lt;output directory&gt;</c>. Optional: <c>ATELIER_GALLERY_PAGES</c> (page
/// indexes, comma-separated; all by default), <c>ATELIER_GALLERY_THEME=dark</c>, and <c>ATELIER_GALLERY_SIZE</c>
/// (<c>WIDTHxHEIGHT</c>, 1280x860 by default; a taller size shows more of a page).
/// </remarks>
internal static class Snapshot
{
    public static bool TryRun()
    {
        string? outputDirectory = Environment.GetEnvironmentVariable("ATELIER_GALLERY_SNAPSHOT");
        if (string.IsNullOrEmpty(outputDirectory))
        {
            return false;
        }

        Directory.CreateDirectory(outputDirectory);
        bool dark = string.Equals(Environment.GetEnvironmentVariable("ATELIER_GALLERY_THEME"), "dark", StringComparison.OrdinalIgnoreCase);
        var theme = dark ? MaterialTheme.CreateDark() : MaterialTheme.CreateLight();
        ThemeManager.Current = theme;

        (int width, int height) = ParseSize(Environment.GetEnvironmentVariable("ATELIER_GALLERY_SIZE"));
        var viewModel = new MainViewModel();
        string? pageList = Environment.GetEnvironmentVariable("ATELIER_GALLERY_PAGES");
        int[] pages = string.IsNullOrWhiteSpace(pageList)
            ? System.Linq.Enumerable.Range(0, viewModel.Pages.Count).ToArray()
            : Array.ConvertAll(pageList.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), int.Parse);

        using var registry = new PaintRegistry();
        foreach (int page in pages)
        {
            viewModel.CurrentPage = viewModel.Pages[page];
            var root = new MainView(viewModel);
            root.AttachToHost();
            root.ApplyStylesToTree();
            root.UseLayoutRounding = true;

            // Two passes: the first layout can change content (e.g. item containers) that the second one measures.
            for (int pass = 0; pass < 2; pass++)
            {
                root.Measure(new Size(width, height));
                root.Arrange(new Rect(0, 0, width, height));
            }

            using var bitmap = new SKBitmap(width, height);
            using (var canvas = new SKCanvas(bitmap))
            {
                var background = theme.Colors.Surface;
                canvas.Clear(new SKColor(background.R, background.G, background.B, background.A));
                var context = new DrawingContext(canvas, registry);
                VisualTreeRenderer.Render(root, ref context, ThemeVisualPresenter.Instance);
            }

            string file = Path.Combine(outputDirectory, $"page{page}{(dark ? "-dark" : "")}.png");
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(file);
            data.SaveTo(stream);
            Console.WriteLine($"Saved {file}");
        }

        return true;
    }

    private static (int Width, int Height) ParseSize(string? size)
    {
        var parts = size?.Split('x', 'X');
        return parts is { Length: 2 } && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h) ? (w, h) : (1280, 860);
    }
}
