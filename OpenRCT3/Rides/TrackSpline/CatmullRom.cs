// Catmull-Rom Spline Math
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;

namespace OpenRCT3.Rides.TrackSpline;

/// <summary>
/// Catmull-Rom (Hermite) spline evaluation and tangent computation.
/// </summary>
public static class CatmullRom {
  /// <summary>
  /// Evaluate a Catmull-Rom spline at parameter t ∈ [0,1].
  /// The curve passes through p1 (at t=0) and p2 (at t=1), with tangent continuity defined by p0 and p3.
  /// </summary>
  /// <param name="t">Parameter in [0,1].</param>
  /// <param name="p0">Control point before the segment.</param>
  /// <param name="p1">Start of segment (t=0).</param>
  /// <param name="p2">End of segment (t=1).</param>
  /// <param name="p3">Control point after the segment.</param>
  /// <returns>Position on the spline at parameter t.</returns>
  public static Vector3 Evaluate(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) {
    var t2 = t * t;
    var t3 = t2 * t;

    // Catmull-Rom basis matrix coefficients.
    var a = -0.5f * t3 + t2 - 0.5f * t;
    var b = 1.5f * t3 - 2.5f * t2 + 1f;
    var c = -1.5f * t3 + 2f * t2 + 0.5f * t;
    var d = 0.5f * t3 - 0.5f * t2;

    return a * p0 + b * p1 + c * p2 + d * p3;
  }

  /// <summary>
  /// Evaluate the tangent (first derivative) of a Catmull-Rom spline at parameter t ∈ [0,1].
  /// </summary>
  /// <param name="t">Parameter in [0,1].</param>
  /// <param name="p0">Control point before the segment.</param>
  /// <param name="p1">Start of segment (t=0).</param>
  /// <param name="p2">End of segment (t=1).</param>
  /// <param name="p3">Control point after the segment.</param>
  /// <returns>Tangent vector (derivative with respect to t); not normalized.</returns>
  public static Vector3 Tangent(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3) {
    var t2 = t * t;

    // Derivative of Catmull-Rom basis matrix.
    var a = -1.5f * t2 + 2f * t - 0.5f;
    var b = 4.5f * t2 - 5f * t;
    var c = -4.5f * t2 + 4f * t + 0.5f;
    var d = 1.5f * t2 - t;

    return a * p0 + b * p1 + c * p2 + d * p3;
  }

  /// <summary>
  /// Evaluate a Catmull-Rom spline for scalar values (e.g., bank angle along the spline).
  /// </summary>
  public static float EvaluateScalar(float t, float v0, float v1, float v2, float v3) {
    var t2 = t * t;
    var t3 = t2 * t;

    var a = -0.5f * t3 + t2 - 0.5f * t;
    var b = 1.5f * t3 - 2.5f * t2 + 1f;
    var c = -1.5f * t3 + 2f * t2 + 0.5f * t;
    var d = 0.5f * t3 - 0.5f * t2;

    return a * v0 + b * v1 + c * v2 + d * v3;
  }

  /// <summary>
  /// Evaluate the tangent of a scalar-valued Catmull-Rom spline (e.g., rate of change of bank).
  /// </summary>
  public static float TangentScalar(float t, float v0, float v1, float v2, float v3) {
    var t2 = t * t;

    var a = -1.5f * t2 + 2f * t - 0.5f;
    var b = 4.5f * t2 - 5f * t;
    var c = -4.5f * t2 + 4f * t + 0.5f;
    var d = 1.5f * t2 - t;

    return a * v0 + b * v1 + c * v2 + d * v3;
  }
}
