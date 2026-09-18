// Arc-Length Parameterization for Catmull-Rom Splines
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;

namespace OpenRCT3.Rides.TrackSpline;

/// <summary>
/// Arc-length computation and parameter mapping for Catmull-Rom splines.
/// Builds a fixed-resolution lookup table (LUT) per segment via forward differencing, then answers
/// distance/parameter queries by binary search + linear interpolation against the table. This avoids
/// re-integrating the curve on every query, which is what made per-node adaptive quadrature during
/// baking too slow to hit the per-piece bake budget.
/// </summary>
public static class ArcLength {
  /// <summary>
  /// LUT sample count used when <c>useTestTolerance</c> is set, for faster (lower-fidelity) unit tests.
  /// </summary>
  private const int TestSampleCount = 8;

  /// <summary>
  /// A fixed-resolution table mapping parameter t to cumulative arc-length, built once per curve segment.
  /// </summary>
  internal readonly struct Lut {
    public readonly float[] Parameters;
    public readonly float[] CumulativeLength;

    public Lut(float[] parameters, float[] cumulativeLength) {
      Parameters = parameters;
      CumulativeLength = cumulativeLength;
    }

    public float TotalLength => CumulativeLength[^1];
  }

  /// <summary>
  /// Build an arc-length LUT for a Catmull-Rom segment by forward-differencing at <paramref name="sampleCount"/>
  /// fixed parameter steps and summing chord lengths. O(sampleCount), no recursion.
  /// </summary>
  internal static Lut BuildLut(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, int sampleCount) {
    var count = sampleCount + 1;
    var parameters = new float[count];
    var cumulativeLength = new float[count];

    var previousPosition = CatmullRom.Evaluate(0f, p0, p1, p2, p3);
    parameters[0] = 0f;
    cumulativeLength[0] = 0f;

    for (int i = 1; i < count; i++) {
      var t = (float)i / sampleCount;
      var position = CatmullRom.Evaluate(t, p0, p1, p2, p3);
      cumulativeLength[i] = cumulativeLength[i - 1] + Vector3.Distance(previousPosition, position);
      parameters[i] = t;
      previousPosition = position;
    }

    return new Lut(parameters, cumulativeLength);
  }

  /// <summary>
  /// Interpolate cumulative arc-length at an arbitrary parameter t via binary search + lerp against the LUT.
  /// </summary>
  internal static float ArcLengthAt(Lut lut, float t) {
    t = Math.Clamp(t, 0f, 1f);
    var index = LowerBound(lut.Parameters, t);
    if (index <= 0) return lut.CumulativeLength[0];
    if (index >= lut.Parameters.Length) return lut.TotalLength;

    var t0 = lut.Parameters[index - 1];
    var t1 = lut.Parameters[index];
    var fraction = t1 > t0 ? (t - t0) / (t1 - t0) : 0f;
    return lut.CumulativeLength[index - 1] + fraction * (lut.CumulativeLength[index] - lut.CumulativeLength[index - 1]);
  }

  /// <summary>
  /// Interpolate the parameter t at an arbitrary cumulative arc-length via binary search + lerp (inverse of
  /// <see cref="ArcLengthAt"/>). Assumes <paramref name="distance"/> is within [0, lut.TotalLength].
  /// </summary>
  internal static float ParameterAt(Lut lut, float distance) {
    var index = LowerBound(lut.CumulativeLength, distance);
    if (index <= 0) return lut.Parameters[0];
    if (index >= lut.CumulativeLength.Length) return lut.Parameters[^1];

    var d0 = lut.CumulativeLength[index - 1];
    var d1 = lut.CumulativeLength[index];
    var fraction = d1 > d0 ? (distance - d0) / (d1 - d0) : 0f;
    return lut.Parameters[index - 1] + fraction * (lut.Parameters[index] - lut.Parameters[index - 1]);
  }

  /// <summary>Smallest index i such that values[i] >= target, over a monotonically increasing array.</summary>
  private static int LowerBound(float[] values, float target) {
    int lo = 0, hi = values.Length;
    while (lo < hi) {
      int mid = (lo + hi) / 2;
      if (values[mid] < target) lo = mid + 1; else hi = mid;
    }
    return lo;
  }

  /// <summary>
  /// Build the arc-length LUT for a segment at the resolution baking/queries should use: a small fixed
  /// count for fast/lower-fidelity unit tests, or <see cref="BakingConfig.ArcLengthSampleCount"/> otherwise.
  /// </summary>
  internal static Lut BuildLutForQuery(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, bool useTestTolerance) =>
    BuildLut(p0, p1, p2, p3, useTestTolerance ? TestSampleCount : BakingConfig.ArcLengthSampleCount);

  /// <summary>
  /// Compute the arc-length of a Catmull-Rom spline segment from t=t1 to t=t2.
  /// </summary>
  public static float ComputeArcLength(
    float t1, float t2,
    Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
    bool useTestTolerance = false) {
    var lut = BuildLutForQuery(p0, p1, p2, p3, useTestTolerance);
    return ArcLengthAt(lut, t2) - ArcLengthAt(lut, t1);
  }

  /// <summary>
  /// Find the parameter t such that arc-length from t=0 to t equals targetDistance.
  /// Returns t in [0, 1]; if targetDistance > total segment length, returns 1.
  /// </summary>
  public static float ParameterAtDistance(
    float targetDistance,
    Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
    bool useTestTolerance = false) {
    if (targetDistance <= 0f) return 0f;

    var lut = BuildLutForQuery(p0, p1, p2, p3, useTestTolerance);
    if (targetDistance >= lut.TotalLength) return 1f;

    return ParameterAt(lut, targetDistance);
  }
}
