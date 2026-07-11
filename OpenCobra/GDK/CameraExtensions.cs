// Camera Extensions
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using Silk.NET.Maths;
using System.Numerics;

namespace OpenCobra.GDK;

/// <summary>
/// Screen-to-world picking for <see cref="Camera"/>, kept as an extension (rather than a <see cref="Camera"/>
/// member) to keep <see cref="Camera"/> itself focused on view/projection state - picking is a downstream
/// concern of a handful of that state (<see cref="Camera.Eye"/>, <see cref="Camera.Target"/>,
/// <see cref="Camera.FieldOfView"/>), not something the camera needs to know about itself.
/// </summary>
public static class CameraExtensions {
  /// <summary>
  /// Builds a world-space <see cref="Ray"/> for <paramref name="screenPos"/> analytically from
  /// <paramref name="camera"/>'s eye, target, and field of view - not by inverting <see cref="Camera.Value"/>.
  /// </summary>
  /// <remarks>
  /// The view-projection matrix's inverse is ill-conditioned in single-precision float at realistic
  /// gameplay camera distances, since <see cref="Camera"/>'s far clip plane can sit hundreds of thousands
  /// of times farther than its fixed 1cm near plane. Reconstructing the ray from the camera's own basis
  /// vectors sidesteps that matrix inversion (and the near/far planes) entirely, so the ratio between them
  /// never enters the computation.
  /// </remarks>
  /// <param name="camera">The camera to cast from. Does not require <see cref="Camera.Update"/> to have
  /// been called first - unlike the view-projection matrix, <see cref="Camera.Eye"/>/<see cref="Camera.Target"/>
  /// are always current.</param>
  /// <param name="screenPos">
  /// A point in framebuffer pixel space (Y-down, origin top-left) — not DPI-scaled logical/window
  /// coordinates. Must match the same pixel space as <paramref name="framebufferSize"/>.
  /// </param>
  /// <param name="framebufferSize">
  /// The framebuffer size, in pixels, e.g. <c>IView.FramebufferSize</c> - also supplies the aspect ratio,
  /// so it stays a single source of truth rather than a separately-passed value that could drift out of
  /// sync with it.
  /// </param>
  public static Ray ToRay(this Camera camera, Vector2 screenPos, Vector2D<int> framebufferSize) {
    var aspectRatio = (float)framebufferSize.X / framebufferSize.Y;

    // Screen pixels (Y-down) -> NDC (Y-up, [-1, 1]).
    var ndcX = (2f * screenPos.X / framebufferSize.X) - 1f;
    var ndcY = 1f - (2f * screenPos.Y / framebufferSize.Y);

    // The camera's true (tilt-aware) view basis - deliberately not Camera.Forward/Camera.Right, which
    // are ground-plane-flattened for WASD panning, not the actual (possibly tilted) viewing direction.
    // Derived the same way Matrix4x4.CreateLookAt derives its axes, so this basis matches Update()'s
    // view matrix exactly: right = cross(forward, worldUp), up = cross(right, forward).
    var forward = Vector3.Normalize(camera.Target - camera.Eye);
    var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ));
    var up = Vector3.Cross(right, forward);

    var tanHalfFov = MathF.Tan(Camera.FieldOfView * 0.5f);
    var direction = Vector3.Normalize(
      forward
      + (right * (ndcX * tanHalfFov * aspectRatio))
      + (up * (ndcY * tanHalfFov)));

    return new Ray(camera.Eye, direction);
  }
}
