// TextureDecoding
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024-2026 OpenRCT3 Contributors. All rights reserved.
//
// Shared tex/flic/btbl decode plumbing. Originally lived entirely in Textures.cs; split out so
// other symbol families that reuse the same on-disk tex/flic/btbl loader shapes - CharacterSkins
// (mms/prt), ParticleEffects (psi) - can decode through the same code instead of duplicating it.
// See .agents/bugs/ovl-texture-decoding.md for the reverse-engineering history behind this format.
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenCobra.OVL.Files;

public class Texture(string name, TextureFormat format, uint width, uint height, uint mipCount = 1) : IDisposable {
  private bool disposed;

  /// <summary>
  /// The symbol name of the texture, e.g. "GUIIcon.txs".
  /// </summary>
  public string Name { get; private set; } = name;
  public readonly TextureFormat Format = format;
  /// <summary>
  /// Symbol reference to a Texture Style (TXS).
  /// </summary>
  public string? Style;
  public readonly uint Width = width;
  public readonly uint Height = height;
  public readonly uint MipCount = mipCount;
  public readonly bool IsCompressed = format.IsCompressed();
  /// <summary>
  /// The decoded texture data.
  /// </summary>
  public readonly Image<Rgba32>[] MipLevels = new Image<Rgba32>[mipCount == 0 ? 1 : mipCount];

  protected virtual void Dispose(bool disposing) {
    if (disposed || !disposing) return;
    foreach (var mipLevel in MipLevels) mipLevel?.Dispose();
    disposed = true;
  }

  public void Dispose() {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }

  /// <returns>A clone of this texture with a new <paramref name="name"/>.</returns>
  public Texture WithName(string name) {
    var clone = MemberwiseClone() as Texture;
    Debug.Assert(clone != null);
    clone.Name = name;
    return clone;
  }
}

public class TextureCollection : IReadOnlyList<Texture> {
  private readonly Dictionary<string, Texture> textures = [];

  public Texture this[int index] => textures.Values.ElementAt(index);
  public Texture this[string name] => textures[name];

  Texture IReadOnlyList<Texture>.this[int index] => throw new NotImplementedException();

  public IEnumerable<string> Names => [.. textures.Keys];
  public int Count => textures.Count;

  int IReadOnlyCollection<Texture>.Count => Count;

  public IEnumerator<Texture> GetEnumerator() => textures.Values.GetEnumerator();
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  internal void Add(Texture texture) => textures[texture.Name] = texture;

  // ReSharper disable once ParameterHidesMember
  internal TextureCollection AddRange(IEnumerable<Texture> textures) {
    foreach (var texture in textures)
      this.textures[texture.Name] = texture;
    return this;
  }
}

internal record struct OvlData(string OvlName, OvlFile File, ReadOnlyMemory<byte> Data);

[StructLayout(LayoutKind.Sequential, Size = 8)]
internal readonly struct BitmapTable {
  [SeenInGame(Values = [0])]
  public readonly uint Unk;
  /// <summary>
  /// Number of textures stored in the table.
  /// </summary>
  public readonly uint Length;
}

[StructLayout(LayoutKind.Sequential, Size = 16)]
internal readonly struct FlicHeader {
  public readonly TextureFormat Format;
  public readonly uint Width;
  public readonly uint Height;
  public readonly uint MipCount;
}

[StructLayout(LayoutKind.Sequential, Size = 16)]
internal readonly struct FlicMipHeader {
  public readonly uint Width;
  public readonly uint Height;
  public readonly uint Pitch;
  public readonly uint Blocks;
}

