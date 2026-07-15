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
  [`OpenCobra.Data.Parks.Paths`](../../../../OpenCobra/Data/Parks/Paths.cs) (path-tile decoding).
  Wired end-to-end: `ParkChooser.ParkSelected` → `World.Load(string parkPath)` (a new overload;
  `World.Load()` still loads the default flat park) → `Park.Load(parkPath)`. Currently populates
  `Park.Paths` only - terrain height integration is this plan's next step (see Testing/Status).
- Loading a park replaces the active `Park`/`Paths` and (once terrain integration lands) should
  trigger a `TerrainMeshBuilder` rebuild so the existing renderer picks up the new data with no
  separate "park renderer" — rendering is already correct once the data model is populated.
- Scope is read-only load of terrain + paths for rendering. Scenery, rides, guests, finances, and
  save (write) support are explicitly out of scope (see Deferred).

## Gaps and Risks

1. **Resolved**: both `GETerrain` and `PathTileList`/`PathNodeArray` have working decoders in
   [`OpenCobra.Data.Parks`](../../../../OpenCobra/Data/Parks).
   - `GETerrain`'s corner-height layout — see
     [rct3-terrain-data-layout.md](../../../research/rct3-terrain-data-layout.md) and
     [`Terrain.cs`](../../../../OpenCobra/Data/Parks/Terrain.cs). Confirmed: `RCT3Terrain.EngineTerrain`
     is a fixed 393,234-byte blob; per-corner height is a `float32` on a 12-byte stride, `+1.0` per
     raise-tool click; a separate 4-float, 4-byte-stride sub-structure holds water data. Still
     unresolved: the other 8 bytes of each 12-byte corner record, the surface-type field's exact
     offset/meaning within that record, water's sign convention, and the grid's width/height (the
     32,769-corner count doesn't factor into an obviously-square grid - see that doc's "Still
     unattempted"). This last unknown is what currently blocks integrating decoded corners into
     `OpenRCT3.Simulation.Terrain`, which needs an explicit width/height.
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
4. **New**: `World.Load(parkPath)` re-runs `BuildScene()`, which unconditionally appends a new
   terrain mesh, marker cube, and window set to `scene.Models`/`scene.Windows` rather than
   replacing what's there — see the `TODO` in `World.cs`'s `BuildScene`. Harmless for the one-time
   startup call `World.Load()` still makes, but selecting a park via the chooser mid-session
   currently duplicates scene content instead of swapping it. Needs a scene-teardown step before
   `ParkChooser` is safe to use more than once per run.

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
- What `Terrain`'s actual grid width/height is, needed to integrate decoded `GETerrain` corners
  into `OpenRCT3.Simulation.Terrain` — see Gap #1. Likely needs either a grid-position-isolating
  edit (raise a corner at a known, in-game-reported tile coordinate) or working out the header/
  corner-count arithmetic more rigorously against a second, differently-sized map.

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
- `OpenCobra.Data.Parks.Terrain`: **no committed test yet** - validated only via a throwaway
  scratch tool during reverse-engineering. Needs a `TerrainTests.cs` mirroring `PathsTests.cs`'s
  approach: known-good case against the `01-one-corner-up.dat`/`01-one-corner-and-other-corner-up.dat`
  fixtures (corner heights `1.0`/`2.0` at the diff-confirmed indices), edge case (water/cliffs, once
  those sub-fields are decoded), failure case (truncated/corrupt terrain block raises a clear
  exception).
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
- `World.Load(parkPath)`: no automated test - `World` depends on a live `Game`/IoC/GL context
  (`BuildScene` resolves `Game.Instance!`), making it awkward to unit test in isolation. Manual
  verification only so far.

## Status

Mostly working end-to-end for paths; terrain height is the remaining piece.

**Implemented and tested**:
- `OpenCobra.Data` project ([`Data.csproj`](../../../../OpenCobra/Data/Data.csproj)) with
  [`Dat`](../../../../OpenCobra/Data/DAT.cs), a full port of `dat.rs`'s header/struct-table/entry-list
  framing.
- [`OpenCobra.Data.Parks.Terrain`](../../../../OpenCobra/Data/Parks/Terrain.cs) and
  [`Paths`](../../../../OpenCobra/Data/Parks/Paths.cs), reverse-engineered from real fixture pairs
  under [`Fixtures/Parks/Reverse Engineering/`](../../../../OpenCobra/Tests/Fixtures/Parks/Reverse%20Engineering) -
  see [rct3-terrain-data-layout.md](../../../research/rct3-terrain-data-layout.md) and
  [rct3-path-tile-layout.md](../../../research/rct3-path-tile-layout.md).
- [`Editor.OpenPark`](../../../../OpenRCT3/Scenario/Editor.cs) → [`ParkChooser`](../../../../OpenRCT3/Scenario/ParkChooser.cs)
  → `ParkChooser.ParkSelected` → [`World.Load(string parkPath)`](../../../../OpenRCT3/Simulation/World.cs)
  → [`Park.Load(string path)`](../../../../OpenRCT3/Simulation/Park.cs), wired end-to-end and
  building. `Park.Load` populates `Park.Paths` from both at-grade and raised tiles.

**Not yet done**:
- Terrain height integration: `Park.Load` doesn't touch `OpenCobra.Data.Parks.Terrain` yet, so a
  loaded park's terrain is always the default flat grid regardless of what's in the save - blocked
  on the grid width/height unknown (Gap #1/Open Questions).
- The scene-duplication issue (Gap #4) if `ParkChooser` is used more than once per run.
- `TerrainTests.cs` for `OpenCobra.Data.Parks.Terrain` (Testing section).
- Non-Windows saved-parks paths, `.prf` confirmation, scenario/park list differentiation (Open
  Questions).
