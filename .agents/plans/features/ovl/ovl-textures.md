# Correctly Parsing Cobra Textures

## 1. Goal Description

This plan addresses several structural and relocation bugs in `OpenCobra`'s texture ingestion pipeline. Ingesting RollerCoaster Tycoon 3 (Cobra engine) textures requires precise handling of `tex`, `flic`, and `btbl` archive resources. The current C# implementation contains alignment misalignments, incorrect block offset lookups, a factor-of-2 size bug for compressed mips, and an interleaved reading loop that fails on bitmap tables (`btbl`).

We will patch `OVL.cs`, `Textures.cs`, and add new comprehensive integration tests in `IngestionTests.cs` to verify texture correctness under all conditions.

---

## 2. Critical Ingestion Pitfalls & Alignment Differences

### 1. Pointer Prefix Offset in `tex.Flic` vs. `FileType.Flic` Symbols
In the reference C++ implementation, the `Flic` struct on disk actually contains a pointer followed by the structure itself:
```cpp
struct Flic {
    FlicStruct *fl;   // Offset 0 (4 bytes)
    FlicStruct fl2;   // Offset 4 (12 bytes)
};
```
* **Symbol Table (`FileType.Flic`)**: Registers resources starting exactly at `fl2` (Offset 4). The returned memory block contains **no pointer prefix**.
* **Tex Pointer (`tex.Flic`)**: Relocated using `GetPointer1`, which points to `fl` (Offset 0). The returned memory block **contains the 4-byte pointer prefix**.

> [!IMPORTANT]
> To properly read flics referenced through `tex.Flic` without corrupting downstream offsets, **you must skip the first 4 bytes** (`fl`) in the stream to align the reader to `fl2`. If reading a flic directly from a symbol record, **do not skip any bytes**.

### 2. Grouped Headers vs. Interleaved Loops in `btbl` Ingestion
The C# parser currently attempts to read a `FlicHeader`, immediately followed by that texture's mip bytes, in an interleaved loop inside `ReadBitmapTable`.
**This is incorrect.** `libOVLng` writes the extra data in two distinct chunks:
1. **Chunk 1**: `8 bytes` padding (always zero) followed by **all** `FlicHeader`s concatenated together (`count * 16` bytes).
2. **Chunk 2**: **All** raw texture mip pixel blocks concatenated together.

> [!TIP]
> The correct `btbl` parsing algorithm must:
> 1. Read `BitmapTable` header (8 bytes).
> 2. Skip `8 bytes` of padding.
> 3. Read **all** `FlicHeader`s into an array first: `var headers = new FlicHeader[count]`.
> 4. Loop through each header and read the corresponding mip pixel bytes sequentially from the stream.

### 3. Mip Dimension and Byte Size Formula
* **Pitch and Blocks**: Each mip level is preceded by a `FlicMipHeader`. `Pitch` (bytes per row/block) and `Blocks` (number of rows/blocks) are calculated by the encoder. The exact size of the raw pixel data block is:
  $$\text{Size} = \text{mipHeader.Pitch} \times \text{mipHeader.Blocks}$$
* **Factor-of-2 Compression Size Bug**: The current C# size calculation `mipHeader.Width * mipHeader.Height * (format.BlockSize() / 8)` is **incorrect**. Because `BlockSize` represents bytes per $4 \times 4$ texel block (16 pixels), the divisor must be `16` instead of `8`:
  $$\text{Size} = \text{mipHeader.Width} \times \text{mipHeader.Height} \times \frac{\text{BlockSize}}{16}$$
  Using a divisor of `8` mistakenly requests double the actual bytes, corrupting subsequent stream parsing.

---

## 3. Proposed Changes

### [OpenCobra/OVL](file:///Users/chancesnow/GitHub/open-rct3/OpenCobra/OVL)

#### [MODIFY] [OVL.cs](file:///Users/chancesnow/GitHub/open-rct3/OpenCobra/OVL/OVL.cs)
* Correct the `ReadResource(RelocationPointer ptr)` implementation.
* Look up the pointer relocation target offset (`TargetOffset`), find the file block that contains that target offset, and calculate the exact read slice from `fileBlock.Offset + relOffset` with correct length `fileBlock.Size - relOffset` (instead of reading the whole block blindly from index zero).

```csharp
  public byte[]? ReadResource(RelocationPointer ptr) {
    var relocation = relocations.FirstOrDefault(rel => rel.Source.Offset == ptr.Value);
    if (relocation == null || relocation.TargetOffset == null) return null;

    var targetOffset = relocation.TargetOffset.Value;
    var blocks =
      from groups in fileBlocks
      from @group in groups
      from fileBlock in @group.Blocks
      where targetOffset >= fileBlock.RelativeOffset && targetOffset < fileBlock.RelativeOffset + fileBlock.Size
      select fileBlock;
    var block = blocks.FirstOrDefault();
    if (block == null) return null;

    var relOffset = targetOffset - block.RelativeOffset;
    var size = block.Size - relOffset;
    var bytes = new byte[size];
    using var fs = new FileStream(block.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
    fs.Seek(Convert.ToInt32(block.Offset + relOffset), SeekOrigin.Begin);
    fs.ReadExactly(bytes, 0, Convert.ToInt32(size));
    return bytes;
  }
```

### [OpenCobra/OVL/Files](file:///Users/chancesnow/GitHub/open-rct3/OpenCobra/OVL/Files)

#### [MODIFY] [Textures.cs](file:///Users/chancesnow/GitHub/open-rct3/OpenCobra/OVL/Files/Textures.cs)
* Update `ReadFlic` signature and body:
  - Add `bool isRelocatedPtr = false` parameter.
  - Skip the first 4 bytes (`fl` pointer prefix) if `isRelocatedPtr` is `true`.
  - Calculate mip dimensions/sizes using `mipHeader.Pitch * mipHeader.Blocks` which is robust and correct for all formats.
* Update `ReadTextures`:
  - Pass `isRelocatedPtr: true` when calling `ReadFlic(..., ovl.ReadResource(tex.Flic), ...)` since it originates from a relocated pointer.
* Update standalone flic decoding path in `Extract`:
  - Pass `isRelocatedPtr: false` (the default) since it was read from a symbol record.
* Update `ReadBitmapTable`:
  - Skip the 8-byte padding.
  - Read **all** `FlicHeader`s first into an array.
  - Loop through each texture, read its mips sequentially, calculating sizes via `mipDim * mipDim * flic.Format.BlockSize() / 16`.
  - Use `.ToImage(flic.Format)` to ensure compressed formats decode properly.

### [OpenCobra/Tests/Integration](file:///Users/chancesnow/GitHub/open-rct3/OpenCobra/Tests/Integration)

#### [MODIFY] [IngestionTests.cs](file:///Users/chancesnow/GitHub/open-rct3/OpenCobra/Tests/Integration/IngestionTests.cs)
* Add a test `LoadUniqueTextures_Succeeds` that loads a unique OVL file (with standalone or indexed textures) to verify cross-relocation and style symbol resolving invariants.
* Add assertions to verify that all mip levels are parsed with non-zero dimensions and valid pixel counts.
* Verify the texture style TXS name mapping matches expectations (e.g. `GUIIcon` or `PathGround` style maps).

---

## 4. Verification Plan

### Automated Tests
* Run the unit and integration tests using `make test integration`.
* Confirm that `LoadTerrainTexture_Succeeds` and the new unique OVL texture tests pass successfully without stream corruption or dimension crashes.
