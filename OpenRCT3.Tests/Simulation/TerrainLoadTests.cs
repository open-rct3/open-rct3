// Verifies Terrain.LoadFromSave against the vendored reverse-engineering fixtures.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using NUnit.Framework;
using OpenRCT3.Simulation;

namespace OpenRCT3.Tests.Simulation;

[TestFixture]
public class TerrainLoadTests {
  // Tile 2902 under the confirmed row-major `index = row*Width + col` layout with declared
  // Width=128 is row 22, column 86 - the same interior tile the reverse-engineering fixtures use
  // throughout (see rct3-terrain-data-layout.md).
  private const int TileCol = 86;
  private const int TileRow = 22;

  private static string ReverseEngineeringFixture(string fileName) =>
    Path.Combine(Constants.ParkFixturesDir, "Reverse Engineering", fileName);

  [Test]
  public void LoadFromSave_Baseline_MatchesDeclaredDimensionsWithOobBorder() {
    var terrain = Terrain.LoadFromSave(ReverseEngineeringFixture("baseline.dat"));

    using (Assert.EnterMultipleScope()) {
      Assert.That(terrain.Width, Is.EqualTo(128 + Park.OutOfBoundsBorder * 2));
      Assert.That(terrain.Height, Is.EqualTo(128 + Park.OutOfBoundsBorder * 2));
      Assert.That(terrain.GetCorner(TileCol, TileRow, TerrainCornerSlot.SouthEast).Height, Is.EqualTo(0));
    }
  }

  [Test]
  public void LoadFromSave_OneCornerUp_StepsSouthEastByOneMeter() {
    var terrain = Terrain.LoadFromSave(ReverseEngineeringFixture("01-one-corner-up.dat"));
    var corner = terrain.GetCorner(TileCol, TileRow, TerrainCornerSlot.SouthEast);

    using (Assert.EnterMultipleScope()) {
      Assert.That(corner.Height, Is.EqualTo(100), "1.0m / HeightStep(0.01m) = 100 units");
      Assert.That(terrain.GetCorner(TileCol, TileRow, TerrainCornerSlot.SouthWest).Height, Is.EqualTo(0));
      Assert.That(terrain.GetCorner(TileCol, TileRow, TerrainCornerSlot.NorthEast).Height, Is.EqualTo(0));
      Assert.That(terrain.GetCorner(TileCol, TileRow, TerrainCornerSlot.NorthWest).Height, Is.EqualTo(0));
    }
  }

  [Test]
  public void LoadFromSave_OneCornerAndOtherCornerUp_StepsSouthEastAndNorthWestIndependently() {
    var terrain = Terrain.LoadFromSave(ReverseEngineeringFixture("01-one-corner-and-other-corner-up.dat"));

    using (Assert.EnterMultipleScope()) {
      Assert.That(terrain.GetCorner(TileCol, TileRow, TerrainCornerSlot.SouthEast).Height, Is.EqualTo(200));
      Assert.That(terrain.GetCorner(TileCol, TileRow, TerrainCornerSlot.NorthWest).Height, Is.EqualTo(100));
    }
  }

  [Test]
  public void LoadFromSave_SurfaceChanged_SetsSurfaceIndexFromSurfaceType() {
    var terrain = Terrain.LoadFromSave(ReverseEngineeringFixture("01-surface-changed.dat"));

    // Tile 2512 under row-major indexing (row 19, col 80) is one of the surface-repainted tiles
    // confirmed in rct3-terrain-data-layout.md.
    var corner = terrain.GetCorner(80, 19, TerrainCornerSlot.SouthEast);
    Assert.That(corner.SurfaceIndex, Is.EqualTo(0x1F));
  }

  [Test]
  public void LoadFromSave_WaterAdded_ClampsNegativeHeightToZero() {
    var terrain = Terrain.LoadFromSave(ReverseEngineeringFixture("01-water-added.dat"));

    // Tile 2766 (row 21, col 78) had all four corners set to -1.0m on disk; TerrainCorner.Height
    // is unsigned, so this must clamp to 0 rather than wrapping/throwing.
    var corner = terrain.GetCorner(78, 21, TerrainCornerSlot.SouthEast);
    Assert.That(corner.Height, Is.EqualTo(0));
  }

  [Test]
  public void LoadFromSave_RealVendoredPark_MatchesDeclaredNonSquareDimensions() {
    var terrain = Terrain.LoadFromSave(Path.Combine(Constants.ParkFixturesDir, "Fun Valley Amusment Park", "Fun Valley Amusment Park.dat"));

    using (Assert.EnterMultipleScope()) {
      Assert.That(terrain.Width, Is.EqualTo(95 + Park.OutOfBoundsBorder * 2));
      Assert.That(terrain.Height, Is.EqualTo(122 + Park.OutOfBoundsBorder * 2));
    }
  }
}
