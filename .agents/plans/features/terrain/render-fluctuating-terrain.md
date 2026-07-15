# Render Fluctuating Terrain

## Context

Roadmap item: load a saved RCT3 park and render its terrain (and paths) in OpenRCT3. **Confirmed
(user, in-game observation)**: RCT3 saved parks are `.dat` files under `Documents\RCT3\Parks`
(e.g. `Rivendell.dat`) — this is *not* RCT2's `.sv6`/`.sc6` naming. **Confirmed (user + web
research)**: this is a distinct, *non-OVL* `.dat` container format — separate from the OVL-archive
object/asset DAT entries `OpenCobra/OVL` already reads — shared across most of RCT3's other
`Documents\RCT3\*` file kinds: `Coasters\*.trk` (track designs) and `Fireworks\*.fwd` (firework
displays, confirmed present locally as `Fireworks\Stratosphere.fwd`) per web research, plus
`*.frw` (firework effect definitions) which the user separately named as `.fwr`/`.prf` — `.fwr` is
likely the same file kind as `.frw` (extension typo), `.prf` unconfirmed by any source found so
far (flagged in Open Questions). Community documentation
([belgabor.vodhin.org/format](http://belgabor.vodhin.org/format/)) describes this shared container
as coming in three versions (v1: no header; v2/v3: a header with a version byte `0x1F`/`0x2F` and
fixed magic bytes `0xDA 0x1E 0xF1`), followed by a variable/class declaration section (Pascal-style
length-prefixed strings naming typed fields — `int32`, `float32`, `bool`, `array`, `struct`,
`reference`, `string`, etc.) and a data section of class-ID-tagged data blocks. This matches
[`assets/reference/dat/dat.rs`](../../../../assets/reference/dat/dat.rs)'s
`DataFile`/`DataStruct`/`StructField`/`DataEntry` model, confirming `dat.rs` is a reference for
*this* non-OVL format, not the OVL-internal object DAT format. `dat.rs` names the save-relevant
field kinds (`GETerrain`, `PathTileList`, `PathNodeArray`, `WaypointList`, `WaterManager`) but
treats every one as an opaque byte blob to skip — none of their internal layout is decoded there or
in the community documentation found so far. This plan has to reverse-engineer that layout from a
real saved-park `.dat` (starting from `Rivendell.dat`, already on hand).

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
  `OpenRCT3/Simulation/ParkFile.cs`, layered on a new `OpenCobra.OVL.SaveFile` (or similar) reader
  — named `SaveFile` for clarity even though the same reader/format also covers `.trk`/`.fwd`/
  `.frw`, since park-loading is this plan's only concrete consumer — that parses the non-OVL `.dat`
  struct-table framing (ported from `dat.rs`'s `DataFile`/`DataStruct`/`StructField` model, cross-
  checked against the [belgabor.vodhin.org/format](http://belgabor.vodhin.org/format/) header/
  version description) and decodes the `GETerrain` and `PathTileList`/`PathNodeArray` field bodies
  into `Terrain` and `Park.Paths` data — the layout of those two field kinds is unknown and must be
  derived empirically from sample saves (hex-diffing `Rivendell.dat` and any other saves the user
  can supply, per the existing scratch-scanner pattern used for OVL archives, adapted to this
  non-OVL container) since neither `dat.rs` nor the community documentation decodes them.
- Loading a park replaces the active `Park.Terrain`/`Park.Paths`/`Park.WaterPools` and triggers a
  `TerrainMeshBuilder` rebuild plus a path-mesh rebuild, so the existing renderer picks up the new
  data with no separate "park renderer" — rendering is already correct once the data model is
  populated, this plan is only about getting real save data into that model.
- Scope is read-only load of terrain + paths for rendering. Scenery, rides, guests, finances, and
  save (write) support are explicitly out of scope (see Deferred).

## Gaps and Risks

1. **Unresolved**: `GETerrain`/`PathTileList` byte layout is unknown; `dat.rs` treats them as
   opaque. This is the single largest risk — the whole plan depends on reverse-engineering these
   two field kinds from sample saved-park `.dat` files before any C# decoder can be written.
2. **Unresolved**: whether RCT3 saves are compressed/encrypted at the container level (some
   Frontier titles zlib-wrap save bodies) — unknown until a sample file's header is inspected.
3. **Resolved**: rendering path is not a gap — `TerrainMeshBuilder` and the path rendering model
   (decal for at-grade, piece models for raised, per `path-network.md`) already exist; this plan
   only needs to call into them after populating `Park`.

## Open Questions

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
- Whether `.prf` is a real RCT3 DAT-format extension (user-named alongside `.trk`/`.fwd`/`.fwr`) —
  no web source or local `Documents\RCT3` folder turned up a `.prf` file or reference to confirm
  what it stores. Doesn't block this plan (park loading only touches `Parks/*.dat`), but the
  shared non-OVL reader (`SaveFile`) should stay generic enough that a future plan can point it at
  whatever `.prf` turns out to be without rework, same as `.trk`/`.fwd`/`.frw`.

## Deferred

- Writing/saving parks (`.dat` write path) — this plan is read-only.
- Scenery, ride, guest, and finance data decoding from the same save — the `GETerrain`/
  `PathTileList` decoder built here should still leave the surrounding `DataEntry` list intact
  (i.e. don't discard unknown entries while parsing) so a later plan can decode the remaining
  field kinds without redoing the container-framing work.
- Scenario loading via "Setup Park" — out of scope per the Open Question above.

## Testing

- New `SaveFile`/DAT-framing reader (port of `dat.rs`): unit tests over one or more captured
  sample saved-park `.dat` files (checked into a test-fixtures location, not `RCT3_PATH` or the
  user's live `Parks` folder, since CI can't assume the game is installed) covering: header parse
  (both extended-header versions if both exist in the wild), struct-table parse producing the
  expected `DataStruct` count/names, and entry-list parse producing the expected entry count.
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
  either); verify manually per this repo's `drive-native-app` skill once implemented.

## Status

Not started. This is a planning-only pass: the file-open UI, folder-location helper, and
data-model wiring are straightforward given existing patterns, but the plan cannot move to
implementation until Gaps and Risks #1–2 are resolved — i.e. until sample saved-park `.dat` files
are obtained (`Rivendell.dat` is already on hand) and their `GETerrain`/`PathTileList` byte layout
is reverse-engineered. That
reverse-engineering is the first implementation step, not something this planning pass could
determine from source alone.
