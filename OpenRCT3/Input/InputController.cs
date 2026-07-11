// Game Input Controller
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using OpenCobra.GDK;
using OpenCobra.GDK.GUI;
using OpenCobra.GDK.Input;
using OpenRCT3.Platforms;
using OpenRCT3.Simulation;
using Silk.NET.Input;
using System.Numerics;

namespace OpenRCT3.Input;

/// <summary>
/// Builds the game's <see cref="InputActionMap"/> (defaults from <see cref="DefaultBindings"/>, layered
/// with any user rebinds from <see cref="AppConfig.KeyBindings"/>) and wires it to camera behavior.
/// </summary>
public sealed class InputController {
  /// <summary>Degrees <see cref="DefaultBindings.RotateMapLeft"/>/<see cref="DefaultBindings.RotateMapRight"/> snap-rotate per press.</summary>
  private const float RotationStepDegrees = 90f;
  /// <summary>Map tiles of zoom per press of the keyboard zoom bindings (PageUp/PageDown), which have no notion of "how hard".</summary>
  private const float ZoomStepTiles = 3f;
  /// <summary>Fixed per-press zoom step, in world units - <see cref="ZoomStepTiles"/> scaled by <see cref="Park.TileSize"/>.</summary>
  private const float ZoomStepDistance = ZoomStepTiles * Park.TileSize;
  /// <summary>
  /// Map tiles of zoom per unit of raw scroll-wheel tick magnitude, before <see cref="ZoomSensitivity"/>.
  /// Chosen to match <see cref="ZoomStepTiles"/> so one "unit" of scroll feels like one PageUp/PageDown
  /// press - what a "unit" of scroll actually corresponds to physically varies by platform/device (see
  /// <see cref="ZoomSensitivity"/>), which is a separate concern from this base scale.
  /// </summary>
  private const float ScrollZoomScaleTiles = 3f;
  /// <summary>Base scroll-wheel zoom scale, in world units - <see cref="ScrollZoomScaleTiles"/> scaled by <see cref="Park.TileSize"/>.</summary>
  private const float ScrollZoomScale = ScrollZoomScaleTiles * Park.TileSize;
  /// <summary>Ground-plane pan speed, in map tiles per second, for the held WASD/arrow-key movement actions.</summary>
  private const float PanSpeedTiles = 6f;
  /// <summary>Ground-plane pan speed, in world units per second - <see cref="PanSpeedTiles"/> scaled by <see cref="Park.TileSize"/>.</summary>
  private const float PanSpeed = PanSpeedTiles * Park.TileSize;
  /// <summary>Degrees of <see cref="Camera.RotateAzimuth"/> per pixel of mouse drag, for <see cref="DefaultBindings.RotateTiltCameraNormal"/>.</summary>
  private const float RotateSensitivityDegrees = 0.25f;
  /// <summary>Degrees of <see cref="Camera.Tilt"/> per pixel of mouse drag, for <see cref="DefaultBindings.RotateTiltCameraNormal"/>.</summary>
  private const float TiltSensitivityDegrees = 0.15f;
  /// <summary>Map tiles of <see cref="Camera.Pan"/> per pixel of mouse drag, for <see cref="DefaultBindings.StrafeCameraNormal"/>.</summary>
  private const float StrafeSensitivityTiles = 0.05f;
  /// <summary>Map tiles per pixel, in world units - <see cref="StrafeSensitivityTiles"/> scaled by <see cref="Park.TileSize"/>.</summary>
  private const float StrafeSensitivity = StrafeSensitivityTiles * Park.TileSize;

  private readonly Camera camera;
  private readonly IMouse mouse;
  /// <summary>The mouse position as of the last <see cref="Update"/> call, or <c>null</c> before the first one.</summary>
  private Vector2? lastMousePosition;

  /// <summary>Resolves the game's named, rebindable input actions against the window's live <see cref="IInputContext"/>.</summary>
  public InputActionMap Actions { get; }

  /// <summary>
  /// Multiplies both <see cref="ZoomStepDistance"/> (keyboard) and <see cref="ScrollZoomScale"/> (scroll
  /// wheel) zoom amounts. Defaulted per-platform here, but a plain settable property so callers can
  /// override it (e.g. a future sensitivity setting).
  /// </summary>
  /// <remarks>
  /// What one scroll-wheel "unit" of <see cref="OpenCobra.GDK.Input.InputActionMap.Scrolled"/> magnitude
  /// physically corresponds to isn't the same across platforms: Windows' <c>InputAdapter</c> normalizes
  /// against <c>WHEEL_DELTA</c> (120, one classic notch == 1.0 - see the FIXME left in that file about
  /// Raw Input's <c>RI_MOUSE_WHEEL</c> for even finer-grained deltas), while macOS's <c>InputAdapter</c>
  /// passes <c>NSEvent.deltaY</c> straight through unnormalized, which reports much smaller values per
  /// scroll/trackpad event than a Windows notch does. Without a per-platform multiplier, the same base
  /// <see cref="ScrollZoomScale"/> would feel wildly different (or, on Windows, simply too strong) across
  /// platforms - these are first-pass estimates, not measured against real macOS hardware.
  /// </remarks>
  public float ZoomSensitivity { get; set; } =
#if WINDOWS
    0.5f;
#elif OSX
    0.1f;
#else
    1f;
#endif

