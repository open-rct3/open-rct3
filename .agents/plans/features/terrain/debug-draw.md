# Immediate-Mode Debug Draw Primitives

## Context

[`raise-lower-smoothing-tools.md`](raise-lower-smoothing-tools.md)'s brush cursor/footprint outline
needs a way to draw a transient overlay (highlight the diameter-N footprint under the picked tile) that
doesn't fit the engine's existing rendering model. Today, `OpenRCT3/OpenGL/Renderer.cs` only ever draws
`PrimitiveType.Triangles` (`Renderer.cs:108`, the single `DrawElements` call in the whole codebase) from
`Scene.Models` — a plain `List<Model>` that nothing removes from at runtime (only two call sites ever
add to it, both at startup in `Game.cs:142`/`158`; no `.Remove` anywhere). Every `Mesh` is uploaded once
via `StaticDraw` (`Mesh.Upload`, `OpenCobra/GDK/Meshes/Mesh.cs:52`) and persists until disposed. There is
no line/wireframe primitive, no transient "this-frame-only" draw list, and no precedent for content that
changes shape every frame — all things a brush cursor (and later the flying-camera route editor's
path/waypoint visualization) need.

This plan designs a small, separate immediate-mode draw path — lines, wireframe boxes/quads — built on
top of the same GL context but bypassing the static-mesh/`Scene.Models` pipeline, since that pipeline's
upload-once assumption doesn't fit content that's rebuilt every frame.

## Goals

- **New type: `OpenCobra.GDK.ImDraw`**, living alongside `Renderer`/`Camera` in the engine layer —
  general-purpose, not terrain-specific, and not named/scoped as debug-only: besides dev use (brush
  cursor, route/waypoint visualization), the scenery "advanced move" translate/rotate gizmo (see
  Deferred) is a player-facing release-build consumer, so the name deliberately echoes ImGui (`ImDraw`,
  "immediate-mode draw") rather than `DebugDraw`, which would misleadingly suggest it's compiled out of
  release builds.
- **API shape is accumulate-then-flush, not immediate GL calls**, deliberately modeled on the web
  Canvas 2D API's stateless draw-call-per-shape ergonomics (`ctx.arc(...)`, `ctx.moveTo`/`lineTo`) rather
  than a `begin()`/`add_vertex()`/`end()` triple like Godot's `ImmediateGeometry` — callers make one flat
  method call per shape and never manage a begin/end pair themselves:
  - `Line(Vector3 a, Vector3 b, Vector4 color)` — raw segment, the primitive every other shape composes
    from.
  - `Axis(Vector3 origin, Quaternion rotation, float size)` — the Blender-style single-vertex marker:
    three `Line` calls from `origin` along the rotated +X/+Y/+Z basis vectors, colored red/green/blue
    respectively (fixed axis colors, not caller-supplied, so every call site reads unambiguously as
    X/Y/Z without checking a color argument).
  - `Circle(Vector3 center, Vector3 normal, float radius, Vector4 color, int segments = 32)` — a closed
    ring of `segments` `Line` calls in the plane perpendicular to `normal`; `segments` defaults rather
    than being hardcoded since a brush-footprint ring at map scale and a small waypoint marker want
    different facet counts.
  - `Arrow(Vector3 from, Vector3 to, Vector4 color, float headSize = ...)` — one `Line` for the shaft
    plus a small open-V or 4-line pyramid head at `to`, oriented along `to - from`; built from `Line`
    segments like everything else, not a separate cone mesh, so it stays in the same wireframe draw call.
  - Every shape method is a thin CPU-side loop that pushes `Line` segments into the same internal
    buffer — `Axis`/`Circle`/`Arrow` are composition, not new primitive types, matching this plan's
    existing "everything is lines" draw call. This mirrors how `debug-draw`/`im3d`/`imdd` (see Research
    below) implement circles and arrows as CPU-tessellated line/triangle loops rather than as GPU
    primitives of their own.
  - `ImDraw` uploads and draws the accumulated batch once per frame, then clears it — "immediate
    mode" in the GL1/Canvas sense of "describe this frame's shapes procedurally," not literally one
    draw call per primitive.
