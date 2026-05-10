# Plan: Decode SceneryItem (SID) Entries

## Problem

SID entries are the most complex OVL file type — they define all placeable scenery objects (rides, stalls, decorations, etc.) with UI metadata, positioning rules, colors, sounds, and references to visual definitions (SVD). The dumper should display comprehensive scenery item metadata.

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

### Testing Strategy (TestRunner)

Create new file `OpenCobra/Tests/TestRunner/Tests/ReadSceneryItems.cs`:

```csharp
using System;
using System.Linq;
using OVL;

namespace OvlTestBench.Tests;

public static class ReadSceneryItems {
  public static readonly OvlTest[] All = [
    new("SceneryItemEntriesDecoded", pair => {
      foreach (var file in pair.Files) {
        if (file.Type != OvlType.Unique) continue;  // SID is unique-only
        try {
          using var stream = System.IO.File.OpenRead(file.Path);
          var ovl = Ovl.Read(stream, file.Path);
          var items = SceneryItems.Extract(ovl);
          if (ovl.LoaderEntries.Any(e => e.Tag == "sid") && items.Count == 0) {
            Assert.That(false, $"{System.IO.Path.GetFileName(file.Path)}: expected scenery items but got none");
          }
          foreach (var item in items) {
            Assert.That(!string.IsNullOrEmpty(item.UI.Name), $"{System.IO.Path.GetFileName(file.Path)}: scenery item has empty name");
          }
        } catch (Exception ex) {
          Assert.That(false, $"{System.IO.Path.GetFileName(file.Path)}: {ex.Message}");
        }
      }
    }),
  ];
}
```

Add to `LoadOvls.All` array or create as separate test file following the existing pattern.

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

When this decoder is implemented:

1. **Create results file**: Add `.opencode/results/ovl-scenery-items.md` with implementation summary
2. **Update README**: Change Status to `Done` in the Plans table and Summary Table
3. **Update this plan**: Change status in "Production OVLs with Entries" section

### Future Work

- Full sound script parsing
- Flat ride animation references
- Export to human-readable format (JSON/XML)
