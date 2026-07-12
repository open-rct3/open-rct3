// Wheel IK and Bogie Placement
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections.Generic;
using System.Numerics;

namespace OpenRCT3.Rides.TrackSpline;

/// <summary>
/// A bogie (wheel assembly) on a train car.
/// </summary>
public struct Bogie {
  /// <summary>Longitudinal offset from car reference point (negative = front, positive = rear).</summary>
  public float LongitudinalOffset;

  /// <summary>Which rail this bogie rides on.</summary>
  public RailSide RailSide;

  /// <summary>Lateral offset perpendicular to track (for skewed bogies).</summary>
  public float LateralOffset;
}

/// <summary>
/// A train car with one or more bogies.
/// </summary>
public class TrainCar {
  /// <summary>Unique car ID within the train.</summary>
  public int CarId { get; set; }

  /// <summary>Bogies on this car.</summary>
  public List<Bogie> Bogies { get; set; } = [];

  /// <summary>
  /// Current arc-length position on the track graph.
  /// Updated each frame by train scheduling/physics.
  /// </summary>
  public float ArcLengthPosition { get; set; }
}

/// <summary>
/// Contact point where a wheel/bogie touches the rail.
/// </summary>
public struct BogiContactPoint {
  /// <summary>Position in world space where the bogie contacts the rail.</summary>
  public Vector3 Position;

  /// <summary>Orientation of the rail at this contact point (heading + pitch).</summary>
  public Quaternion Orientation;

  /// <summary>Bank angle at this contact point.</summary>
  public float Bank;
}

/// <summary>
/// Wheel IK API: place train cars on track and query bogie contact points.
/// </summary>
public static class WheelIK {
  /// <summary>
  /// Place a train car on a track at a specific arc-length position.
  /// Returns the car body transform (position, orientation) derived from bogie contact points.
  /// </summary>
  public static bool PlaceCarOnTrack(
    TrainCar car,
    TrackPiece piece,
    out Vector3 carPosition,
    out Quaternion carOrientation) {
    carPosition = Vector3.Zero;
    carOrientation = Quaternion.Identity;

    if (car.Bogies.Count == 0) return false;

    // Query contact points for all bogies
    var contactPoints = new List<BogiContactPoint>();
    foreach (var bogie in car.Bogies) {
      if (!QueryBogieContactPoint(piece, car.ArcLengthPosition + bogie.LongitudinalOffset, bogie.RailSide, out var contact)) {
        return false;
      }
      contactPoints.Add(contact);
    }

    // Derive car body transform from bogie contact points
    if (contactPoints.Count == 1) {
      // Single bogie: car position = contact point, orientation = contact orientation
      carPosition = contactPoints[0].Position;
      carOrientation = contactPoints[0].Orientation;
    } else if (contactPoints.Count == 2) {
      // Two bogies: car position = midpoint, orientation from bogie-to-bogie vector
      var mid = (contactPoints[0].Position + contactPoints[1].Position) * 0.5f;
      var forward = Vector3.Normalize(contactPoints[1].Position - contactPoints[0].Position);
      var up = Vector3.UnitY; // Simplification: assume gravity up

      carPosition = mid;
      carOrientation = QuaternionFromBasis(forward, up);
    } else {
      // Multiple bogies: use primary bogie (first) for position, average orientation
      carPosition = contactPoints[0].Position;
      carOrientation = Quaternion.Identity;
      foreach (var contact in contactPoints) {
        carOrientation = Quaternion.Slerp(carOrientation, contact.Orientation, 1f / contactPoints.Count);
      }
    }

    return true;
  }

  /// <summary>
  /// Query the contact point for a single bogie on a track piece.
  /// </summary>
  private static bool QueryBogieContactPoint(
    TrackPiece piece,
    float arcLength,
    RailSide railSide,
    out BogiContactPoint contact) {
    contact = default;

    var rail = railSide == RailSide.Left ? piece.LeftRail : piece.RightRail;
    if (!RailQuery.SampleRail(rail, arcLength, out var position, out var orientation, out var bank)) {
      return false;
    }

    contact = new BogiContactPoint {
      Position = position,
      Orientation = orientation,
      Bank = bank,
    };

    return true;
  }

  /// <summary>
  /// Build a quaternion from a forward direction and up vector.
  /// </summary>
  private static Quaternion QuaternionFromBasis(Vector3 forward, Vector3 up) {
    forward = Vector3.Normalize(forward);
    up = Vector3.Normalize(up);

    var right = Vector3.Normalize(Vector3.Cross(up, forward));
    var trueUp = Vector3.Cross(forward, right);

    // Convert basis to quaternion (Shepperd's method)
    var trace = forward.X + trueUp.Y + right.Z;

    if (trace > 0) {
      var s = 0.5f / (float)Math.Sqrt(trace + 1f);
      return new Quaternion(
        (trueUp.Z - right.Y) * s,
        (right.X - forward.Z) * s,
        (forward.Y - trueUp.X) * s,
        0.25f / s
      );
    } else if (forward.X > trueUp.Y && forward.X > right.Z) {
      var s = 2f * (float)Math.Sqrt(1f + forward.X - trueUp.Y - right.Z);
      return new Quaternion(
        0.25f * s,
        (forward.Y + trueUp.X) / s,
        (forward.Z + right.X) / s,
        (trueUp.Z - right.Y) / s
      );
    } else if (trueUp.Y > right.Z) {
      var s = 2f * (float)Math.Sqrt(1f + trueUp.Y - forward.X - right.Z);
      return new Quaternion(
        (forward.Y + trueUp.X) / s,
        0.25f * s,
        (trueUp.Z + right.Y) / s,
        (right.X - forward.Z) / s
      );
    } else {
      var s = 2f * (float)Math.Sqrt(1f + right.Z - forward.X - trueUp.Y);
      return new Quaternion(
        (forward.Z + right.X) / s,
        (trueUp.Z + right.Y) / s,
        0.25f * s,
        (trueUp.X - forward.Y) / s
      );
    }
  }
}
