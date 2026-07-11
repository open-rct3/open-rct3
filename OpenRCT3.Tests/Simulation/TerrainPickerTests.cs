// TerrainPickerTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Numerics;
using OpenCobra.GDK;
using OpenRCT3.Simulation;

namespace OpenRCT3.Tests.Simulation;

[TestFixture]
public class TerrainPickerTests {
  private const float Epsilon = 1e-3f;

  // A small buildable area (Terrain pads it with the 5-tile OOB border on every side), giving a
  // 12x12 OOB-inclusive grid so tile (6, 6) sits at world X/Y [0, 4)/[24, 28) - see
  // TerrainMeshBuilder.CornerPosition's (tileX - Width/2f) centering.
  private static Terrain NewTerrain(ushort initialHeight = 0) => new(width: 2, height: 2, initialHeight);

  [Test]
  public void TryPickTile_StraightDownOverFlatTerrain_HitsExpectedTileAndPoint() {
    var terrain = NewTerrain();
    var ray = new Ray(new Vector3(2, 26, 50), -Vector3.UnitZ);

    var result = TerrainPicker.TryPickTile(ray, terrain, maxSteps: 100);

    Assert.That(result, Is.Not.Null);
    Assert.That(result!.Value.TileX, Is.EqualTo(6));
    Assert.That(result.Value.TileY, Is.EqualTo(6));
    Assert.That(Vector3.Distance(result.Value.Point, new Vector3(2, 26, 0)), Is.EqualTo(0f).Within(Epsilon));
  }

  [Test]
  public void TryPickTile_StraightDownThroughARaisedCorner_ReturnsThatCornersExactHeight() {
    var terrain = NewTerrain();
    // Tile (6, 6)'s NorthEast corner sits at world (4, 28, ...). Raise it (propagating to shared
    // neighbors, i.e. no cliff) so the hit point's Z should exactly match the raised height.
    terrain.RaiseCorner(6, 6, TerrainCornerSlot.NorthEast, delta: 50);
    var expectedZ = Terrain.CornerHeightToWorldZ(terrain.GetCorner(6, 6, TerrainCornerSlot.NorthEast).Height);
    var ray = new Ray(new Vector3(4, 28, 50), -Vector3.UnitZ);

    var result = TerrainPicker.TryPickTile(ray, terrain, maxSteps: 100);

    Assert.That(result, Is.Not.Null);
    Assert.That(result!.Value.Point.Z, Is.EqualTo(expectedZ).Within(Epsilon));
  }

  [Test]
  public void TryPickTile_DetachedCorner_HitsTheEditedTilesOwnHeight_NotTheNeighbors() {
    var terrain = NewTerrain();
    // SetCornerHeight does not propagate - tile (6, 6)'s NorthEast corner is raised, but its neighbor
    // across that corner (tile (7, 7)'s SouthWest copy) stays at 0, detaching the edge (a cliff).
    terrain.SetCornerHeight(6, 6, TerrainCornerSlot.NorthEast, 50);
    // Just inside tile (6, 6)'s bounds rather than exactly on the shared corner point (4, 28) - the
    // latter floor-maps to the neighboring tile (7, 7) instead, which is the whole point of this test.
    var ray = new Ray(new Vector3(3.999f, 27.999f, 50), -Vector3.UnitZ);

    var result = TerrainPicker.TryPickTile(ray, terrain, maxSteps: 100);

    Assert.That(result, Is.Not.Null);
    Assert.That(result!.Value.TileX, Is.EqualTo(6));
    Assert.That(result.Value.TileY, Is.EqualTo(6));
    Assert.That(result.Value.Point.Z, Is.EqualTo(Terrain.CornerHeightToWorldZ(50)).Within(Epsilon));
  }

  [Test]
  public void TryPickTile_RayOffTheOobInclusiveGrid_ReturnsNull() {
    var terrain = NewTerrain();
    // Far outside the grid's X extent (Width=12 tiles -> world X spans roughly [-24, 24]).
    var ray = new Ray(new Vector3(1000, 26, 50), -Vector3.UnitZ);

    var result = TerrainPicker.TryPickTile(ray, terrain, maxSteps: 100);

    Assert.That(result, Is.Null);
  }

  [Test]
  public void TryPickTile_RayNeverConvergingOnTerrain_TerminatesAtTheStepBudgetInsteadOfLoopingForever() {
    var terrain = NewTerrain();
    // Pointing straight up from inside the grid - every triangle test reports "behind origin", so the
    // march must give up once it exhausts maxSteps rather than run unbounded.
    var ray = new Ray(new Vector3(2, 26, 10), Vector3.UnitZ);

    var result = TerrainPicker.TryPickTile(ray, terrain, maxSteps: 5);

    Assert.That(result, Is.Null);
  }
}
