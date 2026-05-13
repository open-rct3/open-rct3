// TextureLoader
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using OpenCobra.GDK.Materials;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using SixLabors.ImageSharp.PixelFormats;

namespace OpenCobra.GDK.Assets;

using Texture = Texture<Rgba32>;

internal record struct FlexiTexture(
  uint Scale, uint Width, uint Height,
  Recolorable Recolorable,
  byte[] Palette,
  byte[] Texture,
  byte[] AlphaMap
);

public class TextureLoader {
  public static Texture? LoadTexture(string ovlPath, string? name = null) {
    if (!File.Exists(ovlPath)) throw new FileNotFoundException(ovlPath);
    using var ovl = Ovl.Load(ovlPath);
    return LoadTexture(ovl, ovl.Find(name, FileType.Texture) ?? throw new AssetException($"Texture '{name}' not found in OVL."));
  }

  public static Texture? LoadTexture(Ovl ovl, OvlFile file) {
    try {
      return LoadTextureFromOvl(file, ovl[file]);
    }
    catch (Exception ex) {
      throw new AssetException(file.Name, ex);
    }
  }

  private static Texture? LoadTextureFromOvl(OvlFile file, OvlEntry entry) {
    var size = entry.Size;
    throw new NotImplementedException($"TODO: Load {file.Name}.{file.Type.ToTagString()} ({size}) from  OVL.");
  }

  public static Texture? LoadFlexiTexture(string ovlPath, string? name = null) {
    if (!File.Exists(ovlPath)) throw new FileNotFoundException(ovlPath);
    using var ovl = Ovl.Load(ovlPath);
    return LoadFlexiTexture(ovl, ovl.Find(name, FileType.FlexibleTexture) ??
      throw new AssetException($"Flexi-texture '{name}' not found in OVL."));
  }

  public static Texture? LoadFlexiTexture(Ovl ovl, OvlFile file) {
    try {
      return LoadFlexiTextureFromOvl(file, ovl[file]);
    }
    catch (Exception ex) {
      throw new AssetException(file.Name, ex);
    }
  }

  private static Texture? LoadFlexiTextureFromOvl(OvlFile file, OvlEntry entry) {
    byte[]? bytes = null;
    using (var ovl = Ovl.Load(file.Path))
      bytes = ovl.ReadResource(file);
    if (bytes == null) return null;

    using var ms = new MemoryStream(bytes);
    using var reader = new BinaryReader(ms);

    // Parse FlexiTextureData
    // Structure (20 bytes header + palette + texture + alpha):
    // uint scale (4), uint width (4), uint height (4)
    // uint recolorable (4), uint hasPalette (4) - usually 1 if it has palette
    var scale = reader.ReadUInt32();
    var width = reader.ReadUInt32();
    var height = reader.ReadUInt32();
    var recolorable = (Recolorable)reader.ReadUInt32();
    var hasPalette = reader.ReadUInt32() != 0;

    var palette = hasPalette
      ? reader.ReadBytes(1024) // 256 * 4 (RGBA)
      : [];

    // FIXME: Use spans instead of copying albedo and alpha pixel data
    var pixelCount = Convert.ToInt32(width * height);
    var textureData = reader.ReadBytes(pixelCount);
    // Some flexi-textures have an alpha channel at the end
    var alphaData = ms.Position < ms.Length ? reader.ReadBytes(pixelCount) : [];

    return ConvertFlexiToGdkTexture(file.Name, new FlexiTexture(
      scale, width, height, recolorable,
      palette, textureData, alphaData
    ));
  }

  private static Texture ConvertFlexiToGdkTexture(string name, FlexiTexture flexi) {
    var width = Convert.ToInt32(flexi.Width);
    var height = Convert.ToInt32(flexi.Height);

    var texture = new Texture(name, width, height, flexi.AlphaMap.Length > 0);
    // Perform pallete conversion in-place
    texture.Pixels.AsSpan().ConvertIndexedToRgba(flexi);

    return texture;
  }
}

internal static class SpanExtensions {
  /// <summary>
  /// Converts an indexed pixel span to RGBA format using the given flexi-texture.
  /// </summary>
  public static void ConvertIndexedToRgba(this Span<Rgba32> destination, FlexiTexture source) {
    var width = Convert.ToInt32(source.Width);
    var height = Convert.ToInt32(source.Height);

    PaletteConverter.ConvertIndexedToRgba(
      destination, source.Texture, width, height, source.Palette, source.AlphaMap
    );
  }
}
