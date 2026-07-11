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

- **New type: `OpenCobra.GDK.DebugDraw`** (or similar), living alongside `Renderer`/`Camera` in the
  engine layer — general-purpose, not terrain-specific, since the flying-camera route editor is a known
  second consumer.
- **API shape is accumulate-then-flush, not immediate GL calls**: callers append primitives once per
  frame (`Line(Vector3 a, Vector3 b, Vector4 color)`, `Box(Vector3 min, Vector3 max, Vector4 color)`,
  built from `Line` segments) into an internal buffer; `DebugDraw` uploads and draws the accumulated
  batch once, then clears it — "immediate mode" in the GL1 sense of "describe this frame's shapes
  procedurally," not literally one draw call per primitive.
- **Dynamic buffer, not `StaticDraw`.** A dedicated VAO/VBO (no EBO — lines don't need indexing) using
  `BufferUsageARB.DynamicDraw` (or `StreamDraw`, to be confirmed against actual churn) re-uploaded every
  frame via `BufferData`/`BufferSubData`, sized to the frame's vertex count — this is new territory:
  every existing `Mesh` upload in the codebase is one-time `StaticDraw`.
- **Own minimal shader, not `Flat`/`Textured`.** A small vertex/fragment pair (matching the existing
  `#version 410 core` inline-string pattern in `Material.cs`) taking only position + color, uniform
  `u_ViewProj` (reusing `Camera.UniformName` so it slots into the same uniform the main pass uses) — no
  `u_Model` since debug-draw primitives are already specified in world space by the caller, not
  transformed from local space.
- **Draw call**: `gl.DrawArrays(PrimitiveType.Lines, 0, vertexCount)` — the first non-`Triangles`
  primitive type anywhere in the renderer. Drawn after the main triangle pass (so debug lines render on
  top) and before `RenderGui`, in a new `Renderer` method (e.g. `RenderDebugLines(Scene scene)`) called
  from `Render(Scene scene)` alongside the existing triangle pass — not folded into `BuildDisplayList`,
  since that path is keyed on `Model`/`Mesh`/`Material` and debug-draw intentionally skips all three.
- **No depth-test override by default**: debug lines respect the existing `DepthTest` enable
  (`Renderer.cs:41`) so a brush outline drawn on sloped/occluded terrain still occludes correctly;
  an always-on-top mode is a per-call flag left for whichever future consumer needs it (not the brush
  cursor, which should occlude normally).
- **Line width**: left at GL default (1px) for v1 — no `gl.LineWidth` call exists anywhere yet, and
  most modern GL profiles ignore widths > 1 anyway; a thicker/quad-based outline is a fallback if 1px
  proves too hard to see, not designed here.

## Gaps and Risks

1. **Line width beyond 1px may not render** on core-profile GL (many drivers clamp `glLineWidth` to 1.0
   in core profile, unlike legacy/compat profile). If the brush outline needs to be visibly thick, the
   fallback is drawing thin quads instead of `GL_LINES` — flagged here so the brush-cursor plan doesn't
   assume line width is tunable without checking this first. *(Open — needs an empirical check once
   implemented.)*
2. **Buffer resize churn**: if the debug-draw vertex count varies a lot frame-to-frame (e.g. brush
   diameter changes), naive `BufferData` every frame reallocates GPU storage each time. A pragmatic v1
   fix (allocate to a rounded-up capacity, `BufferSubData` within it, only reallocate on growth) is
   deferred to implementation rather than decided here. *(Open.)*

## Open Questions

- **`DynamicDraw` vs. `StreamDraw`** for the debug VBO — semantically `StreamDraw` (upload-once,
  draw-once-or-few, discard) fits an every-frame-rebuilt buffer better than `DynamicDraw`
  (upload-many-times, draw-many-times), but this is a driver-hint difference with no measured impact
  yet; pick empirically once profiling is possible, not on paper.
- **Multiple debug-draw "layers"** (e.g. always-on-top vs. depth-tested) — this plan only designs the
  depth-tested case (brush cursor); whether the flying-camera route editor needs an always-on-top mode
  in the same `DebugDraw` type or a second instance is left to that plan.

## Deferred

- **Flying-camera route editor's path/waypoint visualization** — the stated second consumer of this
  primitive; not designed here beyond confirming `DebugDraw`'s API (world-space lines/boxes with color)
  is general enough to serve it. That plan scopes its own visualization needs when it exists.
- **Filled/translucent quad overlays** (vs. wireframe) — if the brush-cursor plan ends up preferring a
  translucent footprint fill over an outline, that's a `PrimitiveType.Triangles` addition to this same
  accumulate-then-flush buffer, not a new primitive type; left unscoped until that decision is made.
- **Text/label debug drawing** — out of scope; ImGui's `RenderGui` pass already covers on-screen text.

## Status

Not started.
