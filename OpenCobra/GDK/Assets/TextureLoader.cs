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
    return LoadFlexiTexture(ovl, ovl.Find(name, FileType.FlexibleTexture) ?? throw new AssetException($"Flexi-texture '{name}' not found in OVL."));
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

    using var img = FlexiTexture.ToBitmap(bytes);
    var pixels = new Rgba32[img.Width * img.Height];
    img.CopyPixelDataTo(pixels.AsSpan());
    var hasAlpha = Array.Exists(pixels, p => p.A < 255);

    return new Texture(file.Name) {
      Width = img.Width,
      Height = img.Height,
      Pixels = pixels,
      HasAlpha = hasAlpha
    };
  }
}
