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
- New test file: `OpenCobra/OVL Tests/ReadTexts.cs`
- Run existing tests before/after

### Testing Strategy

```csharp
[Test] void Extract_StyleCommonOvl_ReturnsTexts()
[Test] void Extract_TextValue_IsValidUtf16()
[Test] void Extract_EmptyOvl_ReturnsEmptyList()
```

### Success Criteria

- All TXT entries extracted as readable strings
- UTF-16LE decoding correct for all characters
- Zero regressions
