// Terrain Texture Catalog
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using OpenCobra.GDK.Materials;

[assembly: InternalsVisibleTo("Tests")]

namespace OpenCobra.GDK.Assets;

/// <summary>
/// Stable, numerically ordered terrain and cliff textures decoded from a paired terrain OVL.
/// </summary>
public sealed class TerrainTextureCatalog : IDisposable {
  public const int SurfaceCount = 26;
  public const int CliffCount = 6;
  internal const string SurfacePrefix = "Terrain_";
  internal const string CliffPrefix = "TerrainCliff";

  private readonly ReadOnlyCollection<Texture> surfaceTextures;
  private readonly ReadOnlyCollection<Texture> cliffTextures;
  private readonly ReadOnlyCollection<string> surfaceNames;
  private readonly ReadOnlyCollection<string> cliffNames;
  private Action<TerrainTextureCatalog>? disposalCallback;
  private int disposed;

  internal TerrainTextureCatalog(IEnumerable<Texture> textures) {
    var ownedTextures = textures.ToArray();
    try {
      var surfaces = OrderAndValidate(
        ownedTextures, SurfacePrefix, "surface", SurfaceCount);
      var cliffs = OrderAndValidate(
        ownedTextures, CliffPrefix, "cliff", CliffCount);
      surfaceTextures = Array.AsReadOnly(surfaces);
      cliffTextures = Array.AsReadOnly(cliffs);
      surfaceNames = Array.AsReadOnly(surfaces.Select(texture => texture.Name).ToArray());
      cliffNames = Array.AsReadOnly(cliffs.Select(texture => texture.Name).ToArray());
    } catch {
      foreach (var texture in ownedTextures) texture.Dispose();
      throw;
    }
  }

  /// <summary>Surface textures ordered by their numeric <c>Terrain_XX</c> suffix.</summary>
  public IReadOnlyList<Texture> SurfaceTextures {
    get {
      ThrowIfDisposed();
      return surfaceTextures;
    }
  }

  /// <summary>Cliff textures ordered by their numeric <c>TerrainCliffN</c> suffix.</summary>
  public IReadOnlyList<Texture> CliffTextures {
    get {
      ThrowIfDisposed();
      return cliffTextures;
    }
  }

  /// <summary>Surface resource names in index order.</summary>
  public IReadOnlyList<string> SurfaceNames {
    get {
      ThrowIfDisposed();
      return surfaceNames;
    }
  }

  /// <summary>Cliff resource names in index order.</summary>
  public IReadOnlyList<string> CliffNames {
    get {
      ThrowIfDisposed();
      return cliffNames;
    }
  }

  /// <summary>Gets the texture addressed by a terrain cell's surface index.</summary>
  public Texture GetSurface(byte index) {
    ThrowIfDisposed();
    if (index >= surfaceTextures.Count)
      throw new ArgumentOutOfRangeException(
        nameof(index), index,
        $"Surface index {index} is out of range for {surfaceTextures.Count} catalog entries.");
    return surfaceTextures[index];
  }

  /// <summary>Gets the texture addressed by a terrain cell's cliff index.</summary>
  public Texture GetCliff(byte index) {
    ThrowIfDisposed();
    if (index >= cliffTextures.Count)
      throw new ArgumentOutOfRangeException(
        nameof(index), index,
        $"Cliff index {index} is out of range for {cliffTextures.Count} catalog entries.");
    return cliffTextures[index];
  }

  internal static bool IsCatalogAssetName(string name) =>
    TryParseIndex(name, SurfacePrefix, out _) || TryParseIndex(name, CliffPrefix, out _);

  internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

  internal void SetDisposalCallback(Action<TerrainTextureCatalog> callback) {
    disposalCallback = callback;
    if (Volatile.Read(ref disposed) == 0) return;
    Interlocked.Exchange(ref disposalCallback, null)?.Invoke(this);
  }

  private static Texture[] OrderAndValidate(
    IEnumerable<Texture> textures, string prefix, string kind, int expectedCount
  ) {
    var indexed = textures
      .Select(texture => (
        texture,
        parsed: TryParseIndex(texture.Name, prefix, out var index),
        index))
      .Where(item => item.parsed)
      .OrderBy(item => item.index)
      .ToArray();

    if (indexed.Length == 0)
      throw new InvalidDataException($"The terrain catalog contains no {kind} textures.");

    var duplicate = indexed.GroupBy(item => item.index).FirstOrDefault(group => group.Count() > 1);
    if (duplicate != null)
      throw new InvalidDataException(
        $"The terrain catalog contains duplicate index {FormatName(prefix, duplicate.Key)}.");

    foreach (var (item, expectedIndex) in indexed.Select((item, index) => (item, index))) {
      if (item.index != expectedIndex)
        throw new InvalidDataException(
          $"The terrain catalog is missing {FormatName(prefix, expectedIndex)}.");
    }

    if (indexed.Length != expectedCount)
      throw new InvalidDataException(
        $"The terrain catalog must contain exactly {expectedCount} {kind} textures; " +
        $"found {indexed.Length}.");

    return indexed.Select(item => item.texture).ToArray();
  }

  private static bool TryParseIndex(string name, string prefix, out int index) {
    index = -1;
    if (!name.StartsWith(prefix, StringComparison.Ordinal)) return false;
    var suffix = name[prefix.Length..];
    var expectedLength = prefix == SurfacePrefix ? 2 : 1;
    return suffix.Length == expectedLength
      && int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out index)
      && index <= byte.MaxValue;
  }

  private static string FormatName(string prefix, int index) => prefix == SurfacePrefix
    ? $"{prefix}{index:D2}"
    : $"{prefix}{index}";

  private void ThrowIfDisposed() =>
    ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

  public void Dispose() {
    if (Interlocked.Exchange(ref disposed, 1) != 0) return;
    foreach (var texture in surfaceTextures.Concat(cliffTextures).Distinct()) texture.Dispose();
    Interlocked.Exchange(ref disposalCallback, null)?.Invoke(this);
    GC.SuppressFinalize(this);
  }
}
