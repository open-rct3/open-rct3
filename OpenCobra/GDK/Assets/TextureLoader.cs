// TextureLoader
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using OpenCobra.GDK.Materials;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;

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
    throw new NotImplementedException();
  }
}
