// CameraExtensionsTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Numerics;
using NUnit.Framework;
using OpenCobra.GDK;
using Silk.NET.Maths;

namespace OpenCobra.Tests.GDK;

[TestFixture]
public class CameraExtensionsTests {
  private const float Epsilon = 1e-4f;
  private static readonly Vector2D<int> FramebufferSize = new(800, 600);

  [Test]
  public void ToRay_ScreenCenter_ProducesARayThroughTarget() {
    var camera = new Camera();
    camera.Frame(new Vector3(50, -50, 0), distance: 20f);

    var ray = camera.ToRay(new Vector2(400, 300), FramebufferSize);

    // Target is the one point guaranteed to be dead-center on screen for an unpanned camera - the ray
    // through the center pixel must pass through it.
    var toTarget = camera.Target - ray.Origin;
    var distanceAlongRay = Vector3.Dot(toTarget, ray.Direction);
    var closestPoint = ray.Origin + (ray.Direction * distanceAlongRay);

    Assert.That(Vector3.Distance(closestPoint, camera.Target), Is.EqualTo(0f).Within(Epsilon));
  }

  [Test]
  public void ToRay_ScreenCenter_OriginatesExactlyAtEyeAndPointsExactlyTowardTheTarget() {
    // Unlike matrix-inversion-based unprojection, the analytic ray needs no near-plane offset and no
    // Matrix4x4.Invert - it's built directly from Eye/Target, so this holds to tight (not "loosened for
    // float noise") precision.
    var camera = new Camera();
    camera.Frame(Vector3.Zero, distance: 20f);

    var ray = camera.ToRay(new Vector2(400, 300), FramebufferSize);

    var expectedDirection = Vector3.Normalize(camera.Target - camera.Eye);
    Assert.That(Vector3.Distance(ray.Direction, expectedDirection), Is.EqualTo(0f).Within(Epsilon));
    Assert.That(ray.Origin, Is.EqualTo(camera.Eye));
  }

  [Test]
  public void ToRay_TopLeftCorner_PointsAwayFromScreenCenterRay() {
    // Catches an X/Y swap or a missed Y-flip (screen space is Y-down, NDC is Y-up) that testing only
    // the screen center couldn't catch, since the center maps to (0, 0) in both conventions.
    var camera = new Camera();
    camera.Frame(Vector3.Zero, distance: 20f);

    var centerRay = camera.ToRay(new Vector2(400, 300), FramebufferSize);
    var cornerRay = camera.ToRay(new Vector2(0, 0), FramebufferSize);

    Assert.That(Vector3.Distance(centerRay.Direction, cornerRay.Direction), Is.GreaterThan(Epsilon));
  }

  [Test]
  public void ToRay_DoesNotRequireUpdateToHaveBeenCalled() {
    // Camera.Value defaults to Matrix4x4.Identity (not null), but ToRay shouldn't depend on Update()
    // having run at all - Eye/Target are always current regardless.
    var camera = new Camera();
    camera.Frame(Vector3.Zero, distance: 20f);

    var ray = camera.ToRay(new Vector2(400, 300), FramebufferSize);

    Assert.That(ray.Origin, Is.EqualTo(camera.Eye));
  }

  [Test]
  public void ToRay_RemainsPreciseAtRealisticFullParkFramingDistances() {
    // Regression test for the precision bug this analytic approach replaced: the old matrix-inversion
    // Camera.Unproject needed its assertion epsilon loosened from 1e-3 to 5e-3 even at a modest distance
    // of 20, and was off by 0.6 units at distance 500 - both symptoms of Matrix4x4.Invert's ill
    // conditioning at large far/near ratios. ToRay never inverts a matrix, so it should stay tight-epsilon
    // precise even at ~1303, the default 128x128 park's actual framing distance (see the far-plane
    // comment in Camera.cs).
    var camera = new Camera();
    camera.Frame(Vector3.Zero, distance: 1303f);

    var ray = camera.ToRay(new Vector2(400, 300), FramebufferSize);

    var expectedDirection = Vector3.Normalize(camera.Target - camera.Eye);
    Assert.That(Vector3.Distance(ray.Direction, expectedDirection), Is.EqualTo(0f).Within(Epsilon));
  }
}
