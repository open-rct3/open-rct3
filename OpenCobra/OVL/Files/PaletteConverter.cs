// PaletteConverter
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using SixLabors.ImageSharp.PixelFormats;

namespace OpenCobra.OVL.Files;

public static class PaletteConverter {
  public static Span<Rgba32> ConvertIndexedBgraToRgba(
    uint width, uint height,
    ReadOnlySpan<byte> palette,
    ReadOnlySpan<byte> indexedPixels,
    ReadOnlySpan<byte> alphaValues
  ) {
    const int paletteSize = 256 * 4;
    var pixelCount = Convert.ToUInt64(width) * Convert.ToUInt64(height);
    if (pixelCount > int.MaxValue)
      throw new InvalidDataException("Indexed texture dimensions overflow the supported pixel count.");

    var length = Convert.ToInt32(pixelCount);
    if (palette.Length < paletteSize)
      throw new InvalidDataException("Indexed texture palette is truncated.");
    if (indexedPixels.Length != length)
      throw new InvalidDataException("Indexed texture pixel data length does not match its dimensions.");
    if (!alphaValues.IsEmpty && alphaValues.Length != length)
      throw new InvalidDataException("Indexed texture alpha length does not match its dimensions.");

    var texture = new Rgba32[length];
    var stride = 4;
    foreach (var i in Enumerable.Range(0, indexedPixels.Length)) {
      var index = indexedPixels[i];
      texture[i] = new Rgba32(
        // BGRA to RGBA conversion
        b: palette[index * stride + 0],
        g: palette[index * stride + 1],
        r: palette[index * stride + 2],
        a: alphaValues.IsEmpty ? byte.MaxValue : alphaValues[i]
      );
    }

    return texture;
  }
}