- **Lines are triangles, not `GL_LINES`, and are screen-space-constant-width by construction.** Per
  Anton Gerdelan's write-up (see Research), core-profile drivers commonly clamp `glLineWidth` to 1.0, so
  this plan doesn't attempt `GL_LINES` at all — every `Line` call is expanded into a thick quad (two
  triangles, four vertices) at submit time:
  - Each `Line(a, b, color)` records both endpoints plus a `width` (pixels) into the vertex buffer, one
    record per endpoint, tagged with a per-vertex "which side of the line" sign (`+1`/`-1`).
  - The vertex shader computes the line's direction in clip space, takes its perpendicular, and offsets
    each vertex along that perpendicular by `sign * (width / 2) / viewportSize` (converting the desired
    pixel width into clip-space units using the viewport dimensions) — the standard "expand in the
    vertex shader using a viewport-size uniform" technique for constant-pixel-width lines, since doing
    the expansion in clip space (after projection) is what makes the width camera-distance-independent
    rather than shrinking with perspective like a world-space quad would.
  - New uniform `u_ViewportSize` (vec2, framebuffer pixel dimensions) alongside `u_ViewProj`, needed only
    by this shader — nothing else in the renderer currently needs viewport dimensions in a shader.
- **The same clip-space-offset technique also gives screen-space-constant shape *extent*, not just line
  thickness — no separate mechanism needed.** The building block isn't really "line width," it's
  "offset this vertex from its anchor by a pixel amount that doesn't shrink with distance"; line
  thickness is one application of it (offset perpendicular to the segment), and offsetting `Axis`'s arm
  endpoints / `Circle`'s ring vertices / `Arrow`'s head outward from their shared origin by the same
  per-vertex clip-space pixel offset is the identical math, just applied along a different per-vertex
  direction. Concretely: `Axis`/`Circle`/`Arrow` gain a `bool screenSpaceExtent` (or equivalent
  size-mode) parameter —
  - `false` (default): endpoints are computed in world space as today (`size`/`radius` in world units) —
    what the brush-footprint `Circle` needs, since its radius must track the actual N-tile diameter and
    shrink/grow with the terrain it's outlining.
  - `true`: endpoints are computed as "anchor position (transformed to clip space) + pixel-space
    direction × size," reusing the exact per-vertex clip-space-offset shader path built for line
    thickness — what an `Axis` gizmo marker or `Arrow` handle wants, so it stays a legible, grabbable
    size regardless of camera distance.
  - Both modes emit the same vertex layout (position, side-sign/direction, width, color) into the same
    buffer and go through the same shader — `screenSpaceExtent` only changes what the CPU-side shape
    builder computes for each vertex's direction/anchor, not the draw path. This is why it isn't deferred
    to a later plan: the plumbing already exists once line-thickness is built, so gating it off would be
    an artificial restriction, not a real scope cut.
  - `Axis`/`Circle`/`Arrow` all inherit both the thickness and the extent behavior for free since they're
    built from the same `Line`-style per-vertex records; no separate handling needed per shape beyond
    picking which anchor/direction each vertex offsets from.
