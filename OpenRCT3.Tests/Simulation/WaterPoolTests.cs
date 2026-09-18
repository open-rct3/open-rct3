// WaterPoolTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using NUnit.Framework;
using OpenRCT3.Simulation;

namespace OpenRCT3.Tests.Simulation;

[TestFixture]
public class WaterPoolTests {
  private static Terrain NewTerrain(ushort initialHeight = 0)
    => new(width: 3, height: 3, initialHeight);

  [Test]
  public void Constructor_FromTiles_GeneratesBothFullTrianglesPerUniqueTile() {
    var pool = new WaterPool(200, [(2, 1), (1, 2), (2, 1)]);

    using (Assert.EnterMultipleScope()) {
      Assert.That(pool.Tiles, Is.EquivalentTo(new[] { (2, 1), (1, 2) }));
      Assert.That(pool.Triangles, Has.Count.EqualTo(4));
      Assert.That(pool.Triangles.Select(triangle => triangle.VertexMask), Is.All.EqualTo(7));
      Assert.That(
        pool.Triangles.Select(triangle => (triangle.X, triangle.Y, triangle.Triangle)),
        Is.EqualTo(new[] {
          (2, 1, WaterTerrainTriangle.SouthWest),
          (2, 1, WaterTerrainTriangle.NorthEast),
          (1, 2, WaterTerrainTriangle.SouthWest),
          (1, 2, WaterTerrainTriangle.NorthEast),
        }));
    }
  }

  [Test]
  public void Constructor_FromTriangles_DerivesUniqueTilesAndKeepsExactMasks() {
    var triangles = new[] {
      new WaterSurfaceTriangle(2, 3, WaterTerrainTriangle.NorthEast, 5),
      new WaterSurfaceTriangle(2, 3, WaterTerrainTriangle.SouthWest, 1),
      new WaterSurfaceTriangle(4, 5, WaterTerrainTriangle.SouthWest, 6),
    };

    var pool = new WaterPool(100, triangles, isOcean: true);

    using (Assert.EnterMultipleScope()) {
      Assert.That(pool.Tiles, Is.EquivalentTo(new[] { (2, 3), (4, 5) }));
      Assert.That(pool.Triangles, Is.EqualTo(triangles));
      Assert.That(pool.IsOcean, Is.True);
    }
  }

  [Test]
  public void Constructor_FromTriangles_RejectsDuplicateTileTriangle() {
    var triangles = new[] {
      new WaterSurfaceTriangle(2, 3, WaterTerrainTriangle.SouthWest, 1),
      new WaterSurfaceTriangle(2, 3, WaterTerrainTriangle.SouthWest, 7),
    };

    Assert.Throws<ArgumentException>(new Action(() => new WaterPool(100, triangles)));
  }

  [Test]
  public void Constructor_FromTriangles_RejectsDefaultInvalidRecord() {
    Assert.Throws<ArgumentException>(new Action(
      () => new WaterPool(100, new[] { default(WaterSurfaceTriangle) })));
  }

  [Test]
  public void WaterSurfaceTriangle_RejectsInvalidTriangleAndMask() {
    var invalidTriangle = Enum.Parse<WaterTerrainTriangle>("2");
    Assert.Throws<ArgumentOutOfRangeException>(new Action(() =>
      new WaterSurfaceTriangle(0, 0, invalidTriangle, 1)));
    Assert.Throws<ArgumentOutOfRangeException>(new Action(() =>
      new WaterSurfaceTriangle(0, 0, WaterTerrainTriangle.SouthWest, 0)));
    Assert.Throws<ArgumentOutOfRangeException>(new Action(() =>
      new WaterSurfaceTriangle(0, 0, WaterTerrainTriangle.SouthWest, 8)));
  }

  [Test]
  public void TryPlaceWaterPool_Succeeds_AndIndexesEveryTile() {
    var park = new Park();
    var terrain = NewTerrain();
    (int X, int Y)[] tiles = [(1, 1), (1, 2), (2, 1)];

    var placed = park.TryPlaceWaterPool(tiles, height: 200, terrain);

    Assert.That(placed, Is.True);
    Assert.That(park.WaterPools, Has.Count.EqualTo(1));
    foreach (var tile in tiles) {
      Assert.That(park.WaterTiles.ContainsKey(tile), Is.True);
      Assert.That(park.WaterTiles[tile], Has.Count.EqualTo(1));
      Assert.That(park.WaterTiles[tile], Does.Contain(park.WaterPools[0]));
    }
    Assert.That(park.WaterPools[0].Height, Is.EqualTo(200));
    Assert.That(park.WaterPools[0].IsOcean, Is.False);
  }