  public InputController(IInputContext context, AppConfig config, Camera camera, Func<bool> quit) {
    this.camera = camera;
    mouse = context.Mice[0];

    // Seed the action map with the game's defaults, then layer in any user rebinds from config.
    Actions = new InputActionMap(context, DefaultBindings.Defaults);
    if (config.KeyBindings is { } overrides) {
      foreach (var (action, binding) in overrides) Actions.Bind(action, binding.ToBinding());
    }

    Actions.Pressed += action => {
      // Gate on WantTextInput, not CaptureKeyboard: CaptureKeyboard stays true for any focused ImGui
      // window (nav/shortcuts), even one with no text field, which would swallow every keyboard shortcut
      // just because e.g. the Scenario Editor window has focus. WantTextInput is only true while a text
      // widget is actually editable - the real condition these keyboard actions conflict with.
      if (Controller.WantTextInput) return;
      else if (action == DefaultBindings.ExitGame) quit();
      else if (action == DefaultBindings.RotateMapLeft) camera.RotateAzimuth(-RotationStepDegrees);
      else if (action == DefaultBindings.RotateMapRight) camera.RotateAzimuth(RotationStepDegrees);
      else if (action == DefaultBindings.ZoomIn) camera.Zoom(-ZoomStepDistance * ZoomSensitivity);
      else if (action == DefaultBindings.ZoomOut) camera.Zoom(ZoomStepDistance * ZoomSensitivity);
    };
    Actions.Scrolled += (action, delta) => {
      if (Controller.CaptureMouse) return;
      // Positive delta (scroll up) zooms in (negative distance change); negative delta zooms out - the
      // sign is already correct for both ZoomIn and ZoomOut bindings, so no branch on action is needed.
      if (action == DefaultBindings.ZoomIn || action == DefaultBindings.ZoomOut) camera.Zoom(-delta * ScrollZoomScale * ZoomSensitivity);
    };
  }

  /// <summary>
  /// Polls the held WASD/arrow-key freelook movement actions and the Normal-mode mouse-drag camera
  /// gestures, applying both to the camera. Unlike the <see cref="InputActionMap.Pressed"/>/
  /// <see cref="InputActionMap.Scrolled"/>-driven handlers above, continuous input while a key/button is
  /// held has no single discrete event to hook, so this must be called once per rendered frame (see
  /// <c>Game.Run</c>) with that frame's elapsed time.
  /// </summary>
  public void Update(float deltaSeconds) {
    if (Controller.WantTextInput) return;

    var pan = Vector3.Zero;
    if (Actions.IsActive(DefaultBindings.MoveForwardsFreelookCamera)) pan += camera.Forward;
    if (Actions.IsActive(DefaultBindings.MoveBackwardsFreelookCamera)) pan -= camera.Forward;
    if (Actions.IsActive(DefaultBindings.MoveRightFreelookCamera)) pan += camera.Right;
    if (Actions.IsActive(DefaultBindings.MoveLeftFreelookCamera)) pan -= camera.Right;
    if (pan != Vector3.Zero) camera.Pan(Vector3.Normalize(pan) * PanSpeed * deltaSeconds);

    UpdateMouseDrag();
  }

  /// <summary>
  /// Normal camera mode's mouse-drag gestures (see the manual's "Camera Movement – Normal" table):
  /// RMB-hold-and-drag pans/strafes, wheel-hold-or-RMB+LMB-hold-and-drag rotates horizontally and tilts
  /// vertically in one combined gesture. Freelook/Isometric are bound (see <see cref="DefaultBindings"/>)
  /// but not consumed here yet.
  /// </summary>
  private void UpdateMouseDrag() {
    var position = mouse.Position;
    var delta = lastMousePosition is { } last ? position - last : Vector2.Zero;
    lastMousePosition = position;

    if (Controller.CaptureMouse || delta == Vector2.Zero) return;

    if (Actions.IsActive(DefaultBindings.RotateTiltCameraNormal)) {
      camera.RotateAzimuth(delta.X * RotateSensitivityDegrees);
      camera.Tilt(-delta.Y * TiltSensitivityDegrees);
    } else if (Actions.IsActive(DefaultBindings.StrafeCameraNormal)) {
      // Drag-to-pan: the camera moves opposite the drag direction, so the point under the cursor
      // (approximately) follows the mouse, matching how map-drag panning feels in other tools. The Y
      // (forward/backward) term is +delta.Y, not -delta.Y: mouse Y grows downward on screen, and dragging
      // down needs to move the camera toward Forward (revealing what's "further away") for content to
      // track the cursor - the X (left/right) term already had the correct sign.
      var strafe = (-camera.Right * delta.X) + (camera.Forward * delta.Y);
      camera.Pan(strafe * StrafeSensitivity);
    }
  }
}
