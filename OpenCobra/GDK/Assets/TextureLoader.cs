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

public static class TextureLoader {
  public static Texture LoadTexture(string ovlPath, string name) {
    if (!File.Exists(ovlPath)) throw new FileNotFoundException(ovlPath);
    using var ovl = Ovl.Load(ovlPath);
    return LoadTexture(ovl, ovl.Find(name, FileType.Texture) ?? throw new AssetException($"Texture '{name}' not found in OVL."));
  }

  public static Texture LoadTexture(Ovl ovl, OvlFile file) {
    try {
      var size = ovl[file].Size;
      throw new NotImplementedException($"TODO: Load {file.Name}.{file.Type.ToTagString()} ({size} bytes) from  OVL.");
    }
    catch (Exception ex) {
      throw new AssetException(file.Name, ex);
    }
  }

  public static Texture LoadFlexiTexture(string ovlPath, string name) {
    if (!File.Exists(ovlPath)) throw new FileNotFoundException(ovlPath);
    using var ovl = Ovl.Load(ovlPath);

    var textures = FlexiTextureList.Load(ovl, ovl.Find(name, FileType.FlexibleTexture) ??
      throw new AssetException($"Flexi-texture '{name}' not found in OVL."));
    return new Texture(name, textures.Width, textures.Height, textures[0].Texture, textures.Recolorable);
  }

  public static AnimatedTexture LoadAnimatedTexture(string ovlPath, string name) {
    if (!File.Exists(ovlPath)) throw new FileNotFoundException(ovlPath);
    using var ovl = Ovl.Load(ovlPath);

    var textures = FlexiTextureList.Load(ovl, ovl.Find(name, FileType.FlexibleTexture) ??
      throw new AssetException($"Flexi-texture '{name}' not found in OVL."));
    return new AnimatedTexture(name, textures);
  }
}
