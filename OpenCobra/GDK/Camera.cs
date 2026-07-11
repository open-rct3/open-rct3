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
  private static readonly float DefaultDistance = DefaultViewOffset.Length();
  /// <summary>
  /// The compass bearing (degrees, around Z) of <see cref="DefaultViewOffset"/>'s horizontal component -
  /// the azimuth-zero reference direction <see cref="UpdateEye"/> rotates by <see cref="Azimuth"/>.
  /// </summary>
  private static readonly float DefaultBearingDegrees =
    MathF.Atan2(DefaultViewOffset.Y, DefaultViewOffset.X) * 180f / MathF.PI;
  /// <summary>
  /// The angle (degrees, above the horizon) of <see cref="DefaultViewOffset"/> - the default value
  /// <see cref="Elevation"/> starts at, computed from <see cref="DefaultViewOffset"/> so the camera's
  /// initial direction matches it exactly (see the class remarks above for why that offset was chosen).
  /// </summary>
  private static readonly float DefaultElevationDegrees = MathF.Atan2(
    DefaultViewOffset.Z,
    new Vector2(DefaultViewOffset.X, DefaultViewOffset.Y).Length()
  ) * 180f / MathF.PI;

  /// <summary>Near clip distance, in world-space meters. 1cm is close enough for any placeable object.</summary>
  private const float NearPlaneDistance = 0.01f;
  /// <summary>
  /// Far clip distance, expressed as a multiple of <see cref="FarPlaneReferenceDistance"/> rather than a
  /// fixed constant. <see cref="Frame"/> callers (see <c>Game.cs</c>) already pick a distance that keeps
  /// an entire park's mesh on-screen, scaled to that park's actual size — so deriving the far plane from
  /// that same distance automatically scales with it too, instead of clipping larger maps that a fixed
  /// constant wasn't sized for (see .agents/bugs/terrain-render-black-and-misoriented.md). The 2x margin
  /// leaves room for a future cube-mapped skybox drawn just outside a park's total (OOB-inclusive)
  /// bounds, without needing to be revisited once one exists.
  /// </summary>
  private const float FarPlaneDistanceMargin = 2f;
  /// <summary>The closest <see cref="Zoom"/> may bring the eye to <see cref="Target"/>.</summary>
  private const float MinDistance = 1f;
  /// <summary>
  /// The shallowest <see cref="Tilt"/> may bring <see cref="Elevation"/> toward the horizon. Elevation
  /// zero would put the eye level with the ground - a nearly-flat, edge-on view has the same "which way is
  /// up" ambiguity <see cref="DefaultViewOffset"/>'s remarks describe, so tilting stops short of it.
  /// </summary>
  private const float MinElevationDegrees = 15f;
  /// <summary>
  /// The steepest <see cref="Tilt"/> may bring <see cref="Elevation"/> toward straight down. Elevation 90°
  /// (looking straight down the Z axis) makes <see cref="Azimuth"/> meaningless - every azimuth looks
  /// identical - so tilting stops short of it.
  /// </summary>
  private const float MaxElevationDegrees = 85f;

  /// <summary>The world-space point the camera is aimed at.</summary>
  public Vector3 Target { get; private set; } = Vector3.Zero;
  /// <summary>The camera's world-space eye position.</summary>
  public Vector3 Eye { get; private set; }
  /// <summary>The current eye-to-target distance, as last set by <see cref="Frame"/>.</summary>
  private float distance = DefaultDistance;
  /// <summary>
  /// The farthest <see cref="Zoom"/> may push the eye from <see cref="Target"/>. Unset (<c>null</c>) by
  /// default; callers that know a natural "fully zoomed out" distance for the current scene (e.g. the
  /// distance that frames an entire park) should set this so <see cref="Zoom"/> can't push past it.
  /// </summary>
  public float? MaxDistance { get; set; }
  /// <summary>
  /// Rotation, in degrees, applied around <see cref="Target"/>'s Z axis on top of
  /// <see cref="DefaultBearingDegrees"/>. See <see cref="RotateAzimuth"/>.
  /// </summary>
  public float Azimuth { get; private set; }
  /// <summary>
  /// The camera's angle, in degrees above the horizon, looking from <see cref="Eye"/> toward
  /// <see cref="Target"/>. Starts at <see cref="DefaultElevationDegrees"/> (matching
  /// <see cref="DefaultViewOffset"/>'s original fixed angle) and is adjusted via <see cref="Tilt"/>.
  /// </summary>
  public float Elevation { get; private set; } = DefaultElevationDegrees;

  public Camera() {
    Value = Matrix4x4.Identity;
    UpdateEye();
  }

  /// <summary>
  /// Re-aims the camera at <paramref name="target"/>, keeping the same South-East/elevated viewing
  /// direction (rotated by any accumulated <see cref="Azimuth"/>), with the eye placed
  /// <paramref name="distance"/> units away along that direction.
  /// </summary>
  public void Frame(Vector3 target, float distance) {
    Target = target;
    this.distance = distance;
    UpdateEye();
  }

  /// <summary>
  /// Rotates the camera around <see cref="Target"/>'s Z axis by <paramref name="degrees"/>, keeping the
  /// same eye-to-target distance and elevation. Positive values rotate counter-clockwise looking down -Z.
  /// </summary>
  public void RotateAzimuth(float degrees) {
    Azimuth = (Azimuth + degrees) % 360f;
    if (Azimuth < 0f) Azimuth += 360f;
    UpdateEye();
  }

  /// <summary>
  /// Moves the eye <paramref name="delta"/> units closer to (negative) or farther from (positive)
  /// <see cref="Target"/>, keeping the same azimuth and elevation. Clamped to <see cref="MinDistance"/>
  /// (so the eye can never reach or pass through <see cref="Target"/>) and, if set, <see cref="MaxDistance"/>.
  /// </summary>
  public void Zoom(float delta) {
    var candidate = MathF.Max(distance + delta, MinDistance);
    distance = MaxDistance is { } max ? MathF.Min(candidate, max) : candidate;
    UpdateEye();
  }

  /// <summary>
  /// Adjusts <see cref="Elevation"/> by <paramref name="degrees"/> (positive tilts the eye up toward
  /// straight-down/bird's-eye, negative tilts it down toward the horizon), keeping the same azimuth and
  /// eye-to-target distance. Clamped to <see cref="MinElevationDegrees"/>/<see cref="MaxElevationDegrees"/>.
  /// </summary>
  public void Tilt(float degrees) {
    Elevation = Math.Clamp(Elevation + degrees, MinElevationDegrees, MaxElevationDegrees);
    UpdateEye();
  }

  /// <summary>
  /// The camera's ground-plane (Z=0) forward direction: the horizontal component of the eye-to-target
  /// viewing direction, normalized. Rotates with <see cref="Azimuth"/>. See <see cref="Pan"/>.
  /// </summary>
  public Vector3 Forward {
    get {
      var back = Vector3.Normalize(Eye - Target);
      return Vector3.Normalize(new Vector3(-back.X, -back.Y, 0));
    }
  }

  /// <summary>
  /// The camera's ground-plane (Z=0) right direction, perpendicular to <see cref="Forward"/>. Derived the
  /// same way <see cref="Matrix4x4.CreateLookAt"/> derives its x-axis (<c>cross(up, eye-target)</c>), so it
  /// matches what "right" looks like on screen. See <see cref="Pan"/>.
  /// </summary>
  public Vector3 Right {
    get {
      var back = Vector3.Normalize(Eye - Target);
      return Vector3.Normalize(new Vector3(-back.Y, back.X, 0));
    }
  }

  /// <summary>
  /// Translates <see cref="Target"/> (and, following it, <see cref="Eye"/>) by <paramref name="delta"/>,
  /// panning the camera across the ground plane without changing <see cref="Azimuth"/> or the
  /// eye-to-target distance. <paramref name="delta"/> is typically a multiple of <see cref="Forward"/>/
  /// <see cref="Right"/>, scaled by speed and frame delta time.
  /// </summary>
  public void Pan(Vector3 delta) {
    Target += delta;
    UpdateEye();
  }

  /// <summary>
  /// Recomputes <see cref="Eye"/> from <see cref="Target"/>, <see cref="distance"/>, <see cref="Azimuth"/>,
  /// and <see cref="Elevation"/> - a spherical-coordinates reconstruction of the eye-to-target direction,
  /// generalizing the fixed <see cref="DefaultViewOffset"/> direction to a rotatable/tiltable one.
  /// </summary>
  private void UpdateEye() {
    var bearingRad = (DefaultBearingDegrees + Azimuth) * MathF.PI / 180f;
    var elevationRad = Elevation * MathF.PI / 180f;
    var horizontalMagnitude = MathF.Cos(elevationRad);
    var direction = new Vector3(
      MathF.Cos(bearingRad) * horizontalMagnitude,
      MathF.Sin(bearingRad) * horizontalMagnitude,
      MathF.Sin(elevationRad));
    Eye = Target + (direction * distance);
  }

  /// <summary>
  /// The distance <see cref="Update"/> sizes the far clip plane from: <see cref="MaxDistance"/> if set,
  /// otherwise the live <see cref="distance"/>.
  /// </summary>
  /// <remarks>
  /// Deliberately not always the live eye-to-target distance. <see cref="Zoom"/> mutates
  /// <see cref="distance"/> continuously as the camera moves closer/farther, but the scene's actual
  /// extent (how far the terrain/objects reach) doesn't shrink just because the camera zoomed in - sizing
  /// the far plane off the live distance pulled it in along with every zoom-in, clipping terrain that
  /// should still be visible (worse at low <see cref="Elevation"/>, where the horizon is much farther
  /// than <see cref="Target"/>). <see cref="MaxDistance"/> is already the stable "whole scene" reference
  /// - <c>Game.cs</c> sets it once, to the same distance <see cref="Frame"/> used to fit the whole park -
  /// so anchoring to it keeps the far plane fixed regardless of zoom/tilt. Falling back to the live
  /// <see cref="distance"/> when <see cref="MaxDistance"/> is unset preserves the original scale-with-Frame
  /// behavior for callers that never zoom (e.g. a fixed camera with no <see cref="MaxDistance"/> set).
  /// </remarks>
  private float FarPlaneReferenceDistance => MaxDistance ?? distance;

  /// <summary>
  /// Updates the camera view and projection matrices.
  /// </summary>
  /// <param name="aspectRatio">The aspect ratio of the viewport.</param>
  public void Update(float aspectRatio) {
    var view = Matrix4x4.CreateLookAt(Eye, Target, Vector3.UnitZ);
    var farPlaneDistance = FarPlaneReferenceDistance * FarPlaneDistanceMargin;
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
