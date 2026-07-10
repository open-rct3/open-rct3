// Camera
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using OpenCobra.GDK.Shaders;
using System.Numerics;

namespace OpenCobra.GDK;

public class Camera : Uniform<Matrix4x4> {
  public static readonly string UniformName = "u_ViewProj";
  public new readonly string Name = UniformName;

  /// <summary>
  /// The world-space offset (South-East, elevated) the camera's default framing looks from.
  /// </summary>
  /// <remarks>
  /// The Z component is deliberately large relative to the X/Y horizontal offset — roughly a 60°
  /// elevation above the horizon, not a shallow grazing angle. A large, flat, unlit, single-color
  /// terrain plane viewed edge-on from a shallow angle (the original (20, -20, 15) offset, ~28°
  /// elevation) has no visual cue distinguishing "looking down at the ground from above" from "looking
  /// up at a ceiling from below" — there's no horizon, no shading gradient, nothing but a silhouette.
  /// A steep, mostly-downward angle removes that ambiguity.
  /// </remarks>
  private static readonly Vector3 DefaultViewOffset = new(20, -20, 50);
  /// <summary>The unit direction of <see cref="DefaultViewOffset"/>, used by <see cref="Frame"/>.</summary>
  private static readonly Vector3 DefaultViewDirection = Vector3.Normalize(DefaultViewOffset);
  private static readonly float DefaultDistance = DefaultViewOffset.Length();

  /// <summary>Near clip distance, in world-space meters. 1cm is close enough for any placeable object.</summary>
  private const float NearPlaneDistance = 0.01f;
  /// <summary>
  /// Far clip distance, expressed as a multiple of the current eye-to-target distance rather than a
  /// fixed constant. <see cref="Frame"/> callers (see <c>Game.cs</c>) already pick a distance that keeps
  /// an entire park's mesh on-screen, scaled to that park's actual size — so deriving the far plane from
  /// that same distance automatically scales with it too, instead of clipping larger maps that a fixed
  /// constant wasn't sized for (see .agents/bugs/terrain-render-black-and-misoriented.md). The 2x margin
  /// leaves room for a future cube-mapped skybox drawn just outside a park's total (OOB-inclusive)
  /// bounds, without needing to be revisited once one exists.
  /// </summary>
  private const float FarPlaneDistanceMargin = 2f;

  /// <summary>The world-space point the camera is aimed at.</summary>
  public Vector3 Target { get; private set; } = Vector3.Zero;
  /// <summary>The camera's world-space eye position.</summary>
  public Vector3 Eye { get; private set; }

  public Camera() {
    Value = Matrix4x4.Identity;
    Eye = Target + (DefaultViewDirection * DefaultDistance);
  }

  /// <summary>
  /// Re-aims the camera at <paramref name="target"/>, keeping the same South-East/elevated viewing
  /// direction, with the eye placed <paramref name="distance"/> units away along that direction.
  /// </summary>
  public void Frame(Vector3 target, float distance) {
    Target = target;
    Eye = target + (DefaultViewDirection * distance);
  }

  /// <summary>
  /// Updates the camera view and projection matrices.
  /// </summary>
  /// <param name="aspectRatio">The aspect ratio of the viewport.</param>
  public void Update(float aspectRatio) {
    var view = Matrix4x4.CreateLookAt(Eye, Target, Vector3.UnitZ);
    var farPlaneDistance = Vector3.Distance(Eye, Target) * FarPlaneDistanceMargin;
    var projection = CreatePerspectiveFieldOfViewGL(MathF.PI / 3f, aspectRatio, NearPlaneDistance, farPlaneDistance);

    Value = view * projection;
  }

  /// <summary>
  /// Creates a right-handed perspective projection matrix using OpenGL's clip-space Z convention:
  /// <paramref name="nearPlaneDistance"/> maps to NDC z = -1 and <paramref name="farPlaneDistance"/>
  /// maps to NDC z = +1.
  /// </summary>
  /// <remarks>
  /// <see cref="Matrix4x4.CreatePerspectiveFieldOfView"/> targets Direct3D's [0, 1] NDC-z convention
  /// instead. This engine renders exclusively via OpenGL (with no <c>glClipControl</c> override — the
  /// GL 4.1 Core profile this project targets predates that extension's core availability), so using
  /// the D3D-convention matrix directly compresses the entire visible depth range into the back half
  /// of the depth buffer, discarding precision where it matters most: close to the camera.
  /// </remarks>
  public static Matrix4x4 CreatePerspectiveFieldOfViewGL(
    float fieldOfView,
    float aspectRatio,
    float nearPlaneDistance,
    float farPlaneDistance) {
    var yScale = 1.0f / MathF.Tan(fieldOfView * 0.5f);
    var xScale = yScale / aspectRatio;
    var range = farPlaneDistance - nearPlaneDistance;

    return new Matrix4x4(
      xScale, 0, 0, 0,
      0, yScale, 0, 0,
      0, 0, -(farPlaneDistance + nearPlaneDistance) / range, -1,
      0, 0, -2 * farPlaneDistance * nearPlaneDistance / range, 0
    );
  }
}
