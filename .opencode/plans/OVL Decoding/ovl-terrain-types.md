# Plan: Decode TerrainType (TER) Entries

## Problem

TER entries define terrain types with color parameters, texture references, and display metadata. Each terrain type has a description name, icon, texture reference, and rendering parameters. The dumper should display terrain metadata and optionally preview the terrain appearance.

## Background Research

**TER Manager** (`ManagerTER.h/cpp`):
- Tag: `"ter"`, Name: `"TerrainType"`, stored in **unique OVL only**
- Each terrain = `TerrainType` struct with:
  - `name` — terrain name (symbol reference)
  - `description_name` — description text reference (TXT)
  - `icon_name` — icon reference (GSI)
  - `texture` — texture reference (TEX)
  - `version` — structure version
  - `addon` — addon pack flag
  - `number` — terrain number
  - `type` — terrain type
  - **Parameters**:
    - `colour01` — primary color (default 0xFFFF007F)
    - `colour02` — secondary color (default 0xFFFF007F)
    - `inv_width` — inverse width (default 0.1)
    - `inv_height` — inverse height (default 0.1)
  - **Unknowns**:
    - `unk02` — unknown (default 0)
    - `unk13` — unknown (default 0.3)
    - `unk14` — unknown (default 0.0)
    - `unk15` — unknown (default 0.5)

**Data Layout**:
- Unique block: `TerrainType` struct with symbol references
- Symbol references to: TXT (description), GSI (icon), TEX (texture)
- String table entries for name, description, icon, texture

## Solution Architecture

### New File: `OpenCobra/OVL/Files/TerrainTypes.cs`

```csharp
public record TerrainParameters {
  uint Color01;       // default 0xFFFF007F
  uint Color02;       // default 0xFFFF007F
  float InvWidth;     // default 0.1
  float InvHeight;    // default 0.1
}

public record TerrainUnknowns {
  uint Unk02;         // default 0
  float Unk13;        // default 0.3
  float Unk14;        // default 0.0
  float Unk15;        // default 0.5
}

public record TerrainType {
  string Name;
  string DescriptionName;  // TXT reference
  string IconName;         // GSI reference
  string TextureRef;       // TEX reference
  uint Version;
  uint Addon;
  uint Number;
  uint Type;
  TerrainParameters Parameters;
  TerrainUnknowns Unknowns;
}

public static class TerrainTypes {
  public static IReadOnlyList<TerrainType> Extract(Ovl ovl);
}
```

### Implementation Steps

1. Find loaders where `Tag == "ter"` (unique OVL only)
2. Parse `TerrainType` struct from loader data
3. Resolve symbol references to TXT, GSI, TEX
4. Read parameters and unknowns
5. Return list of `TerrainType`

### Files to Create/Modify

**Create:**
- `OpenCobra/OVL/Files/TerrainTypes.cs`

### Dependencies

- Existing relocation resolution
- Symbol reference resolution for TXT, GSI, TEX

### Regression Prevention

- No changes to `Ovl.cs`
- New test file: `OpenCobra/Tests/TestRunner/Tests/ReadTerrainTypes.cs`
- Run TestRunner before/after implementation

### Testing Strategy (TestRunner)

Create new file `OpenCobra/Tests/TestRunner/Tests/ReadTerrainTypes.cs`:

```csharp
using System;
using System.Linq;
using OVL;

namespace OvlTestBench.Tests;

public static class ReadTerrainTypes {
  public static readonly OvlTest[] All = [
    new("TerrainTypeEntriesDecoded", pair => {
      foreach (var file in pair.Files) {
        if (file.Type != OvlType.Unique) continue;  // TER is unique-only
        try {
          using var stream = System.IO.File.OpenRead(file.Path);
          var ovl = Ovl.Read(stream, file.Path);
          var terrains = TerrainTypes.Extract(ovl);
          if (ovl.LoaderEntries.Any(e => e.Tag == "ter") && terrains.Count == 0) {
            Assert.That(false, $"{System.IO.Path.GetFileName(file.Path)}: expected terrain types but got none");
          }
          foreach (var terrain in terrains) {
            Assert.That(!string.IsNullOrEmpty(terrain.Name), $"{System.IO.Path.GetFileName(file.Path)}: terrain has empty name");
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

- All TER entries extracted with full metadata
- Symbol references to TXT/GSI/TEX resolved
- Parameters and unknowns parsed correctly
- Zero regressions

## Production OVLs with Entries

> **Status**: Not yet identified

Production OVL archives containing terrain type entries (tag: `"ter"`) have not yet been catalogued. To identify:
1. Scan production OVLs for loader entries with `Tag == "ter"` (unique OVL only)
2. Document common vs unique archive distribution
3. Note sample symbol names for verification

**Known test files**: `style.common.ovl`, `style.unique.ovl` (no TER entries present)

## Post-Implementation Steps

When this decoder is implemented:

1. **Create results file**: Add `.opencode/results/ovl-terrain-types.md` with implementation summary
2. **Update README**: Change Status to `Done` in the Plans table and Summary Table
3. **Update this plan**: Change status in "Production OVLs with Entries" section

### Future Work

- Link terrain textures to decoded Bitmap instances (from Textures.cs)
- Preview terrain appearance using texture + colors
- Export terrain definitions
