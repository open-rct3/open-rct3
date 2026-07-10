// CameraFramingTests
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using OpenCobra.GDK;
using OpenRCT3.Simulation;

namespace OpenRCT3.Tests.Simulation;

[TestFixture]
public class CameraFramingTests {
  // Mirrors the framing Game.cs computes from the loaded Park after World.Load(): center the camera
  // on the buildable area, at a distance equal to its diagonal times a safety margin.
  //
  // The margin exists because Camera's default view direction sits at an exact 45° azimuth (equal X/Y
  // offset), so a square/rectangular map's corners land exactly on its diagonals and render as a
  // rotated "diamond." Perspective foreshortens the near corner (closest to the eye) more than the
  // sine-of-half-FOV bounding-sphere formula a plain diagonal distance assumes, so the near corner
  // clips out of frame well before `distance = diagonal` alone would suggest.
  private const float FramingDistanceMargin = 1.8f;

  private static Camera FrameOnPark(Park park) {
    var camera = new Camera();
    var bounds = park.BuildableBounds;
    var center = new Vector3((bounds.Min.X + bounds.Max.X) / 2f, (bounds.Min.Y + bounds.Max.Y) / 2f, 0f);
    var diagonal = Vector2.Distance(bounds.Min, bounds.Max);
    camera.Frame(center, diagonal * FramingDistanceMargin);
    camera.Update(aspectRatio: 16f / 9f);
    return camera;
  }

  private static Vector2 ProjectToNdc(Camera camera, Vector3 worldPos) {
    Assert.That(camera.Value, Is.Not.Null);
    var clip = Vector4.Transform(new Vector4(worldPos, 1f), camera.Value!.Value);
    return new Vector2(clip.X / clip.W, clip.Y / clip.W);
  }

  private static float ProjectToNdcZ(Camera camera, Vector3 worldPos) {
    Assert.That(camera.Value, Is.Not.Null);
    var clip = Vector4.Transform(new Vector4(worldPos, 1f), camera.Value!.Value);
    return clip.Z / clip.W;
  }

  // The corners of the *rendered mesh*, not just the buildable area: TerrainMeshBuilder renders the
  // full OOB-inclusive grid (see Terrain.cs / CornerPosition), which extends beyond BuildableBounds on
  // every side. Framing needs to keep this larger extent on-screen, not just the buildable area.
  private static Vector3[] FullMeshCorners(Terrain terrain) {
    var halfWidth = terrain.Width / 2f * Park.TileSize;
    var height = terrain.Height * Park.TileSize;
    return [
      new Vector3(-halfWidth, 0, 0),
      new Vector3(halfWidth, 0, 0),
      new Vector3(-halfWidth, height, 0),
      new Vector3(halfWidth, height, 0),
    ];
  }

  [Test]
  public void DefaultCamera_DoesNotFrameTheDefaultPark() {
    // Regression guard for the original bug: Camera's un-framed default (a small fixed offset from the
    // origin, sized for a toy scene) leaves the actual default park's buildable-area corners entirely
    // outside the view frustum.
    var park = new Park();
    var camera = new Camera();
    camera.Update(aspectRatio: 16f / 9f);

    var (min, max) = park.BuildableBounds;
    var corners = new[] {
      new Vector3(min.X, min.Y, 0),
      new Vector3(max.X, min.Y, 0),
      new Vector3(min.X, max.Y, 0),
      new Vector3(max.X, max.Y, 0),
    };

    var allOnScreen = corners.All(c => {
      var ndc = ProjectToNdc(camera, c);
      return ndc.X is >= -1f and <= 1f && ndc.Y is >= -1f and <= 1f;
    });
    Assert.That(allOnScreen, Is.False);
  }

  [Test]
  public void FramedCamera_KeepsDefaultParkRenderedMeshCornersOnScreen() {
    var park = new Park();
    var terrain = new Terrain();
    var camera = FrameOnPark(park);

    foreach (var corner in FullMeshCorners(terrain)) {
      var ndc = ProjectToNdc(camera, corner);
      Assert.That(ndc.X, Is.InRange(-1f, 1f), $"corner {corner} X out of view");
      Assert.That(ndc.Y, Is.InRange(-1f, 1f), $"corner {corner} Y out of view");
      // Regression guard: a fixed far clip plane (previously hardcoded at 1000) doesn't scale with the
      // framing distance Game.cs computes from the park's actual size, so the default 128x128 map's
      // framing distance (~1303) exceeded it and every corner was silently frustum-culled - invisible
      // despite X/Y projecting to plausible on-screen coordinates. Camera.Update now derives the far
      // plane from the eye-to-target distance itself (see Camera.cs), so this must hold for any park size.
      Assert.That(ProjectToNdcZ(camera, corner), Is.InRange(-1f, 1f), $"corner {corner} Z out of view (behind far plane)");
    }
  }

  [Test]
  public void FramedCamera_KeepsSmallerCustomParkRenderedMeshCornersOnScreen() {
    // Same check against a much smaller map, to confirm the framing scales rather than being tuned to
    // one specific map size.
    var park = new Park(buildableWidth: 16, buildableHeight: 16);
    var terrain = new Terrain(width: 16, height: 16);
    var camera = FrameOnPark(park);

    foreach (var corner in FullMeshCorners(terrain)) {
      var ndc = ProjectToNdc(camera, corner);
      Assert.That(ndc.X, Is.InRange(-1f, 1f), $"corner {corner} X out of view");
      Assert.That(ndc.Y, Is.InRange(-1f, 1f), $"corner {corner} Y out of view");
      Assert.That(ProjectToNdcZ(camera, corner), Is.InRange(-1f, 1f), $"corner {corner} Z out of view (behind far plane)");
    }
  }

  [Test]
  public void FramedCamera_KeepsLargerCustomParkRenderedMeshCornersOnScreen() {
    // A map larger than the default proves the far plane truly scales with framing distance rather than
    // happening to clear a fixed constant that was merely large enough for the 128x128 default.
    var park = new Park(buildableWidth: 512, buildableHeight: 512);
    var terrain = new Terrain(width: 512, height: 512);
    var camera = FrameOnPark(park);

    foreach (var corner in FullMeshCorners(terrain)) {
      var ndc = ProjectToNdc(camera, corner);
      Assert.That(ndc.X, Is.InRange(-1f, 1f), $"corner {corner} X out of view");
      Assert.That(ndc.Y, Is.InRange(-1f, 1f), $"corner {corner} Y out of view");
      Assert.That(ProjectToNdcZ(camera, corner), Is.InRange(-1f, 1f), $"corner {corner} Z out of view (behind far plane)");
    }
  }
}
