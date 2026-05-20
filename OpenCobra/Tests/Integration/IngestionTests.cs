// IngestionTests
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
  private string? tempDir;
  private string? uniquePath;

  [OneTimeSetUp]
  public void SetupSuite() {
    var assembly = typeof(OpenCobra.Tests.OVL.TexturesTests).Assembly;
    var commonResourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("style.common.ovl"));
    var uniqueResourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("style.unique.ovl"));

    Assert.That(commonResourceName, Is.Not.Null, "style.common.ovl not found");
    Assert.That(uniqueResourceName, Is.Not.Null, "style.unique.ovl not found");

    tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    Directory.CreateDirectory(tempDir);

    var commonPath = Path.Combine(tempDir, "style.common.ovl");
    uniquePath = Path.Combine(tempDir, "style.unique.ovl");

    {
      using var stream = assembly.GetManifestResourceStream(commonResourceName!);
      using var fs = File.OpenWrite(commonPath);
      stream!.CopyTo(fs);
    }

    {
      using var stream = assembly.GetManifestResourceStream(uniqueResourceName!);
      using var fs = File.OpenWrite(uniquePath);
      stream!.CopyTo(fs);
    }
  }

  [OneTimeTearDown]
  public void TearDownSuite() {
    if (tempDir != null && Directory.Exists(tempDir))
      Directory.Delete(tempDir, true);
  }

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

  [Test]
  public void LoadUniqueTextures_Succeeds() {
    Assert.That(uniquePath, Is.Not.Null.And.Not.Empty);

    using var ovl = Ovl.Load(uniquePath!);
    var textures = Textures.Extract(ovl);
    Assert.That(textures, Is.Not.Null);

    foreach (var texture in textures) {
      Assert.That(texture.Name, Is.Not.Null.And.Not.Empty);
      Assert.That(texture.Width, Is.GreaterThan(0));
      Assert.That(texture.Height, Is.GreaterThan(0));
      Assert.That(texture.MipCount, Is.GreaterThan(0));

      for (var i = 0; i < texture.MipCount; i++) {
        var image = texture.MipLevels[i];
        Assert.That(image, Is.Not.Null);
        Assert.That(image.Width, Is.GreaterThan(0));
        Assert.That(image.Height, Is.GreaterThan(0));
      }
    }
  }
}
