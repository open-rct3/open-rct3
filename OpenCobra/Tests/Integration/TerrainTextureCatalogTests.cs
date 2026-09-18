// Installed Terrain Texture Catalog Tests
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using OpenCobra.GDK;
using OpenCobra.GDK.Assets;
using OVL.Tests;

namespace OpenCobra.Tests.Integration;

[TestFixture]
[NonParallelizable]
public class TerrainTextureCatalogTests {
  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void InstalledPair_HasStableNamesCountsAndCacheIdentity() {
    var ovlPath = TerrainOvlPath();
    using var catalog = TextureLoader.LoadTerrainCatalog(ovlPath);
    var cached = TextureLoader.LoadTerrainCatalog(
      Path.Combine(Path.GetDirectoryName(ovlPath)!, "Terrain_RCT3.unique.ovl"));

    Assert.That(cached, Is.SameAs(catalog));
    Assert.That(catalog.SurfaceNames,
      Is.EqualTo(Enumerable.Range(0, TerrainTextureCatalog.SurfaceCount)
        .Select(index => $"Terrain_{index:D2}")));
    Assert.That(catalog.CliffNames,
      Is.EqualTo(Enumerable.Range(0, TerrainTextureCatalog.CliffCount)
        .Select(index => $"TerrainCliff{index}")));
    Assert.That(catalog.SurfaceTextures, Has.All.Matches<OpenCobra.GDK.Materials.Texture>(
      texture => texture.Width > 0 && texture.Height > 0));
    Assert.That(catalog.CliffTextures, Has.All.Matches<OpenCobra.GDK.Materials.Texture>(
      texture => texture.Width > 0 && texture.Height > 0));
    Assert.Throws<ArgumentOutOfRangeException>(new Action(
      () => catalog.GetSurface(TerrainTextureCatalog.SurfaceCount)));
    Assert.Throws<ArgumentOutOfRangeException>(new Action(
      () => catalog.GetCliff(TerrainTextureCatalog.CliffCount)));

    TestContext.Out.WriteLine(
      $"Terrain_RCT3 catalog: {catalog.SurfaceTextures.Count} surfaces, " +
      $"{catalog.CliffTextures.Count} cliffs");
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void InstalledPair_DisposalEvictsCatalogAndTextures() {
    var ovlPath = TerrainOvlPath();
    var first = TextureLoader.LoadTerrainCatalog(ovlPath);
    var firstTexture = first.GetSurface(0);

    first.Dispose();
    var second = TextureLoader.LoadTerrainCatalog(ovlPath);
    try {
      Assert.That(firstTexture.State, Is.EqualTo(State.Disposed));
      Assert.That(second, Is.Not.SameAs(first));
      Assert.That(second.GetSurface(0).State, Is.EqualTo(State.Uninitialized));
    } finally {
      second.Dispose();
    }
  }

  private static string TerrainOvlPath() => Path.Combine(
    Environment.GetEnvironmentVariable("RCT3_PATH")!,
    "terrain", "RCT3", "Terrain_RCT3.common.ovl");
}
