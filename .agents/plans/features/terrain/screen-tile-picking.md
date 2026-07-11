# Plan: Screen-to-Tile Picking

**Roadmap**: Phase 1, "Render fluctuating terrain" (blocks [`raise-lower-smoothing-tools.md`](raise-lower-smoothing-tools.md) input wiring)

**See also**:
- [`raise-lower-smoothing-tools.md`](raise-lower-smoothing-tools.md) — the terrain-tool plan whose "Input
  wiring" section flagged this gap: no screen→world picking exists anywhere in the codebase.
- [`../../../../OpenCobra/GDK/Camera.cs`](../../../../OpenCobra/GDK/Camera.cs) — camera this plan reads
  from (`Eye`, `Value` view-projection matrix).
- [`../../../../OpenRCT3/Simulation/Terrain.cs`](../../../../OpenRCT3/Simulation/Terrain.cs) — heightfield
  this plan hit-tests against.

## Context

Searched the repo for any existing raycast/pick/screen-to-world helper: none exists.
`OpenCobra.GDK.Camera` exposes only a combined view-projection `Matrix4x4` (`Value`, from `Uniform<Matrix4x4>`)
— no inverse, no separate `View`/`Projection`. There is no `Ray` type and no `Silk.NET.Maths` dependency;
only `System.Numerics` (`Vector2`/`Vector3`/`Matrix4x4`) is used project-wide. This plan is the first
screen-to-world picking primitive in the codebase, not an extension of one.

## Step Zero: Verify Platform Coordinate Spaces (blocking, before any picking math)

Unverified assumption this plan depends on: that `IMouse.Position`, `IView.FramebufferSize`, and the
actual GL viewport all agree on the same pixel space. That's not yet proven true —
[`GameWindow.cs:67`](../../../../OpenRCT3/Platforms/Windows/GameWindow.cs) defines `FramebufferSize` as
`glSurface.ClientSize`, a WinForms client-area size that is DPI-*virtualized* unless the process has
correct per-monitor-v2 DPI awareness, while [`GameWindow.cs:57`](../../../../OpenRCT3/Platforms/Windows/GameWindow.cs)
reads `Dpi` from a separate `Graphics.DpiX`/`DpiY` call — nothing currently cross-checks the two, and
`IMouse.Position` (`InputController.cs:139`) has never been verified against either. On a scaled
(non-100%) display, a mismatch here would make every pick land on the wrong tile by a consistent,
DPI-dependent offset — silently, since nothing renders yet to visually catch it.

Before implementing `Camera.ToRay`/`TryPickTile`, add:

- **Unit test**: exercise the screen→ray math directly against a fixed `Camera` state (no real window
  needed — see `OpenCobra/Tests/GDK/Input/InputMocks.cs` for this repo's existing test-double patterns,
  used elsewhere for `IMouse`/`IKeyboard`), proving the math in isolation from the platform. **Done** —
  see `CameraExtensionsTests.cs`'s `ToRay_*` tests.
