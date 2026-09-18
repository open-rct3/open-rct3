// Terrain Texture Catalog Cache
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Collections.Concurrent;

namespace OpenCobra.GDK.Assets;

internal sealed class TerrainTextureCatalogCache {
  private readonly Func<string, TerrainTextureCatalog> loader;
  private readonly Action<TerrainTextureCatalog>? catalogResolved;
  private readonly ConcurrentDictionary<string, CacheEntry> catalogs =
    new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

  public TerrainTextureCatalogCache(
    Func<string, TerrainTextureCatalog> loader,
    Action<TerrainTextureCatalog>? catalogResolved = null
  ) {
    this.loader = loader;
    this.catalogResolved = catalogResolved;
  }

  public TerrainTextureCatalog Get(string ovlPath) {
    var key = Path.GetFullPath(ovlPath);
    while (true) {
      var entry = catalogs.GetOrAdd(key, path => new CacheEntry(
        cacheEntry => Load(path, cacheEntry)));
      TerrainTextureCatalog catalog;
      try {
        catalog = entry.Catalog.Value;
      } catch {
        Remove(key, entry);
        throw;
      }

      catalogResolved?.Invoke(catalog);
      if (!catalog.IsDisposed) return catalog;
      Remove(key, entry);
    }
  }

  private TerrainTextureCatalog Load(string path, CacheEntry entry) {
    var catalog = loader(path);
    catalog.SetDisposalCallback(_ => Remove(path, entry));
    return catalog;
  }

  private void Remove(string path, CacheEntry entry) =>
    ((ICollection<KeyValuePair<string, CacheEntry>>)catalogs)
      .Remove(new KeyValuePair<string, CacheEntry>(path, entry));

  private sealed class CacheEntry {
    public Lazy<TerrainTextureCatalog> Catalog { get; }

    public CacheEntry(Func<CacheEntry, TerrainTextureCatalog> loader) {
      Catalog = new Lazy<TerrainTextureCatalog>(
        () => loader(this), LazyThreadSafetyMode.ExecutionAndPublication);
    }
  }
}
