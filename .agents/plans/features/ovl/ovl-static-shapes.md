# Plan: Decode StaticShape (SHS) Entries

**See also**:
- [`ovl-scenery-item-visuals.md`](./ovl-scenery-item-visuals.md) — `SVD` references `SHS` directly
  (`SceneryItemVisualLOD.staticshape` for `meshtype == 0`); blocked on this decoder.
- `rct3-importer` `ManagerSHS.h/cpp` and `staticshape.h` — reference C++ implementation; this plan
  mirrors its struct layout and common-block allocation order directly rather than re-deriving
  from raw bytes. See [README.md's "Reference Source" section](./README.md#reference-source-rct3-importer)
  for the local checkout path — cross-checked against that copy on 2026-07-11, which caught and
  fixed two inaccuracies from an earlier revision of this plan (allocation order, `index_count`
  semantics — see Goals below).
- [`ovl-resource-relocation.md`](../../../summaries/completed-work/ovl-resource-relocation.md) —
  the `ftx_ref`/`txs_ref` relocation-resolution helper added there is reusable here.

## Context

`shs`-tagged entries define static 3D shapes used by scenery, terrain, and many ride types. Each
shape is a level-1 `StaticShape` struct (bounding box, mesh count, pointer array to meshes, effect
list) referencing a level-2 `StaticMesh` per sub-mesh (vertex/index arrays + relocated
`ftx_ref`/`txs_ref` symbol references). This is the largest reference-only OVL type we still need
to decode.

**Naming note**: the decoded DTOs are `Vertex`, `Triangle`, `StaticMesh`, `EffectPoint`, and
`StaticShape` (the C++ struct names — `StaticShapeMesh`, `Triangle` (already unqualified in
`staticshape.h`), and the anonymous "effect" concept — are shortened per naming convention, see
Goals below). `Vertex`/`Triangle` live in `OpenCobra.OVL.Files`, same short name as
`OpenCobra.GDK.Meshes.Vertex` (the GPU-facing mesh vertex struct) — no compile-time collision
since they're different namespaces; a future `StaticShapeLoader` converting one into the other
just needs a `using` alias, same as elsewhere in the codebase.

The Ovl-level API (`Ovl.TryResolveRelocation`, `Ovl.TryResolveString`, `Ovl.TryGetDataPointer`,
`Ovl.TryGetRelocationSource`) is already mature enough — see `FlexiTexture.cs` for the closest
precedent.

## Goals

- **New file: `OpenCobra/OVL/Files/StaticShapes.cs`**, public `readonly record struct` DTOs (see
  below) + static `StaticShapes.Extract(Ovl)`, following the `ParticleEffects` / `CharacterSkins`
  module shape for the `Extract` method itself.

- **Records** (per `rct3-importer/include/staticshape.h`, layout-only). All declared as
  `readonly record struct` with positional properties, not `record` (reference type) — these are
  short-lived intermediary DTOs meant to be ingested by a future GDK asset importer under
  `OpenCobra/GDK/Assets/` (a `StaticShapeLoader`, mirroring `TextureLoader.cs`'s
  `Ovl`-decoder-in/GDK-class-out shape) rather than held onto or mutated, so this matches the
  existing convention for that role — see `FlexiTexture.cs`'s `internal readonly record struct
  FlexiFrameData(...)` and `TextureDecoding.cs`'s `internal readonly record struct OvlData(...)`
  for the identical pattern already used one layer up the same pipeline (`MipChain`/`Animation` in
  `GDK/Materials/Texture.cs` do the same on the GDK side once ingested). A mesh/shape with many
  vertices is still cheap to pass around this way since the bulk data lives in the
  `IReadOnlyList<T>` fields (heap-allocated regardless of struct vs class), not inline in the
  struct itself — only the struct's own scalar fields and list references get stack-copied.
  ```csharp
  public readonly record struct Vertex(Vector3 Position, Vector3 Normal, uint Color, float Tu, float Tv);

  public readonly record struct Triangle(uint A, uint B, uint C);

  public readonly record struct StaticMesh(
    string Name,                                    // synthesized "{parent}.mesh{i}" — no source name exists
    uint SupportType,
    string? FtxRef,
    string? TxsRef,
    uint Transparency,
    uint TextureFlags,
    uint Sides,                                      // 1 = double-sided, 3 = single-sided
    IReadOnlyList<Vertex> Vertices,
    IReadOnlyList<Triangle> Triangles,
    IReadOnlyList<Triangle> SortedByY,               // painter's-algorithm tail; empty if no sort algo
    IReadOnlyList<Triangle> SortedByZ                // painter's-algorithm tail; empty if no sort algo
  );

  public readonly record struct EffectPoint(string Name, Matrix4x4 Position);

  public readonly record struct StaticShape(
    string Name,
    Vector3 BoundingBoxMin,
    Vector3 BoundingBoxMax,
    uint TotalVertexCount,
    uint TotalIndexCount,
    uint MeshCount2,                                 // meshes with SupportType == None (ManagerSHS.cpp:223-224)
    IReadOnlyList<StaticMesh> Meshes,
    IReadOnlyList<EffectPoint> Effects
  );
  ```
  The decoder always presents triangles. Per `ManagerSHS.cpp:129-141` (`cStaticShape2::Fill`),
  `index_count`'s meaning is the opposite of what earlier revisions of this plan assumed:
  ```
  if (algo_x == NONE && placetexturing != 0)
      index_count = raw_index_count / 3;   // triangle count
  else
      index_count = raw_index_count;       // raw count (incl. when a sort algo IS present)
  ```
  So `index_count` is a **triangle count only when there is no sort algorithm** (and
  `placetexturing`/`transparency != 0`); when a sort algorithm **is** present, `index_count` is
  the **raw** index count, and that's exactly the case where the sort tail (below) is also
  present. The decoder detects which case it's in the same way the reference implementation
  does: not from `index_count` alone, but from whether a sort tail is present (see the
  allocation-order note below) — if it is, `index_count` is raw and gets divided by 3 for
  presentation; if it isn't, `index_count` is already a triangle count.

- **Common-block allocation order** (per `StaticShape`) — corrected against
  `ManagerSHS.cpp:369-382` (`Make()`); an earlier revision of this plan wrongly described this as
  two batched passes (all vertex arrays, then all index arrays). The real layout **interleaves
  per mesh**:
  1. For each mesh, in order:
     a. `vertexes[]` (relocated → `VERTEX[vertex_count]`).
     b. `indices[]` (relocated → `uint32_t[raw_index_count]`), immediately following that same
        mesh's `vertexes[]`. If `transparency != 0` and a sort algo is set
        (`ManagerSHS.cpp:142-157, 376-380`), two extra `raw_index_count`-length copies (per-Y,
        per-Z sorts) immediately follow this mesh's `indices[]`, *before* the next mesh's
        `vertexes[]` — RCT3's renderer uses these to draw transparent geometry back-to-front. The
        decoder reads and re-groups both into `SortedByY`/`SortedByZ` (empty lists when no sort
        algo is present) so a future renderer doesn't need a decoder revisit.
  2. `effect_names[]` (relocated → `char*[effect_count]`) — only if `effect_count > 0`.
  3. `effect_positions[]` (relocated → `MATRIX[effect_count]`) — only if `effect_count > 0`.
  4. Per effect, in order: padded 4-byte-aligned null-terminated ASCII name.

- **Top-level struct layout**: `StaticShape` struct (60 bytes — bounding box 24B + 4×uint32 +
  pointer to `sh[]` + 3 more fields), resolved via `Ovl.TryGetDataPointer`. `sh[]` is a relocated
  pointer to `StaticShapeMesh*[mesh_count]` in the same block (`ManagerSHS.cpp:343-356`). Per the
  Production OVLs section below, this resolves identically whether the archive handed to
  `Ovl.Load` is the `common.ovl` or `unique.ovl` half of a pack — SHS data is duplicated across
  both, not split between them, so there is no "unique-only" block to call out here.

- **`VECTOR`/`VERTEX`/`MATRIX` byte layout** — confirmed directly against `vertex.h`, not assumed:
  `VECTOR` is 3×`float` (12 bytes, no padding — matches the 24-byte bounding box above being two
  `VECTOR`s). `VERTEX` is `VECTOR position` + `VECTOR normal` + `uint32 color` + `float tu` +
  `float tv` = 36 bytes, all 4-byte-aligned fields, no padding — this is `Vertex`'s exact
  field order above. `MATRIX` is a row-major `float[4][4]` (`_11.._44` naming) = 64 bytes; .NET's
  `System.Numerics.Matrix4x4` uses the same row-major `M11..M44` layout, so `effect_positions[i]`
  reads straight into a `Matrix4x4` with no transpose or field reordering needed.

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
  One `StaticShape` per `shs`-tagged symbol in `ovl.Keys`. Decoded via
  `Parallel.ForEach`/`ConcurrentBag`, matching the pattern both `Textures.Extract`
  (`Textures.cs:66-86`, its single-phase `otherTextureData` loop — the closest match, since `shs`
  has no bitmap-table-style cross-symbol dependency requiring a separate first phase) and
  `ParticleEffects.Extract` already use for `ovl.Keys`-sized workloads: a `ConcurrentBag<StaticShape>`
  for successes, a `ConcurrentBag<OvlFile>` for `failures`, a per-symbol `try/catch` that
  `logger.Error`s and adds to `failures` instead of throwing, and one summary `logger.Error` after
  the loop reporting `failures.Count` against the total. Real archives carry thousands of `shs`
  symbols (see Production OVLs below), the same scale this pattern already targets. As with both
  precedents, output order is therefore not guaranteed to match `ovl.Keys` order — callers needing
  a specific shape look it up by `Name`, not by index. A malformed shape goes into `failures`
  rather than throwing and is excluded from the returned list (this plan's earlier "yields
  `Meshes: []`/`Effects: []`" description undersold it — per the precedents, a symbol that fails
  to decode is dropped from the result entirely, not returned as an empty-shell `StaticShape`).

- **No changes to `Ovl.cs`** — all resolution via existing public surface. Forward-only parsing
  per `AGENTS.md` (no `BaseStream.Position` rewinds, no `while` loops on untrusted counts).

- **New Dumper plugin: `plugins/shs-viewer/`** (per the parent
  [`README.md`](./README.md#dumper-plugin-requirement) requirement — every decoder ships a
  matching viewer). `shs-viewer` is already listed as priority 4 in
  [`plugins/README.md`](../../../../plugins/README.md), described there as "mesh metadata with
  vertex/index arrays" — moderate complexity. Renders: shape name, bounding box, mesh count,
  effect count, and per-mesh vertex/triangle counts + `FtxRef`/`TxsRef`/`Sides` (a table, not a
  3D preview — matches `mam-viewer`'s "vertex/face counts and bounding box" precedent, the
  closest existing plugin in shape). Reference `mam-viewer`'s `index.ts`/`index.test.ts`
  structure directly rather than the bare template.

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
- **`Extract_TwoMeshes_AreDecodedInOrder`** — two meshes, each with its own vertices+indices
  written back-to-back (mesh 0 `vertexes`, mesh 0 `indices`, mesh 1 `vertexes`, mesh 1
  `indices` — the real interleaved layout, not a batched one); assert mesh 0's vertices/triangles
  decode independently of mesh 1's, and `Name == "{parent}.mesh0"`.
- **`Extract_TransparentMeshWithSortAlgo_DecodesSortedByYAndZ`** — `transparency = 1`, sort algo
  set, `index_count` written as the **raw** index count (not divided by 3, per
  `ManagerSHS.cpp:129-141`), followed by `2 * index_count` extra uint32s; assert
  `Triangles.Count == index_count / 3`, `SortedByY.Count == index_count / 3`,
  `SortedByZ.Count == index_count / 3`, and all three are distinct triangle orderings.
- **`Extract_NoSortAlgo_SortedByYAndZAreEmpty`** — `transparency = 0` (or no sort algo);
  `index_count` written as a **triangle** count directly (per the no-sort-algo branch); assert
  `Triangles.Count == index_count`, `SortedByY.Count == 0` and `SortedByZ.Count == 0`, no extra
  bytes read.
- **`Extract_TriangleSortTail_IsSkippedOver`** — same synthetic layout as
  `Extract_TransparentMeshWithSortAlgo_DecodesSortedByYAndZ` (raw `index_count` + `2 *
  index_count` extra uint32s); assert the next mesh's `vertexes[]` is still found at the right
  offset, i.e. immediately after this mesh's sort tail, not immediately after its `indices[]`.
- **`Extract_TriangleCountSemantics_NoSortAlgo_IndexCountIsAlreadyTriangleCount`** — no sort tail
  present, `index_count = 1`, 3 raw uint32s → `Triangles.Count == 1` (the decoder must NOT divide
  by 3 again in this branch).
- **`Extract_EffectsWithNames_DecodesMatrixAndName`**
- **`Extract_NoEffects_EffectsIsEmpty`**
- **`Extract_MeshCount2_IsComputedNotTrusted`** — 3 meshes, `SupportType = 0, 1, 0`, on-disk
  `mesh_count2 = 99` → `MeshCount2 == 2`.

### Integration test: append to `OpenCobra/Tests/Integration/ExtractResources.cs` (gated by `RCT3_PATH`)

- **`StaticShape_DecodesEveryShsSymbolInProductionArchives`** — scan every `*.unique.ovl` under
  `$RCT3_PATH/` (not `*.common.ovl` — confirmed duplicate data per the Production OVLs section
  below, so scanning both halves would just double the runtime for zero extra coverage), decode
  every `shs` symbol, assert no throw and every shape has ≥1 mesh with non-empty
  `Vertices`/`Triangles`. Real targets are cataloged below (~16,306 unique shapes across 5,757
  archives) — no longer expected to report `Inconclusive`.
- **`StaticShape_KnownSymbol_ResolvesFtxRef`** — named spot checks beyond the blanket scan, since
  synthetic unit tests can't catch real-world relocation-resolution regressions: decode
  `ACAMHull` from `ACAM/ACAM.unique.ovl` and `Rhino` from `test/Shapes.unique.ovl`, assert at
  least one mesh on each has a non-null `FtxRef`. (Confirm the exact expected symbol values by
  running the decoder once real data is available — this test's assertions may need the actual
  `FtxRef` string filled in rather than just "non-null" once observed.)

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
status row in [`README.md`](./README.md). Move `shs-viewer` from `📋 Planned` to `✅ Completed` in
[`plugins/README.md`](../../../../plugins/README.md)'s status table once the Dumper plugin ships.
Then unblock [`ovl-scenery-item-visuals.md`](./ovl-scenery-item-visuals.md)'s `meshtype == 0` path.
