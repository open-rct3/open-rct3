// FlexiTexture
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenCobra.OVL.Files;

/// <summary>Decodes a Flexi-Texture (FTX) resource blob into an <see cref="Image{Rgba32}"/>.</summary>
public static class FlexiTexture {
  /// <summary>
  /// Decode raw FTX resource bytes into an image.
  /// </summary>
  /// <remarks>
  /// <see cref="Ovl.GetResourceBytes"/>
  /// <para>Layout:</para>
  /// <ul>
  /// <li>scale(u32), width(u32)
  /// <li>height(u32)</li>
  /// <li>recolorable(u32)</li>
  /// <li>hasPalette(u32)</li>
  /// <li>[palette: 256×COLOURQUAD(RGBA) if hasPalette≠0]</li>
  /// <li>pixels[width×height]</li>
  /// <li>[alpha[width×height]]</li>
  /// </ul>
  /// </remarks>
  public static Image<Rgba32> ToBitmap(byte[] bytes) {
    if (bytes.Length < 20)
      throw new ArgumentException("FTX data too short to contain a valid header.", nameof(bytes));

    var width       = (int) BitConverter.ToUInt32(bytes, 4);
    var height      = (int) BitConverter.ToUInt32(bytes, 8);
    var hasPalette  = BitConverter.ToUInt32(bytes, 16) != 0;

    var offset = 20;
    const int paletteBytes = 256 * 4; // COLOURQUAD: R, G, B, A

    byte[]? palette = null;
    if (hasPalette) {
      if (bytes.Length < offset + paletteBytes)
        throw new ArgumentException("FTX data too short to contain the palette.", nameof(bytes));
      palette = bytes[offset..(offset + paletteBytes)];
      offset += paletteBytes;
    }

    var pixelCount = width * height;
    if (bytes.Length < offset + pixelCount)
      throw new ArgumentException("FTX data too short to contain pixel data.", nameof(bytes));
    var pixels = bytes[offset..(offset + pixelCount)];
    offset += pixelCount;

    byte[]? alpha = null;
    if (bytes.Length >= offset + pixelCount)
      alpha = bytes[offset..(offset + pixelCount)];

    var img = new Image<Rgba32>(Math.Max(width, 1), Math.Max(height, 1));
    if (width == 0 || height == 0) return img;

    img.ProcessPixelRows(accessor => {
      for (var y = 0; y < height; y++) {
        var row = accessor.GetRowSpan(y);
        for (var x = 0; x < width; x++) {
          var i = y * width + x;
          byte r = 0, g = 0, b = 0, a = 255;
          if (palette != null) {
            var p = pixels[i] * 4;
            r = palette[p];
            g = palette[p + 1];
            b = palette[p + 2];
            a = palette[p + 3];
          }
          if (alpha != null) a = alpha[i];
          row[x] = new Rgba32(r, g, b, a);
        }
      }
    });

    return img;
  }
}
