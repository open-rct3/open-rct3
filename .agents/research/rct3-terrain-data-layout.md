# RCT3 `GETerrain` Field Layout

Reverse-engineered by byte-diffing paired saved-park `.dat` fixtures under
[`OpenCobra/Tests/Fixtures/Parks/Reverse Engineering/`](../../OpenCobra/Tests/Fixtures/Parks/Reverse%20Engineering)
— a `baseline.dat` and several variants, each differing from the baseline by exactly one known,
isolated in-game edit — cross-checked against two real, differently-sized parks
(`Rivendell.dat`, `Fun Valley Amusment Park.dat`). Loaded via [`OpenCobra.Data.Dat`](../../OpenCobra/Data/DAT.cs),
which parses the surrounding struct/entry framing but leaves the `GETerrain` field itself as an
opaque [`OpaqueValue`](../../OpenCobra/Data/DAT.cs) blob — this doc is what fills that blob in.
Implemented in [`OpenCobra/Data/Parks/Terrain.cs`](../../OpenCobra/Data/Parks/Terrain.cs).

**Used by**:
- [`render-loaded-parks.md`](../plans/features/terrain/render-loaded-parks.md) —
  Gap #1 (the `GETerrain`/`PathTileList` byte layout being unknown).

## Where it lives

Top-level `DatEntry` named `RCT3Terrain`, field `EngineTerrain`.

## Overall structure

```
[6-byte mini-header][12-byte preamble record][Width x Height tile slots, 24 bytes each]
```

- **Mini-header** (6 bytes): byte 0 = declared grid `Width` in tiles, byte 1 = declared grid
  `Height` in tiles, bytes 2-5 unknown (constant across every sample so far - never observed to
  change).
- **Preamble record** (12 bytes, byte offset 6-17): unknown purpose; constant across every sample
  so far.
- **Tile array** (byte offset 18 onward): exactly `Width x Height` slots, each 24 bytes = two
  back-to-back 12-byte records.

This exactly accounts for the blob's total byte length in every sample checked:
`18 + Width x Height x 24`. Confirmed exact for three independent parks:

| Park | Width x Height | Blob size (bytes) | `18 + W*H*24` |
|---|---|---|---|
| baseline.dat / Rivendell.dat | 128 x 128 | 393,234 | 393,234 |
| Fun Valley Amusment Park.dat | 95 x 122 | 278,178 | 278,178 |

This resolves an earlier, incorrect model that treated the blob as one flat array of independent
12-byte corner records starting after a 6-byte header - that model's derived corner count didn't
factor into a tile-grid shape for Fun Valley's map (23,181 = 3 x 7727), which is what led to
re-deriving the structure here.

## Per-tile records

Each tile owns two adjacent 12-byte records = 24 bytes. A scratch tool
(`Dat.Load` on `baseline.dat` and each `01-*.dat`/`02-*.dat` variant, full-array byte diff of the
raw `EngineTerrain` blob, tile index computed from the confirmed `18 + i*24` formula) produced
these exact diffs:

`01-one-corner-up.dat` vs `baseline.dat` - only 2 bytes differ, both in tile 2902's first record:
```
offset=  69668  base=0x00  var=0x80  tile 2902 (first record), offsetInRecord=2
offset=  69669  base=0x00  var=0x3F  tile 2902 (first record), offsetInRecord=3
```
Bytes 0-1 of that `float32` are already `0x00` in the baseline, so this is offset-0's `float32`
going from `0.0` to `1.0` (`00 00 80 3F`).

`01-one-corner-and-other-corner-up.dat` vs `baseline.dat` - 3 bytes differ, all still tile 2902:
```
offset=  69669  base=0x00  var=0x40  tile 2902 (first record), offsetInRecord=3
offset=  69680  base=0x00  var=0x80  tile 2902 (second record), offsetInRecord=2
offset=  69681  base=0x00  var=0x3F  tile 2902 (second record), offsetInRecord=3
```
First record's offset-0 `float32` is now `2.0` (two raise-clicks); second record's offset-0
`float32` (`Height`) went from `0.0` to `1.0`.

