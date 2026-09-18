// Water Region Tracer Tests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using NUnit.Framework;
using OpenRCT3.Simulation;

namespace OpenRCT3.Tests.Simulation;

[TestFixture]
public class WaterRegionTracerTests {
  private static Terrain NewTerrain(int initialHeight = 1000)
    => new(width: 5, height: 5, initialHeight);

  [Test]
  public void TryTrace_RejectsOffGridSeedWithoutCallingOccupancyQuery() {
    var terrain = NewTerrain();
    var queryCount = 0;

    var traced = WaterRegionTracer.TryTrace(
      terrain,
      -1,
      0,
      (_, _) => {
        queryCount++;
        return false;
      },
      out var result);

    Assert.That(traced, Is.False);
    Assert.That(result, Is.Null);
    Assert.That(queryCount, Is.Zero);
  }

  [TestCase(-201, -200)]
  [TestCase(-200, -200)]
  [TestCase(-199, -100)]
  [TestCase(-1, 0)]
  [TestCase(0, 0)]
  [TestCase(1, 100)]
  [TestCase(99, 100)]
  [TestCase(100, 100)]
  [TestCase(101, 200)]
  public void TryTrace_SnapsSeedLowestCornerUpToOneMeterGrid(
    int seedLowestCorner,
    int expectedHeight) {
    var terrain = NewTerrain();
    SetTileHeight(terrain, (10, 10), seedLowestCorner);

    var traced = TryTrace(terrain, (10, 10), out var result);

    Assert.That(traced, Is.True);
    Assert.That(result!.Height, Is.EqualTo(expectedHeight));
    Assert.That(result.Tiles, Is.EqualTo(new[] { (10, 10) }));
  }

  [Test]
  public void TryTrace_DerivesSurfaceFromLowestSeedCorner() {
    var terrain = NewTerrain();
    terrain.SetCornerHeight(10, 10, TerrainCornerSlot.SouthWest, 101);
    terrain.SetCornerHeight(10, 10, TerrainCornerSlot.SouthEast, 150);
    terrain.SetCornerHeight(10, 10, TerrainCornerSlot.NorthWest, 199);
    terrain.SetCornerHeight(10, 10, TerrainCornerSlot.NorthEast, 200);

    var traced = TryTrace(terrain, (10, 10), out var result);

    Assert.That(traced, Is.True);
    Assert.That(result!.Height, Is.EqualTo(200));
    Assert.That(result.Tiles, Is.EqualTo(new[] { (10, 10) }));
  }

  [Test]
  public void TryTrace_ReturnsIrregularBasinAndExactBoundaryInDeterministicOrder() {
    var terrain = NewTerrain();
    (int X, int Y)[] basin = [
      (10, 10), (11, 10),
      (10, 11),
      (10, 12), (11, 12), (12, 12),
    ];
    foreach (var tile in basin) SetTileHeight(terrain, tile, 100);

    var traced = TryTrace(terrain, (11, 12), out var result);

    Assert.That(traced, Is.True);
    Assert.That(result!.Tiles, Is.EqualTo(basin));
    Assert.That(result.Boundary, Is.EqualTo(new[] {
      new WaterRegionBoundaryEdge(10, 10, Edge.South),
      new WaterRegionBoundaryEdge(10, 10, Edge.West),
      new WaterRegionBoundaryEdge(11, 10, Edge.South),
      new WaterRegionBoundaryEdge(11, 10, Edge.East),
      new WaterRegionBoundaryEdge(11, 10, Edge.North),
      new WaterRegionBoundaryEdge(10, 11, Edge.West),
      new WaterRegionBoundaryEdge(10, 11, Edge.East),
      new WaterRegionBoundaryEdge(10, 12, Edge.West),
      new WaterRegionBoundaryEdge(10, 12, Edge.North),
      new WaterRegionBoundaryEdge(11, 12, Edge.South),
      new WaterRegionBoundaryEdge(11, 12, Edge.North),
      new WaterRegionBoundaryEdge(12, 12, Edge.South),
      new WaterRegionBoundaryEdge(12, 12, Edge.East),
      new WaterRegionBoundaryEdge(12, 12, Edge.North),
    }));
    Assert.That(result.IsOcean, Is.False);

    var mutableTiles = (IList<(int X, int Y)>)result.Tiles;
    var mutableBoundary = (IList<WaterRegionBoundaryEdge>)result.Boundary;
    Assert.Throws<NotSupportedException>(new Action(() => mutableTiles.Add((9, 9))));
    Assert.Throws<NotSupportedException>(new Action(() =>
      mutableBoundary.Add(new WaterRegionBoundaryEdge(9, 9, Edge.South))));
  }

  [Test]
  public void TryTrace_ExcludesDisconnectedBasinBehindSaddle() {
    var terrain = NewTerrain();
    SetTileHeight(terrain, (10, 10), 100);
    SetTileHeight(terrain, (12, 10), 100);
    SetTileHeight(terrain, (11, 10), 100);
    terrain.SetCornerHeight(11, 10, TerrainCornerSlot.NorthEast, 201);

    var traced = TryTrace(terrain, (10, 10), out var result);

    Assert.That(traced, Is.True);
    Assert.That(result!.Tiles, Is.EqualTo(new[] { (10, 10) }));
    Assert.That(result.Boundary, Does.Contain(
      new WaterRegionBoundaryEdge(10, 10, Edge.East)));
    Assert.That(result.Tiles, Does.Not.Contain((12, 10)));
  }

