# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Atelier is a retained-mode desktop UI framework for .NET 10 in plain C# (no XAML): SkiaSharp rendering on an OpenGL
surface via Silk.NET, Material Design 3 theming, and a fluent markup API. `README.md` is the user-facing feature
reference and is kept current (see "Conventions").

## Commands

```bash
dotnet build Atelier.slnx                                   # build everything
dotnet test                                                 # all tests (xUnit, tests/Atelier.Tests)
dotnet test --filter "FullyQualifiedName~BadgeTests"        # one test class
dotnet test --filter "FullyQualifiedName~BadgeTests.Counts_ShowTheLargeBadge_CappedAtMaxCount"  # one test
dotnet run --project samples/Atelier.Gallery                # showcase app
```

CI (`.github/workflows/ci.yml`, windows-latest) builds and tests in **Release**. Release and Debug differ:
`Atelier.DevTools` is only referenced in Debug (by `Atelier.Platform.Silk` and the tests), so code touching DevTools must
compile in both configurations. SDK is pinned by `global.json` (10.0.100, latestFeature).

Headless gallery screenshots (used for `docs/screenshots/`): set `ATELIER_GALLERY_SNAPSHOT=<folder>`, optionally with
`ATELIER_GALLERY_PAGES=0,3,7`, `ATELIER_GALLERY_THEME=dark`, `ATELIER_GALLERY_SIZE=1280x800`, and the other
`ATELIER_GALLERY_*` variables documented in the README ("Running the Gallery") and `samples/Atelier.Gallery/Program.cs`.

## Architecture

Project layering (lower depends on nothing above it):
`Core` → `Layout` → `Rendering` → `Theming` → `Controls` → `Theming.Material` → `Markup` / `NodeEditor` / `DevTools` →
`Platform.Silk`. `Audio` (audio-tool controls: `Knob`, timeline, waveforms) sits on `Markup` and brings its own
renderers (`AudioTheme`) and markup; `NodeEditor` must not depend on it. `Graphics3D` (the OpenGL `Viewport3D` and its scene model) sits on `Markup` the same way, with `Graphics3DTheme`; it renders with Silk.NET.OpenGL through `IHostWindow.GraphicsDevice` (implemented by `SilkWindow`), so it never references `Platform.Silk`. `Charts` (the Skia-drawn `XYChart`) sits on `Markup` the same way, with `ChartsTheme`. `Generators` is a Roslyn source-generator
project referenced as an analyzer (`OutputItemType="Analyzer" ReferenceOutputAssembly="false"`).

- **Property system** (`Atelier.Core/Properties`): elements are `BindableObject`s with static
  `BindableProperty<T>` fields registered via `BindableProperty.Register<TOwner, T>(name, default, changedCallback,
  validateValue: ...)`. Values have precedence (default < inherited < style < local < animation, then coercion); the
  CLR property is a thin get/set wrapper. `Style` (`Atelier.Core/Styling`) applies by type or key: theme style < app
  style < local value.
- **Controls don't draw themselves.** A control (`Atelier.Controls`) holds state, layout and input; drawing is done by
  an `IControlRenderer<T>` looked up in the theme's `RendererRegistry` (`Atelier.Theming/Theme.cs`), resolved by exact
  type or closest base type. Material renderers live in `Atelier.Theming.Material/Renderers` and are registered in
  `MaterialTheme.cs`; default sizes/colors come from `MaterialStyles.CreateStyles`. Adding a control usually means:
  control class + renderer + registration in `MaterialTheme` + default style in `MaterialStyles` + markup methods.
- **Markup** (`Atelier.Markup`, one `*Markup.cs` per control family): hand-written generic extension methods
  `T Prop<T>(this T el, ...) where T : Control => el.Set(Control.PropProperty, value)` so chains keep their type, plus
  `Bind{Prop}` overloads using `BindToSource(...)` with `[CallerArgumentExpression]`, and `On{Event}` handlers.
  Conventions are documented on `MarkupExtensions`.
- **Keybindings/commands** (`Atelier.Core/Keybinding`, `Controls/KeybindingHandler*.cs`): `[Command]`/`[Keybinding]`
  attributes are turned into descriptors by `KeybindingGenerator`; `KeybindingManager` holds groups, window groups
  and user customizations (JSON). Menus, buttons, the command palette and the keybinding editor all read command
  metadata from here.
- **Inspection** (`Atelier.Core/Inspection` + `InspectableGenerator`): compile-time property metadata for
  `PropertyGrid` and DevTools — no reflection.
- **Icons**: `MaterialIconNamesGenerator` generates `MaterialIcons` string constants from `MaterialIconKind`; the
  Material Symbols variable font is embedded in `Atelier.Controls`.
- **NodeEditor** (namespace `Atelier.Nodes`): `Model/` (graph view models, undo, groups, JSON serialization),
  `Evaluation/` (dataflow), `Controls/` (the `NodeEditor` control and its tools).
- **Platform** (`Atelier.Platform.Silk`): `SilkWindow` hosts content (`SetContent(() => ...)` factory enables hot
  reload), `SilkDispatcher`, clipboard. Rendering is on demand; performance goal is zero allocations per steady frame
  (cached paints, fonts, text blobs in `Atelier.Rendering`).

### Gallery (`samples/Atelier.Gallery`)

One `ViewModels/<X>ViewModel.cs` (a `PageViewModel`) + `Views/<X>View.cs` per page; pages are listed in
`MainViewModel`. Uses CommunityToolkit.Mvvm. New controls get a gallery page.

### Tests (`tests/Atelier.Tests`)

xUnit, one `<Feature>Tests.cs` per area. Tests run without a window: build elements with markup, call
`Measure`/`Arrange` directly and assert on bounds/state. Tests needing theme styles wrap with
`using var _ = ActiveTheme.Use(MaterialTheme.CreateLight());` so the global `ThemeManager.Current` is reset afterwards.
Test names read as sentences (`Counts_ShowTheLargeBadge_CappedAtMaxCount`).

## Conventions

- Public API carries thorough XML doc comments (summary, remarks with exact MD3 measurements, examples); match that.
- `Nullable` and `ImplicitUsings` are enabled in every project.
- When adding a user-visible feature, update `README.md`: the controls/feature tables, gallery shortcut table,
  screenshots in `docs/screenshots/` where relevant, and the test count in "Project structure".
