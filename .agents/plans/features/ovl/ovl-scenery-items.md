# Plan: Decode SceneryItem (SID) Entries

**See also**: [`features/scenery-placement-registry.md`](../scenery-placement-registry.md) — confirms (against
`rct3-importer`'s `scenery.h`) that `sizeflag`, the field driving placement footprint/height-sampling
(`SIZE_FULLTILE` etc., mapped there to a `Placement` enum), lives on this `sid` struct, not on `svd`. That
plan's `Placement`/`AnimationKind` design is what this decoder's output will eventually feed; this plan should
add a `SizeFlag`/`Placement`-shaped field to `SceneryItem` (or `SceneryItemPosition`) once implemented, rather
than leaving placement-shape data out of the record below as currently drafted.

## Problem

SID entries are the most complex OVL file type — they define all placeable scenery objects (rides, stalls, decorations,
etc.) with UI metadata, positioning rules, colors, sounds, and references to visual definitions (SVD). The dumper should
display comprehensive scenery item metadata.

## Background Research

**SID Manager** (`ManagerSID.h/cpp`):

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

**Data Layout**:

- Unique block: main `SceneryItem` struct → SVD pointer array → sound array
- Common block: `SceneryItemData[]` → height bitmaps → `SceneryParams[]` → sound script data → animation name pointers
- Extra data for v1/v2: `SceneryExtraSound[]`

**Complexity Notes**:

- 563 lines in ManagerSID.cpp — most complex manager
- Multiple unknown fields (40+ across all structs)
- Conditional size calculations based on version, addon pack, tile count
- Sound scripts with variable-size commands (8 or 16 bytes)

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

### Implementation Steps

1. Find loaders where `Tag == "sid"` (unique OVL only)
2. Determine struct version from extra.version field
3. Parse main `SceneryItem` struct (version-dependent size)
4. Read SVD pointer array and resolve symbol references
5. Read common data: `SceneryItemData[]` (per-tile), height bitmaps
6. Read sound array with SND symbol references
7. Read parameters array
8. Return list of `SceneryItem`

### Files to Create/Modify

**Create:**

- `OpenCobra/OVL/Files/SceneryItems.cs`

### Dependencies

- Existing relocation resolution
- Symbol reference resolution for TXT, GSI, SVD, SND

### Regression Prevention

- No changes to `Ovl.cs`
- New test file: `OpenCobra/Tests/TestRunner/Tests/ReadSceneryItems.cs`
- Run TestRunner before/after implementation

### Testing Strategy

The `TestRunner`/`OvlTest[]` pattern this section originally described no longer exists in the codebase.
Current convention: NUnit tests in `OpenCobra/Tests/OVL/SceneryItemsTests.cs`, plus a real-archive check in
`OpenCobra/Tests/Integration/ExtractResources.cs` gated by `RCT3_PATH` — see `ovl-materials-integration.md`'s
test plan for a live example. Cover: synthetic-struct decode per version (v0/v1/v2), `sizeflag` resolving to
one of the 9 `Placement` values from `scenery-placement-registry.md`, and — against real data — that every
`sid`-tagged symbol (unique OVL only) decodes with a non-empty UI name.

### Success Criteria

- All SID entries extracted with full metadata
- Version-dependent struct parsing correct (v0, v1, v2)
- Symbol references to SVD/SND/TXT/GSI resolved
- Tile data parsed correctly
- Zero regressions

## Production OVLs with Entries

> **Status**: Not yet identified

Production OVL archives containing scenery item entries (tag: `"sid"`) have not yet been catalogued. To identify:

1. Scan production OVLs for loader entries with `Tag == "sid"` (unique OVL only)
2. Document common vs unique archive distribution
3. Note sample symbol names for verification

**Known test files**: `style.common.ovl`, `style.unique.ovl` (no SID entries present)

## Post-Implementation Steps

When this decoder is implemented, add a results summary under `.agents/summaries/` (see
`completed-work/flat-empty-park.md` for the current convention) and update this plan's status/README row.

### Future Work

- Full sound script parsing
- Flat ride animation references
- Export to human-readable format (JSON/XML)
