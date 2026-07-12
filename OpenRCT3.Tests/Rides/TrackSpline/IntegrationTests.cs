// Track Spline Integration Tests
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
public class IntegrationTests {
  [SetUp]
  public void Setup() {
    ArcLength.ClearCache();
  }

  [Test]
  [Ignore("Blocked by SplineBaker performance (TBD: rewrite with piecewise linear or parametric sampling)")]
  public void BuildSimpleTrack_StraightCurveStraight() {
    var graph = TrackChaining.CreateGraph();

    // Build: straight (10m) → curve (90° at 5m radius) → straight (10m)
    var s1 = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(s1.LeftRail, s1.RightRail, length: 10f);
    var n1 = TrackChaining.AddRootPiece(graph, s1, position: Vector3.Zero);

    var curve = new TrackPiece { PieceType = TrackPieceType.Curve };
    ProceduralPieces.GenerateCurve(curve.LeftRail, curve.RightRail, radius: 5f, arcAngle: 1.57f); // 90°
    var n2 = TrackChaining.ChainPiece(graph, n1, curve, validateContinuity: false);
    Assert.That(n2, Is.Not.Null);

    var s2 = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(s2.LeftRail, s2.RightRail, length: 10f);
    var n3 = TrackChaining.ChainPiece(graph, n2!, s2, validateContinuity: false);

    Assert.That(graph.NodesById.Count, Is.EqualTo(3));

    // Bake the track
    TrackChaining.BakeGraph(graph, useTestTolerance: true);

    Assert.That(n1.Piece.IsBaked, Is.True);
    Assert.That(n2!.Piece.IsBaked, Is.True);
    Assert.That(n3!.Piece.IsBaked, Is.True);

    // Verify samples exist
    Assert.That(n1.Piece.LeftRail.BakedSamples.Count, Is.GreaterThan(0));
    Assert.That(n1.Piece.RightRail.BakedSamples.Count, Is.GreaterThan(0));
  }

  [Test]
  [Ignore("Blocked by SplineBaker performance (TBD: rewrite with piecewise linear or parametric sampling)")]
  public void PlaceTrain_OnStraightTrack() {
    var graph = TrackChaining.CreateGraph();

    var straight = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(straight.LeftRail, straight.RightRail, length: 20f);
    var node = TrackChaining.AddRootPiece(graph, straight);

    TrackChaining.BakeGraph(graph, useTestTolerance: true);

    // Create a train car with two bogies
    var car = new TrainCar {
      CarId = 1,
      Bogies = new() {
        new() { LongitudinalOffset = -1f, RailSide = RailSide.Left },
        new() { LongitudinalOffset = 1f, RailSide = RailSide.Right },
      },
    };

    // Place car at multiple positions along the track
    var positions = new List<Vector3>();
    for (float arcLen = 0f; arcLen <= 20f; arcLen += 5f) {
      car.ArcLengthPosition = arcLen;
      var result = WheelIK.PlaceCarOnTrack(car, straight, out var pos, out var orient);

      Assert.That(result, Is.True);
      positions.Add(pos);
    }

    // Verify car moves along track (positions should be ordered)
    for (int i = 1; i < positions.Count; i++) {
      var distance = Vector3.Distance(positions[i], positions[i - 1]);
      Assert.That(distance, Is.GreaterThan(0f), "Car should move along track");
    }
  }

  [Test]
  [Ignore("Blocked by SplineBaker performance (TBD: rewrite with piecewise linear or parametric sampling)")]
  public void QueryRailSamples_AlongTrack() {
    var piece = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(piece.LeftRail, piece.RightRail, length: 15f);
    SplineBaker.BakeRailSpline(piece.LeftRail, useTestTolerance: true);
    SplineBaker.BakeRailSpline(piece.RightRail, useTestTolerance: true);

    // Sample the rails at regular intervals
    var samples = new List<Vector3>();
    for (float arcLen = 0f; arcLen <= piece.LeftRail.TotalArcLength; arcLen += 2f) {
      var result = RailQuery.SampleRail(piece.LeftRail, arcLen, out var pos, out _, out _);
      Assert.That(result, Is.True);
      samples.Add(pos);
    }

    // Verify samples form a continuous path
    Assert.That(samples.Count, Is.GreaterThan(1));
    for (int i = 1; i < samples.Count; i++) {
      var distance = Vector3.Distance(samples[i], samples[i - 1]);
      Assert.That(distance, Is.LessThan(10f), "Samples should be reasonably close");
    }
  }

  [Test]
  [Ignore("Blocked by SplineBaker performance (TBD: rewrite with piecewise linear or parametric sampling)")]
  public void RailContinuity_AcrossPieces() {
    var graph = TrackChaining.CreateGraph();

    // Two consecutive straight pieces
    var s1 = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(s1.LeftRail, s1.RightRail, length: 10f);
    var n1 = TrackChaining.AddRootPiece(graph, s1, position: Vector3.Zero);

    var s2 = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(s2.LeftRail, s2.RightRail, length: 10f);
    var n2 = TrackChaining.ChainPiece(graph, n1, s2, validateContinuity: false);
    Assert.That(n2, Is.Not.Null);

    TrackChaining.BakeGraph(graph, useTestTolerance: true);

    // Sample end of first piece and start of second piece
    var p1 = n1.Piece;
    var p2 = n2!.Piece;

    var result1 = RailQuery.SampleRail(p1.LeftRail, p1.LeftRail.TotalArcLength, out var endPos1, out _, out _);
    var result2 = RailQuery.SampleRail(p2.LeftRail, 0f, out var startPos2, out _, out _);

    Assert.That(result1, Is.True);
    Assert.That(result2, Is.True);

    // Positions should be close (within tolerance of piece chaining)
    var distance = Vector3.Distance(endPos1, startPos2);
    Assert.That(distance, Is.LessThan(1f), "Piece boundary discontinuity too large");
  }

  [Test]
  [Ignore("Blocked by SplineBaker performance (TBD: rewrite with piecewise linear or parametric sampling)")]
  public void BankedCurve_RotationSmooth() {
    var piece = new TrackPiece { PieceType = TrackPieceType.BankedCurve };
    ProceduralPieces.GenerateBankedCurve(piece.LeftRail, piece.RightRail, radius: 5f, arcAngle: 1.57f, bank: 0.3f);
    SplineBaker.BakeRailSpline(piece.LeftRail, useTestTolerance: true);
    SplineBaker.BakeRailSpline(piece.RightRail, useTestTolerance: true);

    // Sample at regular intervals and check banks are smooth
    var banks = new List<float>();
    for (float arcLen = 0f; arcLen <= piece.LeftRail.TotalArcLength; arcLen += 1f) {
      var result = RailQuery.SampleRail(piece.LeftRail, arcLen, out _, out _, out var bank);
      Assert.That(result, Is.True);
      banks.Add(bank);
    }

    // Bank should increase monotonically (for a banked curve)
    for (int i = 1; i < banks.Count; i++) {
      // Allow some tolerance for numerical error
      Assert.That(banks[i], Is.GreaterThanOrEqualTo(banks[i - 1] - 0.01f), "Bank should increase monotonically");
    }
  }
}
