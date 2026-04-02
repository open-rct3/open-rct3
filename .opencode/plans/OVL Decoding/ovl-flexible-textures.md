# Plan: Decode FlexibleTexture (FTX) Entries

## Problem

FTX entries store palette-based animated textures with optional alpha channels. Each FTX contains one or more frames, each with a 256-entry color palette, pixel index data, and optional alpha data. The dumper needs to display these as images.

## Background Research

**FTX Manager** (`ManagerFTX.h/cpp`):
- Tag: `"ftx"`, Loader: `"FGDK"`, Name: `"FlexiTexture"`
- Each FTX = `FlexiTextureInfoStruct` with animation offsets + frame array
- Each frame = `FlexiTextureStruct` with:
  - `scale` (log2 of dimension)
  - `width`, `height` (always square, power of 2)
  - `Recolorable` flag
  - `palette[256]` — array of `COLOURQUAD` (RGBA)
  - `texture[]` — 1 byte per pixel (palette index)
  - `alpha[]` — optional, 1 byte per pixel (0-255)
- Animations: array of offset indices into frames
- Multiple texture/alpha blobs per frame, stored in separate memory blobs

**Struct Sizes**:
- `FlexiTextureInfoStruct`: variable (header + animation offsets + frame count + frames)
- `FlexiTextureStruct`: 256 * sizeof(COLOURQUAD) + dimension*dimension (texture) + optional dimension*dimension (alpha)
- `COLOURQUAD`: 4 bytes (RGBA)

**Data Layout**:
- Main data: `FlexiTextureInfoStruct` with relocated `offset1` and `fts2` pointers
- Per-frame palette: separate blob, relocated
- Per-frame texture: separate blob in block 0, relocated
- Per-frame alpha: separate blob in block 0 (if present), relocated

## Solution Architecture

### New File: `OpenCobra/OVL/Files/FlexibleTextures.cs`

```csharp
public record FlexiTextureFrame {
  uint Width;
  uint Height;
  bool Recolorable;
  Color[] Palette;      // 256 entries
  byte[] PixelData;     // width * height, palette indices
  byte[]? AlphaData;    // width * height, or null
  Bitmap? Bitmap;       // lazily decoded
}

public record FlexiTexture {
  string Name;
  uint FrameCount;
  uint[]? AnimationOffsets;
  IReadOnlyList<FlexiTextureFrame> Frames;
}

public static class FlexibleTextures {
  public static IReadOnlyList<FlexiTexture> Extract(Ovl ovl);
}
```

### Implementation Steps

1. Find loaders where `Tag == "ftx"`
2. Parse `FlexiTextureInfoStruct` from loader data:
   - Read header fields (scale, width, height, fps, recolorable, offsetCount, fts2Count)
   - Read animation offset array (if offsetCount > 0)
   - Read frame array (fts2 entries)
3. For each frame:
   - Read palette (256 * 4 bytes) from relocated pointer
   - Read texture data (width * height bytes) from relocated pointer
   - Read alpha data (width * height bytes) from relocated pointer (if present)
4. Decode frames to Bitmap on demand:
   - Map palette indices to RGBA colors
   - Apply alpha channel if present

### Files to Create/Modify

**Create:**
- `OpenCobra/OVL/Files/FlexibleTextures.cs`

### Dependencies

- `System.Drawing.Common` for Bitmap creation
- Existing relocation resolution

### Regression Prevention

- No changes to `Ovl.cs`
- New test file: `OpenCobra/OVL Tests/ReadFlexibleTextures.cs`
- Run existing tests before/after

### Testing Strategy

```csharp
[Test] void Extract_StyleCommonOvl_ReturnsFlexiTextures()
[Test] void Extract_Frame_HasValidPalette()
[Test] void Extract_Frame_HasCorrectDimensions()
[Test] void DecodeBitmap_WithAlpha_ReturnsCorrectImage()
[Test] void DecodeBitmap_WithoutAlpha_ReturnsCorrectImage()
[Test] void Extract_AnimationOffsets_AreValid()
```

### Success Criteria

- All FTX entries extracted with frames, palettes, and pixel data
- Bitmap decoding correct for both alpha and non-alpha frames
- Animation offsets correctly parsed
- Zero regressions