- **Dynamic buffer, not `StaticDraw`.** A dedicated VAO/VBO (no EBO — the quad-per-line expansion above
  means each line's 4 vertices are already laid out as two triangles, not shared indices) using
  `BufferUsageARB.DynamicDraw` (or `StreamDraw`, to be confirmed against actual churn) re-uploaded every
  frame via `BufferData`/`BufferSubData`, sized to the frame's vertex count — this is new territory:
  every existing `Mesh` upload in the codebase is one-time `StaticDraw`.
- **Growth strategy: allocate-to-capacity + `BufferSubData`, grow-only, never per-frame `BufferData`.**
  Surveyed how other engines handle a rebuilt-every-frame vertex buffer rather than deciding this on
  paper:
  - **Dear ImGui's OpenGL3 backend** re-checks required size against current capacity each frame and
    only reallocates (`glBufferData`) when the new frame's data exceeds it, otherwise reusing the
    existing buffer — ImGui tried buffer-orphaning + `glBufferSubData` in 2021 to fix an Intel-driver
    leak, then partially reverted after it caused glitches on NVIDIA with multi-viewports, landing back
    on plain `glBufferData` for broad driver compatibility (see Research). Lesson: prefer the simpler
    `glBufferData`-on-growth path over orphaning tricks, since orphaning's driver behavior isn't
    portable.
  - **bgfx's transient buffers** (its per-frame dynamic-geometry mechanism, the same role this VBO
    plays) use a capped ring buffer with a configurable max size and *no* runtime growth beyond it —
    stronger than this plan needs (a hard cap fits particles/UI at engine scale), but confirms
    "grow-until-a-ceiling, not unbounded per-frame realloc" is the pattern to follow, not resizing to
    the exact vertex count every frame.
  - **Adopted for `ImDraw`**: keep a capacity larger than or equal to the largest frame seen so far;
    `BufferSubData` into it when the frame's data fits, `BufferData` to a rounded-up (e.g. next
    power-of-two or +50%) capacity only when it doesn't, and never shrink — matching ImGui's
    grow-on-demand shape rather than bgfx's hard cap, since `ImDraw`'s vertex counts (a handful of
    brush-outline/gizmo shapes) are nowhere near bgfx's particle/UI scale where a hard ceiling matters.
- **Own minimal shader, not `Flat`/`Textured`.** A small vertex/fragment pair (matching the existing
  `#version 410 core` inline-string pattern in `Material.cs`) taking position + side-sign + width + color,
  uniforms `u_ViewProj` (reusing `Camera.UniformName` so it slots into the same uniform the main pass
  uses) and `u_ViewportSize` — no `u_Model` since `ImDraw` primitives are already specified in world
  space by the caller, not transformed from local space.
- **Draw call**: `gl.DrawArrays(PrimitiveType.Triangles, 0, vertexCount)` — reuses the renderer's existing
  `Triangles` primitive type (unlike an earlier draft of this plan, which proposed `GL_LINES`), just with
  its own shader/buffer instead of `Scene.Models`. Drawn after the main triangle pass (so lines render on
  top) and before `RenderGui`, in a new `Renderer` method (e.g. `RenderImDraw(Scene scene)`) called from
  `Render(Scene scene)` alongside the existing triangle pass — not folded into `BuildDisplayList`, since
  that path is keyed on `Model`/`Mesh`/`Material` and `ImDraw` intentionally skips all three.
- **Depth test respected by default, with a per-call override flag.** `Line`/`Axis`/`Circle`/`Arrow` all
  take an `alwaysOnTop` bool (default `false`): `false` respects the existing `DepthTest` enable
  (`Renderer.cs:41`) so a brush outline drawn on sloped/occluded terrain still occludes correctly; `true`
  disables depth testing for that draw's vertices only, for future gizmo handles that need to stay
  visible/grabbable through occluding geometry. Cheap to add now (one bool, branching the GL state around
  a sub-range of the draw rather than a whole second buffer/shader) rather than an API break later — the
  same reasoning that pulled `screenSpaceExtent` in-scope above. Implementation detail (not designed
  here): grouping same-flag vertices into contiguous draw ranges, or issuing two `DrawArrays` calls with
  `glDepthMask`/`glDisable(GL_DEPTH_TEST)` toggled between them, since a single draw call can't mix depth
  modes per-vertex.
- **Default line width**: a `width` parameter in pixels on `Line` (and therefore `Axis`/`Circle`/
  `Arrow`), not a global constant — the shader expansion above makes width a per-vertex value, so there's
  no reason to hardcode one. Actual default pixel value is an implementation-time eyeball check, not
  decided on paper here.

