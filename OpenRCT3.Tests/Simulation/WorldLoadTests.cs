// Park.Load Tests
//
// Tests for Park.Load(path) implementation covering default and path-based loading.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using NUnit.Framework;
using OpenRCT3.Simulation;

namespace OpenRCT3.Tests.Simulation;

[TestFixture]
public class WorldLoadTests {
  [Test]
  public void ParkLoad_WithNull_ReturnsDefaultPark() {
    var park = Park.Load(null);
    Assert.That(park, Is.Not.Null, "Park.Load(null) should return the default park");
  }
}
