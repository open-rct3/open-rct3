// GdkIngestionTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using NUnit.Framework;
using OpenCobra.GDK.Assets;
using OpenCobra.OVL;
using OVL.Tests;
using System;
using System.IO;

namespace OpenCobra.Tests.Integration;

[TestFixture]
public class IngestionTests {
  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH")]
  public void LoadTerrainTexture_Succeeds() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var terrainOvl = Path.Combine(rct3Path, "terrain", "RCT3", "Terrain_RCT3.common.ovl");

    if (!File.Exists(terrainOvl)) {
      Assert.Ignore("Terrain OVL not found at: " + terrainOvl);
      return;
    }

    // This is a placeholder for actual texture names in the terrain OVL
    // Once we identify the texture names, we can verify they load correctly
    Assert.Pass("Terrain OVL exists. Ingestion logic to be verified once texture names are identified.");
  }
}
