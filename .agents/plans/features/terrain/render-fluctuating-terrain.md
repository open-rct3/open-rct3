# Render Fluctuating Terrain

## Context

Roadmap item: load a saved RCT3 park and render its terrain (and paths) in OpenRCT3. RCT3 saved
parks are `.dat` files under `Documents\RCT3\Parks` (e.g. `Rivendell.dat`), stored in a non-OVL
DAT container format shared with `.trk`/`.fwd`/`.frw` files — see
[rct3-non-ovl-dat-format.md](../../../research/rct3-non-ovl-dat-format.md) for the full format
writeup (container structure, version history, and what's still undecoded). The short version:
[`assets/reference/dat/dat.rs`](../../../../assets/reference/dat/dat.rs) is a Rust reference for
this container's framing (struct table + entry list), but the two field kinds this plan actually
needs — `GETerrain` and `PathTileList`/`PathNodeArray` — are undecoded opaque blobs there, so their
byte layout has to be reverse-engineered from a real saved-park `.dat` (starting from
`Rivendell.dat`, already on hand) before any C# decoder can be written.

Today [`Terrain`](../../../../OpenRCT3/Simulation/Terrain.cs) and
[`PathTile`](../../../../OpenRCT3/Simulation/PathTile.cs) are fully implemented in-memory data
models with editing APIs (see [terrain-heightmap.md](../terrain-heightmap.md),
[path-network.md](../path-network.md)) and
[`TerrainMeshBuilder`](../../../../OpenRCT3/Simulation/TerrainMeshBuilder.cs) can turn a `Terrain`
into a mesh — but nothing populates a `Park` from a saved-park `.dat`.

[`OpenRCT3/Scenario/Editor.cs`](../../../../OpenRCT3/Scenario/Editor.cs) is the scenario editor
window; it already has a `TODO`-stubbed "Setup Park" button and no "Open" button.
[`AppConfig.cs`](../../../../OpenRCT3/Platforms/AppConfig.cs) shows the existing pattern for
locating well-known folders via `Environment.GetFolderPath`.

## Goals

- Add an "Open" button to `Editor.cs` next to "Setup Park" that opens a new `ParkFileDialog`
  ImGui window (modal, list-based — not a native `OpenFileDialog`, to stay consistent with the
  rest of the ImGui-driven UI and stay cross-platform; the reference screenshot at
  `assets/reference/gui/save dialog.png` only shows RCT3's native save/load toolbar icons, not the
  list itself, so the new dialog's layout is our own call, not a port).
- `ParkFileDialog` lists `.dat` files found under the user's RCT3 saved-parks folder (confirmed:
  `Documents\RCT3\Parks`, e.g. `Rivendell.dat`). Locate that folder with a new
  `RCT3Paths.SavedParksDirectory` helper (mirrors the `AppConfig.cs` `Environment.GetFolderPath`
  pattern) resolving to `%UserProfile%\Documents\RCT3\Parks` on Windows via
  `Environment.SpecialFolder.MyDocuments`, and the equivalent per-OS "My Games"-style path on
  macOS/Linux once those platforms' save locations are confirmed (`Program.macOS.cs` /
  `Program.windows.cs` already branch per-OS for the install path — follow that same branch
  structure). `Coasters\*.trk`, `Fireworks\*.fwd`, and `*.frw` use their own extensions rather than
  `.dat`, so the dialog only needs to scope by directory (`Parks/`) and extension (`*.dat`) with no
  magic-number sniff needed at listing time. Sniffing still matters at load time, to fail clearly
  on a corrupt or unexpectedly-shaped `.dat` rather than misparsing it as terrain data. **Open
  Question** below covers non-Windows paths, since only the Windows path is confirmed.
- Selecting a file calls a new `ParkFile.Load(string path) -> Park` in
  `OpenRCT3/Simulation/ParkFile.cs`, layered on
  [`OpenCobra.Data.Dat`](../../../../OpenCobra/Data/DAT.cs) — the non-OVL `.dat` container reader
  (ported from `dat.rs`'s `DataFile`/`DataStruct`/`StructField` model, per
  [rct3-non-ovl-dat-format.md](../../../research/rct3-non-ovl-dat-format.md)), already implemented
  and building in the new `OpenCobra.Data` project, alongside `OpenCobra.OVL`. `Dat.Load` parses
  the struct table and entry list but leaves `GETerrain` and `PathTileList`/`PathNodeArray` field
  bodies as opaque `DatOpaqueValue` byte blobs (see `DatOpaqueValue` in `DAT.cs`), since their
  internal layout isn't decoded by `dat.rs` or the community documentation either. `ParkFile.Load`
  is what turns those two opaque blobs into `Terrain` and `Park.Paths` data — that decoding logic
  is new, plan-specific work layered on top of `Dat`, and still has to be derived empirically from
  sample saves (hex-diffing `Rivendell.dat` and any other saves the user can supply, per the
  existing scratch-scanner pattern used for OVL archives, adapted to this non-OVL container).