`01-water-added.dat` vs `baseline.dat` - 8 bytes differ, all tile 2766:
```
offset=  66404  base=0x00  var=0x80  tile 2766 (first record), offsetInRecord=2
offset=  66405  base=0x00  var=0xBF  tile 2766 (first record), offsetInRecord=3
offset=  66408  base=0x00  var=0x80  tile 2766 (first record), offsetInRecord=6
offset=  66409  base=0x00  var=0xBF  tile 2766 (first record), offsetInRecord=7
offset=  66412  base=0x00  var=0x80  tile 2766 (first record), offsetInRecord=10
offset=  66413  base=0x00  var=0xBF  tile 2766 (first record), offsetInRecord=11
offset=  66416  base=0x00  var=0x80  tile 2766 (second record), offsetInRecord=2
offset=  66417  base=0x00  var=0xBF  tile 2766 (second record), offsetInRecord=3
```
This is the finding that overturns the old model: the first record's bytes 4-7 and 8-11 *also*
move here (each `00 00 80 BF` = `-1.0`), even though `01-one-corner-up.dat` only ever touched
bytes 0-3. That rules out "one float32 + 8 unknown bytes" for the first record - it's **three**
back-to-back `float32`s (offsets 0, 4, 8), independently steppable (only the first moves for a
single-corner raise; all three move together here).

**Confirmed structure per tile (24 bytes)**:
- First record (offset 0-11 of slot): three `float32`s - `SouthEast`@0, `SouthWest`@4, `NorthEast`@8.
  All default to `0.0`.
- Second record (offset 12-23 of slot): `NorthWest` (`float32`@0) and `SurfaceType`
  (`byte`@4, defaults to `0x0B`/11). Bytes 5-11 (7 bytes) still not decoded - every variant tested
  leaves them unchanged from baseline's `00`.

**Conclusions**:
- One raise-tool click = exactly `+1.0` on whichever of the four floats it targets (matches
  [`terrain-heightmap.md`](../plans/features/terrain-heightmap.md)'s 1m corner-height step from
  [`terrain-tools.md`](terrain-tools.md)).
- A tile has four independently-steppable height floats, not two. `01-one-corner-and-other-corner-up.dat`'s
  "raise a second corner" edit touched `SouthEast` (again) and `NorthWest` on the *same*
  tile - not a different tile - confirming these are per-tile fields, not per-grid-corner ones.
- `01-water-added.dat`'s in-game edit lowered the tile by one raise/lower-tool click (1m) before
  placing water on it: all four corner floats become `-1.0` uniformly (ordinary flatten/lower, not
  a water-specific sentinel). The actual water-placement data lives in the separate top-level
  `WaterManager` entry instead (see Water below), not in `EngineTerrain` at all.

### Corner identity (`01-one-far-corner-up.dat`, `01-near-left/right-corner-up.dat`, `01-far-left-corner-up.dat`)

The game has no in-editor coordinate readout or compass, so these fixtures instead identify each
corner relative to the fixed camera facing at a new park's start (camera is inside the park,
facing outward toward the boundary skirt/entrance).

`01-one-far-corner-up.dat` raised "the tile corner furthest to the left of the park's entrance,"
on a tile at the park boundary:
```
=== 01-one-far-corner-up.dat ===
Total differing bytes: 2
  offset=   3072  base=0x00  var=0x80  tile 127 (first record), offsetInRecord=6
  offset=   3073  base=0x00  var=0x3F  tile 127 (first record), offsetInRecord=7
Distinct tiles touched: [127]
```
Tile index 127, under the confirmed row-major `index = row*Width + col` layout with `Width=128`,
is row 0, column 127 - literally one of the map's four corner tiles, consistent with the user's
report that the map's boundary skirt mesh visually reacted to this edit (checked: the separate
`RCT3Terrain.SkirtTrees` opaque field is byte-identical, `00000000`, in both files - the skirt
reaction is the renderer responding live to `EngineTerrain` height near the map edge, not a
second stored copy of the height).

