# Render Loaded Parks

<!-- Renamed from render-fluctuating-terrain.md - the roadmap item is "load a saved RCT3 park and
render it", not terrain specifically; paths and (now) the park-chooser UI are equally in scope. -->

## Context

Roadmap item: load a saved RCT3 park and render its terrain (and paths) in OpenRCT3. RCT3 saved
parks are `.dat` files under `Documents\RCT3\Parks` (e.g. `Rivendell.dat`), stored in a non-OVL
DAT container format shared with `.trk`/`.fwd`/`.frw` files — see
[rct3-non-ovl-dat-format.md](../../../research/rct3-non-ovl-dat-format.md) for the full format
writeup (container structure, version history, and what's still undecoded). The short version:
[`assets/reference/dat/dat.rs`](../../../../assets/reference/dat/dat.rs) is a Rust reference for
this container's framing (struct table + entry list); the two field kinds this plan needed decoded
— `GETerrain` and `PathTileList`/`PathNodeArray` — were undecoded opaque blobs there, so their byte
layout had to be reverse-engineered from real saved-park `.dat` files. That reverse-engineering is
done — see Status.

[`Terrain`](../../../../OpenRCT3/Simulation/Terrain.cs) and
[`PathTile`](../../../../OpenRCT3/Simulation/PathTile.cs) are fully implemented in-memory data
models with editing APIs (see [terrain-heightmap.md](../terrain-heightmap.md),
[path-network.md](../path-network.md)) and
[`TerrainMeshBuilder`](../../../../OpenRCT3/Simulation/TerrainMeshBuilder.cs) can turn a `Terrain`
into a mesh.

[`OpenRCT3/Scenario/Editor.cs`](../../../../OpenRCT3/Scenario/Editor.cs) has an `OpenPark` event,
raised by its "Open" button. [`OpenRCT3/Platforms/Paths.cs`](../../../../OpenRCT3/Platforms/Paths.cs)
exposes every `Documents\RCT3\*` subfolder (`ParksDirectory`, `ScenariosDirectory`,
`NewScenariosDirectory`, etc.) via `Environment.SpecialFolder.MyDocuments`.

## Goals

- ~~Add an "Open" button to `Editor.cs`~~ **Done**: `Editor.cs` has an `OpenPark` event wired to its
  existing "Open" button (this landed ahead of this plan being updated to reflect it - the button
  was already there next to "Save"/"Quit", not added new).
- ~~`ParkFileDialog` lists saved-park `.dat` files~~ **Done, as [`ParkChooser`](../../../../OpenRCT3/Scenario/ParkChooser.cs)**:
  an `IWindow` shown via `editor.OpenPark += parkChooser.Show`. Aggregates `.dat` files from
  `Paths.NewScenariosDirectory`, `Paths.ParksDirectory`, and `Paths.ScenariosDirectory` together
  (not scoped to `Parks/` only, per a since-revised decision - see Deferred for why scenario/save
  distinction is still punted). Lists filenames in a scrollable `Selectable` list; single-click
  selects, double-click or the "Open" button raises `ParkSelected(string path)`.
- ~~Locate the saved-parks folder~~ **Done, as [`OpenRCT3.Platforms.Paths`](../../../../OpenRCT3/Platforms/Paths.cs)**:
  covers every `Documents\RCT3\*` subfolder, not just `Parks/`, via `Environment.SpecialFolder.MyDocuments`.
  Still Windows-only (`// TODO: Ensure this works on macOS and Linux` in the file itself) - see Open
  Questions.
- ~~Selecting a file calls `ParkFile.Load`~~ **Done, as [`Park.Load`](../../../../OpenRCT3/Simulation/Park.cs)**
  (a static method on the existing `Park` type, not a separate `ParkFile` type), layered on
  [`OpenCobra.Data.Dat`](../../../../OpenCobra/Data/DAT.cs) (the non-OVL `.dat` container reader,
  implemented in the `OpenCobra.Data` project alongside `OpenCobra.OVL`) and
  [`OpenCobra.Data.Parks.Paths`](../../../../OpenCobra/Data/Parks/Paths.cs) (path-tile decoding)
  and [`OpenCobra.Data.Parks.Terrain`](../../../../OpenCobra/Data/Parks/Terrain.cs) (corner-height
  decoding). Wired end-to-end: `ParkChooser.ParkSelected` → `World.Load(string parkPath)` (a new
  overload; `World.Load()` still loads the default flat park) → `Park.Load(parkPath)` +
  `Terrain.Load(parkPath)`.
- ~~Loading a park replaces the active `Park`/`Paths` and (once terrain integration lands) should
  trigger a `TerrainMeshBuilder` rebuild~~ **Done**: `World.BuildScene` rebuilds the terrain mesh
  from the newly-loaded `Terrain` every call and replaces the previous scene rather than appending
  to it (Gap #4) — no separate "park renderer" needed, rendering was already correct once the data
  model was populated.
- Scope is read-only load of terrain + paths for rendering. Scenery, rides, guests, finances, and
  save (write) support are explicitly out of scope (see Deferred).

## Gaps and Risks

1. **Resolved**: both `GETerrain` and `PathTileList`/`PathNodeArray` have working decoders in
   [`OpenCobra.Data.Parks`](../../../../OpenCobra/Data/Parks).
   - `GETerrain`'s full per-tile corner-height layout — see
     [rct3-terrain-data-layout.md](../../../research/rct3-terrain-data-layout.md) and
     [`Terrain.cs`](../../../../OpenCobra/Data/Parks/Terrain.cs). Confirmed: `RCT3Terrain.EngineTerrain`
     = a 6-byte mini-header (`Width`/`Height` in tiles) + a 12-byte preamble record (purpose
     unresolved) + `Width x Height` tile slots, each holding four independently-steppable corner
     heights (`SouthEast`/`SouthWest`/`NorthEast`/`NorthWest`, `+1.0` per raise-tool click) and a
     `SurfaceType` byte. Verified exact against three real parks' total blob length and against
     every known-edit fixture, including four corner-isolating captures
     (`01-near-left/right-corner-up.dat`, `01-far-left-corner-up.dat`, `01-one-far-corner-up.dat`).
     Water is not stored here at all — it lives in a separate `WaterManager` top-level entry. Small
     residual unknowns remain (preamble record, a handful of still-unknown bytes, `NorthEast` was
     deduced by elimination rather than independently isolated) but don't block ingestion.
   - `PathTileList`/`PathNodeArray` turned out not to need byte-level decoding at all — see
     [rct3-path-tile-layout.md](../../../research/rct3-path-tile-layout.md) and
     [`Paths.cs`](../../../../OpenCobra/Data/Parks/Paths.cs). Path data lives as ordinary top-level
     `PathTile` (at-grade) / `PathFlying` (raised, cross-referencing a companion `SceneryItem` for
     the 3D support piece) entries, already fully decoded by `Dat`'s generic struct-table framing;
     the `PathTileList` field itself is always empty. Both extractors are validated against real
     fixture pairs and wired into `Park.Load` (`PathsTests.cs`, `ParkLoadTests.cs`, all passing).
2. **Resolved**: RCT3 saves are not compressed/encrypted at the container level — `Dat.Load` reads
   real, unmodified saves end-to-end with plain `BinaryReader` calls and produces well-formed
   entry lists; a compressed byte stream wouldn't coincidentally match this reader's expected
   framing at every step.
3. **Resolved**: rendering path is not a gap — `TerrainMeshBuilder` and the path rendering model
   (decal for at-grade, piece models for raised, per `path-network.md`) already exist; this plan
   only needs to call into them once terrain data is populated too.
4. **Resolved**: `World.Load(parkPath)` used to re-run `BuildScene()` unconditionally appending a
   new terrain mesh, marker cube, and window set to `scene.Models`/`scene.Windows` rather than
   replacing what's there. `BuildScene` now clears/disposes `scene.Models` and `scene.Windows` at
   the top of every call, reuses the `Editor`/`ParkChooser` instances (created once, event handlers
   wired once) rather than recreating them, and registers the terrain mesh/`UI.Debug` factory with
   `IfAlreadyRegistered.Replace`/a one-time guard respectively — so selecting a different park via
   `ParkChooser` now replaces the scene instead of duplicating it.
5. **New, deliberately scoped down**: `ParkChooser.ParkSelected` calling `World.Load(path)` directly
   hung the whole app — `Renderer`/`ThreadAffine` marshals each frame onto the WinForms UI thread,
   so `ParkChooser`'s click handling (and thus `ParkSelected`) runs there, and `World.Load`'s
   blocking `.Wait()` froze that thread's message pump entirely. A first fix (deferring the load to
   `Game.Run()`'s loop via a `pendingParkLoad` field/`ProcessPendingLoad()`) was reverted as too
   janky. Current state: `ParkChooser.ParkSelected` → `World.ReplaceTerrain(path)`, which hand-loads
   only `Terrain` (via `Terrain.LoadFromSave`, no OVL texture I/O) and swaps just the terrain mesh
   model — small enough on-thread work not to visibly hang, but `Park`/paths/water/scenery/camera
   framing/grass texture are all left stale, flagged with a `TODO` at the `ParkSelected` wiring in
   `World.BuildScene`. See [`world-system-scheduling.md`](../world-system-scheduling.md) for the
   plan that replaces this with a real `ISystem`/`Scheduler`-driven full reload (already scaffolded
   in the codebase but never wired up) - not started.

## Open Questions

- How to reverse-engineer `GETerrain`/`PathTileList`/`PathNodeArray`'s byte layout. **Resolved**:
  no existing community decoder was found (GitHub code search, the local `rct3-importer` checkout,
  and belgabor's format writeup all came up empty); hex-diffing paired saves with isolated in-game
  edits worked for both. See Gap #1 above and the two research docs it links.
- Non-Windows saved-parks folder location (macOS `~/Library/Application Support/...` vs. Linux
  equivalent under Proton/Wine) — still not confirmed; `OpenRCT3.Platforms.Paths` carries its own
  `// TODO: Ensure this works on macOS and Linux` rather than per-OS branches. Needs a real
  macOS/Linux RCT3 install to check, or community documentation.
- Whether `.prf` is a real RCT3 DAT-format extension — unconfirmed, see
  [rct3-non-ovl-dat-format.md](../../../research/rct3-non-ovl-dat-format.md). Doesn't block this
  plan, but `OpenCobra.Data.Dat` is already generic enough that a future plan can point it at
  whatever `.prf` turns out to be without rework, same as `.trk`/`.fwd`/`.frw`.
- `ParkChooser` aggregates `NewScenariosDirectory`/`ParksDirectory`/`ScenariosDirectory` into one
  undifferentiated list rather than scoping to saved parks only, superseding this plan's earlier
  "scope to `Parks/` only, leave scenarios to a future plan" decision. Whether/how to visually
  distinguish scenarios from saved parks in the chooser is left open - "we can enhance the
  differences later" (user, when this was implemented).
- Whether decoded `GETerrain` tiles map to `OpenRCT3.Simulation.Terrain`'s buildable size directly
  or need an OOB-border offset, and whether the DAT's row-major tile index maps to simulation
  `(tileX, tileY)` as `(col, row)` with no rotation/flip — neither independently confirmed by any
  fixture; see `Park.Load`'s doc comment for the assumption made when this integration landed.

## Deferred

- Writing/saving parks (`.dat` write path) — this plan is read-only.
- Scenery, ride, guest, and finance data decoding from the same save — `Dat` already preserves
  every undecoded field kind as raw bytes (`OpaqueValue`) rather than discarding it, so a later
  plan can decode the remaining field kinds without redoing the container-framing work.
- Scenario loading distinct from park loading — `ParkChooser` currently aggregates both under one
  list (see Open Questions); splitting them out, and confirming whether scenarios need different
  load semantics than saved parks, is future work.

## Testing

- `OpenCobra.Data.Dat`: [`DatTests.cs`](../../../../OpenCobra/Tests/Data/DatTests.cs) auto-discovers
  every embedded `.dat` fixture under [`Fixtures/Parks/`](../../../../OpenCobra/Tests/Fixtures/Parks)
  and asserts `Dat.Load` parses without throwing and returns a non-empty entry list. Still missing:
  assertions on *which* struct/field names and counts are expected (so a framing regression fails
  loudly instead of just "still non-empty"), coverage of the extended-header version-byte branch if
  a fixture using the other version (`0x1A` vs `0x2A`) turns up, and a failure-case test
  (truncated/corrupt file raises `DatException` rather than an unhandled exception).
- `OpenCobra.Data.Parks.Terrain`: [`TerrainTests.cs`](../../../../OpenCobra/Tests/Data/TerrainTests.cs),
  10/10 passing — known-good cases for every corner-isolating fixture (each of the four corners
  individually, a map-edge tile, two corners on one tile independently, surface-type repaint
  leaving heights untouched, water-added flattening all four corners), plus a path-edit case
  confirming `EngineTerrain` is byte-identical when only paths change, and a real-vendored-park
  blob-length sanity check. Still missing: a failure case (truncated/corrupt terrain block raises a
  clear exception).
- `OpenCobra.Data.Parks.Paths`: [`PathsTests.cs`](../../../../OpenCobra/Tests/Data/PathsTests.cs) -
  known-good cases for one and two adjacent at-grade tiles and one raised tile, against the
  `02-*.dat` fixtures. Still missing: a failure case and a path-removal case (not yet captured).
- `Park.Load`: [`ParkLoadTests.cs`](../../../../OpenRCT3.Tests/Simulation/ParkLoadTests.cs) - exact
  tile-placement assertions against the same `02-*.dat` fixtures (reused directly via
  `Constants.ParkFixturesDir`, no duplication), plus a smoke test against the two vendored real
  parks (`Rivendell.dat`, `Fun Valley Amusment park.dat`). Still missing: terrain-height assertions
  once that integration lands, and a failure case (missing/corrupt file).
- `ParkChooser`: no automated UI test (ImGui windows aren't covered elsewhere in this repo either);
  verified manually in-app.
- `Terrain.LoadFromSave` (the DAT-to-simulation conversion half of `Terrain.Load(string?)`):
  [`TerrainLoadTests.cs`](../../../../OpenRCT3.Tests/Simulation/TerrainLoadTests.cs), 6/6 passing -
  exact-index assertions against the same reverse-engineering fixtures (dimensions with OOB border,
  single/double corner steps, surface-type mapping, negative-height clamping, a real non-square
  park). `Terrain.Load(string?)` itself (the OVL texture-loading wrapper around it) has no
  automated test - it requires a real RCT3 install via `AppConfig.Instance`, same as the
  pre-existing parameterless `Terrain.Load()`.
- `World.Load(parkPath)`: no automated test - `World` depends on a live `Game`/IoC/GL context
  (`BuildScene` resolves `Game.Instance!`), making it awkward to unit test in isolation. Manual
  verification only so far.

## Status

Terrain and path decoding are both solid; wiring a *selected* park into the running game is
deliberately scoped down to terrain-only for now (see Gap #5).

**Implemented and tested**:
- `OpenCobra.Data` project ([`Data.csproj`](../../../../OpenCobra/Data/Data.csproj)) with
  [`Dat`](../../../../OpenCobra/Data/DAT.cs), a full port of `dat.rs`'s header/struct-table/entry-list
  framing.
- [`OpenCobra.Data.Parks.Terrain`](../../../../OpenCobra/Data/Parks/Terrain.cs) and
  [`Paths`](../../../../OpenCobra/Data/Parks/Paths.cs), reverse-engineered from real fixture pairs
  under [`Fixtures/Parks/Reverse Engineering/`](../../../../OpenCobra/Tests/Fixtures/Parks/Reverse%20Engineering) -
  see [rct3-terrain-data-layout.md](../../../research/rct3-terrain-data-layout.md) and
  [rct3-path-tile-layout.md](../../../research/rct3-path-tile-layout.md).
- [`Park.Load(string path)`](../../../../OpenRCT3/Simulation/Park.cs) (populates `Park.Paths` from
  both at-grade and raised tiles) and [`Terrain.Load(string?)`](../../../../OpenRCT3/Simulation/Terrain.cs)/
  `Terrain.LoadFromSave` (populates all four corner heights and `SurfaceIndex` per tile) both exist
  and are tested, and both run at startup via `World.Load(null)` → the default flat park path.
  Neither is currently reachable with a real `parkPath` from inside a running game session, though -
  see the next bullet.
- [`Editor.OpenPark`](../../../../OpenRCT3/Scenario/Editor.cs) → [`ParkChooser`](../../../../OpenRCT3/Scenario/ParkChooser.cs)
  → `ParkChooser.ParkSelected` → [`World.ReplaceTerrain(string parkPath)`](../../../../OpenRCT3/Simulation/World.cs),
  **not** the full `World.Load(string?)` pipeline (Gap #5) - `ReplaceTerrain` calls
  `Terrain.LoadFromSave` directly and swaps just the terrain mesh model, leaving `Park`/paths/
  water/scenery/camera framing/grass texture untouched. `BuildScene` (used only by the
  startup `World.Load(null)` path today) still replaces the previous scene rather than duplicating
  it, so it's safe to call more than once per run whenever something does call it again.

**Not yet done**:
- A real, non-blocking full reload reachable from `ParkChooser` - see Gap #5 and
  [`world-system-scheduling.md`](../world-system-scheduling.md) (not started).
- Non-Windows saved-parks paths, `.prf` confirmation, scenario/park list differentiation (Open
  Questions).
- The DAT-tile-to-simulation-tile mapping assumption flagged in `Terrain.Load`'s doc comment
  (buildable-size vs. OOB-inclusive, row-major with no rotation/flip) - unconfirmed by any fixture.
- The residual `EngineTerrain` unknowns noted in `rct3-terrain-data-layout.md` (preamble record,
  a few still-undecoded bytes, `NorthEast` deduced by elimination) and `WaterManager`'s per-pool
  record layout - none block rendering, all are candidates for future cleanup.
