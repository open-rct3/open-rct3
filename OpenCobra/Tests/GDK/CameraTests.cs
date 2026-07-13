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
    // Regression test for a row-vector/column-vector convention mismatch bug class:
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
    // The far clip plane must scale with whatever distance Frame() was given, not a fixed constant -
    // a fixed 1000-unit plane would cull the default 128x128 park's terrain mesh (framing distance
    // ~1303). Proven here across several very different distances, not just the default map.
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

  [Test]
  public void Update_FarPlaneStaysPinnedToMaxDistance_AndDoesNotShrinkWhenZoomingIn() {
    // Regression test: Update() used to size the far plane off the *live* eye-to-target distance, which
    // Zoom() shrinks continuously as the camera moves closer - even though the scene's actual extent
    // (what MaxDistance represents, e.g. "whole park") hasn't changed. That clipped terrain that should
    // still be visible any time the camera zoomed in, worse the closer it got. The far plane must stay
    // anchored to MaxDistance instead, regardless of the live zoomed-in distance.
    var camera = new Camera { MaxDistance = 1000f };
    camera.Frame(Vector3.Zero, distance: 1000f);

    camera.Zoom(-950f); // zoomed in close: live eye-to-target distance is now only 50
    camera.Update(aspectRatio: 1f);
    Assert.That(camera.Value, Is.Not.Null);

    // The far plane should still land at 2x MaxDistance (1000), not 2x the shrunk live distance (50).
    var direction = Vector3.Normalize(camera.Target - camera.Eye);
    var expectedFarPoint = camera.Eye + (direction * 1000f * 2f);

    Assert.That(NdcZ(camera.Value!.Value, expectedFarPoint), Is.EqualTo(1f).Within(Epsilon));
  }

  [Test]
  public void Update_FarPlaneStaysPinnedToMaxDistance_WhenZoomedInAndTiltedTowardTheGround() {
    // Same regression as above, but combined with a low Elevation (the other half of the reported bug:
    // "zoom in further and also down to ground-level") - the far plane must still be sized off
    // MaxDistance, not the shrunk live distance, regardless of viewing angle.
    var camera = new Camera { MaxDistance = 1000f };
    camera.Frame(Vector3.Zero, distance: 1000f);

    camera.Zoom(-950f);
    camera.Tilt(-1000f); // clamps to the minimum (near-horizon) elevation
    camera.Update(aspectRatio: 1f);
    Assert.That(camera.Value, Is.Not.Null);

    var direction = Vector3.Normalize(camera.Target - camera.Eye);
    var expectedFarPoint = camera.Eye + (direction * 1000f * 2f);

    Assert.That(NdcZ(camera.Value!.Value, expectedFarPoint), Is.EqualTo(1f).Within(Epsilon));
  }

  [Test]
  public void Update_FarPlaneFallsBackToLiveDistance_WhenMaxDistanceIsUnset() {
    // MaxDistance defaults to null (e.g. a camera Game.cs never wires up a park-framing distance for) -
    // Update() should still size the far plane off the live distance in that case, matching the original
    // pre-MaxDistance behavior, rather than defaulting to zero or throwing.
    var camera = new Camera();
    camera.Frame(Vector3.Zero, distance: 500f);

    camera.Zoom(-400f); // live distance now 100, MaxDistance still unset
    camera.Update(aspectRatio: 1f);
    Assert.That(camera.Value, Is.Not.Null);

    var direction = Vector3.Normalize(camera.Target - camera.Eye);
    var expectedFarPoint = camera.Eye + (direction * 100f * 2f);

    Assert.That(NdcZ(camera.Value!.Value, expectedFarPoint), Is.EqualTo(1f).Within(Epsilon));
  }

  [Test]
  public void RotateAzimuth_AccumulatesAcrossCalls() {
    var camera = new Camera();

    camera.RotateAzimuth(30f);
    camera.RotateAzimuth(15f);

    Assert.That(camera.Azimuth, Is.EqualTo(45f).Within(Epsilon));
  }

  [Test]
  public void RotateAzimuth_WrapsPositiveOverflowIntoZeroToThreeSixtyRange() {
    var camera = new Camera();

    camera.RotateAzimuth(350f);
    camera.RotateAzimuth(30f);

    Assert.That(camera.Azimuth, Is.EqualTo(20f).Within(Epsilon));
  }

  [Test]
  public void RotateAzimuth_WrapsNegativeUnderflowIntoZeroToThreeSixtyRange() {
    var camera = new Camera();

    camera.RotateAzimuth(10f);
    camera.RotateAzimuth(-30f);

    Assert.That(camera.Azimuth, Is.EqualTo(340f).Within(Epsilon));
  }

  [Test]
  public void RotateAzimuth_KeepsTheSameEyeToTargetDistance() {
    var camera = new Camera();
    camera.Frame(Vector3.Zero, distance: 250f);

    camera.RotateAzimuth(137f);

    Assert.That(Vector3.Distance(camera.Eye, camera.Target), Is.EqualTo(250f).Within(Epsilon));
  }

  [Test]
  public void Zoom_MovesEyeCloserForNegativeDelta() {
    var camera = new Camera();
    camera.Frame(Vector3.Zero, distance: 200f);

    camera.Zoom(-50f);

    Assert.That(Vector3.Distance(camera.Eye, camera.Target), Is.EqualTo(150f).Within(Epsilon));
  }

  [Test]
  public void Zoom_MovesEyeFartherForPositiveDelta() {
    var camera = new Camera();
    camera.Frame(Vector3.Zero, distance: 200f);

    camera.Zoom(50f);

    Assert.That(Vector3.Distance(camera.Eye, camera.Target), Is.EqualTo(250f).Within(Epsilon));
  }

  [Test]
  public void Zoom_ClampsToAMinimumDistanceSoTheEyeCannotReachTheTarget() {
    var camera = new Camera();
    camera.Frame(Vector3.Zero, distance: 10f);

    camera.Zoom(-1000f);

    Assert.That(Vector3.Distance(camera.Eye, camera.Target), Is.GreaterThan(0f));
  }

  [Test]
  public void Zoom_IsUnclampedByDefaultWhenZoomingOut() {
    // MaxDistance defaults to null - Zoom should be free to push the eye arbitrarily far away until a
    // caller opts into a cap.
    var camera = new Camera();
    camera.Frame(Vector3.Zero, distance: 200f);

    camera.Zoom(10_000f);

    Assert.That(Vector3.Distance(camera.Eye, camera.Target), Is.EqualTo(10_200f).Within(Epsilon));
  }

  [Test]
  public void Zoom_ClampsToMaxDistanceWhenSet() {
    var camera = new Camera { MaxDistance = 300f };
    camera.Frame(Vector3.Zero, distance: 250f);

    camera.Zoom(500f);

    Assert.That(Vector3.Distance(camera.Eye, camera.Target), Is.EqualTo(300f).Within(Epsilon));
  }

  [Test]
  public void Zoom_DoesNotClampBelowMaxDistanceWhenZoomingIn() {
    var camera = new Camera { MaxDistance = 300f };
    camera.Frame(Vector3.Zero, distance: 250f);

    camera.Zoom(-100f);

    Assert.That(Vector3.Distance(camera.Eye, camera.Target), Is.EqualTo(150f).Within(Epsilon));
  }

  [Test]
  public void Forward_IsUnitLengthAndFlatOnTheGroundPlane() {
    var camera = new Camera();

    Assert.That(camera.Forward.Length(), Is.EqualTo(1f).Within(Epsilon));
    Assert.That(camera.Forward.Z, Is.EqualTo(0f).Within(Epsilon));
  }

  [Test]
  public void Right_IsUnitLengthAndFlatOnTheGroundPlane() {
    var camera = new Camera();

    Assert.That(camera.Right.Length(), Is.EqualTo(1f).Within(Epsilon));
    Assert.That(camera.Right.Z, Is.EqualTo(0f).Within(Epsilon));
  }

  [Test]
  public void ForwardAndRight_AreOrthogonal() {
    var camera = new Camera();

    Assert.That(Vector3.Dot(camera.Forward, camera.Right), Is.EqualTo(0f).Within(Epsilon));
  }

  [Test]
  public void Forward_MatchesExpectedDirectionAtDefaultAzimuth() {
    // DefaultViewOffset is (20, -20, 50), i.e. eye sits South-East of target - Forward (eye toward target,
    // projected flat) should point North-West: (-1, 1, 0) normalized.
    var camera = new Camera();

    var expected = Vector3.Normalize(new Vector3(-1, 1, 0));
    Assert.That(Vector3.Distance(camera.Forward, expected), Is.EqualTo(0f).Within(Epsilon));
  }

  [Test]
  public void ForwardAndRight_RotateWithAzimuth() {
    var camera = new Camera();
    var forwardBefore = camera.Forward;
    var rightBefore = camera.Right;

    camera.RotateAzimuth(90f);

    var rotation = Matrix4x4.CreateRotationZ(90f * MathF.PI / 180f);
    var expectedForward = Vector3.Normalize(Vector3.Transform(forwardBefore, rotation));
    var expectedRight = Vector3.Normalize(Vector3.Transform(rightBefore, rotation));

    Assert.That(Vector3.Distance(camera.Forward, expectedForward), Is.EqualTo(0f).Within(Epsilon));
    Assert.That(Vector3.Distance(camera.Right, expectedRight), Is.EqualTo(0f).Within(Epsilon));
  }

  [Test]
  public void Pan_TranslatesTargetByDelta() {
    var camera = new Camera();
    var delta = new Vector3(30, -15, 0);

    camera.Pan(delta);

    Assert.That(Vector3.Distance(camera.Target, delta), Is.EqualTo(0f).Within(Epsilon));
  }

  [Test]
  public void Pan_TranslatesEyeByTheSameDelta_KeepingDistanceAndAzimuthUnchanged() {
    var camera = new Camera();
    camera.Frame(new Vector3(5, 5, 0), distance: 400f);
    camera.RotateAzimuth(60f);
    var eyeBefore = camera.Eye;
    var delta = new Vector3(30, -15, 0);

    camera.Pan(delta);

    Assert.That(Vector3.Distance(camera.Eye, eyeBefore + delta), Is.EqualTo(0f).Within(Epsilon));
    Assert.That(Vector3.Distance(camera.Eye, camera.Target), Is.EqualTo(400f).Within(Epsilon));
    Assert.That(camera.Azimuth, Is.EqualTo(60f).Within(Epsilon));
  }

  [Test]
  public void Elevation_DefaultsToTheOriginalFixedViewOffsetAngle() {
    // The pre-Tilt implementation used a fixed (20, -20, 50) offset; Elevation's default must reproduce
    // that exact angle (atan2(50, |(20,-20)|)) so existing framing/rotation behavior is unchanged.
    var camera = new Camera();

    var expectedDegrees = MathF.Atan2(50f, new Vector2(20f, -20f).Length()) * 180f / MathF.PI;
    Assert.That(camera.Elevation, Is.EqualTo(expectedDegrees).Within(Epsilon));
  }

  [Test]
  public void Tilt_PositiveDegreesIncreasesElevation() {
    var camera = new Camera();
    var before = camera.Elevation;

    camera.Tilt(10f);

    Assert.That(camera.Elevation, Is.EqualTo(before + 10f).Within(Epsilon));
  }

  [Test]
  public void Tilt_NegativeDegreesDecreasesElevation() {
    var camera = new Camera();
    var before = camera.Elevation;

    camera.Tilt(-10f);

    Assert.That(camera.Elevation, Is.EqualTo(before - 10f).Within(Epsilon));
  }

  [Test]
  public void Tilt_ClampsToAMinimumElevationNearTheHorizon() {
    var camera = new Camera();

    camera.Tilt(-1000f);

    Assert.That(camera.Elevation, Is.GreaterThan(0f));
  }

  [Test]
  public void Tilt_ClampsToAMaximumElevationNearStraightDown() {
    var camera = new Camera();

    camera.Tilt(1000f);

    Assert.That(camera.Elevation, Is.LessThan(90f));
  }

  [Test]
  public void Tilt_KeepsTheSameEyeToTargetDistance() {
    var camera = new Camera();
    camera.Frame(Vector3.Zero, distance: 300f);

    camera.Tilt(20f);

    Assert.That(Vector3.Distance(camera.Eye, camera.Target), Is.EqualTo(300f).Within(Epsilon));
  }

  [Test]
  public void Tilt_DoesNotChangeAzimuth() {
    var camera = new Camera();
    camera.RotateAzimuth(123f);

    camera.Tilt(20f);

    Assert.That(camera.Azimuth, Is.EqualTo(123f).Within(Epsilon));
  }

  [Test]
  public void RotateAzimuth_DoesNotChangeElevation() {
    var camera = new Camera();
    camera.Tilt(15f);
    var elevationBefore = camera.Elevation;

    camera.RotateAzimuth(90f);

    Assert.That(camera.Elevation, Is.EqualTo(elevationBefore).Within(Epsilon));
  }

  [Test]
  public void Tilt_TowardStraightDown_RaisesEyeAboveTarget() {
    var camera = new Camera();
    camera.Frame(Vector3.Zero, distance: 300f);

    camera.Tilt(1000f);

    // A steep elevation should put the eye almost directly above the target.
    Assert.That(camera.Eye.Z, Is.GreaterThan(camera.Target.Z));
    var horizontalDistance = new Vector2(camera.Eye.X - camera.Target.X, camera.Eye.Y - camera.Target.Y).Length();
    Assert.That(horizontalDistance, Is.LessThan(Vector3.Distance(camera.Eye, camera.Target) * 0.2f));
  }
}
