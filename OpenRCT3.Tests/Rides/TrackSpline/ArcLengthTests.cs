// Arc-Length Tests
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
public class ArcLengthTests {
  private const float Tolerance = 1e-3f;

  [SetUp]
  public void Setup() {
    ArcLength.ClearCache();
  }

  [Test]
  public void ComputeArcLength_StraightLine_EqualsDistance() {
    // Four collinear points: (0,0,0), (1,0,0), (2,0,0), (3,0,0)
    // Arc-length from t=0 to t=1 should equal distance from (1,0,0) to (2,0,0) = 1.0
    var p0 = new Vector3(0, 0, 0);
    var p1 = new Vector3(1, 0, 0);
    var p2 = new Vector3(2, 0, 0);
    var p3 = new Vector3(3, 0, 0);

    var arcLength = ArcLength.ComputeArcLength(0f, 1f, p0, p1, p2, p3, useTestTolerance: true);

    Assert.That(arcLength, Is.EqualTo(1f).Within(Tolerance));
  }

  [Test]
  public void ComputeArcLength_HalfSegment() {
    var p0 = new Vector3(0, 0, 0);
    var p1 = new Vector3(1, 0, 0);
    var p2 = new Vector3(2, 0, 0);
    var p3 = new Vector3(3, 0, 0);

    var fullLength = ArcLength.ComputeArcLength(0f, 1f, p0, p1, p2, p3, useTestTolerance: true);
    var halfLength = ArcLength.ComputeArcLength(0f, 0.5f, p0, p1, p2, p3, useTestTolerance: true);

    // Half segment should be ~half the full length (not exact due to curve)
    Assert.That(halfLength, Is.LessThan(fullLength * 0.6f));
    Assert.That(halfLength, Is.GreaterThan(fullLength * 0.4f));
  }

  [Test]
  public void ComputeArcLength_Additivity() {
    var p0 = new Vector3(0, 0, 0);
    var p1 = new Vector3(1, 0, 0);
    var p2 = new Vector3(2, 0, 0);
    var p3 = new Vector3(3, 0, 0);

    var partA = ArcLength.ComputeArcLength(0f, 0.5f, p0, p1, p2, p3, useTestTolerance: true);
    var partB = ArcLength.ComputeArcLength(0.5f, 1f, p0, p1, p2, p3, useTestTolerance: true);
    var total = ArcLength.ComputeArcLength(0f, 1f, p0, p1, p2, p3, useTestTolerance: true);

    // Parts should sum to total
    Assert.That(partA + partB, Is.EqualTo(total).Within(Tolerance * 2));
  }

  [Test]
  [CancelAfter(5000)]
  public void ParameterAtDistance_ZeroDistance_ReturnsZero() {
    var p0 = new Vector3(0, 0, 0);
    var p1 = new Vector3(1, 0, 0);
    var p2 = new Vector3(2, 0, 0);
    var p3 = new Vector3(3, 0, 0);

    var t = ArcLength.ParameterAtDistance(0f, p0, p1, p2, p3, useTestTolerance: true);

    Assert.That(t, Is.EqualTo(0f).Within(Tolerance));
  }

  [Test]
  [CancelAfter(5000)]
  public void ParameterAtDistance_FullDistance_ReturnsOne() {
    var p0 = new Vector3(0, 0, 0);
    var p1 = new Vector3(1, 0, 0);
    var p2 = new Vector3(2, 0, 0);
    var p3 = new Vector3(3, 0, 0);

    var fullLength = ArcLength.ComputeArcLength(0f, 1f, p0, p1, p2, p3, useTestTolerance: true);
    var t = ArcLength.ParameterAtDistance(fullLength, p0, p1, p2, p3, useTestTolerance: true);

    Assert.That(t, Is.EqualTo(1f).Within(Tolerance));
  }

  [Test]
  [CancelAfter(5000)]
  public void ParameterAtDistance_HalfDistance() {
    var p0 = new Vector3(0, 0, 0);
    var p1 = new Vector3(1, 0, 0);
    var p2 = new Vector3(2, 0, 0);
    var p3 = new Vector3(3, 0, 0);

    var fullLength = ArcLength.ComputeArcLength(0f, 1f, p0, p1, p2, p3, useTestTolerance: true);
    var t = ArcLength.ParameterAtDistance(fullLength * 0.5f, p0, p1, p2, p3, useTestTolerance: true);

    // t should be roughly 0.5 (will vary slightly due to curve parameterization)
    Assert.That(t, Is.GreaterThan(0.4f));
    Assert.That(t, Is.LessThan(0.6f));
  }

  [Test]
  [CancelAfter(5000)]
  public void ParameterAtDistance_ExceedsMaxDistance_ClampsToOne() {
    var p0 = new Vector3(0, 0, 0);
    var p1 = new Vector3(1, 0, 0);
    var p2 = new Vector3(2, 0, 0);
    var p3 = new Vector3(3, 0, 0);

    var t = ArcLength.ParameterAtDistance(1000f, p0, p1, p2, p3, useTestTolerance: true);

    Assert.That(t, Is.EqualTo(1f).Within(Tolerance));
  }
}
