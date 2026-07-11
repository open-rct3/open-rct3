// Flic
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2024-2026 OpenRCT3 Contributors. All rights reserved.
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;

namespace OpenCobra.OVL.Files;

// See ManagerFLIC.cpp. Decodes a "flic" loader's raw extra-data chunk into a Texture.
internal static class Flic {
  // `chunk` is a loader's raw extra-data chunk: either a 4-byte index into `table`
  // (bitmap-table-backed archives) or a standalone FlicHeader + mips + pixel data blob. Neither
  // case has a leading FlicStruct - that struct is only ever a placeholder holding zeros,
  // reachable via relocation but never used to store the extracted pixel data.
  public static Texture Read(string name, byte[] chunk, Texture[]? table = null) {
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
    var mipCount = Convert.ToUInt32(TextureDecoding.ComputeMipCount(header));
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
}