- Loading a park replaces the active `Park.Terrain`/`Park.Paths`/`Park.WaterPools` and triggers a
  `TerrainMeshBuilder` rebuild plus a path-mesh rebuild, so the existing renderer picks up the new
  data with no separate "park renderer" — rendering is already correct once the data model is
  populated, this plan is only about getting real save data into that model.
- Scope is read-only load of terrain + paths for rendering. Scenery, rides, guests, finances, and
  save (write) support are explicitly out of scope (see Deferred).

## Gaps and Risks

1. **Partially resolved**: `GETerrain`'s corner-height layout is now known — see
   [rct3-terrain-getterrain-layout.md](../../../research/rct3-terrain-getterrain-layout.md),
   reverse-engineered from paired fixture saves under
   [`Fixtures/Parks/Reverse Engineering/`](../../../../OpenCobra/Tests/Fixtures/Parks/Reverse%20Engineering)
   by byte-diffing isolated in-game edits. Confirmed: `RCT3Terrain.EngineTerrain` is a fixed
   393,234-byte blob; per-corner height is a `float32` on a 12-byte stride, `+1.0` per raise-tool
   click; a separate 4-float, 4-byte-stride sub-structure holds water data. Still unresolved: the
   other 8 bytes of each 12-byte corner record, the surface-type field's exact offset/meaning
   within that record, water's sign convention, and `PathTileList`/`PathNodeArray` (no path-edit
   fixtures exist yet - deferred, see that doc's "Still unattempted" section). `PathTileList` is
   the larger remaining risk now that terrain height decoding has a concrete starting point.
2. **Resolved**: RCT3 saves are not compressed/encrypted at the container level — `Dat.Load` reads
   `Rivendell.dat` and `Fun Valley Amusment park.dat` (both real, unmodified saves, see
   `OpenCobra/Tests/Fixtures/Parks/`) end-to-end with plain `BinaryReader` calls and produces a
   non-empty, well-formed entry list for both (`DatTests.cs`, 2/2 passing) — if either file were
   compressed or encrypted, the struct-table/entry-list framing would have failed to parse rather
   than silently succeeding, since a compressed byte stream wouldn't coincidentally match the
   expected string-length-prefix/field-kind/size framing this reader expects at every step.
3. **Resolved**: rendering path is not a gap — `TerrainMeshBuilder` and the path rendering model
   (decal for at-grade, piece models for raised, per `path-network.md`) already exist; this plan
   only needs to call into them after populating `Park`.

## Open Questions

- How to reverse-engineer `GETerrain`/`PathTileList`/`PathNodeArray`'s byte layout (Gap #1).
  **Decided (user)**: search first for existing community decoders beyond `dat.rs` and
  belgabor's writeup (OpenRCT2-adjacent tooling, forks of `dat.rs`, older RCT3 map-editor
  projects) before doing original reverse-engineering; fall back to hex-diffing two saves with a
  known, isolated terrain edit if nothing turns up. **Searched (2026-07-15), found nothing**: GitHub
  code search (`gh search code`) for `PathTileList`/`PathNodeArray`/`GE_Terrain` turns up no hits
  outside this repo's own `dat.rs`; the local [`rct3-importer`](../../../../../rct3-importer)
  checkout's `libRawParse`/`libOVL` only cover OVL-format scenery import, not save parsing;
  belgabor's format writeup documents only the generic container framing, with no terrain/path
  section. **Fallback executed (2026-07-15)**: the user got RCT3 running again and captured six
  paired saves (baseline + 5 isolated terrain edits) under
  [`Fixtures/Parks/Reverse Engineering/`](../../../../OpenCobra/Tests/Fixtures/Parks/Reverse%20Engineering).
  Byte-diffing them (via a throwaway console tool referencing `OpenCobra.Data`, per the
  scratch-scanner pattern) resolved `GETerrain`'s corner-height layout — see
  [rct3-terrain-getterrain-layout.md](../../../research/rct3-terrain-getterrain-layout.md) for the
  full writeup. Path edits weren't captured this round (deferred by the user); the same
  hex-diffing approach should work for `PathTileList`/`PathNodeArray` once path-edit fixture
  pairs exist.

- Non-Windows saved-parks folder location (macOS `~/Library/Application Support/...` vs. Linux
  equivalent under Proton/Wine) — not yet confirmed; only the Windows
  `Documents\RCT3\Parks` path is user-confirmed. Needs a real macOS/Linux RCT3 install to check,
  or community documentation, before implementing those branches.
