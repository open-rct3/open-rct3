# Decode SceneryItem (SID) and SceneryItemVisual (SVD) Entries

**See also**: [`features/scenery-placement-registry.md`](../scenery-placement-registry.md) — confirms (against
`rct3-importer`'s `scenery.h`) that `sizeflag`, the field driving placement footprint/height-sampling
(`SIZE_FULLTILE` etc., mapped there to a `Placement` enum), lives on the `sid` struct, not `svd`. `svd` is
purely visual/render data (LOD, sway, brightness, mesh refs). That plan's `Placement`/`AnimationKind` design is
what this decoder's output will eventually feed; this plan should add a `SizeFlag`/`Placement`-shaped field to
`SceneryItem` (or `SceneryItemPosition`) once implemented, rather than leaving placement-shape data out of the
record below as currently drafted.

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
  - **Extra**: version (0/1/2), addon pack (0=vanilla, 1=soaked, 2=wild), generic addon
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

```csharp
public record SceneryItemUI {
  string Name;
  string Icon;
  string Group;
  string GroupIcon;
  uint Type;
  int Cost;
  int RemovalCost;
}

public record SceneryItemPosition {
  ushort PositioningType;
  uint XSquares;
  uint ZSquares;
  float XPos, YPos, ZPos;
  float XSize, YSize, ZSize;
  string Supports;
}

public record SceneryItemTile {
  uint Flags;
  int MinHeight;
  int MaxHeight;
  uint Supports;
}

public record SceneryItemSound {
  IReadOnlyList<string> SoundNames;  // SND references
  IReadOnlyList<SoundScript> AnimationScripts;
}

public record SceneryItemExtra {
  ushort Version;
  uint AddonPack;       // 0=vanilla, 1=soaked, 2=wild
  uint GenericAddon;
  float UnkF;
  uint BillboardAspect;
}

public record SceneryItem {
  string Name;
  string OvlPath;
  SceneryItemUI UI;
  SceneryItemPosition Position;
  uint[] DefaultColors;  // 3 entries
  IReadOnlyList<SceneryItemTile> Tiles;
  SceneryItemExtra Extra;
  IReadOnlyList<SceneryItemSound> Sounds;
  IReadOnlyList<string> SvdRefs;  // SVD references
  IReadOnlyList<SceneryParam> Parameters;
}

public static class SceneryItems {
  public static IReadOnlyList<SceneryItem> Extract(Ovl ovl);
}
```

### New File: `OpenCobra/OVL/Files/SceneryItemVisuals.cs`

```csharp
public enum MeshType {
  StaticShape = 0,
  BoneShape = 3,
  Billboard = 4
}

public record LodEntry {
  string Name;
  MeshType MeshType;
  string? StaticShapeRef;
  string? BoneShapeRef;
  string? FtsRef;       // FlexiTexture (billboards)
  string? TxsRef;       // Texture Style (billboards)
  float Distance;       // LOD distance threshold
  IReadOnlyList<string> AnimationRefs;  // BoneAnim references
}

public record SceneryItemVisual {
  string Name;
  uint Flags;
  float Sway;
  float Brightness;
  float Scale;
  IReadOnlyList<LodEntry> Lods;
  string? ProxyRef;     // ManifoldMesh reference
}

public static class SceneryItemVisuals {
  public static IReadOnlyList<SceneryItemVisual> Extract(Ovl ovl);
}
```

### Implementation Steps

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
6. Read sound array with SND symbol references
7. Read parameters array
8. Return list of `SceneryItem`

### Files to Create/Modify

**Create:**

- `OpenCobra/OVL/Files/SceneryItemVisuals.cs`
- `OpenCobra/OVL/Files/SceneryItems.cs`

### Dependencies

- Existing relocation resolution
- Symbol reference resolution for TXT, GSI, SVD, SND, SHS, BSH, FTX, TXS, BAN, MAM
- **SHS: unblocked.** `ovl-static-shapes.md` is done — `OpenCobra.OVL.Files.StaticShapes.Extract`
  and `Ovl.TryFindSymbol` are implemented and verified against every real `shs` symbol under
  `RCT3_PATH`, so `svd`'s `meshtype == 0` (StaticShape) case can resolve `staticshape` symbol refs
  directly via `StaticShapes.Extract`/`TryExtractOne` rather than needing new decoder work here.
  BSH (BoneShape), FTX, TXS, BAN, MAM remain undecoded and still block the other `meshtype` cases.

### Regression Prevention

- No changes to `Ovl.cs`
- Run TestRunner before/after implementation

### Testing Strategy

The `TestRunner`/`OvlTest[]` pattern this section originally described no longer exists in the codebase.
Current convention: NUnit tests in `OpenCobra/Tests/OVL/SceneryItemVisualsTests.cs` and
`OpenCobra/Tests/OVL/SceneryItemsTests.cs`, plus real-archive checks in
`OpenCobra/Tests/Integration/ExtractResources.cs` gated by `RCT3_PATH` — see `ovl-materials-integration.md`'s
test plan for a live example. Cover:

- **SVD**: synthetic-struct decode of a `SceneryItemVisual` with one LOD per mesh type
  (StaticShape/BoneShape/Billboard), and — against real data — that every `svd`-tagged symbol (unique OVL
  only) decodes with at least one LOD.
- **SID**: synthetic-struct decode per version (v0/v1/v2), `sizeflag` resolving to one of the 9 `Placement`
  values from `scenery-placement-registry.md`, and — against real data — that every `sid`-tagged symbol
  (unique OVL only) decodes with a non-empty UI name.
- **SID ↔ SVD linkage**: against real data, every `SceneryItem.SvdRefs` entry resolves to a symbol name present
  in the decoded `SceneryItemVisual` set (no dangling refs).

Also worth checking against
[`ovl-resource-relocation.md`](../../summaries/completed-work/ovl-resource-relocation.md) before trusting
decoded output: that bug's fix targeted `svd`/`ftx` symbol resolution specifically, so this decoder is a
natural place to add the byte-offset `SvdFlags` coverage test that bug's writeup flags as a follow-up.

### Success Criteria

- All SVD entries extracted with LOD structure; mesh types correctly identified
  (StaticShape/BoneShape/Billboard); symbol references resolved for all mesh types; LOD distance thresholds
  parsed
- All SID entries extracted with full metadata; version-dependent struct parsing correct (v0, v1, v2); symbol
  references to SVD/SND/TXT/GSI resolved; tile data parsed correctly
- Every SID's `SvdRefs` resolves against the decoded SVD set
- Zero regressions

## Dumper Plugin

Ship one `sid-viewer` Extism plugin (per `plugins/README.md`'s contract) that renders a scenery item's metadata
alongside its resolved SVDs/LODs inline, rather than separate `sid-viewer`/`svd-viewer` plugins — this mirrors
how the two resources are actually consumed in-game and avoids the user having to cross-reference two plugin
outputs by symbol name. Use the `Ovl` host-function surface (`resolve_pointer`/`get_relocation_source`/
`find_symbol`/`read_resource`/`current_resource_address`, per the OVL plans README) to pull SVD data on demand
from an SID's `SvdRefs` rather than requiring both resources pre-flattened.

## Production OVLs with Entries

> **Status**: Not yet identified

Production OVL archives containing scenery item (`sid`) and scenery item visual (`svd`) entries have not yet
been catalogued. To identify:

1. Scan production OVLs for loader entries with `Tag == "sid"` and `Tag == "svd"` (unique OVL only)
2. Document common vs unique archive distribution
3. Note sample symbol names for verification, and confirm `SceneryItem.SvdRefs` values match observed `svd`
   symbol names

**Known test files**: `style.common.ovl`, `style.unique.ovl` (no SID or SVD entries present)

## Post-Implementation Steps

When this decoder is implemented, add a results summary under `.agents/summaries/` (see
`completed-work/flat-empty-park.md` for the current convention) and update this plan's status/README row.

### Future Work

- Full sound script parsing
- Flat ride animation references
- Export to human-readable format (JSON/XML)
- Visualize LOD switching distances
- Export mesh reference graph
