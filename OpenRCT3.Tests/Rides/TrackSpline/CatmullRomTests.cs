// Catmull-Rom Spline Tests
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
public class CatmullRomTests {
  private const float Tolerance = 1e-5f;

  [Test]
  public void Evaluate_AtT0_ReturnsP1() {
    var p0 = new Vector3(0, 0, 0);
    var p1 = new Vector3(1, 2, 3);
    var p2 = new Vector3(5, 6, 7);
    var p3 = new Vector3(8, 9, 10);

    var result = CatmullRom.Evaluate(0f, p0, p1, p2, p3);

    Assert.That(result.X, Is.EqualTo(p1.X).Within(Tolerance));
    Assert.That(result.Y, Is.EqualTo(p1.Y).Within(Tolerance));
    Assert.That(result.Z, Is.EqualTo(p1.Z).Within(Tolerance));
  }

  [Test]
  public void Evaluate_AtT1_ReturnsP2() {
    var p0 = new Vector3(0, 0, 0);
    var p1 = new Vector3(1, 2, 3);
    var p2 = new Vector3(5, 6, 7);
    var p3 = new Vector3(8, 9, 10);

    var result = CatmullRom.Evaluate(1f, p0, p1, p2, p3);

    Assert.That(result.X, Is.EqualTo(p2.X).Within(Tolerance));
    Assert.That(result.Y, Is.EqualTo(p2.Y).Within(Tolerance));
    Assert.That(result.Z, Is.EqualTo(p2.Z).Within(Tolerance));
  }

  [Test]
  public void Evaluate_StraightLine() {
    // Four collinear points: (0,0,0), (1,0,0), (2,0,0), (3,0,0)
    // Spline should pass through (1,0,0) at t=0 and (2,0,0) at t=1
    // At t=0.5, should be at (1.5,0,0)
    var p0 = new Vector3(0, 0, 0);
    var p1 = new Vector3(1, 0, 0);
    var p2 = new Vector3(2, 0, 0);
    var p3 = new Vector3(3, 0, 0);

    var result = CatmullRom.Evaluate(0.5f, p0, p1, p2, p3);

    Assert.That(result.X, Is.EqualTo(1.5f).Within(Tolerance));
    Assert.That(result.Y, Is.EqualTo(0f).Within(Tolerance));
    Assert.That(result.Z, Is.EqualTo(0f).Within(Tolerance));
  }

  [Test]
  public void Tangent_AtT0_PointsForward() {
    var p0 = new Vector3(0, 0, 0);
    var p1 = new Vector3(1, 0, 0);
    var p2 = new Vector3(3, 0, 0);
    var p3 = new Vector3(4, 0, 0);

    var tangent = CatmullRom.Tangent(0f, p0, p1, p2, p3);

    // For a straight line, tangent should point in +X direction
    Assert.That(tangent.X, Is.GreaterThan(0));
    Assert.That(Math.Abs(tangent.Y), Is.LessThan(Tolerance));
    Assert.That(Math.Abs(tangent.Z), Is.LessThan(Tolerance));
  }

  [Test]
  public void Tangent_AtT1_PointsForward() {
    var p0 = new Vector3(0, 0, 0);
    var p1 = new Vector3(1, 0, 0);
    var p2 = new Vector3(3, 0, 0);
    var p3 = new Vector3(4, 0, 0);

    var tangent = CatmullRom.Tangent(1f, p0, p1, p2, p3);

    // For a straight line, tangent should still point in +X direction
    Assert.That(tangent.X, Is.GreaterThan(0));
    Assert.That(Math.Abs(tangent.Y), Is.LessThan(Tolerance));
    Assert.That(Math.Abs(tangent.Z), Is.LessThan(Tolerance));
  }

  [Test]
  public void EvaluateScalar_AtT0_ReturnsV1() {
    var result = CatmullRom.EvaluateScalar(0f, 10f, 20f, 30f, 40f);
    Assert.That(result, Is.EqualTo(20f).Within(Tolerance));
  }

  [Test]
  public void EvaluateScalar_AtT1_ReturnsV2() {
    var result = CatmullRom.EvaluateScalar(1f, 10f, 20f, 30f, 40f);
    Assert.That(result, Is.EqualTo(30f).Within(Tolerance));
  }

  [Test]
  public void EvaluateScalar_LinearInterpolation() {
    // Values: 0, 10, 20, 30
    // At t=0.5, should interpolate between 10 and 20
    var result = CatmullRom.EvaluateScalar(0.5f, 0f, 10f, 20f, 30f);
    Assert.That(result, Is.EqualTo(15f).Within(Tolerance));
  }

  [Test]
  public void TangentScalar_LinearSequence() {
    // Values: 0, 10, 20, 30 (linear, +10 per unit)
    // Rate of change should be positive
    var tangent = CatmullRom.TangentScalar(0.5f, 0f, 10f, 20f, 30f);
    Assert.That(tangent, Is.GreaterThan(0));
  }
}