- **Integration test** (real `GameWindow`/`GLSurface`, at 100% and at least one non-100% Windows display
  scale — e.g. run manually at 125%/150% first, then decide whether CI can simulate it): confirm
  `IView.FramebufferSize` matches the actual `glViewport` dimensions in use, and that a known
  `IMouse.Position` value (e.g. simulated click at the window's exact center) resolves to a world-space
  ray passing through `Camera.Target` — the one point guaranteed to be dead-center on screen for an
  unpanned camera. A mismatch here means `FramebufferSize`, `Dpi`, or `IMouse.Position` needs a
  correction factor applied before `Camera.ToRay` is trustworthy.

This is a prerequisite, not part of the Testing section below — if it turns up a mismatch, fixing it is
in scope for this plan before the ray march is built on top of unverified coordinates.

## Goals

- **`OpenCobra.GDK.Ray`**: a new `readonly record struct Ray(Vector3 Origin, Vector3 Direction)` in the
  engine layer (`OpenCobra.GDK`), alongside `Camera` — this is graphics-engine math, not game logic, and
  belongs where `Camera` already lives rather than under `OpenRCT3/`.
  - `CameraExtensions.ToRay(this Camera camera, Vector2 screenPos, Vector2D<int> framebufferSize) : Ray`
    — an extension method (kept off `Camera` itself, in a new `OpenCobra.GDK.CameraExtensions` static
    class) that builds the ray **analytically from the camera's basis vectors**, not by inverting
    `Camera.Value`. See "Superseded" below for why the matrix-inversion approach this Goals section
    originally specified was replaced.
  - `framebufferSize` is `IView.FramebufferSize` (`Silk.NET.Maths.Vector2D<int>`, pixels) — the same
    property `Game.cs` already reads to size the projection's aspect ratio — not a DPI-scaled logical
    size. `screenPos` must already be in that same pixel space. `framebufferSize` also supplies the
    aspect ratio directly (`X / Y`), so it stays a single source of truth rather than a separately-passed
    value that could drift out of sync with it.
  - Steps:
    1. Convert `screenPos` (pixels, Y-down) to NDC X/Y in `[-1, 1]` (Y-up, so flip):
       `ndcX = 2 * screenPos.X / framebufferSize.X - 1`,
       `ndcY = 1 - 2 * screenPos.Y / framebufferSize.Y`.
    2. Build the camera's true (tilt-aware) view basis the same way `Matrix4x4.CreateLookAt` derives its
       axes, so it matches `Update()`'s view matrix exactly — deliberately not `Camera.Forward`/
       `Camera.Right`, which are ground-plane-flattened for WASD panning, not the actual viewing
       direction: `forward = Normalize(Target - Eye)`, `right = Normalize(Cross(forward, UnitZ))`,
       `up = Cross(right, forward)`.
    3. `direction = Normalize(forward + right * (ndcX * tan(FieldOfView/2) * aspectRatio) + up * (ndcY * tan(FieldOfView/2)))`.
    4. `Ray.Origin = Camera.Eye`; `Ray.Direction = direction`. No near/far plane, no matrix inversion —
       the ray is exact regardless of camera distance.

### Superseded: matrix-inversion `Camera.Unproject`

The first implementation of this plan built the ray by inverting `Camera.Value` and unprojecting NDC
points at the near (`z=-1`) and far (`z=+1`) clip planes (the standard technique, e.g.
[Coding Labs: World, View and Projection Transformation Matrices](https://www.codinglabs.net/article_world_view_projection_matrix.aspx),
[Der Schmale: Unprojections Explained](https://www.derschmale.com/2014/09/28/unprojections-explained/)).
That approach shipped, then was found (via its own test suite) to lose real precision at realistic
gameplay camera distances — see the removed "Finding" note this section replaces, previously in
Implementation Notes. Root cause: `Camera`'s far clip plane sits `FarPlaneDistanceMargin` (2x) times the
framing distance from a fixed 1cm near plane, so at the default park's ~1303-unit framing distance the
far/near ratio is ~260,000:1 — ill-conditioned for `Matrix4x4.Invert` in single-precision float,
independent of near/far plane tuning.
The analytic `ToRay` approach above replaces it entirely: it never inverts a matrix and never touches the
near/far planes, so the far/near ratio doesn't enter the computation at all. Confirmed by
`ToRay_RemainsPreciseAtRealisticFullParkFramingDistances`, which holds a tight (`1e-4`) epsilon at the
same ~1303-unit distance that made the matrix-inversion approach's tests need a loosened epsilon at
*20 units*.
- **`Ray` vs. triangle intersection**: standard Möller–Trumbore (or equivalent) ray-triangle test, since
  the heightfield hit test below is triangle-based, not a plane test.
- **Shared corner-to-world mapping**: `TerrainMeshBuilder.CornerPosition` (currently `private static`)
  becomes `internal static` (or moves to a shared static helper both classes call) so the picker's
  triangle test uses the exact same world-space formula the render mesh was built from — including the
  `(tileX + dx - Width / 2f) * TileSize` X-centering offset — rather than a second, independently
  maintained copy that could silently drift from what's actually on screen.
- **Grid-stepped heightfield ray march** — the actual picking algorithm, matching how era-appropriate
  (circa-2004) engines picked against heightfields rather than raycasting a full render mesh:
  - Starting from `Camera.Eye`, step the ray in `Park.TileSize` (4 m) increments along its direction.
  - At each step, resolve the current world position `(worldX, worldY)` to a candidate tile via the
    inverse of `CornerPosition`'s mapping: `tileX = floor(worldX / TileSize + terrain.Width / 2f)`,
    `tileY = floor(worldY / TileSize)`.
  - Test that one tile's two corner-triangles for intersection, in the same fixed order
    `TerrainMeshBuilder.AddTopFace` emits them — `(SW, SE, NE)` then `(SW, NE, NW)` — since the diagonal
    split is fixed per-tile, not data-dependent; not the tile's rendered mesh, and not neighboring tiles,
    since a 4 m step can't skip past a tile without also testing it.
  - Stop at the first hit; also stop (return no pick) once the march leaves the OOB-inclusive grid
    (`Terrain.HasTile` false) or exceeds a step budget derived from `Camera.MaxDistance ?? distance`
    (mirroring `Camera.FarPlaneReferenceDistance`'s own fallback, so the budget is always finite even
    when a camera was never framed to a park — e.g. in unit tests).
  - This is synchronous, run once per click (or per hover-frame for brush preview) — no background task,
    no caching, no BVH: a handful of triangle tests per pick, bounded by the ~128×128 grid and typical
    camera-to-ground step counts (tens, not hundreds).
- **Result shape**: `TilePickResult? TryPickTile(Ray ray, Terrain terrain)` returning the hit
  `(tileX, tileY)`, the exact world-space hit point (for brush-center precision, not just tile snapping),
  and which triangle/corner-pair was hit (useful later for corner-precise tools, not just tile-precise
  ones) — or `null` if the march exits the grid/step budget with no hit.
- **Call site**: resolved via the existing `IGame.IoC` container — no new plumbing needed. The window
  (`IWindow`, extending Silk.NET's `IView`) supplies `FramebufferSize`; the live mouse position comes
  from the same `IMouse.Position` source `InputController` already reads (`OpenRCT3/Input/InputController.cs:139`).
  A picking helper (owned by `raise-lower-smoothing-tools.md`, not this plan) resolves both from IoC,
  calls `Camera.ToRay`, then `TryPickTile`.
- **[`Debug.cs`](../../../../OpenRCT3/UI/Debug.cs) diagnostics line**: `Debug.Render()` gains
  `PlatformWindow`/`IInputContext` constructor dependencies (resolved via `Game.IoC`'s `Made.Of`
  registration in `Game.cs`, not manual `Resolve` calls inside `Render()` — see Implementation Notes) and
  re-runs `Camera.ToRay` + `TryPickTile` once per frame against the current mouse position, purely for
  display — this is a second, independent call from whatever `raise-lower-smoothing-tools.md` wires up
  for actual tool input, not a shared/cached result, since a debug overlay shouldn't add a hidden
  dependency between the two.
  - Renders `ImGui.Text($"Cursor: {kind} at ({x:0.00}, {y:0.00}, {z:0.00})")` using `TilePickResult`'s
    world-space hit point.
  - `{kind}` is a placeholder for future pick categories (scenery, rides, paths — all out of scope per
    the Deferred section above); until those exist, this plan only ever prints `"Terrain"`.
  - **Gated on `!ImGui.GetIO().WantCaptureMouse`**: when the mouse is over an ImGui window (the Debug
    panel itself, or any other UI), `IMouse.Position` still reports a screen coordinate, and
    `TryPickTile` would happily report a bogus pick for whatever's behind that panel. Skip the
    `ToRay`/`TryPickTile` call entirely when `WantCaptureMouse` is true and render
    `ImGui.Text("Cursor: (UI)")` instead.
  - When `TryPickTile` returns `null` (cursor off the OOB-inclusive grid), renders
    `ImGui.Text("Cursor: none")` instead.

## Resolved Questions

- **Sub-tile precision for corner-snapping tools**: `TilePickResult` keeps the exact world-space hit
  point and hit triangle/corner-pair, even though no tool in this plan's scope consumes it yet. Kept
  deliberately: [`../../../research/terrain-tools.md:16`](../../../research/terrain-tools.md) documents
  that brush size 1 in RCT3's grid-based tool panel lets you drag a single tile's edge or corner
  directly, so `raise-lower-smoothing-tools.md` will need corner-precise hit data, not just tile
  indices, once it implements that mode — and the intersection point is already computed as part of the
  Möller–Trumbore test, so returning it costs nothing extra.

## Testing

`OpenCobra.GDK.Camera` and `OpenCobra/OVL` have no existing test coverage — this plan is the first
code to touch `Camera`'s matrix math, so new tests are added for the paths it introduces, not just
`Ray`/picking itself:

- **`Camera.ToRay`**: known screen point (e.g. viewport center) against a fixed `Eye`/`Target` state →
  known world-space ray direction (pointing at `Target`) and origin (exactly `Eye`, no `Update()`
  required first). Also a corner of the framebuffer, to catch an X/Y or Y-flip mistake that center-only
  testing wouldn't, and a realistic full-park framing distance (~1303 units) to confirm precision holds
  where the superseded matrix-inversion approach lost it.
- **Ray-triangle intersection**: unit tests on the Möller–Trumbore helper directly, decoupled from
  the terrain march — hit, miss, edge-on (ray parallel to triangle plane), and hit-behind-origin
  (should not count as a hit) cases.
- **Grid-stepped march (`TryPickTile`)**: against small synthetic `Terrain` instances —
  - Flat terrain: ray straight down hits the expected `(tileX, tileY)` and world Z.
  - A raised (propagated) corner: hit point's Z matches the raised height exactly at that vertex.
  - A detached corner (`SetCornerHeight`, no propagation — a cliff between two tiles): a ray just
    inside the edited tile hits its own raised height, not the neighbor's unraised copy of the same
    world-space corner.
  - Off-grid ray (never crosses `Terrain.HasTile`): returns `null`.
  - Step-budget exhaustion: a ray that never converges on any triangle still terminates at `maxSteps`
    and returns `null` rather than looping unbounded. (Implemented as a plain `int maxSteps` parameter,
    not a `Camera` reference — see Implementation Notes for why.)
- **`TerrainMeshBuilder.CornerPosition`**: currently untested despite already shipping in the render
  path; once it's shared with the picker (see Goals above), add a test pinning its formula (including
  the `Width / 2f` X-centering offset) so a future edit can't silently desync the picker from the
  render mesh.

## Deferred (out of scope for this plan)

- Brush cursor/preview rendering, diameter spinner, click/drag dispatch to tool decision functions — all
  still owned by `raise-lower-smoothing-tools.md`; this plan only supplies the `(tileX, tileY)` (or
  `TilePickResult`) that those consume.
- Picking against non-terrain objects (scenery, rides, paths) — out of scope; this plan is terrain-only.
  The `Debug.cs` cursor line's `{kind}` placeholder anticipates this (e.g. a future scenery-picking plan
  distinguishing "Scenery" from "Terrain" hits) but no scenery/ride hit-testing exists yet, so that line
  only ever prints `"Terrain"` for now.

## Implementation Notes

- [`OpenCobra/GDK/Ray.cs`](../../../../OpenCobra/GDK/Ray.cs) — `Ray` record struct with a Möller–Trumbore
  `Intersects` method.
- [`OpenCobra/GDK/Camera.cs`](../../../../OpenCobra/GDK/Camera.cs) — gained a public `FieldOfView` const
  (previously an inline `MathF.PI / 3f` literal in `Update()`), shared with `CameraExtensions.ToRay` so
  picking's frustum always matches the rendered one. The matrix-inversion `Unproject`/`UnprojectPoint`
  methods originally added here were removed entirely — see "Superseded" in Goals.
- [`OpenCobra/GDK/CameraExtensions.cs`](../../../../OpenCobra/GDK/CameraExtensions.cs) — `ToRay`, matching
  the Goals section's analytic-basis math exactly.
- [`OpenRCT3/Simulation/TerrainMeshBuilder.cs`](../../../../OpenRCT3/Simulation/TerrainMeshBuilder.cs) —
  `CornerPosition` changed from `private` to `internal`, shared with `TerrainPicker`.
- [`OpenRCT3/Simulation/TerrainPicker.cs`](../../../../OpenRCT3/Simulation/TerrainPicker.cs) —
  `TilePickResult` and `TerrainPicker.TryPickTile`. One deviation from the Goals section:
  `TryPickTile(Ray ray, Terrain terrain, int maxSteps)` takes the step budget as a plain `int` rather than
  a `Camera` reference — `Camera`'s live eye-to-target `distance` field is private (no public accessor),
  so the `MaxDistance ?? distance` fallback has to be computed by the caller (via
  `Vector3.Distance(camera.Eye, camera.Target)`, mathematically identical to the private field) and
  passed in, rather than `TryPickTile` reaching into `Camera` itself.
- [`OpenRCT3/UI/Debug.cs`](../../../../OpenRCT3/UI/Debug.cs) — `RenderCursorPosition()`, gated on
  `!ImGui.GetIO().WantCaptureMouse`. `Debug`'s primary constructor takes `PlatformWindow window` and
  `IInputContext inputContext` directly (aliased as `PlatformWindow` locally —
  `OpenCobra.GDK.GUI.IWindow`, the ImGui-window interface `Debug` itself implements, and
  `OpenCobra.GDK.Platform.IWindow`, the platform window, collide on the bare name once both namespaces
  are imported) rather than resolving them from `Game.IoC` inside `Render()` each frame — see the IoC
  refactor below. `Terrain`/`Camera` still come directly from the `Game` reference (`game.World.Terrain`,
  `game.Scene.Camera`).
- [`OpenRCT3/Game.cs`](../../../../OpenRCT3/Game.cs) — refactored to construct `Debug` through `Game.IoC`
  instead of `new UI.Debug(this, terrainMesh)`: registers `this` (`IoC.RegisterInstance(this)`) and the
  terrain `Mesh` (keyed as `"Terrain"`, not by bare `Mesh` type, so a later feature registering some other
  `Mesh` instance can't collide with it), then registers `Debug` via
  `Made.Of(() => new UI.Debug(Arg.Of<Game>(), Arg.Of<Mesh>("Terrain"), Arg.Of<IWindow>(), Arg.Of<IInputContext>()))`
  — the same statically-checked constructor-selection pattern `GLSurface.cs` already uses for
  `GUI.Controller`, rather than reflection-based `Parameters.Of` — and resolves it
  (`Scene.Windows.Add(IoC.Resolve<UI.Debug>())`). All four of `Debug`'s dependencies now come from the
  container; `Debug` itself never touches `Game.IoC`.

### Precision fix: matrix-inversion `Unproject` replaced with analytic `ToRay`

The first implementation of this plan (see "Superseded" in Goals) built the ray via `Matrix4x4.Invert`.
Its own test suite (`Unproject_RayOriginatesNearTheEyeAndPointsTowardTheTarget`) surfaced a real
precision problem: at the default park's ~1303-unit framing distance, the far/near ratio is ~260,000:1,
ill-conditioned for single-precision matrix inversion regardless of near/far tuning. Concretely, that
test needed its epsilon loosened from 1e-3 to 5e-3 at a test distance of just 20, and was off by 0.6
units at distance 500 — a real, growing accuracy risk for real gameplay clicks near tile boundaries when
zoomed out, not just test noise.

Fixed by replacing `Unproject` with `CameraExtensions.ToRay`, which builds the ray directly from the
camera's basis vectors (`Eye`, `Target`, `FieldOfView`) instead of inverting a matrix — no near/far
planes enter the computation at all, so the far/near ratio that caused the imprecision doesn't either.
`ToRay_RemainsPreciseAtRealisticFullParkFramingDistances` confirms a tight `1e-4` epsilon holds at the
same ~1303-unit distance that broke the old approach's tests at 20 units. This also simplified
`Debug.cs`'s null-checks: `ToRay` doesn't require `Camera.Update()` to have run first (unlike `Unproject`,
which needed `Value` to be non-null), so the `camera.Value is null` guard was removed.

### Not done in this pass: Step Zero's platform integration test

The unit-test half of Step Zero (screen→ray math, tested in isolation) is done — see
`CameraExtensionsTests.cs`. The **integration test half — real `GameWindow`/`GLSurface` at non-100%
Windows display scaling, confirming `FramebufferSize` matches the live GL viewport and `IMouse.Position`
resolves to a ray through `Camera.Target` — was not implemented.** This repo has no existing pattern for
spinning up a real windowed/GL context inside a test run (the `Integration` test project only exercises
OVL file loading, not rendering), and building that harness is a substantial task on its own. The DPI
mismatch risk described in Step Zero above is therefore still unverified in practice, not just in theory —
recommend a manual check (run at 125%/150% Windows scaling, watch the `Debug.cs` cursor line against a
known tile) before relying on `ToRay` for real tool input in `raise-lower-smoothing-tools.md`.

## Status

Core picking primitives (`Ray`, `CameraExtensions.ToRay`, `TerrainPicker.TryPickTile`) and the `Debug.cs`
cursor readout are implemented and unit-tested (60 new/updated tests across `OpenCobra.Tests` and
`OpenRCT3.Tests`, all passing, including a regression test proving the analytic `ToRay` stays precise at
realistic full-park framing distances where the original matrix-inversion approach lost precision; full
existing suites re-run clean aside from 7 pre-existing, unrelated `OpenCobra/OVL`
texture-name-mangling failures predating this work). `Debug.cs` and `Game.cs` were also refactored to
construct `Debug` fully through `Game.IoC` (`Made.Of` + keyed registrations) rather than manual `Resolve`
calls inside `Render()`. Not yet done: the Step Zero platform/DPI integration test (see above — recommend
manual verification first), and everything in Deferred (brush/click dispatch, non-terrain picking) which
remains `raise-lower-smoothing-tools.md`'s responsibility.
