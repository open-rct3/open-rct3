# RCT3 `GETerrain` Field Layout

Reverse-engineered by byte-diffing paired saved-park `.dat` fixtures under
[`OpenCobra/Tests/Fixtures/Parks/Reverse Engineering/`](../../OpenCobra/Tests/Fixtures/Parks/Reverse%20Engineering)
— a `baseline.dat` and several variants, each differing from the baseline by exactly one known,
isolated in-game edit. Loaded via [`OpenCobra.Data.Dat`](../../OpenCobra/Data/DAT.cs), which parses
the surrounding struct/entry framing but leaves the `GETerrain` field itself as an opaque
[`DatOpaqueValue`](../../OpenCobra/Data/DAT.cs) blob — this doc is what fills that blob in.

**Used by**:
- [`render-fluctuating-terrain.md`](../plans/features/terrain/render-fluctuating-terrain.md) —
  Gap #1 (the `GETerrain`/`PathTileList` byte layout being unknown).

## Where it lives

Top-level `DatEntry` named `RCT3Terrain`, field `EngineTerrain`. In every fixture tested (baseline
and all terrain-edit variants), this blob is a fixed **393,234 bytes**, regardless of what terrain
edit was made — confirms it's a flat, pre-allocated grid, not a variable-length list.

## Per-corner height

A `float32`, one per grid corner, arranged in a **12-byte stride** (i.e. each corner's record is
(at least) 12 bytes; the height float is the first field of that record — not confirmed independently
of the stride, since no fixture yet isolates a *non-first* field at a known offset within one record).

**Evidence**:

| Fixture | Offset | Base | Variant | Delta |
|---|---|---|---|---|
| `01-one-corner-up.dat` | `0x11022` | `0.0` | `1.0` | `+1.0` |
| `01-one-corner-up-again.dat` (from `01-one-corner-up`) | `0x11022` | `0.0`* | `2.0` | `+1.0` from the 1.0 state |
| `01-one-corner-and-other-corner-up.dat` | `0x11022` | `0.0` | `2.0` | (same corner as above) |
| `01-one-corner-and-other-corner-up.dat` | `0x1102E` | `0.0` | `1.0` | `+1.0` |

\* "Base" here is `baseline.dat`'s value (0.0); the diff was computed against `baseline.dat` for
every variant, not chained variant-to-variant, so `01-one-corner-up-again`'s on-disk value is
`2.0`, reached via two in-game raise-clicks from the 0.0 baseline.

**Conclusions**:
- One raise-tool click = **exactly `+1.0`** in this float, confirming it's a direct meters value
  (matches [`terrain-heightmap.md`](../plans/features/terrain-heightmap.md)'s 1m corner-height
  step from [`terrain-tools.md`](terrain-tools.md)), not a scaled/quantized integer.
- A second corner (the user's "other corner of the same tile" edit) has its height float exactly
  **12 bytes (`0xC`)** after the first corner's — i.e. `0x1102E - 0x11022 = 12`. This is the
  corner-to-corner stride within the grid, not necessarily "the two corners of one tile" in the
  narrow sense — it's equally consistent with "the next corner in row-major order" if the user's
  in-game click happened to land on an adjacent grid corner. Either reading gives the same 12-byte
  stride fact; which corner-adjacency relationship it represents (same-row neighbor vs. diagonal
  tile corner) isn't yet distinguished.

**Unresolved**: the other 8 bytes of each 12-byte corner record. Not yet isolated to specific
fields - could be a `Vector3` (X, Y, Height) with height as the 3rd component, a packed
normal/slope value, or something else. A save with only a horizontal-position-relevant edit (none
attempted yet) would help isolate this.

## Surface type

A single byte (top 3 bytes of its containing `uint16`-ish window are zero) that changes from
`0x0B` to `0x1F` when repainting a tile's terrain texture in `01-surface-changed.dat`. Appears at
the same relative offset in each of several adjacent corner records (offsets `0xEBA0`, `0xEBB8`,
`0xF788`, `0xF7A0`, `0x10388`, `0x103A0`, `0x10F88`, `0x10FA0` — each pair 12 bytes apart within
one edit, consistent with the corner stride above; the gap between pairs, e.g. `0xEBB8` to
`0xF788` = `0xBD0` = 3024 bytes = 252 corners, is a plausible terrain-grid row width, unconfirmed).

Two things worth noting:
- Multiple corners changed for a single "repaint one tile" edit (11 bytes total across 4 offset
  pairs) — repainting likely touches every corner of the affected tile(s), or the brush affected
  more than one tile.
  `01-surface-changed.dat`'s top-level entry count also grew by 2 (3945 → 3947) versus baseline,
  separate from the `EngineTerrain` blob itself (which stayed the same 393,234-byte size) — likely
  a new terrain-type reference entry added elsewhere in the file, not investigated further here.
- `0x0B` = 11, `0x1F` = 31 — plausible small terrain-type-table indices, but not yet cross-checked
  against `OpenCobra.OVL`'s `TerrainType`/`ter` OVL resources or in-game terrain-type ordering.

**Unresolved**: exact byte offset of this field within the 12-byte corner record (relative to the
height float), and its full value range/meaning.

## Water

Four consecutive `float32` fields, each exactly **4 bytes apart** (not the 12-byte corner stride
above — a separate, tightly-packed sub-structure), all flipping from `0.0` to `-1.0` in
`01-water-added.dat`:

| Offset | Base | Variant |
|---|---|---|
| `0x10362` | `0.0` | `-1.0` |
| `0x10366` | `0.0` | `-1.0` |
| `0x1036A` | `0.0` | `-1.0` |
| `0x1036E` | `0.0` | `-1.0` |

Four floats, likely one per tile corner (contiguous rather than 12-byte-strided, unlike the height
grid), most plausibly a water-surface height/flag using `-1.0` as a "no water" or
"below/at-ground" sentinel rather than an absolute elevation — an absolute water height would be
expected to vary with the actual water level added, not land on the same round sentinel across
all 4 corners regardless of where the tile sits.

**Unresolved**: whether `-1.0` means "water present here" or "water absent here" (i.e. sign
convention), how this 4-float block's position relates to the height grid's corner indexing (its
offset, `0x10362`, is *before* the height-diff fixtures' `0x11022`/`0x1102E`, so it isn't simply
"the water level at the same corner index" - it may be a wholly separate sub-array), and whether
non-sentinel values ever appear (untested - only a single water tile add/no-add pair exists so
far).

## Still unattempted

- Path edits (`PathTileList`/`PathNodeArray`) - deferred per the plan; no path-edit fixture pairs
  exist yet.
- A grid-position-isolating edit (e.g. a corner at a known, reported in-game tile coordinate) to
  derive the grid's width/height and confirm the row-stride hypothesis under Surface type above.
- A fixture pair with a non-sentinel water height, to determine whether the water floats store an
  absolute elevation or a flag.
