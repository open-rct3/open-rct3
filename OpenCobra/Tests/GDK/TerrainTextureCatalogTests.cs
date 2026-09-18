// Terrain Texture Catalog Tests
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using OpenCobra.GDK;
using OpenCobra.GDK.Assets;
using OpenCobra.GDK.Materials;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace OVL.Tests.GDK;

[TestFixture]
public class TerrainTextureCatalogTests {
  [Test]
  public void Catalog_OrdersExactNamesAndMapsIndices() {
    using var catalog = new TerrainTextureCatalog(CreateCompleteTextures().Reverse());

    Assert.That(catalog.SurfaceNames,
      Is.EqualTo(Enumerable.Range(0, TerrainTextureCatalog.SurfaceCount)
        .Select(index => $"Terrain_{index:D2}")));
    Assert.That(catalog.CliffNames,
      Is.EqualTo(Enumerable.Range(0, TerrainTextureCatalog.CliffCount)
        .Select(index => $"TerrainCliff{index}")));
    Assert.That(catalog.GetSurface(25), Is.SameAs(catalog.SurfaceTextures[25]));
    Assert.That(catalog.GetCliff(5), Is.SameAs(catalog.CliffTextures[5]));
  }

  [TestCase("Terrain_25", "exactly 26 surface")]
  [TestCase("TerrainCliff5", "exactly 6 cliff")]
  public void Catalog_TruncatedTrailingAssetsFailAndDisposeInput(
    string removedName, string expectedMessage
  ) {
    var textures = CreateCompleteTextures()
      .Where(texture => texture.Name != removedName)
      .ToArray();

    var exception = Assert.Throws<InvalidDataException>(
      new Action(() => new TerrainTextureCatalog(textures)));

    Assert.That(exception!.Message, Does.Contain(expectedMessage));
    Assert.That(textures.Select(texture => texture.State), Is.All.EqualTo(State.Disposed));
  }

  [Test]
  public void Catalog_MissingMiddleIndexFailsAndDisposesInput() {
    var textures = CreateCompleteTextures()
      .Where(texture => texture.Name != "Terrain_01")
      .ToArray();

    var exception = Assert.Throws<InvalidDataException>(
      new Action(() => new TerrainTextureCatalog(textures)));

    Assert.That(exception!.Message, Does.Contain("Terrain_01"));
    Assert.That(textures.Select(texture => texture.State), Is.All.EqualTo(State.Disposed));
  }

  [Test]
  public void Catalog_OutOfRangeAndDisposedAccessFailExplicitly() {
    var catalog = CreateCompleteCatalog();
    var surface = catalog.GetSurface(0);

    var surfaceOutOfRange = Assert.Throws<ArgumentOutOfRangeException>(
      new Action(() => catalog.GetSurface(TerrainTextureCatalog.SurfaceCount)));
    var cliffOutOfRange = Assert.Throws<ArgumentOutOfRangeException>(
      new Action(() => catalog.GetCliff(TerrainTextureCatalog.CliffCount)));
    Assert.That(surfaceOutOfRange!.Message, Does.Contain("Surface index 26"));
    Assert.That(cliffOutOfRange!.Message, Does.Contain("Cliff index 6"));

    catalog.Dispose();

    Assert.That(surface.State, Is.EqualTo(State.Disposed));
    Assert.Throws<ObjectDisposedException>(new Action(() => catalog.GetSurface(0)));
    Assert.Throws<ObjectDisposedException>(new Action(() => _ = catalog.SurfaceNames));
  }

  [Test]
  public void CatalogDisposal_WaitsForTerrainMaterialLeases() {
    var catalog = CreateCompleteCatalog();
    var surface = catalog.GetSurface(11);
    var cliff = catalog.GetCliff(4);
    var surfaceMaterial = new Textured { AlbedoTexture = surface };
    var cliffMaterial = new Textured { AlbedoTexture = cliff };

    catalog.Dispose();

    Assert.That(surface.State, Is.EqualTo(State.Uninitialized));
    Assert.That(cliff.State, Is.EqualTo(State.Uninitialized));
    Assert.Throws<ObjectDisposedException>(new Action(() => catalog.GetSurface(0)));

    surfaceMaterial.Dispose();
    cliffMaterial.Dispose();

    Assert.That(surface.State, Is.EqualTo(State.Disposed));
    Assert.That(cliff.State, Is.EqualTo(State.Disposed));
  }

