# Screen-to-Tile Picking

Terrain-only screen→world picking: the first raycast/pick primitive in the codebase. Full history is in
git log (`Add terrain screen-tile picking, refactor Debug window through IoC` onward) — this doc is a
snapshot of the current design and status, not a turn-by-turn narrative.

## Architecture

- **`OpenCobra.GDK.Ray`**: `readonly record struct Ray(Vector3 Origin, Vector3 Direction)` with a
  Möller–Trumbore `Intersects(v0, v1, v2, out point)` triangle test.
- **`OpenCobra.GDK.CameraExtensions.ToRay`**: builds a screen-to-world `Ray` **analytically** from
  `Camera`'s `Eye`/`Target`/`FieldOfView` (a newly-public const, shared with `Camera.Update`) — not by
  inverting the view-projection matrix. Kept as an extension rather than a `Camera` member so `Camera`
  stays focused on view/projection state. The camera's true (tilt-aware) basis is rebuilt the same way
  `Matrix4x4.CreateLookAt` derives its axes — deliberately not `Camera.Forward`/`Camera.Right`, which are
  ground-plane-flattened for WASD panning, not the actual viewing direction.
- **`OpenRCT3.Simulation.TerrainPicker.TryPickTile(Ray, Terrain, int maxSteps)`**: grid-stepped
  heightfield ray march (`Park.TileSize` increments), testing each stepped-into tile's two fixed
  corner-triangles via `TerrainMeshBuilder.CornerPosition` (now `internal`, shared with the render mesh
  builder so picking and rendering can't drift apart). Returns a `TilePickResult` (tile index, exact
  world-space hit point, hit triangle/corner-pair) or `null` off-grid/past the step budget.
- **`Debug.cs`** renders a live `"Cursor: Terrain at (x, y, z)"` line, gated on
  `!ImGui.GetIO().WantCaptureMouse`. `Debug`'s constructor now takes `PlatformWindow`/`IInputContext`
  directly; `Game.cs` constructs it through `Game.IoC` (`Made.Of` + keyed `Mesh` registration) rather than
  `new UI.Debug(...)`, matching the `GUI.Controller` registration pattern in `GLSurface.cs`.

## Precision fix: analytic `ToRay` replaced matrix-inversion `Unproject`

The first implementation built the ray via `Matrix4x4.Invert(Camera.Value, ...)`, unprojecting NDC points
at the near/far clip planes. Its own tests surfaced a real precision problem: at the default park's
~1303-unit framing distance, the far/near ratio is ~260,000:1 — ill-conditioned for single-precision
matrix inversion, independent of near/far tuning. Replaced entirely with the analytic `ToRay` above, which
never inverts a matrix or touches the near/far planes, so the ratio that caused the imprecision doesn't
enter the computation at all — confirmed by a regression test holding a tight `1e-4` epsilon at the same
~1303-unit distance that broke the old approach's tests at 20 units.

## Known follow-ups

- **Step Zero's platform/DPI integration test was never done.** The unit-test half (screen→ray math in
  isolation) is done, but confirming `IView.FramebufferSize` matches the real GL viewport and
  `IMouse.Position` at real (non-100%) Windows display scaling was not — this repo has no existing
  pattern for a windowed/GL integration test harness (the `Integration` test project only exercises OVL
  file loading). **Recommend a manual check** (run at 125%/150% Windows scaling, watch the `Debug.cs`
  cursor line against a known tile) before `raise-lower-smoothing-tools.md` relies on `ToRay` for real
  tool input.
- Brush cursor/preview rendering, diameter spinner, click/drag dispatch, and picking against non-terrain
  objects (scenery, rides, paths) are explicitly out of scope here — owned by
  [`raise-lower-smoothing-tools.md`](../../plans/features/terrain/raise-lower-smoothing-tools.md), which
  consumes `TilePickResult`.
- `Debug.cs`'s `"Cursor: {kind} ..."` line only ever prints `"Terrain"` today; `{kind}` anticipates a
  future scenery/ride-picking plan.

## Tests

- `OpenCobra/Tests/GDK/CameraExtensionsTests.cs`: `ToRay_*` — screen-center ray through `Target`, exact
  `Eye` origin, corner-vs-center direction divergence (X/Y-swap/Y-flip regression), no `Update()`
  dependency, and precision at realistic full-park framing distance.
- `OpenCobra/Tests/GDK/RayTests.cs`: `Intersects_*` — hit, miss, parallel-to-plane, hit-behind-origin.
- `OpenRCT3.Tests/Simulation/TerrainPickerTests.cs`: flat terrain, raised (propagated) corner, detached
  (cliff) corner, off-grid ray, step-budget exhaustion.
- `OpenRCT3.Tests/Simulation/TerrainMeshBuilderTests.cs`: `CornerPosition_*` pins the shared
  corner-to-world formula (including the `Width / 2f` X-centering offset).
