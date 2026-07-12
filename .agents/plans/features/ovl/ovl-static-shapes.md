# Decode StaticShape (SHS) Entries

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

- **Top-level struct layout**: `StaticShape` struct is **56 bytes**, not 60 (corrected — an
  earlier revision miscounted). Confirmed by summing the 10 fields in `staticshape.h`, all 4-byte
  aligned with no padding:
  | Offset | Field | Size |
  |---|---|---|
  | 0 | `bounding_box_min` | 12 |
  | 12 | `bounding_box_max` | 12 |
  | 24 | `total_vertex_count` | 4 |
  | 28 | `total_index_count` | 4 |
  | 32 | `mesh_count2` | 4 |
  | 36 | `mesh_count` | 4 |
  | 40 | `sh` (relocated ptr) | 4 |
  | 44 | `effect_count` | 4 |
  | 48 | `effect_positions` (relocated ptr) | 4 |
  | 52 | `effect_names` (relocated ptr) | 4 |

  Resolved via `Ovl.TryGetDataPointer`. `sh` is a relocated pointer to `StaticShapeMesh*[mesh_count]`
  (an array of pointers, each individually relocated to a 40-byte `StaticShapeMesh` — see below) in
  the same block (`ManagerSHS.cpp:343-356`). Per the Production OVLs section below, this resolves
  identically whether the archive handed to `Ovl.Load` is the `common.ovl` or `unique.ovl` half of
  a pack — SHS data is duplicated across both, not split between them, so there is no
  "unique-only" block to call out here.

  `StaticShapeMesh` is **40 bytes** (10×4-byte fields, same no-padding reasoning): `support_type`
  (0), `ftx_ref` ptr (4), `txs_ref` ptr (8), `transparency` (12), `texture_flags` (16), `sides`
  (20), `vertex_count` (24), `index_count` (28), `vertexes` ptr (32), `indices` ptr (36).

- **`VECTOR`/`VERTEX`/`MATRIX` byte layout** — confirmed directly against `vertex.h`, not assumed:
  `VECTOR` is 3×`float` (12 bytes, no padding — matches the 24-byte bounding box above being two
  `VECTOR`s). `VERTEX` is `VECTOR position` + `VECTOR normal` + `uint32 color` + `float tu` +
  `float tv` = 36 bytes, all 4-byte-aligned fields, no padding — this is `Vertex`'s exact
  field order above. `MATRIX` is a row-major `float[4][4]` (`_11.._44` naming) = 64 bytes; .NET's
  `System.Numerics.Matrix4x4` uses the same row-major `M11..M44` layout, so `effect_positions[i]`
  reads straight into a `Matrix4x4` with no transpose or field reordering needed.

- **`ftx_ref`/`txs_ref` resolution — corrected**: an earlier revision cited `FlexiTexture.cs:49-83`
  as "the same chain," but that code only resolves a pointer to raw bytes (`TryResolveRelocation`)
  — it never turns a pointer into a *symbol name*. `ftx_ref`/`txs_ref` are relocated pointers
  directly to the referenced symbol's *data* address (`loader.assignSymbolReference(...)` in
  `ManagerSHS.cpp:387-388` writes exactly that), so recovering the *name* needs a pointer → symbol
  reverse lookup. Rather than have this decoder build one locally, `Ovl` itself now exposes
  `public bool TryFindSymbol(uint dataPtr, out OvlFile file)` (`OpenCobra/OVL/OVL.cs`) — the
  reverse of the existing `TryGetDataPointer`, backed by a `Dictionary<uint, OvlFile>` populated
  alongside `entryDataPtrs` in `IngestArchive`/`ExtractResources` (first symbol registered for a
  given address wins; harmless for SHS's confirmed common/unique duplication, see Production OVLs
  below). This *is* a change to `Ovl.cs` — supersedes this plan's earlier "No changes to `Ovl.cs`"
  goal, since a reverse lookup is generally useful to any future decoder resolving a
  relocated-pointer-to-symbol reference, not SHS-specific. `Ovl.TryGetRelocationSource` still
  gates the field's raw pointer value first (same double-hop pattern as
  `TextureDecoding.cs:399-406`); a pointer that doesn't resolve to any known symbol yields `null`.

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

