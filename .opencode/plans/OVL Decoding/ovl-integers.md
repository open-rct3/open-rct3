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
- New test file: `OpenCobra/OVL Tests/ReadIntegers.cs`
- Run existing tests before/after

### Testing Strategy

```csharp
[Test] void Extract_StyleCommonOvl_ReturnsIntegers()
[Test] void Extract_IntegerValue_IsCorrect()
[Test] void Extract_EmptyOvl_ReturnsEmptyList()
```

### Success Criteria

- All integer entries extracted with correct values
- Zero regressions