## Research: how other engines/libraries do this

- **[glampert/debug-draw](https://github.com/glampert/debug-draw)** — immediate-mode, renderer-agnostic
  API that batches every primitive and only actually draws on an explicit `dd::flush()` at end of frame:
  the same accumulate-then-flush shape this plan already commits to. Confirms that approach is the norm,
  not a simplification.
- **[im3d](https://github.com/john-chapman/im3d)** and **[imdd](https://github.com/sjb3d/imdd)** — both
  CPU-tessellate circles/arrows/gizmos into line or triangle lists rather than exposing them as GPU
  primitives; `imdd` ships drop-in GL3.2/Vulkan backends confirming a single generic vertex-color shader
  (this plan's "own minimal shader") is the standard backend shape for this kind of API.
- **[Anton Gerdelan's "Debug Draw" article](https://antongerdelan.net/blog/formatted/2015_06_16_debug_draw.html)**
  — direct confirmation that `GL_LINES`/point-size/line-width are unreliable across modern-GL drivers,
  and that triangles are the more portable choice when a shape needs to render *thick* (arrowheads,
  wide outlines); reinforces this plan's existing "quad fallback" note for line width (Gaps and Risks
  #1) rather than introducing anything new.
- **[Godot's `ImmediateGeometry`](https://docs.godotengine.org/en/3.2/tutorials/content/procedural_geometry/immediategeometry.html)**
  — the `begin()`/`add_vertex()`/`end()` shape the user asked to avoid;
  noted here only to record why this plan's API is call-per-shape (`Line`/`Axis`/`Circle`/`Arrow`)
  instead: it reads closer to the Canvas 2D API the user prefers, and avoids callers having to manage a
  begin/end pair or get the primitive-type argument right at each call site.
- **[Dear ImGui's OpenGL3 backend](https://github.com/ocornut/imgui/blob/master/backends/imgui_impl_opengl3.cpp)**
  (see its [orphaning commit](https://github.com/ocornut/imgui/commit/389982eb5afbb9f93873d87f1201842ccbc82dec)
  and [partial revert](https://github.com/ocornut/imgui/commit/ca222d30c8ca3e469c56dd981f3a348ea83b829f))
  — grows its vertex/index buffers on demand and reuses them otherwise; tried buffer-orphaning +
  `glBufferSubData` to fix an Intel-driver leak, then partially reverted it after NVIDIA multi-viewport
  glitches, settling back on plain `glBufferData` for portability. Directly informs this plan's buffer
  growth strategy (Goals, above).
- **[bgfx's transient buffers](https://bkaradzic.github.io/bgfx/internals.html)** — the same per-frame
  dynamic-geometry role this plan's VBO plays (bgfx explicitly lists debug rendering as a use case), but
  backed by a capped ring buffer with no runtime growth past a configured max — confirms
  "grow-until-a-ceiling" is the general pattern, even though `ImDraw`'s scale doesn't warrant bgfx's hard
  cap.

## Gaps and Risks

1. **Screen-space-width shader math needs an empirical check.** The clip-space perpendicular-offset
   technique (Goals, above) is standard but fiddly to get pixel-exact. Near-degenerate cases (a line
   nearly parallel to the view direction, where its screen-space perpendicular is ill-defined) collapse
   to a zero-area quad — i.e. that segment briefly disappears rather than rendering a visible artifact —
   which is the resolved behavior; the exact numerical threshold for "degenerate enough to collapse"
   still needs an empirical check once implemented. *(Open — threshold value only.)*
2. **Buffer resize churn — resolved, see Goals' "Growth strategy" above.** Grow-on-demand
   (`BufferSubData` within current capacity, `BufferData` to a rounded-up capacity only when it doesn't
   fit, never shrink), following Dear ImGui's OpenGL3 backend rather than per-frame reallocation or
   buffer-orphaning (see Research for why orphaning was ruled out — driver-specific glitches, not a
   portable win).
3. **`ImDraw` must not be compiled/stripped out of release builds** — the scenery "advanced move"
   translate/rotate gizmo (see Deferred) is player-facing, so whatever this type becomes has to ship in
   release. The `ImDraw` name (rather than `DebugDraw`) already signals this; flagged here as a residual
   risk so a future contributor doesn't add a `#if DEBUG` guard around it out of habit, matching the
   type's name-only fix, not a build-system change.
4. **`Circle`'s segment count is a per-call tradeoff, not a global constant.** Too few segments on a
   large-diameter brush footprint reads as visibly polygonal; too many on every small waypoint marker
   wastes vertices for no visible gain. Defaulting `segments` (see Goals) rather than hardcoding covers
   this, but the actual default value needs an empirical look once brush footprints exist to eyeball
   against — not decided on paper here.

## Open Questions

- **`DynamicDraw` vs. `StreamDraw`** for the debug VBO — semantically `StreamDraw` (upload-once,
  draw-once-or-few, discard) fits an every-frame-rebuilt buffer better than `DynamicDraw`
  (upload-many-times, draw-many-times), but this is a driver-hint difference with no measured impact
  yet; pick empirically once profiling is possible, not on paper.

## Deferred

Known future consumers of this same `ImDraw` type/primitive set, listed so this plan's API isn't
accidentally shaped around the brush cursor alone — none of them are designed here, but each is a reason
`Line`/`Axis`/`Circle`/`Arrow` are kept generic (world-space, color-parameterized, not terrain-specific):

- **Flying-camera route editor's path/waypoint visualization** — the route itself as a `Line` polyline
  through waypoints, each waypoint marked with `Axis` (orientation) and/or `Circle` (capture radius).
  Not designed here beyond confirming `ImDraw`'s API is general enough to serve it; that plan scopes
  its own visualization needs when it exists.
- **Peep waypoint / pathfinding debugging** — same shape as the route editor's need (a `Line` path
  through discrete points, each markable with `Axis`/`Circle`), for visualizing peep AI navigation.
  Confirms `Circle`'s `normal` parameter matters (a peep waypoint's capture radius is drawn flat on the
  ground plane, a camera waypoint's likely isn't) rather than always defaulting to a fixed axis. Not
  designed here — no peep pathfinding system exists yet to hang this off of.
- **General-purpose dev debug gizmos** (arbitrary future "draw this vector"/"mark this point" needs
  during development) — the reason `Axis`/`Circle`/`Arrow`/`Line` are exposed as a public, general
  `ImDraw` API rather than four bespoke methods hardcoded to the brush cursor's exact call sites.
- **"Advanced move" translate/rotate gizmos for placed scenery** — a player-facing in-game tool (not a
  dev-only debug aid like the other consumers above), so its `ImDraw`-rendered handles double as
  released-build UI, not something compiled out in release. The most demanding future consumer, but
  lighter-weight than it first looked: `screenSpaceExtent` (Goals) already gives it fixed-pixel-size
  handles that don't vanish at distance or dwarf the viewport up close, using the same clip-space-offset
  math built for line thickness. What's still out of scope is **hit-testing** — this plan draws shapes,
  it doesn't pick them, so ray/handle intersection for drag-start detection belongs to whichever plan
  designs that gizmo, not here. `Axis`/`Arrow` give that future plan its visual vocabulary (the arrow
  shape for translate handles, `Axis` for the orientation triad) and now its sizing behavior too, but not
  the interaction layer.
- **Filled/translucent quad overlays** (vs. wireframe) — if the brush-cursor plan ends up preferring a
  translucent footprint fill over an outline, that's a `PrimitiveType.Triangles` addition to this same
  accumulate-then-flush buffer, not a new primitive type; left unscoped until that decision is made.
- **Text/label debug drawing** — out of scope; ImGui's `RenderGui` pass already covers on-screen text.
- **Full 3D gizmo interaction** (drag handles, hover highlighting, im3d-style manipulators, hit-testing
  against a rendered handle) — `Axis` here is a static marker (Blender's single-vertex XYZ indicator),
  not an interactive translate/rotate/scale gizmo; out of scope per this plan's Context (brush cursor +
  route waypoints only need static shapes) even though it's now sized correctly (`screenSpaceExtent`) for
  that future use. The scenery move/rotate gizmo above is the concrete future plan that would need the
  actual drag/hover/hit-testing behavior.

## Status

Not started. Amended to add `Axis`/`Circle`/`Arrow` primitives (composed from `Line`) per
`raise-lower-smoothing-tools.md`'s brush cursor needs, after a survey of glampert/debug-draw, im3d, imdd,
and Godot's `ImmediateGeometry` (see Research) — API is call-per-shape, Canvas-2D-flavored, not a
begin/end triple. Renamed `DebugDraw` → `ImDraw` (release-build player-facing gizmo use, not debug-only)
and redesigned lines as screen-space-constant-width triangles (clip-space vertex-shader expansion)
instead of `GL_LINES`, per known future consumers: route/waypoint visualization, peep pathfinding debug,
general dev gizmos, and the player-facing scenery "advanced move" translate/rotate gizmo. Added
`screenSpaceExtent` on `Axis`/`Circle`/`Arrow` so shape *extent* (not just thickness) can be constant
on-screen size — no longer deferred to the gizmo plan; only hit-testing/drag interaction remains deferred
there. Added a per-call `alwaysOnTop` depth override flag; resolved the degenerate-line-direction edge
case (collapse to zero-area quad) and the buffer-growth strategy (grow-on-demand `BufferSubData`,
following Dear ImGui's OpenGL3 backend over buffer-orphaning or bgfx-style hard caps — see Research).

**Implemented.** [`OpenCobra/GDK/ImDraw.cs`](../../../../OpenCobra/GDK/ImDraw.cs),
[`OpenCobra/GDK/Scene.cs`](../../../../OpenCobra/GDK/Scene.cs) (`Scene.ImDraw`, alongside `Scene.Camera`),
[`OpenRCT3/OpenGL/Renderer.cs`](../../../../OpenRCT3/OpenGL/Renderer.cs) (`InitializeImDraw`/
`RenderImDraw`/`UploadImDraw`, `#region ImDraw`). Two corrections found during implementation, not
foreseeable on paper:
- **`screenSpaceExtent` ended up CPU-side distance/FOV scaling, not a literal reuse of the line-thickness
  vertex-shader offset.** The thickness technique needs a screen-projected reference direction (the
  line's `OtherPosition`) that a single-origin shape like `Axis`'s marker or `Circle`'s radius doesn't
  have at the vertex level. Implemented instead as the standard editor-gizmo formula
  (`pixels * 2 * distance * tan(fov/2) / viewportHeight`, computed once per frame from `Scene.Camera.Eye`
  via `ImDraw.BeginFrame`) applied to `size`/`radius` before generating world-space `Line` calls — same
  "reuse info already available every frame" spirit, different mechanism than what Goals originally
  described.
- **Draw order isn't "before `RenderGui`."** `ImDraw` shapes are submitted *by* `IWindow.Render()` (a
  terrain tool's brush cursor, drawn from inside its own window `Render()`), which only runs *inside*
  `RenderGui`'s window loop — so drawing before `RenderGui` would render last frame's (or an empty)
  buffer. `Renderer.RenderGui` now calls `scene.ImDraw.BeginFrame(...)`, then the windows loop (shapes
  get submitted here), then `RenderImDraw`, then `scene.ImDraw.Clear()`, then `gui.Render()` — geometry
  ends up over the 3D scene and under ImGui's composited overlay, matching the original intent, just
  sequenced correctly relative to when shapes actually arrive.

Not yet exercised by a real caller (`raise-lower-smoothing-tools.md`'s brush cursor is still not started)
— compiles and the render path is wired up, but visual verification with an actual submitted shape is
still open.
