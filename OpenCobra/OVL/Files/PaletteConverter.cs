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
    var texture = new Rgba32[width * height];
    var stride = 4;

    for (var i = 0; i < indexedPixels.Length; i += 1) {
      var index = indexedPixels[i];
        texture[i] = new Rgba32(
          // BGRA to RGBA conversion
          b: palette[index * stride + 0],
          g: palette[index * stride + 1],
          r: palette[index * stride + 2],
          a: alphaValues[i]
        );
      }

    return texture;
  }
}
