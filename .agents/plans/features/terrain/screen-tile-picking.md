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

Before implementing `Camera.Unproject`/`TryPickTile`, add:

- **Unit test**: a fake/mock `IWindow` (see `OpenCobra.Tests/GDK/Input/InputMocks.cs` for existing
  mocking patterns) asserting `Camera.Unproject`'s NDC conversion math is internally consistent for a
  given `(screenPos, framebufferSize)` pair — this doesn't touch the real platform, just proves the math
  in isolation.
- **Integration test** (real `GameWindow`/`GLSurface`, at 100% and at least one non-100% Windows display
  scale — e.g. run manually at 125%/150% first, then decide whether CI can simulate it): confirm
  `IView.FramebufferSize` matches the actual `glViewport` dimensions in use, and that a known
  `IMouse.Position` value (e.g. simulated click at the window's exact center) unprojects to a world-space
  ray passing through `Camera.Target` — the one point guaranteed to be dead-center on screen for an
  unpanned camera. A mismatch here means `FramebufferSize`, `Dpi`, or `IMouse.Position` needs a
  correction factor applied before `Camera.Unproject` is trustworthy.

This is a prerequisite, not part of the Testing section below — if it turns up a mismatch, fixing it is
in scope for this plan before the ray march is built on top of unverified coordinates.

## Goals

- **`OpenCobra.GDK.Ray`**: a new `readonly record struct Ray(Vector3 Origin, Vector3 Direction)` in the
  engine layer (`OpenCobra.GDK`), alongside `Camera` — this is graphics-engine math, not game logic, and
  belongs where `Camera` already lives rather than under `OpenRCT3/`.
  - `Camera.Unproject(Vector2 screenPos, Vector2D<int> framebufferSize) : Ray` — converts a screen-space
    point to a world-space ray using `Matrix4x4.Invert(Value, out var inverse)` computed at call time (no
    cached inverse on `Camera`; picking is a per-click operation, not per-frame, so recomputing is
    negligible and avoids widening `Camera`'s API/invariants for every other feature that reads `Value`).
  - `framebufferSize` is `IView.FramebufferSize` (`Silk.NET.Maths.Vector2D<int>`, pixels) — the same
    property `Game.cs` already reads to size the projection's aspect ratio — not a DPI-scaled logical
    size. `screenPos` must already be in that same pixel space.
  - Unprojection steps (standard technique, e.g.
    [Coding Labs: World, View and Projection Transformation Matrices](https://www.codinglabs.net/article_world_view_projection_matrix.aspx),
    [Der Schmale: Unprojections Explained](https://www.derschmale.com/2014/09/28/unprojections-explained/)),
    expressed with this project's existing `System.Numerics` types — no new math types needed:
    1. Convert `screenPos` (pixels, Y-down) to NDC X/Y in `[-1, 1]` (Y-up, so flip):
       `ndcX = 2 * screenPos.X / framebufferSize.X - 1`,
       `ndcY = 1 - 2 * screenPos.Y / framebufferSize.Y`.
    2. Unproject two points at the same NDC X/Y but different NDC Z — near (`-1`) and far (`+1`) — using
       this project's own GL depth convention (`Camera.CreatePerspectiveFieldOfViewGL`'s remarks: NDC
       z=-1 is the near plane, z=+1 is the far plane, not D3D's `[0, 1]`).
    3. For each: build a `Vector4(ndcX, ndcY, ndcZ, 1)`, transform by `inverse` (`Vector4.Transform`), then
       perspective-divide by `.W` to get the world-space point.
    4. `Ray.Origin` = the near-plane world point (not `Camera.Eye` — off-axis/asymmetric projections would
       make those differ, though this project's projection is symmetric today); `Ray.Direction` =
       `Vector3.Normalize(farPoint - nearPoint)`.
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
  calls `Camera.Unproject`, then `TryPickTile`.
- **[`Debug.cs`](../../../../OpenRCT3/UI/Debug.cs) diagnostics line**: `Debug.Render()` (currently
  constructor-injected with only `Game` and the terrain `Mesh`) gains an `IMouse`/`Terrain`/`Camera`
  dependency (resolved from `IGame.IoC`, same as the picking call site above — not passed through the
  constructor, to avoid widening `Debug`'s signature for a diagnostics-only feature) and re-runs
  `Camera.Unproject` + `TryPickTile` once per frame against the current mouse position, purely for
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
    `Unproject`/`TryPickTile` call entirely when `WantCaptureMouse` is true and render
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

- **`Camera.Unproject`**: known screen point (e.g. viewport center) against a fixed `Eye`/`Target`/
  `Update(aspectRatio)` state → known world-space ray direction (pointing at `Target`) and origin
  (`Eye`). Also a corner of the framebuffer, to catch an X/Y or Y-flip mistake that center-only
  testing wouldn't.
- **Ray-triangle intersection**: unit tests on the Möller–Trumbore helper directly, decoupled from
  the terrain march — hit, miss, edge-on (ray parallel to triangle plane), and hit-behind-origin
  (should not count as a hit) cases.
- **Grid-stepped march (`TryPickTile`)**: against small synthetic `Terrain` instances —
  - Flat terrain: ray straight down hits the expected `(tileX, tileY)` and world Z.
  - Sloped terrain: hit point's Z matches the interpolated corner heights, not a flat plane.
  - A cliff (`IsEdgeDetached`) tile: verifies the march tests the near face, not the far tile's
    corners, when the ray crosses a detached edge.
  - Off-grid ray (never crosses `Terrain.HasTile`): returns `null`.
  - Step-budget exhaustion with `Camera.MaxDistance` unset: still terminates and returns `null`
    rather than looping unbounded, exercising the `MaxDistance ?? distance` fallback.
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

## Status

Not started.
