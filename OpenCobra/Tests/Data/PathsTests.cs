// Verifies Paths' at-grade/raised tile extraction against real reverse-engineering fixtures.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Reflection;
using NUnit.Framework;
using OpenCobra.Data;
using OpenCobra.Data.Parks;

namespace OpenCobra.Tests.Data;

[TestFixture]
public class PathsTests {
  private static Dat LoadFixture(string fileName) {
    var assembly = Assembly.GetExecutingAssembly();
    var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(fileName));
    Assert.That(resourceName, Is.Not.Null, $"Embedded resource ending in '{fileName}' not found.");

    var tempPath = Path.Combine(Directory.CreateTempSubdirectory().FullName, fileName);
    using var stream = assembly.GetManifestResourceStream(resourceName)!;
    using var fs = File.OpenWrite(tempPath);
    stream.CopyTo(fs);
    fs.Close();
    return Dat.Load(tempPath);
  }

  [Test]
  public void ExtractAtGrade_OneTileAdded_FindsExactlyTheNewTile() {
    var baseline = LoadFixture("baseline.dat");
    var variant = LoadFixture("02-one-tile-added.dat");

    var baseTiles = Paths.ExtractAtGrade(baseline);
    var variantTiles = Paths.ExtractAtGrade(variant);

    Assert.That(variantTiles, Has.Count.EqualTo(baseTiles.Count + 1));

    var baseKeys = baseTiles.Select(t => (t.ColIndex, t.RowIndex)).ToHashSet();
    var newTiles = variantTiles.Where(t => !baseKeys.Contains((t.ColIndex, t.RowIndex))).ToList();
    Assert.That(newTiles, Has.Count.EqualTo(1));
    Assert.That(newTiles[0].ColIndex, Is.EqualTo(95));
    Assert.That(newTiles[0].RowIndex, Is.EqualTo(25));
  }

  [Test]
  public void ExtractAtGrade_TwoTiles_FindsBothAdjacentNewTiles() {
    var baseline = LoadFixture("baseline.dat");
    var variant = LoadFixture("02-two-tiles.dat");

    var baseTiles = Paths.ExtractAtGrade(baseline);
    var variantTiles = Paths.ExtractAtGrade(variant);

    Assert.That(variantTiles, Has.Count.EqualTo(baseTiles.Count + 2));

    var baseKeys = baseTiles.Select(t => (t.ColIndex, t.RowIndex)).ToHashSet();
    var newTiles = variantTiles.Where(t => !baseKeys.Contains((t.ColIndex, t.RowIndex))).ToList();
    Assert.That(
      newTiles.Select(t => ((int)t.ColIndex, (int)t.RowIndex)),
      Is.EquivalentTo(new[] { (95, 25), (94, 25) })
    );
  }

  [Test]
  public void ExtractRaised_OneRaisedTile_DecodesHeightSlopeAndSceneryLink() {
    var baseline = LoadFixture("baseline.dat");
    var variant = LoadFixture("02-one-raised-tile.dat");

    Assert.That(Paths.ExtractRaised(baseline), Is.Empty);

    var raised = Paths.ExtractRaised(variant);
    Assert.That(raised, Has.Count.EqualTo(1));

    var tile = raised[0];
    Assert.That(tile.ColIndex, Is.EqualTo(84));
    Assert.That(tile.RowIndex, Is.EqualTo(18));
    Assert.That(tile.QuantisedHeight, Is.EqualTo(1));
    Assert.That(tile.SlopeType, Is.EqualTo(0));
    Assert.That(tile.SceneryItem, Is.Not.EqualTo(0uL), "raised tile must reference its 3D support piece");
  }
}
