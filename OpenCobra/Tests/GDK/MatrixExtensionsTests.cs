// MatrixExtensionsTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Numerics;
using NUnit.Framework;
using OpenCobra.GDK;

namespace OpenCobra.Tests.GDK;

[TestFixture]
public class MatrixExtensionsTests {
  private const float Epsilon = 1e-5f;

  [Test]
  public void ToGl_TranslationLandsInLastColumnNotLastRow() {
    // Regression test: Matrix4x4 is a row-vector matrix (translation in M41/M42/M43), but GLSL's
    // `mat4 * vec4` is column-vector and needs translation in the last column. ToGl() must actually
    // transpose the matrix, not just relabel its storage order as column-major - a bug that shipped
    // silently because no prior test ever inspected ToGl()'s output directly (see
    // .agents/bugs/terrain-render-black-and-misoriented.md).
    var translation = Matrix4x4.CreateTranslation(2f, 3f, 4f);

    var glArray = translation.ToGl();

    // Column-major: index 12/13/14 is row 0..2 of column 3 - i.e. the translation column GLSL reads.
    Assert.That(glArray[12], Is.EqualTo(2f).Within(Epsilon));
    Assert.That(glArray[13], Is.EqualTo(3f).Within(Epsilon));
    Assert.That(glArray[14], Is.EqualTo(4f).Within(Epsilon));
    Assert.That(glArray[15], Is.EqualTo(1f).Within(Epsilon));
    // Bottom row (indices 3, 7, 11) must NOT carry the translation - that was the actual bug.
    Assert.That(glArray[3], Is.EqualTo(0f).Within(Epsilon));
    Assert.That(glArray[7], Is.EqualTo(0f).Within(Epsilon));
    Assert.That(glArray[11], Is.EqualTo(0f).Within(Epsilon));
  }

  [Test]
  public void ToGl_MatchesRowVectorTransformWhenAppliedAsColumnVectorMultiply() {
    // End-to-end proof: transforming a point via .NET's row-vector convention (v * M) must equal
    // transforming the same point via GLSL's column-vector convention (Mgl * v), where Mgl is built
    // from ToGl()'s output read back column-major. This is the "C#-math-vs-GPU-render gap" the bug
    // doc identified as never having been closed.
    var matrix = Matrix4x4.CreateTranslation(5f, -7f, 11f) * Matrix4x4.CreateRotationY(MathF.PI / 4f);
    var point = new Vector3(1f, 2f, 3f);

    var expected = Vector3.Transform(point, matrix);

    var glArray = matrix.ToGl();
    var actual = MultiplyColumnMajorByColumnVector(glArray, point);

    Assert.That(actual.X, Is.EqualTo(expected.X).Within(Epsilon));
    Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(Epsilon));
    Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(Epsilon));
  }

  private static Vector3 MultiplyColumnMajorByColumnVector(float[] glArray, Vector3 point) {
    // Reconstruct GLSL's `mat4 * vec4(point, 1.0)` from a column-major float[16], exactly as the GPU
    // would read it: 4 consecutive floats = 1 column.
    Vector4 Column(int i) => new(glArray[i * 4], glArray[i * 4 + 1], glArray[i * 4 + 2], glArray[i * 4 + 3]);
    var c0 = Column(0);
    var c1 = Column(1);
    var c2 = Column(2);
    var c3 = Column(3);

    var result = (c0 * point.X) + (c1 * point.Y) + (c2 * point.Z) + c3;
    return new Vector3(result.X, result.Y, result.Z);
  }
}
