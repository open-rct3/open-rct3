# Plan: Decode ManifoldMesh (MAM) Entries

## Problem

MAM entries store 3D manifold mesh data with vertices and triangle indices. These are raw geometry data structures, not directly viewable as images. The dumper should display mesh metadata and optionally export to a standard format.

## Background Research

**MAM Manager** (`ManagerMAM.h/cpp`):
- Tag: `"mam"`, Name: `"ManifoldMesh"`, Type: 4 (block 4)
- Each mesh = `ManifoldMesh` struct with:
  - `bbox_min`, `bbox_max` — bounding box vectors (each has position + unk04)
  - `vertex_count` — number of vertices
  - `mainfoldface_count` — number of triangle faces
  - `vertices[]` — array of `ManifoldMeshVertex`
  - `mainfoldface_indices[]` — array of `uint16_t` triangle indices
- Indices are padded to multiples of 8 for alignment
- All entries stored in common OVL only

**Data Layout**:
- Main data: `ManifoldMesh` struct with relocated `vertices` and `mainfoldface_indices` pointers
- Vertex data follows immediately after struct
- Index data follows vertex data (padded to 8-alignment)

## Solution Architecture

### New File: `OpenCobra/OVL/Files/ManifoldMeshes.cs`

```csharp
public record ManifoldMesh {
  string Name;
  Vector3 BoundingBoxMin;
  Vector3 BoundingBoxMax;
  IReadOnlyList<MeshVertex> Vertices;
  IReadOnlyList<ushort> Indices;  // triangle indices (groups of 3)
  int TriangleCount => Indices.Count / 3;
}

public static class ManifoldMeshes {
  public static IReadOnlyList<ManifoldMesh> Extract(Ovl ovl);
}
```

### Implementation Steps

1. Find loaders where `Tag == "mam"`
2. Parse `ManifoldMesh` struct from loader data
3. Read vertex array from relocated pointer
4. Read index array from relocated pointer (padded to 8-alignment)
5. Return list of `ManifoldMesh` with metadata

### Files to Create/Modify

**Create:**
- `OpenCobra/OVL/Files/ManifoldMeshes.cs`

### Dependencies

- Existing relocation resolution
- No external 3D libraries needed for parsing

### Regression Prevention

- No changes to `Ovl.cs`
- New test file: `OpenCobra/Tests/TestRunner/Tests/ReadManifoldMeshes.cs`
- Run TestRunner before/after implementation

### Testing Strategy (TestRunner)

Create new file `OpenCobra/Tests/TestRunner/Tests/ReadManifoldMeshes.cs`:

```csharp
using System;
using System.Linq;
using OVL;

namespace OvlTestBench.Tests;

public static class ReadManifoldMeshes {
  public static readonly OvlTest[] All = [
    new("ManifoldMeshEntriesDecoded", pair => {
      foreach (var file in pair.Files) {
        try {
          using var stream = System.IO.File.OpenRead(file.Path);
          var ovl = Ovl.Read(stream, file.Path);
          var meshes = ManifoldMeshes.Extract(ovl);
          if (ovl.LoaderEntries.Any(e => e.Tag == "mam") && meshes.Count == 0) {
            Assert.That(false, $"{System.IO.Path.GetFileName(file.Path)}: expected manifold meshes but got none");
          }
          foreach (var mesh in meshes) {
            Assert.That(mesh.Vertices.Count > 0, $"{System.IO.Path.GetFileName(file.Path)}: mesh '{mesh.Name}' has no vertices");
            Assert.That(mesh.TriangleCount > 0, $"{System.IO.Path.GetFileName(file.Path)}: mesh '{mesh.Name}' has no triangles");
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

- All MAM entries extracted with vertices and indices
- Bounding box values correct
- Index padding handled correctly
- Zero regressions

## Production OVLs with Entries

> **Status**: Not yet identified

Production OVL archives containing manifold mesh entries (tag: `"mam"`) have not yet been catalogued. To identify:
1. Scan production OVLs for loader entries with `Tag == "mam"`
2. Document common vs unique archive distribution
3. Note sample symbol names for verification

**Known test files**: `style.common.ovl`, `style.unique.ovl` (no MAM entries present)

## Post-Implementation Steps

When this decoder is implemented:

1. **Create results file**: Add `.opencode/results/ovl-manifold-meshes.md` with implementation summary
2. **Update README**: Change Status to `Done` in the Plans table
3. **Update this plan**: Change status in "Production OVLs with Entries" section