  [Test]
  public void TryPlaceWaterPool_RejectsEmptyTileSet() {
    var park = new Park();
    var terrain = NewTerrain();

    var placed = park.TryPlaceWaterPool([], height: 200, terrain);

    Assert.That(placed, Is.False);
    Assert.That(park.WaterPools, Is.Empty);
  }

  [Test]
  public void TryPlaceWaterPool_RejectsOffGridTile() {
    var park = new Park();
    var terrain = NewTerrain();

    var placed = park.TryPlaceWaterPool([(-1, 0)], height: 200, terrain);

    Assert.That(placed, Is.False);
    Assert.That(park.WaterPools, Is.Empty);
  }

  [Test]
  public void TryPlaceWaterPool_RejectsOverlapWithExistingPool() {
    var park = new Park();
    var terrain = NewTerrain();
    park.TryPlaceWaterPool([(1, 1)], height: 200, terrain);

    var placed = park.TryPlaceWaterPool([(1, 1), (1, 2)], height: 100, terrain);

    Assert.That(placed, Is.False);
    Assert.That(park.WaterPools, Has.Count.EqualTo(1));
    Assert.That(park.WaterTiles.ContainsKey((1, 2)), Is.False);
  }

  [Test]
  public void TryPlaceWaterTriangles_AllowsOppositeTrianglesInDifferentPools() {
    var park = new Park();
    var terrain = NewTerrain();
    var southWest = new WaterSurfaceTriangle(
      1, 1, WaterTerrainTriangle.SouthWest, vertexMask: 7);
    var northEast = new WaterSurfaceTriangle(
      1, 1, WaterTerrainTriangle.NorthEast, vertexMask: 7);

    var firstPlaced = park.TryPlaceWaterTriangles([southWest], 100, terrain);
    var secondPlaced = park.TryPlaceWaterTriangles([northEast], 200, terrain);

    using (Assert.EnterMultipleScope()) {
      Assert.That(firstPlaced, Is.True);
      Assert.That(secondPlaced, Is.True);
      Assert.That(park.WaterPools, Has.Count.EqualTo(2));
      Assert.That(park.WaterTiles[(1, 1)], Has.Count.EqualTo(2));
      Assert.That(
        park.WaterTriangles[(1, 1, WaterTerrainTriangle.SouthWest)],
        Is.SameAs(park.WaterPools[0]));
      Assert.That(
        park.WaterTriangles[(1, 1, WaterTerrainTriangle.NorthEast)],
        Is.SameAs(park.WaterPools[1]));
    }
  }

  [Test]
  public void InvalidateWaterPoolAt_RemovesEveryPoolTouchingTheTile() {
    var park = new Park();
    var terrain = NewTerrain();
    park.TryPlaceWaterTriangles([
      new WaterSurfaceTriangle(1, 1, WaterTerrainTriangle.SouthWest, 7)
    ], 100, terrain);
    park.TryPlaceWaterTriangles([
      new WaterSurfaceTriangle(1, 1, WaterTerrainTriangle.NorthEast, 7)
    ], 200, terrain);

    var invalidated = park.InvalidateWaterPoolAt(1, 1);

    using (Assert.EnterMultipleScope()) {
      Assert.That(invalidated, Is.True);
      Assert.That(park.WaterPools, Is.Empty);
      Assert.That(park.WaterTiles, Does.Not.ContainKey((1, 1)));
      Assert.That(park.WaterTriangles, Is.Empty);
    }
  }

  [Test]
  public void TryPlaceWaterPool_IsOceanFlagIsStored() {
    var park = new Park();
    var terrain = NewTerrain();

    park.TryPlaceWaterPool([(0, 0)], height: 0, terrain, isOcean: true);

    Assert.That(park.WaterPools[0].IsOcean, Is.True);
  }

