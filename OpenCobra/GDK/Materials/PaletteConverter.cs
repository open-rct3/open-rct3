// PaletteConverter
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using SixLabors.ImageSharp.PixelFormats;

namespace OpenCobra.GDK.Materials;

public static class PaletteConverter {
  public static void ConvertIndexedToRgba(
    ReadOnlySpan<byte> indexedPixels,
    int width, int height,
    ReadOnlySpan<byte> palette,
    ReadOnlySpan<byte> alphaPixels,
    Span<Rgba32> outputRgba
  ) {
    int pixelCount = width * height;
    bool hasAlpha = alphaPixels.Length > 0;
    bool hasPalette = palette.Length > 0;
    int stride = palette.Length >= 1024 ? 4 : 3;

    for (int i = 0; i < pixelCount; i++) {
      byte index = indexedPixels[i];
      if (hasPalette) {
        outputRgba[i] = new Rgba32(
          r: palette[index * stride + 0],
          g: palette[index * stride + 1],
          b: palette[index * stride + 2],
          a: hasAlpha ? alphaPixels[i] : (stride == 4 ? palette[index * stride + 3] : Convert.ToByte(255))
        );
      } else {
        outputRgba[i] = new Rgba32(0, 0, 0, hasAlpha ? alphaPixels[i] : Convert.ToByte(255));
      }
    }
  }
}