// TextureStruct (icontexture.h), 60 bytes. Only the fields OpenCobra actually needs are named;
// unk1-unk8 are always 0x70007 and otherwise unused.
[StructLayout(LayoutKind.Explicit, Size = 60)]
internal readonly struct Tex {
  [FieldOffset(32)]
  [SeenInGame(Values = [1])]
  public readonly uint Unk9;

  [FieldOffset(36)]
  [SeenInGame(Values = [8])]
  public readonly uint Unk10;

  [FieldOffset(40)]
  [SeenInGame(Values = [0x0D, 0x10])]
  public readonly uint Unk11;
  public readonly TextureType Type => (TextureType)Unk11;

  /// <summary>Symbol reference for a TXS (texture style). Always 0 on disk.</summary>
  [FieldOffset(44)]
  public readonly uint TextureData;

  [FieldOffset(48)]
  [SeenInGame(Values = [1])]
  public readonly uint Unk12;

  /// <summary>
  /// A <c>FlicStruct**</c> - "always points to pointer before flic data" (rct3tex.cpp:621). Reading
  /// pixel data means chasing this through two chained relocation-table lookups (Part 6 Finding 2);
  /// see <see cref="TextureDecoding.ReadTexture"/>.
  /// </summary>
  [FieldOffset(52)]
  public readonly uint FlicPtr;
}

internal static class BinaryReaderExtensions {
  public static BitmapTable ReadBitmapTable(this BinaryReader reader) =>
    reader.Read<BitmapTable>(out var table) != 0 ? table : default;

  public static FlicHeader ReadFlicHeader(this BinaryReader reader) =>
    reader.Read<FlicHeader>(out var flic) != 0 ? flic : default;

  /// <summary>
  /// Reads a structure of type <typeparamref name="T"/> from the binary reader and returns the number of bytes read.
  /// </summary>
  public static uint Read<T>(this BinaryReader reader, out T data) {
    byte[] bytes = reader.ReadBytes(Marshal.SizeOf(typeof(T)));

    var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
    var ptr = handle.AddrOfPinnedObject();
    if (ptr == nint.Zero) {
      data = default!;
      return 0;
    }
    var structure = (T?)Marshal.PtrToStructure(ptr, typeof(T));
    if (structure == null) data = default!;
    handle.Free();

    data = structure!;
    return Convert.ToUInt32(bytes.Length);
  }
}

internal static class ReadOnlySpanExtensions {
  public static Image<Rgba32> ToImage(this ReadOnlySpan<byte> span, TextureFormat format, int width, int height) {
    // QUESTION: Add support for TextureFormat.P8?
    // Reference's `ReadTexture2` shows P8 needs palette lookup to convert indexed colors to RGBA.
    return format switch {
      TextureFormat.A8R8G8B8 => DecodeA8R8G8B8(span, width, height),
      TextureFormat.Dxt1 => DxtDecoder.DecodeDxt1(span, width, height),
      TextureFormat.Dxt3 => DxtDecoder.DecodeDxt3(span, width, height),
      TextureFormat.Dxt5 => DxtDecoder.DecodeDxt5(span, width, height),
      _ => throw new InvalidOperationException($"Unsupported texture format: {format}"),
    };
  }

  private static Image<Rgba32> DecodeA8R8G8B8(ReadOnlySpan<byte> span, int width, int height) {
    // Raw A8R8G8B8 pixel bytes, not an encoded image file - Image.Load's format auto-detection
    // fails (or worse, silently misreads) on this data. Load it as pixel data instead.
    using var image = Image.LoadPixelData<Argb32>(span, width, height);
    return image.CloneAs<Rgba32>();
  }
}

