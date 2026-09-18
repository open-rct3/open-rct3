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

  // See FlexiTextureInfoStruct/FlexiTextureStruct in flexitexture.h and ManagerFTX.cpp.
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
    // `offset1` and `fts2` are relocated pointers, not inline data: the animation frame order and
    // the per-frame FlexiTextureStruct array both live elsewhere in the archive's block data.
    var offset1Ptr = reader.ReadUInt32();
    var frameCount = reader.ReadUInt32();
    var fts2Ptr = reader.ReadUInt32();

    _ = offsetCount;
    _ = offset1Ptr;

    if (!ovl.TryResolveRelocation(fts2Ptr, out var framesBlock, out var framesOffset))
      throw new InvalidOperationException($"Could not resolve FlexiTexture frame data for '{file.Name}'.");

    const int frameStructSize = 28; // scale, width, height, Recolorable, palette*, texture*, alpha*
    var frames = new FlexiTexture[frameCount];
    for (var i = 0; i < frameCount; i++) {
      var frameOffset = Convert.ToInt32(framesOffset) + i * frameStructSize;
      using var frameMs = new ReadOnlyMemory<byte>(framesBlock, frameOffset, frameStructSize).AsStream();
      using var frameReader = new BinaryReader(frameMs);

      Debug.Assert(scale == frameReader.ReadUInt32());
      Debug.Assert(width == frameReader.ReadUInt32());
      Debug.Assert(height == frameReader.ReadUInt32());
      Debug.Assert(Convert.ToUInt32(recolorable) == frameReader.ReadUInt32());
      var palettePtr = frameReader.ReadUInt32();
      var texturePtr = frameReader.ReadUInt32();
      var alphaPtr = frameReader.ReadUInt32();

      if (!ovl.TryResolveRelocation(palettePtr, out var paletteBlock, out var paletteOffset))
        throw new InvalidOperationException($"Could not resolve FlexiTexture palette for '{file.Name}' frame {i}.");
      var palette = paletteBlock.AsSpan(Convert.ToInt32(paletteOffset), 256 * 4); // 256 × 4 (BGRA)

      if (!ovl.TryResolveRelocation(texturePtr, out var textureBlock, out var textureOffset))
        throw new InvalidOperationException($"Could not resolve FlexiTexture pixel data for '{file.Name}' frame {i}.");
      var texture = textureBlock.AsSpan(Convert.ToInt32(textureOffset), Convert.ToInt32(width * height));

      byte[] alpha;
      if (alphaPtr != 0 && ovl.TryResolveRelocation(alphaPtr, out var alphaBlock, out var alphaOffset)) {
        alpha = alphaBlock.AsSpan(Convert.ToInt32(alphaOffset), Convert.ToInt32(width * height)).ToArray();
      } else {
        // A null alpha pointer means the frame has no alpha mask: fully opaque.
        alpha = new byte[width * height];
        Array.Fill(alpha, (byte) 255);
      }

      var rgbaTexture = PaletteConverter.ConvertIndexedBgraToRgba(width, height, palette, texture, alpha);
      frames[i] = new FlexiTexture(
        recolorable, Image.LoadPixelData<Rgba32>(rgbaTexture, Convert.ToInt32(width), Convert.ToInt32(height))
      );
    }

    return new FlexiTextureList(fps, frames);
  }
}
