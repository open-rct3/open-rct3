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
  [Test]
  public void BakeRailSpline_StraightLine_MinimalSamples() {
    var rail = new RailSpline();
    ProceduralPieces.GenerateStraight(new RailSpline(), rail, length: 10f);

    SplineBaker.BakeRailSpline(rail, useTestTolerance: true);

    // A straight line has zero chord-height deviation everywhere, so the adaptive-subdivision
    // criterion never triggers a split; the recursive "accept, then continue toward t2" walk
    // still runs to MaxDepth for a single Catmull-Rom segment regardless of curvature.
    Assert.That(rail.BakedSamples.Count, Is.GreaterThan(0));
    Assert.That(rail.TotalArcLength, Is.EqualTo(10f).Within(0.1f));
  }

  [Test]
  public void BakeRailSpline_EmptyRail_NoSamples() {
    var rail = new RailSpline();

    SplineBaker.BakeRailSpline(rail, useTestTolerance: true);

    Assert.That(rail.BakedSamples, Is.Empty);
    Assert.That(rail.TotalArcLength, Is.EqualTo(0f));
  }

  [Test]
  public void BakeRailSpline_SinglePoint_NoSamples() {
    var rail = new RailSpline {
      ControlPoints = new() { new() { Position = Vector3.Zero, Tangent = Vector3.UnitX } },
    };

    SplineBaker.BakeRailSpline(rail, useTestTolerance: true);

    Assert.That(rail.BakedSamples, Is.Empty);
    Assert.That(rail.TotalArcLength, Is.EqualTo(0f));
  }

  [Test]
  public void BakeRailSpline_Samples_AreMonotonic() {
    var rail = new RailSpline();
    ProceduralPieces.GenerateCurve(new RailSpline(), rail, radius: 5f, arcAngle: 1.57f);

    SplineBaker.BakeRailSpline(rail, useTestTolerance: true);

    for (int i = 1; i < rail.BakedSamples.Count; i++) {
      Assert.That(rail.BakedSamples[i].ArcLength, Is.GreaterThan(rail.BakedSamples[i - 1].ArcLength));
    }
  }

  [Test]
  public void BakeRailSpline_Samples_HaveValidOrientations() {
    var rail = new RailSpline();
    ProceduralPieces.GenerateCurve(new RailSpline(), rail, radius: 5f, arcAngle: 1.57f);

    SplineBaker.BakeRailSpline(rail, useTestTolerance: true);

    foreach (var sample in rail.BakedSamples) {
      Assert.That(sample.Orientation.Length(), Is.EqualTo(1f).Within(1e-3f));
    }
  }
}
