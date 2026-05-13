// PaletteConverter
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using SixLabors.ImageSharp.PixelFormats;

namespace OpenCobra.GDK.Materials;

public static class PaletteConverter {
  public static Span<Rgba32> ConvertIndexedToRgba(
    ReadOnlySpan<byte> indexedPixels,
    int width, int height,
    ReadOnlySpan<byte> palette,
    ReadOnlySpan<byte> alphaPixels
  ) => ConvertIndexedToRgba(new Rgba32[width * height], indexedPixels, width, height, palette, alphaPixels);

  internal static Span<Rgba32> ConvertIndexedToRgba(
    Span<Rgba32> destination,
    ReadOnlySpan<byte> indexedPixels,
    int width, int height,
    ReadOnlySpan<byte> palette,
    ReadOnlySpan<byte> alphaPixels
  ) {
    int pixelCount = width * height;
    bool hasAlpha = alphaPixels.Length > 0;
    bool hasPalette = palette.Length > 0;
    int stride = palette.Length >= 1024 ? 4 : 3;
    Debug.Assert(destination.Length == pixelCount);

    for (int i = 0; i < pixelCount; i++) {
      byte index = indexedPixels[i];
      if (hasPalette) {
        destination[i] = new Rgba32(
          r: palette[index * stride + 0],
          g: palette[index * stride + 1],
          b: palette[index * stride + 2],
          a: hasAlpha ? alphaPixels[i] : (stride == 4 ? palette[index * stride + 3] : Convert.ToByte(255))
        );
      }
      else {
        destination[i] = new Rgba32(0, 0, 0, hasAlpha ? alphaPixels[i] : Convert.ToByte(255));
      }
    }

    return destination;
  }
}
