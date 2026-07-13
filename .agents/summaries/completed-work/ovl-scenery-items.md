# OVL Scenery Items (SID/SVD/MAM/TXT Decoders)

Implemented decoders for `sid` (SceneryItem), `svd` (SceneryItemVisual), `mam` (ManifoldMesh), and
`txt` (Text) OVL resources. Along the way, found and fixed a prerequisite gap in `Ovl.cs` itself:
cross-resource symbol references (`assignSymbolReference`-driven fields like `ftx_ref`,
`svds_ref[i]`, `name_ref`) were never resolvable against real archives — only intra-block pointers
were. This also silently affected already-shipped `StaticShapes.cs`. Remaining/outstanding work
(the `sid-viewer` `render()` crash, sound-script/ANR/LOD-distance follow-ups) is tracked in
[`ovl-scenery-items.md`](../../plans/features/ovl/ovl-scenery-items.md), not here.

## Why SID and SVD were decoded together

Every `sid` entry holds an array of `svd` symbol references (its visual definitions), and an `svd`
has no meaning without the `sid` that places it — the scenery-placement registry keys entries on
the raw `svd` symbol name, but placement shape (`sizeflag`) lives on the owning `sid`. Decoding
them separately would mean resolving half of a single conceptual relationship twice; doing them
together let the `SceneryItem.SvdRefs` → `SceneryItemVisual` link be modeled and tested end-to-end
in one pass, and let one Dumper plugin display both a scenery item and its resolved visuals/LODs in
one view instead of requiring two separate viewers cross-referenced by symbol name.

## What landed

1. **Layering consolidation (prerequisite)**: `OpenRCT3.Simulation.Placement` and
   `OpenCobra.OVL.SidPosition` were duplicate 9-value enums for the same `sizeflag` values, split
   across layers only because `Placement` was designed first in the simulation layer. Moved
   `Placement` down into `OpenCobra.OVL.Placement` (the shared, lower-layer type
   `OpenRCT3.Simulation` now references instead of duplicating) and deleted the redundant
   `SidPosition`. Same situation for bounding boxes: `OpenCobra.GDK.Meshes.BoundingBox` lived one
   layer above `OpenCobra.OVL` (confirmed via `GDK.csproj`'s `ProjectReference` to `OVL.csproj` —
   GDK depends on OVL, not the reverse), so `StaticShapes.cs` had worked around this by declaring
   `BoundingBoxMin`/`BoundingBoxMax` as two loose `Vector3` fields instead of reusing that type.
   Moved `BoundingBox` down into `OpenCobra.OVL.BoundingBox` too (small blast radius: only
   `GDK/Meshes/Mesh.cs` and `Tests/GDK/MeshTests.cs` consumed it), deleted the `GDK.Meshes` copy.
   Both were prerequisites so the new decoders and `ManifoldMesh` could use the shared, lower-layer
   types directly; retrofitting `StaticShape.BoundingBoxMin`/`BoundingBoxMax` to the same type
   remains optional cleanup, not done here.
2. **New decoders** (`OpenCobra/OVL/Files/`): `Text.cs` (bare UTF-16LE null-terminated string, no
   header struct, per `ManagerTXT.cpp`), `ManifoldMeshes.cs` (bbox + vertex/uint16-triangle-index
   arrays, per `manifoldmesh.h`/`ManagerMAM.cpp` — small, static, non-animated, referenced only from
   `svd`'s `proxy_ref`), `SceneryItemVisuals.cs` (versioned `SceneryItemVisual_V/_S/_W`, per-LOD
   mesh/texture refs, proxy mesh, per `sceneryvisual.h`/`ManagerSVD.cpp`), `SceneryItems.cs`
   (versioned `SceneryItem_V/_S/_W`, per-tile data, sound scripts, params, SVD/ANR refs, per
   `sceneryrevised.h`/`ManagerSID.cpp` — the most complex OVL manager: 40+ unknown fields,
   conditional sizing by version/addon/tile-count, variable-size 8-/16-byte sound-script commands).
