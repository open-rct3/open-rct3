# Plan: Decipher Uncompressed Bitmaps into .NET Bitmap Instances

## Problem

The OVL library can parse archive structure (headers, symbols, loaders, relocations) but cannot yet interpret texture data into usable .NET `Bitmap` instances. RCT3 stores textures through a two-part system: **TEX** (texture metadata/symbol references) and **FLIC** (actual pixel data with optional compression/mipmaps). We need to decode these into displayable images.

## Background Research

### RCT3 Texture Architecture (from libOVL reference)

**TEX Manager** (`ManagerTEX.h/cpp`):
- Creates `Tex` structures = `TextureStruct` + `TextureStruct2`
- `TextureStruct`: 12 unknown fields (mostly 0x70007), `unk9` (count, usually 1), `unk10` (usually 8), `unk11` (usually 0x10), `TextureData` (symbol ref to TXS style), `unk12` (usually 1), `Flic` pointer array, `ts2` pointer
- `TextureStruct2`: back-pointer to TextureStruct, `Flic` pointer array
- Symbol reference for texture style (e.g., `GUIIcon:txs`, `PathGround:txs`)

**FLIC Manager** (`ManagerFLIC.h/cpp`):
- Stores actual pixel data in extra data chunks
- Extra data layout: `FlicHeader` → `FlicMipHeader[]` → pixel data → trailing zeroed `FlicMipHeader`
- `FlicHeader`: Format (see format table below), Width, Height, Mipcount (0 or 9)
- `FlicMipHeader`: MWidth, MHeight, Pitch, Blocks
- Mipcount=9 means 7 mipmap levels + zeroed header for 256×256 textures
- Can use BTBL (Bitmap Table) manager for indexed textures, or direct pixel data

**BTBL Manager** (`ManagerBTBL.h/cpp`):
- Stores multiple textures in a bitmap table with shared format definitions
- Extra data chunk 1: `FlicHeader[]` array (one per texture)
- Extra data chunk 2: raw pixel data concatenated (no `FlicMipHeader` between textures)
- `cTexture::GetBlockSize()` determines bytes per 4×4 block: A8R8G8B8=64, DXT1=8, DXT3/DXT5=16
- Dimension calculation: `sqrt(size * 16 / GetBlockSize(format))`

**Known Texture Formats** (from `cTexture` enum in `ManagerBTBL.h`):

| Code | Format | BPP | Seen? | Block Size | Notes |
|------|--------|-----|-------|------------|-------|
| `0x01` | `FORMAT_R8G8B8` | 24 | No | — | RGB, no alpha |
| `0x02` | `FORMAT_A8R8G8B8` | 32 | **Yes** | 64 | Full RGBA, primary uncompressed target |
| `0x03` | `FORMAT_X8R8G8B8` | 32 | No | — | RGB with unused alpha byte |
| `0x04` | `FORMAT_R5G6B5` | 16 | No | — | 5-6-5 RGB |
| `0x05` | `FORMAT_X1R5G5B5` | 16 | No | — | RGB with 1 unused bit |
| `0x07` | `FORMAT_P8` | 8 | No | — | 8-bit palette index |
| `0x08` | `FORMAT_A1R5G5B5` | 16 | No | — | 1-bit alpha + 15-bit RGB |
| `0x09` | `FORMAT_X4R4G4B4` | 16 | No | — | 4-4-4 RGB with 4 unused bits |
| `0x0A` | `FORMAT_A4R4G4B4` | 16 | No | — | 4-bit alpha + 12-bit RGB |
| `0x0B` | `FORMAT_L8` | 8 | No | — | 8-bit luminance (grayscale) |
| `0x0C` | `FORMAT_A8L8` | 16 | No | — | 8-bit alpha + 8-bit luminance |
| `0x0E` | `FORMAT_V8U8` | 16 | No | — | Normal map format |
| `0x10` | `FORMAT_UYVY` | 16 | No | — | YUV 4:2:2 packed |
| `0x11` | `FORMAT_YUY2` | 16 | No | — | YUV 4:2:2 packed |
| `0x12` | `FORMAT_DXT1` | 4 | **Yes** | 8 | S3TC/DXT1 compression |
| `0x13` | `FORMAT_DXT3` | 8 | **Yes** | 16 | S3TC/DXT3 compression |
| `0x14` | `FORMAT_DXT5` | 8 | No | 16 | S3TC/DXT5 compression |
| `0x15` | `FORMAT_R3G3B2` | 8 | No | — | 3-3-2 RGB |
| `0x16` | `FORMAT_A8` | 8 | No | — | Alpha-only |
| `0x100` | `FORMAT_D16` | 16 | No | — | Depth buffer |
| `0x101` | `FORMAT_D32` | 32 | No | — | Depth buffer |
| `0x102` | `FORMAT_D15S1` | 16 | No | — | Depth + stencil |
| `0x103` | `FORMAT_D24S8` | 32 | No | — | Depth + stencil |

