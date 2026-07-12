// Procedural Pieces Tests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using NUnit.Framework;
using OpenRCT3.Rides.TrackSpline;

namespace OpenRCT3.Tests.Rides.TrackSpline;

[TestFixture]
public class ProceduralPiecesTests {
  [Test]
  public void GenerateStraight_CreatesValidRails() {
    var left = new RailSpline();
    var right = new RailSpline();

    ProceduralPieces.GenerateStraight(left, right, length: 10f);

    Assert.That(left.ControlPoints.Count, Is.EqualTo(4));
    Assert.That(right.ControlPoints.Count, Is.EqualTo(4));
    Assert.That(left.ControlPoints[0].Position.X, Is.EqualTo(0f));
    Assert.That(left.ControlPoints[3].Position.X, Is.EqualTo(10f));
  }

  [Test]
  public void GenerateCurve_CreatesValidRails() {
    var left = new RailSpline();
    var right = new RailSpline();

    ProceduralPieces.GenerateCurve(left, right, radius: 5f, arcAngle: 1.57f);

    Assert.That(left.ControlPoints.Count, Is.GreaterThan(0));
    Assert.That(right.ControlPoints.Count, Is.GreaterThan(0));
  }

  [Test]
  public void GenerateSlope_CreatesValidRails() {
    var left = new RailSpline();
    var right = new RailSpline();

    ProceduralPieces.GenerateSlope(left, right, length: 10f, heightChange: 2f);

    Assert.That(left.ControlPoints.Count, Is.EqualTo(4));
    Assert.That(right.ControlPoints.Count, Is.EqualTo(4));
  }

  [Test]
  public void GenerateLoop_CreatesValidRails() {
    var left = new RailSpline();
    var right = new RailSpline();

    ProceduralPieces.GenerateLoop(left, right, radius: 5f);

    Assert.That(left.ControlPoints.Count, Is.GreaterThan(0));
    Assert.That(right.ControlPoints.Count, Is.GreaterThan(0));
  }

  [Test]
  public void GenerateCorkscrew_CreatesValidRails() {
    var left = new RailSpline();
    var right = new RailSpline();

    ProceduralPieces.GenerateCorkscrew(left, right, radius: 5f, arcAngle: 3.14f, bankEnd: 0.5f);

    Assert.That(left.ControlPoints.Count, Is.GreaterThan(0));
    Assert.That(right.ControlPoints.Count, Is.GreaterThan(0));
  }

  [Test]
  public void GenerateBankedCurve_CreatesValidRails() {
    var left = new RailSpline();
    var right = new RailSpline();

    ProceduralPieces.GenerateBankedCurve(left, right, radius: 5f, arcAngle: 1.57f, bank: 0.3f);

    Assert.That(left.ControlPoints.Count, Is.GreaterThan(0));
    Assert.That(right.ControlPoints.Count, Is.GreaterThan(0));
  }
}
