// SceneryPlacementTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.OVL;
using OpenRCT3.Simulation;

namespace OpenRCT3.Tests.Simulation;

[TestFixture]
public class SceneryPlacementTests {
  private static Terrain NewTerrain(ushort initialHeight = 0)
    => new(width: 4, height: 4, initialHeight);

  private static SceneryRegistry NewRegistry() {
    var registry = new SceneryRegistry();
    registry.Register("Tree_Oak", new SceneryDefinition(Placement.FullTile));
    registry.Register("Wall_Brick", new SceneryDefinition(Placement.Wall));
    registry.Register("Fence_Picket", new SceneryDefinition(Placement.PathEdgeOuter));
    registry.Register("Ride_FlatFoundation", new SceneryDefinition(Placement.FullTile, footprintWidth: 2, footprintHeight: 3));
    return registry;
  }

  [Test]
  public void TryPlaceScenery_RejectsUnregisteredObjectKey() {
    var park = new Park();
    var terrain = NewTerrain();
    var placement = new SceneryPlacement("Unknown", 1, 1);

    var placed = park.TryPlaceScenery(placement, NewRegistry(), terrain);

    using (Assert.EnterMultipleScope()) {
      Assert.That(placed, Is.False);
      Assert.That(park.SceneryPlacements, Is.Empty);
    }
  }

  [Test]
  public void TryPlaceScenery_RejectsOffGridAnchor() {
    var park = new Park();
    var terrain = NewTerrain();
    var placement = new SceneryPlacement("Tree_Oak", -1, 0);

    var placed = park.TryPlaceScenery(placement, NewRegistry(), terrain);

    Assert.That(placed, Is.False);
  }

  [Test]
  public void TryPlaceScenery_SingleTileFullTile_PlacesOnSlopedTerrain() {
    var park = new Park();
    var terrain = NewTerrain();
    terrain.RaiseCorner(1, 1, TerrainCornerSlot.NorthEast, delta: 10);
    var placement = new SceneryPlacement("Tree_Oak", 1, 1);

    var placed = park.TryPlaceScenery(placement, NewRegistry(), terrain);

    using (Assert.EnterMultipleScope()) {
      Assert.That(placed, Is.True);
      Assert.That(park.SceneryPlacements, Has.Count.EqualTo(1));
    }
  }

  [Test]
  public void TryPlaceScenery_MultiTileFullTile_RejectsUnlevelFootprint() {
    var park = new Park();
    var terrain = NewTerrain();
    // Raises just the shared corner between the two tiles the 2x3 footprint would cover.
    terrain.RaiseCorner(1, 1, TerrainCornerSlot.NorthEast, delta: 10);
    var placement = new SceneryPlacement("Ride_FlatFoundation", 0, 0);

    var placed = park.TryPlaceScenery(placement, NewRegistry(), terrain);

    using (Assert.EnterMultipleScope()) {
      Assert.That(placed, Is.False);
      Assert.That(park.SceneryPlacements, Is.Empty);
    }
  }

  [Test]
  public void TryPlaceScenery_MultiTileFullTile_PlacesOnLevelFootprint() {
    var park = new Park();
    var terrain = NewTerrain();
    var placement = new SceneryPlacement("Ride_FlatFoundation", 0, 0);

    var placed = park.TryPlaceScenery(placement, NewRegistry(), terrain);

    Assert.That(placed, Is.True);
  }

  [Test]
  public void TryPlaceScenery_MultiTileFullTile_RotationSwapsFootprintForFlatnessCheck() {
    var park = new Park();
    var terrain = NewTerrain();
    // Unrotated footprint is 2 wide x 3 tall; raise a corner only covered once rotated 90 degrees
    // (West), which swaps the footprint to 3 wide x 2 tall and reaches tile (2, 0)'s far corner.
    terrain.RaiseCorner(2, 0, TerrainCornerSlot.SouthEast, delta: 10);
    var placement = new SceneryPlacement("Ride_FlatFoundation", 0, 0, Edge.West);

    var placed = park.TryPlaceScenery(placement, NewRegistry(), terrain);

    Assert.That(placed, Is.False);
  }

  [Test]
  public void GetSceneryHeight_FullTile_ReturnsAverageOfTileCorners() {
    var terrain = NewTerrain();
    terrain.SetCornerHeight(1, 1, TerrainCornerSlot.SouthWest, 0);
    terrain.SetCornerHeight(1, 1, TerrainCornerSlot.SouthEast, 10);
    terrain.SetCornerHeight(1, 1, TerrainCornerSlot.NorthWest, 10);
    terrain.SetCornerHeight(1, 1, TerrainCornerSlot.NorthEast, 20);
    var definition = new SceneryDefinition(Placement.FullTile);
    var placement = new SceneryPlacement("Tree_Oak", 1, 1);

    var (near, far) = Park.GetSceneryHeight(placement, definition, terrain);

    using (Assert.EnterMultipleScope()) {
      Assert.That(near, Is.EqualTo(10));
      Assert.That(far, Is.EqualTo(10));
    }
  }

  [Test]
  public void GetSceneryHeight_Wall_ReturnsTheTwoCornersBoundingTheRotationEdge() {
    var terrain = NewTerrain();
    terrain.SetCornerHeight(1, 1, TerrainCornerSlot.SouthWest, 5);
    terrain.SetCornerHeight(1, 1, TerrainCornerSlot.SouthEast, 15);
    var definition = new SceneryDefinition(Placement.Wall);
    var placement = new SceneryPlacement("Wall_Brick", 1, 1, Edge.South);

    var (near, far) = Park.GetSceneryHeight(placement, definition, terrain);

    using (Assert.EnterMultipleScope()) {
      Assert.That(near, Is.EqualTo(5));
      Assert.That(far, Is.EqualTo(15));
    }
  }

  [Test]
  public void GetSceneryHeight_PathEdgeOuter_ConformsToEdgeSlope() {
    var terrain = NewTerrain();
    terrain.SetCornerHeight(1, 1, TerrainCornerSlot.NorthWest, 30);
    terrain.SetCornerHeight(1, 1, TerrainCornerSlot.NorthEast, 40);
    var definition = new SceneryDefinition(Placement.PathEdgeOuter);
    var placement = new SceneryPlacement("Fence_Picket", 1, 1, Edge.North);

    var (near, far) = Park.GetSceneryHeight(placement, definition, terrain);

    using (Assert.EnterMultipleScope()) {
      Assert.That(near, Is.EqualTo(30));
      Assert.That(far, Is.EqualTo(40));
    }
  }
}
