# Decode TerrainType (TER) Entries — Implementation Summary

## Summary

The terrain type OVL decoder now extracts all TerrainType entries from production archives (`Terrain_RCT3.*.ovl` and `Terrain_CT.*.ovl`), exposing full metadata for each terrain surface type (Ground Unblended/Cliff/Ground Blended) via a `TerrainTypeEntry` record. `Terrain.Load()` now uses decoded metadata to identify grass, replacing a hard-coded `"Terrain_06"` string match with a type-based + nearest-color filter. The `ter-viewer` Dumper plugin renders full terrain metadata (type, colors, resolved symbol references, unknown fields).

## What landed

- **`OpenCobra/OVL/Files/TerrainTypes.cs`**: Decoder extracting all `ter` entries in parallel, mirroring
  `StaticShapes.cs` pattern. Exports:
  - `TerrainType` enum: `GroundUnblended`, `Cliff`, `GroundBlended`
  - `TerrainParameters` / `TerrainUnknowns` records: color and rendering parameters
  - `TerrainTypeEntry` record: the full 60-byte struct representation
  - `TerrainTypes.Extract(Ovl)`: parallel extraction with per-symbol error handling
  - `ReadRelocationString` helper: safely resolves TXT/GSI/TEX symbol references

- **`OpenCobra/Tests/OVL/TerrainTypesTests.cs`**: NUnit tests covering synthetic struct decode
  (field-offset verification), real-archive extraction (32 `Terrain_RCT3` + 6 `Terrain_CT` entries),
  and grass-identification path (nearest-color match to `0xFF4F810E` yields `Terrain_06`)

- **`plugins/ter-viewer/`**: Dumper plugin (index.ts + package.json + asconfig.json) rendering
  `TerrainTypeEntry` metadata: Version, Addon, Number, `Type` as enum name, colour swatches
  (`ColourSimple`/`ColourMap` as CSS `<div>` backgrounds), resolved symbol names
  (`TextureRef`/`DescriptionName`/`IconName`), unknown fields (`Unk02`/`Unk13`/`Unk14`/`Unk15`),
  plus hex-view fallback.

- **`OpenRCT3/Simulation/Terrain.cs::Load()`**: Replaced hardcoded `Contains("Terrain_06")` with
  decoded-metadata filter. Extracts `TerrainTypes`, filters to `Type==GroundBlended`, finds
  nearest-color match to grass color via `ColorDistance` helper, looks up `TextureRef` in
  `Textures.Extract()`.

- **`OpenCobra/Tests/Integration/IngestionTests.cs`**: Added `LoadTerrainTypes_DecodesAllEntries()`
  integration test verifying all 38 entries (32 RCT3 + 6 CT) decode successfully with valid
  metadata.

- **`plugins/README.md`**: Moved `ter-viewer` from "📋 Planned (5/11)" to "✅ Completed (7/11)",
  noting metadata-only render (pixel preview is deferred).

## Discovery findings

**Production OVLs with `ter` entries (confirmed)**:
- `terrain/RCT3/Terrain_RCT3.common.ovl`: 32 entries (`Cliff_00`–`05`, `Terrain_00`–`25`)
- `terrain/RCT3/Terrain_RCT3.unique.ovl`: same 32, byte-identical data (not "unique only" as
  research doc claimed)
- `terrain/CT/Terrain_CT.common.ovl`: 6 entries (`Terrain_26`–`31`), all `Addon=1` (Soaked)
- `terrain/CT/Terrain_CT.unique.ovl`: same 6, byte-identical data

**Terrain_CT deviations**: `Terrain_27`/`28` are `Type=GroundUnblended` (brownish, `InvWidth`/`InvHeight=0.25`, only deviation from `Terrain_RCT3`'s ~0.1); `Terrain_26`/`29`/`30`/`31` are `Type=GroundBlended`. No `Cliff`-type entries in this pack.

**Unknown fields investigation** (5-minute timebox + follow-up scan):
- `Unk02`: Constant `0` across all 38 entries. Likely reserved/unused.
- `Unk13`: Varies by entry, clusters by visual "roughness" family (`0.02` darkest/smoothest rock,
  `0.1` sand/dirt, `0.3` grass, `0.5`–`0.7` rocky). Speculative: blend-noise scale for
  `GroundBlended` auto-paint. Not confirmed.
- `Unk14`: Range `-1`–`4`. Original hypothesis (altitude-band weight for auto-snow) disproven by
  `Terrain_31` (near-black, `Unk14=4`), same value as light-colored entries. No independent
  pattern found across `Terrain_CT`. Speculative purpose unknown. Not confirmed.
- `Unk15`: Range `-0.5`–`1`, loosely tracks `Unk14`'s sign. No independent pattern. Not confirmed.

## Testing

- Unit tests in `TerrainTypesTests.cs`: synthetic struct decode (60-byte offset verification),
  real-archive decode (32 RCT3 + 6 CT entries), invariant checks (Version==1, Addon values),
  grass-identification path.
- Integration test in `IngestionTests.cs` (`LoadTerrainTypes_DecodesAllEntries`): real-archive
  sweep confirming all 38 entries decode with non-empty names and valid `TerrainType` values.
- All tests passing.

## Integration

`Terrain.Load()` now identifies grass via decoded metadata instead of a string literal, resolving
the "Terrain_00 is grass" uncertainty flagged in `grass-from-ovl.md`'s risk #5. The
`SurfaceIndex`/`CliffIndex` → `TerrainTypeEntry[]` lookup (built from `TerrainType.Number`) is
now available for `terrain-heightmap.md`'s consumers (stored per-corner, renderer still out of
scope).

## References

- [ovl-terrain-types.md](.agents/plans/features/ovl/ovl-terrain-types.md) — full plan with struct
  layout, production-OVL discovery, unknown-field investigation, integration wiring
- [grass-from-ovl.md](.agents/research/grass-from-ovl.md) — prior research on `ter` entries' `type`
  field (Ground/Cliff/Blended) and `texture_ref` pointer chain
- [terrain-heightmap.md](.agents/plans/features/terrain-heightmap.md) — consumer of
  `SurfaceIndex`/`CliffIndex` → `TerrainType` lookup for blended rendering (deferred)