Three follow-up fixtures then isolated each of tile 2902's (the same interior tile used
throughout) four corners individually, described by the user relative to that tile:
"near-left" = the corner matching the far-corner edit above; "near-right" = the corner nearest the
park entrance; "far-left" = the corner opposite near-right, nearest the camera, furthest from the
entrance. Full-array diffs against `baseline.dat`:
```
=== 01-near-left-corner-up.dat ===
Total differing bytes: 2
  offset=  69672  base=0x00  var=0x80  tile 2902 (first record), offsetInRecord=6
  offset=  69673  base=0x00  var=0x3F  tile 2902 (first record), offsetInRecord=7
=== 01-near-right-corner-up.dat ===
Total differing bytes: 2
  offset=  69668  base=0x00  var=0x80  tile 2902 (first record), offsetInRecord=2
  offset=  69669  base=0x00  var=0x3F  tile 2902 (first record), offsetInRecord=3
=== 01-far-left-corner-up.dat ===
Total differing bytes: 2
  offset=  69680  base=0x00  var=0x80  tile 2902 (second record), offsetInRecord=2
  offset=  69681  base=0x00  var=0x3F  tile 2902 (second record), offsetInRecord=3
```

**Resolved mapping**:
- `SouthEast` (first record, offset 0) - directly diffed via `01-near-right-corner-up.dat` and
  `01-one-corner-up.dat` (same slot).
- `SouthWest` (first record, offset 4) - directly diffed via `01-near-left-corner-up.dat` and
  `01-one-far-corner-up.dat` (same slot, on the map-edge tile).
- `NorthWest` (second record, offset 0) - directly diffed via `01-far-left-corner-up.dat`.
- `NorthEast` (first record, offset 8) - **deduced by elimination**, not independently diffed: a
  tile has exactly four corners, and the other three are each pinned to a distinct slot above, so
  the one remaining slot (first record, offset 8) must be `NorthEast`. No fixture isolates this
  slot alone.

The user's original "near/far"/"left/right" labels (matching the fixture filenames above) are
relative to the fixed camera facing at a new park's start, not compass directions - the game
exposes no absolute coordinate system to derive true NE/NW/SE/SW from, and the local
`rct3-importer` C++ reference checkout was checked and contains no terrain corner/height code to
cross-reference (`grep -i corner` there only matches scenery/path code, not `GE_Terrain`).
`Terrain.cs`'s `SouthEast`/`SouthWest`/`NorthEast`/`NorthWest` field names are a naming-convention
alignment with `OpenRCT3.Simulation.TerrainCornerSlot`'s SW/SE/NW/NE scheme (mapping
near→south, far→north, left→west, right→east) for consistency across the codebase, not an
independently-verified world-space compass claim.

## Surface type

A single byte (`SurfaceType`, second-record offset 4) that changes from `0x0B` to `0x1F` when
repainting a tile's terrain texture in `01-surface-changed.dat`. A fresh full-array byte diff (not
reused old numbers) against `baseline.dat` finds exactly 11 differing bytes, one per tile, at
these tile indices:

```
Distinct tiles touched: [2512, 2513, 2639, 2640, 2641, 2767, 2768, 2769, 2895, 2896, 2897]
Gaps between consecutive touched tiles: [1, 126, 1, 1, 126, 1, 1, 126, 1, 1]
```

Four clusters (`[2512,2513]`, `[2639,2640,2641]`, `[2767,2768,2769]`, `[2895,2896,2897]`), each
cluster's start landing 126 tiles after the previous cluster's start - `128 (Width) - 2`. With
row-major indexing this means each successive row of the paint brush starts 2 columns further left
than the row above it: a diamond/circle-shaped brush growing from 2 tiles wide to 3, not a
row-stride bug. This resolves the earlier open question about the 126-vs-128 spacing.

