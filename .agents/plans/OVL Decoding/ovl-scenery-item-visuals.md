# Plan: Decode SceneryItemVisual (SVD) Entries

## Problem

SVD entries define the visual representation of scenery items with multiple LOD (Level of Detail) models. Each SVD references StaticShape, BoneShape, or Billboard meshes with distance-based LOD switching and animation references. The dumper should display visual metadata and LOD structure.

## Background Research

**SVD Manager** (`ManagerSVD.h/cpp`):
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

**Data Layout**:
- Unique block: `SceneryItemVisual` struct → LOD pointer array → LOD structs
- Common block: animation name arrays → LOD model references
- Symbol references to: SHS (StaticShape), BSH (BoneShape), FTX (FlexiTexture), TXS (Texture Style), BAN (BoneAnim), MAM (ManifoldMesh)

## Solution Architecture

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

1. Find loaders where `Tag == "svd"` (unique OVL only)
2. Parse `SceneryItemVisual` struct from loader data
3. Read LOD array from relocated pointers
4. For each LOD:
   - Determine mesh type and extract appropriate reference
   - Read animation references (for BoneShape meshes)
   - Read billboard references (fts/txs for Billboard meshes)
5. Resolve symbol references to SHS, BSH, FTX, TXS, BAN, MAM
6. Return list of `SceneryItemVisual`

### Files to Create/Modify

**Create:**
- `OpenCobra/OVL/Files/SceneryItemVisuals.cs`

### Dependencies

- Existing relocation resolution
- Symbol reference resolution for SHS, BSH, FTX, TXS, BAN, MAM

### Regression Prevention

- No changes to `Ovl.cs`
- New test file: `OpenCobra/Tests/TestRunner/Tests/ReadSceneryItemVisuals.cs`
- Run TestRunner before/after implementation

### Testing Strategy (TestRunner)

Create new file `OpenCobra/Tests/TestRunner/Tests/ReadSceneryItemVisuals.cs`:

```csharp
using System;
using System.Linq;
using OVL;

namespace OvlTestBench.Tests;

public static class ReadSceneryItemVisuals {
  public static readonly OvlTest[] All = [
    new("SceneryItemVisualEntriesDecoded", pair => {
      foreach (var file in pair.Files) {
        if (file.Type != OvlType.Unique) continue;  // SVD is unique-only
        try {
          using var stream = System.IO.File.OpenRead(file.Path);
          var ovl = Ovl.Read(stream, file.Path);
          var visuals = SceneryItemVisuals.Extract(ovl);
          if (ovl.LoaderEntries.Any(e => e.Tag == "svd") && visuals.Count == 0) {
            Assert.That(false, $"{System.IO.Path.GetFileName(file.Path)}: expected scenery visuals but got none");
          }
          foreach (var visual in visuals) {
            Assert.That(visual.Lods.Count > 0, $"{System.IO.Path.GetFileName(file.Path)}: visual '{visual.Name}' has no LODs");
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

- All SVD entries extracted with LOD structure
- Mesh types correctly identified (StaticShape/BoneShape/Billboard)
- Symbol references resolved for all mesh types
- LOD distance thresholds parsed
- Zero regressions

## Production OVLs with Entries

> **Status**: Not yet identified

Production OVL archives containing scenery item visual entries (tag: `"svd"`) have not yet been catalogued. To identify:
1. Scan production OVLs for loader entries with `Tag == "svd"` (unique OVL only)
2. Document common vs unique archive distribution
3. Note sample symbol names for verification

**Known test files**: `style.common.ovl`, `style.unique.ovl` (no SVD entries present)

## Post-Implementation Steps

When this decoder is implemented:

1. **Create results file**: Add `.opencode/results/ovl-scenery-item-visuals.md` with implementation summary
2. **Update README**: Change Status to `Done` in the Plans table
3. **Update this plan**: Change status in "Production OVLs with Entries" section

### Future Work

- Link SVD entries to their parent SID entries
- Visualize LOD switching distances
- Export mesh reference graph
