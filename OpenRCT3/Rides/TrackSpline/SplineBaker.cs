// Spline Baking Algorithm
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections.Generic;
using System.Numerics;

namespace OpenRCT3.Rides.TrackSpline;

/// <summary>
/// Bakes a rail spline (Catmull-Rom control points) into a set of adaptive-resolution samples
/// suitable for fast runtime queries via binary search + interpolation.
/// </summary>
public static class SplineBaker {
  /// <summary>
  /// Bake a rail spline into adaptive-resolution samples driven by chord-height deviation
  /// and bank-angle rate of change.
  /// </summary>
  public static void BakeRailSpline(RailSpline rail, float gauge = 0.4f, bool useTestTolerance = false) {
    if (rail.ControlPoints.Count < 2) {
      rail.BakedSamples.Clear();
      rail.TotalArcLength = 0f;
      return;
    }

    var samples = new List<BakedSample>();
    var chordTolerance = BakingConfig.ComputeChordHeightTolerance(gauge);
    var bankThreshold = BakingConfig.BankRateThreshold;
    var arcLengthOffset = 0f;

    // Walk through each segment (p_i, p_{i+1}) of the Catmull-Rom spline
    for (int i = 0; i < rail.ControlPoints.Count - 1; i++) {
      var p0 = i > 0 ? rail.ControlPoints[i - 1].Position : rail.ControlPoints[i].Position;
      var p1 = rail.ControlPoints[i].Position;
      var p2 = rail.ControlPoints[i + 1].Position;
      var p3 = i < rail.ControlPoints.Count - 2 ? rail.ControlPoints[i + 2].Position : rail.ControlPoints[i + 1].Position;

      var b0 = i > 0 ? rail.ControlPoints[i - 1].Bank : rail.ControlPoints[i].Bank;
      var b1 = rail.ControlPoints[i].Bank;
      var b2 = rail.ControlPoints[i + 1].Bank;
      var b3 = i < rail.ControlPoints.Count - 2 ? rail.ControlPoints[i + 2].Bank : rail.ControlPoints[i + 1].Bank;

      BakeSegment(samples, p0, p1, p2, p3, b0, b1, b2, b3, chordTolerance, bankThreshold, gauge, arcLengthOffset, useTestTolerance);
      arcLengthOffset = samples[^1].ArcLength;
    }

    rail.BakedSamples = samples;
    rail.TotalArcLength = samples.Count > 0 ? samples[^1].ArcLength : 0f;
  }

  /// <summary>
  /// Bake a single Catmull-Rom segment (from p1 to p2, using p0 and p3 for tangent continuity).
  /// Recursively subdivides based on adaptive criteria. Arc-length is read from a LUT built once for
  /// the whole segment up front, rather than re-integrated at every recursion node, and continues from
  /// <paramref name="arcLengthOffset"/> so samples stay monotonic across segment boundaries within a rail.
  /// Bootstraps an explicit sample at this rail's true t=0 start if none has been emitted yet.
  /// </summary>
  private static void BakeSegment(
    List<BakedSample> samples,
    Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
    float b0, float b1, float b2, float b3,
    float chordTolerance, float bankThreshold, float gauge, float arcLengthOffset, bool useTestTolerance = false) {
    var arcLengthLut = ArcLength.BuildLutForQuery(p0, p1, p2, p3, useTestTolerance);

    if (samples.Count == 0) {
      var startPos = CatmullRom.Evaluate(0f, p0, p1, p2, p3);
      var startTangent = Vector3.Normalize(CatmullRom.Tangent(0f, p0, p1, p2, p3));
      var startBank = CatmullRom.EvaluateScalar(0f, b0, b1, b2, b3);
      EmitSample(samples, startPos, startTangent, startBank, arcLengthOffset);
    }

    BakeSegmentRecursive(samples, p0, p1, p2, p3, b0, b1, b2, b3, chordTolerance, bankThreshold, arcLengthLut, 0f, 1f, arcLengthOffset, depth: 0);
  }

