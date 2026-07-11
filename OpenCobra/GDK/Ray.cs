// Ray
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Numerics;

namespace OpenCobra.GDK;

/// <summary>
/// A world-space ray, e.g. produced by <see cref="Camera.Unproject"/> for screen-to-world picking.
/// </summary>
/// <param name="Origin">The ray's world-space starting point.</param>
/// <param name="Direction">The ray's world-space direction. Expected to be unit length.</param>
public readonly record struct Ray(Vector3 Origin, Vector3 Direction) {
  /// <summary>
  /// Tests this ray against the triangle <paramref name="v0"/>, <paramref name="v1"/>,
  /// <paramref name="v2"/> using the Möller–Trumbore algorithm.
  /// </summary>
  /// <param name="v0">The triangle's first vertex.</param>
  /// <param name="v1">The triangle's second vertex.</param>
  /// <param name="v2">The triangle's third vertex.</param>
  /// <param name="point">The world-space intersection point, if any.</param>
  /// <returns>
  /// <c>true</c> if the ray hits the triangle at or in front of <see cref="Origin"/> (a hit strictly
  /// behind the origin does not count); <c>false</c> otherwise, including when the ray is parallel to
  /// the triangle's plane.
  /// </returns>
  public bool Intersects(Vector3 v0, Vector3 v1, Vector3 v2, out Vector3 point) {
    const float epsilon = 1e-6f;
    point = default;

    var edge1 = v1 - v0;
    var edge2 = v2 - v0;
    var pVec = Vector3.Cross(Direction, edge2);
    var det = Vector3.Dot(edge1, pVec);
    if (MathF.Abs(det) < epsilon) return false; // Ray is parallel to the triangle's plane.

    var invDet = 1f / det;
    var tVec = Origin - v0;
    var u = Vector3.Dot(tVec, pVec) * invDet;
    if (u < 0f || u > 1f) return false;

    var qVec = Vector3.Cross(tVec, edge1);
    var v = Vector3.Dot(Direction, qVec) * invDet;
    if (v < 0f || u + v > 1f) return false;

    var t = Vector3.Dot(edge2, qVec) * invDet;
    if (t < 0f) return false; // Behind the ray's origin.

    point = Origin + (Direction * t);
    return true;
  }
}
