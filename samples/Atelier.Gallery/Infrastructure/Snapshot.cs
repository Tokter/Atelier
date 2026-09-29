using System;
using System.IO;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Tree;
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
/// (<c>WIDTHxHEIGHT</c>, 1280x860 by default; a taller size shows more of a page), <c>ATELIER_GALLERY_TOOLTIP</c> and
/// <c>ATELIER_GALLERY_FILE_DIALOG</c> and <c>ATELIER_GALLERY_COMMAND_EDITOR</c> (see below), and <c>ATELIER_GALLERY_SEED</c>
/// (<c>#RRGGBB[:Variant]</c>, a generated theme).
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
        // ATELIER_GALLERY_SEED=#RRGGBB[:Variant] generates the schemes from an accent color, as the Theme Editor does.
        if (Environment.GetEnvironmentVariable("ATELIER_GALLERY_SEED") is { Length: > 0 } seedText)
        {
            var parts = seedText.Split(':');
            var seed = Color.FromHex(parts[0]);
            var variant = parts.Length > 1 ? Enum.Parse<MaterialSchemeVariant>(parts[1], ignoreCase: true) : MaterialSchemeVariant.TonalSpot;
            GalleryTheme.Generate(seed, variant);
        }
        GalleryTheme.IsDark = dark;
        GalleryTheme.Apply();

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

            // ATELIER_GALLERY_FILE_DIALOG=open|save|folder shows a file dialog over the page, limited to this repository
            // (its RootDirectory), so the picture shows no other folders of the machine.
            if (Environment.GetEnvironmentVariable("ATELIER_GALLERY_FILE_DIALOG") is { Length: > 0 } fileDialog && FindRepositoryRoot() is { } repository)
            {
                CommonItemDialog dialog = fileDialog switch
                {
                    "save" => new SaveFileDialog { Filter = "C# source file (*.cs)|*.cs|All files (*.*)|*.*", FileName = "DataGridView.cs" },
                    "folder" => new FolderBrowserDialog { Description = "Choose where the export goes." },
                    _ => new OpenFileDialog { Filter = "C# source (*.cs)|*.cs|Projects (*.csproj)|*.csproj|All files (*.*)|*.*", Multiselect = true },
                };
                dialog.RootDirectory = repository;
                dialog.InitialDirectory = Path.Combine(repository, "src", "Atelier.Controls");
                _ = dialog.ShowAsync(root);
                if (dialog is OpenFileDialog && dialog.View is { } view)
                {
                    var selected = System.Linq.Enumerable.FirstOrDefault(view.Entries, e => e.Name == "DataGrid.cs") ?? System.Linq.Enumerable.FirstOrDefault(view.Entries, e => !e.IsDirectory);
                    if (selected != null) view.FileList.SelectedItem = selected;
                }
                for (int pass = 0; pass < 2; pass++)
                {
                    root.Measure(new Size(width, height));
                    root.Arrange(new Rect(0, 0, width, height));
                }
            }

            // ATELIER_GALLERY_COMMAND_EDITOR=Group/Name opens the command editor dialog with that command (and its group) selected.
            if (Environment.GetEnvironmentVariable("ATELIER_GALLERY_COMMAND_EDITOR") is { Length: > 0 } command)
            {
                viewModel.CustomizeCommandsCommand.Execute(null);
                var parts = command.Split('/');
                if (parts.Length == 2 && Find<Dialog>(root) is { } dialog && Find<KeybindingEditor>(dialog) is { } editor)
                {
                    editor.ShowGroup(parts[0]);
                    editor.Select(parts[0], parts[1]);
                }
                for (int pass = 0; pass < 2; pass++)
                {
                    root.Measure(new Size(width, height));
                    root.Arrange(new Rect(0, 0, width, height));
                }
            }

            // ATELIER_GALLERY_TOOLTIP=n opens the tooltip of the page's n-th element (0-based) that has one.
            if (int.TryParse(Environment.GetEnvironmentVariable("ATELIER_GALLERY_TOOLTIP"), out int toolTipIndex)
                && FindToolTipOwner(root, ref toolTipIndex) is { } owner)
            {
                ToolTipService.Show(owner);
            }

            UIElement rendered = root;
