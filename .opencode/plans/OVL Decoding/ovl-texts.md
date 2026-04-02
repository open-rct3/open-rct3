# Plan: Decode FileType.Text (TXT) Entries

## Problem

TXT entries store localized text strings in OVL archives. They are stored as null-terminated UTF-16LE (wide char) strings. The dumper needs to display these strings in a text viewer.

## Background Research

**TXT Manager** (`ManagerTXT.h/cpp`):
- Tag: `"txt"`, Loader: `"FGDK"`, Type: 2
- Data is stored directly as `wchar_t*` (UTF-16LE on Windows)
- Size calculation: `(wcslen(str) + 1) * 2` bytes
- First TXT entry has extra data chunk: `uint = 1` + V5INFO_1
- All entries stored in common OVL only

**Data Layout**:
- Loader data pointer → raw `wchar_t[]` string (null-terminated)
- No additional structures or headers
- Multiple strings concatenated in block 2

## Solution Architecture

### New File: `OpenCobra/OVL/Files/Texts.cs`

```csharp
public record TextEntry {
  string Name;        // symbol name
  string Value;       // decoded UTF-16LE string
}

public static class Texts {
  public static IReadOnlyList<TextEntry> Extract(Ovl ovl);
}
```

### Implementation Steps

1. Find loaders where `Tag == "txt"`
2. Read raw bytes from the loader's data address
3. Decode as UTF-16LE until null terminator
4. Return list of `TextEntry` (name + value)

### Files to Create/Modify

**Create:**
- `OpenCobra/OVL/Files/Texts.cs`

### Dependencies

- `System.Text.Encoding.Unicode` for UTF-16LE decoding
- Existing loader/symbol infrastructure

### Regression Prevention

- No changes to `Ovl.cs`
- New test file: `OpenCobra/Tests/TestRunner/Tests/ReadTexts.cs`
- Run TestRunner before/after implementation

### Testing Strategy (TestRunner)

Create new file `OpenCobra/Tests/TestRunner/Tests/ReadTexts.cs`:

```csharp
using System;
using System.Linq;
using OVL;

namespace OvlTestBench.Tests;

public static class ReadTexts {
  public static readonly OvlTest[] All = [
    new("TextEntriesDecoded", pair => {
      foreach (var file in pair.Files) {
        try {
          using var stream = System.IO.File.OpenRead(file.Path);
          var ovl = Ovl.Read(stream, file.Path);
          var texts = Texts.Extract(ovl);
          if (ovl.LoaderEntries.Any(e => e.Tag == "txt") && texts.Count == 0) {
            Assert.That(false, $"{System.IO.Path.GetFileName(file.Path)}: expected texts but got none");
          }
          foreach (var t in texts) {
            Assert.That(!string.IsNullOrEmpty(t.Value), $"{System.IO.Path.GetFileName(file.Path)}: text '{t.Name}' is empty");
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

- All TXT entries extracted as readable strings
- UTF-16LE decoding correct for all characters
- Zero regressions

## Production OVLs with Entries

> **Status**: Not yet identified

Production OVL archives containing text entries (tag: `"txt"`) have not yet been catalogued. To identify:
1. Scan production OVLs for loader entries with `Tag == "txt"`
2. Document common vs unique archive distribution
3. Note sample symbol names for verification

**Known test files**: `style.common.ovl`, `style.unique.ovl` (no TXT entries present)

## Post-Implementation Steps

When this decoder is implemented:

1. **Create results file**: Add `.opencode/results/ovl-texts.md` with implementation summary
2. **Update README**: Change Status to `Done` in the Plans table and Summary Table
3. **Update this plan**: Change status in "Production OVLs with Entries" section
