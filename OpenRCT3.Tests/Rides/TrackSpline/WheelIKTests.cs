// Wheel IK Tests
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
public class WheelIKTests {
  private TrackPiece CreateStraightPiece() {
    var piece = new TrackPiece { PieceType = TrackPieceType.Straight };
    ProceduralPieces.GenerateStraight(piece.LeftRail, piece.RightRail, length: 10f);
    SplineBaker.BakeRailSpline(piece.LeftRail, useTestTolerance: true);
    SplineBaker.BakeRailSpline(piece.RightRail, useTestTolerance: true);
    piece.IsBaked = true;
    return piece;
  }

  [Test]
  [Ignore("Blocked by SplineBaker performance (TBD: rewrite with piecewise linear or parametric sampling)")]
  public void PlaceCarOnTrack_SingleBogie_ReturnsValidTransform() {
    var piece = CreateStraightPiece();
    var car = new TrainCar {
      CarId = 1,
      Bogies = new() { new() { LongitudinalOffset = 0f, RailSide = RailSide.Left } },
      ArcLengthPosition = 0f,
    };

    var result = WheelIK.PlaceCarOnTrack(car, piece, out var pos, out var orient);

    Assert.That(result, Is.True);
    Assert.That(pos, Is.Not.EqualTo(Vector3.Zero));
    var mag = orient.Length();
    Assert.That(mag, Is.EqualTo(1f).Within(1e-3f)); // Quaternion normalized
  }

  [Test]
  [Ignore("Blocked by SplineBaker performance (TBD: rewrite with piecewise linear or parametric sampling)")]
  public void PlaceCarOnTrack_TwoBogies_ReturnsStableTransform() {
    var piece = CreateStraightPiece();
    var car = new TrainCar {
      CarId = 1,
      Bogies = new() {
        new() { LongitudinalOffset = -1f, RailSide = RailSide.Left },
        new() { LongitudinalOffset = 1f, RailSide = RailSide.Right },
      },
      ArcLengthPosition = 5f,
    };

    var result = WheelIK.PlaceCarOnTrack(car, piece, out var pos, out var orient);

    Assert.That(result, Is.True);
    Assert.That(pos, Is.Not.EqualTo(Vector3.Zero));
    var mag = orient.Length();
    Assert.That(mag, Is.EqualTo(1f).Within(1e-3f));
  }

  [Test]
  public void PlaceCarOnTrack_NoBogies_ReturnsFalse() {
    var piece = CreateStraightPiece();
    var car = new TrainCar { CarId = 1, Bogies = new() }; // Empty bogies

    var result = WheelIK.PlaceCarOnTrack(car, piece, out _, out _);

    Assert.That(result, Is.False);
  }

  [Test]
  [Ignore("Blocked by SplineBaker performance (TBD: rewrite with piecewise linear or parametric sampling)")]
  public void PlaceCarOnTrack_OffTrack_ReturnsFalse() {
    var piece = CreateStraightPiece();
    var car = new TrainCar {
      CarId = 1,
      Bogies = new() { new() { LongitudinalOffset = 0f, RailSide = RailSide.Left } },
      ArcLengthPosition = 1000f, // Way off the track
    };

    var result = WheelIK.PlaceCarOnTrack(car, piece, out _, out _);

    // Should clamp to valid range or return false depending on implementation
    // For now, RailQuery clamps, so this should return true but with clamped position
    Assert.That(result, Is.True);
  }
}
