# Render Flat, Empty Park — Milestone Complete

**Issue**: [#4](https://github.com/open-rct3/open-rct3/issues/4) (GitHub)

**Status**: Milestone complete. Phases 1–5 done; the goal of rendering a flat, empty park is satisfied by the
solid-colored terrain mesh described below. Phase 6 (real grass texture) was deferred at the time this
milestone was closed, blocked on the OVL texture-decoding pipeline — that bug is now fixed (see
[`completed-work/ovl-texture-decoding.md`](ovl-texture-decoding.md) and
[`completed-work/ovl-materials-integration.md`](ovl-materials-integration.md)), and Phase 6 has an active
follow-up plan: [`plans/grass-texture-from-terrain-ovl.md`](../../plans/grass-texture-from-terrain-ovl.md).
Still not required for *this* milestone, which remains closed regardless of Phase 6's outcome.

## Phases

| Phase | Status | Description |
|-------|--------|-------------|
| 1. Scaffold GDK | ✅ | `OpenCobra/GDK/` project with `Material`, `Mesh`, `ShaderProgram` primitives |
| 2. GDK Primitives | ✅ | Backend-agnostic rendering data structures with `IDisposable` |
| 3. RCT3 Install Detection | ✅ | `InstallFinder` + `AppConfig` in `OpenRCT3/Platforms/` |
| 4. Solid Color Plane | ✅ | `IRenderer` interface, `Renderer` implementation, `Transform`, `Model`, `Scene` |
| 5. Textured Plane (nullbmp) | ✅ | `TextureLoader`, palette conversion, WebGL texture binding |
| 6. Textured Plane (grass) | In progress | Texture pipeline unblocked; see [`grass-texture-from-terrain-ovl.md`](../../plans/grass-texture-from-terrain-ovl.md) |

Since Phases 4/5 landed, the flat quad they produced was superseded by a real terrain mesh: the park now
renders a height-accurate, solid-colored mesh generated from the `Terrain` corner-height grid via
[`TerrainMeshBuilder`](../../../OpenRCT3/Simulation/TerrainMeshBuilder.cs) (see
[`terrain-heightmap.md`](../../plans/features/terrain-heightmap.md)), not a static single quad. This exceeds
the original Phase 4/5 scope (a flat plane) and is the basis for calling the "flat, empty park" milestone
itself done — the park terrain renders and reflects real height data; it just isn't grass-textured yet.

## Phase 5

- Palette conversion: `OpenCobra/Tests/GDK/PaletteConverterTests.cs`, covering the empty-vs-provided
  `alphaPixels` case.
- `TextureLoader`: `LoadTextureFromOvl`/`LoadFlexiTextureFromOvl` read the 768-byte (256×RGB) palette;
  `ConvertFlexiToGdkTexture` uses the correct pixel stride.
- `Scene`/`Renderer`: texture bound to `u_Texture` (unit 0), correct UVs, `Rgba32`/`UnsignedByte` upload.
- Verified via `make test` and a live app launch showing the `nullbmp` texture on the ground plane.

## Phase 6 (Deferred): Render Grass from Terrain OVL

**Deferred out of this milestone.** Phase 6 was the stretch goal of texturing the terrain with RCT3's actual
grass texture instead of a solid color. It remains blocked on the still-open OVL texture-decoding bug
([`ovl-texture-decoding.md`](../../bugs/ovl-texture-decoding.md) — `mms`/`prt`/`psi`/`fct` symbol-resolution
issue) and is no longer required for issue [#4](https://github.com/open-rct3/open-rct3/issues/4) to be
considered resolved. Revisit once the texture pipeline is fixed; the plan below is unchanged and still
accurate for when that happens.

Phase 6 is not a simple texture swap. Loading grass requires navigating the full texture ingestion pipeline: the `tex` → `flic` → `btbl` hierarchy, each with distinct binary layouts and load-order requirements.

For detailed binary layouts and ingestion workflows, see [Parsing Texture Data](../Parsing%20Texture%20Data.md).

### Understanding the Texture Pipeline

The Cobra engine manages terrain textures via three cooperating resource types:

| Tag | Name | C# Representation | Role |
| :--- | :--- | :--- | :--- |
| `btbl` | Bitmap Table | [BitmapTable](../../../OpenCobra/OVL/Files/Textures.cs) | Aggregates shared textures into a continuous block; must be loaded first |
| `flic` | Flic | [Flic](../../../OpenCobra/OVL/Files/Textures.cs) | Contains image dimensions, format, mip headers, and raw pixel data |
| `tex` | Texture | [Tex](../../../OpenCobra/OVL/Files/Textures.cs) | Holds style mappings and pointers to flic definitions |

**Key constraint**: If a terrain OVL contains a `btbl` resource, all `flic` entries in that archive live inside it. The `flic` symbols store only a 4-byte table index, not full payloads. Therefore, `btbl` must be parsed first to build the texture pool before resolving `flic` index references.

### `OpenCobra/GDK/`

1. **Load Terrain OVL Archives**:
   - Load `terrain/RCT3/Terrain_RCT3.common.ovl`
   - Load `terrain/RCT3/Terrain_RCT3.unique.ovl` (31,037 bytes)
   - The virtual address space spans both files transparently via relocations

1. **Parse Bitmap Table (`btbl` i.e. [`BitmapTable`](../../../OpenCobra/OVL/Files/Textures.cs#L278)) First**:
   - Extract all texture entries from the `btbl` resource if present
   - Build a texture pool keyed by table index

2. **Parse Flic (`flic` i.e. [`Flic`](../../../OpenCobra/OVL/Files/Textures.cs#L278)) Resources**:
   - Resolve each `flic` against the texture pool (standalone vs. table-indexed)
   - Extract `FlicHeader` (16 bytes): Format, Width, Height, MipCount
   - Extract `FlicMipHeader` (16 bytes per mip): Width, Height, Pitch, Blocks
   - Decode raw pixel data (pitch × blocks bytes per mip)

3. **Parse Texture (`tex` i.e. [`Tex`](../../../OpenCobra/OVL/Files/Textures.cs#L301)) Resource**:
   - Locate `Terrain_xx` entries (where xx is terrain type index 0–25)
   - Map texture style reference (TXS) via `TexExtra` struct
   - Retrieve associated `FlicStruct` array via `Flic` pointer

5. **Select Default Grass Texture**:
   - Identify grass terrain type index from RCT3 data
   - Return the corresponding `Flic` pixel data and dimensions

### `OpenRCT3/OpenGL/`

6. **Convert to GDK `Material`**:
   - Upload decoded pixel data as OpenGL texture
   - Configure albedo texture sampler
   - Apply any needed format conversion (BTX → RGBA)

7. **Update `OpenGLRenderer`**:
   - Replace nullbmp with grass material

**Assets**:

- Path: `$RCT3_PATH/terrain/RCT3/Terrain_RCT3.common.ovl`
- Paired with: `Terrain_RCT3.unique.ovl` (31,037 bytes)

**Binary structures** (see [Textures.cs](../../../OpenCobra/OVL/Files/Textures.cs)):

- [`Tex`](../../../OpenCobra/OVL/Files/Textures.cs#L301): 60 bytes, `Count` at offset 32, `Unk12` at offset 44
- [`Flic`](../../../OpenCobra/OVL/Files/Textures.cs#L278): 12 bytes, `DataRelocation`, `Unk1`, `Unk2`
- [`FlicHeader`](../../../OpenCobra/OVL/Files/Textures.cs#L285): 16 bytes, Format/Width/Height/MipCount
- [`FlicMipHeader`](../../../OpenCobra/OVL/Files/Textures.cs#L293): 16 bytes per mip level
- [`BitmapTable`](../../../OpenCobra/OVL/Files/Textures.cs#L278): 8 bytes, `Unk`, `Length`

**Milestone**: Flat plane textured with RCT3's default grass

## References

- **Path lookup**: [InstallFinder.cs](../../../OpenCobra/OVL/InstallFinder.cs)
- **OVL structures**: [OVL.cs](../../../OpenCobra/OVL/OVL.cs) (`FlexiTextureData`)
- **File types**: [FileTypes.cs](../../../OpenCobra/OVL/Files/FileTypes.cs) (`FlexibleTexture`/`ftx`)
- **Texture resources**: [Textures.cs](../../../OpenCobra/OVL/Files/Textures.cs) (`Tex`, `Flic`, `BitmapTable`)
- **Existing abstractions**: [IGraphicsSurface.cs](../../../OpenRCT3/Platforms/IGraphicsSurface.cs), [IWindow.cs](../../../OpenRCT3/Platforms/IWindow.cs)
- **Texture pipeline**: [Parsing Texture Data](../Parsing%20Texture%20Data.md)