`0x0B` = 11, `0x1F` = 31 - plausible small terrain-type-table indices, not yet cross-checked
against `OpenCobra.OVL`'s `TerrainType`/`ter` OVL resources or in-game terrain-type ordering.

`01-surface-changed.dat`'s top-level entry count also grew by 2 (3945 → 3947) versus baseline,
separate from the `EngineTerrain` blob itself (whose byte length is unchanged) - likely a new
terrain-type reference entry added elsewhere in the file, not investigated further here.

## Water (`WaterManager`, a separate top-level entry, not `RCT3Terrain`)

Not stored in `EngineTerrain` - lives in its own top-level `DatEntry` named `WaterManager`, field
`WaterManager` (also an opaque blob). Grows from 6 bytes in `baseline.dat` to 22 bytes in
`01-water-added.dat`:

| Fixture | Bytes |
|---|---|
| `baseline.dat` | `80 80 00 00 00 00` |
| `01-water-added.dat` | `80 80 01 00 00 00 00 00 00 00 02 00 00 00 4E 15 00 07 4E 15 01 07` |

Bytes 0-1 (`80 80` = 128, 128) match `EngineTerrain`'s own `Width`/`Height` mini-header exactly.
Byte 2 flips `0x00` → `0x01` when the one water pool is added - a pool count. The 16 bytes appended
after that (`00 00 00 00 00 00 02 00 00 00 4E 15 00 07 4E 15 01 07`, only present in the
water-added variant) are presumably one pool record; not decoded further, but `0x154E` (5454)
appears twice within it (at what would be offsets 2 and 6 into the 16-byte record, read as `u16`),
suggesting a repeated coordinate/index field.

## Path edits don't touch `EngineTerrain`

Full-array byte diff of `02-one-tile-added.dat`, `02-two-tiles.dat`, and `02-one-raised-tile.dat`
(each a path-placement edit, see fixtures README) against `baseline.dat` finds **zero** differing
bytes in `EngineTerrain` for all three - confirmed live:

```
=== 02-one-tile-added.dat ===
Total differing bytes: 0
=== 02-two-tiles.dat ===
Total differing bytes: 0
=== 02-one-raised-tile.dat ===
Total differing bytes: 0
```

Path data lives entirely outside `EngineTerrain`, per [rct3-path-tile-layout.md](rct3-path-tile-layout.md).

## Still unresolved

- The preamble record's (byte offset 6-17) purpose - confirmed byte-identical to baseline across
  all 8 variants tested (`01-one-corner-up`, `01-one-corner-up-again`,
  `01-one-corner-and-other-corner-up`, `01-water-added`, `01-surface-changed`,
  `02-one-tile-added`, `02-two-tiles`, `02-one-raised-tile`), but its purpose is still unknown.
- Bytes 2-5 of the mini-header (also unchanged across the same 8 variants).
- The second record's 7 bytes after `SurfaceType` (`TerrainTile.Unknown`, bytes 5-11) - unchanged
  across every variant tested.
- `NorthEast` (first record, offset 8) was deduced by elimination, not independently diffed - no
  fixture isolates that slot alone.
- Absolute (compass/world-space) identity of `SouthEast`/`SouthWest`/`NorthEast`/`NorthWest` - these
  are only known relative to the fixed camera facing at a new park's start, since the game exposes
  no coordinate system to derive absolute directions from.
- `WaterManager`'s 16-byte per-pool record layout beyond the repeated `0x154E` field noted above -
  only one sample (a single one-tile pool) exists so far.

## Still unattempted

- A fixture isolating `NorthEast` (first record, offset 8) alone, to move it from "deduced by
  elimination" to independently diffed.
- A second, differently-placed/differently-sized water pool, to isolate `WaterManager`'s per-pool
  record fields (position, extent, height) from the single sample decoded so far.
