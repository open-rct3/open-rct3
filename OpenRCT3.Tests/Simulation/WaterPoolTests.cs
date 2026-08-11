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
  public void TryPlaceWaterPool_Succeeds_AndIndexesEveryTile() {
    var park = new Park();
    var terrain = NewTerrain();
    (int X, int Y)[] tiles = [(1, 1), (1, 2), (2, 1)];

    var placed = park.TryPlaceWaterPool(tiles, height: 200, terrain);

    Assert.That(placed, Is.True);
    Assert.That(park.WaterPools, Has.Count.EqualTo(1));
    foreach (var tile in tiles) {
      Assert.That(park.WaterTiles.ContainsKey(tile), Is.True);
      Assert.That(park.WaterTiles[tile], Is.SameAs(park.WaterPools[0]));
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
