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
using Silk.NET.Input;

namespace OpenRCT3.Input;

/// <summary>
/// Builds the game's <see cref="InputActionMap"/> (defaults from <see cref="DefaultBindings"/>, layered
/// with any user rebinds from <see cref="AppConfig.KeyBindings"/>) and wires it to camera behavior.
/// </summary>
public sealed class GameInputController {
  /// <summary>Degrees <see cref="DefaultBindings.RotateMapLeft"/>/<see cref="DefaultBindings.RotateMapRight"/> snap-rotate per press.</summary>
  private const float RotationStepDegrees = 90f;
  /// <summary>Fixed per-press step for the keyboard zoom bindings (PageUp/PageDown), which have no notion of "how hard".</summary>
  private const float ZoomStepDistance = 50f;
  /// <summary>
  /// Units of zoom per unit of raw scroll-wheel tick magnitude (one notch == 1.0, see
  /// <c>Platforms.Windows.InputAdapter</c>'s <see cref="ScrollWheel"/> construction) - chosen to match
  /// <see cref="ZoomStepDistance"/> so a single notch feels the same as a single PageUp/PageDown press.
  /// </summary>
  private const float ScrollZoomScale = 50f;

  /// <summary>Resolves the game's named, rebindable input actions against the window's live <see cref="IInputContext"/>.</summary>
  public InputActionMap Actions { get; }

  public GameInputController(IInputContext context, AppConfig config, Camera camera, Func<bool> quit) {
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
      else if (action == DefaultBindings.ZoomIn) camera.Zoom(-ZoomStepDistance);
      else if (action == DefaultBindings.ZoomOut) camera.Zoom(ZoomStepDistance);
    };
    Actions.Scrolled += (action, delta) => {
      if (Controller.CaptureMouse) return;
      // Positive delta (scroll up) zooms in (negative distance change); negative delta zooms out - the
      // sign is already correct for both ZoomIn and ZoomOut bindings, so no branch on action is needed.
      if (action == DefaultBindings.ZoomIn || action == DefaultBindings.ZoomOut) camera.Zoom(-delta * ScrollZoomScale);
    };
  }
}
