# Phase 6: Render Grass Terrain

**Status**: Deferred — blocked on the open OVL texture-decoding bug
([`ovl-texture-decoding.md`](../../bugs/ovl-texture-decoding.md), `mms`/`prt`/`psi`/`fct` symbol-resolution
issue). Not required for the "flat, empty park" milestone, which is already complete — see
[`completed-work/flat-empty-park.md`](../../summaries/completed-work/flat-empty-park.md). This doc is rewritten
to match the terrain architecture actually implemented since it was first drafted; revisit once the texture
pipeline is fixed.

## What's already done (superseding the original per-tile-quad plan below)

The original version of this plan described building `Terrain` from scratch as a grid of textured quads. That
architecture no longer matches reality — terrain is now a real data model with its own design doc and
implementation:

- **`Terrain`/`TerrainCorner`/`TerrainCornerSlot`/`Edge`**: per-tile corner-height storage (not per-tile quads),
  with cliff detection via corner-height mismatch across edges. See
  [`features/terrain-heightmap.md`](../features/terrain-heightmap.md).
- **`TerrainMeshBuilder`**: generates a single mesh for the whole grid (top faces + cliff side-faces) from the
  corner data — not one quad per tile with an individually-applied texture. Currently solid-colored; see
  [`OpenRCT3/Simulation/TerrainMeshBuilder.cs`](../../../OpenRCT3/Simulation/TerrainMeshBuilder.cs).
- **`World`/`Park` integration**: `World` already owns `Park` and `Terrain` (`OpenRCT3/Simulation/World.cs`),
  loaded during `Game` startup — the "World Integration" task below is done, just not the way originally
  described (no separate `OpenRCT3/Terrain` namespace was created; `Terrain` lives in
  `OpenRCT3/Simulation/` alongside `Park`).
- **`TerrainCorner.SurfaceIndex`/`CliffIndex`**: storage for per-corner paint-type indices already exists
  (see `terrain-heightmap.md`), but nothing writes them yet (no paint tool) and nothing reads them for
  rendering — this is the actual remaining gap, not a from-scratch `Terrain` class.

## What's actually left (once the texture pipeline is fixed)

1. **Load `Terrain_RCT3.common.ovl`/`Terrain_RCT3.unique.ovl`** and decode the `Terrain_00`..`Terrain_25` /
   `Cliff_00`..`Cliff_05` texture entries (32 entries, per
   [`grass-texture-from-terrain-ovl.md`](../../research/grass-texture-from-terrain-ovl.md)) — this is the
   part actually blocked on the open texture bug.
2. **Extend `TerrainMeshBuilder`** (or a follow-on renderer) to sample `TerrainCorner.SurfaceIndex` per vertex
   and resolve it to the matching decoded texture/UV, instead of the current flat solid color. Blending across
   a tile for "Ground Blended" (`TerrainType.type == 2`) surfaces is an open question already tracked in
   `terrain-heightmap.md`'s Deferred section.
3. **A paint tool** to actually write `SurfaceIndex`/`CliffIndex` — none exists yet; until then every corner's
   `SurfaceIndex` defaults to `0` (whatever `Terrain_00` turns out to be, presumably grass), so a texture-aware
   renderer would currently paint the whole map with one texture regardless of blending logic.

## Dependencies

- `OpenCobra/OVL/Files/Textures.cs` (`Tex`/`Flic`/`BitmapTable` — blocked on `ovl-texture-decoding.md`)
- `OpenCobra/GDK/Assets/TextureLoader.cs`
- `OpenRCT3/Simulation/Terrain.cs`, `TerrainCorner.cs`, `TerrainMeshBuilder.cs`
- [`features/terrain-heightmap.md`](../features/terrain-heightmap.md) — current terrain data model and open
  blending question
