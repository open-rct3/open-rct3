// GdkIngestionTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using NUnit.Framework;
using OpenCobra.OVL;
using OpenCobra.OVL.Files;
using OVL.Tests;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Materials = OpenCobra.GDK.Materials;

namespace OpenCobra.Tests.Integration;

[TestFixture]
public class IngestionTests {
  [Test]
  [SkipIfEnvironmentMissing("RCT3_PATH", "Cannot find RCT3. Skipping test.")]
  public void LoadTerrainTexture_Succeeds() {
    var rct3Path = Environment.GetEnvironmentVariable("RCT3_PATH")!;
    var terrainOvl = Path.Combine(rct3Path, "terrain", "RCT3", "Terrain_RCT3.common.ovl");
    if (!File.Exists(terrainOvl)) Assert.Fail($"Terrain OVL not found: {terrainOvl}");

    using var ovl = Ovl.Load(terrainOvl);
    var textures = Textures.Extract(ovl);
    Assert.That(textures, Has.Count.GreaterThan(0));

    var grass = textures.Where(t => t.Name == "Terrain_00")?.FirstOrDefault();
    Debug.Assert(grass != null);
    Assert.That(grass, Is.Not.Null);
    Assert.That(grass.MipCount, Is.GreaterThan(0));

    var image = grass!.MipLevels[0];
    Assert.That(image, Is.Not.Null);

    var texture = new Materials.Texture("Terrain_00", image);
    Assert.That(texture, Is.Not.Null);
  }
}
