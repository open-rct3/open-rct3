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
  private static readonly TerrainTextureCatalogCache terrainCatalogs =
    new(LoadTerrainCatalogUncached);
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
  /// Loads and caches the numerically ordered terrain/cliff texture catalog from a paired OVL.
  /// The same live catalog is returned for equivalent paths until it is disposed.
  /// </summary>
  public static TerrainTextureCatalog LoadTerrainCatalog(string ovlPath) =>
    terrainCatalogs.Get(GetTerrainCommonPath(ovlPath));

  public static Texture LoadFlexiTexture(string ovlPath, string name) {
    if (!File.Exists(ovlPath)) throw new FileNotFoundException(ovlPath);
    using var ovl = Ovl.Load(ovlPath);
    return LoadTexture(ovl, ovl.Find(name, FileType.FlexibleTexture) ??
      throw new AssetException($"Flexi-texture '{name}' not found in OVL."));
  }

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

  private static TerrainTextureCatalog LoadTerrainCatalogUncached(string commonPath) {
    var uniquePath = GetPairedPath(commonPath, ".unique.ovl");
    if (!File.Exists(commonPath))
      throw new FileNotFoundException($"Terrain common OVL not found: {commonPath}", commonPath);
    if (!File.Exists(uniquePath))
      throw new FileNotFoundException($"Terrain unique OVL not found: {uniquePath}", uniquePath);

    try {
      using var ovl = Ovl.Load(commonPath);
      var files = ovl.Keys
        .Where(file => file.Type == FileType.Texture
          && TerrainTextureCatalog.IsCatalogAssetName(file.Name))
        .OrderBy(file => file.Name, StringComparer.Ordinal)
        .ToArray();
      if (files.Length == 0)
        throw new InvalidDataException(
          "The paired OVL contains no Terrain_XX or TerrainCliffN textures.");

      using var decodedTextures = Textures.Extract(ovl);
      var decodedNames = decodedTextures.Names.ToHashSet(StringComparer.Ordinal);
      var textures = new List<Texture>(files.Length);
      try {
        foreach (var file in files) {
          var decodedName = file.ToString();
          if (!decodedNames.Contains(decodedName))
            throw new InvalidDataException($"Texture '{decodedName}' failed to decode.");
          textures.Add(CreateTexture(file, decodedTextures[decodedName]));
        }
        return new TerrainTextureCatalog(textures);
      } catch {
        foreach (var texture in textures) texture.Dispose();
        throw;
      }
    } catch (AssetException) {
      throw;
    } catch (Exception ex) {
      throw new AssetException(Path.GetFileName(commonPath), ex);
    }
  }

  private static string GetTerrainCommonPath(string ovlPath) {
    ArgumentException.ThrowIfNullOrWhiteSpace(ovlPath);
    var fullPath = Path.GetFullPath(ovlPath);
    var fileName = Path.GetFileName(fullPath);
    if (!fileName.EndsWith(".common.ovl", StringComparison.OrdinalIgnoreCase)
        && !fileName.EndsWith(".unique.ovl", StringComparison.OrdinalIgnoreCase))
      throw new ArgumentException(
        "Terrain OVL path must end with '.common.ovl' or '.unique.ovl'.", nameof(ovlPath));

    var commonPath = GetPairedPath(fullPath, ".common.ovl");
    var uniquePath = GetPairedPath(fullPath, ".unique.ovl");
    if (!File.Exists(commonPath))
      throw new FileNotFoundException($"Terrain common OVL not found: {commonPath}", commonPath);
    if (!File.Exists(uniquePath))
      throw new FileNotFoundException($"Terrain unique OVL not found: {uniquePath}", uniquePath);
    return commonPath;
  }

  private static string GetPairedPath(string ovlPath, string suffix) {
    var directory = Path.GetDirectoryName(ovlPath) ?? string.Empty;
    var fileName = Path.GetFileName(ovlPath);
    var pairMarker = fileName.EndsWith(".common.ovl", StringComparison.OrdinalIgnoreCase)
      ? ".common.ovl"
      : ".unique.ovl";
    return Path.Combine(directory, fileName[..^pairMarker.Length] + suffix);
  }

  private static Texture CreateTexture(OvlFile file, OpenCobra.OVL.Files.Texture decoded) {
    if (decoded.MipLevels.Length == 0 || decoded.MipLevels[0] == null)
      throw new InvalidDataException($"Texture '{file}' has no decoded base mip.");

    return ToGl(decoded);
  }
}