**Key Insight**: The "uncompressed" bitmaps we want are:
1. **Primary target**: `FORMAT_A8R8G8B8` (0x02) — confirmed seen in production, maps directly to `PixelFormat.Format32bppArgb`
2. **Secondary targets**: `FORMAT_R8G8B8` (0x01), `FORMAT_X8R8G8B8` (0x03), `FORMAT_R5G6B5` (0x04), `FORMAT_A4R4G4B4` (0x0A), `FORMAT_A1R5G5B5` (0x08), `FORMAT_L8` (0x0B), `FORMAT_A8` (0x16)
3. Textures with `Mipcount=0` (no mipmaps, single image)
4. Palette-based textures via BTBL (`FORMAT_P8`)

### Existing C# Code

- `FlexiTexture` already handles FTX format with palette/alpha channels
- `Color` struct uses BGRA byte order (matches RCT3)
- Uses `System.Drawing` namespace
- Post-processing pipeline: `ResolveRelocations()` → `ParseStrings()` → `ParseSymbols()` → `ParseLoaders()`

## Solution Architecture

### New File: `OpenCobra/OVL/Files/Textures.cs`

#### Core Types

```csharp
// On-disk structures from icontexture.h
struct TextureStruct {
  uint unk1-unk8;     // always 0x70007
  uint unk9;          // count (usually 1)
  uint unk10;         // usually 8
  uint unk11;         // usually 0x10
  uint textureDataAddr; // symbol ref address
  uint unk12;         // usually 1
  uint flicArrayAddr; // pointer to FlicStruct array
  uint ts2Addr;       // pointer to TextureStruct2
}

struct TextureStruct2 {
  uint textureAddr;   // back-pointer
  uint flicAddr;      // FlicStruct pointer
}

struct FlicStruct {
  uint flicDataPtr;   // always 0 on disk
  uint unk1;          // always 1
  float unk2;         // always 1.0
}

struct FlicHeader {
  uint format;        // texture format code
  uint width;
  uint height;
  uint mipcount;      // 0 or 9
}

struct FlicMipHeader {
  uint mWidth;
  uint mHeight;
  uint pitch;
  uint blocks;
}

// High-level texture representation
record Texture : IDisposable {
  string Name;              // symbol name (e.g. "GUIIcon:txs")
  TextureFormat Format;
  uint Width;               // from FlicHeader
  uint Height;              // from FlicHeader
  uint MipCount;
  bool IsCompressed;        // computed from Format
  bool IsSupported;         // uncompressed + known format
  IReadOnlyList<MipLevel> MipLevels;
  Bitmap? Bitmap;           // lazily decoded, first mip only
}
```

#### Textures Class

```csharp
public static class Textures {
  // Main entry point - extract all textures from an OVL
  public static TextureCollection Extract(Ovl ovl);
  
  // Format helpers
  public static bool IsCompressed(TextureFormat format);
  public static bool IsDecodable(TextureFormat format);
  public static uint GetBlockSize(TextureFormat format);
}

// Collection wrapper
public class TextureCollection : IReadOnlyList<Texture> {
  Texture this[int index] { get; }
  Texture this[string name] { get; }
  IEnumerable<string> Names { get; }
  int Count { get; }
}
```

### Implementation Steps

