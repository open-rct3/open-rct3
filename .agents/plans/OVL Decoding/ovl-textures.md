# Correctly Parsing Cobra Textures

## Critical Ingestion Pitfalls & Alignment Differences

When porting `libOVLng`'s texture parser to C#, three critical bugs/misalignments must be accounted for:

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

## Re-implementation (C# Parser Patches)

### Standalone FLIC Decoder
```csharp
private static Texture ReadFlic(string name, ReadOnlyMemory<byte> data, Version ovlVersion, bool isRelocatedPtr = false, Texture[]? table = null) {
  using var reader = new BinaryReader(data.AsStream());

  // Pitfall 1: Skip 4-byte fl pointer if the stream was obtained via a tex.Flic relocation pointer
  if (isRelocatedPtr) {
    reader.BaseStream.Position += 4;
  }

  var read = reader.Read<Flic>(out _);
  Debug.Assert(read == Marshal.SizeOf<Flic>());

  if (table != null) {
    var index = reader.ReadUInt32();
    Debug.Assert(index < table.Length);
    if (ovlVersion == Version.Five)
      reader.BaseStream.Position += Marshal.SizeOf<ExtraDataInfoV5>();
    return table[index].WithName(name);
  }

  var header = reader.ReadFlicHeader();
  var format = header.Format;
  var texture = new Texture(name, format, header.Width, header.Height, header.MipCount);

  for (var i = 0; i < header.MipCount; i++) {
    read = reader.Read<FlicMipHeader>(out var mipHeader);
    Debug.Assert(read == Marshal.SizeOf<FlicMipHeader>());

    // Pitfall 3: Calculate size using pitch * blocks to support all formats safely
    var size = Convert.ToInt32(mipHeader.Pitch * mipHeader.Blocks);
    ReadOnlySpan<byte> pixels = reader.ReadBytes(size);
    texture.MipLevels[i] = pixels.ToImage(format);
  }

  return texture;
}
```

### Bitmap Table Decoder
```csharp
private static Texture[] ReadBitmapTable(string name, Stream stream) {
  using var reader = new BinaryReader(stream);

  var header = reader.ReadBitmapTable();
  if (header.Length == 0) return [];

  // Skip the 8-byte padding in Chunk 1
  reader.BaseStream.Position += 8;

  // Pitfall 2: Read all FlicHeaders first
  var headers = new FlicHeader[header.Length];
  for (int i = 0; i < header.Length; i++) {
    headers[i] = reader.ReadFlicHeader();
  }

  // Read all raw texture pixels sequentially from Chunk 2
  var textures = new Texture[header.Length];
  for (int i = 0; i < header.Length; i++) {
    var flic = headers[i];
    textures[i] = new Texture(name, flic.Format, flic.Width, flic.Height, flic.MipCount);

    for (int mip = 0; mip < flic.MipCount; mip++) {
      // Calculate mip dimensions and size
      var mipDim = flic.Width >> mip;
      var size = mipDim * mipDim * flic.Format.BlockSize() / 16;

      var data = reader.ReadBytes(Convert.ToInt32(size));
      textures[i].MipLevels[mip] = Image.Load<Rgba32>(data);
    }
  }

  return textures;
}
```

### Resolving Texture Styles (TXS)
Texture styles mapped by `TextureData` can be resolved by looking up the relocation table offset:
```csharp
public string? ResolveTextureStyle(Ovl ovl, Tex tex) {
  if (tex.TextureData.Value == 0) return null;

  // Find the relocation corresponding to the TextureData pointer address
  var relocation = ovl.Relocations.FirstOrDefault(r => r.Source.Offset == tex.TextureData.Value);
  if (relocation?.TargetOffset == null) return null;

  // Look up the symbol whose data pointer matches the relocation's target offset
  return ovl.Keys.FirstOrDefault(k => ovl[k].Offset == relocation.TargetOffset)?.Name;
}
```
