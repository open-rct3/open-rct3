// TransformTests
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
public class TransformTests {
  private const float Epsilon = 1e-4f;

  private static Vector3 TransformPoint(Transform transform, Vector3 point) =>
    Vector3.Transform(point, transform.Matrix);

  [Test]
  public void Translate_Vector3_MovesOriginByOffset() {
    var transform = new Transform();

    transform.Translate(new Vector3(1, 2, 3));

    Assert.That(TransformPoint(transform, Vector3.Zero), Is.EqualTo(new Vector3(1, 2, 3)).Using(ApproximatelyEqual));
  }

  [Test]
  public void Translate_Components_MatchesVector3Overload() {
    var transform = new Transform();

    transform.Translate(1f, 2f, 3f);

    Assert.That(TransformPoint(transform, Vector3.Zero), Is.EqualTo(new Vector3(1, 2, 3)).Using(ApproximatelyEqual));
  }

  [Test]
  public void Translate_IsRelativeToTheExistingMatrix() {
    // Two successive Translate calls must accumulate, not overwrite - i.e. Matrix is post-multiplied,
    // matching the "relative" translation this helper promises rather than resetting the transform.
    var transform = new Transform();

    transform.Translate(new Vector3(1, 0, 0));
    transform.Translate(new Vector3(0, 1, 0));

    Assert.That(TransformPoint(transform, Vector3.Zero), Is.EqualTo(new Vector3(1, 1, 0)).Using(ApproximatelyEqual));
  }

  [Test]
  public void RotateZ_NinetyDegrees_RotatesXAxisOntoYAxis() {
    var transform = new Transform();

    transform.RotateZ(90f);

    Assert.That(TransformPoint(transform, Vector3.UnitX), Is.EqualTo(Vector3.UnitY).Using(ApproximatelyEqual));
  }

  [Test]
  public void RotateX_NinetyDegrees_RotatesYAxisOntoZAxis() {
    var transform = new Transform();

    transform.RotateX(90f);

    Assert.That(TransformPoint(transform, Vector3.UnitY), Is.EqualTo(Vector3.UnitZ).Using(ApproximatelyEqual));
  }

  [Test]
  public void RotateY_NinetyDegrees_RotatesZAxisOntoXAxis() {
    var transform = new Transform();

    transform.RotateY(90f);

    Assert.That(TransformPoint(transform, Vector3.UnitZ), Is.EqualTo(Vector3.UnitX).Using(ApproximatelyEqual));
  }

  [Test]
  public void Rotate_AroundArbitraryAxis_MatchesEquivalentAxisSpecificHelper() {
    var transform = new Transform();
    var expected = new Transform();

    transform.Rotate(Vector3.UnitZ, 90f);
    expected.RotateZ(90f);

    Assert.That(transform.Matrix, Is.EqualTo(expected.Matrix).Using(MatrixApproximatelyEqual));
  }

  [Test]
  public void Rotate_NormalizesANonUnitAxis() {
    var transform = new Transform();
    var expected = new Transform();

    // A non-unit axis vector must be normalized internally, so rotating around (0, 0, 5) produces the
    // same result as rotating around (0, 0, 1).
    transform.Rotate(new Vector3(0, 0, 5), 90f);
    expected.RotateZ(90f);

    Assert.That(transform.Matrix, Is.EqualTo(expected.Matrix).Using(MatrixApproximatelyEqual));
  }

  [Test]
  public void RotateZ_IsRelativeToAnExistingTranslation() {
    // Rotation is post-multiplied onto whatever's already in Matrix, so a prior Translate stays in
    // effect (rotation composes with it, rather than replacing it) - the "relative" contract this
    // class of helper promises.
    var transform = new Transform();
    transform.Translate(new Vector3(1, 0, 0));

    transform.RotateZ(90f);

    // Rotating the whole transform 90° about Z spins its translation component too: (1,0,0) -> (0,1,0).
    Assert.That(TransformPoint(transform, Vector3.Zero), Is.EqualTo(new Vector3(0, 1, 0)).Using(ApproximatelyEqual));
  }

  private static readonly Comparison<Vector3> ApproximatelyEqual =
    (a, b) => Vector3.Distance(a, b) < Epsilon ? 0 : 1;

  private static readonly Comparison<Matrix4x4> MatrixApproximatelyEqual =
    (a, b) => {
      var diff = a - b;
      var maxAbs = Math.Max(
        Math.Max(Math.Max(Math.Abs(diff.M11), Math.Abs(diff.M12)), Math.Max(Math.Abs(diff.M13), Math.Abs(diff.M14))),
        Math.Max(
          Math.Max(Math.Max(Math.Abs(diff.M21), Math.Abs(diff.M22)), Math.Max(Math.Abs(diff.M23), Math.Abs(diff.M24))),
          Math.Max(
            Math.Max(Math.Max(Math.Abs(diff.M31), Math.Abs(diff.M32)), Math.Max(Math.Abs(diff.M33), Math.Abs(diff.M34))),
            Math.Max(Math.Max(Math.Abs(diff.M41), Math.Abs(diff.M42)), Math.Max(Math.Abs(diff.M43), Math.Abs(diff.M44))))));
      return maxAbs < Epsilon ? 0 : 1;
    };
}
