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
}