internal static class DxtDecoder {
  // DXT block decoders. The data layout is 4x4 blocks; each block is 8 (DXT1) or 16 (DXT3/5)
  // bytes. `span` length must be a whole number of blocks; width/height are in pixels and may
  // be any value (padded to 4x4 internally).
  public static Image<Rgba32> DecodeDxt1(ReadOnlySpan<byte> span, int width = 0, int height = 0) {
    if (width == 0 || height == 0)
      // Without explicit dimensions we assume a square 4-aligned texture whose pixel count
      // matches the block count. Bitmap-table paths pass dimensions explicitly.
      throw new InvalidOperationException("DXT1 decode needs explicit dimensions");
    var pixels = new Rgba32[width * height];
    var blockCount = span.Length / 8;
    var blocksPerRow = Math.Max(1, (width + 3) / 4);
    var blocksRows = Math.Max(1, (height + 3) / 4);
    if (blockCount < blocksPerRow * blocksRows)
      throw new InvalidDataException(
        $"DXT1 data truncated: {blockCount} blocks, need {blocksPerRow * blocksRows} for {width}x{height}");
    for (var by = 0; by < blocksRows; by++)
    for (var bx = 0; bx < blocksPerRow; bx++) {
      var blockOffset = (by * blocksPerRow + bx) * 8;
      DecodeDxt1Block(span.Slice(blockOffset, 8), pixels, width, bx * 4, by * 4);
    }
    return Image.LoadPixelData<Rgba32>(pixels, width, height);
  }

  public static Image<Rgba32> DecodeDxt3(ReadOnlySpan<byte> span, int width = 0, int height = 0) {
    if (width == 0 || height == 0)
      throw new InvalidOperationException("DXT3 decode needs explicit dimensions");
    var pixels = new Rgba32[width * height];
    var blockCount = span.Length / 16;
    var blocksPerRow = Math.Max(1, (width + 3) / 4);
    var blocksRows = Math.Max(1, (height + 3) / 4);
    if (blockCount < blocksPerRow * blocksRows)
      throw new InvalidDataException(
        $"DXT3 data truncated: {blockCount} blocks, need {blocksPerRow * blocksRows} for {width}x{height}");
    for (var by = 0; by < blocksRows; by++)
    for (var bx = 0; bx < blocksPerRow; bx++) {
      var blockOffset = (by * blocksPerRow + bx) * 16;
      DecodeDxt3Block(span.Slice(blockOffset, 16), pixels, width, bx * 4, by * 4);
    }
    return Image.LoadPixelData<Rgba32>(pixels, width, height);
  }

  public static Image<Rgba32> DecodeDxt5(ReadOnlySpan<byte> span, int width = 0, int height = 0) {
    if (width == 0 || height == 0)
      throw new InvalidOperationException("DXT5 decode needs explicit dimensions");
    var pixels = new Rgba32[width * height];
    var blockCount = span.Length / 16;
    var blocksPerRow = Math.Max(1, (width + 3) / 4);
    var blocksRows = Math.Max(1, (height + 3) / 4);
    if (blockCount < blocksPerRow * blocksRows)
      throw new InvalidDataException(
        $"DXT5 data truncated: {blockCount} blocks, need {blocksPerRow * blocksRows} for {width}x{height}");
    for (var by = 0; by < blocksRows; by++)
    for (var bx = 0; bx < blocksPerRow; bx++) {
      var blockOffset = (by * blocksPerRow + bx) * 16;
      DecodeDxt5Block(span.Slice(blockOffset, 16), pixels, width, bx * 4, by * 4);
    }
    return Image.LoadPixelData<Rgba32>(pixels, width, height);
  }

  private static void DecodeDxt1Block(ReadOnlySpan<byte> block, Rgba32[] pixels, int width, int x, int y) {
    var c0 = BitConverter.ToUInt16(block.Slice(0, 2));
    var c1 = BitConverter.ToUInt16(block.Slice(2, 2));
    var lut = BitConverter.ToUInt32(block.Slice(4, 4));
    var color0 = Rgb565(c0);
    var color1 = Rgb565(c1);
    for (var py = 0; py < 4; py++)
    for (var px = 0; px < 4; px++) {
      var idx = (int)((lut >> ((py * 4 + px) * 2)) & 0x3);
      Rgba32 rgba;
      if (idx == 0) rgba = color0;
      else if (idx == 1) rgba = color1;
      else if (c0 > c1) rgba = new Rgba32(
        (byte)((color0.R * 2 + color1.R + 1) / 3),
        (byte)((color0.G * 2 + color1.G + 1) / 3),
        (byte)((color0.B * 2 + color1.B + 1) / 3)
      );
      else if (idx == 2) rgba = new Rgba32(
        (byte)((color0.R + color1.R + 1) / 2),
        (byte)((color0.G + color1.G + 1) / 2),
        (byte)((color0.B + color1.B + 1) / 2)
      );
      else rgba = new Rgba32(0, 0, 0, 0);
      var dx = x + px;
      var dy = y + py;
      if ((uint)dx < (uint)width && (uint)dy < (uint)(pixels.Length / width))
        pixels[dy * width + dx] = rgba;
    }
  }

