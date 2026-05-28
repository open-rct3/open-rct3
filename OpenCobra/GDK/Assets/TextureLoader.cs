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

internal record FlexiTextureData(
  uint Scale, uint Width, uint Height,
  Recolorable Recolorable,
  byte[] Palette,
  byte[] TextureData,
  byte[] AlphaData
);

public class TextureLoader {
  public static Texture? LoadTexture(string ovlPath, string? name = null) {
    if (!File.Exists(ovlPath)) throw new FileNotFoundException(ovlPath);
    using var ovl = Ovl.Load(ovlPath);
    return LoadTexture(ovl, ovl.Find(name) ?? throw new AssetException($"Texture '{name}' not found in OVL."));
  }

  public static Texture? LoadTexture(Ovl ovl, OvlFile file) {
    try {
      return LoadTextureFromOvl(file, ovl[file]);
    }
    catch (Exception ex) {
      throw new AssetException($"{AssetException.MessagePrefix}: {file.Name}", ex);
    }
  }

  private static Texture? LoadTextureFromOvl(OvlFile file, OvlEntry entry) {
    var size = entry.Size;
    throw new NotImplementedException($"TODO: Load {file.Name}.{file.Type.ToTagString()} ({size}) from  OVL.");
  }

  public static Texture? LoadFlexiTexture(string ovlPath, string? name = null) {
    if (!File.Exists(ovlPath)) throw new FileNotFoundException(ovlPath);
    using var ovl = Ovl.Load(ovlPath);
    return LoadFlexiTexture(ovl, ovl.Find(name) ?? throw new AssetException($"Flexi-texture '{name}' not found in OVL."));
  }

  public static Texture? LoadFlexiTexture(Ovl ovl, OvlFile file) {
    try {
      return LoadFlexiTextureFromOvl(file, ovl[file]);
    }
    catch (Exception ex) {
      throw new AssetException($"{AssetException.MessagePrefix}: {file.Name}", ex);
    }
  }

  private static Texture? LoadFlexiTextureFromOvl(OvlFile file, OvlEntry entry) {
    var bytes = Ovl.Load(file.Path).ReadResource(file);
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
      ? reader.ReadBytes(768) // 256 * 3 (RGB)
      : [];

    var pixelCount = Convert.ToInt32(width * height);
    var textureData = reader.ReadBytes(pixelCount);
    // Some flexi-textures have an alpha channel at the end
    var alphaData = ms.Position < ms.Length ? reader.ReadBytes(pixelCount) : [];

    return ConvertFlexiToGdkTexture(file.Name, new FlexiTextureData(
      scale, width, height, recolorable,
      palette, textureData, alphaData
    ));
  }

  private static Texture ConvertFlexiToGdkTexture(string name, FlexiTextureData flexi) {
    var width = Convert.ToInt32(flexi.Width);
    var height = Convert.ToInt32(flexi.Height);
    var pixels = new Rgba32[width * height * 4];

    PaletteConverter.ConvertIndexedToRgba(
      flexi.TextureData, width, height, flexi.Palette, flexi.AlphaData, pixels
    );

    return new(name) {
      Width = width,
      Height = height,
      Pixels = pixels,
      HasAlpha = flexi.AlphaData.Length > 0
    };
  }
}
