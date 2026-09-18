// Verifies Terrain.LoadFromSave against the vendored reverse-engineering fixtures.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using NUnit.Framework;
using OpenRCT3.Serialization;
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
  public void LoadFromSave_Baseline_PreservesDeclaredDimensionsIncludingOobBorder() {
    var terrain = Terrain.LoadFromSave(ReverseEngineeringFixture("baseline.dat"));

    using (Assert.EnterMultipleScope()) {
      Assert.That(terrain.Width, Is.EqualTo(128));
      Assert.That(terrain.Height, Is.EqualTo(128));
      Assert.That(terrain.GetCorner(TileCol, TileRow, TerrainCornerSlot.SouthEast).Height, Is.EqualTo(0));
    }
  }

  [Test]
  public void LoadFromSave_OneCornerUp_StepsSouthWestByOneMeter() {
    var terrain = Terrain.LoadFromSave(ReverseEngineeringFixture("01-one-corner-up.dat"));
    var corner = terrain.GetCorner(TileCol, TileRow, TerrainCornerSlot.SouthWest);

    using (Assert.EnterMultipleScope()) {
      // Fixture camera-relative labels do not establish global compass directions. Adjacent corners
      // in Fun Valley establish that the first positional value is this world-space SW corner.
      Assert.That(corner.Height, Is.EqualTo(100), "1.0m / HeightStep(0.01m) = 100 units");
      Assert.That(terrain.GetCorner(TileCol, TileRow, TerrainCornerSlot.SouthEast).Height, Is.EqualTo(0));
      Assert.That(terrain.GetCorner(TileCol, TileRow, TerrainCornerSlot.NorthEast).Height, Is.EqualTo(0));
      Assert.That(terrain.GetCorner(TileCol, TileRow, TerrainCornerSlot.NorthWest).Height, Is.EqualTo(0));
    }
  }

  [Test]
  public void LoadFromSave_OneCornerAndOtherCornerUp_StepsSouthWestAndNorthEastIndependently() {
    var terrain = Terrain.LoadFromSave(ReverseEngineeringFixture("01-one-corner-and-other-corner-up.dat"));

    using (Assert.EnterMultipleScope()) {
      Assert.That(terrain.GetCorner(TileCol, TileRow, TerrainCornerSlot.SouthWest).Height, Is.EqualTo(200));
      Assert.That(terrain.GetCorner(TileCol, TileRow, TerrainCornerSlot.NorthEast).Height, Is.EqualTo(100));
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
  public void LoadFromSave_WaterAdded_PreservesSignedHeight() {
    var terrain = Terrain.LoadFromSave(ReverseEngineeringFixture("01-water-added.dat"));

    // Tile 2766 (row 21, col 78) had all four corners set to -1.0m on disk; TerrainCorner.Height
    // is signed, so loading preserves that value rather than flattening the terrain.
    var corner = terrain.GetCorner(78, 21, TerrainCornerSlot.SouthEast);
    Assert.That(corner.Height, Is.EqualTo(-100));
  }

  [Test]
  public void LoadFromSave_RealVendoredPark_MatchesDeclaredNonSquareDimensions() {
    var terrain = Terrain.LoadFromSave(Path.Combine(Constants.ParkFixturesDir, "Fun Valley Amusment Park", "Fun Valley Amusment Park.dat"));

    using (Assert.EnterMultipleScope()) {
      Assert.That(terrain.Width, Is.EqualTo(95));
      Assert.That(terrain.Height, Is.EqualTo(122));
    }
  }

  [Test]
  public void Read_RealVendoredPark_ChoosesTheCornerMappingWithContinuousSharedEdges() {
    var path = Path.Combine(
      Constants.ParkFixturesDir,
      "Fun Valley Amusment Park",
      "Fun Valley Amusment Park.dat");
    var data = DatTerrainReader.Read(path);
    var decoded = CountSharedEdgeMatches(data, reflectX: false);
    var reflected = CountSharedEdgeMatches(data, reflectX: true);

    Assert.That(decoded.Matching, Is.GreaterThan(reflected.Matching),
      $"Fun Valley shared corners: decoded={decoded.Matching}/{decoded.Total}, " +
      $"absolute mismatch={decoded.AbsoluteMismatch}; x-reflected=" +
      $"{reflected.Matching}/{reflected.Total}, absolute mismatch={reflected.AbsoluteMismatch}.");
  }

  private static (int Matching, int Total, double AbsoluteMismatch) CountSharedEdgeMatches(
    DatTerrainData data,
    bool reflectX
  ) {
    var matching = 0;
    var total = 0;
    var absoluteMismatch = 0d;
    for (var y = 0; y < data.Height; y++) {
      for (var x = 0; x < data.Width; x++) {
        var cell = data.Cells[(y * data.Width) + x];
        if (x + 1 < data.Width) {
          var east = data.Cells[(y * data.Width) + x + 1];
          var southDifference = Math.Abs(
            Corner(cell, TerrainCornerSlot.SouthEast, reflectX) -
            Corner(east, TerrainCornerSlot.SouthWest, reflectX));
          var northDifference = Math.Abs(
            Corner(cell, TerrainCornerSlot.NorthEast, reflectX) -
            Corner(east, TerrainCornerSlot.NorthWest, reflectX));
          matching += Convert.ToInt32(southDifference == 0f);
          matching += Convert.ToInt32(northDifference == 0f);
          absoluteMismatch += southDifference + northDifference;
          total += 2;
        }
        if (y + 1 < data.Height) {
          var north = data.Cells[((y + 1) * data.Width) + x];
          var westDifference = Math.Abs(
            Corner(cell, TerrainCornerSlot.NorthWest, reflectX) -
            Corner(north, TerrainCornerSlot.SouthWest, reflectX));
          var eastDifference = Math.Abs(
            Corner(cell, TerrainCornerSlot.NorthEast, reflectX) -
            Corner(north, TerrainCornerSlot.SouthEast, reflectX));
          matching += Convert.ToInt32(westDifference == 0f);
          matching += Convert.ToInt32(eastDifference == 0f);
          absoluteMismatch += westDifference + eastDifference;
          total += 2;
        }
      }
    }
    return (matching, total, absoluteMismatch);
  }

  private static float Corner(DatTerrainCell cell, TerrainCornerSlot slot, bool reflectX) {
    if (reflectX) slot = slot switch {
      TerrainCornerSlot.SouthWest => TerrainCornerSlot.SouthEast,
      TerrainCornerSlot.SouthEast => TerrainCornerSlot.SouthWest,
      TerrainCornerSlot.NorthWest => TerrainCornerSlot.NorthEast,
      TerrainCornerSlot.NorthEast => TerrainCornerSlot.NorthWest,
      _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };
    return slot switch {
      TerrainCornerSlot.SouthWest => cell.SouthWestHeight,
      TerrainCornerSlot.SouthEast => cell.SouthEastHeight,
      TerrainCornerSlot.NorthWest => cell.NorthWestHeight,
      TerrainCornerSlot.NorthEast => cell.NorthEastHeight,
      _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, null),
    };
  }
}