3. **`Ovl.cs`: new symbol-reference table parser.** Added `TryResolveSymbolReference` (backed by a
   new `symbolReferenceTargets` map), which parses the archive's `SymbolRefStruct`/`SymbolRefStruct2`
   table (block type-index 2's third sub-block, right after the symbol table and loader table) —
   see `rct3-importer`'s `LodSymRefManager.cpp`/`ovlstructs.h`. This is genuinely distinct from the
   existing `relocations` base-fixup table: that one only resolves pointers to *other data within
   the archive's own blocks* (arrays, same-resource sub-structs); any field a symbol name resolves
   into elsewhere (`ftx_ref`, `txs_ref`, `shs_ref`, `svds_ref[i]`, `name_ref`, etc.) is left as an
   unpatched placeholder there and is only correctly resolved by walking this separate table.
4. **`StaticShapes.cs` fix**: `FtxRef`/`TxsRef` now use `TryResolveSymbolReference` — previously
   always resolved to `null` against real data (confirmed empirically: zero non-null hits across
   `Style\Vanilla\WallSets\Colonial\*`, despite those pieces definitely being textured).
5. **Dumper plugins**: added a new `ovl` host function pair, `resolve_symbol_reference` (wraps
   `TryResolveSymbolReference`) and `symbol_address` (resolves a symbol's own archive address, for
   walking *into* a different resource). Fixed `shs-viewer`'s `FtxRef`/`TxsRef` and `ter-viewer`'s
   `TextureRef` to use `resolve_symbol_reference` instead of `get_relocation_source`. Added a new
   `sid-viewer` plugin (metadata table, SVG placement diagram, LOD table per linked SVD, using the
   `Ovl` host-function surface to pull SVD data on demand from an SID's `SvdRefs` rather than
   requiring both resources pre-flattened) — see Known Issues below for its one open bug.

## How the `Ovl.cs` gap was found

Implementing `SceneryItems.cs`'s `SvdRefs`/`Listing.Name`/`Icon` resolution against real data
(`BigVase.common.ovl`, `Style\Vanilla\Style.common.ovl`, and the repo's vendored
`OpenCobra/Tests/Fixtures/OVL/*` fixtures) consistently returned empty results, even though the raw
`svd_count`/`icon_ref` fields clearly had nonzero values. Dumping the archive's raw
`SymbolRefStruct` entries directly (via a scratch console app referencing `OVL.csproj`) showed the
entries genuinely exist on disk (e.g. `BigVaseIcon:gsi` targeting the exact `icon_ref` field
address) but the existing `TryGetRelocationSource`/`TryFindSymbol` pair never looks at that table at
all. Cross-checked against `rct3-importer`'s reference source (`LodSymRefManager.cpp`,
`ovlstructs.h`) to confirm the on-disk layout and implement the parser.

## Symbol resolution scope: what's decoded vs. left as a raw ref