  private static void DecodeDxt3Block(ReadOnlySpan<byte> block, Rgba32[] pixels, int width, int x, int y) {
    for (var py = 0; py < 4; py++)
    for (var px = 0; px < 4; px++) {
      var alphaByte = block[py * 2 + (px / 4)];
      var alpha = (byte)(((alphaByte >> ((px & 3) * 2)) & 0x3) * 0x55);
      var saved = pixels[(y + py) * width + (x + px)];
      pixels[(y + py) * width + (x + px)] = new Rgba32(saved.R, saved.G, saved.B, alpha);
    }
    DecodeDxt1Block(block.Slice(8, 8), pixels, width, x, y);
  }

  private static void DecodeDxt5Block(ReadOnlySpan<byte> block, Rgba32[] pixels, int width, int x, int y) {
    var a0 = block[0];
    var a1 = block[1];
    var alphaLut = (ulong)BitConverter.ToUInt32(block.Slice(2, 4))
                 | ((ulong)BitConverter.ToUInt32(block.Slice(6, 4)) << 32);
    var alphas = new byte[8];
    alphas[0] = a0;
    alphas[1] = a1;
    if (a0 > a1) {
      for (var i = 0; i < 6; i++)
        alphas[i + 2] = (byte)(((6 - i) * a0 + (i + 1) * a1) / 7);
    } else {
      for (var i = 0; i < 4; i++)
        alphas[i + 2] = (byte)(((4 - i) * a0 + (i + 1) * a1) / 5);
      alphas[6] = 0;
      alphas[7] = 0xFF;
    }
    for (var py = 0; py < 4; py++)
    for (var px = 0; px < 4; px++) {
      var idx = (int)((alphaLut >> ((py * 4 + px) * 3)) & 0x7);
      var saved = pixels[(y + py) * width + (x + px)];
      pixels[(y + py) * width + (x + px)] = new Rgba32(saved.R, saved.G, saved.B, alphas[idx]);
    }
    DecodeDxt1Block(block.Slice(8, 8), pixels, width, x, y);
  }

  private static Rgba32 Rgb565(ushort c) => new(
    (byte)(((c >> 11) & 0x1F) * 255 / 31),
    (byte)(((c >> 5) & 0x3F) * 255 / 63),
    (byte)((c & 0x1F) * 255 / 31)
  );
}