  [Test]
  public void TryTrace_TreatsExistingPoolTilesAsBoundaryAndRejectsOccupiedSeed() {
    var terrain = NewTerrain();
    SetTileHeight(terrain, (10, 10), 100);
    SetTileHeight(terrain, (11, 10), 100);
    SetTileHeight(terrain, (12, 10), 100);
    var occupied = new HashSet<(int X, int Y)> { (11, 10) };

    var traced = WaterRegionTracer.TryTrace(
      terrain,
      10,
      10,
      (x, y) => occupied.Contains((x, y)),
      out var result);
    var occupiedSeedTraced = WaterRegionTracer.TryTrace(
      terrain,
      11,
      10,
      (x, y) => occupied.Contains((x, y)),
      out var occupiedSeedResult);

    Assert.That(traced, Is.True);
    Assert.That(result!.Tiles, Is.EqualTo(new[] { (10, 10) }));
    Assert.That(result.Boundary, Does.Contain(
      new WaterRegionBoundaryEdge(10, 10, Edge.East)));
    Assert.That(occupiedSeedTraced, Is.False);
    Assert.That(occupiedSeedResult, Is.Null);
  }

  [Test]
  public void TryTrace_ClassifiesRegionReachingMapEdgeAsOcean() {
    var terrain = NewTerrain();
    SetTileHeight(terrain, (1, 1), -150);
    SetTileHeight(terrain, (1, 0), -200);

    var traced = TryTrace(terrain, (1, 1), out var result);

    Assert.That(traced, Is.True);
    Assert.That(result!.Height, Is.EqualTo(-100));
    Assert.That(result.Tiles, Is.EqualTo(new[] { (1, 0), (1, 1) }));
    Assert.That(result.Boundary, Does.Contain(
      new WaterRegionBoundaryEdge(1, 0, Edge.South)));
    Assert.That(result.IsOcean, Is.True);
  }

  [Test]
  public void TryTrace_StopsAtOccupiedMapEdgeWithoutClassifyingOcean() {
    var terrain = NewTerrain();
    SetTileHeight(terrain, (1, 1), 0);
    SetTileHeight(terrain, (1, 0), 0);

    var traced = WaterRegionTracer.TryTrace(
      terrain,
      1,
      1,
      (x, y) => (x, y) == (1, 0),
      out var result);

    Assert.That(traced, Is.True);
    Assert.That(result!.Tiles, Is.EqualTo(new[] { (1, 1) }));
    Assert.That(result.IsOcean, Is.False);
  }

  [Test]
  public void TryTrace_ExaminesEachGridTileAtMostOnce() {
    var terrain = new Terrain(width: 2, height: 3, initialHeight: 0);
    var queryCount = 0;

    var traced = WaterRegionTracer.TryTrace(
      terrain,
      10,
      10,
      (_, _) => {
        queryCount++;
        return false;
      },
      out var result);

    Assert.That(traced, Is.True);
    Assert.That(result!.Tiles, Has.Count.EqualTo(terrain.Width * terrain.Height));
    Assert.That(queryCount, Is.EqualTo(terrain.Width * terrain.Height));
    Assert.That(result.Boundary, Has.Count.EqualTo((terrain.Width + terrain.Height) * 2));
    Assert.That(result.IsOcean, Is.True);
  }

  [Test]
  public void TryTrace_RejectsSeedWithAnyCornerAboveSurface() {
    var terrain = NewTerrain();
    SetTileHeight(terrain, (10, 10), 0);
    terrain.SetCornerHeight(10, 10, TerrainCornerSlot.NorthWest, 201);

    var traced = TryTrace(terrain, (10, 10), out var result);

    Assert.That(traced, Is.False);
    Assert.That(result, Is.Null);
  }

  [Test]
  public void TryTrace_RejectsLowestCornerWhoseUpwardSnapExceedsSignedRange() {
    var terrain = NewTerrain();
    SetTileHeight(terrain, (10, 10), int.MaxValue);

    var traced = TryTrace(terrain, (10, 10), out var result);

    Assert.That(traced, Is.False);
    Assert.That(result, Is.Null);
  }

  [Test]
  public void TryTrace_SnapsIntMinValueWithoutOverflow() {
    var terrain = NewTerrain();
    SetTileHeight(terrain, (10, 10), int.MinValue);

    var traced = TryTrace(terrain, (10, 10), out var result);

    Assert.That(traced, Is.True);
    Assert.That(result!.Height, Is.EqualTo(int.MinValue + 48));
    Assert.That(result.Tiles, Is.EqualTo(new[] { (10, 10) }));
  }

  [Test]
  public void TraceResult_WithNegativeHeight_CanBePlacedWithoutConversion() {
    var terrain = NewTerrain();
    var park = new Park();
    SetTileHeight(terrain, (10, 10), -150);

    var traced = WaterRegionTracer.TryTrace(
      terrain,
      10,
      10,
      (x, y) => park.WaterTiles.ContainsKey((x, y)),
      out var result);

    Assert.That(traced, Is.True);
    var placed = park.TryPlaceWaterPool(
      result!.Tiles,
      result.Height,
      terrain,
      result.IsOcean);

    Assert.That(placed, Is.True);
    Assert.That(park.WaterPools[0].Height, Is.EqualTo(-100));
  }

  private static bool TryTrace(
    Terrain terrain,
    (int X, int Y) seed,
    out WaterRegionTraceResult? result)
    => WaterRegionTracer.TryTrace(
      terrain,
      seed.X,
      seed.Y,
      (_, _) => false,
      out result);

  private static void SetTileHeight(
    Terrain terrain,
    (int X, int Y) tile,
    int height) {
    foreach (var slot in Enum.GetValues<TerrainCornerSlot>())
      terrain.SetCornerHeight(tile.X, tile.Y, slot, height);
  }
}
