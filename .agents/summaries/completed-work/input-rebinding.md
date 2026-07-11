# Rebindable Input Actions

Cross-cutting infrastructure motivated by RCT3's Game Options ▸ Keyboard Bindings screen. Full history is
in git log (`Add and integrate a player input rebinding system` onward) — this doc is a snapshot of the
current design and status, not a turn-by-turn narrative.

## Architecture

- **`OpenCobra.GDK.Input`** (platform/game-agnostic): `IInputBinding` implementations —
  `KeyboardBinding` (key + modifiers), `MouseBinding` (button), `MouseScrollBinding` (scroll direction),
  `MouseChordBinding` (two buttons held together) — and `InputActionMap`, which resolves string-named,
  game-defined actions against a live `Silk.NET.Input.IInputContext`. Holds
  `Dictionary<string, List<IInputBinding>>` (multiple bindings per action, e.g. alternate keys) with
  `Bind`/`AddBinding`/`GetBindings`/`IsActive`, and three events: `Pressed`/`Released` (key/button
  transitions) and `Scrolled` (scroll ticks, carrying raw tick magnitude — scroll never fires
  `Pressed`/`Released`). GDK has no notion of what actions exist or what camera modes mean.
- **`OpenRCT3.Input`**: `DefaultBindings.cs` (every action name + default binding, matching the game
  manual's keyboard and mouse camera-control tables across Normal/Freelook/Isometric modes),
  `KeyBindingOverride.cs` (JSON-serializable binding shape for `AppConfig.KeyBindings` persistence), and
  `InputController` — builds the `InputActionMap` from defaults + config overrides, and is the sole
  consumer that turns actions into camera behavior.
- **`Camera`** (`OpenCobra/GDK/Camera.cs`) gained real movement/orientation state it didn't originally
  have: `Azimuth` (rotation), `Elevation`/`Tilt` (spherical pitch, replacing the old fixed view-offset
  direction), `Zoom` (clamped to `MinDistance`/optional `MaxDistance`), and `Forward`/`Right`/`Pan`
  (ground-plane direction/panning). `MaxDistance` is set once by `Game.cs` to the whole-park framing distance and also
  anchors the far clip plane (`FarPlaneReferenceDistance`) so it doesn't shrink as the camera zooms in.

## What's implemented

- Full keyboard action set from the Game Options screenshot (universal + during-play), all bound.
- Q/E map rotation: 90° snap per press (`Camera.RotateAzimuth`).
- Zoom: keyboard (PageUp/PageDown, fixed step) and mouse wheel (magnitude-scaled), both run through
  `InputController.ZoomSensitivity` — a settable multiplier defaulted per-platform (`WINDOWS`/`OSX`
  compile constants) since Windows/macOS report scroll deltas on very different scales.
- WASD/arrow-key freelook pan, polled every frame via `IsActive` (held-key movement has no discrete
  press event) and scaled in map tiles/sec.
- **Normal camera mode's mouse-drag gestures** (from the manual's "Camera Movement" table): RMB-hold-drag
  pans/strafes (drag-to-pan sign convention), wheel-hold-or-RMB+LMB-hold-drag rotates + tilts in one
  combined gesture.
- **Freelook/Isometric mouse-drag bindings exist in `DefaultBindings.cs` but aren't wired to behavior** —
  same "data complete, behavior deferred" pattern used for other unconsumed actions.
- ImGui capture fix: game keyboard shortcuts gate on `Controller.WantTextInput` (true only while a text
  widget is actually editable), not `WantCaptureKeyboard` (true for any focused ImGui window regardless of
  whether it has a text field) — the latter was swallowing Q/E/zoom whenever the Scenario Editor window
  had focus. Mouse actions still gate on `CaptureMouse`.

## Known follow-ups

- Freelook/Isometric mouse-drag behavior (bindings only, no camera effect yet).
- A `CameraMode` enum/switch — mode-dependent effects (e.g. Q/E snap vs. continuous rotate) are currently
  hand-coded per action rather than dispatched through an explicit mode concept.
- Windows Raw Input API (`RI_MOUSE_WHEEL`/`RI_MOUSE_HWHEEL`) for finer scroll-wheel granularity than
  WinForms' `WM_MOUSEWHEEL`-based `MouseWheel` event currently provides — noted in `InputAdapter.cs`.
  `ZoomSensitivity`'s macOS default (`0.1f`) is an untested estimate, not measured against real hardware.
- Gamepad bindings, a rebinding UI, and binding-conflict detection — none implemented, structure allows
  for them later (binding kinds are an open set; conflict detection is just unimplemented).

## Tests

- `OpenCobra/Tests/GDK/Input/`: binding types (`KeyboardBindingTests`, `MouseBindingTests`,
  `MouseScrollBindingTests`, `MouseChordBindingTests`) and `InputActionMapTests` (dispatch, `Bind` vs
  `AddBinding`, `IsActive` polling), using hand-rolled `IKeyboard`/`IMouse`/`IInputContext` fakes in
  `InputMocks.cs` (no mocking framework in this project).
- `OpenCobra/Tests/GDK/CameraTests.cs`: rotation, zoom (including the `MaxDistance` far-plane regression),
  `Forward`/`Right`/`Pan`, `Elevation`/`Tilt`.
- `OpenCobra/Tests/GDK/TransformTests.cs`: `Translate`/`Rotate`/`RotateX`/`RotateY`/`RotateZ` helpers.
