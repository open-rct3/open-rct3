// Procedural Track Piece Generation
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;

namespace OpenRCT3.Rides.TrackSpline;

/// <summary>
/// Generate standard track pieces (straight, curve, slope, loop, corkscrew) as Catmull-Rom splines
/// from profile curve parameters (radius, pitch, bank as functions of arc-length).
/// </summary>
public static class ProceduralPieces {
  /// <summary>
  /// Generate a straight piece of given length, optionally with pitch (vertical climb) and bank.
  /// </summary>
  public static void GenerateStraight(
    RailSpline leftRail, RailSpline rightRail,
    float length, float gauge = 0.4f, float pitch = 0f, float bank = 0f) {
    // Straight line: 4 colinear control points
    var p0 = new Vector3(0, pitch * length, 0);
    var p1 = new Vector3(0, pitch * 0.5f * length, 0);
    var p2 = new Vector3(length, pitch * 0.5f * length, 0);
    var p3 = new Vector3(length, pitch * length, 0);

    var dir = Vector3.UnitX;
    var leftOffset = -gauge * 0.5f * Vector3.UnitY;
    var rightOffset = gauge * 0.5f * Vector3.UnitY;

    leftRail.ControlPoints = new() {
      new() { Position = p0 + leftOffset, Tangent = dir, Bank = bank },
      new() { Position = p1 + leftOffset, Tangent = dir, Bank = bank },
      new() { Position = p2 + leftOffset, Tangent = dir, Bank = bank },
      new() { Position = p3 + leftOffset, Tangent = dir, Bank = bank },
    };

    rightRail.ControlPoints = new() {
      new() { Position = p0 + rightOffset, Tangent = dir, Bank = bank },
      new() { Position = p1 + rightOffset, Tangent = dir, Bank = bank },
      new() { Position = p2 + rightOffset, Tangent = dir, Bank = bank },
      new() { Position = p3 + rightOffset, Tangent = dir, Bank = bank },
    };
  }

  /// <summary>
  /// Generate a circular curve piece (flat or banked).
  /// </summary>
  public static void GenerateCurve(
    RailSpline leftRail, RailSpline rightRail,
    float radius, float arcAngle, float gauge = 0.4f, float bank = 0f) {
    // Circular arc from (0,0) to (x,y) with given radius and arc angle
    var numSegments = 4; // Use 4 Catmull-Rom segments to approximate arc
    var anglePerSegment = arcAngle / numSegments;

    var leftPoints = new System.Collections.Generic.List<RailControlPoint>();
    var rightPoints = new System.Collections.Generic.List<RailControlPoint>();

    for (int i = 0; i <= numSegments; i++) {
      var angle = i * anglePerSegment;
      var cos = (float)Math.Cos(angle);
      var sin = (float)Math.Sin(angle);

      // Center of arc at (radius, 0)
      var pos = new Vector3(radius - radius * cos, 0, radius * sin);
      var tangent = Vector3.Normalize(new Vector3(-sin, 0, cos));

      leftPoints.Add(new() { Position = pos - gauge * 0.5f * Vector3.UnitY, Tangent = tangent, Bank = bank });
      rightPoints.Add(new() { Position = pos + gauge * 0.5f * Vector3.UnitY, Tangent = tangent, Bank = bank });
    }

    leftRail.ControlPoints = leftPoints;
    rightRail.ControlPoints = rightPoints;
  }

  /// <summary>
  /// Generate a slope piece (climb or descent).
  /// </summary>
  public static void GenerateSlope(
    RailSpline leftRail, RailSpline rightRail,
    float length, float heightChange, float gauge = 0.4f) {
    var pitch = heightChange / length;
    GenerateStraight(leftRail, rightRail, length, gauge, pitch);
  }

  /// <summary>
  /// Generate a vertical loop piece (simplified: circular arc in vertical plane).
  /// </summary>
  public static void GenerateLoop(
    RailSpline leftRail, RailSpline rightRail,
    float radius, float gauge = 0.4f) {
    // Vertical loop: 270° arc (3/4 circle) to approximate a loop
    var fullLoop = (float)Math.PI * 1.5f; // 270°
    GenerateCurve(leftRail, rightRail, radius, fullLoop, gauge, bank: 0f);
  }

  /// <summary>
  /// Generate a corkscrew piece (horizontal curve with increasing bank).
  /// </summary>
  public static void GenerateCorkscrew(
    RailSpline leftRail, RailSpline rightRail,
    float radius, float arcAngle, float bankEnd, float gauge = 0.4f) {
    // Circular horizontal curve with bank rotating from 0 to bankEnd
    var numSegments = 4;
    var anglePerSegment = arcAngle / numSegments;

    var leftPoints = new System.Collections.Generic.List<RailControlPoint>();
    var rightPoints = new System.Collections.Generic.List<RailControlPoint>();

    for (int i = 0; i <= numSegments; i++) {
      var angle = i * anglePerSegment;
      var bankAtI = bankEnd * (i / (float)numSegments); // Linear bank increase
      var cos = (float)Math.Cos(angle);
      var sin = (float)Math.Sin(angle);

      var pos = new Vector3(radius - radius * cos, 0, radius * sin);
      var tangent = Vector3.Normalize(new Vector3(-sin, 0, cos));

      leftPoints.Add(new() { Position = pos - gauge * 0.5f * Vector3.UnitY, Tangent = tangent, Bank = bankAtI });
      rightPoints.Add(new() { Position = pos + gauge * 0.5f * Vector3.UnitY, Tangent = tangent, Bank = bankAtI });
    }

    leftRail.ControlPoints = leftPoints;
    rightRail.ControlPoints = rightPoints;
  }

  /// <summary>
  /// Generate a banked curve piece (horizontal curve with constant bank).
  /// </summary>
  public static void GenerateBankedCurve(
    RailSpline leftRail, RailSpline rightRail,
    float radius, float arcAngle, float bank, float gauge = 0.4f) {
    GenerateCurve(leftRail, rightRail, radius, arcAngle, gauge, bank);
  }
}
