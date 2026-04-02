# Plan: Decode Integer (int) Entries

## Problem

Integer entries store raw integer values in OVL archives. These are simple numeric constants used by various game systems. The dumper should display these values alongside their symbol names.

## Background Research

**Integer entries**:
- Tag: `"int"`, FileType: `Integer`
- Simple 32-bit integer values stored directly in the data block
- No complex structures or additional metadata
- Referenced by symbol name like other OVL entries
- Likely used for game constants, configuration values, or numeric parameters

**Data Layout**:
- Loader data pointer → raw `uint32` or `int32` value
- Single integer per entry
- Stored in common OVL (like Text entries)

## Solution Architecture

### New File: `OpenCobra/OVL/Files/Integers.cs`

```csharp
public record IntegerEntry {
  string Name;    // symbol name
  int Value;      // integer value
}

public static class Integers {
  public static IReadOnlyList<IntegerEntry> Extract(Ovl ovl);
}
```

### Implementation Steps

1. Find loaders where `Tag == "int"`
2. Read 4 bytes from the loader's data address
3. Interpret as signed 32-bit integer
4. Return list of `IntegerEntry` (name + value)

### Files to Create/Modify

**Create:**
- `OpenCobra/OVL/Files/Integers.cs`

### Dependencies

- Existing loader/symbol infrastructure
- No external libraries needed

### Regression Prevention

- No changes to `Ovl.cs`
- New test file: `OpenCobra/Tests/TestRunner/Tests/DecodeIntegers.cs`
- Run TestRunner before/after implementation

### Testing Strategy (TestRunner)

Create new file `OpenCobra/Tests/TestRunner/Tests/DecodeIntegers.cs`:

```csharp
using System;
using System.Linq;
using OVL;
using OVL.Files;
using SysFile = System.IO.File;

namespace OvlTestBench.Tests;

public static class DecodeIntegers {
  public static readonly OvlTest Test = new("IntegerEntriesDecoded", pair => {
    foreach (var file in pair.Files) {
      try {
        using var stream = SysFile.OpenRead(file.Path);
        var ovl = Ovl.Read(stream, file.Path);
        var integers = Integers.Extract(ovl);
        if (ovl.LoaderEntries.Any(e => e.Tag == "int") && integers.Count == 0) {
          Assert.That(false, $"{System.IO.Path.GetFileName(file.Path)}: expected integers but got none");
        }
      } catch (Exception ex) {
        Assert.That(false, $"{System.IO.Path.GetFileName(file.Path)}: {ex.Message}");
      }
    }
  });
}
```

Register in `LoadOvls.All` array:
```csharp
DecodeIntegers.Test,
```

### Success Criteria

- All integer entries extracted with correct values
- Zero regressions

## Production OVLs with Entries

> **Status**: Not yet identified

Production OVL archives containing integer entries (tag: `"int"`) have not yet been catalogued. To identify:
1. Scan production OVLs for loader entries with `Tag == "int"`
2. Document common vs unique archive distribution
3. Note sample symbol names for verification

**Known test files**: `style.common.ovl`, `style.unique.ovl` (no INT entries present)

## Post-Implementation Steps (Completed)

1. ✅ **Created results file**: Added `.opencode/results/ovl-integers.md` with implementation summary
2. ✅ **Updated README**: Changed Status to `Done` in the Plans table and Summary Table
3. ✅ **Updated this plan**: Changed status in "Production OVLs with Entries" section
