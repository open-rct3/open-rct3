// WaterManagerLoaderTests
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenRCT3.Serialization;
using OpenRCT3.Simulation;

namespace OpenRCT3.Tests.Simulation;

[TestFixture]
public class WaterManagerLoaderTests {
  [Test]
  public void Load_DecodedRecords_CreatesExactPoolAndDerivesOcean() {
    var terrain = new Terrain(width: 1, height: 1);
    var park = new Park(terrain);
    var manager = new DatWaterManagerData(terrain.Width, terrain.Height, [
      new DatWaterPoolData(10.99952507019043f, [
        new DatWaterRecord(0, 0, 0, 7),
        new DatWaterRecord(0, 0, 1, 3),
      ]),
    ]);

    WaterManagerLoader.Load(park, terrain, manager);

    using (Assert.EnterMultipleScope()) {
      Assert.That(park.WaterPools, Has.Count.EqualTo(1));
      Assert.That(park.WaterPools[0].Height, Is.EqualTo(1100));
      Assert.That(park.WaterPools[0].IsOcean, Is.True);
      Assert.That(park.WaterPools[0].Triangles.Select(triangle => triangle.VertexMask),
        Is.EqualTo(new byte[] { 7, 3 }));
      Assert.That(park.WaterTiles[(0, 0)], Does.Contain(park.WaterPools[0]));
    }
  }

  [Test]
  public void Load_EmptyDecodedPool_DoesNotCreateUnrenderablePool() {
    var terrain = new Terrain(width: 1, height: 1);
    var park = new Park(terrain);
    var manager = new DatWaterManagerData(terrain.Width, terrain.Height, [
      new DatWaterPoolData(0f, []),
    ]);

    WaterManagerLoader.Load(park, terrain, manager);

    Assert.That(park.WaterPools, Is.Empty);
  }

  [Test]
  public void Load_OppositeTileTrianglesInDifferentPools_CreatesBothPools() {
    var terrain = new Terrain(width: 1, height: 1);
    var park = new Park(terrain);
    var manager = new DatWaterManagerData(terrain.Width, terrain.Height, [
      new DatWaterPoolData(0f, [new DatWaterRecord(1, 1, 0, 7)]),
      new DatWaterPoolData(0f, [new DatWaterRecord(1, 1, 1, 7)]),
    ]);

    WaterManagerLoader.Load(park, terrain, manager);

    using (Assert.EnterMultipleScope()) {
      Assert.That(park.WaterPools, Has.Count.EqualTo(2));
      Assert.That(park.WaterTiles[(1, 1)], Has.Count.EqualTo(2));
      Assert.That(park.WaterTriangles, Has.Count.EqualTo(2));
    }
  }

  [Test]
  public void Load_OverlappingDecodedTriangles_ThrowsInvalidDataException() {
    var terrain = new Terrain(width: 1, height: 1);
    var park = new Park(terrain);
    var manager = new DatWaterManagerData(terrain.Width, terrain.Height, [
      new DatWaterPoolData(0f, [new DatWaterRecord(1, 1, 0, 7)]),
      new DatWaterPoolData(1f, [new DatWaterRecord(1, 1, 0, 3)]),
    ]);

    Assert.Throws<InvalidDataException>(
      new Action(() => WaterManagerLoader.Load(park, terrain, manager)));
  }

  [Test]
  public void Load_MismatchedDimensions_ThrowsInvalidDataException() {
    var terrain = new Terrain(width: 1, height: 1);
    var manager = new DatWaterManagerData(1, 1, []);

    Assert.Throws<InvalidDataException>(new Action(() =>
      WaterManagerLoader.Load(new Park(terrain), terrain, manager)));
  }
}
