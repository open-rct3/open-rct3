// BitmapTable
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024-2026 OpenRCT3 Contributors. All rights reserved.
using CommunityToolkit.HighPerformance;

namespace OpenCobra.OVL.Files;

// See ManagerBTBL.cpp. Decodes a "btbl" loader into its table of Textures.
internal static class BitmapTables {
  // Only the tiny BmpTbl{unk,count} header is inline resource data; the FlicHeader array and raw
  // pixel data are both attached to the "btbl" loader as two separate extra-data chunks (chunk 0:
  // two leading zero longs + `count` FlicHeaders; chunk 1: all mip pixel bytes for every texture,
  // concatenated in table order).
  public static Texture[] Read(string name, Ovl ovl, OvlFile file, ReadOnlyMemory<byte> bytes) {
    using var headerMs = bytes.AsStream();
    using var headerReader = new BinaryReader(headerMs);
    var table = headerReader.ReadBitmapTable();
    if (table.Length == 0) return [];

    if (!ovl.TryReadExtraData(file, out var chunks) || chunks.Count < 2)
      throw new InvalidOperationException($"Failed to resolve bitmap table data for {name}");

    return Decode(name, table, chunks);
  }

  // "btbl" is a loader-category tag only, never a classified symbol (Part 6 Finding 5) - unlike
  // Read above (which needs an OvlFile symbol), this reads a loader instance directly by its
  // relocation-resolved data address, discovered by walking Ovl.LoaderEntriesInOrder.
  public static Texture[] ReadAt(string name, Ovl ovl, uint dataAddress) {
    if (!ovl.TryReadBytes(dataAddress, 8, out var headerBytes)) return [];
    using var headerMs = new ReadOnlyMemory<byte>(headerBytes).AsStream();
    using var headerReader = new BinaryReader(headerMs);
    var table = headerReader.ReadBitmapTable();
    if (table.Length == 0) return [];

    if (!ovl.TryReadExtraData(dataAddress, out var chunks) || chunks.Count < 2)
      throw new InvalidOperationException($"Failed to resolve bitmap table data at {dataAddress:X}");

    return Decode(name, table, chunks);
  }

  private static Texture[] Decode(string name, BitmapTable table, IReadOnlyList<byte[]> chunks) {
    using var headersMs = new ReadOnlyMemory<byte>(chunks[0]).AsStream();
    using var headersReader = new BinaryReader(headersMs);
    headersReader.BaseStream.Position += 8; // Two leading zero longs

    using var pixelsMs = new ReadOnlyMemory<byte>(chunks[1]).AsStream();
    using var pixelsReader = new BinaryReader(pixelsMs);

    var textures = new Texture[table.Length];
    for (var i = 0; i < table.Length; i++) {
      var flic = headersReader.ReadFlicHeader();
      // btbl.rs::decode_entry uses the on-disk FlicHeader.MipCount directly (not a derived
      // log2(width)+1 guess like the standalone-flic path) - a BTBL entry's mip loop length is
      // exactly this stored field, and the pixel chunk has exactly that many mips concatenated.
      var mipCount = flic.MipCount;
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
