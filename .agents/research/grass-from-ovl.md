# Plan: Render Grass Texture from Terrain OVL — Research Findings

**Status**: Research complete. Implementation blocked on locating the terrain bitmap table.

**Used by**: [`terrain-heightmap.md`](../plans/features/terrain-heightmap.md) — the Terrain Heightmap Data
Model plan, whose `TerrainCorner.SurfaceIndex`/`CliffIndex` fields reference the `ter` entries and `type`
field (Ground Unblended / Cliff / Ground Blended) described here.

## Goal

Replace the solid `Color.FromArgb(79, 129, 14)` ground plane in [OpenRCT3/Game.cs:117](OpenRCT3/Game.cs) with a real grass texture loaded from the RCT3 install via the OVL pipeline.

## Research Summary

A working C# program iterated every OVL under the local install and inspected the `tex`/`ter` pointer chain in `terrain/RCT3/Terrain_RCT3.*.ovl`. Findings:

### Resources present in `Terrain_RCT3.{common,unique}.ovl`

- 32 `ter` entries: `Terrain_00`..`Terrain_25` + `Cliff_00`..`Cliff_05`
- 32 `tex` entries: same names (paired 1:1 with `ter`)
- **No `flic` entries, no `btbl` entries** in either file of the pair

### Pointer chain (per [icontexture.h](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/include/icontexture.h), [terraintype.h](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/include/terraintype.h))

```
ter.texture_ref  ──▶  tex (TextureStruct)
                          └─ Ts2Ptr  ──▶  TextureStruct2
                                              └─ Flic  ──▶  flic loader dataPtr
                                                               └─ extra data: 4-byte BTBL index
```

Sampled indices for `Terrain_00`..`Terrain_25`: `0, 1, 2, 5, 10, 15, 20, 25` — i.e. the flic loader's extra data is a **bitmap-table index**, not inline pixel data.

The 4-byte BTBL index means the actual grass pixels live in a bitmap table that is **not** stored in `Terrain_RCT3.*.ovl`.

### Bitmap-table search across the install

Scanned all 14,980 OVLs under `$RCT3_PATH`:

- `Main.*.ovl` — 1 BTBL (`GUIFontSmallNumbers:fct.btbl`)
- `Particles/Particles.*.ovl` — 1 BTBL (`FWFlares02_00:psi.btbl`)
- `Characters/*/*_Main.*.ovl` — 2 BTBLs each (mms + prt)
- `terrain/*/*.*.ovl` — **0 BTBLs**

No terrain bitmap table exists in the install under any path the OVL reader can resolve. `Textures.Extract(ovl)` therefore returns 0 textures from the terrain OVL pair.

### Cross-file relocation hypothesis (not yet verified)

The reference's `Textures.Extract` only searches for `btbl` in the same archive. RCT3 may store the terrain BTBL in a file the current loader does not associate with the terrain pair, or the flic pointer may need to be re-resolved against the common file's BTBL pool. The pending fix in [.agents/summaries/flic-decode-gaps.md](../summaries/flic-decode-gaps.md) ("#2 ~50 entries: Cross-file relocation retry") describes the same shape of bug for other archives.

### `TerrainType` struct layout (60 bytes, [terraintype.h](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/include/terraintype.h))

| Offset | Field | Notes |
|--------|-------|-------|
| 0 | `unk01` (u32) | structure version, always 1 |
| 4 | `unk02` (u32) | always 0 |
| 8 | `addon` (u32) | 0=Vanilla, 1=Soaked, 2=Wild |
| 12 | `number` (u32) | terrain index |
| 16 | `type` (u32) | 0=Ground Unblended, 1=Cliff, 2=Ground Blended |
| 20 | `texture_ref` (u32, relocated) | `TextureStruct*` → a `tex` entry |
| 24 | `description_name` (u32, relocated) | `char*` → `txt` in localization OVLs |
| 28 | `icon_name` (u32, relocated) | `char*` → `gsi` in Main OVL |
| 32 | `colour_simple` / `colour_simple_int` (u32) | small terrain colour, default `0xFFFF007F` |
| 36 | `colour_map` / `colour_map_int` (u32) | map-overlay colour |
| 40 | `inv_width` (f32) | 0.25 = 1 tile wide |
| 44 | `inv_height` (f32) | inverse height of texture |
| 48 | `unk13` (f32) | 0.3 |
| 52 | `unk14` (f32) | 0.0 |
| 56 | `unk15` (f32) | 0.5 |

`colour_simple_int` for grass matches `Color.FromArgb(79, 129, 14)` (BGRA = `0xFF4F810E` ⇒ bytes `4F 81 0E FF`). The `ter` entry's `type` field (Ground vs. Cliff vs. Blended) is the cleanest way to identify a candidate grass `ter` entry, then `colour_simple_int` disambiguates between the unblended grass variants.

### GDK side

- `Material.Flat` ([OpenCobra/GDK/Materials/Material.cs:68](OpenCobra/GDK/Materials/Material.cs)) ignores `AlbedoTexture`. Use `Material.Textured` ([Material.cs:94](OpenCobra/GDK/Materials/Material.cs)) instead.
- `Primitives.Plane` already supplies `TexCoord`s; the textured material samples `u_Texture` correctly.
- `Texture.Upload()` ([OpenCobra/GDK/Materials/Texture.cs:49](OpenCobra/GDK/Materials/Texture.cs)) already handles mip0 + GL upload; `Material.State` will go `Ready` once `AlbedoTexture.State == Ready` ([Material.cs:46](OpenCobra/GDK/Materials/Material.cs)).

## Remaining Unknowns