  /// <summary>
  /// Adaptively subdivide [t1, t2]: if the interval is flat enough (or the recursion cap is hit), emit one
  /// sample at t2 and stop; otherwise split at the midpoint and recurse into both halves. A sample already
  /// exists at t1 (either the rail's bootstrapped start, or the previous accepted interval's t2), so this
  /// only ever needs to emit the far endpoint of whatever interval it accepts — never the midpoint, which
  /// previously left samples marching toward t2 forever instead of terminating.
  /// </summary>
  private static void BakeSegmentRecursive(
    List<BakedSample> samples,
    Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
    float b0, float b1, float b2, float b3,
    float chordTolerance, float bankThreshold, ArcLength.Lut arcLengthLut,
    float t1, float t2, float cumulativeArcLength, int depth) {
    const int MaxDepth = 30;

    var t_mid = (t1 + t2) * 0.5f;

    // Evaluate at three points: t1, t_mid, t2
    var pos1 = CatmullRom.Evaluate(t1, p0, p1, p2, p3);
    var pos_mid = CatmullRom.Evaluate(t_mid, p0, p1, p2, p3);
    var pos2 = CatmullRom.Evaluate(t2, p0, p1, p2, p3);

    // Look up arc-lengths from the precomputed LUT
    var arcLenAt1 = ArcLength.ArcLengthAt(arcLengthLut, t1);
    var arcLenAtMid = ArcLength.ArcLengthAt(arcLengthLut, t_mid);
    var arcLenAt2 = ArcLength.ArcLengthAt(arcLengthLut, t2);
    var arcLen_1_mid = arcLenAtMid - arcLenAt1;
    var arcLen_mid_2 = arcLenAt2 - arcLenAtMid;
    var arcLen_1_2 = arcLenAt2 - arcLenAt1;

    // Check chord-height deviation: distance from midpoint to spline
    var chordLine = pos2 - pos1;
    var chordLen = chordLine.Length();
    float deviation = 0f;
    if (chordLen > 1e-6f) {
      // Distance from pos_mid to line segment (pos1, pos2)
      var toMid = pos_mid - pos1;
      var projectedDist = Vector3.Dot(toMid, chordLine) / (chordLen * chordLen);
      projectedDist = Math.Clamp(projectedDist, 0f, 1f);
      var closestOnLine = pos1 + projectedDist * chordLine;
      deviation = (pos_mid - closestOnLine).Length();
    }

    // Check bank-angle rate of change
    var bank1 = CatmullRom.EvaluateScalar(t1, b0, b1, b2, b3);
    var bank_mid = CatmullRom.EvaluateScalar(t_mid, b0, b1, b2, b3);
    var bank2 = CatmullRom.EvaluateScalar(t2, b0, b1, b2, b3);

    var bankRate_1_mid = arcLen_1_mid > 1e-6f ? Math.Abs(bank_mid - bank1) / arcLen_1_mid : 0f;
    var bankRate_mid_2 = arcLen_mid_2 > 1e-6f ? Math.Abs(bank2 - bank_mid) / arcLen_mid_2 : 0f;

    // Subdivide if either criterion triggers
    bool needsSubdivision = deviation > chordTolerance || bankRate_1_mid > bankThreshold || bankRate_mid_2 > bankThreshold;

    if (depth >= MaxDepth || !needsSubdivision || arcLen_1_2 < 1e-6f) {
      // Accept: this whole [t1, t2] span is flat enough. Emit its far endpoint and stop recursing.
      var tangent2 = Vector3.Normalize(CatmullRom.Tangent(t2, p0, p1, p2, p3));
      EmitSample(samples, pos2, tangent2, bank2, cumulativeArcLength + arcLen_1_2);
      return;
    }

    // Subdivide: recurse into both halves, left-to-right so samples come out in increasing arc-length order.
    BakeSegmentRecursive(samples, p0, p1, p2, p3, b0, b1, b2, b3, chordTolerance, bankThreshold, arcLengthLut, t1, t_mid, cumulativeArcLength, depth + 1);
    BakeSegmentRecursive(samples, p0, p1, p2, p3, b0, b1, b2, b3, chordTolerance, bankThreshold, arcLengthLut, t_mid, t2, cumulativeArcLength + arcLen_1_mid, depth + 1);
  }

  private static void EmitSample(List<BakedSample> samples, Vector3 position, Vector3 tangent, float bank, float arcLength) {
    // Build orientation quaternion from tangent and bank
    // Forward is along tangent; bank rotates around forward axis
    var forward = tangent;
    var up = Vector3.UnitY;

    // If forward is nearly vertical, use a different up vector
    if (Math.Abs(Vector3.Dot(forward, up)) > 0.99f) {
      up = Vector3.UnitZ;
    }

    var right = Vector3.Normalize(Vector3.Cross(up, forward));
    up = Vector3.Cross(forward, right);

    // Apply bank rotation around forward axis
    var bankCos = (float)Math.Cos(bank);
    var bankSin = (float)Math.Sin(bank);
    var bankUp = bankCos * up + bankSin * right;
    var bankRight = -bankSin * up + bankCos * right;

    // Construct quaternion from basis vectors
    var orientation = QuaternionFromBasis(forward, bankRight, bankUp);

    samples.Add(new BakedSample {
      Position = position,
      Orientation = orientation,
      Bank = bank,
      ArcLength = arcLength,
    });
  }

  private static Quaternion QuaternionFromBasis(Vector3 forward, Vector3 right, Vector3 up) {
    // Convert basis vectors to quaternion using Shepperd's method
    var trace = forward.X + right.Y + up.Z;

    if (trace > 0) {
      var s = 0.5f / (float)Math.Sqrt(trace + 1f);
      return new Quaternion(
        (up.Y - right.Z) * s,
        (forward.Z - up.X) * s,
        (right.X - forward.Y) * s,
        0.25f / s
      );
    } else if (forward.X > right.Y && forward.X > up.Z) {
      var s = 2f * (float)Math.Sqrt(1f + forward.X - right.Y - up.Z);
      return new Quaternion(
        0.25f * s,
        (forward.Y + right.X) / s,
        (forward.Z + up.X) / s,
        (up.Y - right.Z) / s
      );
    } else if (right.Y > up.Z) {
      var s = 2f * (float)Math.Sqrt(1f + right.Y - forward.X - up.Z);
      return new Quaternion(
        (forward.Y + right.X) / s,
        0.25f * s,
        (right.Z + up.Y) / s,
        (forward.Z - up.X) / s
      );
    } else {
      var s = 2f * (float)Math.Sqrt(1f + up.Z - forward.X - right.Y);
      return new Quaternion(
        (forward.Z + up.X) / s,
        (right.Z + up.Y) / s,
        0.25f * s,
        (right.X - forward.Y) / s
      );
    }
  }
}
