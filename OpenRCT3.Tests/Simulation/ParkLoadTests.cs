// Verifies Park.Load against the vendored saved-park fixtures.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using NUnit.Framework;
using OpenRCT3.Simulation;

namespace OpenRCT3.Tests.Simulation;

[TestFixture]
public class ParkLoadTests {
  private static string ReverseEngineeringFixture(string fileName) =>
    Path.Combine(Constants.ParkFixturesDir, "Reverse Engineering", fileName);

  [Test]
  public void Load_OneTileAdded_PlacesExactlyOneAtGradePath() {
    var baseline = Park.Load(ReverseEngineeringFixture("baseline.dat"));
    var variant = Park.Load(ReverseEngineeringFixture("02-one-tile-added.dat"));

    using (Assert.EnterMultipleScope()) {
      Assert.That(variant.Paths, Has.Count.EqualTo(baseline.Paths.Count + 1));
      Assert.That(variant.Paths.ContainsKey((95, 25)), Is.True);
      Assert.That(variant.Paths[(95, 25)].Raised, Is.False);
    }
  }

  [Test]
  public void Load_TwoTiles_PlacesBothAdjacentAtGradePaths() {
    var baseline = Park.Load(ReverseEngineeringFixture("baseline.dat"));
    var variant = Park.Load(ReverseEngineeringFixture("02-two-tiles.dat"));

    using (Assert.EnterMultipleScope()) {
      Assert.That(variant.Paths, Has.Count.EqualTo(baseline.Paths.Count + 2));
      Assert.That(variant.Paths.ContainsKey((95, 25)), Is.True);
      Assert.That(variant.Paths.ContainsKey((94, 25)), Is.True);
    }
  }

  [Test]
  public void Load_OneRaisedTile_PlacesRaisedPathWithDecodedHeightAndSlope() {
    var variant = Park.Load(ReverseEngineeringFixture("02-one-raised-tile.dat"));

    Assert.That(variant.Paths.ContainsKey((84, 18)), Is.True);
    var tile = variant.Paths[(84, 18)];
    using (Assert.EnterMultipleScope()) {
      Assert.That(tile.Raised, Is.True);
      Assert.That(tile.RaisedHeight, Is.EqualTo(1));
      Assert.That(tile.RaisedSlope, Is.EqualTo(PathRaisedSlope.Flat));
    }
  }

  [Test]
  public void Load_RealVendoredParks_DoesNotThrow() {
    Assert.DoesNotThrow(new Action(() => Park.Load(Path.Combine(Constants.ParkFixturesDir, "Rivendell", "Rivendell.dat"))));
    Assert.DoesNotThrow(new Action(() => Park.Load(Path.Combine(Constants.ParkFixturesDir, "Fun Valley Amusment Park", "Fun Valley Amusment Park.dat"))));
  }

  #region Rivendell Park

  [Test]
  public void ParkLoadSystem_Rivendell_LoadsSuccessfully() {
    var rivendellPath = Path.Combine(Constants.ParkFixturesDir, "Rivendell", "Rivendell.dat");
    if (!File.Exists(rivendellPath)) {
      Assert.Ignore("Rivendell fixture not available");
    }

    var world = new TestWorld();
    var parkLoadSystem = new ParkLoadSystem();
    world.TryAddSystem(parkLoadSystem);

    var loadCount = 0;
    var loadedPath = "";
    world.OnLoadCalled += () => {
      loadCount++;
      loadedPath = world.LastLoadedPath ?? "";
    };

    parkLoadSystem.RequestLoad(rivendellPath);
    world.Update(TimeSpan.FromMilliseconds(16));

    Assert.That(loadCount, Is.EqualTo(1), "Should load exactly once");
    Assert.That(loadedPath, Is.EqualTo(rivendellPath), "Should load Rivendell park");

    parkLoadSystem.Dispose();
    world.Dispose();
  }

  #endregion

  #region Fun Valley Amusement Park

  [Test]
  public void ParkLoadSystem_FunValley_LoadsSuccessfully() {
    var funValleyPath = Path.Combine(Constants.ParkFixturesDir, "Fun Valley Amusment Park", "Fun Valley Amusment Park.dat");
    if (!File.Exists(funValleyPath)) {
      Assert.Ignore("Fun Valley fixture not available");
    }

    var world = new TestWorld();
    var parkLoadSystem = new ParkLoadSystem();
    world.TryAddSystem(parkLoadSystem);

    var loadCount = 0;
    var loadedPath = "";
    world.OnLoadCalled += () => {
      loadCount++;
      loadedPath = world.LastLoadedPath ?? "";
    };

    parkLoadSystem.RequestLoad(funValleyPath);
    world.Update(TimeSpan.FromMilliseconds(16));

    Assert.That(loadCount, Is.EqualTo(1), "Should load exactly once");
    Assert.That(loadedPath, Is.EqualTo(funValleyPath), "Should load Fun Valley park");

    parkLoadSystem.Dispose();
    world.Dispose();
  }

  #endregion

  private class TestWorld : OpenCobra.GDK.Game.World, IParkLoader {
    public event Action? OnLoadCalled;
    public string? LastLoadedPath { get; private set; }

    public override void Load() => (this as IParkLoader).Load(null);

    void IParkLoader.Load(string? parkPath) {
      LastLoadedPath = parkPath;
      OnLoadCalled?.Invoke();
    }

    public bool TryAddSystem(OpenCobra.GDK.Game.ISystem system) => base.AddSystem(system);
  }
}
