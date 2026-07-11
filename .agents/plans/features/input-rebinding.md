# Plan: Rebindable Input Actions

**Roadmap**: Cross-cutting infrastructure (no single phase owns this — camera freelook, tool activation, and
every other control surface consumes it). Motivated by RCT3's full keybinding UI (Game Options ▸ Keyboard
Bindings), which OpenRCT3 doesn't yet have any path toward.

**See also**:
- [`OpenRCT3/Platforms/Input/InputContext.cs`](../../../OpenRCT3/Platforms/Input/InputContext.cs) — existing
  per-platform `IInputContext` implementation this plan builds on top of, not replaces.
- [`OpenCobra/GDK/GUI/Controller.cs`](../../../OpenCobra/GDK/GUI/Controller.cs) — the only existing input
  consumer today (ImGui mouse forwarding); establishes the precedent that GDK consumes `IInputContext`
  directly rather than owning window/device creation itself.

## Context

Today there is no game-facing input consumption at all: `IInputContext` is created per-platform
(`OpenRCT3/Platforms/Windows/InputAdapter.cs`, `.../macOS/InputAdapter.cs`), registered with DryIoc in
`GameWindow.cs:47-52`, and the only thing that reads from it is `Controller.cs`, which wires
`IMouse` events straight into ImGui — no keyboard handling, no concept of a "game action," nothing
rebindable. `Camera.cs` has no freelook input handling yet either; the keybinding list in Game Options
(attached screenshot) describes a target UI surface, not anything implemented.

GDK is platform- and game-agnostic by design (see `GDK.csproj`'s dependency on `Silk.NET.Input.Common`
only, no WinForms/AppKit references). It must define the *shape* of rebindable input — actions, bindings,
resolution from raw device events — without knowing what actions exist. OpenRCT3 owns the actual action
list (`rotate-map-left`, `move-forwards-freelook-camera`, etc.) and default bindings, matching the
screenshot's Universal / During Play groupings.

## Goals

- **New namespace `OpenCobra.GDK.Input`** in `OpenCobra/GDK`, layered on top of the existing
  `Silk.NET.Input.IInputContext` (no replacement of `InputContext.cs`/the platform adapters — those still
  own device enumeration and raw event plumbing).
