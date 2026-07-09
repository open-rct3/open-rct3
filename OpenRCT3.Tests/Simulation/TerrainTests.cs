// TerrainTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using NUnit.Framework;
using OpenRCT3.Simulation;

namespace OpenRCT3.Tests.Simulation;

[TestFixture]
public class TerrainTests {
  // Small buildable area; Terrain still pads it with the standard OOB border, but tile (0,0) and its
  // immediate neighbors stay well within the grid for easy reasoning.
  private static Terrain NewTerrain(ushort initialHeight = 0)
    => new(width: 2, height: 2, initialHeight);

  [Test]
  public void Constructor_InitializesAllCornersToInitialHeight() {
    var terrain = NewTerrain(initialHeight: 5);

    for (var y = 0; y < terrain.Height; y++) {
      for (var x = 0; x < terrain.Width; x++) {
        foreach (TerrainCornerSlot slot in Enum.GetValues<TerrainCornerSlot>())
          Assert.That(terrain.GetCorner(x, y, slot).Height, Is.EqualTo(5));
      }
    }
  }

  [Test]
  public void RaiseCorner_PropagatesToSharedCornerOnNeighboringTile() {
    var terrain = NewTerrain();

    // Tile (0,0)'s NorthEast corner is the same world-space point as tile (1,1)'s SouthWest corner.
    terrain.RaiseCorner(0, 0, TerrainCornerSlot.NorthEast, delta: 3);

    Assert.That(terrain.GetCorner(0, 0, TerrainCornerSlot.NorthEast).Height, Is.EqualTo(3));
    Assert.That(terrain.GetCorner(1, 1, TerrainCornerSlot.SouthWest).Height, Is.EqualTo(3));
    Assert.That(terrain.GetCorner(1, 0, TerrainCornerSlot.NorthWest).Height, Is.EqualTo(3));
    Assert.That(terrain.GetCorner(0, 1, TerrainCornerSlot.SouthEast).Height, Is.EqualTo(3));
  }

  [Test]
  public void LowerCorner_PropagatesToSharedCornerOnNeighboringTile() {
    var terrain = NewTerrain(initialHeight: 10);

    terrain.LowerCorner(0, 0, TerrainCornerSlot.NorthEast, delta: 4);

    Assert.That(terrain.GetCorner(0, 0, TerrainCornerSlot.NorthEast).Height, Is.EqualTo(6));
    Assert.That(terrain.GetCorner(1, 1, TerrainCornerSlot.SouthWest).Height, Is.EqualTo(6));
  }

  [Test]
  public void IsEdgeDetached_FalseForFlatTerrain() {
    var terrain = NewTerrain();

    Assert.That(terrain.IsEdgeDetached(0, 0, Edge.North), Is.False);
    Assert.That(terrain.IsEdgeDetached(0, 0, Edge.East), Is.False);
  }

  [Test]
  public void SetCornerHeight_DetachesEdgeWithoutPropagating() {
    var terrain = NewTerrain();

    // Only write tile (0,0)'s NorthEast corner directly; its neighbors keep the original height.
    terrain.SetCornerHeight(0, 0, TerrainCornerSlot.NorthEast, height: 9);

    Assert.That(terrain.GetCorner(0, 0, TerrainCornerSlot.NorthEast).Height, Is.EqualTo(9));
    Assert.That(terrain.GetCorner(1, 1, TerrainCornerSlot.SouthWest).Height, Is.EqualTo(0));
    Assert.That(terrain.IsEdgeDetached(0, 0, Edge.North), Is.True);
    Assert.That(terrain.IsEdgeDetached(0, 0, Edge.East), Is.True);
  }

