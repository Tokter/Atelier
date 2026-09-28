<p align="center">
  <img src="Assets/Logo.png" alt="Atelier logo" width="160" />
</p>

<h1 align="center">Atelier</h1>

<p align="center">
  <strong>A GPU-accelerated desktop UI framework for .NET 9, written in plain C#</strong><br>
  <em>SkiaSharp rendering · Silk.NET windowing · Material Design 3</em>
</p>

<p align="center">
  <a href="https://github.com/Tokter/Atelier/actions/workflows/ci.yml"><img src="https://github.com/Tokter/Atelier/actions/workflows/ci.yml/badge.svg" alt="CI" /></a>
  <img src="https://img.shields.io/badge/.NET-9.0-purple.svg" alt=".NET 9" />
  <img src="https://img.shields.io/badge/Rendering-SkiaSharp-blue.svg" alt="SkiaSharp" />
  <img src="https://img.shields.io/badge/Windowing-Silk.NET-green.svg" alt="Silk.NET" />
  <img src="https://img.shields.io/badge/Design-Material%203-teal.svg" alt="Material Design 3" />
  <img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="MIT License" />
</p>

<p align="center">
  <img src="docs/screenshots/selection-controls-light.png" alt="Atelier Gallery, light theme" width="49%" />
  <img src="docs/screenshots/selection-controls-dark.png" alt="Atelier Gallery, dark theme" width="49%" />
</p>

---

## Overview

Atelier is a retained-mode UI framework for desktop applications. You build the interface in C# with a fluent,
type-safe markup API instead of XAML, and every control is drawn with SkiaSharp on an OpenGL surface.

- **C# only.** No XAML, no CSS, no web view. Views are ordinary C# with IntelliSense, refactoring and hot reload.
- **Material Design 3 out of the box.** Light and dark color schemes, the MD3 type scale, default styles sized for
  desktop use, and 2,100+ Material Symbols with all four variable font axes.
- **A real property system.** Bindable properties with value precedence (default, inherited, style, local, animation,
  coerced), styles, attached properties, typed data binding and change notifications.
- **Built for low overhead.** Rendering reuses cached paints, fonts and text blobs, so a steady frame allocates nothing,
  and windows only redraw when something changed.
- **Reflection-free tooling.** Keybindings and property-grid metadata come from source generators.

---

## Screenshots

All screenshots show the included [Gallery](samples/Atelier.Gallery) application.

| | |
|---|---|
| ![Data grid](docs/screenshots/data-grid.png) | ![Open file dialog](docs/screenshots/file-dialog.png) |
| **Data grid**: 10,000 virtualized rows, sorting, filter menus, column chooser, cell editing | **File dialogs**: open, save and folder dialogs with a path bar, folder tree and file grid |
| ![Color picker, dark theme](docs/screenshots/color-picker-dark.png) | ![Date and time pickers, dark theme](docs/screenshots/date-time-pickers-dark.png) |
| **Color picker**: wheel, RGB/HSL/HSB gradient sliders, alpha and hex | **Date & time pickers**: MD3 docked and modal calendars, clock dial and text input |
| ![Tabs](docs/screenshots/tabs.png) | ![Buttons](docs/screenshots/buttons.png) |
| **Tabs**: MD3 primary and secondary tabs, closeable and reorderable browser tabs | **Buttons**: variants, commands, repeat and toggle buttons, toolbars |
| ![Text fields, dark theme](docs/screenshots/text-fields-dark.png) | ![Cards](docs/screenshots/cards.png) |
| **Text fields**: outlined and filled, labels, validation, binding | **Cards**: variants, elevation, shape, media and clipping |
| ![Icons, dark theme](docs/screenshots/icons-dark.png) | ![Typography](docs/screenshots/typography.png) |
| **Icons**: Material Symbols with fill, weight, grade and optical size | **Typography**: the MD3 type scale and a live playground |
| ![Layout panels, dark theme](docs/screenshots/layout-dark.png) | ![Popups and dialogs](docs/screenshots/popups-dialogs.png) |
| **Layout**: stack, wrap, dock, grid, uniform grid and canvas | **Popups & dialogs**: presets, custom content, scoped hosts |
| ![Property grid](docs/screenshots/property-grid.png) | ![Tooltips](docs/screenshots/tooltips.png) |
| **Property grid**: generated metadata, validation, custom editors | **Tooltips**: plain text, or rich content with clickable actions |

---

## Quick start