  [Test]
  public void Cache_ReturnsSameLiveCatalogAndReloadsAfterDisposal() {
    var loadCount = 0;
    var cache = new TerrainTextureCatalogCache(_ => {
      loadCount++;
      return CreateCompleteCatalog();
    });
    var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Terrain_RCT3.common.ovl");

    var first = cache.Get(path);
    var equivalentPath = OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path;
    var second = cache.Get(equivalentPath);

    Assert.That(second, Is.SameAs(first));
    Assert.That(loadCount, Is.EqualTo(1));

    first.Dispose();
    var reloaded = cache.Get(path);
    try {
      Assert.That(reloaded, Is.Not.SameAs(first));
      Assert.That(loadCount, Is.EqualTo(2));
    } finally {
      reloaded.Dispose();
    }
  }

  [Test]
  public void Cache_DisposeDuringAcquisition_RetriesWithLiveCatalog() {
    var loadCount = 0;
    var pauseAcquisition = 0;
    using var catalogResolved = new ManualResetEventSlim();
    using var continueAcquisition = new ManualResetEventSlim();
    var cache = new TerrainTextureCatalogCache(
      _ => {
        Interlocked.Increment(ref loadCount);
        return CreateCompleteCatalog();
      },
      _ => {
        if (Volatile.Read(ref pauseAcquisition) == 0) return;
        catalogResolved.Set();
        continueAcquisition.Wait();
      });
    var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Terrain_RCT3.common.ovl");
    var first = cache.Get(path);
    Volatile.Write(ref pauseAcquisition, 1);
    var acquisition = Task.Run(() => cache.Get(path));
    TerrainTextureCatalog? acquired = null;

    try {
      Assert.That(catalogResolved.Wait(TimeSpan.FromSeconds(5)), Is.True);
      first.Dispose();
      Volatile.Write(ref pauseAcquisition, 0);
      continueAcquisition.Set();

      acquired = acquisition.GetAwaiter().GetResult();
      Assert.That(acquired, Is.Not.SameAs(first));
      Assert.That(acquired.IsDisposed, Is.False);
      Assert.That(acquired.GetSurface(0).State, Is.Not.EqualTo(State.Disposed));
      Assert.That(loadCount, Is.EqualTo(2));
    } finally {
      Volatile.Write(ref pauseAcquisition, 0);
      continueAcquisition.Set();
      first.Dispose();
      if (acquired != null) acquired.Dispose();
      else if (acquisition.Wait(TimeSpan.FromSeconds(5))) acquisition.Result.Dispose();
    }
  }

  [Test]
  public void Cache_DoesNotRetainFailedLoads() {
    var loadCount = 0;
    var cache = new TerrainTextureCatalogCache(_ => {
      loadCount++;
      throw new FileNotFoundException("missing pair");
    });
    var path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "Terrain_RCT3.common.ovl");

    Assert.Throws<FileNotFoundException>(new Action(() => cache.Get(path)));
    Assert.Throws<FileNotFoundException>(new Action(() => cache.Get(path)));

    Assert.That(loadCount, Is.EqualTo(2));
  }

  [Test]
  public void Loader_MissingPairFailsBeforeOpeningArchive() {
    var missingPath = Path.Combine(
      TestContext.CurrentContext.WorkDirectory,
      Guid.NewGuid().ToString("N"),
      "Terrain_RCT3.common.ovl");

    var exception = Assert.Throws<FileNotFoundException>(
      new Action(() => TextureLoader.LoadTerrainCatalog(missingPath)));

    Assert.That(exception!.FileName, Is.EqualTo(Path.GetFullPath(missingPath)));
  }

  [Test]
  public void Loader_MissingUniquePairFailsBeforeOpeningCommonArchive() {
    var directory = Path.Combine(
      Path.GetTempPath(), $"openrct3-terrain-catalog-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    var commonPath = Path.Combine(directory, "Terrain_RCT3.common.ovl");
    File.WriteAllBytes(commonPath, []);
    try {
      var exception = Assert.Throws<FileNotFoundException>(
        new Action(() => TextureLoader.LoadTerrainCatalog(commonPath)));

      Assert.That(exception!.FileName,
        Is.EqualTo(Path.Combine(directory, "Terrain_RCT3.unique.ovl")));
    } finally {
      Directory.Delete(directory, true);
    }
  }

  private static TerrainTextureCatalog CreateCompleteCatalog() =>
    new(CreateCompleteTextures());

  private static Texture[] CreateCompleteTextures() => [
    .. Enumerable.Range(0, TerrainTextureCatalog.SurfaceCount)
      .Select(index => CreateTexture($"Terrain_{index:D2}")),
    .. Enumerable.Range(0, TerrainTextureCatalog.CliffCount)
      .Select(index => CreateTexture($"TerrainCliff{index}")),
  ];

  private static Texture CreateTexture(string name) =>
    new(name, 1, 1, new Image<Rgba32>(1, 1));
}
