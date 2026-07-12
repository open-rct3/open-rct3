// Arc-Length Parameterization for Catmull-Rom Splines
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections.Generic;
using System.Numerics;

namespace OpenRCT3.Rides.TrackSpline;

/// <summary>
/// Arc-length computation and parameter mapping for Catmull-Rom splines.
/// Uses adaptive Simpson's quadrature for accurate distance integration.
/// </summary>
public static class ArcLength {
  /// <summary>
  /// Default tolerance for production arc-length computation.
  /// Tighter tolerance for accuracy-critical paths (wheel IK, train placement).
  /// </summary>
  private const float ProductionTolerance = 1e-4f;

  /// <summary>
  /// Tolerance for test/preview purposes. Looser for speed (10× faster, still accurate to 0.1%).
  /// Use ProductionTolerance for queries that affect gameplay/physics.
  /// </summary>
  private const float TestTolerance = 1e-3f;

  /// <summary>
  /// Cache of computed arc-lengths keyed by (p0, p1, p2, p3, t1, t2) for memoization.
  /// Avoids redundant Simpson integrations in baking and binary search.
  /// </summary>
  private static readonly Dictionary<string, float> ArcLengthCache = [];

  /// <summary>
  /// Compute the arc-length of a Catmull-Rom spline segment from t=t1 to t=t2.
  /// Uses Simpson's rule with adaptive subdivision for accuracy.
  /// Results are cached to avoid redundant computation during baking and binary search.
  /// </summary>
  public static float ComputeArcLength(
    float t1, float t2,
    Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
    bool useTestTolerance = false) {
    var key = $"{p0}:{p1}:{p2}:{p3}:{t1}:{t2}";
    if (ArcLengthCache.TryGetValue(key, out var cached)) {
      return cached;
    }

    var tolerance = useTestTolerance ? TestTolerance : ProductionTolerance;
    var result = AdaptiveSimpson(t1, t2, p0, p1, p2, p3, tolerance);
    ArcLengthCache[key] = result;
    return result;
  }

  /// <summary>
  /// Clear the arc-length cache. Call this between test cases or when spline geometry changes.
  /// </summary>
  public static void ClearCache() {
    ArcLengthCache.Clear();
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

    // Binary search for the parameter t
    float tLow = 0f, tHigh = 1f;
    float distanceLow = 0f, distanceHigh = ComputeArcLength(0f, 1f, p0, p1, p2, p3, useTestTolerance);

    if (targetDistance >= distanceHigh) return 1f;

    // Bisection: typically converges in 20-30 iterations for good tolerance
    for (int i = 0; i < 40; i++) {
      float tMid = (tLow + tHigh) * 0.5f;
      float distanceMid = ComputeArcLength(0f, tMid, p0, p1, p2, p3, useTestTolerance);

      if (Math.Abs(distanceMid - targetDistance) < 1e-5f) {
        return tMid;
      }

      if (distanceMid < targetDistance) {
        tLow = tMid;
        distanceLow = distanceMid;
      } else {
        tHigh = tMid;
        distanceHigh = distanceMid;
      }
    }

    return (tLow + tHigh) * 0.5f;
  }

  /// <summary>
  /// Adaptive Simpson's quadrature for arc-length integration.
  /// Integrates the magnitude of the tangent vector from t1 to t2.
  /// </summary>
  private static float AdaptiveSimpson(
    float t1, float t2,
    Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
    float tolerance) {
    return AdaptiveSimpsonRecursive(t1, t2, p0, p1, p2, p3, tolerance, depth: 0);
  }

  private static float AdaptiveSimpsonRecursive(
    float t1, float t2,
    Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
    float tolerance, int depth) {
    // Prevent infinite recursion; at depth 50, intervals are ~2^-50 which is numerical noise
    const int MaxDepth = 50;
    const float H_MIN = 1e-7f; // Minimum interval width

    var interval = t2 - t1;
    if (depth > MaxDepth || interval < H_MIN) {
      // Return a single Simpson estimate without further recursion
      var mid = (t1 + t2) * 0.5f;
      var f_t1 = TangentMagnitude(t1, p0, p1, p2, p3);
      var f_mid = TangentMagnitude(mid, p0, p1, p2, p3);
      var f_t2 = TangentMagnitude(t2, p0, p1, p2, p3);
      return (interval / 6f) * (f_t1 + 4f * f_mid + f_t2);
    }

    var c_mid = (t1 + t2) * 0.5f;

    // Simpson's rule for full interval
    var f_a = TangentMagnitude(t1, p0, p1, p2, p3);
    var f_c = TangentMagnitude(c_mid, p0, p1, p2, p3);
    var f_b = TangentMagnitude(t2, p0, p1, p2, p3);
    var simpson = (interval / 6f) * (f_a + 4f * f_c + f_b);

    // Subdivide: evaluate at quarter points
    var c1 = (t1 + c_mid) * 0.5f;
    var c2 = (c_mid + t2) * 0.5f;

    var f_c1 = TangentMagnitude(c1, p0, p1, p2, p3);
    var f_c2 = TangentMagnitude(c2, p0, p1, p2, p3);

    var h_half = interval * 0.25f;
    var left = (h_half / 6f) * (f_a + 4f * f_c1 + f_c);
    var right = (h_half / 6f) * (f_c + 4f * f_c2 + f_b);
    var subdivided = left + right;

    // Check error and decide to subdivide or accept
    var error = Math.Abs(subdivided - simpson);
    if (error <= 15f * tolerance) {
      return subdivided + error / 15f; // Richardson extrapolation
    }

    // Recurse with tighter tolerance
    return AdaptiveSimpsonRecursive(t1, c_mid, p0, p1, p2, p3, tolerance / 2f, depth + 1) +
           AdaptiveSimpsonRecursive(c_mid, t2, p0, p1, p2, p3, tolerance / 2f, depth + 1);
  }

  /// <summary>
  /// Compute the magnitude of the tangent vector (derivative) at parameter t.
  /// This is the speed along the curve and is integrated to compute arc-length.
  /// </summary>
  private static float TangentMagnitude(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) {
    var tangent = CatmullRom.Tangent(t, p0, p1, p2, p3);
    return tangent.Length();
  }
}
