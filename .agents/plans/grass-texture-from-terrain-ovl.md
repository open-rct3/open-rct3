# Plan: Render Grass Texture from Terrain OVL

**Status**: OVL `tex`/`flic`/`btbl` decoding now works. Plan pivots to a small follow-up: pull a grass texture out of the decoded `TextureCollection` and feed it into the terrain mesh as `AlbedoTexture`.

## What's already done (commit 2a35414 "🎉 Finally fix OVL texture decoding")

The blocker from the previous plan is resolved. Whole-install verification per [.agents/summaries/ovl-texture-scan.csv](../summaries/ovl-texture-scan.csv) and [.agents/summaries/completed-work/ovl-texture-decoding.md](../summaries/completed-work/ovl-texture-decoding.md):

- `terrain/RCT3/Terrain_RCT3.common.ovl`: **33 textures decoded** (32 `tex` + 1 `fct`), 0 crashes
- 7,490 `*.common.ovl` files scanned: **258 → 2,599 textures decoded**, 0 regressions
- `Main.common.ovl`: 0 → 84/84

The fixes (4 stacked root causes) that unblocked this are in [OpenCobra/OVL/OVL.cs](../../OpenCobra/OVL/OVL.cs) and [OpenCobra/OVL/Files](../../OpenCobra/OVL/Files):

1. **Relocation-fixup table is now parsed** (`Ovl.ReadRelocations` / `TryGetRelocationSource`) instead of seek-skipped. This is what gives the `tex → flic` chain its second hop — see `tex.rs::Texture::decode` in `assets/reference/ovl/`.
2. **`ReadTexture` chases `FlicPtr` (offset 52, double pointer, two relocation hops)**, not `Ts2Ptr` (offset 56, dead).
3. **`btbl` / `flic` are discovered via `Ovl.LoaderEntriesInOrder`** (walking the loader table directly), since they're loader-category tags not classified symbols. `Textures.Extract` now builds a `flicAddress → currentBtblTable` map from this walk.
4. **Bitmap-table mip count is read from disk (`FlicHeader.MipCount`)**, not derived from `log2(width)+1`. The old derivation desynced the shared pixel cursor on the first table entry and corrupted every entry after.

Decode code is split:

- [OpenCobra/OVL/Files/Textures.cs](../../OpenCobra/OVL/Files/Textures.cs) — `Textures.Extract(ovl)` is the single unified entry point. Returns a `TextureCollection` keyed by symbol name. Walks `LoaderEntriesInOrder` for `btbl`/`flic` and `ovl.Keys` for `tex`/`ftx` in one pass.
- [OpenCobra/OVL/Files/TextureDecoding.cs](../../OpenCobra/OVL/Files/TextureDecoding.cs) — shared plumbing (`Texture`, `TextureCollection`, `Tex` struct, `ReadTexture`, DXT decoders, `ComputeMipCount`).
- [OpenCobra/OVL/Files/Flic.cs](../../OpenCobra/OVL/Files/Flic.cs) — `Flic.Read(name, chunk, table?)` for both 4-byte BTBL indices and standalone flic blobs.
- [OpenCobra/OVL/Files/BitmapTable.cs](../../OpenCobra/OVL/Files/BitmapTable.cs) — `BitmapTables.Read` (symbol-driven) and `BitmapTables.ReadAt` (data-address-driven).

The `ter` (TerrainType) tag is still undecoded, but it is no longer required: each `ter` entry's `texture_ref` points to a `tex` entry with the same name (`Terrain_00`..`Terrain_25`, `Cliff_00`..`Cliff_05`, `TerrainCliff0`..`TerrainCliff5`), and the `tex` entries now decode cleanly via the loader walk.

## What's left

Three small changes, no decoder work needed:

### 1. `OpenRCT3/Simulation/Terrain.cs` — populate `GrassTexture` from the decoded collection

Replace the stub `Terrain.Load()` ([OpenRCT3/Simulation/Terrain.cs:81](OpenRCT3/Simulation/Terrain.cs)) with a real implementation:

```csharp
public static Terrain Load() {
  var config = AppConfig.Instance;
  Debug.Assert(config.InstallPath != null);
  var terrain = new Terrain();
  var terrainOvl = Path.Combine(config.InstallPath, "terrain", "RCT3", "Terrain_RCT3.common.ovl");
  using var ovl = Ovl.Load(terrainOvl);
  var textures = Textures.Extract(ovl);
  if (textures["Terrain_00"] is { MipLevels: [{ } mip0] } tex) {
    terrain.GrassTexture = new GDK.Materials.Texture(
      tex.Name, tex.Width, tex.Height, mip0, tex.Recolorable);
  }
  return terrain;
}
```

Choice of `Terrain_00` as the grass candidate: it's the first `tex` entry in the OVL, its name has no `Cliff` prefix, and its BTBL index in the loader walk is `0` — the surface a freshly-built park drops you onto. Confirmed structurally grass-shaped; visual confirmation requires the renderer change below.

`using` matters: `Ovl` and the loaded `TextureCollection` should be released after extraction. Each `OVL.Files.Texture` from the collection is its own `Image<Rgba32>` mip array, independent of the Ovl — passing `mip0` into the GDK `Texture` adapter is enough.

### 2. `OpenRCT3/Simulation/TerrainMeshBuilder.cs` — add UVs

The current builder ([OpenRCT3/Simulation/TerrainMeshBuilder.cs:59](OpenRCT3/Simulation/TerrainMeshBuilder.cs)) writes only `Position`, `Normal`, `Color` — no `TexCoord`. `Material.Textured` ([OpenCobra/GDK/Materials/Material.cs:101](OpenCobra/GDK/Materials/Material.cs)) reads `a_TexCoord` and the current shader will sample `(0, 0)` for every vertex, producing a black quad.

