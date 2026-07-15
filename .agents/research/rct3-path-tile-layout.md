# RCT3 Path Tile Layout

Reverse-engineered by diffing paired saved-park `.dat` fixtures under
[`OpenCobra/Tests/Fixtures/Parks/Reverse Engineering/`](../../OpenCobra/Tests/Fixtures/Parks/Reverse%20Engineering)
(`02-one-tile-added.dat`, `02-two-tiles.dat`, `02-one-raised-tile.dat`, all diffed against
`baseline.dat`) — same fixture set and method as
[rct3-terrain-getterrain-layout.md](rct3-terrain-getterrain-layout.md), but path data turned out
not to need byte-level diffing at all.

**Used by**:
- [`render-fluctuating-terrain.md`](../plans/features/terrain/render-fluctuating-terrain.md) — the
  `PathTileList`/`PathNodeArray` half of Gap #1.

## Where it lives

Unlike `GETerrain`, path data is **not** packed into an opaque blob. `PathManager.TileList` (the
`PathTileList`-kind field) is present but always **empty** (`Size = 0`) in every fixture tested.
The real data lives as ordinary **top-level `DatEntry` records** — `PathTile` for at-grade tiles,
`PathFlying` for raised tiles — already fully decoded by [`OpenCobra.Data.Dat`](../../OpenCobra/Data/DAT.cs)'s
generic struct-table framing. No opaque-blob decoding was needed; see
[`OpenCobra/Data/Parks/Paths.cs`](../../OpenCobra/Data/Parks/Paths.cs) for the typed wrapper.

## At-grade tiles (`PathTile`)

One entry per tile, fields: `ColIndex`/`RowIndex` (`UInt8` grid coordinates), `Direction`
(`UInt8`, always `0` in every sample), `PathType` (`UInt8`, always `0` — likely a path-theme
index), `Surface` (`Reference`, points to the underlying terrain surface), `SurfaceType` (`UInt8`,
always `255` — plausible "none" sentinel), and an oddly-named on-disk field literally called
`bool` (`UInt8`, always `0`, not exposed by `Paths.cs` since its purpose is unknown).

**Note on entry `Id`**: `PathTile`/`PathFlying`/etc. `Id` values are **not stable across saves** —
re-saving the same park under a different name reassigns ids to unrelated entries. Identity across
two saves has to be established by content (`ColIndex`/`RowIndex`), not `Id`. This is why
`Paths.cs`'s API returns plain lists rather than an id-keyed lookup.

**Evidence**:

| Fixture | Baseline tile count | Variant tile count | New tile(s) (by Col/Row) |
|---|---|---|---|
| `02-one-tile-added.dat` | 20 | 21 | `(95, 25)` |
| `02-two-tiles.dat` | 20 | 22 | `(95, 25)`, `(94, 25)` |

The two-tile fixture's new tiles are adjacent (`RowIndex` matches, `ColIndex` differs by 1),
matching the user's described edit (place one tile, then a second connected to it) — but no
distinct "connectivity" field was found; adjacency here is purely positional (same as
`OpenRCT3.Simulation.PathTile`'s existing model, which also derives connectivity from
grid-adjacency rather than a stored link field).

## Raised tiles (`PathFlying`)

A **completely separate representation**, not a variant/flag on `PathTile`. Confirms
`render-fluctuating-terrain.md`'s existing note that raised paths render as separate 3D piece
models: `PathFlying` carries `BaseHeight`/`QuantisedHeight`/`SlopeType`, plus a `SceneryItem`
(`Reference`) pointing at a companion top-level `SceneryItem` entry representing the actual 3D
support piece placed in the world.

**Evidence** (`02-one-raised-tile.dat`, the only raised-tile sample so far): 0 → 1 `PathFlying`
entries, plus 1 new `SceneryItem` entry:

```
PathFlying (id=6652)
  BaseHeight: Int32 = -1
  ColIndex: UInt8 = 84
  Direction: UInt8 = 1
  PathType: UInt8 = 1
  QuantisedHeight: Int32 = 1
  RowIndex: UInt8 = 18
  SceneryItem: Reference = 0x1A0A
  SlopeType: UInt8 = 0
  Surface: Reference = 0x567
  SurfaceType: UInt8 = 255
  UndergroundFlag: Bool = False
  bool: UInt8 = 0

SceneryItem (id=6666)
  ...
  HEIGHTOFFSET: Int32 = 1
  SceneryItemDataField: struct
    POSX: Int32 = 84
    POSZ: Int32 = 18
    HEIGHT: Int32 = 1
    ...
```

The `SceneryItem`'s `SceneryItemDataField.POSX`/`POSZ`/`HEIGHT` (`84`, `18`, `1`) match the
`PathFlying`'s `ColIndex`/`RowIndex`/`QuantisedHeight` exactly, confirming the cross-reference.

**Unresolved**:
- `BaseHeight = -1` and `Direction = 1`/`PathType = 1` (differing from `PathTile`'s always-`0`
  samples) are only single-sample observations — not yet distinguished as meaningful vs.
  coincidental to this one capture.
- `SlopeType`'s non-flat values (only `0` observed) — matches
  [`path-network.md`](../plans/features/path-network.md)'s `PathRaisedSlope` enum
  (`Flat`/`Sloped`/`SteepStair`) conceptually, but the other two haven't been captured.

## Still unattempted

- `PathNodeArray`/`WaypointList` — not investigated; may relate to guest pathfinding rather than
  tile placement, unconfirmed.
- Path removal (does the entry disappear, or get flagged inactive?).
- A `PathType` value other than the default theme, and a `SurfaceType` other than `255`.
