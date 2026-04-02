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
- New test file: `OpenCobra/OVL Tests/ReadManifoldMeshes.cs`
- Run existing tests before/after

### Testing Strategy

```csharp
[Test] void Extract_StyleCommonOvl_ReturnsMeshes()
[Test] void Extract_Mesh_HasValidBoundingBox()
[Test] void Extract_Mesh_VerticesCountMatches()
[Test] void Extract_Mesh_IndicesArePaddedTo8()
[Test] void Extract_Mesh_TriangleCountIsCorrect()
```

### Success Criteria

- All MAM entries extracted with vertices and indices
- Bounding box values correct
- Index padding handled correctly
- Zero regressions