Add a UV per top-face vertex. Each top face is one tile (4 m × 4 m, see `Park.TileSize`), and the grass texture is intended to tile across the park — the simplest correct mapping is world-space metres, scaled by an inverse like `1 / Park.TileSize` so a single grass tile maps to one world tile. Set `TexCoord = new Vector2(worldX, worldY) * (1f / Park.TileSize)` in `AddTopFace` and on cliff-face vertices. Adjust scale later if a different repeat density is preferred.

The existing `Vertex` struct ([OpenCobra/GDK/Meshes/Vertex.cs:25](OpenCobra/GDK/Meshes/Vertex.cs)) already has a `TexCoord` field, so no GDK changes are needed.

### 3. `OpenRCT3/Game.cs:131` — switch to `Textured` material

```csharp
var terrainMesh = TerrainMeshBuilder.Build(World.Terrain, grass);
var ground = new Model(terrainMesh) {
  Material = new Textured { AlbedoTexture = World.Terrain?.GrassTexture }
};
```

The vertex `Color` of `Color.FromArgb(79, 129, 14)` (BGRA 0x4F, 0x81, 0x0E) is still passed in so the rendered surface falls back to a flat grass colour if no texture is available, matching `Textured`'s `FragColor = texColor * v_Color` math. Once the texture is wired up, the shader multiplies it by white (or whatever colour the user picks) — pass `Color.White` once a texture is present and the fallback becomes a no-op.

The marker cube at [OpenRCT3/Game.cs:150](OpenRCT3/Game.cs) can stay on `Flat`; it's a proof-of-concept, not a textured object.

## Verification

1. Run `make test` — should pass with no new failures. The 33-textures-decoded number for `terrain/RCT3/Terrain_RCT3.common.ovl` in `.agents/summaries/ovl-texture-scan.csv` proves the decode path is exercised in integration tests.
2. Launch the game: the ground plane should show the RCT3 grass texture, tiled across the buildable area. A `nullbmp` or missing-install surface would render as a black quad (because `Textured` samples `u_Texture(0,0)`); a missing `Terrain_00` from the collection would render flat green via the `v_Color` fallback.
3. Add a unit test in [OpenCobra/Tests/Integration/IngestionTests.cs](../../OpenCobra/Tests/Integration/IngestionTests.cs) that loads the terrain OVL via `Ovl.Load` and asserts `Textures.Extract(ovl)["Terrain_00"]` has a non-empty `MipLevels[0]`.

## Files to edit

- [OpenRCT3/Simulation/Terrain.cs:81](OpenRCT3/Simulation/Terrain.cs) — implement `Load` body
- [OpenRCT3/Simulation/TerrainMeshBuilder.cs](OpenRCT3/Simulation/TerrainMeshBuilder.cs) — add `TexCoord` to top-face and cliff-face vertices
- [OpenRCT3/Game.cs:131](OpenRCT3/Game.cs) — swap `new Flat()` → `new Textured { AlbedoTexture = ... }`

## Open follow-ups (not blocking this plan)

- `BinaryReaderExtensions.Read<T>` ([OpenCobra/OVL/Files/TextureDecoding.cs:158](../../OpenCobra/OVL/Files/TextureDecoding.cs)) doesn't validate `BinaryReader.ReadBytes` returned the full size before `Marshal.PtrToStructure`. Only the `Tex` path was hardened; `FlicHeader`/`FlicMipHeader`/`BitmapTable` are still vulnerable.
- `CharacterSkins` / `ParticleEffects` premise (`mms`/`prt`/`psi` reuse tex/flic/btbl shapes) is wrong; needs investigation or removal.
- `gsi` / `shs` show the same "0 LoaderStruct entries" signature as `tex`/`fct` in `Main.common.ovl` but weren't independently root-caused.

## References

- [.agents/summaries/completed-work/ovl-texture-decoding.md](../summaries/completed-work/ovl-texture-decoding.md) — final fix summary, 4 root causes
- [.agents/summaries/ovl-texture-scan.csv](../summaries/ovl-texture-scan.csv) — full-install decode results (`terrain/RCT3/Terrain_RCT3.common.ovl,33,0,`)
- [OpenCobra/OVL/Files/Textures.cs](../../OpenCobra/OVL/Files/Textures.cs) — `Textures.Extract`
- [OpenCobra/OVL/Files/TextureDecoding.cs](../../OpenCobra/OVL/Files/TextureDecoding.cs) — `ReadTexture`, `Texture` / `TextureCollection`
- [OpenCobra/OVL/Files/Flic.cs](../../OpenCobra/OVL/Files/Flic.cs) — `Flic.Read`
- [OpenCobra/OVL/Files/BitmapTable.cs](../../OpenCobra/OVL/Files/BitmapTable.cs) — `BitmapTables.Read` / `ReadAt`
- [OpenCobra/OVL/OVL.cs](../../OpenCobra/OVL/OVL.cs) — `LoaderEntriesInOrder`, `TryGetRelocationSource`, `TryGetDataPointer`
- [OpenCobra/GDK/Materials/Material.cs:101](OpenCobra/GDK/Materials/Material.cs) — `Textured` material
- [OpenCobra/GDK/Materials/Texture.cs](OpenCobra/GDK/Materials/Texture.cs) — GDK `Texture` adapter
- [OpenRCT3/Simulation/Terrain.cs](OpenRCT3/Simulation/Terrain.cs) — load site
- [OpenRCT3/Simulation/TerrainMeshBuilder.cs](OpenRCT3/Simulation/TerrainMeshBuilder.cs) — mesh builder (needs UVs)
- [OpenRCT3/Game.cs:131](OpenRCT3/Game.cs) — ground model creation site
