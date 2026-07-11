// MatrixExtensions
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;

namespace OpenCobra.GDK;

/// <remarks>
/// Lives in the engine layer, not the app layer, because every GDK type that produces a
/// <see cref="Matrix4x4"/> destined for a GLSL uniform (<see cref="Camera"/>, <see cref="ImDraw"/>)
/// needs this same row-vector-to-column-major conversion — it's GPU-serialization knowledge tied to
/// what those types produce, not something specific to any one renderer.
/// </remarks>
public static class MatrixExtensions {
  /// <summary>
  /// Convert this 4x4 matrix into the column-major float array GLSL expects, transposed to account for
  /// <see cref="Matrix4x4"/>'s row-vector convention (translation in <c>M41</c>/<c>M42</c>/<c>M43</c>)
  /// versus GLSL's column-vector convention (translation must be in the last column). Emitting the
  /// fields in natural declared order, unlike, say, a manual index swap, produces exactly that transpose
  /// when read back column-major: row-major storage of <c>M</c> equals column-major storage of
  /// <c>Mᵀ</c>.
  /// </summary>
  /// <param name="matrix">A row-major <see cref="Matrix4x4"/></param>
  /// <returns>New float array, column-major, representing the transpose of <paramref name="matrix"/>.</returns>
  public static float[] ToGl(this Matrix4x4 matrix) => [
    matrix.M11, matrix.M12, matrix.M13, matrix.M14,
    matrix.M21, matrix.M22, matrix.M23, matrix.M24,
    matrix.M31, matrix.M32, matrix.M33, matrix.M34,
    matrix.M41, matrix.M42, matrix.M43, matrix.M44
  ];
}
