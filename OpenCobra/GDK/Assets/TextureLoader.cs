// TextureLoader
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using OpenCobra.GDK.Materials;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using OpenCobra.Textures;

namespace OpenCobra.GDK.Assets;

public class TextureLoader {
  public static Texture? LoadTexture(string ovlPath, string? name = null) {
    if (!System.IO.File.Exists(ovlPath)) throw new FileNotFoundException(ovlPath);

    using var ovl = Ovl.Load(ovlPath);
    return LoadTexture(ovl, name);
  }

  public static Texture? LoadTexture(Ovl ovl, string? name = null) {
    try {
      var entry = ovl.LoaderEntries.FirstOrDefault(e =>
        e.Tag == FileType.Texture.ToTagString() &&
        (name == null || e.SymbolName.Contains(name, StringComparison.OrdinalIgnoreCase))
      );

      return LoadTextureFromOvl(ovl, entry);
    } catch (Exception ex) {
      throw new AssetException(ex);
    }
  }

  private static Texture? LoadTextureFromOvl(Ovl ovl, OvlLoaderEntry entry) {
    throw new NotImplementedException();
  }

  public static Texture? LoadFlexiTexture(string ovlPath, string? name = null) {
    if (!System.IO.File.Exists(ovlPath)) throw new FileNotFoundException(ovlPath);

    using var ovl = Ovl.Load(ovlPath);
    return LoadFlexiTexture(ovl, name);
  }

  public static Texture? LoadFlexiTexture(Ovl ovl, string? name = null) {
    try {
      var entry = ovl.LoaderEntries.FirstOrDefault(e =>
        e.Tag == FileType.FlexibleTexture.ToTagString() &&
        (name == null || e.SymbolName.Contains(name, StringComparison.OrdinalIgnoreCase))
      );

      return LoadFlexiTextureFromOvl(ovl, entry);
    } catch (Exception ex) {
      throw new AssetException(ex);
    }
  }

  private static Texture? LoadFlexiTextureFromOvl(Ovl ovl, OvlLoaderEntry entry) {
    var bytes = ovl.GetResourceBytes(entry);
    if (bytes == null) return null;

    using var ms = new MemoryStream(bytes);
    using var reader = new BinaryReader(ms);

    // Parse FlexiTextureData
    // Structure (20 bytes header + palette + texture + alpha):
    // uint scale (4)
    // uint width (4)
    // uint height (4)
    // uint recolorable (4)
    // uint hasPalette (4) - usually 1 if it has palette

    var scale = reader.ReadUInt32();
    var width = reader.ReadUInt32();
    var height = reader.ReadUInt32();
    var recolorable = (Recolorable)reader.ReadUInt32();
    var hasPalette = reader.ReadUInt32() != 0;

    byte[] palette = [];
    if (hasPalette) {
      palette = reader.ReadBytes(768); // 256 * 3 (RGB)
    }

    var pixelCount = (int)(width * height);
    var textureData = reader.ReadBytes(pixelCount);

    // Some flexi-textures have an alpha channel at the end
    byte[] alphaData = [];
    if (ms.Position < ms.Length) {
      alphaData = reader.ReadBytes(pixelCount);
    }

    var flexi = new FlexiTextureData {
      scale = scale,
      width = width,
      height = height,
      recolorable = recolorable,
      palette = palette,
      texture = textureData,
      alpha = alphaData
    };

    return ConvertFlexiToGdkTexture(entry.SymbolName, flexi);
  }

  private static Texture ConvertFlexiToGdkTexture(string name, FlexiTextureData flexi) {
    var width = (int)flexi.width;
    var height = (int)flexi.height;
    var pixels = new byte[width * height * 4];

    PaletteConverter.ConvertIndexedToRgba(flexi.texture, width, height, flexi.palette, flexi.alpha, pixels);

    return new Texture(name, System.Drawing.Imaging.PixelFormat.Format32bppArgb) {
      Width = width,
      Height = height,
      Pixels = pixels,
      HasAlpha = flexi.alpha.Length > 0
    };
  }
}
