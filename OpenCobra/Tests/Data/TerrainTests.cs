// Verifies Terrain.Extract's tile-array decoding against real reverse-engineering fixtures.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Reflection;
using NUnit.Framework;
using OpenCobra.Data;
using OpenCobra.Data.Parks;

namespace OpenCobra.Tests.Data;

[TestFixture]
public class TerrainTests {
  private static Dat LoadFixture(string fileName) {
    var assembly = Assembly.GetExecutingAssembly();
    var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(fileName));
    Assert.That(resourceName, Is.Not.Null, $"Embedded resource ending in '{fileName}' not found.");

    var tempPath = Path.Combine(Directory.CreateTempSubdirectory().FullName, fileName);
    using var stream = assembly.GetManifestResourceStream(resourceName)!;
    using var fs = File.OpenWrite(tempPath);
    stream.CopyTo(fs);
    fs.Close();
    return Dat.Load(tempPath);
  }

  [Test]
  public void Extract_Baseline_MatchesDeclaredDimensionsExactly() {
    var grid = Terrain.Extract(LoadFixture("baseline.dat"));

    using (Assert.EnterMultipleScope()) {
      Assert.That(grid.Width, Is.EqualTo(128));
      Assert.That(grid.Height, Is.EqualTo(128));
      Assert.That(grid.Tiles, Has.Count.EqualTo(128 * 128));
    }
  }

  [Test]
  public void Extract_OneCornerUp_StepsOnlySouthEast() {
    var baseline = Terrain.Extract(LoadFixture("baseline.dat"));
    var variant = Terrain.Extract(LoadFixture("01-one-corner-up.dat"));

    using (Assert.EnterMultipleScope()) {
      Assert.That(variant.Tiles[2902].SouthEast, Is.EqualTo(1.0f));
      Assert.That(variant.Tiles[2902].SouthWest, Is.EqualTo(0.0f));
      Assert.That(variant.Tiles[2902].NorthEast, Is.EqualTo(0.0f));
      Assert.That(variant.Tiles[2902].NorthWest, Is.EqualTo(baseline.Tiles[2902].NorthWest));
    }
  }

  [Test]
  public void Extract_OneFarCornerUp_StepsOnlySouthWestOnAMapEdgeTile() {
    var baseline = Terrain.Extract(LoadFixture("baseline.dat"));
    var variant = Terrain.Extract(LoadFixture("01-one-far-corner-up.dat"));

    // Tile 127 = row 0, col 127 under the confirmed row-major layout - a map-corner tile,
    // consistent with this edit's in-game report of a tile at the park's boundary skirt.
    using (Assert.EnterMultipleScope()) {
      Assert.That(variant.Tiles[127].SouthEast, Is.EqualTo(baseline.Tiles[127].SouthEast));
      Assert.That(variant.Tiles[127].SouthWest, Is.EqualTo(1.0f));
      Assert.That(variant.Tiles[127].NorthEast, Is.EqualTo(baseline.Tiles[127].NorthEast));
      Assert.That(variant.Tiles[127].NorthWest, Is.EqualTo(baseline.Tiles[127].NorthWest));
    }
  }

  [Test]
  public void Extract_NearLeftCornerUp_StepsOnlySouthWest() {
    var baseline = Terrain.Extract(LoadFixture("baseline.dat"));
    var variant = Terrain.Extract(LoadFixture("01-near-left-corner-up.dat"));

    using (Assert.EnterMultipleScope()) {
      Assert.That(variant.Tiles[2902].SouthWest, Is.EqualTo(1.0f));
      Assert.That(variant.Tiles[2902].SouthEast, Is.EqualTo(baseline.Tiles[2902].SouthEast));
      Assert.That(variant.Tiles[2902].NorthEast, Is.EqualTo(baseline.Tiles[2902].NorthEast));
      Assert.That(variant.Tiles[2902].NorthWest, Is.EqualTo(baseline.Tiles[2902].NorthWest));
    }
  }

  [Test]
  public void Extract_NearRightCornerUp_StepsOnlySouthEast() {
    var baseline = Terrain.Extract(LoadFixture("baseline.dat"));
    var variant = Terrain.Extract(LoadFixture("01-near-right-corner-up.dat"));

    using (Assert.EnterMultipleScope()) {
      Assert.That(variant.Tiles[2902].SouthEast, Is.EqualTo(1.0f));
      Assert.That(variant.Tiles[2902].SouthWest, Is.EqualTo(baseline.Tiles[2902].SouthWest));
      Assert.That(variant.Tiles[2902].NorthEast, Is.EqualTo(baseline.Tiles[2902].NorthEast));
      Assert.That(variant.Tiles[2902].NorthWest, Is.EqualTo(baseline.Tiles[2902].NorthWest));
    }
  }

  [Test]
  public void Extract_FarLeftCornerUp_StepsOnlyNorthWest() {
    var baseline = Terrain.Extract(LoadFixture("baseline.dat"));
    var variant = Terrain.Extract(LoadFixture("01-far-left-corner-up.dat"));

    using (Assert.EnterMultipleScope()) {
      Assert.That(variant.Tiles[2902].NorthWest, Is.EqualTo(1.0f));
      Assert.That(variant.Tiles[2902].SouthEast, Is.EqualTo(baseline.Tiles[2902].SouthEast));
      Assert.That(variant.Tiles[2902].SouthWest, Is.EqualTo(baseline.Tiles[2902].SouthWest));
      Assert.That(variant.Tiles[2902].NorthEast, Is.EqualTo(baseline.Tiles[2902].NorthEast));
    }
  }

  [Test]
  public void Extract_OneCornerAndOtherCornerUp_StepsSouthEastAndNorthWestIndependently() {
    var variant = Terrain.Extract(LoadFixture("01-one-corner-and-other-corner-up.dat"));

    using (Assert.EnterMultipleScope()) {
      Assert.That(variant.Tiles[2902].SouthEast, Is.EqualTo(2.0f));
      Assert.That(variant.Tiles[2902].SouthWest, Is.EqualTo(0.0f));
      Assert.That(variant.Tiles[2902].NorthEast, Is.EqualTo(0.0f));
      Assert.That(variant.Tiles[2902].NorthWest, Is.EqualTo(1.0f));
    }
  }

  [Test]
  public void Extract_SurfaceChanged_UpdatesSurfaceTypeWithoutTouchingHeights() {
    var baseline = Terrain.Extract(LoadFixture("baseline.dat"));
    var variant = Terrain.Extract(LoadFixture("01-surface-changed.dat"));

    var changed = variant.Tiles
      .Select((t, i) => (Tile: t, Index: i))
      .Where(t => t.Tile.SurfaceType != baseline.Tiles[t.Index].SurfaceType)
      .ToList();

    // Confirmed via full-array byte diff: exactly these 11 tiles change, in 4 brush-shaped
    // clusters 126 (not 128) tiles apart - a diamond brush shifting 2 columns per row, not a
    // row-stride bug.
    Assert.That(changed.Select(t => t.Index), Is.EqualTo(new[] {
      2512, 2513, 2639, 2640, 2641, 2767, 2768, 2769, 2895, 2896, 2897,
    }));
    foreach (var (tile, index) in changed) {
      Assert.That(tile.SurfaceType, Is.EqualTo(0x1F));
      Assert.That(baseline.Tiles[index].SurfaceType, Is.EqualTo(0x0B));
      Assert.That(tile.SouthEast, Is.EqualTo(baseline.Tiles[index].SouthEast));
      Assert.That(tile.SouthWest, Is.EqualTo(baseline.Tiles[index].SouthWest));
      Assert.That(tile.NorthEast, Is.EqualTo(baseline.Tiles[index].NorthEast));
      Assert.That(tile.NorthWest, Is.EqualTo(baseline.Tiles[index].NorthWest));
    }
  }

  [Test]
  public void Extract_WaterAdded_SetsAllFourCornersToTheSentinel() {
    var variant = Terrain.Extract(LoadFixture("01-water-added.dat"));

    using (Assert.EnterMultipleScope()) {
      Assert.That(variant.Tiles[2766].SouthEast, Is.EqualTo(-1.0f));
      Assert.That(variant.Tiles[2766].SouthWest, Is.EqualTo(-1.0f));
      Assert.That(variant.Tiles[2766].NorthEast, Is.EqualTo(-1.0f));
      Assert.That(variant.Tiles[2766].NorthWest, Is.EqualTo(-1.0f));
    }
  }

  [TestCase("02-one-tile-added.dat")]
  [TestCase("02-two-tiles.dat")]
  [TestCase("02-one-raised-tile.dat")]
  public void Extract_PathEdits_LeaveEngineTerrainByteForByteIdentical(string fileName) {
    var baseline = LoadFixture("baseline.dat").FirstByName("RCT3Terrain").FirstByName("EngineTerrain").AsOpaque().Data;
    var variant = LoadFixture(fileName).FirstByName("RCT3Terrain").FirstByName("EngineTerrain").AsOpaque().Data;

    Assert.That(variant, Is.EqualTo(baseline));
  }

  [Test]
  public void Extract_RealVendoredParks_ConsumesTheWholeBlobExactly() {
    var dat = LoadFixture("Fun Valley Amusment Park.dat");
    var blob = dat.FirstByName("RCT3Terrain").FirstByName("EngineTerrain").AsOpaque().Data;
    var grid = Terrain.Extract(dat);

    Assert.That(grid.Width * grid.Height, Is.EqualTo(grid.Tiles.Count));
    Assert.That(6 + 12 + grid.Tiles.Count * 24, Is.EqualTo(blob.Length));
  }
}