- Whether scenario files (loaded via the existing "Setup Park" TODO button) use the same `.dat`
  container in a different folder, or a distinct extension/format entirely — not yet confirmed.
  `Editor.cs` already has "Setup Park" as a separate button from "Open", suggesting scenarios stay
  under that button regardless of the answer. Left open until scenario-loading is scoped as its
  own plan; the `ParkFileDialog`/`RCT3Paths` work here should stay narrowly scoped to the
  `Parks/` directory so it doesn't presume an answer.
- Whether `.prf` is a real RCT3 DAT-format extension — unconfirmed, see
  [rct3-non-ovl-dat-format.md](../../../research/rct3-non-ovl-dat-format.md). Doesn't block this
  plan (park loading only touches `Parks/*.dat`), but `OpenCobra.Data.Dat` is already generic
  enough that a future plan can point it at whatever `.prf` turns out to be without rework, same
  as `.trk`/`.fwd`/`.frw`.

## Deferred

- Writing/saving parks (`.dat` write path) — this plan is read-only.
- Scenery, ride, guest, and finance data decoding from the same save — the `GETerrain`/
  `PathTileList` decoder built here should still leave the surrounding `DataEntry` list intact
  (i.e. don't discard unknown entries while parsing) so a later plan can decode the remaining
  field kinds without redoing the container-framing work.
- Scenario loading via "Setup Park" — out of scope per the Open Question above.

## Testing

- `OpenCobra.Data.Dat` (implemented, port of `dat.rs`): [`DatTests.cs`](../../../../OpenCobra/Tests/Data/DatTests.cs)
  covers the happy path — auto-discovers every embedded `.dat` fixture under
  [`Fixtures/Parks/`](../../../../OpenCobra/Tests/Fixtures/Parks) (currently `Rivendell.dat` and
  `Fun Valley Amusment park.dat`, both attributed) and asserts `Dat.Load` parses without throwing
  and returns a non-empty entry list. Still missing: assertions on *which* struct/field names and
  counts are expected (so a framing regression fails loudly instead of just "still non-empty"),
  coverage of the extended-header version-byte branch if a fixture using the other version
  (`0x1A` vs `0x2A`) turns up, and a failure-case test (truncated/corrupt file raises `DatException`
  rather than an unhandled `EndOfStreamException` or index-out-of-range).
- `GETerrain` decoder: known-good case (a save with a hand-verified height at a known tile,
  cross-checked against loading the same save in RCT3 itself or against `terrain-heightmap.md`'s
  existing corner-height conventions), edge case (a save containing water/cliffs), failure case
  (truncated/corrupt terrain block raises a clear exception rather than reading out of bounds).
- `PathTileList`/`PathNodeArray` decoder: known-good case (a save with a short straight path run,
  tile positions cross-checked against `PathTile`/`PathRaisedSlope` conventions from
  `path-network.md`), edge case (raised path with a slope), failure case (malformed entry).
- `RCT3Paths.SavedParksDirectory`: unit test that it resolves without throwing when
  `MyDocuments` exists, independent of whether the RCT3 folder itself exists (folder-missing is a
  valid state — dialog should show "no saves found", not crash).
- `ParkFileDialog`: no automated UI test (ImGui windows aren't covered elsewhere in this repo
  either); verify manually in user testing.

## Status

In progress. Implemented and tested: the `OpenCobra.Data` project
([`Data.csproj`](../../../../OpenCobra/Data/Data.csproj)) with
[`Dat`](../../../../OpenCobra/Data/DAT.cs), a full port of `dat.rs`'s header/struct-table/entry-list
framing (`DatFieldKind`, `DatValue` hierarchy, `DatOpaqueValue` for the still-undecoded kinds).
Validated against two real, attributed saved-park fixtures under
[`OpenCobra/Tests/Fixtures/Parks/`](../../../../OpenCobra/Tests/Fixtures/Parks) via
[`DatTests.cs`](../../../../OpenCobra/Tests/Data/DatTests.cs) (2/2 passing) — this also resolved
Gap #2 (saves are not compressed/encrypted at the container level).

Also done: `GETerrain`'s corner-height layout is reverse-engineered (float32, 12-byte corner
stride, +1.0/click) via byte-diffing real fixture pairs — see
[rct3-terrain-getterrain-layout.md](../../../research/rct3-terrain-getterrain-layout.md). Still
open within that: the rest of the 12-byte corner record, surface-type's exact offset, and water's
sign convention.

Not yet started: a `GETerrain` decoder in `OpenCobra.Data` (or `ParkFile`) that turns those known
offsets into structured data, `PathTileList`/`PathNodeArray` decoding (no path-edit fixtures
captured yet), `ParkFile.Load`, the "Open" button,
`ParkFileDialog`, and `RCT3Paths.SavedParksDirectory`.
