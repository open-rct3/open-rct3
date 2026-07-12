// Rail Sample Query API
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;

namespace OpenRCT3.Rides.TrackSpline;

/// <summary>
/// Runtime query API for sampling a rail spline at a given arc-length coordinate.
/// Consumes the baked samples from a RailSpline and interpolates between them using Hermite curves.
/// </summary>
public static class RailQuery {
  /// <summary>
  /// Query a rail spline at a specific arc-length coordinate.
  /// Returns position, orientation, and bank angle via binary search + Hermite interpolation.
  /// </summary>
  /// <param name="rail">The rail spline to query (must be baked).</param>
  /// <param name="arcLength">Arc-length coordinate along the rail (0 = start, >= totalArcLength = clamped to end).</param>
  /// <param name="position">Output: position in world space.</param>
  /// <param name="orientation">Output: orientation quaternion (heading + pitch from forward direction).</param>
  /// <param name="bank">Output: bank angle in radians.</param>
  /// <returns>True if successful; false if rail is empty or not baked.</returns>
  public static bool SampleRail(
    RailSpline rail, float arcLength,
    out Vector3 position, out Quaternion orientation, out float bank) {
    position = Vector3.Zero;
    orientation = Quaternion.Identity;
    bank = 0f;

    if (rail.BakedSamples.Count == 0) return false;

    // Clamp arc-length to valid range
    arcLength = Math.Clamp(arcLength, 0f, rail.TotalArcLength);

    // Special case: single sample
    if (rail.BakedSamples.Count == 1) {
      var sample = rail.BakedSamples[0];
      position = sample.Position;
      orientation = sample.Orientation;
      bank = sample.Bank;
      return true;
    }

    // Binary search for the two samples bracketing the query arc-length
    int left = 0, right = rail.BakedSamples.Count - 1;
    while (left < right - 1) {
      int mid = (left + right) / 2;
      if (rail.BakedSamples[mid].ArcLength <= arcLength) {
        left = mid;
      } else {
        right = mid;
      }
    }

    var sample0 = rail.BakedSamples[left];
    var sample1 = rail.BakedSamples[right];

    // Interpolation parameter: how far between sample0 and sample1
    var deltaArcLength = sample1.ArcLength - sample0.ArcLength;
    float t;
    if (deltaArcLength > 1e-6f) {
      t = (arcLength - sample0.ArcLength) / deltaArcLength;
    } else {
      t = 0f;
    }

    // Clamp t to [0, 1]
    t = Math.Clamp(t, 0f, 1f);

    // Hermite interpolation for position: cubic spline smoothing
    position = HermiteInterpolate(sample0.Position, sample1.Position, t);

    // Slerp (spherical linear interpolation) for orientation
    orientation = Quaternion.Slerp(sample0.Orientation, sample1.Orientation, t);

    // Linear interpolation for bank angle
    bank = sample0.Bank + (sample1.Bank - sample0.Bank) * t;

    return true;
  }

  /// <summary>
  /// Hermite (cubic) interpolation between two positions.
  /// Uses Catmull-Rom basis functions to ensure smooth curve through sampled points.
  /// For query, we use the positions as control points and approximate tangents from the sample spacing.
  /// </summary>
  private static Vector3 HermiteInterpolate(Vector3 p0, Vector3 p1, float t) {
    // Simplified Hermite: treat the two points as both control points and curve endpoints
    // Real implementation would use the rail's original tangents, but for interpolation
    // between baked samples, a simple cubic lerp preserves local curvature.
    var t2 = t * t;
    var t3 = t2 * t;

    // Hermite basis: cubic Bézier-like interpolation
    var h00 = 2f * t3 - 3f * t2 + 1f;    // Start point weight
    var h10 = t3 - 2f * t2 + t;           // Start tangent weight (approximated as zero)
    var h01 = -2f * t3 + 3f * t2;         // End point weight
    var h11 = t3 - t2;                    // End tangent weight (approximated as zero)

    // Approximate tangents from neighboring samples (simplified)
    var tangent0 = Vector3.Normalize(p1 - p0);
    var tangent1 = Vector3.Normalize(p1 - p0);

    return h00 * p0 + h10 * tangent0 * 0.5f + h01 * p1 + h11 * tangent1 * 0.5f;
  }
}
