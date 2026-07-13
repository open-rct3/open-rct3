// GdkIngestionTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using NUnit.Framework;
using OpenCobra.GDK.Assets;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using OVL.Tests;
using System;
using System.IO;

namespace OpenCobra.Tests.Integration;

[TestFixture]
public class IngestionTests {
  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void LoadTerrainTexture_Succeeds() {
    using var _ = Assert.EnterMultipleScope();
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var terrainOvl = Path.Combine(rct3Path, "terrain", "RCT3", "Terrain_RCT3.common.ovl");

    if (!File.Exists(terrainOvl))
      Assert.Fail("Terrain OVL not found at: " + terrainOvl);

    using var ovl = Ovl.Load(terrainOvl);
    var textures = Textures.Extract(ovl);
    Assert.That(textures.Names, Does.Contain("Terrain_06"));
    var grassTexture = textures["Terrain_06"];
    Assert.That(grassTexture.MipLevels, Is.Not.Empty);
    Assert.That(grassTexture.MipLevels[0], Is.Not.Null);
  }

  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void LoadTerrainTypes_DecodesAllEntries() {
    using var _ = Assert.EnterMultipleScope();
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var rct3OvlPath = Path.Combine(rct3Path, "terrain", "RCT3", "Terrain_RCT3.common.ovl");
    var ctOvlPath = Path.Combine(rct3Path, "terrain", "CT", "Terrain_CT.common.ovl");

    if (!File.Exists(rct3OvlPath))
      Assert.Fail("Terrain_RCT3.common.ovl not found at: " + rct3OvlPath);

    // Load Terrain_RCT3
    using (var ovl = Ovl.Load(rct3OvlPath)) {
      var entries = TerrainTypes.Extract(ovl);
      Assert.That(entries, Is.Not.Null);
      Assert.That(entries.Count, Is.EqualTo(32), "Terrain_RCT3 should have 32 entries");

      // Verify all entries decode properly
      foreach (var entry in entries) {
        Assert.That(entry.Name, Is.Not.Empty, $"Entry should have Name");
        Assert.That(entry.Version, Is.EqualTo(1u), $"{entry.Name}: Version");
        Assert.That(entry.Type, Is.AnyOf(TerrainType.GroundUnblended, TerrainType.Cliff, TerrainType.GroundBlended),
          $"{entry.Name}: Type out of range");
      }
    }

    // Load Terrain_CT if available
    if (File.Exists(ctOvlPath)) {
      using var ovl = Ovl.Load(ctOvlPath);
      var entries = TerrainTypes.Extract(ovl);
      Assert.That(entries, Is.Not.Null);
      Assert.That(entries.Count, Is.EqualTo(6), "Terrain_CT should have 6 entries");

      // Verify Addon values (all Soaked/1)
      foreach (var entry in entries) {
        Assert.That(entry.Addon, Is.EqualTo(1u), $"{entry.Name}: Addon should be 1 (Soaked)");
      }
    }
  }
}