  [Test]
  public void RaiseCorner_RejoinsPreviouslyDetachedEdge() {
    var terrain = NewTerrain();
    terrain.SetCornerHeight(0, 0, TerrainCornerSlot.NorthEast, height: 9);
    Assert.That(terrain.IsEdgeDetached(0, 0, Edge.North), Is.True);

    // Raising the shared corner through the normal API brings every copy back into agreement.
    terrain.RaiseCorner(1, 1, TerrainCornerSlot.SouthWest, delta: 9);

    Assert.That(terrain.GetCorner(0, 0, TerrainCornerSlot.NorthEast).Height, Is.EqualTo(9));
    Assert.That(terrain.GetCorner(1, 1, TerrainCornerSlot.SouthWest).Height, Is.EqualTo(9));
    Assert.That(terrain.IsEdgeDetached(0, 0, Edge.North), Is.False);
    Assert.That(terrain.IsEdgeDetached(0, 0, Edge.East), Is.False);
  }

  [Test]
  public void RaiseCorner_ClampsToMaxHeightQueryCeiling() {
    var terrain = NewTerrain();

    terrain.RaiseCorner(0, 0, TerrainCornerSlot.SouthWest, delta: 100, maxHeightQuery: (_, _, _) => 7);

    Assert.That(terrain.GetCorner(0, 0, TerrainCornerSlot.SouthWest).Height, Is.EqualTo(7));
  }

  [Test]
  public void LowerCorner_ClampsToMinHeightQueryFloor() {
    var terrain = NewTerrain(initialHeight: 50);

    terrain.LowerCorner(0, 0, TerrainCornerSlot.SouthWest, delta: 100, minHeightQuery: (_, _, _) => 12);

    Assert.That(terrain.GetCorner(0, 0, TerrainCornerSlot.SouthWest).Height, Is.EqualTo(12));
  }

  [Test]
  public void RaiseCorner_OnOobEdgeTile_DoesNotThrowAndSkipsMissingNeighbors() {
    var terrain = NewTerrain();

    Assert.DoesNotThrow(new Action(() =>
      terrain.RaiseCorner(terrain.Width - 1, terrain.Height - 1, TerrainCornerSlot.NorthEast, delta: 5)));
    Assert.That(
      terrain.GetCorner(terrain.Width - 1, terrain.Height - 1, TerrainCornerSlot.NorthEast).Height,
      Is.EqualTo(5));
  }

  [Test]
  public void HasTile_TrueWithinGridFalseOutside() {
    var terrain = NewTerrain();

    Assert.That(terrain.HasTile(0, 0), Is.True);
    Assert.That(terrain.HasTile(terrain.Width - 1, terrain.Height - 1), Is.True);
    Assert.That(terrain.HasTile(-1, 0), Is.False);
    Assert.That(terrain.HasTile(0, -1), Is.False);
    Assert.That(terrain.HasTile(terrain.Width, 0), Is.False);
    Assert.That(terrain.HasTile(0, terrain.Height), Is.False);
  }

  [Test]
  public void CornerHeightToWorldZ_ScalesByHeightStep() {
    Assert.That(Terrain.CornerHeightToWorldZ(100), Is.EqualTo(1.0f).Within(0.0001f));
    Assert.That(Terrain.CornerHeightToWorldZ(0), Is.EqualTo(0.0f));
  }

  [Test]
  public void GetCorners_ReturnsFourCornersInSlotOrder() {
    var terrain = NewTerrain();
    terrain.SetCornerHeight(0, 0, TerrainCornerSlot.SouthWest, 1);
    terrain.SetCornerHeight(0, 0, TerrainCornerSlot.SouthEast, 2);
    terrain.SetCornerHeight(0, 0, TerrainCornerSlot.NorthWest, 3);
    terrain.SetCornerHeight(0, 0, TerrainCornerSlot.NorthEast, 4);

    var corners = terrain.GetCorners(0, 0);

    Assert.That(corners.Length, Is.EqualTo(4));
    Assert.That(corners[(int)TerrainCornerSlot.SouthWest].Height, Is.EqualTo(1));
    Assert.That(corners[(int)TerrainCornerSlot.SouthEast].Height, Is.EqualTo(2));
    Assert.That(corners[(int)TerrainCornerSlot.NorthWest].Height, Is.EqualTo(3));
    Assert.That(corners[(int)TerrainCornerSlot.NorthEast].Height, Is.EqualTo(4));
  }
}
