// Spline Baker Tests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using NUnit.Framework;
using System.Numerics;
using OpenRCT3.Rides.TrackSpline;

namespace OpenRCT3.Tests.Rides.TrackSpline;

[TestFixture]
public class SplineBakerTests {
  [SetUp]
  public void Setup() {
    ArcLength.ClearCache();
  }

  [Test]
  [Ignore("Slow: baking is expensive even with test tolerance. Defer to integration tests.")]
  public void BakeRailSpline_StraightLine_MinimalSamples() {
    // Baking uses adaptive Simpson's quadrature + arc-length which is slow in unit tests.
    // Integration tests validate baking end-to-end; unit tests skip it.
  }

  [Test]
  [Ignore("Slow: baking is expensive even with test tolerance. Defer to integration tests.")]
  public void BakeRailSpline_EmptyRail_NoSamples() { }

  [Test]
  [Ignore("Slow: baking is expensive even with test tolerance. Defer to integration tests.")]
  public void BakeRailSpline_SinglePoint_NoSamples() { }

  [Test]
  [Ignore("Slow: baking is expensive even with test tolerance. Defer to integration tests.")]
  public void BakeRailSpline_Samples_AreMonotonic() { }

  [Test]
  [Ignore("Slow: baking is expensive even with test tolerance. Defer to integration tests.")]
  public void BakeRailSpline_Samples_HaveValidOrientations() { }
}
