# Render Grass Texture from Terrain OVL — Implementation Notes

## Summary

The terrain ground plane now renders a real decoded grass texture (`Terrain_06`, from
`terrain/RCT3/Terrain_RCT3.common.ovl`) instead of a flat green fill. Builds on the `tex`/`flic`/`btbl`
decode work in [ovl-texture-decoding.md](ovl-texture-decoding.md) and the unified `TextureCollection`
model in [ovl-materials-integration.md](ovl-materials-integration.md).

## What landed

- **`OVL.Files.Texture.TakeMip(int level)`** ([TextureDecoding.cs](../../../OpenCobra/OVL/Files/TextureDecoding.cs))
  removes and returns a mip, nulling the source slot so `Dispose()` skips it. GDK `Texture`'s `texture`
  ctor parameter is now `[TakesOwnership]`-annotated, closing the `GDK002` analyzer warning it used to raise.
- **`Terrain.Load()`** ([Terrain.cs](../../../OpenRCT3/Simulation/Terrain.cs)) extracts the terrain OVL's
  `TextureCollection`, takes `Terrain_06`'s mip 0 via `TakeMip`, and wraps it in a GDK `Texture` for
  `Terrain.GrassTexture` — then disposes every extracted OVL texture, safe because `TakeMip` already
  detached the one mip still in use.
- **`Terrain_06` is grass, not `Terrain_00`** — confirmed by eye against `assets/prod/terrain/Terrain_*.png`
  (`Terrain_00` is dirt). No `ter`/`TerrainType` decode exists yet to give this an authoritative source;
  tracked separately in [`ovl-terrain-types.md`](../../plans/features/ovl/ovl-terrain-types.md).
- **`FileTypeExtensions.StripOvlTagSuffix`** ([FileTypes.cs](../../../OpenCobra/OVL/Files/FileTypes.cs))
  strips a symbol's `.tex`/`:tex`-style tag suffix, applied at `OVL.Files.Texture` construction. Without
  this, `Texture.Name` kept the raw tagged symbol name (e.g. `"Terrain_06.tex"`), so `Terrain.Load()`'s
  `Contains("Terrain_06")` lookup silently missed and the terrain rendered flat green with no error.
- **`TerrainMeshBuilder`** ([TerrainMeshBuilder.cs](../../../OpenRCT3/Simulation/TerrainMeshBuilder.cs))
  emits a `TexCoord` per vertex, mapping each tile's four corners directly to the unit square
  (`(0,0)`/`(1,0)`/`(1,1)`/`(0,1)`) rather than scaling from world-space position — one texture tile per
  grid square, chosen for testing simplicity over continuous world-space tiling.
- **`Game.cs`** switches the terrain `Model`'s material to `Textured` (vertex color `White`, a pure tint
  multiplier) when `GrassTexture` is set, falling back to `Flat` + the original flat green otherwise.

## Testing

`TakeMipTests` in [TexturesTests.cs](../../../OpenCobra/Tests/OVL/TexturesTests.cs) cover the ownership
transfer (slot nulled, no double-dispose, safe hand-off into a GDK `Texture`).
[FileTypeNameManglingTests.cs](../../../OpenCobra/Tests/OVL/FileTypeNameManglingTests.cs) and
[TextureNameManglingTests.cs](../../../OpenCobra/Tests/GDK/TextureNameManglingTests.cs) cover
`StripOvlTagSuffix`. `IngestionTests.cs`'s `LoadTerrainTexture_Succeeds` loads the real terrain OVL (gated
on `RCT3_PATH`) and asserts `Textures.Extract(ovl)["Terrain_06"]` decodes a non-empty mip 0. Confirmed
visually in-game.

## References

- [OpenCobra/OVL/Files/TextureDecoding.cs](../../../OpenCobra/OVL/Files/TextureDecoding.cs) — `TakeMip`
- [OpenCobra/OVL/Files/FileTypes.cs](../../../OpenCobra/OVL/Files/FileTypes.cs) — `StripOvlTagSuffix`
- [OpenRCT3/Simulation/Terrain.cs](../../../OpenRCT3/Simulation/Terrain.cs) — load site
- [OpenRCT3/Simulation/TerrainMeshBuilder.cs](../../../OpenRCT3/Simulation/TerrainMeshBuilder.cs) — UVs
- [OpenRCT3/Game.cs](../../../OpenRCT3/Game.cs) — material selection
