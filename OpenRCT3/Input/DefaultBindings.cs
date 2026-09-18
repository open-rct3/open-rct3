// Default Input Bindings
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Collections.Generic;
using OpenCobra.GDK.Input;
using Silk.NET.Input;

namespace OpenRCT3.Input;

/// <summary>
/// The game's default action names and their out-of-the-box key/mouse bindings, matching RCT3's
/// Game Options ▸ Keyboard Bindings screen. Consumed to seed an <see cref="InputActionMap"/> at startup;
/// user overrides are layered on top via <see cref="Platforms.AppConfig"/>.
/// </summary>
public static class DefaultBindings {
  // Universal
  public const string ExitGame = "exit-game";
  public const string TakeScreenshot = "take-a-screenshot";
  public const string TakeAMovie = "take-a-movie";

  // During play
  public const string CycleCoastercamTypes = "cycle-coastercam-types";
  public const string JumpToNextCamera = "jump-to-next-camera";
  public const string JumpToPrevCamera = "jump-to-prev-camera";
  public const string JumpToNextRide = "jump-to-next-ride";
  public const string JumpToPrevRide = "jump-to-prev-ride";
  public const string ToggleCoasterCam = "toggle-coastercam";
  public const string ResetCamera = "reset-camera";
  public const string MoveLeftFreelookCamera = "move-left-freelook-camera";
  public const string MoveRightFreelookCamera = "move-right-freelook-camera";
  public const string MoveForwardsFreelookCamera = "move-forwards-freelook-camera";
  public const string MoveBackwardsFreelookCamera = "move-backwards-freelook-camera";
  public const string ZoomIn = "zoom-in";
  public const string ZoomOut = "zoom-out";
  public const string RotateMapLeft = "rotate-map-left";
  public const string RotateMapRight = "rotate-map-right";
  public const string RotateMapUp = "rotate-map-up";
  public const string RotateMapDown = "rotate-map-down";
  public const string ToggleLightUnderMousePointer = "toggle-light-under-mouse-pointer";
  public const string RotateIsometricCamera = "rotate-isometric-camera";
  public const string ReverseIsometricCameraRotation = "reverse-isometric-camera-rotation";
  public const string FlattenTerrainSmoothed = "flatten-terrain-smoothed";
  public const string FlattenTerrainPlacingScenery = "flatten-terrain-placing-scenery";
  public const string TogglePause = "toggle-pause";
  public const string CloseTopmostWindow = "close-topmost-window";

  // Mouse camera movement, per camera mode (see the game manual's "Camera Movement" tables). Action names
  // are per-mode since the same gesture means different things per mode (e.g. RMB-hold alone strafes in
  // Normal/Isometric but rotates-around-self in Freelook). Rows that drive one combined gesture via two
  // alternate triggers (e.g. Normal's wheel-hold and RMB+LMB-hold rotate/tilt rows) share one action with
  // two bindings instead. Only "Normal" is consumed by InputController today; Freelook/Isometric bindings
  // exist but aren't wired to behavior yet.

  // Normal camera mode
  /// <summary>RMB-hold + move mouse: 2D pan across the ground plane.</summary>
  public const string StrafeCameraNormal = "strafe-camera-normal";
  /// <summary>
  /// Wheel-hold or RMB+LMB-hold + move mouse: horizontal drag rotates, vertical drag tilts - one combined
  /// gesture, two alternate triggers for it (see the class remarks above).
  /// </summary>
  public const string RotateTiltCameraNormal = "rotate-tilt-camera-normal";

  // Freelook camera mode
  /// <summary>RMB-hold + move mouse: horizontal drag rotates the camera around its own position, vertical drag tilts it.</summary>
  public const string RotateTiltCameraFreelook = "rotate-tilt-camera-freelook";
  /// <summary>Wheel-hold or RMB+LMB-hold + move mouse: horizontal drag strafes, vertical drag zooms.</summary>
  public const string StrafeZoomCameraFreelook = "strafe-zoom-camera-freelook";

  // Isometric camera mode
  /// <summary>RMB-hold + move mouse: 2D pan across the ground plane.</summary>
  public const string StrafeCameraIsometric = "strafe-camera-isometric";
  /// <summary>Wheel-hold or RMB+LMB-hold + move mouse: horizontal drag snap-rotates 90° left/right.</summary>
  public const string RotateCamera90Isometric = "rotate-camera-90-isometric";