#### Phase 1: Structure Parsing (New File Only)
1. **Create `Textures.cs`** with on-disk structs matching `icontexture.h` and `ManagerBTBL.h`
2. **Parse TEX loader entries** from `Ovl.LoaderEntries` where `Tag == "tex"`
3. **Parse FLIC loader entries** from `Ovl.LoaderEntries` where `Tag == "flic"`
4. **Parse BTBL loader entries** from `Ovl.LoaderEntries` where `Tag == "btbl"` (if present)
5. **Extract texture metadata**: name from symbol, dimensions from FLIC data
6. **No modifications to `Ovl.cs`** — all parsing is additive in `Textures.cs`

#### Phase 2: FLIC Data Extraction (Two Layouts)
1. **Detect BTBL presence** — check if any loader has `Tag == "btbl"`
2. **BTBL layout** (when BTBL present):
   - Parse BTBL loader data for `BmpTbl` struct (unk + count)
   - Extra data chunk 0: skip 8 bytes → `FlicHeader[]` array
   - Extra data chunk 1: raw pixel data concatenated
   - For each FLIC loader: extra data chunk 0 = bitmap index into BTBL arrays
   - Calculate pixel offsets by advancing through mip levels using format-specific sizes
3. **Non-BTBL layout** (direct FLIC):
   - Extra data chunk 0: `FlicHeader` (16 bytes) → `FlicMipHeader` (16 bytes) → pixel data
   - Skip 32 bytes from chunk start to reach pixel data
4. **Extract first mip level only** — ignore additional mip levels initially

#### Phase 3: Bitmap Decoding
1. **Identify uncompressed formats** — only handle `FORMAT_A8R8G8B8` (0x02) initially
2. **Throw `NotSupportedException`** for compressed formats (DXT1/DXT3/DXT5) and unknown formats
3. **Create `Bitmap` instances** using `LockBits` + `PixelFormat.Format32bppArgb`
4. **Handle BGRA byte order** — RCT3 uses BGRA, .NET `Format32bppArgb` expects BGRA on little-endian (matches)

#### Phase 4: Integration (No Ovl.cs Changes)
1. **`Textures` class is standalone** — takes `Ovl` instance as parameter, never modifies it
2. **Lazy bitmap creation** — store raw pixel data, decode to `Bitmap` on first access
3. **Error handling** — throw descriptive exceptions for unsupported formats, corrupted data
4. **Memory management** — implement `IDisposable` on texture types to release `Bitmap` resources

### Format Code Research (RESOLVED)

All texture format codes have been identified from `cTexture` enum in `ManagerBTBL.h`. No further empirical investigation needed for format discovery.

**Conclusions**:
- **22 total formats** defined, covering uncompressed, compressed, and special-purpose formats
- **3 formats confirmed in production**: `FORMAT_A8R8G8B8` (0x02), `FORMAT_DXT1` (0x12), `FORMAT_DXT3` (0x13)
- **12 uncompressed formats** suitable for direct Bitmap conversion
- **3 compressed formats** (DXT1/DXT3/DXT5) require decompression — out of scope
- **7 special formats** (palette, YUV, depth buffers) — not standard bitmaps
- **Block size formula**: A8R8G8B8=64 bytes/4×4 block, DXT1=8, DXT3/DXT5=16
- **Dimension formula**: `sqrt(size * 16 / GetBlockSize(format))`

### Reference Dumper Limitations (RESOLVED)

The reference `OVLDump.cpp` **does NOT dump textures to disk**. It only:
- Parses FLIC data into an in-memory `m_flics` map (function `MakeFlics()`)
- Writes the entire OVL binary back via `WriteFile()` — not individual textures
- Only recognizes 4 formats for BTBL size calculation: `0x02`, `0x12`, `0x13`, `0x14` — throws `EOvlD` on unknown formats
- Never converts raw pixel data to any image format (DDS/BMP/PNG/TGA)

**Our implementation goes beyond the reference** — we must actually decode pixels to .NET `Bitmap` instances.

### Two FLIC Data Layouts (RESOLVED)

The reference handles two distinct FLIC data layouts:

**BTBL case** (when `bmptablecount > 0`):
- Extra data chunk 0: bitmap index (`uint`)
- Extra data chunk 1: `FlicHeader[]` array (one per texture)
- Extra data chunk 2: raw pixel data concatenated (no `FlicMipHeader` between textures)
- Pixel data offset calculated by advancing through mip levels using format-specific size formulas

