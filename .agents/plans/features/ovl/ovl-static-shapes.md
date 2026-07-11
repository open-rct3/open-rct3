# Plan: Decode StaticShape (SHS) Entries

**See also**:
- [`ovl-scenery-item-visuals.md`](./ovl-scenery-item-visuals.md) — `SVD` references `SHS` directly
  (`SceneryItemVisualLOD.staticshape` for `meshtype == 0`); blocked on this decoder.
- [`rct3-importer` `ManagerSHS.h/cpp`](https://github.com/anomalyco/rct3-importer/tree/master/RCT3%20Importer/src/libOVLng/ManagerSHS.cpp)
  and [`staticshape.h`](https://github.com/anomalyco/rct3-importer/tree/master/RCT3%20Importer/include/staticshape.h) —
  reference C++ implementation; this plan mirrors its struct layout and common-block allocation
  order directly rather than re-deriving from raw bytes.
- [`ovl-resource-relocation.md`](../../../summaries/completed-work/ovl-resource-relocation.md) —
  the `ftx_ref`/`txs_ref` relocation-resolution helper added there is reusable here.

## Context

`shs`-tagged entries define static 3D shapes used by scenery, terrain, and many ride types. Each
shape is a level-1 `StaticShape` struct (bounding box, mesh count, pointer array to meshes, effect
list) referencing a level-2 `StaticShapeMesh` per sub-mesh (vertex/index arrays + relocated
`ftx_ref`/`txs_ref` symbol references). This is the largest reference-only OVL type we still need
to decode.

The Ovl-level API (`Ovl.TryResolveRelocation`, `Ovl.TryResolveString`, `Ovl.TryGetDataPointer`,
`Ovl.TryGetRelocationSource`) is already mature enough — see `FlexiTexture.cs` for the closest
precedent.

## Goals

- **New file: `OpenCobra/OVL/Files/StaticShapes.cs`**, public records + static
  `StaticShapes.Extract(Ovl)`, following the `ParticleEffects` / `CharacterSkins` module shape.

- **Records** (per `rct3-importer/include/staticshape.h`, layout-only):
  ```csharp
  public record ShapeVertex(Vector3 Position, Vector3 Normal, uint Color, float Tu, float Tv);

  public record ShapeTriangle(uint A, uint B, uint C);

  public record StaticShapeMesh {
    public string Name;               // synthesized "{parent}.mesh{i}" — no source name exists
    public uint SupportType;
    public string? FtxRef;
    public string? TxsRef;
    public uint Transparency;
    public uint TextureFlags;
    public uint Sides;                // 1 = double-sided, 3 = single-sided
    public IReadOnlyList<ShapeVertex> Vertices;
    public IReadOnlyList<ShapeTriangle> Triangles;
    public IReadOnlyList<ShapeTriangle> SortedByY;  // painter's-algorithm tail; empty if no sort algo
    public IReadOnlyList<ShapeTriangle> SortedByZ;  // painter's-algorithm tail; empty if no sort algo
  }

  public record ShapeEffect(string Name, Matrix4x4 Position);

  public record StaticShape {
    public string Name;
    public Vector3 BoundingBoxMin;
    public Vector3 BoundingBoxMax;
    public uint TotalVertexCount;
    public uint TotalIndexCount;
    public uint MeshCount2;           // meshes with SupportType == None (ManagerSHS.cpp:223-224)
    public IReadOnlyList<StaticShapeMesh> Meshes;
    public IReadOnlyList<ShapeEffect> Effects;
  }
  ```
  The decoder always presents triangles: `index_count` is a triangle count when a sort algorithm
  is present (`ManagerSHS.cpp:135-138`), otherwise a raw-index count divided by 3. Detected by
  checking whether `2 * index_count` extra uint32s follow (sort tail present).

- **Common-block allocation order** (per `StaticShape`):
  1. Per mesh, in order: `vertexes[]` (relocated → `VERTEX[vertex_count]`).
  2. Per mesh, in order: `indices[]` (relocated → `uint32_t[index_count]`). If
     `transparency != 0` and a sort algo is set (`ManagerSHS.cpp:142-157, 376-380`), two extra
     `index_count`-length copies (per-Y, per-Z sorts) follow — RCT3's renderer uses these to
     draw transparent geometry back-to-front. The decoder reads and re-groups both into
     `SortedByY`/`SortedByZ` (empty lists when no sort algo is present) so a future renderer
     doesn't need a decoder revisit.
  3. `effect_names[]` (relocated → `char*[effect_count]`) — only if `effect_count > 0`.
  4. `effect_positions[]` (relocated → `MATRIX[effect_count]`) — only if `effect_count > 0`.
  5. Per effect, in order: padded 4-byte-aligned null-terminated ASCII name.

- **Top-level struct layout**: `StaticShape` struct (60 bytes — bounding box 24B + 4×uint32 +
  pointer to `sh[]` + 3 more fields), resolved via `Ovl.TryGetDataPointer`. `sh[]` is a relocated
  pointer to `StaticShapeMesh*[mesh_count]` in the same block (`ManagerSHS.cpp:343-356`). Per the
  Production OVLs section below, this resolves identically whether the archive handed to
  `Ovl.Load` is the `common.ovl` or `unique.ovl` half of a pack — SHS data is duplicated across
  both, not split between them, so there is no "unique-only" block to call out here.

- **`ftx_ref`/`txs_ref` resolution**: `Ovl.TryGetRelocationSource` first, fall back to
  `Ovl.TryResolveRelocation` — same chain as `FlexiTexture.cs:49-83`.

- **No per-loader extra data** — SHS has no `HasExtraData` `LoaderStruct`; don't call
  `Ovl.TryReadExtraData`.

- **Public surface**:
  ```csharp
  public static class StaticShapes {
    public static IReadOnlyList<StaticShape> Extract(Ovl ovl);
  }
  ```
  One `StaticShape` per `shs`-tagged symbol in `ovl.Keys` order. A malformed shape yields
  `Meshes: []` / `Effects: []` rather than throwing (mirrors `ParticleEffects.Extract`).

- **No changes to `Ovl.cs`** — all resolution via existing public surface. Forward-only parsing
  per `AGENTS.md` (no `BaseStream.Position` rewinds, no `while` loops on untrusted counts).

## Gaps and Risks

1. **Sort-algo index-copy tail** is untested against real data until a `shs` with
   `SortedByY`/`SortedByZ` non-empty is decoded and diffed against a known-transparent mesh.
   Path stays unit-tested via synthetic input in the meantime.
2. **`mesh_count2`** is a derived "meshes with no support" counter (`ManagerSHS.cpp:220-225`), not
   a redundant count — the decoder computes it from decoded meshes rather than trusting the
   on-disk value.
3. **Per-mesh `Name`**: C++ has no mesh name field at all (meshes are identified only by index);
   the decoder always synthesizes `$"{parent.Name}.mesh{i}"`.
4. **`txs_ref`** is whatever Texture Style the artist assigned — can be empty/`null`. The decoder
   does not assume `BillboardStandard` (that's an SVD-side claim, not SHS).
5. **No `shs` symbols in `style.common.ovl`/`style.unique.ovl`** (the repo's existing fixture
   pair) — unit tests use synthetic `BinaryWriter`-built input with manually installed relocation
   fixups. Not a blocker for the integration test: see Production OVLs below, which now has real
   targets to decode against.

## Deferred

- **Direct rendering integration.** This plan only decodes to records; a follow-up renderer plan
  will consume `StaticShape.Meshes[i].Vertices`/`.Triangles` to build GDK meshes.

## Testing

Per `AGENTS.md` and `README.md` convention (NUnit unit tests + integration check):

### Unit tests: `OpenCobra/Tests/OVL/StaticShapesTests.cs` (synthetic, no `RCT3_PATH`)

- **`Extract_EmptyOvl_ReturnsEmpty`**
- **`Extract_SingleMeshOneTriangle_DecodesAllFields`** — one mesh, 3 vertices, 1 triangle,
  `support_type = 1`, `sides = 3`, `transparency = 0`, `ftx_ref` resolving to
  `"FooTexture:ftx"`.
- **`Extract_ResolvesFtxAndTxsSymbolRefs`**
- **`Extract_NullFtxRef_ReturnsNullFtxRef`** — zero relocation entry → `null`, not `""`.
- **`Extract_TwoMeshes_AreDecodedInOrder`** — mesh 0 vertices precede mesh 1;
  `Name == "{parent}.mesh0"`.
- **`Extract_TransparentMeshWithSortAlgo_DecodesSortedByYAndZ`** — `transparency = 1`, sort algo
  set, `2 * index_count` extra uint32s after the primary index list; assert
  `SortedByY.Count == index_count / 3`, `SortedByZ.Count == index_count / 3`, and both are
  distinct triangle orderings from `Triangles`.
- **`Extract_NoSortAlgo_SortedByYAndZAreEmpty`** — `transparency = 0` (or no sort algo); assert
  `SortedByY.Count == 0` and `SortedByZ.Count == 0`, no extra bytes read.
- **`Extract_TriangleSortTail_IsSkippedOver`** — `transparency = 1` + `2 * index_count` extra
  uint32s; next mesh's vertices still found at the right offset.
- **`Extract_TriangleCountSemantics_WithSortAlgo_ReturnsTrianglesByCount`** — sort tail present,
  `index_count = 1`, 3 raw uint32s → `Triangles.Count == 1`.
- **`Extract_EffectsWithNames_DecodesMatrixAndName`**
- **`Extract_NoEffects_EffectsIsEmpty`**
- **`Extract_MeshCount2_IsComputedNotTrusted`** — 3 meshes, `SupportType = 0, 1, 0`, on-disk
  `mesh_count2 = 99` → `MeshCount2 == 2`.

### Integration test: append to `OpenCobra/Tests/Integration/ExtractResources.cs` (gated by `RCT3_PATH`)

- **`StaticShape_DecodesEveryShsSymbolInProductionArchives`** — scan every `*.ovl` under
  `$RCT3_PATH/`, decode every `shs` symbol, assert no throw and every shape has ≥1 mesh with
  non-empty `Vertices`/`Triangles`. Real targets are cataloged below (32,612 symbols across
  11,514 archives) — no longer expected to report `Inconclusive`.

## Implementation Notes

TBD.

## Status

Not started. Blocks `ovl-scenery-item-visuals.md`'s `meshtype == 0` resolution. Production OVLs
containing `shs` entries catalogued (see below).

## Production OVLs with Entries

> **Status**: Catalogued via scratch scan tool (full `$RCT3_PATH` sweep, 14,980 `*.ovl` files,
> 0 crashes on the `ovl.Keys` scan pass).

- **32,612 total `shs` symbols** across **11,514 files** — **5,757 `common.ovl` + 5,757
  `unique.ovl`**, always paired 1:1 per content pack (same symbol names, same count, in both
  halves of a pack).
- **The `common`/`unique` split is not a data partition for SHS — each half independently
  contains a complete, resolvable copy of the mesh data.** Verified via `TryGetDataPointer` +
  `TryResolveRelocation`: for `Rhino` in `test/Shapes.{common,unique}.ovl` and for both symbols
  in `ACAM/ACAM.{common,unique}.ovl`, the resolved struct bytes at the same `dataPtr` are
  byte-for-byte identical whether loaded from the common or the unique archive alone. Given the
  CSV shows this same paired-and-equal-count pattern for every one of the 5,757 packs, the
  decoder should assume duplication (not partition) holds archive-wide rather than re-verifying
  every pack. This means `StaticShapes.Extract(ovl)` will decode identically regardless of
  whether it's handed the `common.ovl` or `unique.ovl` half of a pack — the plan's earlier
  "unique-only" assumption and the "split evenly" phrasing from an earlier revision of this
  section were both wrong; there is no split.
- Full per-file breakdown (relative path, `shs_entries` count, first 3 sample symbol names):
  [`ovl-static-shapes-scan.csv`](../../../summaries/ovl-static-shapes-scan.csv).
- Sample entries: `Main.common.ovl`/`Main.unique.ovl` (3: `FlyingCamera_Control`,
  `FlyingCamera_Camera`, `FlyingCamera_Target`), `ACAM\ACAM.common.ovl`/`.unique.ovl` (2:
  `ACAMHull`, `OldACAMWheel`).

**Known test files**: `style.common.ovl`, `style.unique.ovl` (no SHS entries present — still
useful only as a negative-case fixture).

## Post-Implementation Steps

Add a results summary under [`.agents/summaries/`](../../../summaries/) and update this plan's
status row in [`README.md`](./README.md). Then unblock
[`ovl-scenery-item-visuals.md`](./ovl-scenery-item-visuals.md)'s `meshtype == 0` path.