// See ManagerTEX.cpp/ManagerFLIC.cpp/ManagerBTBL.cpp (icontexture.h). Shared decode logic for the
// three on-disk loader shapes ("tex", "flic", "btbl") that back tex/flic/btbl-tagged symbols, plus
// the other symbol families that reuse the exact same shapes under different tags (mms/prt/psi -
// see CharacterSkins.cs, ParticleEffects.cs).
internal static class TextureDecoding {
  // See ManagerTEX.cpp/icontexture.h (TextureStruct). Texture pixel data is never inline:
  // TextureStruct.Flic (FlicPtr, offset 52) is a FlicStruct** - "always points to pointer before
  // flic data" (rct3tex.cpp:621) - needing two chained relocation-table lookups to resolve to the
  // owning FLIC loader's own data address (Part 6 Finding 2). That pixel data itself lives in the
  // loader's "extra data" chunk stream, not any relocatable block.
  //
  // Reference (rct3tex.cpp:1368 TextureLoader) checks `tex->Flic != 0` and silently does nothing
  // (`tex->Flic = 0`) when it's absent - it's not an error, just a tex symbol with no backing image
  // (e.g. runtime-generated render targets, LOD placeholders like "...Dummy"). FlicPtr for these is
  // only ever populated by the relocation-fixup table at load time, so on disk it's an
  // uninitialized/placeholder value that never appears as a listed relocation source - there is no
  // way to recover pixel data for them, because there isn't any. Returning null here (rather than
  // throwing) lets callers skip them without counting them as decode failures.
  // <paramref name="bitmapTablesByFlicAddress"/> maps a "flic"-tagged loader's relocation-resolved
  // data address to whichever "btbl" table was current at that point in loader-table order (Part 6
  // Finding 4 - btbl/flic association is positional by loader-table order, not one-table-per-file),
  // built by walking Ovl.LoaderEntriesInOrder.
  public static Texture? ReadTexture(
    string name, Ovl ovl, uint texAddress, ReadOnlyMemory<byte> bytes,
    IReadOnlyDictionary<uint, Texture[]>? bitmapTablesByFlicAddress
  ) {
    using var ms = bytes.AsStream();
    using var reader = new BinaryReader(ms);

    var read = reader.Read<Tex>(out var tex);
    Debug.Assert(read == Marshal.SizeOf<Tex>());

    // Hop 1: gate FlicPtr's on-disk value as a real (fixed-up) pointer rather than unpatched
    // placeholder bytes, by requiring texAddress+52 (the field's own location) to be listed in the
    // relocation-fixup table.
    if (!ovl.TryGetRelocationSource(texAddress + 52, out var flicSlot))
      return null;
    // Hop 2: FlicPtr is a double pointer - flicSlot is itself a relocatable location, and the value
    // stored there is the FLIC loader's own (equally relocation-gated) data address.
    if (!ovl.TryGetRelocationSource(flicSlot, out var flicAddr))
      return null;

    if (!ovl.TryReadExtraData(flicAddr, out var chunks) || chunks.Count == 0)
      return null;

    var bitmapTable = bitmapTablesByFlicAddress?.GetValueOrDefault(flicAddr);
    var texture = ReadFlic(name, chunks[0], bitmapTable);

    // Default style to GUI icon for icon textures
    if (tex.Type == TextureType.Icon)
      texture.Style = "GUIIcon";
    // TODO: Resolve the real texture style symbol reference (a SymbolRefStruct entry, not yet
    // parsed by Ovl). TextureStruct.TextureData is documented as always 0 on disk, so it cannot be
    // used for this - see ManagerTEX.cpp:83, LodSymRefManager.cpp's reserveSymbolReference.

    return texture;
  }

