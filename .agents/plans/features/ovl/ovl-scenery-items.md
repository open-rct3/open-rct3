# Decode SceneryItem (SID) and SceneryItemVisual (SVD) Entries

**Roadmap**: Phase 1, "Render built-in static (unanimated) scenery items" and "Render built-in animated scenery items"

**See also**: [`features/scenery-placement-registry.md`](../scenery-placement-registry.md) — confirms (against
`rct3-importer`'s `scenery.h`) that `sizeflag`, the field driving placement footprint/height-sampling
(`SIZE_FULLTILE` etc.), lives on the `sid` struct, not `svd`. `svd` is purely visual/render data (LOD, sway,
brightness, mesh refs).

**Decided**: `OpenCobra.OVL`'s existing `SidPosition` enum ([`Enums.cs`](../../../../OpenCobra/OVL/Enums.cs))
and `OpenRCT3.Simulation.Placement` ([`Placement.cs`](../../../../OpenRCT3/Simulation/Placement.cs)) are
duplicate 9-value enums for the same `sizeflag` values, split across layers only because `Placement` was
designed first in the simulation layer. Consolidate: move `Placement` down into `OpenCobra.OVL` (so it
becomes the shared, lower-layer type `OpenRCT3.Simulation` references rather than duplicates), delete the
redundant `SidPosition`, and update call sites. `SceneryItem.Position.Placement` in this plan is typed
as `Placement` directly (see Solution Architecture) — this consolidation is a prerequisite step for this plan,
not a follow-up.

**Also decided**: same situation, same fix, for bounding boxes. `OpenCobra.GDK.Meshes.BoundingBox`
(`Min`/`Max`/`Center`) lives one layer up from `OpenCobra.OVL` — confirmed via `GDK.csproj`'s
`ProjectReference` to `OVL.csproj` (GDK depends on OVL, not the reverse) — so `StaticShapes.cs` already works
around this by declaring `BoundingBoxMin`/`BoundingBoxMax` as two loose `Vector3` fields instead of reusing
that type. Upstream `BoundingBox` down into `OpenCobra.OVL` too (small blast radius: only
`GDK/Meshes/Mesh.cs` and `Tests/GDK/MeshTests.cs` consume it today), delete the `GDK.Meshes` copy, and update
those call sites. `ManifoldMesh` in this plan uses the relocated `BoundingBox` type from the start (see
Solution Architecture) rather than repeating `StaticShapes.cs`'s loose-field workaround; retrofitting
`StaticShape.BoundingBoxMin`/`BoundingBoxMax` to the same type is optional cleanup, not required by this plan.

## Why SID and SVD are one plan

These two resource types are covered together because they're tightly coupled in the real game: every `sid`
entry holds an array of `svd` symbol references (its visual definitions), and an `svd` has no meaning without
the `sid` that places it — `scenery-placement-registry.md` keys registry entries on the raw `svd` symbol name,
but placement shape (`sizeflag`) lives on the owning `sid`. Decoding them separately would mean resolving half
of a single conceptual relationship in each plan; doing them together lets the `SceneryItem.SvdRefs` →
`SceneryItemVisual` link be modeled and tested end-to-end in one pass, and lets one Dumper plugin display both
a scenery item and its resolved visuals/LODs in one view.

## Problem

SID entries are the most complex OVL file type — they define all placeable scenery objects (rides, stalls,
decorations, etc.) with UI metadata, positioning rules, colors, sounds, and references to visual definitions
(SVD). SVD entries define the visual representation referenced by SID: multiple LOD (Level of Detail) models,
each referencing StaticShape, BoneShape, or Billboard meshes with distance-based LOD switching and animation
references. The dumper should display comprehensive scenery item metadata alongside resolved visual/LOD
structure.

## Background Research

### SID (`ManagerSID.h/cpp`)

- Tag: `"sid"`, Name: `"SceneryItem"`, stored in **unique OVL only**
- Each SID = `cSid` with extensive metadata:
  - **UI**: name, icon, group, groupicon, type, cost, removal_cost
  - **Position**: positioning type, tile dimensions (x/z), position (x/y/z), size (x/y/z), supports
  - **Colors**: 3 default color values
  - **Square unknowns**: per-tile flags, min/max height, height bitmask, supports
  - **Extra**: version (0/1/2), addon pack (0=vanilla, 1=soaked, 2=wild — already has an `Addon` enum at
    [`OpenCobra/OVL/Enums.cs`](../../../../OpenCobra/OVL/Enums.cs), reuse it rather than a new
    `SceneryItemExtra.AddonPack`-specific type), generic addon
  - **Sounds**: array of sound references + animation scripts
  - **SVDs**: array of visual definition references
  - **Parameters**: key-value string pairs
  - **Flat ride**: individual animation references, chunked ANR parameters
- Multiple struct versions: `SceneryItem_V` (base), `SceneryItem_S` (v1), `SceneryItem_W` (v2)
- Common data: `SceneryItemData[]` (per-tile), `SceneryParams[]`, sound scripts, animation names
- Symbol references to: TXT (names), GSI (icons), SVD (visuals), SND (sounds)
- Data layout: unique block (main `SceneryItem` struct → SVD pointer array → sound array); common block
  (`SceneryItemData[]` → height bitmaps → `SceneryParams[]` → sound script data → animation name pointers);
  extra data for v1/v2 (`SceneryExtraSound[]`)
- Complexity notes: 563 lines in ManagerSID.cpp — most complex manager; 40+ unknown fields across all structs;
  conditional size calculations based on version, addon pack, tile count; sound scripts with variable-size
  commands (8 or 16 bytes)

### SVD (`ManagerSVD.h/cpp`)

- Tag: `"svd"`, Name: `"SceneryItemVisual"`, stored in **unique OVL only**
- Each SVD = `SceneryItemVisual` with:
  - `sivflags` — visual flags (bitfield):
    - 0x01 = Trees, Shrubs & Fern
    - 0x02 = Flowers
    - 0x04 = Rotational Variation
    - 0x70 = Unknown
    - 0x01000000 = Soaked!
    - 0x02000000 = Wild!
  - `sway` — amount of swaying (0.0 for static, 0.2 for trees)
  - `brightness` — brightness variation (1.0 default, 0.8 for swaying)
  - `scale` — scale variation (0.0 default, 0.4 for trees)
  - `lods[]` — array of `SceneryItemVisualLOD`
  - `proxy_ref` — manifold mesh reference
- Each LOD = `SceneryItemVisualLOD` with:
  - `meshtype` — 0 = StaticShape, 3 = BoneShape, 4 = Billboard
  - `name` — LOD name
  - `staticshape` — StaticShape reference (for meshtype 0)
  - `boneshape` — BoneShape reference (for meshtype 3)
  - `fts` — FlexiTexture reference (for billboards)
  - `txs` — Texture Style reference (for billboards, always "BillboardStandard")
  - `distance` — LOD distance threshold
  - `animations[]` — BoneAnim references (for animated meshes)
  - Various unknown floats (unk7-unk14)
- Data layout: unique block (`SceneryItemVisual` struct → LOD pointer array → LOD structs); common block
  (animation name arrays → LOD model references)
- Symbol references to: SHS (StaticShape), BSH (BoneShape), FTX (FlexiTexture), TXS (Texture Style), BAN
  (BoneAnim), MAM (ManifoldMesh)

## Solution Architecture

### New File: `OpenCobra/OVL/Files/SceneryItems.cs`

All records below follow the codebase's existing convention (`StaticShapes.cs`, `TerrainTypes.cs`) of
`public readonly record struct`, not plain `record` classes — cheaper allocation for what's otherwise a flat
bag of value fields plus a few reference-typed lists.

The signatures below are the pseudo-code contract only — implementation adds real XML doc summaries/remarks
on every public type and member (per the codebase's existing convention, e.g. `StaticShapes.cs`,
`TerrainTypes.cs`), not just the inline comments shown here.

```csharp
public readonly record struct SceneryItemListing(
  string Name,          // resolved TXT content, not just a symbol name - see Dependencies (TXT is in scope)
  string Icon,           // GSI symbol name only - GSI decoding stays out of scope, see Dependencies
  string Group,
  string GroupIcon,
  uint SceneryType,      // named to avoid shadowing System.Type
  int Cost,
  int RemovalCost
);

public readonly record struct SceneryItemPosition(
  Placement Placement,   // OpenRCT3.Simulation.Placement, consolidated down into OpenCobra.OVL - see top-of-file note
  uint XSquares,
  uint ZSquares,
  Vector3 Position,
  Vector3 Size,
  string SupportsRef
);

public readonly record struct SceneryItemTile(
  uint Flags,
  int MinHeight,
  int MaxHeight,
  uint SupportFlags
);

// Opaque for this pass - command parsing deferred to Future Work ("Full sound script parsing").
// RawCommands holds each variable-size (8- or 16-byte) command's bytes undecoded, just sliced out.
public readonly record struct SoundScript(
  IReadOnlyList<ReadOnlyMemory<byte>> RawCommands
);

// Opaque for this pass - key/value are read as-is; typed/validated parameter parsing is Future Work.
public readonly record struct SceneryParam(
  string Key,
  string Value
);

public readonly record struct SceneryItemSound(
  IReadOnlyList<string> SoundRefs,  // SND symbol names only - SND decoding stays out of scope
  IReadOnlyList<SoundScript> AnimationScripts
);

public readonly record struct SceneryItemExtra(
  ushort Version,
  Addon AddonPack,      // reuse existing OpenCobra.OVL.Addon enum, not a new type
  uint GenericAddon,
  float Unknown,
  uint BillboardAspect
);

public readonly record struct SceneryItem(
  string Name,
  string OvlPath,
  SceneryItemListing Listing,
  SceneryItemPosition Position,
  uint PrimaryColor, uint SecondaryColor, uint TertiaryColor,
  IReadOnlyList<SceneryItemTile> Tiles,
  SceneryItemExtra Extra,
  IReadOnlyList<SceneryItemSound> Sounds,
  IReadOnlyList<string> SvdRefs,  // SVD references
  IReadOnlyList<SceneryParam> Parameters,
  IReadOnlyList<string> AnrRefs  // flat ride ANR symbol refs, raw names only - ANR decoding is a separate future plan
);

public static class SceneryItems {
  public static IReadOnlyList<SceneryItem> Extract(Ovl ovl);
}
```

### New File: `OpenCobra/OVL/Files/SceneryItemVisuals.cs`

```csharp
// Mesh type reuses the existing SvdLodType enum (OpenCobra/OVL/Enums.cs) - do not redeclare it here.

public readonly record struct LodEntry(
  string Name,
  SvdLodType MeshType,
  string? StaticShapeRef,
  string? BoneShapeRef,
  string? FtsRef,       // FlexiTexture (billboards) - resolves via FlexiTextureList.Load
  string? TxsRef,       // Texture Style (billboards) - resolved symbol name only, see Dependencies
  float LodDistance,
  IReadOnlyList<string> AnimationRefs  // BoneAnim references
);

public readonly record struct SceneryItemVisual(
  string Name,
  SvdFlags Flags,        // reuse existing OpenCobra.OVL.SvdFlags enum, not a raw uint
  float Sway,
  float Brightness,
  float Scale,
  IReadOnlyList<LodEntry> Lods,
  ManifoldMesh? ProxyMesh    // decoded via ManifoldMeshes.Extract, see below - not a raw symbol-name ref
);

public static class SceneryItemVisuals {
  public static IReadOnlyList<SceneryItemVisual> Extract(Ovl ovl);
}
```

### New File: `OpenCobra/OVL/Files/ManifoldMeshes.cs`

In scope for this plan (see Dependencies below for why) — mirrors `StaticShapes.cs`'s pattern for a small,
self-contained mesh format: bbox, vertex array, triangle-index array, no textures, no version branching.

```csharp
public readonly record struct ManifoldMesh(
  string Name,
  BoundingBox BoundingBox,  // relocated OpenCobra.OVL.BoundingBox - see top-of-file note
  IReadOnlyList<Vector3> Vertices,
  IReadOnlyList<Triangle> Faces   // reuse existing Triangle from StaticShapes.cs
);

public static class ManifoldMeshes {
  public static IReadOnlyList<ManifoldMesh> Extract(Ovl ovl);
}
```

### Implementation Steps

**Step 0 — layering consolidation (prerequisite)**: two moves, both described in the top-of-file note, both
required before `SceneryItems.cs`/`ManifoldMeshes.cs` can depend on the relocated types:
- Move `OpenRCT3.Simulation.Placement` down into `OpenCobra.OVL`, delete the redundant `SidPosition` enum,
  update `OpenRCT3.Simulation` call sites.
- Move `OpenCobra.GDK.Meshes.BoundingBox` down into `OpenCobra.OVL`, delete the `GDK.Meshes` copy, update
  `GDK/Meshes/Mesh.cs` and `Tests/GDK/MeshTests.cs` call sites.

**Decided**: implement SVD and SID together in one pass (not staged as separate PRs) — they're tightly
coupled and the plan already models the `SvdRefs` cross-check as part of one end-to-end change. Order within
the pass still starts with SVD (fewer unknowns, no version branching), then SID, as below.

**Undecoded refs (BSH/BAN)**: see Dependencies below for what's resolvable now (SHS, FTX, TXS, MAM) vs. what
still needs a decoder. Both are per-LOD (`LodEntry.BoneShapeRef` for `meshtype == 3`, `LodEntry.AnimationRefs`)
and store raw symbol-name strings only until their own follow-up decoder plans land. MAM is in scope here —
`SceneryItemVisual.ProxyMesh` resolves to a decoded `ManifoldMesh`, not a raw string; see New Files below.

**SVD first** (fewer unknowns, no version branching) — decode it standalone, then use it to resolve
`SceneryItem.SvdRefs` when decoding SID:

1. Find loaders where `Tag == "svd"` (unique OVL only)
2. Parse `SceneryItemVisual` struct from loader data
3. Read LOD array from relocated pointers
4. For each LOD: determine mesh type and extract appropriate reference; read animation references (for
   BoneShape meshes); read billboard references (fts/txs for Billboard meshes)
5. Resolve symbol references to SHS, BSH, FTX, TXS, BAN, MAM
6. Return list of `SceneryItemVisual`

**SID second**, using the SVD decoder's symbol names to validate `SvdRefs`:

1. Find loaders where `Tag == "sid"` (unique OVL only)
2. Determine struct version from extra.version field
3. Parse main `SceneryItem` struct (version-dependent size)
4. Read SVD pointer array and resolve symbol references (cross-check against `SceneryItemVisuals.Extract`
   output — every `SvdRef` should resolve to a decoded `SceneryItemVisual`)
5. Read common data: `SceneryItemData[]` (per-tile), height bitmaps
6. Resolve the `Listing.Name` TXT reference to its decoded text content (TXT is in scope, see Dependencies);
   leave `Listing.Icon` (GSI) as a resolved symbol name only
7. Read sound array; resolve `SoundRefs` as SND symbol names only (SND stays out of scope), read each
   sound script's raw command bytes into `SoundScript.RawCommands` without parsing them
8. Read parameters array into raw `SceneryParam` key/value pairs
9. Read flat-ride ANR pointer array into `AnrRefs` as raw symbol names (ANR decoding is a separate future plan)
10. Return list of `SceneryItem`

### Files to Create/Modify

**Create:**

- `OpenCobra/OVL/Files/SceneryItemVisuals.cs`
- `OpenCobra/OVL/Files/SceneryItems.cs`
- `OpenCobra/OVL/Files/ManifoldMeshes.cs`
- `OpenCobra/OVL/Files/Text.cs`

**Modify:**

- `OpenRCT3/Simulation/Placement.cs` → moves to `OpenCobra/OVL/Enums.cs` (or a new `OpenCobra/OVL/Placement.cs`);
  delete `SidPosition`; update `OpenRCT3.Simulation` call sites (Step 0 above)
- `OpenCobra/GDK/Meshes/BoundingBox.cs` → moves to `OpenCobra/OVL/BoundingBox.cs`; delete the `GDK.Meshes`
  copy; update `GDK/Meshes/Mesh.cs` and `Tests/GDK/MeshTests.cs` call sites (Step 0 above)

### Dependencies

- Existing relocation resolution
- Symbol reference resolution for TXT, GSI, SVD, SND, SHS, BSH, FTX, TXS, BAN, MAM
- **SHS: unblocked.** [`ovl-static-shapes.md`](../../../summaries/completed-work/ovl-static-shapes.md)
  is done — `OpenCobra.OVL.Files.StaticShapes.Extract` and `Ovl.TryFindSymbol` are implemented and
  verified against every real `shs` symbol under `RCT3_PATH`, so `svd`'s `meshtype == 0`
  (StaticShape) case can resolve `staticshape` symbol refs directly via
  `StaticShapes.Extract`/`TryExtractOne` rather than needing new decoder work here.
- **FTX: unblocked.** `OpenCobra/OVL/Files/FlexiTexture.cs` (`FlexiTextureList.Load`) already fully decodes
  `ftx` resources, so Billboard LODs' `FtsRef` can resolve real flexi-texture data, not just a symbol name.
- **TXS: not actually blocked — nothing to decode.** Per the reference source (`ManagerCommon.h`'s
  `ovlTXSManager`), `"txs"` is a bare tag with no struct/payload; a txs symbol only exists so other
  resources can look up its *name string* by relocated pointer. `TxsRef` is just the resolved symbol name via
  `Ovl.TryFindSymbol`, same as `StaticMesh.TxsRef` already does (`StaticShapes.cs:160`).
- **MAM: in scope for this plan.** `ManifoldMesh` (`manifoldmesh.h`) is a small, static, non-animated
  struct — bbox min/max, vertex array, triangle-index array, nothing else — and `ManagerMAM.cpp` (104 lines)
  is referenced from `ManagerSVD.cpp` only, nowhere else in the reference source. Unlike BAN, it's genuinely
  scenery-item-visual-specific, so decode it here: add `OpenCobra/OVL/Files/ManifoldMeshes.cs` (mirroring
  `StaticShapes.cs`'s pattern) so `SceneryItemVisual.ProxyMesh` resolves to real geometry instead of a raw
  symbol name.
- **TXT: in scope for this plan.** No C# decoder exists yet (`FileTypes.cs` only recognizes the `"txt"` tag,
  same as TXS before this session), but `Listing.Name` is directly user-visible in the sid-viewer's metadata
  table, so decode it here: add `OpenCobra/OVL/Files/Text.cs`.
- **GSI (icons) and SND (sounds) stay out of scope.** Neither has a C# decoder yet; resolve them as symbol
  names only via `Ovl.TryFindSymbol` (`Listing.Icon`, `SceneryItemSound.SoundRefs`) — actual icon image and
  sound content decoding are separate future plans.
- **BSH (BoneShape) and BAN (BoneAnim) stay out of scope** — real candidates for their own follow-up decoder
  plans, not folded in here. BSH (`ManagerBSH.cpp`, 345 lines) is a substantially larger mesh format than
  MAM. BAN (`ManagerBAN.cpp`, 156 lines; per-bone translate/rotate keyframe tracks, no vertex skinning
  weights) is reused well beyond scenery items — Wild! addon animal rigs and the Safari elephant-riding
  tracked ride both need it — so it belongs in a plan of its own rather than as a scenery-items side effect.
  (Those animal/ride systems reportedly use a separate, more complex morph/skinned animation system too,
  whose OVL resource tag isn't yet identified — a BAN decoder wouldn't cover that regardless.) Until BSH/BAN
  land, `SVD` stores their refs as raw symbol-name strings only: `LodEntry.BoneShapeRef` (for
  `meshtype == 3` LODs) and `LodEntry.AnimationRefs`.

### Regression Prevention

- No changes to `Ovl.cs`
- Run the existing NUnit suite (see Testing Strategy below) before/after implementation

### Testing Strategy

The `TestRunner`/`OvlTest[]` pattern this section originally described no longer exists in the codebase.
Current convention: NUnit tests in `OpenCobra/Tests/OVL/SceneryItemVisualsTests.cs` and
`OpenCobra/Tests/OVL/SceneryItemsTests.cs`, plus real-archive checks in
`OpenCobra/Tests/Integration/ExtractResources.cs` gated by `RCT3_PATH` — see `ovl-materials-integration.md`'s
test plan for a live example. Cover:

- **SVD**: synthetic-struct decode of a `SceneryItemVisual` with one LOD per mesh type
  (StaticShape/BoneShape/Billboard), and — against real data — that every `svd`-tagged symbol (unique OVL
  only) decodes with at least one LOD.
- **SID**: synthetic-struct decode per version (v0/v1/v2), `sizeflag` resolving to one of the 9 consolidated
  `Placement` values, and — against real data — that every `sid`-tagged symbol (unique OVL only) decodes
  with a non-empty `Listing.Name` (resolved TXT content).
- **SID ↔ SVD linkage**: against real data, every `SceneryItem.SvdRefs` entry resolves to a symbol name present
  in the decoded `SceneryItemVisual` set (no dangling refs).
- **MAM**: synthetic-struct decode of a `ManifoldMesh` (bbox + vertex/face arrays), and — against real data —
  that every SVD with a non-null proxy ref decodes without error via `ManifoldMeshes.Extract`.
- **Layering consolidation**: after Step 0, `OpenRCT3.Simulation`'s existing `SceneryPlacementTests.cs` and
  `Tests/GDK/MeshTests.cs` still pass unchanged against the relocated `Placement`/`BoundingBox` types —
  confirms both moves didn't change values/order/behavior.

Also worth checking against
[`ovl-resource-relocation.md`](../../summaries/completed-work/ovl-resource-relocation.md) before trusting
decoded output: that bug's fix targeted `svd`/`ftx` symbol resolution specifically, so this decoder is a
natural place to add the byte-offset `SvdFlags` coverage test that bug's writeup flags as a follow-up.

### Success Criteria

- `Placement`/`SidPosition` and `BoundingBox` consolidated into `OpenCobra.OVL` with no behavior change in
  `OpenRCT3.Simulation`/`OpenCobra.GDK`
- All SVD entries extracted with LOD structure; mesh types correctly identified
  (StaticShape/BoneShape/Billboard); symbol references resolved for all mesh types; LOD distance thresholds
  parsed
- Every SVD with a non-null proxy mesh decodes a `ManifoldMesh` (bbox, vertices, faces) via `ManifoldMeshes.Extract`
- All SID entries extracted with full metadata; version-dependent struct parsing correct (v0, v1, v2);
  `Listing.Name` resolved via decoded TXT content; `Listing.Icon`/`SoundRefs` resolved as symbol names;
  `SoundScript`/`SceneryParam`/`AnrRefs` populated with raw (undecoded) data rather than dropped; tile data
  parsed correctly
- Every SID's `SvdRefs` resolves against the decoded SVD set
- Zero regressions

## Dumper Plugin

Ship one `sid-viewer` Extism plugin (per `plugins/README.md`'s contract) that renders a scenery item's metadata
alongside its resolved SVDs/LODs inline, rather than separate `sid-viewer`/`svd-viewer` plugins — this mirrors
how the two resources are actually consumed in-game and avoids the user having to cross-reference two plugin
outputs by symbol name. Use the `Ovl` host-function surface (`resolve_pointer`/`get_relocation_source`/
`find_symbol`/`read_resource`/`current_resource_address`, per the OVL plans README) to pull SVD data on demand
from an SID's `SvdRefs` rather than requiring both resources pre-flattened.

**Layout** (three sections, all metadata/diagram — no rendered geometry, no texture thumbnails, no raw hex
view; deliberately smaller scope than `shs-viewer`/`mam-viewer`'s hex-view fallback):

1. **Metadata table** — `Listing` (name/icon/group/cost/removal cost), `Extra` (version, `Addon` enum,
   generic addon), default colors as swatches (not raw ints).
2. **Placement diagram** — an SVG grid combining the (now-consolidated, see top-of-file note) `Placement`
   enum with the `XSquares`/`ZSquares` tile footprint: shade occupied tiles, mark the anchor tile, and
   annotate edge/quarter/wall placements (`PathEdgeInner`/`PathEdgeOuter`/`Quarter`/`Wall`/etc.) so a
   reviewer can see placement shape at a glance instead of decoding `Placement` + tile-flag numbers by hand.
   Per-tile flags (min/max height, supports, collision) shown as a hover/label on each grid cell.
3. **LOD table** (per resolved SVD) — one row per `LodEntry`: mesh type, distance threshold, resolved refs
   (`StaticShapeRef`/`FtsRef`/`TxsRef` as symbol names; `BoneShapeRef`/`AnimationRefs` shown as raw symbol
   names labeled "(undecoded — BSH/BAN)" per the Dependencies section above), `SceneryItemVisual.ProxyMesh`
   summarized (vertex/face counts) when present.

## Production OVLs with Entries

> **Status**: Identified — see `.agents/summaries/ovl-sid-svd-scan.csv`

Scanned every `*.ovl` under `RCT3_PATH\Style` (`Themed/*` and `Vanilla/*`, common+unique pairs, 3053 files,
zero crashes): **3068 `sid` entries, 2694 `svd` entries** across the built-in scenery themes. Both `sid` and
`svd` appear in `common.ovl` and `unique.ovl` alike (not unique-only as `ManagerSID.h/cpp`'s comment implied —
verify against real data once the decoder reads struct contents, not just loader tags).

Good targets for integration tests — small files isolating exactly one `sid`/`svd` pair instead of a
176-entry `Style.ovl`:
- `Style\Themed\Atlantis\Scenery\Vases\BigVase.{common,unique}.ovl` (and sibling `*Vase*.ovl` files) — 1 sid, 1
  svd, matching symbol name
- `Style\Themed\IslandParadise\PathExtras\Torches\BeachTorch01.{common,unique}.ovl` — 1 sid
  (`BeachTorchScenery01`), 2 svd (`BeachTorch01`, `BeachTorchScenery01`) — good for the SID→multiple-SVD case

Large multi-entry files for broader coverage: `Style\Themed\Adventure\Style.{common,unique}.ovl` (176 sid, 0
svd), `Style\Themed\Atlantis\Style.{common,unique}.ovl` (67 sid, 0 svd).

**Correction**: the plan's prior "known test files" line (`style.common.ovl`/`style.unique.ovl`, claimed no SID
or SVD entries) was wrong — `Style\Vanilla\Style.{common,unique}.ovl` is that same file and has 293 `sid`
entries (0 `svd`). No confirmed SID/SVD-empty file has been identified; not needed now that populated files
above are known.

## Post-Implementation Steps

When this decoder is implemented, add a results summary under `.agents/summaries/` (see
`completed-work/flat-empty-park.md` for the current convention) and update this plan's status/README row.

### Future Work

- Full sound script parsing
- Flat ride animation references
- Visualize LOD switching distances
