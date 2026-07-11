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

/// <summary>
/// A single flexi-texture frame's raw, already relocation-resolved bytes: palette (256 × 4 BGRA),
/// indexed pixel data, and an optional alpha mask. Produced by <see cref="FlexiTextureList.Load"/>
/// (the only place that talks to <see cref="Ovl"/>'s relocation table) and consumed by
/// <see cref="FlexiTextureList.Parse"/> (the decode/naming logic, which has no dependency on
/// relocation resolution and so is unit-testable without a real OVL archive).
/// </summary>
internal readonly record struct FlexiFrameData(
  Recolorable Recolorable, ReadOnlyMemory<byte> Palette, ReadOnlyMemory<byte> Texture, ReadOnlyMemory<byte> Alpha
);

public static class FlexiTextureList {
  // See FlexiTextureInfoStruct/FlexiTextureStruct in flexitexture.h and ManagerFTX.cpp.
  public static TextureCollection Load(Ovl ovl, OvlFile file) {
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

    var name = file.ToString();
    const int frameStructSize = 28; // scale, width, height, Recolorable, palette*, texture*, alpha*
    var frames = new FlexiFrameData[frameCount];
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
      var palette = new ReadOnlyMemory<byte>(paletteBlock, Convert.ToInt32(paletteOffset), 256 * 4); // 256 × 4 (BGRA)

      if (!ovl.TryResolveRelocation(texturePtr, out var textureBlock, out var textureOffset))
        throw new InvalidOperationException($"Could not resolve FlexiTexture pixel data for '{file.Name}' frame {i}.");
      var texture = new ReadOnlyMemory<byte>(textureBlock, Convert.ToInt32(textureOffset), Convert.ToInt32(width * height));

      ReadOnlyMemory<byte> alpha;
      if (alphaPtr != 0 && ovl.TryResolveRelocation(alphaPtr, out var alphaBlock, out var alphaOffset)) {
        alpha = new ReadOnlyMemory<byte>(alphaBlock, Convert.ToInt32(alphaOffset), Convert.ToInt32(width * height));
      } else {
        // A null alpha pointer means the frame has no alpha mask: fully opaque.
        var opaque = new byte[width * height];
        Array.Fill(opaque, (byte) 255);
        alpha = opaque;
      }

      frames[i] = new FlexiFrameData(recolorable, palette, texture, alpha);
    }

    return Parse(name, fps, width, height, frames);
  }

  // Pure decode/naming logic: no dependency on Ovl or relocation resolution, so it's directly
  // unit-testable with synthetic FlexiFrameData rather than a real OVL archive.
  internal static TextureCollection Parse(
    string name, uint fps, uint width, uint height, IReadOnlyList<FlexiFrameData> frameData
  ) {
    var frames = new Texture[frameData.Count];
    for (var i = 0; i < frameData.Count; i++) {
      var data = frameData[i];
      var rgbaTexture = PaletteConverter.ConvertIndexedBgraToRgba(
        width, height, data.Palette.Span, data.Texture.Span, data.Alpha.Span
      );
      var image = Image.LoadPixelData<Rgba32>(rgbaTexture, Convert.ToInt32(width), Convert.ToInt32(height));

      // Single-frame ftx keeps the symbol's own name; multi-frame gets a "#i" suffix (same rule
      // as Textures.Extract used before this collapsed into TextureCollection).
      var frameName = frameData.Count == 1 ? name : $"{name}#{i}";
      frames[i] = new Texture(
        frameName, TextureFormat.A8R8G8B8, width, height, mipCount: 1, data.Recolorable
      ) {
        MipLevels = { [0] = image },
      };
    }

    return new TextureCollection(frames, fps);
  }
}