  /// <summary>
  /// Default action-to-binding pairs. Some actions (e.g. freelook movement) intentionally appear more
  /// than once, one entry per alternate key - <see cref="InputActionMap"/> supports multiple bindings per
  /// action.
  /// </summary>
  public static IEnumerable<KeyValuePair<string, IInputBinding>> Defaults => new[] {
    // Universal
    Pair(ExitGame, new KeyboardBinding(Key.Escape)),
    Pair(TakeScreenshot, new KeyboardBinding(Key.F10)),
    Pair(TakeAMovie, new KeyboardBinding(Key.F11, KeyModifiers.Control | KeyModifiers.Shift)),

    // During play
    Pair(CycleCoastercamTypes, new KeyboardBinding(Key.B)),
    Pair(JumpToNextCamera, new KeyboardBinding(Key.N)),
    Pair(JumpToPrevCamera, new KeyboardBinding(Key.M)),
    Pair(JumpToNextRide, new KeyboardBinding(Key.N, KeyModifiers.Shift)),
    Pair(JumpToPrevRide, new KeyboardBinding(Key.M, KeyModifiers.Shift)),
    Pair(ToggleCoasterCam, new KeyboardBinding(Key.C, KeyModifiers.Control)),
    Pair(ResetCamera, new KeyboardBinding(Key.R)),
    Pair(MoveLeftFreelookCamera, new KeyboardBinding(Key.Left)),
    Pair(MoveLeftFreelookCamera, new KeyboardBinding(Key.A)),
    Pair(MoveRightFreelookCamera, new KeyboardBinding(Key.Right)),
    Pair(MoveRightFreelookCamera, new KeyboardBinding(Key.D)),
    Pair(MoveForwardsFreelookCamera, new KeyboardBinding(Key.Up)),
    Pair(MoveForwardsFreelookCamera, new KeyboardBinding(Key.W)),
    Pair(MoveBackwardsFreelookCamera, new KeyboardBinding(Key.Down)),
    Pair(MoveBackwardsFreelookCamera, new KeyboardBinding(Key.S)),
    Pair(ZoomIn, new KeyboardBinding(Key.PageUp)),
    Pair(ZoomIn, new MouseScrollBinding(ScrollDirection.Up)),
    Pair(ZoomOut, new KeyboardBinding(Key.PageDown)),
    Pair(ZoomOut, new MouseScrollBinding(ScrollDirection.Down)),
    Pair(RotateMapLeft, new KeyboardBinding(Key.Q)),
    Pair(RotateMapRight, new KeyboardBinding(Key.E)),
    Pair(RotateMapUp, new KeyboardBinding(Key.Home)),
    Pair(RotateMapDown, new KeyboardBinding(Key.End)),
    Pair(ToggleLightUnderMousePointer, new KeyboardBinding(Key.L, KeyModifiers.Shift)),
    Pair(RotateIsometricCamera, new KeyboardBinding(Key.Enter)),
    Pair(ReverseIsometricCameraRotation, new KeyboardBinding(Key.Enter, KeyModifiers.Shift)),
    Pair(FlattenTerrainSmoothed, new KeyboardBinding(Key.AltLeft, KeyModifiers.Control)),
    Pair(FlattenTerrainPlacingScenery, new KeyboardBinding(Key.AltLeft)),
    Pair(TogglePause, new KeyboardBinding(Key.P)),
    Pair(CloseTopmostWindow, new KeyboardBinding(Key.Backspace)),

    // Mouse camera movement (see the manual's "Camera Movement" tables; ZoomIn/ZoomOut above already
    // cover the plain "mouse wheel scroll" rows shared by Normal/Isometric).
    // Normal
    Pair(StrafeCameraNormal, new MouseBinding(MouseButton.Right)),
    Pair(RotateTiltCameraNormal, new MouseBinding(MouseButton.Middle)),
    Pair(RotateTiltCameraNormal, new MouseChordBinding(MouseButton.Right, MouseButton.Left)),
    // Freelook
    Pair(RotateTiltCameraFreelook, new MouseBinding(MouseButton.Right)),
    Pair(StrafeZoomCameraFreelook, new MouseBinding(MouseButton.Middle)),
    Pair(StrafeZoomCameraFreelook, new MouseChordBinding(MouseButton.Right, MouseButton.Left)),
    // Isometric
    Pair(StrafeCameraIsometric, new MouseBinding(MouseButton.Right)),
    Pair(RotateCamera90Isometric, new MouseBinding(MouseButton.Middle)),
    Pair(RotateCamera90Isometric, new MouseChordBinding(MouseButton.Right, MouseButton.Left)),
  };

  private static KeyValuePair<string, IInputBinding> Pair(string action, IInputBinding binding) => new(action, binding);
}
