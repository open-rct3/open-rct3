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
    for (int i = 0; i < pixelCount; i++) {
      byte index = indexedPixels[i];
      int dst = i * 4;
      outputRgba[dst + 0] = new Rgba32(
        r: palette[index * 3 + 0],
        g: palette[index * 3 + 1],
        b: palette[index * 3 + 2],
        a: hasAlpha ? alphaPixels[i] : (byte)255
      );
    }
  }
}