  // See ManagerFLIC.cpp. `chunk` is a loader's raw extra-data chunk: either a 4-byte index into
  // `table` (bitmap-table-backed archives) or a standalone FlicHeader + mips + pixel data blob.
  // Neither case has a leading FlicStruct - that struct is only ever a placeholder holding zeros,
  // reachable via relocation but never used to store the extracted pixel data.
  public static Texture ReadFlic(string name, byte[] chunk, Texture[]? table = null) {
    // A bitmap-table index chunk is always exactly 4 bytes (see ManagerFLIC.cpp::Make); anything
    // else is a standalone flic blob. Deciding this from the chunk's own length, rather than
    // solely from whether a bitmap table happens to be available, prevents a 4-byte index from
    // being misread as a 16-byte FlicHeader if the archive's bitmap table failed to decode.
    if (chunk.Length == 4) {
      if (table == null)
        throw new InvalidOperationException($"'{name}' references a bitmap table that failed to decode");

      var index = BitConverter.ToUInt32(chunk, 0);
      if (index >= table.Length)
        throw new InvalidOperationException(
          $"'{name}' bitmap table index {index} is out of range (table has {table.Length} entries)");

      return table[index].WithName(name);
    }

    using var ms = new ReadOnlyMemory<byte>(chunk).AsStream();
    using var reader = new BinaryReader(ms);

    var header = reader.ReadFlicHeader();
    if (header.Format == 0)
      throw new InvalidDataException($"'{name}' has zero format");
    if (header.Width == 0 || header.Height == 0)
      throw new InvalidDataException($"'{name}' has zero dimensions ({header.Width}x{header.Height})");

    var format = header.Format;
    // Reference (ManagerFLIC.cpp; rct3tex.cpp::ReadTexture) doesn't trust the header's MipCount:
    // it pre-reads the first FlicMipHeader, then loops while (width && height && pitch && blocks)
    // are all non-zero, reading a new mip header at the tail of each iteration. A mip is
    // "accepted" only when its MWidth/MHeight match max(1, header.W/H >> level); `level` only
    // advances on a match, so a mip header that doesn't line up with the expected downsampled
    // dimensions is simply skipped over. We size the Texture's mip array to log2(width)+1, the
    // number of distinct downsampled sizes available, and let the loop leave unused slots null.
    var mipCount = Convert.ToUInt32(ComputeMipCount(header));
    var texture = new Texture(name, format, header.Width, header.Height, mipCount);

    if (reader.Read<FlicMipHeader>(out var mipHeader) != Marshal.SizeOf<FlicMipHeader>())
      throw new InvalidDataException($"'{name}' mip header truncated");

    for (var level = 0; header.Width > 0 && header.Height > 0 && mipHeader.Pitch > 0 && mipHeader.Blocks > 0; level++) {
      var expectedWidth = Math.Max(1u, header.Width >> level);
      var expectedHeight = Math.Max(1u, header.Height >> level);
      if (mipHeader.Width == expectedWidth && mipHeader.Height == expectedHeight) {
        // QUESTION: What is the purpose of `mipHeader.Pitch`?
        var size = Convert.ToInt32(mipHeader.Pitch * mipHeader.Blocks);
        if (reader.BaseStream.Position + size > reader.BaseStream.Length)
          throw new InvalidDataException($"'{name}' mip {level} data exceeds file size");
        ReadOnlySpan<byte> data = reader.ReadBytes(size);
        texture.MipLevels[level] = data.ToImage(format,
          Convert.ToInt32(expectedWidth), Convert.ToInt32(expectedHeight));
      }

      if (reader.Read<FlicMipHeader>(out var next) != Marshal.SizeOf<FlicMipHeader>()
          || next.Width == 0 || next.Height == 0 || next.Pitch == 0 || next.Blocks == 0)
        break;
      mipHeader = next;
    }

    return texture;
  }

  // Reference (ManagerFLIC.cpp) mip count: right-shift width until 0, plus one. The mip header
  // table is therefore `log2(width) + 1` entries long, with the smallest mip clamped to 1x1.
  public static int ComputeMipCount(FlicHeader header) {
    var count = 0;
    var width = header.Width;
    var height = header.Height;
    while (width > 0 && height > 0) {
      count++;
      width >>= 1;
      height >>= 1;
    }
    return Math.Max(count, 1);
  }

  // See ManagerBTBL.cpp. Only the tiny BmpTbl{unk,count} header is inline resource data; the
  // FlicHeader array and raw pixel data are both attached to the "btbl" loader as two separate
  // extra-data chunks (chunk 0: two leading zero longs + `count` FlicHeaders; chunk 1: all mip
  // pixel bytes for every texture, concatenated in table order).
  public static Texture[] ReadBitmapTable(string name, Ovl ovl, OvlFile file, ReadOnlyMemory<byte> bytes) {
    using var headerMs = bytes.AsStream();
    using var headerReader = new BinaryReader(headerMs);
    var table = headerReader.ReadBitmapTable();
    if (table.Length == 0) return [];

    if (!ovl.TryReadExtraData(file, out var chunks) || chunks.Count < 2)
      throw new InvalidOperationException($"Failed to resolve bitmap table data for {name}");

    return DecodeBitmapTable(name, table, chunks);
  }

