// MatrixExtensions
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;

namespace OpenRCT3.OpenGL;

internal static class MatrixExtensions {
  /// <summary>
  /// Convert this 4x4 matrix into a column-major float array.
  /// </summary>
  /// <param name="matrix">A row-major <see cref="Matrix4x4"/></param>
  /// <returns>New float array in column-major order.</returns>
  public static float[] ToGl(this Matrix4x4 matrix) => [
    matrix.M11, matrix.M21, matrix.M31, matrix.M41,
    matrix.M12, matrix.M22, matrix.M32, matrix.M42,
    matrix.M13, matrix.M23, matrix.M33, matrix.M43,
    matrix.M14, matrix.M24, matrix.M34, matrix.M44
  ];
}