1. **Where is the terrain bitmap table?** The terrain OVL has only `tex` and `ter` entries; the flic indices (0..~25) point at a BTBL that no `btbl` resource in any OVL under `$RCT3_PATH` resolves to. Options to investigate:
   - A wider search (e.g. user-saved parks, scenario files, or the asset.dat)
   - Cross-file `btbl` re-resolution: extend `Textures.ReadTexture` to retry the flic pointer lookup against the common file's bitmap table when the same-archive lookup fails
   - Direct flic storage: confirm whether the 4-byte chunk is really a BTBL index or actually a small inline mip-0 `FlicHeader + data` blob
2. **Which `ter` entry is the grass?** Without a decoded BTBL we cannot visually verify, but `ter` entries with `type == 0` (Ground, Unblended) and `colour_simple_int == 0xFF4F810E` (BGRA 79,129,14) are the leading candidates. The `colour_simple` field is only meaningful for the editor UI, not for the rendered surface; the actual colour comes from the decoded texture.

## Proposed Implementation (once the BTBL question is resolved)

1. **Add `OpenCobra/OVL/Files/Terrain.cs`** — a `TerrainType` decoder mirroring `FlexiTexture.Load` ([OpenCobra/OVL/Files/FlexiTexture.cs:24](OpenCobra/OVL/Files/FlexiTexture.cs)):
   - Parse the 60-byte `ter` struct from `ovl.ReadResource(file)`
   - Resolve `texture_ref` to the matching `tex` entry's loader data pointer
   - Follow `Ts2Ptr` → `TextureStruct2.Flic` → flic loader extra data
   - Use the 4-byte index to look up the `FlicHeader`+pixels from a bitmap table (once cross-file resolution is in place)
2. **Extend `OpenCobra/OVL/Files/Textures.cs::ReadTexture`** to retry flic-data lookups against the common file's BTBL pool (or to a user-supplied BTBL) when the same-archive lookup yields only a 4-byte chunk without a sibling BTBL. This is the same fix [.agents/summaries/flic-decode-gaps.md](../summaries/flic-decode-gaps.md) describes for ~50 entries.
3. **Replace the stub in `OpenRCT3/Simulation/Terrain.cs::Load`** with a real implementation that:
   - Loads `Terrain_RCT3.common.ovl` via `Ovl.Load`
   - Iterates `ter` entries, picks the candidate grass `ter` (Ground, `colour_simple` == grass)
   - Follows `texture_ref` → `tex` → flic → decoded `Image<Rgba32>` and assigns `terrain.GrassTexture = new Texture(...)`
   - Disposes the `Ovl` after extraction
4. **Update `OpenRCT3/Game.cs:117`**:
   - Change `Material = new Flat()` to `Material = new Textured { AlbedoTexture = World.Terrain.GrassTexture }`
   - Drop the `Color.FromArgb(79, 129, 14).ToGl()` vertex colour
5. **Verify with `make test`** — `World.cs::Dispose` already calls `Terrain?.GrassTexture?.Dispose()`, so no extra cleanup is needed.
6. **Optional**: add a unit test in [OpenCobra/Tests/Integration/IngestionTests.cs:18](OpenCobra/Tests/Integration/IngestionTests.cs) that loads `Terrain_00` and confirms a non-empty `Image<Rgba32>` is returned.

## Files To Edit

- [OpenRCT3/Simulation/World.cs](OpenRCT3/Simulation/World.cs) — minor: already calls `Terrain.Load()` and disposes `GrassTexture`; no changes expected
- [OpenRCT3/Simulation/Terrain.cs](OpenRCT3/Simulation/Terrain.cs) — implement `Load`
- [OpenRCT3/Game.cs:117](OpenRCT3/Game.cs) — swap `Flat` for `Textured` + `AlbedoTexture`
- [OpenCobra/OVL/Files/Textures.cs](OpenCobra/OVL/Files/Textures.cs) — add cross-file BTBL resolution
- `OpenCobra/OVL/Files/Terrain.cs` — new file: `TerrainType` decoder

## References

- [icontexture.h](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/include/icontexture.h) — `TextureStruct` (60 B), `TextureStruct2` (8 B), `FlicStruct`, `FlicHeader`, `FlicMipHeader`, `BmpTbl` (8 B)
- [terraintype.h](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/include/terraintype.h) — `TerrainType` (60 B)
- [ManagerTER.cpp](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/src/libOVLng/ManagerTER.cpp) — confirms `ter` references `tex` via `texture_ref`
- [ManagerTEX.cpp](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/src/libOVLng/ManagerTEX.cpp) — confirms `tex` references a flic loader via `Ts2Ptr → TextureStruct2.Flic`
- [OVLDump.cpp](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/src/libOVLDump/OVLDump.cpp#L321) — `MakeFlics` handles both BTBL-indexed and standalone flic data
- [.agents/summaries/Parsing Texture Data.md](../summaries/Parsing%20Texture%20Data.md) — existing C# ingestion workflow
- [.agents/summaries/flic-decode-gaps.md](../summaries/flic-decode-gaps.md) — known issues with cross-file flic resolution (~50 of ~320 failures)
- [OpenCobra/OVL/Files/Textures.cs](OpenCobra/OVL/Files/Textures.cs) — existing `Tex` (60 B) and `BitmapTable` (8 B) structs
- [OpenCobra/GDK/Materials/Material.cs:94](OpenCobra/GDK/Materials/Material.cs) — `Textured` material
- [OpenCobra/GDK/Materials/Texture.cs:49](OpenCobra/GDK/Materials/Texture.cs) — `Texture.Upload` pipeline
- [OpenRCT3/Game.cs:117](OpenRCT3/Game.cs) — ground plane creation site
