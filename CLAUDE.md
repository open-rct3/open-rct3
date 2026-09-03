# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Read AGENTS.md first

[`AGENTS.md`](./AGENTS.md) holds the binding rules for this repo (prose style, no em-dashes, relative paths only, ask before any `git` command, commits are the user's job, where new tools live, C# conventions). Everything there applies. This file only adds build/test commands and architecture that AGENTS.md does not cover.

## Toolchain

- **.NET SDK 10** is required ([`global.json`](./global.json), [`.tool-versions`](./.tool-versions), `README.md`). Project files currently declare `net8.0` / `net9.0` TFMs, multi-targeted per platform: a `-windows10.0.17763.0` TFM with `UseWindowsForms` on Windows, a `-macos` TFM on macOS, and a plain TFM for the `Testing=true` library configuration. The [`Makefile`](./Makefile) computes test-DLL paths from the csproj TFM (with a hardcoded fallback), so trust `make` output over any hardcoded framework string.
- **Deno** (see [`deno.json`](./deno.json)) drives the desktop client shell, the website, and the WASM plugins.
- **GNU Make** ([`Makefile`](./Makefile)) is the top-level entry point; it wires the .NET and Deno builds together and only rebuilds when sources change.
- [`.env`](./.env) sets `RCT3_PATH` (a local RollerCoaster Tycoon 3 install) for OVL/DAT scanning and integration tests.
- Rendering uses [Silk.NET](https://github.com/dotnet/Silk.NET) OpenGL bindings (WGL on Windows), ImGui via `Hexa.NET.ImGui`, DryIoc for DI.

## Common commands

Run from repo root. Per user rules, invoke Make as `make -C . <target>`.

| Task | Command |
| --- | --- |
| Build & run the game (debug) | `make -C . debug` |
| Release build (game + GUI + OVL) | `make -C . release` |
| Run the OVL Dumper tool | `make -C . dumper` |
| **Unit tests** (run these on any C# source change) | `make -C . test` |
| Coverage report | `make -C . cover` |
| Integration tests (only when explicitly asked) | `make -C . integration` |
| Plugin check + tests | `make -C . test-plugins` |
| Build WASM plugins | `deno task build:plugins` |
| Website build | `make -C . website` |

Notes:
- `make -C . test` builds `Tests.dll` and `OpenRCT3.Tests.dll` if stale, then runs `dotnet test OpenRCT3.tests.slnf --no-build /p:Testing=true`.
- Do **not** run bare `dotnet test` on the test csprojs: it bakes `*Undefined*` into fixture paths. Use `make -C . test`, or pass `-p:SolutionDir=` (see [`Directory.Build.props`](./Directory.Build.props), which also feeds `ThisAssembly.Constants.SolutionDir`).
- Never pipe test output (no `| head`, `2>&1`, etc.); let the runner print its full summary.
- Run a single fixture with an NUnit filter: `dotnet test OpenRCT3.tests.slnf --no-build /p:Testing=true --filter "FullyQualifiedName~SplineBaker"`.
- Deno formatting for `.ts`: `deno fmt` (line width 120, semicolons). Do not `cd` into a package first; use `deno task --cwd <package>` from root.

## Architecture

Two stacks sit side by side: **OpenCobra** (the engine and RCT3 file-format framework, domain-agnostic) and **OpenRCT3** (the game built on it). A skill named `drive-native-app` covers launching and screenshotting the running game.

### OpenCobra (framework, must stay game-agnostic)

- **`OpenCobra/OVL`** (`OpenCobra.OVL`): reads/writes RCT3 Overlay (`.ovl`) archives. Port of the C++ `rct3-importer` libOVL. `OVL.cs` parses forward only (no `BaseStream.Position` rewinds, no unbounded loops per AGENTS.md). `Files/` has per-resource-type decoders (StaticShapes, Terrain, TrackSections, splines, etc.). `InstallFinder.cs` locates an RCT3 install.
- **`OpenCobra/Data`** (`OpenCobra.Data`): the *non-OVL* `DAT` container used by saved parks (`Documents\RCT3\Parks\*.dat`), track designs (`*.trk`), fireworks. Distinct format from OVL.
- **`OpenCobra/GDK`** (`GDK.csproj`): the graphics/game development kit. `Scene.cs`, `Camera.cs`, `Model.cs`, `Materials/`, `Meshes/`, `Shaders/`, `Streaming/`, GL state wrappers. Ownership of GL/native resources is enforced by attributes (`[TakesOwnership]`, `[Unowned]`) checked by the analyzers. Game-specific interfaces (e.g. `IParkLoader`) do **not** belong here; they live in the game project.
- **`OpenCobra/Analyzers`**: Roslyn analyzers (`DisposableOwnershipAnalyzer`, `UnownedReferenceAnalyzer`, `ReentrancyAnalyzer`) referenced as analyzers by the game project.
- **`OpenCobra/Tests`** (`Tests.csproj`): unit tests for `OVL` + `GDK` only, never dependent on the game. `Integration/` and `TestRunner/` (`OvlTestBench`) are separate projects excluded from the unit build and only run via `make -C . integration`.

### OpenRCT3 (the game)

- **`OpenRCT3/`** root: `Game.cs`, `Program.windows.cs` / `Program.macOS.cs` entry points, `Reactivity.cs`.
- **`OpenGL/`**: `Renderer.cs` and platform GL context creation (`GLContext.windows.cs` uses WGL, `GLContext.macOS.cs`); the non-target file is `<Compile Remove>`d per platform.
- **`Simulation/`**: the park model. `Park.cs`, `World.cs`, `Terrain.cs` + `TerrainMeshBuilder.cs`, path tiles/slopes, `SceneryRegistry` / `SceneryDefinition` / `SceneryPlacement`, `WaterPool.cs`. `IParkLoader.cs` is the game-side loader seam.
- **`Rides/`**: `Ride` base, `TrackedRide` (adds `Length`, `MaxHeight`), `Coaster` (adds `Inversions`). `TrackLibrary.cs` is the tracked-ride segment library. `Rides/OVL/` correlates rides to OVL track data.
- **`Rides/TrackSpline/`**: procedural track geometry. `SplineBaker.cs` bakes Catmull-Rom / procedural pieces into rail geometry using a fixed-resolution arc-length LUT (`ArcLength.cs`, `BakingConfig.cs`); `TrackChaining.cs`, `RailQuery.cs`, `WheelIK.cs`. The arc-length path is performance-sensitive; keep expensive test tolerances loose and production tolerances tight.
- **`Scenario/`**: `Editor.cs`, `ParkChooser.cs`. **`ViewModels/`** + **`UI/`**: ImGui inspector/GUI. **`Input/`**: key bindings.
- **`OpenRCT3.Tests`** (`OpenRCT3.Tests.csproj`): game-level integration/unit tests that *may* reference the game, `OVL`, and `GDK`. Built with `/p:Testing=true` so the game compiles as a library.

### Dumper + plugins

- **`Dumper/`**: a WinForms app (`OpenCobra.Dumper`) that browses OVL archives. `Plugins/ViewerPlugin.cs` hosts Extism WASM plugins and exposes "ovl" host functions (`resolve_pointer`, `resolve_symbol_reference`, `symbol_address`, `read_resource`, ...) so plugins can walk relocated pointers and cross-resource symbol refs in the open archive.
- **`plugins/<tag>-viewer/`**: AssemblyScript, compiled to `bin/plugins/*.wasm` by `scripts/build-plugins.ts`. One per OVL resource tag; each implements `name()` / `version()` / `file_types()` / `render(bytes)`. Full byte-level decoding stays in `OpenCobra.OVL`; plugins are summary views. Shared helpers in `plugins/lib/` (`ovl.ts` wraps the host functions).

### clients

- **`clients/desktop/`**: Deno + `@webview/webview` shell that launches the compiled game (`deno task dev:desktop`, built via `deno task build:desktop`).
- **`clients/website/`**: Lume static site.

### Tooling / working notes

- **`.agents/`**: plans, research, war-stories, summaries, templates. Source comments must never reference these (AGENTS.md). Check `.agents/summaries/*.csv` for existing `RCT3_PATH` scan results before re-scanning.
- **`.agents/tools/`**: standalone console tools (`OvlScanner`, `TrackDataVerifier`, `TrackedRideCorrelator`). New tools go here, each in its own subdir with a `.csproj`, scaffolded with file-writing tools; extend an existing one before adding another.
- `dotnet tool` `cslint` is pinned in [`dotnet-tools.json`](./dotnet-tools.json).