  [Test]
  public void InvalidateWaterPoolAt_RemovesWholePoolAndEveryTileItCovered() {
    var park = new Park();
    var terrain = NewTerrain();
    park.TryPlaceWaterPool([(1, 1), (1, 2), (2, 1)], height: 200, terrain);

    var invalidated = park.InvalidateWaterPoolAt(1, 2);

    Assert.That(invalidated, Is.True);
    Assert.That(park.WaterPools, Is.Empty);
    Assert.That(park.WaterTiles.ContainsKey((1, 1)), Is.False);
    Assert.That(park.WaterTiles.ContainsKey((1, 2)), Is.False);
    Assert.That(park.WaterTiles.ContainsKey((2, 1)), Is.False);
  }

  [Test]
  public void InvalidateWaterPoolAt_FalseWhenNoPoolCoversTile() {
    var park = new Park();

    Assert.That(park.InvalidateWaterPoolAt(0, 0), Is.False);
  }

  [Test]
  public void RaiseTerrainCorner_InvalidatesPoolCoveringEditedTile() {
    var park = new Park();
    var terrain = NewTerrain();
    park.TryPlaceWaterPool([(1, 1)], height: 200, terrain);

    park.RaiseTerrainCorner(terrain, 1, 1, TerrainCornerSlot.SouthWest, delta: 10);

    Assert.That(park.WaterPools, Is.Empty);
    // The underlying terrain edit itself must still have happened.
    Assert.That(terrain.GetCorner(1, 1, TerrainCornerSlot.SouthWest).Height, Is.EqualTo(10));
  }

  [Test]
  public void RaiseTerrainCorner_InvalidatesPoolOnNeighborSharingTheRaisedCorner() {
    var park = new Park();
    var terrain = NewTerrain();
    // Tile (1,1)'s NorthEast corner is shared with tile (2,2)'s SouthWest corner.
    park.TryPlaceWaterPool([(2, 2)], height: 200, terrain);

    park.RaiseTerrainCorner(terrain, 1, 1, TerrainCornerSlot.NorthEast, delta: 10);

    Assert.That(park.WaterPools, Is.Empty);
  }

  [Test]
  public void RaiseTerrainCorner_LeavesUnrelatedPoolIntact() {
    var park = new Park();
    var terrain = NewTerrain();
    park.TryPlaceWaterPool([(0, 0)], height: 200, terrain);

    park.RaiseTerrainCorner(terrain, 2, 2, TerrainCornerSlot.NorthEast, delta: 10);

    Assert.That(park.WaterPools, Has.Count.EqualTo(1));
    Assert.That(park.WaterTiles.ContainsKey((0, 0)), Is.True);
  }

  [Test]
  public void LowerTerrainCorner_InvalidatesPoolCoveringEditedTile() {
    var park = new Park();
    var terrain = NewTerrain(initialHeight: 50);
    park.TryPlaceWaterPool([(1, 1)], height: 200, terrain);

    park.LowerTerrainCorner(terrain, 1, 1, TerrainCornerSlot.SouthWest, delta: 10);

    Assert.That(park.WaterPools, Is.Empty);
    Assert.That(terrain.GetCorner(1, 1, TerrainCornerSlot.SouthWest).Height, Is.EqualTo(40));
  }

  [Test]
  public void SetTerrainCornerHeight_InvalidatesOnlyTheEditedTilesPool() {
    var park = new Park();
    var terrain = NewTerrain();
    park.TryPlaceWaterPool([(1, 1)], height: 200, terrain);
    park.TryPlaceWaterPool([(2, 2)], height: 200, terrain);

    // Unlike raise/lower, SetCornerHeight does not propagate to the shared corner on (2,2), so that
    // pool's tile height is untouched.
    park.SetTerrainCornerHeight(terrain, 1, 1, TerrainCornerSlot.NorthEast, height: 30);

    Assert.That(park.WaterTiles.ContainsKey((1, 1)), Is.False);
    Assert.That(park.WaterTiles.ContainsKey((2, 2)), Is.True);
    Assert.That(terrain.GetCorner(1, 1, TerrainCornerSlot.NorthEast).Height, Is.EqualTo(30));
  }
}
