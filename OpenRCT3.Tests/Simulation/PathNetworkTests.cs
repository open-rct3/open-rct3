// PathNetworkTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using NUnit.Framework;
using OpenRCT3.Simulation;

namespace OpenRCT3.Tests.Simulation;

[TestFixture]
public class PathNetworkTests {
  private static Terrain NewTerrain(ushort initialHeight = 0)
    => new(width: 3, height: 3, initialHeight);

  [Test]
  public void TryPlacePath_AtGrade_SucceedsOnFlatTerrain() {
    var park = new Park();
    var terrain = NewTerrain();

    var placed = park.TryPlacePath(1, 1, terrain, new PathTile());

    Assert.That(placed, Is.True);
    Assert.That(park.Paths.ContainsKey((1, 1)), Is.True);
  }

  [Test]
  public void TryPlacePath_AtGrade_RejectsWhenTerrainTooSteep() {
    var park = new Park();
    var terrain = NewTerrain();
    // Raising one corner past AtGradePathMaxRise makes tile (1,1) too steep to place a path on.
    terrain.RaiseCorner(1, 1, TerrainCornerSlot.NorthEast, delta: Park.AtGradePathMaxRise + 1);

    var placed = park.TryPlacePath(1, 1, terrain, new PathTile());

    Assert.That(placed, Is.False);
    Assert.That(park.Paths.ContainsKey((1, 1)), Is.False);
  }

  [Test]
  public void TryPlacePath_AtGrade_RejectsOffGrid() {
    var park = new Park();
    var terrain = NewTerrain();

    var placed = park.TryPlacePath(-1, 0, terrain, new PathTile());

    Assert.That(placed, Is.False);
  }

  [Test]
  public void TryPlacePath_Raised_IgnoresTerrainSteepness() {
    var park = new Park();
    var terrain = NewTerrain();
    terrain.RaiseCorner(1, 1, TerrainCornerSlot.NorthEast, delta: Park.AtGradePathMaxRise + 1);

    var placed = park.TryPlacePath(1, 1, terrain, new PathTile { Raised = true, RaisedHeight = 500 });

    Assert.That(placed, Is.True);
  }

  [Test]
  public void IsPathConnected_AtGrade_TrueOnFlatAdjacentTiles() {
    var park = new Park();
    var terrain = NewTerrain();
    park.TryPlacePath(1, 1, terrain, new PathTile());
    park.TryPlacePath(1, 2, terrain, new PathTile());

    Assert.That(park.IsPathConnected(1, 1, Edge.North, terrain), Is.True);
    Assert.That(park.IsPathConnected(1, 2, Edge.South, terrain), Is.True);
  }

  [Test]
  public void IsPathConnected_AtGrade_FalseWhenNoNeighborPath() {
    var park = new Park();
    var terrain = NewTerrain();
    park.TryPlacePath(1, 1, terrain, new PathTile());

    Assert.That(park.IsPathConnected(1, 1, Edge.North, terrain), Is.False);
  }

  [Test]
  public void IsPathConnected_AtGrade_TrueWithinHalfMaxRiseAcrossEdge() {
    var park = new Park();
    var terrain = NewTerrain();
    park.TryPlacePath(1, 1, terrain, new PathTile());
    park.TryPlacePath(1, 2, terrain, new PathTile());

    // SetCornerHeight (unlike RaiseCorner) doesn't propagate to the shared corner on tile (1,2), so
    // this creates a real mismatch across the shared edge instead of raising both tiles together.
    terrain.SetCornerHeight(1, 1, TerrainCornerSlot.NorthWest, height: (ushort)(Park.AtGradePathMaxRise / 2));
    terrain.SetCornerHeight(1, 1, TerrainCornerSlot.NorthEast, height: (ushort)(Park.AtGradePathMaxRise / 2));

    Assert.That(park.IsPathConnected(1, 1, Edge.North, terrain), Is.True);
  }

  [Test]
  public void IsPathConnected_AtGrade_FalseBeyondHalfMaxRiseAcrossEdge() {
    var park = new Park();
    var terrain = NewTerrain();
    park.TryPlacePath(1, 1, terrain, new PathTile());
    park.TryPlacePath(1, 2, terrain, new PathTile());

    terrain.SetCornerHeight(1, 1, TerrainCornerSlot.NorthWest, height: (ushort)((Park.AtGradePathMaxRise / 2) + 1));
    terrain.SetCornerHeight(1, 1, TerrainCornerSlot.NorthEast, height: (ushort)((Park.AtGradePathMaxRise / 2) + 1));

    Assert.That(park.IsPathConnected(1, 1, Edge.North, terrain), Is.False);
  }

  [Test]
  public void IsPathConnected_Raised_TrueWhenSharedEdgeHeightsMatch() {
    var park = new Park();
    var terrain = NewTerrain();
    park.TryPlacePath(1, 1, terrain, new PathTile { Raised = true, RaisedHeight = 300 });
    park.TryPlacePath(1, 2, terrain, new PathTile { Raised = true, RaisedHeight = 300 });

    Assert.That(park.IsPathConnected(1, 1, Edge.North, terrain), Is.True);
  }

  [Test]
  public void IsPathConnected_Raised_FalseWhenSharedEdgeHeightsDiffer() {
    var park = new Park();
    var terrain = NewTerrain();
    park.TryPlacePath(1, 1, terrain, new PathTile { Raised = true, RaisedHeight = 300 });
    park.TryPlacePath(1, 2, terrain, new PathTile { Raised = true, RaisedHeight = 301 });

    Assert.That(park.IsPathConnected(1, 1, Edge.North, terrain), Is.False);
  }

  [Test]
  public void IsPathConnected_RaisedNeverConnectsToAtGrade() {
    var park = new Park();
    var terrain = NewTerrain();
    park.TryPlacePath(1, 1, terrain, new PathTile { Raised = true, RaisedHeight = 0 });
    park.TryPlacePath(1, 2, terrain, new PathTile());

    Assert.That(park.IsPathConnected(1, 1, Edge.North, terrain), Is.False);
  }

  [Test]
  public void GetRaisedEdgeHeight_Sloped_HighOnFacingEdgeLowOnOpposite() {
    var tile = new PathTile {
      Raised = true,
      RaisedHeight = 100,
      RaisedSlope = PathRaisedSlope.Sloped,
      RaisedSlopeDirection = Edge.North,
    };

    Assert.That(tile.GetRaisedEdgeHeight(Edge.North), Is.EqualTo(300)); // 100 + 200 rise
    Assert.That(tile.GetRaisedEdgeHeight(Edge.South), Is.EqualTo(100));
    Assert.That(tile.GetRaisedEdgeHeight(Edge.East), Is.EqualTo(200)); // midpoint
    Assert.That(tile.GetRaisedEdgeHeight(Edge.West), Is.EqualTo(200));
  }

  [Test]
  public void GetRaisedEdgeHeight_Flat_SameOnEveryEdge() {
    var tile = new PathTile { Raised = true, RaisedHeight = 150, RaisedSlope = PathRaisedSlope.Flat };

    foreach (Edge edge in Enum.GetValues<Edge>())
      Assert.That(tile.GetRaisedEdgeHeight(edge), Is.EqualTo(150));
  }
}
