# Plan: Decode Spline (SPL) Entries

## Problem

SPL entries store cubic Bézier spline curves with control points, segment lengths, and pre-computed interpolation data. These define paths for rides, queues, and other game objects. The dumper should display spline metadata and optionally visualize the curve.

## Background Research

**SPL Manager** (`ManagerSPL.h/cpp`):
- Tag: `"spl"`, Loader: `"FGDK"`, Name: `"Spline"`
- Each spline = `Spline` struct with:
  - `nodecount` — number of spline nodes
  - `cyclic` — 0 = open, 1 = closed loop
  - `totallength` — total curve length
  - `inv_totallength` — 1 / totallength
  - `max_y` — maximum Y coordinate
  - `nodes[]` — array of `SplineNode` (relocated)
  - `lengths[]` — array of segment lengths (relocated)
  - `datas[]` — array of `SplineData` (14 bytes each, relocated)
- Each `SplineNode` contains:
  - `pos` — position vector (x, y, z)
  - `cp1` — control point 1 (incoming tangent)
  - `cp2` — control point 2 (outgoing tangent)
- `SplineData`: 14-byte array with pre-computed Bézier interpolation values
- Segment lengths calculated using De Casteljau algorithm with 15 subdivisions
- All entries stored in common OVL only

**Bézier Curve Calculation**:
- Each segment is a cubic Bézier curve: P1 → CP2 → CP1 → P2
- Length calculated with 15-point De Casteljau subdivision
- `SplineData` stores pre-computed distance markers for interpolation

**Data Layout**:
- Main data: `Spline` struct → `SplineNode[]` → `lengths[]` → `datas[]`
- All arrays are relocated pointers within the same block

## Solution Architecture

### New File: `OpenCobra/OVL/Files/Splines.cs`

```csharp
public record SplineNode {
  Vector3 Position;
  Vector3 ControlPoint1;  // incoming tangent
  Vector3 ControlPoint2;  // outgoing tangent
}

public record SplineEntry {
  string Name;
  bool IsCyclic;
  float TotalLength;
  float MaxY;
  IReadOnlyList<SplineNode> Nodes;
  IReadOnlyList<float> SegmentLengths;
  IReadOnlyList<SplineData> DataItems;  // 14 bytes each
  int SegmentCount => Nodes.Count - (IsCyclic ? 0 : 1);
}

public static class Splines {
  public static IReadOnlyList<SplineEntry> Extract(Ovl ovl);
}
```

### Implementation Steps

1. Find loaders where `Tag == "spl"`
2. Parse `Spline` struct from loader data
3. Read nodes array from relocated pointer
4. Read lengths array from relocated pointer
5. Read datas array from relocated pointer (14 bytes per item)
6. Return list of `SplineEntry`

### Files to Create/Modify

**Create:**
- `OpenCobra/OVL/Files/Splines.cs`

### Dependencies

- Existing relocation resolution

### Regression Prevention

- No changes to `Ovl.cs`
- New test file: `OpenCobra/OVL Tests/ReadSplines.cs`
- Run existing tests before/after

### Testing Strategy

```csharp
[Test] void Extract_StyleCommonOvl_ReturnsSplines()
[Test] void Extract_Spline_HasCorrectNodeCount()
[Test] void Extract_Spline_CyclicFlagIsCorrect()
[Test] void Extract_Spline_TotalLengthMatchesSegments()
[Test] void Extract_Spline_DataItemsAre14Bytes()
[Test] void Extract_Spline_MaxYIsCorrect()
```

### Success Criteria

- All SPL entries extracted with nodes, lengths, and data items
- Cyclic/open splines correctly identified
- Segment lengths sum to total length
- Zero regressions

### Future Work

- Spline curve visualization in dumper
- Export to standard spline format
- Calculate points along curve using pre-computed data