```csharp
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Layout;
using Atelier.Markup;
using Atelier.Platform.Silk;
using Atelier.Theming;
using Atelier.Theming.Material;
using CommunityToolkit.Mvvm.ComponentModel;

ThemeManager.Current = MaterialTheme.CreateLight();

var vm = new GreetingViewModel();
var window = new SilkWindow(title: "Hello Atelier", width: 640, height: 400);

// A factory lambda enables hot reload: the view is rebuilt from the same view model.
window.SetContent(() =>
    new StackPanel()
        .Spacing(16)
        .Margin(32)
        .Children(
            new TextBlock("Welcome to Atelier").HeadlineMedium(),
            new TextBox()
                .Label("Your name")
                .LeadingIconKind(MaterialIconKind.Person)
                .MaxWidth(320)
                .HorizontalAlignment(HorizontalAlignment.Left)
                .BindText(vm, v => v.Name, (v, name) => v.Name = name),
            new TextBlock().BodyLarge().BindText(vm, v => $"Hello, {v.Name}!"),
            new Button("Say hello")
                .HorizontalAlignment(HorizontalAlignment.Left)
                .OnClick(() => vm.Name = "World")));

window.Run();

public partial class GreetingViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "Atelier";
}
```

Reference `Atelier.Markup` and `Atelier.Platform.Silk`; they bring in the rest of the framework.

### The markup API

Every property has a fluent method with the same name that returns the element, so calls chain and keep their type:

```csharp
new Button("Save").Variant(ButtonVariant.Tonal).Command(vm.SaveCommand).IsEnabled(false);
new Grid().Columns("Auto,*").Spacing(12, 8).Children(label.Cell(0, 0), field.Cell(0, 1));
```

- `Bind{Property}(source, s => s.Value)` binds to an object; `Bind{Property}((MyViewModel vm) => vm.Value)` binds to
  the `DataContext`. An optional setter makes the binding two-way.
- `On{Event}(...)` attaches handlers; events without data also accept an `Action`.
- Controls get their size, shape and colors from the theme, so views only set what they change.

---

## Features

### Controls

| Control | Highlights |
|---|---|
| `Button`, `RepeatButton`, `ToggleButton` | Filled, tonal, elevated, outlined and text variants; commands with `CanExecute`; click modes; repeat delay and interval; two- and three-state toggles |
| `CheckBox`, `RadioButton`, `Switch` | Three states, rich content, named and unnamed radio groups, thumb icons, value-matching radio binding |
| `TextBox` | Outlined and filled variants, floating label, placeholder, leading icon, supporting text, `INotifyDataErrorInfo` validation, max length, password mode, undo/redo, clipboard and word navigation |
| `ComboBox` | Items or bound collections, typed item templates, text search, keyboard navigation, max drop-down height, compact mode |
| `Slider`, `ProgressBar` | Ranges, small and large steps, tick snapping, formatted value bubble; determinate and indeterminate progress |
| `ListBox`, `ItemsControl` | UI virtualization (only the items in view get containers), incremental updates from observable collections, typed templates, type-to-search, keyboard navigation |
| `TreeView` | Data-bound or item-built trees, children selector, typed templates, configurable expander icons; the shown nodes form one virtualized list |
| `DataGrid` | Virtualized rows; text, template and check box columns; multi-column sorting; Pixel, Auto and Star widths; resizing, reordering and a column chooser with saved layouts; a filter row and spreadsheet-style filter menus; single, check box and extended selection of rows or cells; F2 editing with custom editors and validation; Ctrl+C as tab-separated text; row context menus and empty placeholders |
| `OpenFileDialog`, `SaveFileDialog`, `FolderBrowserDialog` | The same dialog on every platform, with WPF's properties (`Filter`, `Multiselect`, `CheckFileExists`, `OverwritePrompt`, `InitialDirectory`, ...): path bar with clickable folders, folder tree, file grid with optional attribute columns, new folder and rename; any file system through `IFileSystemProvider` |
| `Menu`, `MenuItem`, `ContextMenu` | Menu bars (also in the title bar), submenus and context menus; icons, check and radio items, separators; shortcut text from the command's keybinding; `CanExecute` disables or hides items; access keys; menus built from data with a children selector and item templates |
| `TabControl` | MD3 primary and secondary tabs with a sliding indicator; browser-style tabs that close, add and reorder by dragging; header and content templates |
| `DatePicker`, `TimePicker` | MD3 docked and modal calendars and date input; clock dial and text input with 12- and 24-hour clocks; typed dates and times, ranges and validation; compact mode |
| `ColorPicker` | Hue/saturation wheel, RGB, HSL and HSB sliders that show their gradients, alpha, hex and channel inputs |
| `Badge` | MD3 small dot and large count or text badges on any element |
| `Card`, `Border` | Outlined, elevated and filled cards; elevation shadows; per-corner radii; clipping |
| `Popup`, `Dialog`, `DialogHost` | Smart placement with flipping (or at the pointer), light dismiss, dialog presets and custom buttons, async results, scoped hosts |
| `ToolTip`, `RichToolTip` | `.ToolTip("text")` on any element; rich tooltips hold any element and stay open for their buttons and links; placement, delays, disabled elements, optional keyboard-focus tooltips |
| `ScrollViewer` | Per-axis scroll bar modes, wheel and keyboard scrolling, scroll events |
| `TransitioningContentControl` | Fade, slide, zoom, slide-and-fade and composite transitions with configurable duration and easing |
| `PropertyGrid` | Categories, sorting, filtering, validation, custom editors per type or predicate, a resizable label column; metadata generated at compile time |
| `GridSplitter` | Resizes grid columns or rows by dragging or with the arrow keys; keeps star proportions; min/max limits, snapping, preview mode, Escape to cancel, double-click to restore |
| `Icon`, `Image`, `TextBlock` | Material Symbols with variable axes and custom SVG paths; stretch modes; wrapping, trimming, max lines and line height |
| `TitleBar`, `Toolbar`, `KeybindingHandler` | Custom window chrome, action bars, scoped keyboard shortcuts with chords |

