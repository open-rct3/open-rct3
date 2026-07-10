// CameraTests
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
public class CameraTests {
  private const float Epsilon = 1e-3f;

  private static float NdcZ(Matrix4x4 viewProj, Vector3 worldPos) {
    var clip = Vector4.Transform(new Vector4(worldPos, 1f), viewProj);
    return clip.Z / clip.W;
  }

  [Test]
  public void CreatePerspectiveFieldOfViewGL_NearPlaneMapsToNegativeOneNdcZ() {
    // Regression test: Matrix4x4.CreatePerspectiveFieldOfView targets Direct3D's [0, 1] NDC-z range,
    // which silently compresses the visible depth range into the back half of the depth buffer when
    // fed to OpenGL (which expects [-1, 1]). A camera looking down -Z with a point exactly `near` units
    // in front of it must land at NDC z == -1, not 0.
    var proj = Camera.CreatePerspectiveFieldOfViewGL(MathF.PI / 3f, aspectRatio: 1f, nearPlaneDistance: 0.1f, farPlaneDistance: 1000f);
    var view = Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0, 0, -1), Vector3.UnitY);
    var viewProj = view * proj;

    var ndcZ = NdcZ(viewProj, new Vector3(0, 0, -0.1f));

    Assert.That(ndcZ, Is.EqualTo(-1f).Within(Epsilon));
  }

  [Test]
  public void CreatePerspectiveFieldOfViewGL_FarPlaneMapsToPositiveOneNdcZ() {
    var proj = Camera.CreatePerspectiveFieldOfViewGL(MathF.PI / 3f, aspectRatio: 1f, nearPlaneDistance: 0.1f, farPlaneDistance: 1000f);
    var view = Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0, 0, -1), Vector3.UnitY);
    var viewProj = view * proj;

    var ndcZ = NdcZ(viewProj, new Vector3(0, 0, -1000f));

    Assert.That(ndcZ, Is.EqualTo(1f).Within(Epsilon));
  }

  [Test]
  public void CreatePerspectiveFieldOfViewGL_MidpointMapsWithinNdcRange() {
    // A sanity bound: every point strictly between the near and far planes must land strictly within
    // [-1, 1], not just the two endpoints.
    var proj = Camera.CreatePerspectiveFieldOfViewGL(MathF.PI / 3f, aspectRatio: 1f, nearPlaneDistance: 0.1f, farPlaneDistance: 1000f);
    var view = Matrix4x4.CreateLookAt(Vector3.Zero, new Vector3(0, 0, -1), Vector3.UnitY);
    var viewProj = view * proj;

    var ndcZ = NdcZ(viewProj, new Vector3(0, 0, -50f));

    Assert.That(ndcZ, Is.InRange(-1f, 1f));
  }

  [Test]
  public void Frame_PlacesEyeAtGivenDistanceFromTarget() {
    var camera = new Camera();
    var target = new Vector3(10, 20, 0);

    camera.Frame(target, distance: 100f);

    Assert.That(camera.Target, Is.EqualTo(target));
    Assert.That(Vector3.Distance(camera.Eye, camera.Target), Is.EqualTo(100f).Within(Epsilon));
  }

  [Test]
  public void Frame_KeepsTheSameViewingDirectionRegardlessOfTarget() {
    var camera = new Camera();
    var defaultDirection = Vector3.Normalize(camera.Eye - camera.Target);

    camera.Frame(new Vector3(500, -300, 0), distance: 50f);
    var framedDirection = Vector3.Normalize(camera.Eye - camera.Target);

    Assert.That(Vector3.Distance(defaultDirection, framedDirection), Is.EqualTo(0f).Within(Epsilon));
  }

  [Test]
  public void Update_TargetProjectsNearScreenCenter() {
    var camera = new Camera();
    camera.Frame(new Vector3(1000, -1000, 0), distance: 500f);

    camera.Update(aspectRatio: 1f);
    Assert.That(camera.Value, Is.Not.Null);
    var clip = Vector4.Transform(new Vector4(camera.Target, 1f), camera.Value!.Value);
    var ndc = new Vector2(clip.X / clip.W, clip.Y / clip.W);

    Assert.That(ndc.Length(), Is.LessThan(Epsilon));
  }

  [Test]
  public void Update_TargetProjectsNearScreenCenter_UnderColumnVectorConvention() {
    // Regression test for the class of bug found in .agents/bugs/terrain-render-black-and-misoriented.md:
    // Camera.Value is a row-vector matrix (v' = v * M, per System.Numerics.Matrix4x4), but the GPU
    // consumes it as a column-vector matrix (v' = Mgl * v) via OpenRCT3.OpenGL.MatrixExtensions.ToGl(),
    // which the GDK project cannot reference directly. This test proves Camera's own math is at least
    // internally reconcilable with column-vector semantics by manually transposing camera.Value (the
    // conversion ToGl() must perform) and re-deriving the same NDC result the row-vector test above
    // gets - catching a regression if the row-vector/column-vector convention mismatch reappears at the
    // Camera level, independent of the game project's ToGl() implementation.
    var camera = new Camera();
    camera.Frame(new Vector3(1000, -1000, 0), distance: 500f);
    camera.Update(aspectRatio: 1f);
    Assert.That(camera.Value, Is.Not.Null);

    var transposed = Matrix4x4.Transpose(camera.Value!.Value);
    var point = new Vector4(camera.Target, 1f);
    // Simulate GLSL's `mat4 * vec4`: result_i = sum_j M[i, j] * v[j], with M read row-by-row from the
    // transposed matrix - i.e. exactly what ToGl() + glUniformMatrix4fv(transpose: false) hands the GPU.
    var clip = new Vector4(
      (transposed.M11 * point.X) + (transposed.M12 * point.Y) + (transposed.M13 * point.Z) + (transposed.M14 * point.W),
      (transposed.M21 * point.X) + (transposed.M22 * point.Y) + (transposed.M23 * point.Z) + (transposed.M24 * point.W),
      (transposed.M31 * point.X) + (transposed.M32 * point.Y) + (transposed.M33 * point.Z) + (transposed.M34 * point.W),
      (transposed.M41 * point.X) + (transposed.M42 * point.Y) + (transposed.M43 * point.Z) + (transposed.M44 * point.W));
    var ndc = new Vector2(clip.X / clip.W, clip.Y / clip.W);

    Assert.That(ndc.Length(), Is.LessThan(Epsilon));
  }

  [Test]
  public void Update_NearPlaneIsOneCentimeter() {
    var camera = new Camera();
    camera.Frame(Vector3.Zero, distance: 500f);
    camera.Update(aspectRatio: 1f);
    Assert.That(camera.Value, Is.Not.Null);

    const float near = 0.01f;
    var direction = Vector3.Normalize(camera.Target - camera.Eye);
    var nearPoint = camera.Eye + (direction * near);

    Assert.That(NdcZ(camera.Value!.Value, nearPoint), Is.EqualTo(-1f).Within(Epsilon));
  }

  [Test]
  public void Update_FarPlaneScalesWithFramingDistance() {
    // Regression test: the far clip plane used to be a fixed 1000-unit constant, which the default
    // 128x128 park's framing distance (~1303, see .agents/bugs/terrain-render-black-and-misoriented.md)
    // already exceeded, silently culling the entire terrain mesh. Camera.Update now derives the far
    // plane as a multiple of the current eye-to-target distance, so it must scale with whatever distance
    // Frame() was given - proven here across several very different distances, not just the default map.
    foreach (var distance in new[] { 100f, 1303f, 5000f }) {
      var camera = new Camera();
      camera.Frame(Vector3.Zero, distance);
      camera.Update(aspectRatio: 1f);
      Assert.That(camera.Value, Is.Not.Null);

      // A point exactly `distance * 2` from Eye, continuing straight past Target along the view axis:
      // Eye - Target = direction * distance, so 2*Target - Eye = Target - (Eye - Target) sits `distance`
      // further past Target, i.e. `distance * 2` total from Eye.
      var farPoint = (2 * camera.Target) - camera.Eye;

      Assert.That(NdcZ(camera.Value!.Value, farPoint), Is.EqualTo(1f).Within(Epsilon),
        $"far plane did not land at 2x the framing distance ({distance})");
    }
  }
}