- **One addition to `Ovl.cs`: `TryFindSymbol`** (see `ftx_ref`/`txs_ref` resolution above) —
  otherwise all resolution is via existing public surface. Forward-only parsing per `AGENTS.md`
  (no `BaseStream.Position` rewinds, no `while` loops on untrusted counts).

- **New Dumper plugin: `plugins/shs-viewer/`** (per the parent
  [`README.md`](./README.md#dumper-plugin-requirement) requirement — every decoder ships a
  matching viewer). **Revised twice**: first assumed per-mesh data was directly renderable from
  raw resource bytes (wrong — `render(bytes)` only ever gets a resource's own unresolved bytes,
  confirmed via `Dumper/MainForm.cs`), then scoped down to a header-only view as a result. Neither
  is where this landed — instead, `Dumper/Plugins/ViewerPlugin.cs` gained a small set of **"ovl"
  host functions** (`resolve_pointer`/`get_relocation_source`/`find_symbol`/`read_resource`/
  `current_resource_address`), wrapped for AssemblyScript callers by `plugins/lib/ovl.ts`'s `Ovl`
  class, so any plugin can request further archive data on demand against whichever archive is
  currently open (`PluginManager.CurrentOvl`/`CurrentFile`, updated by `MainForm.LoadOvl`/
  `OvlTree_AfterSelect`). `shs-viewer` uses these to walk `sh[]` live and render a real per-mesh
  table (vertex/index counts, support type, sides, resolved `FtxRef`/`TxsRef` symbol names) on top
  of the inline 56-byte header (bounding box, counts). Struct-layout/decode-quirk knowledge stays
  centralized in .NET (`StaticShapes.cs` remains the source of truth for e.g. the sort-tail
  ambiguity) — plugins only walk pointers via these host functions, they don't reinterpret struct
  layouts themselves. Full byte-level decoding (vertices, triangles, sort-tail handling) is still
  `StaticShapes.Extract`'s job, not this plugin's — `shs-viewer` is a summary/inspector view.
  See [`plugins/README.md`](../../../../plugins/README.md)'s `shs-viewer` note for the host
  function list; future pointer-heavy decoders (`svd-viewer`, `ftx-viewer`, `sid-viewer`) should
  reuse the same "ovl" host functions rather than growing per-type host code.

## Gaps and Risks

1. **Sort-algo index-copy tail presence cannot be determined from the archive alone — confirmed
   during implementation, not just theorized.** `algo_x`/`algo_y`/`algo_z` (`ManagerSHS.h:64-66`)
   live only on the writer's in-memory `cStaticShape2`, never serialized to the on-disk
   `StaticShapeMesh`. `transparency != 0` is necessary but **not sufficient** for a sort tail to
   exist: tested against real data (`test/Shapes.unique.ovl`, `ACAM/ACAM.unique.ovl`),
   `SwingShipHLod`, `SwingShipMLod`, `Straight4mTP01`, and `OldACAMWheel` all have
   `transparency != 0` with **no sort tail actually present** — the decoder's first version
   assumed `transparency != 0` ⇒ sort tail and crashed (`ArgumentOutOfRangeException` in
   `ReadTriangles`) reading past the resolved block on all four. **Current mitigation, not a real
   fix**: `StaticShapes.cs` only commits to the "raw index count + sort tail" interpretation when
   `transparency != 0` AND the 3× tail actually fits within the resolved block's remaining bytes;
   otherwise it falls back to "index_count is already a triangle count." This never crashes and
   passes on all 13 known real symbols, but it's a bounds-check heuristic, not a verified decode
   rule — a mesh where the wrong interpretation *also* happens to fit in bounds would silently
   decode wrong data. **Still open**: find an authoritative signal (if one exists) or accept this
   heuristic permanently; not blocking, since it degrades to a plausible-if-unverified read rather
   than a crash or silent corruption in all cases tested so far.
2. **`mesh_count2`** is a derived "meshes with no support" counter (`ManagerSHS.cpp:220-225`), not
   a redundant count — the decoder computes it from decoded meshes rather than trusting the
   on-disk value. **Corrected**: `SupportType::None` is `0xFFFFFFFF` (`rct3constants.h:233`), not
   `0` — an earlier revision of the unit test below assumed `0`, which would have asserted the
   wrong `MeshCount2`.
3. **Per-mesh `Name`**: C++ has no mesh name field at all (meshes are identified only by index);
   the decoder always synthesizes `$"{parent.Name}.mesh{i}"`.
4. **`txs_ref`** is whatever Texture Style the artist assigned — can be empty/`null`. The decoder
   does not assume `BillboardStandard` (that's an SVD-side claim, not SHS).
5. **No `shs` symbols in `style.common.ovl`/`style.unique.ovl`** (the repo's existing fixture
   pair) — not a blocker: see Production OVLs below, which has real targets to decode against, and
   Testing below for why this plan ended up integration-test-only (no synthetic unit tests).
6. **Some shapes legitimately have zero meshes** — confirmed against real data, not a decode bug:
   `invisibleproxy` (`Enclosures/Shelters`), vehicle "Bogey" pieces (`Cars/CoasterCars`,
   `Cars/TrackedRideCars`), and track-joint `_ME` pieces all decode successfully with
   `Meshes.Count == 0`. An early version of the integration test below wrongly asserted every
   shape has ≥1 mesh and failed on 106 real symbols as a result — fixed by only asserting
   non-empty `Vertices`/`Triangles` for shapes that do have meshes.

## Deferred

- **Direct rendering integration.** This plan only decodes to records; a follow-up renderer plan
  will consume `StaticShape.Meshes[i].Vertices`/`.Triangles` to build GDK meshes.

## Testing

**Deviated from `AGENTS.md`/`README.md`'s usual "unit tests + integration check" convention —
integration-only, deliberately.** No synthetic-OVL-with-relocations unit-test harness exists
anywhere in this codebase (checked `FlexiTexture`/`ParticleEffects`/`CharacterSkins`: none have
unit tests either), and real fixture archives already cover the decoder thoroughly — building a
byte-level synthetic-archive harness from scratch was explicitly decided against as unnecessary
effort given that. All testing landed in `OpenCobra/Tests/Integration/ExtractResources.cs` (gated
by `RCT3_PATH`), as implemented:

- **`StaticShapes_AreDecodable(string ovlPath)`** — `[TestCaseSource]`-driven over every
  `*.unique.ovl` under `$RCT3_PATH/` (not `*.common.ovl` — confirmed duplicate data per the
  Production OVLs section below, so scanning both halves would just double the runtime for zero
  extra coverage), mirroring the existing `FtxResources_AreDecodable`/`GetOvlFixtures` pattern.
  Per archive: `Ignore`s if no `shs` symbols present; otherwise decodes via `StaticShapes.Extract`
  and asserts (a) every symbol decoded (none silently dropped into `Extract`'s `failures` bag) and
  (b) for shapes that do have meshes, at least one mesh has non-empty `Vertices`/`Triangles`. Does
  **not** assert every shape has ≥1 mesh — an early version did and failed on 106 real symbols
  that legitimately have zero (see Gaps #6). Run against the full install: **passes on every
  `*.unique.ovl` fixture**, including `test/Shapes.unique.ovl` and `ACAM/ACAM.unique.ovl`.
- **`Load_ACAMHull_DecodesRealShapeWithGenuinelyNullSymbolRefs`** — named spot check via
  `StaticShapes.TryExtractOne`, asserting the exact known values observed during implementation:
  3 meshes, 305 total vertices, 486 total triangles, 5 effects, and mesh 0's `FtxRef`/`TxsRef`
  both `null` (confirmed genuinely absent from the relocation table, not a resolution bug — see
  Implementation Notes). Passes.

## Implementation Notes

- `OpenCobra/OVL/Files/StaticShapes.cs` implemented and manually verified (via a scratch console
  tool, not committed test code) against all 13 real `shs` symbols in
  `test/Shapes.unique.ovl` (11) and `ACAM/ACAM.unique.ovl` (2) — no crashes, plausible
  vertex/triangle/effect counts, correct null-`FtxRef`/`TxsRef` handling confirmed against raw
  relocation-table entries (not just a resolution-code guess).
- `Ovl.TryFindSymbol` added to `OpenCobra/OVL/OVL.cs` (see `ftx_ref`/`txs_ref` resolution in
  Goals above) — the reverse of `TryGetDataPointer`, generally reusable by future decoders.
- Found and fixed a real crash (see Gaps #1) during this verification pass, not caught by
  planning alone — `transparency != 0` does not imply a sort tail is present on disk.
- **`shs-viewer` Dumper plugin implemented**, twice-revised in scope (see Goals above) — landed
  as a real per-mesh live-resolving viewer, not the header-only fallback originally planned.
  Required adding general "ovl" host functions to `Dumper/Plugins/ViewerPlugin.cs`
  (`resolve_pointer`/`get_relocation_source`/`find_symbol`/`read_resource`/
  `current_resource_address`) plus `PluginManager.CurrentOvl`/`CurrentFile` live references and a
  `plugins/lib/ovl.ts` AssemblyScript wrapper — all reusable by future pointer-heavy decoders
  (`svd-viewer`, `ftx-viewer`, `sid-viewer`), not shs-specific. Verified end-to-end with a real
  Deno test (`shs-viewer/index.test.ts`) mocking the host functions and asserting the rendered
  HTML contains resolved per-mesh vertex/index counts and a resolved `TxsRef` symbol name — not
  just that the plugin loads.
- **Integration tests added** to `OpenCobra/Tests/Integration/ExtractResources.cs` (see Testing
  above) — `StaticShapes_AreDecodable` and `Load_ACAMHull_DecodesRealShapeWithGenuinelyNullSymbolRefs`,
  both passing against the full `RCT3_PATH` install. Caught a real test-writing bug along the way
  (not a decoder bug): the first version of `StaticShapes_AreDecodable` assumed every shape has
  ≥1 mesh and failed on 106 real symbols that legitimately have zero (`invisibleproxy`, vehicle
  "Bogey" pieces, track-joint `_ME` pieces) — fixed by only asserting non-empty
  `Vertices`/`Triangles` for shapes that do have meshes. No synthetic unit tests were written —
  decided against deliberately (see Testing above), not left undone.

## Status

**Implementation complete.** Core decoder (`StaticShapes.cs`), its `Ovl.TryFindSymbol` dependency,
the `shs-viewer` Dumper plugin (including new general-purpose "ovl" host functions), and
integration tests (`ExtractResources.cs`) are all implemented and verified against real archives.
Remaining work is exclusively the Post-Implementation Steps below (summary doc, README status
rows) and unblocking `ovl-scenery-item-visuals.md`'s `meshtype == 0` resolution. Production OVLs
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

- [x] Update this plan's status row in [`README.md`](./README.md) — now `✅ Done`.
- [x] Move `shs-viewer` from `📋 Planned` to `✅ Completed` in
      [`plugins/README.md`](../../../../plugins/README.md)'s status table.
- [x] Unblock `ovl-scenery-items.md`'s SHS dependency (note: `ovl-scenery-item-visuals.md` was
      consolidated into [`ovl-scenery-items.md`](./ovl-scenery-items.md) — that plan's
      Dependencies section now notes SHS is unblocked).
- [ ] Add a results summary under [`.agents/summaries/`](../../../summaries/) documenting this
      decoder (struct layout, the sort-tail heuristic, the "ovl" host-function surface) — not yet
      written.
