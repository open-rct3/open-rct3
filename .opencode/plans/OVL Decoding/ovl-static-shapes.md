# Plan: Decode StaticShape (SHS) Entries

## Problem

SHS entries store static 3D shapes with multiple meshes, vertices, indices, texture references, and effect positions. These are complex geometry structures with symbol references to FTX (FlexiTexture) and TXS (Texture Style). The dumper should display shape metadata and mesh information.

## Background Research

**SHS Manager** (`ManagerSHS.h/cpp`):
- Tag: `"shs"`, Name: `"StaticShape"`, Type: 4 (block 4)
- Each shape = `StaticShape` (level 1) containing:
  - `bounding_box_min`, `bounding_box_max`
  - `mesh_count`, `mesh_count2`
  - `sh[]` — array of `StaticShapeMesh*` pointers (relocated)
  - `effect_count`, `effect_names[]`, `effect_positions[]`
- Each mesh = `StaticShapeMesh` (level 2) containing:
  - `support_type` — mesh support type enum
  - `ftx_ref` — symbol ref to FlexiTexture (relocated)
  - `txs_ref` — symbol ref to Texture Style (relocated)
  - `transparency`, `texture_flags`, `sides`
  - `vertex_count`, `index_count`
  - `vertexes[]` — array of `VERTEX` (relocated)
  - `indices[]` — array of `uint32_t` (relocated)
- Effects: name strings + `MATRIX` position transforms
- **Unique OVL only** (not common)

**Data Layout**:
- Unique block: `StaticShape` struct → `StaticShapeMesh*[]` → each mesh struct
- Common block (per model): vertex arrays → index arrays → effect name pointers → effect name strings → effect position matrices
- Triangle sorting algorithms for placement texturing (x, y, z axes)

**VERTEX Structure**:
- `position` (x, y, z floats)
- `normal` (x, y, z floats)
- `texcoord` (u, v floats)
- Additional fields per vertex

## Solution Architecture

### New File: `OpenCobra/OVL/Files/StaticShapes.cs`

```csharp
public record StaticShapeMesh {
  string Name;
  int SupportType;
  string? FtxRef;       // FlexiTexture symbol name
  string? TxsRef;       // Texture Style symbol name
  uint Transparency;
  uint TextureFlags;
  uint Sides;
  IReadOnlyList<Vertex> Vertices;
  IReadOnlyList<uint> Indices;
  int TriangleCount => Indices.Count / 3;
}

public record StaticShape {
  string Name;
  Vector3 BoundingBoxMin;
  Vector3 BoundingBoxMax;
  IReadOnlyList<StaticShapeMesh> Meshes;
  IReadOnlyList<ShapeEffect> Effects;
}

public record ShapeEffect {
  string Name;
  Matrix4x4 Position;
}

public static class StaticShapes {
  public static IReadOnlyList<StaticShape> Extract(Ovl ovl);
}
```

### Implementation Steps

1. Find loaders where `Tag == "shs"` (unique OVL only)
2. Parse `StaticShape` struct from loader data
3. Follow relocated `sh[]` pointer array to each `StaticShapeMesh`
4. For each mesh:
   - Read vertex array from common block relocated pointer
   - Read index array from common block relocated pointer
   - Resolve `ftx_ref` and `txs_ref` symbol references
5. Parse effect names and position matrices from common block
6. Return list of `StaticShape` with all mesh data

### Files to Create/Modify

**Create:**
- `OpenCobra/OVL/Files/StaticShapes.cs`

### Dependencies

- Existing relocation resolution
- Symbol reference resolution for FTX/TXS

### Regression Prevention

- No changes to `Ovl.cs`
- New test file: `OpenCobra/Tests/TestRunner/Tests/ReadStaticShapes.cs`
- Run TestRunner before/after implementation

### Testing Strategy (TestRunner)

Create new file `OpenCobra/Tests/TestRunner/Tests/ReadStaticShapes.cs`:

```csharp
using System;
using System.Linq;
using OVL;

namespace OvlTestBench.Tests;

public static class ReadStaticShapes {
  public static readonly OvlTest[] All = [
    new("StaticShapeEntriesDecoded", pair => {
      foreach (var file in pair.Files) {
        if (file.Type != OvlType.Unique) continue;  // SHS is unique-only
        try {
          using var stream = System.IO.File.OpenRead(file.Path);
          var ovl = Ovl.Read(stream, file.Path);
          var shapes = StaticShapes.Extract(ovl);
          if (ovl.LoaderEntries.Any(e => e.Tag == "shs") && shapes.Count == 0) {
            Assert.That(false, $"{System.IO.Path.GetFileName(file.Path)}: expected shapes but got none");
          }
          foreach (var shape in shapes) {
            Assert.That(shape.Meshes.Count > 0, $"{System.IO.Path.GetFileName(file.Path)}: shape '{shape.Name}' has no meshes");
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

- All SHS entries extracted with meshes, vertices, and indices
- Symbol references to FTX/TXS resolved correctly
- Effect positions parsed
- Zero regressions

## Production OVLs with Entries

> **Status**: Not yet identified

Production OVL archives containing static shape entries (tag: `"shs"`) have not yet been catalogued. To identify:
1. Scan production OVLs for loader entries with `Tag == "shs"` (unique OVL only)
2. Document common vs unique archive distribution
3. Note sample symbol names for verification

**Known test files**: `style.common.ovl`, `style.unique.ovl` (no SHS entries present)

## Post-Implementation Steps

When this decoder is implemented:

1. **Create results file**: Add `.opencode/results/ovl-static-shapes.md` with implementation summary
2. **Update README**: Change Status to `Done` in the Plans table and Summary Table
3. **Update this plan**: Change status in "Production OVLs with Entries" section
