// TextureLoader
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using NLog;
using OpenCobra.GDK.Materials;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

using Texture = OpenCobra.GDK.Materials.Texture;

namespace OpenCobra.GDK.Assets;

public static class TextureLoader {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();

  public static Texture LoadTexture(string ovlPath, string name) {
    if (!File.Exists(ovlPath)) throw new FileNotFoundException(ovlPath);
    using var ovl = Ovl.Load(ovlPath);
    var file = ovl.Find(name, FileType.Texture);
    var flexiFile = ovl.Find(name, FileType.FlexibleTexture);
    if (file != null && flexiFile != null)
      logger.Warn("'{Name}' resolves to both a Texture and a FlexibleTexture symbol; using the Texture.", name);

    var resolved = file ?? flexiFile ??
      throw new AssetException($"Texture '{name}' not found in OVL.");
    return LoadTexture(ovl, resolved);
  }

  public static Texture LoadTexture(Ovl ovl, OvlFile file) {
    try {
      switch (file.Type) {
        case FileType.Texture: {
          var bitmapTablesByFlicAddress = TextureDecoding.BuildBitmapTablesByFlicAddress(ovl);
          if (!ovl.TryGetDataPointer(file, out var texAddress))
            throw new InvalidOperationException($"Failed to resolve data pointer for {file.Name}");
          var bytes = ovl.ReadResource(file) ??
            throw new InvalidOperationException($"Resource '{file.Name}' not found in OVL.");
          var texture = TextureDecoding.ReadTexture(file.ToString(), ovl, texAddress, bytes, bitmapTablesByFlicAddress) ??
            throw new InvalidOperationException($"'{file.Name}' has no backing pixel data.");
          return ToGl(texture);
        }
        case FileType.FlexibleTexture: {
          var collection = FlexiTextureList.Load(ovl, file);
          Texture? result = null;
          for (var i = 0; i < collection.Count; i++) {
            var ovlTexture = collection[i];
            var animation = i == 0
              ? new Animation(collection.Fps, Convert.ToInt32(ovlTexture.Width), Convert.ToInt32(ovlTexture.Height), collection.Count)
              : (Animation?)null;
            var frameTexture = ToGl(ovlTexture, animation);
            if (result == null) {
              result = frameTexture;
            } else {
              // Flatten every frame into a single GDK Texture with Frames.Count == collection.Count.
              result = new Texture(result.Name, result.Width, result.Height, result.Pixels, result.Recolorable) {
                Format = result.Format,
                Frames = [.. result.Frames, .. frameTexture.Frames],
                Animation = result.Animation,
              };
            }
          }
          return result ?? throw new InvalidOperationException($"'{file.Name}' has no decoded frames.");
        }
        default:
          throw new NotSupportedException($"Cannot load a Texture from a '{file.Type}' symbol.");
      }
    } catch (Exception ex) {
      throw new AssetException(file.Name, ex);
    }
  }

  /// <summary>
  /// Converts a decoded OVL <see cref="OVL.Files.Texture"/> into a GPU-resident GDK
  /// <see cref="Texture"/>, copying every mip image so the GDK texture owns its data outright.
  /// This sidesteps the <see cref="OVL.Files.Texture.WithName"/> shared-<c>MipLevels</c>
  /// double-free risk: disposing the GDK texture never collides with a still-live OVL texture.
  /// </summary>
  internal static Texture ToGl(OVL.Files.Texture src, Animation? animation = null) {
    var mips = new Image<Rgba32>[src.MipLevels.Length];
    for (var i = 0; i < mips.Length; i++) {
      var srcMip = src.MipLevels[i]
        ?? throw new InvalidOperationException($"Texture '{src.Name}' has no decoded mip {i}");
      // Image.Clone shares the buffer but is an independent disposable instance.
      mips[i] = srcMip.Clone();
    }
    var frames = new[] { new MipChain(mips) };
    return new Texture(src.Name, Convert.ToInt32(src.Width), Convert.ToInt32(src.Height), mips[0], src.Recolorable) {
      Format = src.Format,
      Frames = frames,
      Animation = animation,
    };
  }
}