- **`InputAction`**: an immutable, string-named identifier (mirrors the `Uniform`/`Attribute` string-name
  pattern in `OpenCobra/GDK/Shaders/ShaderProgram.cs` rather than introducing a closed enum — GDK can't
  enumerate the game's action set, and a game-defined open string set matches how uniforms are already
  named by the consumer rather than GDK). Two binding kinds only, per the requirement that GDK not care
  about the concrete action list, just device class:
  - `KeyboardBinding(Key key, KeyModifiers modifiers = None)` — wraps `Silk.NET.Input.Key`.
  - `MouseBinding(MouseButton button)` — wraps `Silk.NET.Input.MouseButton`.
  - (Gamepad explicitly deferred, per the request — the binding kind enum/union should be structured so
    adding `GamepadBinding` later doesn't reshape existing bindings.)
- **`InputActionMap`**: the rebindable registry. Holds `Dictionary<string, IInputBinding>` (action name →
  current binding), seeded from a caller-supplied set of defaults. Exposes:
  - `Bind(string action, IInputBinding binding)` — overwrite a binding (the "user re-binds a key" path).
  - `Rebind` conflict handling: **out of scope for this plan** — first pass allows two actions to share a
    binding (matches vanilla RCT3, which does not warn on duplicate binds in the screenshot's UI); flag as
    future work rather than designing it now.
  - `TryGetBinding(string action, out IInputBinding binding)`.
  - `IsActive(string action)` — resolves the action's current binding against live `IInputContext` device
    state (`IKeyboard.IsKeyPressed`/`IMouse.IsButtonPressed`) each call rather than caching, since polling
    once per frame is cheap and avoids a second event-subscription lifecycle to manage.
  - `Pressed`/`Released` events per action, driven off the underlying `IKeyboard.KeyDown`/`KeyUp` and
    `IMouse.MouseDown`/`MouseUp` events (subscribed once per bound device, re-subscribed on rebind) — needed
    for one-shot actions like "Toggle pause" (`P`) vs. held actions like freelook movement.
- **Construction**: `InputActionMap` takes an `IInputContext` (already DI-resolvable per `GameWindow.cs:47`)
  plus the default bindings; it does not create or own the `IInputContext`.
- **Serialization boundary**: `InputActionMap` exposes enough (`IReadOnlyDictionary<string, IInputBinding>`,
  plus a way to reconstruct from one) for OpenRCT3 to persist overrides — GDK does not itself do JSON I/O,
  consistent with `AppConfig`'s existing `System.Text.Json` ownership living in `OpenRCT3.Platforms`, not GDK.

## OpenRCT3-side integration

- **Default action list**: a new `OpenRCT3/Input/DefaultBindings.cs` (or similar) enumerating the actions
  from the attached Game Options screenshot's "Universal" and "During play" groups (`exit-game` → Esc,
  `rotate-map-left` → Q, `rotate-map-right` → E, `move-forwards-freelook-camera` → W/Up-Arrow, etc.),
  registered into an `InputActionMap` at startup — the concrete list this plan intentionally leaves to
  OpenRCT3 rather than GDK.
- **Config persistence**: extend `AppConfig` (`OpenRCT3/Platforms/AppConfig.cs`) with a
  `Dictionary<string, string>? KeyBindings` (or similar serializable shape) property. On `Load()`, apply any
  stored overrides onto the default `InputActionMap` via `Bind`; `Save()` already round-trips the whole
  record via `System.Text.Json`, so no new I/O path is needed — only a new property and an apply/extract
  step around it.
- **Wiring point**: `Game.cs`'s constructor (where `Scene`, `World`, `Camera` are already assembled) is
  where an `InputActionMap` instance gets built and threaded to whatever first consumes it — most likely
  `Camera`'s eventual freelook/rotate handling, which doesn't exist yet either; this plan defines the
  action-resolution layer only, not camera movement itself (separate follow-up).
- **`Controller.cs` is untouched by this plan**: it's scoped as temporary ImGui scaffolding for other
  in-progress dev tooling, not the game's real UI — it keeps consuming `IInputContext.Mice[0]` directly for
  ImGui's own IO, with no rebinding, no `InputActionMap` involvement, and no changes here. `Controller
  .CaptureMouse`/`CaptureKeyboard` remain the "GUI has focus" signal that game-action polling should
  short-circuit on (see PoC below), but that's the only touchpoint between the two.

## Proof of concept: Q/E map rotation

To validate the abstraction end-to-end (not just define it), this plan includes wiring
`rotate-map-left`/`rotate-map-right` (Q/E) all the way through to visible camera behavior. Both actions
already exist as `KeyboardBinding`s per the Goals section; what's new here is a consumer.

- **Camera modes are game-specific, not GDK's concern**: OpenRCT3 has (at least) three distinct camera
  modes — **Isometric** (RCT2-style, 90° rotation snaps), **Regular** (perspective, Q/E free-rotate
  continuously around the focal point, plus zoom), and **Freelook** (perspective; Q/E do nothing in this
  mode — movement instead comes from other actions: right-mouse-drag rotates around the camera's own world
  position, WASD/arrow-keys translate relative to it). GDK's `InputActionMap` only resolves "is this named
  action currently active/pressed" — it has no notion of camera modes, snapping, or focal points. All of
  the mode-dependent interpretation below lives in OpenRCT3/`Camera.cs`, not GDK.
  - This also means `rotate-map-left`/`rotate-map-right`'s *effect* is mode-dependent (snap step in
    Isometric, continuous rotation in Regular, no-op in Freelook), even though the *binding* (Q/E) and the
    fact that they fire is identical across modes — the mode switch happens on the consuming side, after
    `InputActionMap` has already resolved the action as pressed/held.
- **`Camera.cs` currently has no rotation state or mode concept**: `Eye`/`Target` are set by `Frame`, with
  a single fixed `DefaultViewDirection` and no notion of Isometric/Regular/Freelook. This PoC only needs
  enough of that to demonstrate Q/E, not the full mode system:
  - Add an `Azimuth` float and rotate the Eye-relative offset around `Target`'s Z axis before `Update()`
    builds the view matrix (`Eye = Target + RotateZ(offset, Azimuth)`), replacing the fixed
    `DefaultViewDirection` use in `Frame`.
  - Scope the PoC to **Isometric-style behavior only** (snap `Azimuth` by a fixed step, e.g. 90°, per
    `Pressed` event) since it's the simplest of the three and doesn't require a held-key integration loop
    or a mode enum yet. Regular's continuous free-rotation (needs `IsActive` polling + delta-time
    integration, not `Pressed`) and Freelook's WASD/right-mouse-drag handling are real follow-up work, not
    part of proving the abstraction — noted below as out of scope so this doesn't quietly turn into a full
    camera-mode implementation.
- **Wiring**: in `Game.cs`, construct one `InputActionMap` (default bindings from the new
  `OpenRCT3/Input/DefaultBindings.cs`, including at minimum `rotate-map-left`/`rotate-map-right`) once at
  startup, alongside `World`/`Scene`. Subscribe `Camera`'s snap-rotation step to the map's `Pressed` event
  for those two actions (short-circuited when `Controller.CaptureKeyboard` is true, per above) — a discrete
  snap-rotate is exactly the one-shot case `Pressed` exists for.
- **Scope boundary**: only Isometric-mode Q/E snapping gets real behavior in this PoC. The rest of the
  screenshot's list (freelook movement, zoom, pause, etc.) stays in `DefaultBindings.cs` as
  bound-but-unconsumed entries — proving the map/binding/config layers work without scoping this plan into
  a full camera-controls rewrite.

## Explicitly out of scope

- Gamepad action bindings (structure for it, don't implement it).
- A rebinding UI (Game Options screen) — this plan is the data/resolution layer under that screen.
- Conflict detection/warning on duplicate bindings.
- A `CameraMode` enum/switch, Regular-mode continuous Q/E rotation, Freelook-mode WASD/right-mouse-drag
  handling, and every other action beyond Isometric-mode Q/E snapping and zoom in the PoC above.
- Any change to `Controller.cs`/ImGui input handling.

## Status

**Implemented**, including one addition beyond the original PoC scope (mouse-wheel zoom) and one unrelated
startup bugfix found along the way.

- **`OpenCobra/GDK/Input`**: `IInputBinding`, `KeyboardBinding`, `MouseBinding`, `KeyModifiers`,
  `InputActionMap`. One shape change from the Goals section: `InputActionMap` holds
  `Dictionary<string, List<IInputBinding>>` rather than one binding per action, plus an `AddBinding`
  method alongside `Bind` — needed because several default actions (e.g. freelook movement) already have
  two default keys (arrow key + WASD), so "one action → one binding" didn't hold even at the default-binding
  stage, not just after user rebinding. `TryGetBinding` from the Goals section became `GetBindings`
  (returns the list, empty if unbound) to match.
- **Mouse scroll support (not in the original Goals)**: `MouseScrollBinding`/`ScrollDirection`, plus a new
  `InputActionMap.Scrolled(string action, float magnitude)` event, added when wiring zoom — a scroll tick
  is a discrete event like a key press, but unlike a press it carries a magnitude (`ScrollWheel.Y`) that a
  zoom consumer wants to scale by, which neither `Pressed` nor `IsActive` can carry. `Pressed`/`Released`
  never fire for scroll-bound actions; `Scrolled` is scroll-only.
- **`OpenRCT3/Input`**: `DefaultBindings.cs` (full action list from the Game Options screenshot, all
  Universal/During-play actions bound) and `KeyBindingOverride.cs` (JSON-serializable `IInputBinding`
  shape for `AppConfig.KeyBindings`, per the Config persistence goal). `AppConfig` and `Game.cs` wiring
  match the plan as written.
- **PoC, expanded**: Q/E Isometric-style 90° snap-rotation (`Camera.RotateAzimuth`, clamped to `[0, 360)`)
  is done as planned. Zoom was added to the same PoC beyond the original scope line — `Camera.Zoom`
  (clamped to a 1-unit minimum eye-to-target distance) is driven by both `ZoomIn`/`ZoomOut`'s keyboard
  bindings (fixed step per `Pressed`) and their new scroll bindings (magnitude-scaled per `Scrolled`).
  Trigger was practical, not scope creep for its own sake: a proof-of-concept marker cube added to verify
  rotation visually turned out to need zooming out to even be on-screen at the default framing distance.
- **Startup bugfix found via the PoC, unrelated to input design**: `OpenRCT3/Platforms/Windows/GameWindow.cs`
  registered `IInputContext` with DryIoc as `Reuse.Scoped`, but nothing in the app ever opens a DryIoc
  scope — any direct `Resolve<IInputContext>()` threw `DryIoc.Error.NoCurrentScope`. `GLSurface.cs` was
  already constructing the real `IInputContext` directly via `mainWindow.CreateInput()` (bypassing the
  broken registration entirely) to hand to `Controller` — the dead `Reuse.Scoped` registration just hadn't
  been resolved directly by anything until this plan's `Game.cs` wiring tried to. Fixed by having
  `GLSurface.cs` register that same directly-constructed instance (`RegisterInstance`, replacing the dead
  registration) instead of leaving a second, non-functional registration path in `GameWindow.cs`.
- **Tests**: `OpenCobra/Tests/GDK/Input/` covers `KeyboardBindingTests`, `MouseBindingTests`,
  `MouseScrollBindingTests`, and `InputActionMapTests` (press/release/scroll dispatch, `Bind` vs
  `AddBinding`, `IsActive` polling), using hand-rolled `IKeyboard`/`IMouse`/`IInputContext` fakes in
  `InputMocks.cs` (no mocking framework in this project). `OpenCobra/Tests/GDK/TransformTests.cs` covers
  the `Translate`/`Rotate`/`RotateX`/`RotateY`/`RotateZ` helpers added to `Transform.cs` to support the
  PoC's camera math.
