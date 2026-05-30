// FlexiTexture
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Diagnostics;
using CommunityToolkit.HighPerformance;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenCobra.OVL.Files;

public record struct FlexiTexture(Recolorable Recolorable, Image<Rgba32> Texture);

public record struct FlexiTextureList(uint Fps, FlexiTexture[] Frames) {
  public readonly int Width => Frames[0].Texture.Width;
  public readonly int Height => Frames[0].Texture.Height;
  public readonly Recolorable Recolorable => Frames[0].Recolorable;
  public readonly int Length => Frames.Length;
  public readonly FlexiTexture this[int index] => Frames[index];

  public static FlexiTextureList Load(Ovl ovl, OvlFile file) {
    var bytes = ovl.ReadResource(file) ??
      throw new InvalidOperationException($"Resource '{file.Name}' not found in OVL.");

    using var ms = new ReadOnlyMemory<byte>(bytes).AsStream();
    using var reader = new BinaryReader(ms);

    var scale = reader.ReadUInt32();
    var width = reader.ReadUInt32();
    var height = reader.ReadUInt32();
    var fps = reader.ReadUInt32();
    var recolorable = (Recolorable)reader.ReadUInt32();
    var offsetCount = reader.ReadUInt32();
    var offsets = new uint[offsetCount];
    for (var i = 0; i < offsetCount; i++) {
      offsets[i] = reader.ReadUInt32();
    }
    var frameCount = reader.ReadUInt32();
    _ = reader.ReadUInt32();
    _ = reader.ReadUInt32();
    var frames = new FlexiTexture[frameCount];
    for (var i = 0; i < frameCount; i++) {
      Debug.Assert(scale == reader.ReadUInt32());
      Debug.Assert(width == reader.ReadUInt32());
      Debug.Assert(height == reader.ReadUInt32());
      Debug.Assert(Convert.ToUInt32(recolorable) == reader.ReadUInt32());
      var palette = reader.ReadBytes(Convert.ToInt32(256 * 4)); // 256 × 4 (BGRA)
      var texture = reader.ReadBytes(Convert.ToInt32(width * height));
      // FIXME: var alpha = reader.ReadBytes(Convert.ToInt32(width * height));
      var alpha = new byte[width * height];
      Array.Fill(alpha, (byte) 255);

      var rgbaTexture = PaletteConverter.ConvertIndexedBgraToRgba(width, height, palette, texture, alpha);
      frames[i] = new FlexiTexture(
        recolorable, Image.LoadPixelData<Rgba32>(rgbaTexture, Convert.ToInt32(width), Convert.ToInt32(height))
      );
    }

    return new FlexiTextureList(fps, frames);
  }
}