**Non-BTBL case** (direct FLIC):
- Extra data chunk 0: `FlicHeader` → `FlicMipHeader` → pixel data
- Skip `FlicHeader` (16 bytes) + `FlicMipHeader` (16 bytes) to reach pixel data
- No mip level offset calculation needed — data pointer is direct

### Files to Create/Modify

**Create:**
- `OpenCobra/OVL/Files/Textures.cs` - main texture parsing/decoding logic

**Modify:**
- `OpenCobra/OVL/OVL.cs` - possibly add `ExtractTextures()` helper method
- `OpenCobra/OVL Tests/` - add texture extraction tests

### Dependencies

- `System.Drawing.Common` for `Bitmap` (already referenced via `System.Drawing` using)
- Existing relocation resolution infrastructure
- Symbol/loader parsing already implemented

### Regression Prevention

**CRITICAL: Zero tolerance for regressions in existing OVL parsing.**

1. **Run all existing tests BEFORE any changes** — baseline must pass:
   - `OpenCobra/OVL Tests/ReadArchives.cs` (21 tests)
   - `OpenCobra/OVL Tests/ListResources.cs`
   - `OpenCobra/OVL Tests/ReadProdArchives.cs`
2. **NEVER modify existing test files** — all texture tests go in new file
3. **New test file only**: `OpenCobra/OVL Tests/ReadTextures.cs`
4. **No changes to `Ovl.cs` core parsing** — texture code is additive only
5. **Verify existing tests pass AFTER changes** — CI gate

### Testing Strategy

**New test file: `OpenCobra/OVL Tests/ReadTextures.cs`**

```csharp
[TestFixture]
public class ReadTextures {
  // Structure parsing tests
  [Test] void FlicHeader_ParsesCorrectly()
  [Test] void FlicMipHeader_ParsesCorrectly()
  [Test] void TextureStruct_ParsesCorrectly()
  
  // Format detection tests
  [Test] void IsCompressed_DXT1_ReturnsTrue()
  [Test] void IsCompressed_DXT3_ReturnsTrue()
  [Test] void IsCompressed_DXT5_ReturnsTrue()
  [Test] void IsCompressed_A8R8G8B8_ReturnsFalse()
  [Test] void IsCompressed_R8G8B8_ReturnsFalse()
  [Test] void IsCompressed_R5G6B5_ReturnsFalse()
  [Test] void IsCompressed_UnknownFormat_ThrowsOrReturnsFalse()
  
  // Block size calculation tests
  [Test] void GetBlockSize_A8R8G8B8_Returns64()
  [Test] void GetBlockSize_DXT1_Returns8()
  [Test] void GetBlockSize_DXT3_Returns16()
  [Test] void GetBlockSize_DXT5_Returns16()
  [Test] void GetBlockSize_UnknownFormat_Throws()
  
  // Dimension calculation tests
  [Test] void GetDimension_A8R8G8B8_256x256_Returns256()
  [Test] void GetDimension_DXT3_256x256_Returns256()
  
  // Bitmap decoding tests
  [Test] void DecodeToBitmap_A8R8G8B8_ReturnsValidBitmap()
  [Test] void DecodeToBitmap_A8R8G8B8_CorrectDimensions()
  [Test] void DecodeToBitmap_A8R8G8B8_CorrectPixelFormat()
  [Test] void DecodeToBitmap_R8G8B8_ReturnsValidBitmap()
  [Test] void DecodeToBitmap_R5G6B5_ReturnsValidBitmap()
  [Test] void DecodeToBitmap_CompressedFormat_ThrowsNotSupportedException()
  [Test] void DecodeToBitmap_UnknownFormat_ThrowsNotSupportedException()
  
  // Integration tests with real OVL files
  [Test] void ExtractTextures_StyleCommonOvl_ReturnsTextures()
  [Test] void ExtractTextures_StyleCommonOvl_A8R8G8B8TexturesDecodable()
  [Test] void ExtractTextures_WithBTBL_ParsesCorrectly()
  [Test] void ExtractTextures_WithMipmaps_ExtractsFirstLevel()
  
  // Edge case tests
  [Test] void ExtractTextures_EmptyOvl_ReturnsEmptyList()
  [Test] void ExtractTextures_NoTexEntries_ReturnsEmptyList()
  [Test] void DecodeToBitmap_CorruptedData_ThrowsGracefully()
  [Test] void DecodeToBitmap_ZeroWidth_ThrowsArgumentException()
  [Test] void DecodeToBitmap_ZeroHeight_ThrowsArgumentException()
}
```

### Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Breaking existing OVL parsing | Low | Critical | No modifications to `Ovl.cs` core; run existing tests before/after |
| Incorrect format detection | Medium | High | Unit tests for all 22 format codes; explicit enum matching libOVL |
| Memory leaks from Bitmap disposal | Medium | Medium | Use `using` statements; test Bitmap disposal |
| BGRA/RGBA byte order swap | Medium | High | Test pixel values against known data; verify against existing `Color` struct |
| Mipmap level extraction errors | Low | Medium | Only extract level 0 initially; test mipcount=0 case first |
| BTBL extra data parsing errors | Medium | Medium | Separate test path for BTBL vs FLIC; validate header counts |
| Unsupported format crashes | Low | High | Throw `NotSupportedException` with format code; never crash |

### Tradeoffs Considered

- **Lazy vs eager bitmap creation**: Lazy saves memory, eager simplifies error handling
- **System.Drawing vs SkiaSharp**: System.Drawing is built-in but deprecated on non-Windows; SkiaSharp is cross-platform but adds dependency
- **Full FLIC decompression vs uncompressed only**: Starting with uncompressed simplifies initial implementation

## Success Criteria

- Can extract texture metadata (name, dimensions, format) from any OVL
- Can decode uncompressed textures to valid `Bitmap` instances
- Handles edge cases gracefully (missing data, unsupported formats)
- **All existing tests pass** — zero regressions
- **New test file with 30+ tests** covering formats, decoding, edge cases
- Performance acceptable for GUI icon textures (typically 32x32 to 256x256)

## Future Work: Dumper App Integration

The `Textures` class is designed as a standalone library component. The following changes to the Dumper app will enable texture browsing and display:

### Files to Modify

**`Dumper/MainForm.cs`** (Windows) and **`Dumper/Documents/OvlViewController.cs`** (macOS):

1. **Add "Textures" node** to the tree view alongside existing file type groups
   - Populate with `texture.Names` from `Textures.Extract(ovl)`
   - Show format, dimensions, and supported status in node subtitle

2. **Handle texture selection** in tree view click/double-click handler:
   ```csharp
   var textures = Textures.Extract(ovl);
   var texture = textures[selectedName];
   if (texture.IsSupported) {
     contentArea.Image = texture.Bitmap;
   } else {
     contentArea.ShowWarning($"Unsupported format: {texture.Format}");
   }
   ```

3. **Show texture metadata** in a detail panel or tooltip:
   - Name, Format, Width, Height, MipCount
   - IsCompressed, IsSupported flags
   - Raw pixel data size

### UI Considerations

- **Lazy loading**: Call `Textures.Extract()` once when OVL loads, cache the `TextureCollection`
- **Memory management**: Dispose `TextureCollection` when closing OVL (releases all `Bitmap` instances)
- **Unsupported formats**: Show a placeholder icon or warning message for DXT-compressed textures
- **Large textures**: Consider scaling for display if dimensions exceed content area

### Future Enhancements (Out of Scope)

- **DXT decompression**: Add S3TC/DXT decoder to support compressed textures
- **Mipmap viewer**: Allow switching between mip levels in the UI
- **Export to file**: Save decoded textures as PNG/BMP/DDS
- **FTX support**: Decode FlexiTexture (palette-based animated textures)
- **Batch export**: Export all supported textures from an OVL at once

## References

- [icontexture.h](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/include/icontexture.h) - Texture structure definitions
- [ManagerTEX.h/cpp](https://github.com/chances/rct3-importer/tree/main/RCT3%20Importer/src/libOVLng) - TEX manager implementation
- [ManagerFLIC.h/cpp](https://github.com/chances/rct3-importer/tree/main/RCT3%20Importer/src/libOVLng) - FLIC manager implementation
- [ovlstructs.h](https://github.com/chances/rct3-importer/blob/main/RCT3%20Importer/include/ovlstructs.h) - OVL structure definitions
- [Porting libOVL.md](../docs/ai/Porting%20libOVL.md) - Existing porting documentation
