# Immediate-Mode Draw Primitives (`ImDraw`)

Built a general-purpose immediate-mode line/wireframe draw path — `Line`/`Axis`/`Circle`/`Arrow` —
separate from the engine's static-mesh pipeline, for content that's rebuilt every frame (brush
cursors, route/waypoint visualization, future scenery move/rotate gizmos).

## What landed

1. **`OpenCobra/GDK/ImDraw.cs`** — accumulate-then-flush API (`Line`, `Axis`, `Circle`, `Arrow`),
   Canvas-2D-flavored (one flat call per shape, no begin/end pair). Lines are triangles (thick
   quads), not `GL_LINES`, expanded in the vertex shader using a `u_ViewportSize` uniform for
   constant-pixel-width lines regardless of camera distance. Dynamic VBO with grow-on-demand
   `BufferSubData`/`BufferData` (never shrink), following Dear ImGui's OpenGL3 backend rather than
   buffer-orphaning or a bgfx-style hard cap.
2. **`OpenCobra/GDK/Scene.cs`** — `Scene.ImDraw` property alongside `Scene.Camera`.
3. **`OpenRCT3/OpenGL/Renderer.cs`** — `InitializeImDraw`/`RenderImDraw`/`UploadImDraw`
   (`#region ImDraw`), own minimal shader (position + side-sign + width + color; reuses
   `Camera.UniformName` for `u_ViewProj`). Per-call `alwaysOnTop` bool bypasses depth test for
   that draw's vertices only.
4. `Axis`/`Circle`/`Arrow` support `screenSpaceExtent` for fixed-pixel-size shapes (gizmo handles)
   vs. world-scale (brush-footprint rings).

## Corrections found during implementation (not foreseeable on paper)

- **`screenSpaceExtent` is CPU-side distance/FOV scaling, not a reuse of the line-thickness
  vertex-shader offset.** A single-origin shape (`Axis` marker, `Circle` radius) has no
  screen-projected reference direction the thickness technique needs. Implemented as the standard
  editor-gizmo formula (`pixels * 2 * distance * tan(fov/2) / viewportHeight`), computed once per
  frame from `Scene.Camera.Eye` via `ImDraw.BeginFrame`, applied to `size`/`radius` before
  generating world-space `Line` calls.
- **Draw order is not "before `RenderGui`."** `ImDraw` shapes are submitted *by* `IWindow.Render()`
  (e.g. a terrain tool's brush cursor), which only runs *inside* `RenderGui`'s window loop.
  `Renderer.RenderGui` now calls `scene.ImDraw.BeginFrame(...)` → windows loop (shapes submitted
  here) → `RenderImDraw` → `scene.ImDraw.Clear()` → `gui.Render()` — geometry renders over the 3D
  scene and under ImGui's composited overlay.

Visually verified: `Axis` renders correctly in-game (Blender-style red/green/blue marker,
screen-space-constant size). Exercised end-to-end through the real render path, not just
compiling.

## Not done / deferred

`ImDraw`'s API was deliberately kept generic (world-space, color-parameterized, not
terrain-specific) for these known future consumers — none designed here, only confirmed to be
servable by the shipped API:

- **Flying-camera route editor's path/waypoint visualization** — route as a `Line` polyline
  through waypoints, each marked with `Axis` (orientation) and/or `Circle` (capture radius).
- **Peep waypoint / pathfinding debugging** — same shape as the route editor's need; confirms
  `Circle`'s `normal` parameter matters (a peep waypoint's capture radius lies flat on the ground
  plane, a camera waypoint's likely doesn't) rather than a fixed default axis.
- **General-purpose dev debug gizmos** — arbitrary future "draw this vector"/"mark this point"
  needs; the reason `Axis`/`Circle`/`Arrow`/`Line` are a public API rather than bespoke methods.
- **"Advanced move" translate/rotate gizmos for placed scenery** — player-facing, released-build
  UI (not dev-only), which is why the type is named `ImDraw` rather than `DebugDraw` and must
  never be compiled out of release builds. `screenSpaceExtent` already gives it fixed-pixel-size
  handles; `Axis`/`Arrow` give it its visual vocabulary (orientation triad, translate-handle
  shape). Still out of scope: **hit-testing** — this plan draws shapes, it doesn't pick them, so
  ray/handle intersection for drag-start detection belongs to whichever plan designs that gizmo.
- **Filled/translucent quad overlays** (vs. wireframe) — if the brush-cursor plan ends up wanting
  a translucent footprint fill instead of an outline, that's a `PrimitiveType.Triangles` addition
  to the same accumulate-then-flush buffer, not a new primitive type.
- **Text/label debug drawing** — out of scope; ImGui's `RenderGui` pass already covers on-screen
  text.
- **Full 3D gizmo interaction** (drag handles, hover highlighting, hit-testing against a rendered
  handle) — `Axis` here is a static marker (Blender's single-vertex XYZ indicator), not an
  interactive manipulator; the scenery move/rotate gizmo above is the concrete future plan that
  would need the actual drag/hover/hit-testing behavior.

Also not done — brush cursor and route-editor consumers themselves are still future work; `ImDraw`
is exercised end-to-end through the real render path but has no real caller yet. Empirical tuning
left open: degenerate-line-direction collapse threshold, default line width, default `Circle`
segment count, `DynamicDraw` vs. `StreamDraw` — all deferred to eyeballing once real consumers
exist.

## References

- [`OpenCobra/GDK/ImDraw.cs`](../../../OpenCobra/GDK/ImDraw.cs)
- [`OpenCobra/GDK/Scene.cs`](../../../OpenCobra/GDK/Scene.cs)
- [`OpenRCT3/OpenGL/Renderer.cs`](../../../OpenRCT3/OpenGL/Renderer.cs)
- [`raise-lower-smoothing-tools.md`](../../plans/features/terrain/raise-lower-smoothing-tools.md) — the brush-cursor plan this was carved out of