### Layout

`StackPanel`, `WrapPanel`, `DockPanel`, `Grid` (Auto, pixel and star sizes, spans), `UniformGrid`, `Canvas` and
`VirtualizingStackPanel` (realizes only the items in view and reuses their containers; fixed or measured heights), plus
margin, padding, alignment, min/max sizes, visibility, clipping, opacity, layout transforms and render transforms. Any
element can set the mouse cursor (`.Cursor(CursorType.Hand)`); text boxes show the I-beam and splitters the resize arrows.

### Theming and styling

- `MaterialTheme.CreateLight()` / `CreateDark()`: MD3 color schemes, switchable at runtime in every open window.
- Theme default styles follow the MD3 specs with a desktop density (32 px buttons, 48 px text fields);
  `MaterialSizing.Touch` gives the standard MD3 sizes.
- Styles apply by type or by key (`.StyleKey(...)`, typography shortcuts such as `.TitleLarge()`), with the precedence
  theme style < app style < local value.
- Focus rings appear for keyboard navigation only.

### Platform

- Borderless windows with native resizing, snapping and shadows on Windows; multiple windows with per-window focus.
- Render-on-demand: idle windows don't draw.
- Hot reload through .NET Hot Reload, or manually with F5 or Ctrl+R when the app doesn't handle those keys itself.
- System clipboard, key repeat, and an optional frame-rate overlay (`SilkWindow.ShowFpsOverlay`).

---

## Project structure

| Project | Contents |
|---|---|
| [`Atelier.Core`](src/Atelier.Core) | Visual tree, bindable properties and bindings, styles, routed input, focus, animation, dispatcher, keybindings |
| [`Atelier.Rendering`](src/Atelier.Rendering) | SkiaSharp drawing context, paint, font and text caches, text measurement |
| [`Atelier.Layout`](src/Atelier.Layout) | Layout panels and `Border` |
| [`Atelier.Controls`](src/Atelier.Controls) | The control library |
| [`Atelier.Theming`](src/Atelier.Theming) | Theme infrastructure and renderer registry |
| [`Atelier.Theming.Material`](src/Atelier.Theming.Material) | Material Design 3 renderers, color schemes, typography, sizing and default styles |
| [`Atelier.Markup`](src/Atelier.Markup) | The fluent markup API |
| [`Atelier.Generators`](src/Atelier.Generators) | Source generators for keybindings and property-grid metadata |
| [`Atelier.Platform.Silk`](src/Atelier.Platform.Silk) | Windows, OpenGL context, input and clipboard via Silk.NET |
| [`Atelier.Gallery`](samples/Atelier.Gallery) | Showcase application for every control |
| [`Atelier.Tests`](tests/Atelier.Tests) | Unit, layout, binding and rendering tests (1,100+) |

---

## Running the Gallery

```bash
dotnet run --project samples/Atelier.Gallery
```

The gallery has a page for every control group. Each page demonstrates the control's properties, events, bindings and
states, with the code that builds it. Useful shortcuts:

| Shortcut | Action |
|---|---|
| Ctrl+F | Search the pages |
| Ctrl+T | Switch between the light and dark theme |
| Ctrl+Shift+F | Show the frame-rate overlay |
| F5 / Ctrl+R | Rebuild the window content (hot reload; F5 resets the demos on the Keybindings page) |

To render pages to PNG files without opening a window, as for the screenshots above, set
`ATELIER_GALLERY_SNAPSHOT=<output folder>`. Optionally also set `ATELIER_GALLERY_PAGES=0,3,7`,
`ATELIER_GALLERY_THEME=dark` and `ATELIER_GALLERY_SIZE=1280x800`. `ATELIER_GALLERY_TOOLTIP=<n>` opens the page's n-th
tooltip, and `ATELIER_GALLERY_FILE_DIALOG=open|save|folder` shows a file dialog limited to the repository (run it
from the repository folder).

---

## Running the tests

```bash
dotnet test
```

---

## License

This project is licensed under the [MIT License](LICENSE).
Copyright (c) 2026 Markus Luedin.
