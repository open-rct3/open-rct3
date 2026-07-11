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
}
