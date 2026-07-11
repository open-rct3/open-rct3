# Render Grass Texture from Terrain OVL

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

## Gaps and risks (found during review, before execution)

1. ~~No renderer texture-binding path~~ — **verified already working, no action needed.** Checked [OpenRCT3/OpenGL/Renderer.cs](../../OpenRCT3/OpenGL/Renderer.cs): `UploadMaterial` calls `Texture.Upload()` for any material texture still `Uninitialized`, and the draw loop (`Render`/`BuildDisplayList`) binds `material.AlbedoTexture.Handle` to `TEXTURE0` and sets `u_Texture` before `DrawElements`. This landed after the "What's already done" snapshot at the top of this plan (commit `b22f4da` "Wire OVL Textures into GDK Materials" and later). `Textured`'s shader (`Material.cs:101`) and `Mesh.Upload`'s conditional `a_TexCoord` binding are also already correct. Steps 1–3 below are the complete remaining work.
2. **`worldX`/`worldY` don't exist in `AddTopFace`'s scope.** They're locals inside `CornerPosition` ([OpenRCT3/Simulation/TerrainMeshBuilder.cs:44-57](OpenRCT3/Simulation/TerrainMeshBuilder.cs)), which returns only `Vector3 Position` (already centered by `-terrain.Width/2f * TileSize`). **Resolved**: derive `TexCoord` from `Position.X`/`Position.Z` directly — no signature change to `CornerPosition`. Texture origin will shift slightly if `terrain.Width` changes, but tiling density and correctness are unaffected. Step 2's snippet below reflects this.
3. **Green tint never gets removed, and vertex-color's role needs documenting.** The step-3 code sample keeps passing the green `grass` vertex color even after the texture is wired up, so `Textured`'s `FragColor = texColor * v_Color` would tint the real grass texture dark green. **Resolved**: `Game.cs` picks `Color.White` when `GrassTexture` is set, green flat-color otherwise. Also confirmed by grep: `v_Color` currently has exactly one job — flat-shading fallback / tint multiplier for `Textured` — it's not used for baked lighting, AO, or anything else today, and the separate `Recolorable` flag threaded through `OVL.Files.Texture` → GDK `Texture` (`Texture.cs:32,43,71`, `IsRecolorable`) is fully unused dead code with zero call sites, so there's no existing recolor-via-vertex-color mechanism to conflict with. Renamed `v_Color`'s doc comment in the shader source to spell out "tint multiplier" so a future recolor or baked-lighting feature doesn't silently overload it without noticing the collision — see step 3 below.
4. **Shared-`Image<Rgba32>` double-dispose landmine — partially mitigated, not closed.** `OVL.Files.Texture.MipLevels` are independent copies safe to outlive `using var ovl` — confirmed the DXT/A8R8G8B8 decoders build fresh pixel arrays, not mmap-backed views. But GDK `Texture` (`OpenCobra/GDK/Materials/Texture.cs:32,52`) stores the passed `Image<Rgba32>` **by reference**, not by copy. The plan's snippet never disposes the `TextureCollection` returned by `Textures.Extract` (leaving ~32 unused decoded textures for the GC), which is fine *as written* — but if anyone later "cleans up" that leak by adding `using var textures = Textures.Extract(ovl)`, it will double-dispose the same `Image<Rgba32>` that `GDK.Texture.Dispose()` also disposes via `World.Dispose`. Add an explicit code comment at the call site: `// Do not dispose `textures` — GrassTexture holds a live reference into MipLevels[0].`

   Since this risk was written, a new Roslyn analyzer (`OpenCobra/Analyzers/DisposableOwnershipAnalyzer.cs`, rules `GDK002`/`GDK003`) was built specifically for this bug shape and confirmed to fire on `Texture.cs:52` — but it does **not** close this specific hazard:
   - `Texture.cs`'s `texture` ctor param is deliberately left un-annotated with `[TakesOwnership]` (so `GDK002` keeps nagging until a real fix lands), which means `GDK003` — the rule that would catch a double-dispose at a call site — is inert everywhere right now, including here.
   - Even once annotated, `GDK003` only does identifier-level matching (same variable name passed as the argument *and* separately disposed/`using`'d in the same block). In this plan's exact hazard scenario the disposed identifier is `textures`, but the constructor argument is `mip0`, extracted via a property pattern (`textures["Terrain_00"] is { MipLevels: [{ } mip0] } tex`) — different identifier text, no dataflow tracing through the indexer/pattern-match, so `GDK003` would silently miss it even if the annotation were added.
   - **Scheduled, not deferred**: the structural fix — give `OVL.Files.Texture` a `TakeMip(int level)` that removes-and-returns rather than exposing `MipLevels` for read+alias, then mark the GDK `Texture` ctor param `[TakesOwnership]` for real — is step 0 below, done as part of this plan rather than left as a follow-up. Once it lands, `GDK002` goes quiet on `Texture.cs:52` (the annotation is now honest — the source object can no longer double-dispose), and the `// Do not dispose` comment in step 1 becomes redundant (`textures` disposal becomes safe by construction, though still unnecessary since nothing else in `TextureCollection` needs releasing early).
5. **"Terrain_00 is grass" is an unverified guess.** No per-texture-name data exists in `.agents/summaries/ovl-texture-scan.csv` (only aggregate counts), and the `ter`/`TerrainType` tag — the only structure that would authoritatively map a `TerrainType` enum value to a texture name — is still undecoded per this plan's own text. The selection rationale (first `tex` entry, no `Cliff` prefix, BTBL index 0) is circumstantial. Treat step 1's texture choice as provisional until the visual-confirmation step in Verification actually runs; if it's wrong, swap the string constant only — no structural rework needed.

   **Decided**: proceed with the guess and verify visually rather than block on decoding `ter` first — this plan is small enough that a wrong guess costs a one-line fix. Decoding `ter` for real (giving an authoritative `TerrainType.texture` reference instead of a guess) is tracked separately as [`.agents/plans/features/ovl/ovl-terrain-types.md`](features/ovl/ovl-terrain-types.md) — parallel verification work, not a dependency of this plan.

## What's left

Four changes, no decoder work needed, no renderer changes needed (risk #1 above confirmed that path already works):

### 0. `OpenCobra/OVL/Files/TextureDecoding.cs` — `TakeMip`, and annotate the GDK `Texture` ctor

Add a removes-and-returns accessor to `OVL.Files.Texture` alongside the existing `MipLevels` property:

```csharp
public Image<Rgba32> TakeMip(int level) {
  var mip = MipLevels[level];
  MipLevels[level] = null!; // Dispose() below skips nulled entries
  return mip;
}
```

(`Dispose()` — TextureDecoding.cs:41-45 — must skip `null` entries in the loop.) Then mark GDK `Texture.cs:32`'s `texture` parameter `[TakesOwnership]` ([OpenCobra/GDK/TakesOwnershipAttribute.cs](OpenCobra/GDK/TakesOwnershipAttribute.cs)) — it's now an honest annotation, since the source can no longer alias-and-dispose the same image. This closes the `GDK002` warning `DisposableOwnershipAnalyzer` already raises on `Texture.cs:52`, and it's the only thing that makes step 1's `mip0` extraction actually safe rather than "safe because nothing disposes `textures` yet."

### 1. `OpenRCT3/Simulation/Terrain.cs` — populate `GrassTexture` from the decoded collection

Replace the stub `Terrain.Load()` ([OpenRCT3/Simulation/Terrain.cs:81](OpenRCT3/Simulation/Terrain.cs)) with a real implementation:

```csharp
public static Terrain Load() {
  var config = AppConfig.Instance;
  Debug.Assert(config.InstallPath != null);
  var terrain = new Terrain();
  var terrainOvl = Path.Combine(config.InstallPath, "terrain", "RCT3", "Terrain_RCT3.common.ovl");
  using var ovl = Ovl.Load(terrainOvl);
  using var textures = Textures.Extract(ovl); // safe: TakeMip (step 0) detaches the mip before this disposes
  if (textures["Terrain_00"] is { } tex) {
    var mip0 = tex.TakeMip(0);
    terrain.GrassTexture = new GDK.Materials.Texture(
      tex.Name, tex.Width, tex.Height, mip0, tex.Recolorable);
  }
  return terrain;
}
```

Choice of `Terrain_00` as the grass candidate: it's the first `tex` entry in the OVL, its name has no `Cliff` prefix, and its BTBL index in the loader walk is `0` — the surface a freshly-built park drops you onto. Confirmed structurally grass-shaped; visual confirmation requires the renderer change below.

Step 0's `TakeMip` is what makes `using var textures = ...` here actually safe: the mip handed to `GDK.Materials.Texture` is removed from `tex.MipLevels` before `textures`' disposal walks it, so there's no double-dispose regardless of what a future reader does with this method. `[TakesOwnership]` on the GDK `Texture` ctor (also step 0) documents the same guarantee at the receiving end.

### 2. `OpenRCT3/Simulation/TerrainMeshBuilder.cs` — add UVs

The current builder ([OpenRCT3/Simulation/TerrainMeshBuilder.cs:59](OpenRCT3/Simulation/TerrainMeshBuilder.cs)) writes only `Position`, `Normal`, `Color` — no `TexCoord`. `Material.Textured` ([OpenCobra/GDK/Materials/Material.cs:101](OpenCobra/GDK/Materials/Material.cs)) reads `a_TexCoord` and the current shader will sample `(0, 0)` for every vertex, producing a black quad.

Add a UV per top-face vertex. Each top face is one tile (4 m × 4 m, see `Park.TileSize`), and the grass texture is intended to tile across the park — the simplest correct mapping is world-space metres, scaled by an inverse like `1 / Park.TileSize` so a single grass tile maps to one world tile. `CornerPosition` only returns a `Vector3 Position` (already centered by `-terrain.Width/2f * TileSize`), so derive the UV from that rather than plumbing through separate raw grid coordinates: set `TexCoord = new Vector2(Position.X, Position.Z) * (1f / Park.TileSize)` in `AddTopFace` and on cliff-face vertices. The texture origin will shift slightly if `terrain.Width` changes, which is fine for tiling. Adjust scale later if a different repeat density is preferred.

The existing `Vertex` struct ([OpenCobra/GDK/Meshes/Vertex.cs:25](OpenCobra/GDK/Meshes/Vertex.cs)) already has a `TexCoord` field, so no GDK changes are needed.

### 3. `OpenRCT3/Game.cs:131` — switch to `Textured` material

```csharp
var hasGrassTexture = World.Terrain?.GrassTexture != null;
var terrainMesh = TerrainMeshBuilder.Build(World.Terrain, hasGrassTexture ? Color.White : grass);
var ground = new Model(terrainMesh) {
  Material = new Textured { AlbedoTexture = World.Terrain?.GrassTexture }
};
```

`Textured`'s `FragColor = texColor * v_Color` math means the vertex `Color` is a tint multiplier, not a lighting term — pass `Color.White` when `GrassTexture` is present so the texture renders unmodified, and fall back to the flat `Color.FromArgb(79, 129, 14)` grass colour (BGRA 0x4F, 0x81, 0x0E) only when there's no texture to show. Add a one-line comment on `v_Color` in `Textured`'s fragment shader source ([OpenCobra/GDK/Materials/Material.cs:101](OpenCobra/GDK/Materials/Material.cs)) — `// vertex color is a tint multiplier over the sampled texture, not a lighting term` — so a future recolor feature or baked-lighting pass doesn't silently repurpose it without noticing the existing usage. (The `Recolorable` flag already threaded through `OVL.Files.Texture` → GDK `Texture.IsRecolorable` is currently dead code with no render-time behavior, so there's nothing to reconcile with yet — just the comment to prevent a future collision.)

The marker cube at [OpenRCT3/Game.cs:150](OpenRCT3/Game.cs) can stay on `Flat`; it's a proof-of-concept, not a textured object.

## Verification

1. Run `make test` — should pass with no new failures. The 33-textures-decoded number for `terrain/RCT3/Terrain_RCT3.common.ovl` in `.agents/summaries/ovl-texture-scan.csv` proves the decode path is exercised in integration tests.
2. Launch the game: the ground plane should show the RCT3 grass texture, tiled across the buildable area. A `nullbmp` or missing-install surface would render as a black quad (because `Textured` samples `u_Texture(0,0)`); a missing `Terrain_00` from the collection would render flat green via the `v_Color` fallback.
3. Add a unit test in [OpenCobra/Tests/Integration/IngestionTests.cs](../../OpenCobra/Tests/Integration/IngestionTests.cs) that loads the terrain OVL via `Ovl.Load` and asserts `Textures.Extract(ovl)["Terrain_00"]` has a non-empty `MipLevels[0]`.

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
