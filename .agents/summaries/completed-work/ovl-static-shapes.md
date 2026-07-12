# OVL Static Shape (shs) Decoding

Decoded `shs`-tagged `StaticShape` entries — the vertex/index/effect geometry referenced by
scenery, terrain, and ride `svd` visuals. `OpenCobra/OVL/Files/StaticShapes.cs`:
`StaticShapes.Extract`/`TryExtractOne` produce `StaticShape` → `StaticMesh[]` → `Vertex`/
`Triangle`/`EffectPoint`, matching `rct3-importer`'s `staticshape.h`/`ManagerSHS.cpp` struct
layout and allocation order.

Whole-install verification (`RCT3_PATH`, `*.unique.ovl` only — `common`/`unique` halves of a pack
are confirmed byte-identical for this type, not partitioned): **32,612 `shs` symbols across
11,514 files, every one decodes, 0 crashes.** `make test` (233 tests) passes.

## What landed

1. **`StaticShapes.cs`** decodes the full struct chain via `Ovl.TryResolveRelocation`/
   `TryGetRelocationSource`, including the per-mesh sort tail (two extra index-array copies used
   for transparent-geometry back-to-front sorting).
2. **`Ovl.TryFindSymbol`** added to `OpenCobra/OVL/OVL.cs` — the reverse of the existing
   `TryGetDataPointer`, resolving a relocated pointer that points directly at another symbol's
   data (e.g. `StaticShapeMesh.ftx_ref`/`txs_ref`) back to that symbol's name. Generic, not
   SHS-specific; backed by a `Dictionary<uint, OvlFile>` populated alongside the existing
   `entryDataPtrs`.
3. **General "ovl" host functions** in `Dumper/Plugins/ViewerPlugin.cs`
   (`resolve_pointer`/`get_relocation_source`/`find_symbol`/`read_resource`/
   `current_resource_address`), wrapped for AssemblyScript by `plugins/lib/ovl.ts`. Lets any
   Dumper plugin request further archive data on demand against whichever archive is currently
   open (`PluginManager.CurrentOvl`/`CurrentFile`), rather than only ever seeing its own
   resource's raw, unresolved bytes. `plugins/shs-viewer` uses this to render a real per-mesh
   table (vertex/index counts, support type, sides, resolved `FtxRef`/`TxsRef`), not just a
   header/hex dump.
4. **Integration tests** in `OpenCobra/Tests/Integration/ExtractResources.cs` —
   `StaticShapes_AreDecodable` (every `*.unique.ovl` under `RCT3_PATH`) and
   `Load_ACAMHull_DecodesRealShapeWithGenuinelyNullSymbolRefs` (named spot check against exact
   known values). No synthetic-OVL-with-relocations unit-test harness was built — decided against
   deliberately: no such harness exists anywhere in this codebase yet, and real fixture archives
   already cover the decoder thoroughly.

## Format gotchas found during implementation (not caught by planning alone)

- **`transparency != 0` does not imply a sort tail is present.** `algo_x`/`algo_y`/`algo_z`
  (`ManagerSHS.h`) exist only on the writer's in-memory pre-serialization struct, never persisted
  to disk — there is no reliable on-disk signal for sort-tail presence. `StaticShapes.cs` only
  commits to the "raw index count + sort tail" read when `transparency != 0` **and** the tail
  actually fits within the resolved block's remaining bytes; otherwise it falls back to
  "`index_count` is already a triangle count." This is a bounds-check heuristic, not a proven
  decode rule — still open if a real signal turns out to exist.
- **`SupportType::None` is `0xFFFFFFFF`** (`rct3constants.h`), not `0` — relevant to
  `StaticShape.MeshCount2`, which the decoder computes from decoded meshes rather than trusting
  the on-disk field.
- **`StaticShape` is 56 bytes, not 60** — an earlier plan revision miscounted; confirmed by
  summing the 10 actual fields in `staticshape.h`.
- **Some real shapes legitimately have zero meshes** (`invisibleproxy`, vehicle "Bogey" pieces,
  track-joint `_ME` pieces) — not a decode failure.
- **`FtxRef`/`TxsRef` can be genuinely absent** (confirmed against `ACAMHull`'s raw relocation
  table entries, not just a resolution-code guess) — a mesh with no `ftx_ref`/`txs_ref` listed at
  all is expected, not a bug.

## Fixed in passing

- **`StripOvlTagSuffix`** (`OpenCobra/OVL/Files/FileTypes.cs`) left multi-frame names like
  `"Foo.ftx#0"` unstripped — the tag-boundary scan didn't stop at `#`, so `"ftx#0"` failed
  `ToFileType()` and the whole name was returned unchanged. Unrelated to SHS; surfaced via
  `make test` while finishing this work.
- **`make test` silently ran a stale `Tests.dll`/missing `OpenRCT3.Tests.dll`** — the Makefile's
  `test` target didn't depend on `OpenRCT3.Tests.dll` at all, and its own source-change detection
  shelled out to `find`, which resolves inconsistently across shells/platforms (Windows' builtin
  `FIND.EXE` vs GNU find). Replaced with a pure `$(wildcard)`-based `rwildcard` macro; added the
  missing dependency.

## Not done / deferred

- Direct rendering integration (building GDK meshes from `StaticShape.Meshes[i].Vertices`/
  `.Triangles`) — a follow-up renderer plan's job, not this one's.
- The sort-tail heuristic above is unverified against a real symbol that would expose it wrong;
  no such symbol has been found yet in 32,612 real shapes.

## References

- [`OpenCobra/OVL/Files/StaticShapes.cs`](../../../OpenCobra/OVL/Files/StaticShapes.cs)
- [`OpenCobra/OVL/OVL.cs`](../../../OpenCobra/OVL/OVL.cs) — `TryFindSymbol`
- [`Dumper/Plugins/ViewerPlugin.cs`](../../../Dumper/Plugins/ViewerPlugin.cs) — "ovl" host functions
- [`plugins/lib/ovl.ts`](../../../plugins/lib/ovl.ts), [`plugins/shs-viewer`](../../../plugins/shs-viewer)
- [`OpenCobra/Tests/Integration/ExtractResources.cs`](../../../OpenCobra/Tests/Integration/ExtractResources.cs)
- `rct3-importer`: `include/staticshape.h`, `src/libOVLng/ManagerSHS.{h,cpp}`, `include/rct3constants.h`
- [`ovl-resource-relocation.md`](ovl-resource-relocation.md) — the relocation-resolution machinery this decoder builds on