- **SHS (StaticShape), FTX (FlexiTexture), TXS (Texture Style), MAM (ManifoldMesh), TXT (Text)**:
  fully resolved/decoded. SHS/FTX already had decoders (`StaticShapes.cs`, `FlexiTexture.cs`); TXS
  is a bare tag with no payload of its own (`ManagerCommon.h`'s `ovlTXSManager`) so a resolved
  symbol name is already the complete answer; MAM and TXT got new decoders here (see above) since
  MAM is genuinely scenery-item-visual-specific (`ManagerMAM.cpp` is referenced only from
  `ManagerSVD.cpp`) and `Listing.Name`'s TXT content is directly user-visible in `sid-viewer`.
- **GSI (icons) and SND (sounds) stay out of scope**: no C# decoder exists for either; resolved as
  symbol names only (`Listing.Icon`, `SceneryItemSound.SoundRefs`) — actual icon-image and
  sound-content decoding are separate future efforts, not part of this work.
- **BSH (BoneShape) and BAN (BoneAnim) stay out of scope**: real candidates for their own decoder
  efforts, not folded in here. BSH (`ManagerBSH.cpp`, 345 lines) is a substantially larger mesh
  format than MAM. BAN (`ManagerBAN.cpp`, 156 lines; per-bone translate/rotate keyframe tracks, no
  vertex skinning weights) is reused well beyond scenery items — Wild! addon animal rigs and the
  Safari elephant-riding tracked ride both need it — so it belongs on its own rather than as a
  scenery-items side effect. (Those animal/ride systems reportedly use a separate, more complex
  morph/skinned animation system too, whose OVL resource tag isn't yet identified — a BAN decoder
  wouldn't cover that regardless.) `SVD` stores their refs as raw symbol-name strings only:
  `LodEntry.BoneShapeRef` (for `meshtype == 3` LODs) and `LodEntry.AnimationRefs`.
- `Listing.Name`/`Icon`/`Group`/`GroupIcon` legitimately come back empty when a single scenery
  item's own `common`/`unique` OVL pair is loaded in isolation (confirmed against real data): those
  symbols typically live in a pack-wide shared text/icon catalog OVL, not the item's own files. This
  isn't a decoder bug — resolving them requires loading that catalog file too, same as the real game
  loading a whole pack together, not a per-item decode step.

## Production OVLs with entries

Scanned every `*.ovl` under `RCT3_PATH\Style` (`Themed/*` and `Vanilla/*`, common+unique pairs, 3053
files, zero crashes): **3068 `sid` entries, 2694 `svd` entries** across the built-in scenery themes
(see `.agents/summaries/ovl-sid-svd-scan.csv`). Both `sid` and `svd` appear in `common.ovl` and
`unique.ovl` alike, not unique-only as `ManagerSID.h/cpp`'s comment implied.

Small single-pair files used as integration-test targets:
- `Style\Themed\Atlantis\Scenery\Vases\BigVase.{common,unique}.ovl` (and sibling `*Vase*.ovl`
  files) — 1 sid, 1 svd, matching symbol name.
- `Style\Themed\IslandParadise\PathExtras\Torches\BeachTorch01.{common,unique}.ovl` — 1 sid
  (`BeachTorchScenery01`), 2 svd (`BeachTorch01`, `BeachTorchScenery01`) — SID-to-multiple-SVD case.
- `Path\UnderWater\WaterFlat.common.ovl` — real proxy-mesh (`ManifoldMesh`) case.

Large multi-entry files for broader coverage: `Style\Themed\Adventure\Style.{common,unique}.ovl`
(176 sid, 0 svd), `Style\Themed\Atlantis\Style.{common,unique}.ovl` (67 sid, 0 svd),
`Style\Vanilla\Style.{common,unique}.ovl` (293 sid, 0 svd — used for the SID↔SVD linkage test).

## Testing

- `OpenCobra/Tests/OVL/SceneryItemVisualsTests.cs`, `SceneryItemsTests.cs`, `ManifoldMeshesTests.cs`,
  `TextTests.cs`: synthetic-struct byte-layout sanity checks, plus real-archive assertions (gated on
  `RCT3_PATH`) against `BigVase.common.ovl`, `BeachTorch01.common.ovl`, `Path\UnderWater\WaterFlat.common.ovl`,
  and `Style\Vanilla\Style.common.ovl` (SID↔SVD linkage, no dangling refs).
- Full solution test suite: unaffected suites still pass (`OpenCobra.Tests`: 191 passed;
  `OpenRCT3.Tests`: 59 passed; `OpenCobra.Tests.Integration`: 7,912 passed against the full real RCT3
  asset library, 0 failed — same `StaticShapes_AreDecodable`/`Load_ACAMHull_...` coverage as before,
  confirming the `Ovl.cs`/`StaticShapes.cs` changes introduced no regressions).
- Plugin tests (`deno test --allow-all` under `plugins/`): `shs-viewer`, `ter-viewer`, `mam-viewer`
  all pass; `sid-viewer`'s `name()`/`file_types()` pass, its two `render()` tests are
  `Deno.test.ignore`d (see Known Issues).

## Known Issues

- **`sid-viewer` `render()` crash (unresolved)**: calling `render()` with the full plugin (metadata
  table + placement diagram + LOD table all together) crashes the Extism JS test harness with
  `RangeError: Offset is outside the bounds of the DataView` inside Extism's own `store_u8`, thrown
  while marshaling the call. Bisected into standalone minimal plugins, every individual piece of the
  logic (SID field parsing, the placement SVG, the SVD/LOD-walking host-function chain, the TXT
  content fetch) passes on its own — the crash only reproduces once everything is combined in the
  real file, and the exact interaction hasn't been pinned down. The two affected tests are
  `Deno.test.ignore`d in `plugins/sid-viewer/index.test.ts` with a note pointing back here; `name()`/
  `file_types()` still pass, and the plugin does build cleanly via `scripts/build-plugins.ts`. This
  is tracked as open, active work in [`ovl-scenery-items.md`](../../plans/features/ovl/ovl-scenery-items.md).

## References

- [`ovl-scenery-items.md`](../../plans/features/ovl/ovl-scenery-items.md) — tracks remaining work
  (the `sid-viewer` crash, sound-script/ANR/LOD-distance follow-ups)
- [`OpenCobra/OVL/OVL.cs`](../../../OpenCobra/OVL/OVL.cs) — `Ovl.TryResolveSymbolReference`,
  `Ovl.ReadSymbolReferences`, `Ovl.SplitSymbolNameTag`
- [`OpenCobra/OVL/Files/SceneryItems.cs`](../../../OpenCobra/OVL/Files/SceneryItems.cs),
  [`SceneryItemVisuals.cs`](../../../OpenCobra/OVL/Files/SceneryItemVisuals.cs),
  [`ManifoldMeshes.cs`](../../../OpenCobra/OVL/Files/ManifoldMeshes.cs),
  [`Text.cs`](../../../OpenCobra/OVL/Files/Text.cs)
- [`plugins/sid-viewer/`](../../../plugins/sid-viewer/), [`plugins/lib/ovl.ts`](../../../plugins/lib/ovl.ts),
  [`Dumper/Plugins/ViewerPlugin.cs`](../../../Dumper/Plugins/ViewerPlugin.cs)
- [`features/scenery-placement-registry.md`](../../plans/features/scenery-placement-registry.md) —
  confirms (against `rct3-importer`'s `scenery.h`) that `sizeflag` lives on the `sid` struct, not `svd`
- Reference C++: `rct3-importer` → `RCT3 Importer/src/libOVLng/ManagerSID.cpp`,
  `ManagerSVD.cpp`, `ManagerMAM.cpp`, `ManagerTXT.cpp`, `LodSymRefManager.cpp`, `ovlstructs.h`,
  `sceneryrevised.h`, `sceneryvisual.h`, `manifoldmesh.h`
