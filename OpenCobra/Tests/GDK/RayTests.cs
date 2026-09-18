// RayTests
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
public class RayTests {
  private const float Epsilon = 1e-4f;

  private static readonly Vector3 V0 = new(-1, 0, 0);
  private static readonly Vector3 V1 = new(1, 0, 0);
  private static readonly Vector3 V2 = new(0, 1, 0);

  [Test]
  public void Intersects_StraightHitThroughTriangleCenter_ReturnsTrue() {
    var ray = new Ray(new Vector3(0, 0.3f, -5), Vector3.UnitZ);

    var hit = ray.Intersects(V0, V1, V2, out var point);

    Assert.That(hit, Is.True);
    Assert.That(Vector3.Distance(point, new Vector3(0, 0.3f, 0)), Is.EqualTo(0f).Within(Epsilon));
  }

  [Test]
  public void Intersects_MissesOutsideTriangleBounds_ReturnsFalse() {
    var ray = new Ray(new Vector3(0, 5f, -5), Vector3.UnitZ);

    var hit = ray.Intersects(V0, V1, V2, out _);

    Assert.That(hit, Is.False);
  }

  [Test]
  public void Intersects_ParallelToTrianglePlane_ReturnsFalse() {
    // The triangle lies in the Z=0 plane; a ray traveling within that plane never converges on it.
    var ray = new Ray(new Vector3(0, -5, 0), Vector3.UnitY);

    var hit = ray.Intersects(V0, V1, V2, out _);

    Assert.That(hit, Is.False);
  }

  [Test]
  public void Intersects_HitBehindOrigin_ReturnsFalse() {
    // The triangle is at Z=0; a ray starting past it and pointing further away should not report a hit.
    var ray = new Ray(new Vector3(0, 0.3f, 5), Vector3.UnitZ);

    var hit = ray.Intersects(V0, V1, V2, out _);

    Assert.That(hit, Is.False);
  }
}
