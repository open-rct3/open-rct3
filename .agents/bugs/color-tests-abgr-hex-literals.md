# Bug: Inverted ABGR Hex Color Literals in ColorTests.cs

## Overview

A failing unit test exists in [ColorTests.cs](file:///d:/Users/enigm/GitHub/open-rct3/OpenCobra/Tests/Numerics/ColorTests.cs) under method `BlendOverZeroAlpha`.
The test expects `BlendOver` to produce `r = 0x00` and `b = 0xFF`, but instead receives `r = 255` and `b = 0`.

## Root Cause

[`OpenCobra.GDK.Numerics.Color.BlendOver`](file:///d:/Users/enigm/GitHub/open-rct3/OpenCobra/GDK/Numerics/Color.cs) operates on ImGui packed ABGR `uint` values:
- Channel 0 (bits 0..7): Red (`color & 0xFF`)
- Channel 1 (bits 8..15): Green (`(color >> 8) & 0xFF`)
- Channel 2 (bits 16..23): Blue (`(color >> 16) & 0xFF`)
- Channel 3 (bits 24..31): Alpha (`(color >> 24) & 0xFF`)

In [ColorTests.cs](file:///d:/Users/enigm/GitHub/open-rct3/OpenCobra/Tests/Numerics/ColorTests.cs), the test cases were authored using ARGB hex notation instead of ABGR:
- `0x00FF0000u` was written intended as 0% alpha red, but in ABGR it represents 0% alpha blue.
- `0xFF0000FFu` was written intended as opaque blue, but in ABGR it represents opaque red (`R=255, G=0, B=0, A=255`).
- Opaque blue in ABGR format is `0xFFFF0000u` (`B=255, A=255`).
- 0% alpha red in ABGR format is `0x000000FFu` (`R=255, A=0`).
- 50% alpha red in ABGR format is `0x800000FFu` (`R=255, A=128`).

When `BlendOver` processes a foreground with alpha 0 over background `0xFF0000FFu`, the resulting color is the background unchanged (`0xFF0000FFu`), which has `r = 255` and `b = 0`. The test assertions checked for `r == 0x00` and `b == 0xFF`, causing the failure.

## File Tool Failure

`view_file`, `replace_file_content`, and `write_to_file` fail on [ColorTests.cs](file:///d:/Users/enigm/GitHub/open-rct3/OpenCobra/Tests/Numerics/ColorTests.cs) with the error:
```
while decoding file, failed to detect charset with sufficient confidence
```
The file contains a byte sequence that triggers this charset detection error in the internal tool parser.

## Fix Instructions

In [ColorTests.cs](file:///d:/Users/enigm/GitHub/open-rct3/OpenCobra/Tests/Numerics/ColorTests.cs), update the hex literals in the following three test methods:

### 1. `BlendOverAlphaComposite`
Replace:
```csharp
var foreground = 0x80FF0000u; // 50% red
var background = 0xFF0000FFu; // opaque blue
```
With:
```csharp
var foreground = 0x800000FFu; // 50% red (ABGR)
var background = 0xFFFF0000u; // opaque blue (ABGR)
```

### 2. `BlendOverZeroAlpha`
Replace:
```csharp
var transparent = 0x00FF0000u; // 0% alpha red
var background = 0xFF0000FFu; // opaque blue
```
With:
```csharp
var transparent = 0x000000FFu; // 0% alpha red (ABGR)
var background = 0xFFFF0000u; // opaque blue (ABGR)
```

### 3. `BlendOverRgbChannels`
Replace:
```csharp
var foreground = 0x80FF0000u; // 50% red (R=255, G=0, B=0, A=128)
var background = 0xFF0000FFu; // opaque blue (R=0, G=0, B=255, A=255)
```
With:
```csharp
var foreground = 0x800000FFu; // 50% red (R=255, G=0, B=0, A=128 in ABGR)
var background = 0xFFFF0000u; // opaque blue (R=0, G=0, B=255, A=255 in ABGR)
```

## Verification

After editing, run:
```bash
make test
```
All 452 tests in `Tests.dll` and 148 tests in `OpenRCT3.Tests.dll` should pass.
