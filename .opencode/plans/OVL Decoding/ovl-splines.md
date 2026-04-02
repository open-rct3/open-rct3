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
- New test file: `OpenCobra/Tests/TestRunner/Tests/ReadSplines.cs`
- Run TestRunner before/after implementation

### Testing Strategy (TestRunner)

Create new file `OpenCobra/Tests/TestRunner/Tests/ReadSplines.cs`:

```csharp
using System;
using System.Linq;
using OVL;

namespace OvlTestBench.Tests;

public static class ReadSplines {
  public static readonly OvlTest[] All = [
    new("SplineEntriesDecoded", pair => {
      foreach (var file in pair.Files) {
        try {
          using var stream = System.IO.File.OpenRead(file.Path);
          var ovl = Ovl.Read(stream, file.Path);
          var splines = Splines.Extract(ovl);
          if (ovl.LoaderEntries.Any(e => e.Tag == "spl") && splines.Count == 0) {
            Assert.That(false, $"{System.IO.Path.GetFileName(file.Path)}: expected splines but got none");
          }
          foreach (var s in splines) {
            Assert.That(s.Nodes.Count >= 2, $"{System.IO.Path.GetFileName(file.Path)}: spline '{s.Name}' has fewer than 2 nodes");
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

- All SPL entries extracted with nodes, lengths, and data items
- Cyclic/open splines correctly identified
- Segment lengths sum to total length
- Zero regressions

## Production OVLs with Entries

> **Status**: Not yet identified

Production OVL archives containing spline entries (tag: `"spl"`) have not yet been catalogued. To identify:
1. Scan production OVLs for loader entries with `Tag == "spl"`
2. Document common vs unique archive distribution
3. Note sample symbol names for verification

**Known test files**: `style.common.ovl`, `style.unique.ovl` (no SPL entries present)

## Post-Implementation Steps

When this decoder is implemented:

1. **Create results file**: Add `.opencode/results/ovl-splines.md` with implementation summary
2. **Update README**: Change Status to `Done` in the Plans table and Summary Table
3. **Update this plan**: Change status in "Production OVLs with Entries" section

### Future Work

- Spline curve visualization in dumper
- Export to standard spline format
- Calculate points along curve using pre-computed data