#if DEBUG
            // ATELIER_GALLERY_DEVTOOLS=text[:tab] opens the developer tools (Debug builds), selects the first element
            // whose description contains the text, and shows the tab (0 Properties, 1 Values, 2 Layout, 3 Zoom).
            if (Environment.GetEnvironmentVariable("ATELIER_GALLERY_DEVTOOLS") is { Length: > 0 } devTools)
            {
                var host = new SnapshotDevToolsHost { Content = root };
                var session = Atelier.DevTools.DevToolsManager.Open(host)!;
                rendered = host.Content!;
                rendered.UseLayoutRounding = true;
                for (int pass = 0; pass < 2; pass++)
                {
                    rendered.Measure(new Size(width, height));
                    rendered.Arrange(new Rect(0, 0, width, height));
                }
                var parts = devTools.Split(':');
                session.Panel.FindNext(parts[0]);
                if (parts.Length > 1 && int.TryParse(parts[1], out int tab)) session.Panel.Tabs.SelectedIndex = tab;
                if (parts.Length > 2 && float.TryParse(parts[2], System.Globalization.CultureInfo.InvariantCulture, out float zoom)) session.Panel.Zoom.Zoom = zoom;
                for (int pass = 0; pass < 3; pass++)
                {
                    rendered.Measure(new Size(width, height));
                    rendered.Arrange(new Rect(0, 0, width, height));
                }
            }
#endif
            PopupManager.UpdatePopups(new Size(width, height), rendered);

            using var bitmap = new SKBitmap(width, height);
            using (var canvas = new SKCanvas(bitmap))
            {
                var background = GalleryTheme.Scheme(dark).Surface;
                canvas.Clear(new SKColor(background.R, background.G, background.B, background.A));
                var context = new DrawingContext(canvas, registry);
                VisualTreeRenderer.Render(rendered, ref context, ThemeVisualPresenter.Instance);
                PopupManager.RenderPopups(ref context, ThemeVisualPresenter.Instance, rendered);
            }
            ToolTipService.Close();

            string file = Path.Combine(outputDirectory, $"page{page}{(dark ? "-dark" : "")}.png");
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(file);
            data.SaveTo(stream);
            Console.WriteLine($"Saved {file}");
        }

        return true;
    }

    // The folder of the repository (the one with src/Atelier.Controls), from the gallery's folder upwards.
    private static string? FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "Atelier.Controls"))) return dir.FullName;
        }
        for (var dir = new DirectoryInfo(Environment.CurrentDirectory); dir != null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "Atelier.Controls"))) return dir.FullName;
        }
        return null;
    }

    // Depth-first search for the index-th element with a tooltip.
    private static T? Find<T>(VisualNode node) where T : class
    {
        if (node is T match) return match;
        foreach (var child in node.Children)
        {
            if (Find<T>(child) is { } found) return found;
        }
        return null;
    }

    private static UIElement? FindToolTipOwner(VisualNode node, ref int index)
    {
        if (node is UIElement element && ToolTipService.GetToolTip(element) != null && index-- == 0)
        {
            return element;
        }

        for (int i = 0; i < node.Children.Count; i++)
        {
            if (FindToolTipOwner(node.Children[i], ref index) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    private static (int Width, int Height) ParseSize(string? size)
    {
        var parts = size?.Split('x', 'X');
        return parts is { Length: 2 } && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h) ? (w, h) : (1280, 860);
    }
}

#if DEBUG
// The snapshot's root as a window for the developer tools.
internal sealed class SnapshotDevToolsHost : Atelier.DevTools.IDevToolsHost
{
    private UIElement? _content;

    public UIElement? Content
    {
        get => _content;
        set
        {
            _content?.DetachFromHost();
            _content = value;
            _content?.AttachToHost();
            _content?.ApplyStylesToTree();
        }
    }

    public bool ShowFpsOverlay { get; set; }

    public void InvalidateRender()
    {
    }
}
#endif