  // "btbl" is a loader-category tag only, never a classified symbol (Part 6 Finding 5) - unlike
  // ReadBitmapTable above (which needs an OvlFile symbol), this reads a loader instance directly by
  // its relocation-resolved data address, discovered by walking Ovl.LoaderEntriesInOrder.
  internal static Texture[] ReadBitmapTableAt(string name, Ovl ovl, uint dataAddress) {
    if (!ovl.TryReadBytes(dataAddress, 8, out var headerBytes)) return [];
    using var headerMs = new ReadOnlyMemory<byte>(headerBytes).AsStream();
    using var headerReader = new BinaryReader(headerMs);
    var table = headerReader.ReadBitmapTable();
    if (table.Length == 0) return [];

    if (!ovl.TryReadExtraData(dataAddress, out var chunks) || chunks.Count < 2)
      throw new InvalidOperationException($"Failed to resolve bitmap table data at {dataAddress:X}");

    return DecodeBitmapTable(name, table, chunks);
  }

  private static Texture[] DecodeBitmapTable(string name, BitmapTable table, IReadOnlyList<byte[]> chunks) {
    using var headersMs = new ReadOnlyMemory<byte>(chunks[0]).AsStream();
    using var headersReader = new BinaryReader(headersMs);
    headersReader.BaseStream.Position += 8; // Two leading zero longs

    using var pixelsMs = new ReadOnlyMemory<byte>(chunks[1]).AsStream();
    using var pixelsReader = new BinaryReader(pixelsMs);

    var textures = new Texture[table.Length];
    for (var i = 0; i < table.Length; i++) {
      var flic = headersReader.ReadFlicHeader();
      var mipCount = Convert.ToUInt32(ComputeMipCount(flic));
      textures[i] = new Texture(name, flic.Format, flic.Width, flic.Height, mipCount);

      // Reference (rct3tex.cpp::ReadTextures) sizes each mip independently. The base W/H are
      // already in pixels; for DXT they are first divided by 4 to get block counts, then
      // halved per level (clamped to 1). `num` is the per-block byte size for compressed formats
      // (BitsPerPixel()/8 truncates to 0 for Dxt1's 4 bits/pixel, so it can't be used here), or
      // the per-pixel byte width for uncompressed formats.
      var num = flic.Format.IsCompressed() ? flic.Format.BlockSize() : flic.Format.BitsPerPixel() / 8;
      var baseW = flic.Width;
      var baseH = flic.Height;
      if (flic.Format.IsCompressed()) {
        baseW = Math.Max(1u, baseW / 4);
        baseH = Math.Max(1u, baseH / 4);
      }
      for (var mip = 0; mip < mipCount; mip++) {
        var w = Math.Max(1u, baseW >> mip);
        var h = Math.Max(1u, baseH >> mip);
        var size = Convert.ToInt32(w * h * num);
        var data = pixelsReader.ReadBytes(size);
        if (data.Length != size)
          throw new InvalidDataException(
            $"'{name}' bitmap table entry {i} mip {mip} truncated: needed {size} bytes, got {data.Length}");
        var pixelWidth = Convert.ToInt32(Math.Max(1u, flic.Width >> mip));
        var pixelHeight = Convert.ToInt32(Math.Max(1u, flic.Height >> mip));
        ReadOnlySpan<byte> span = data;
        textures[i].MipLevels[mip] = span.ToImage(flic.Format, pixelWidth, pixelHeight);
      }
    }

    return textures;
  }
}
