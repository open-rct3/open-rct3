# Raise/Lower Brush and Smoothing Tools (Scoped Cut)

**Roadmap**: Phase 1, "Render fluctuating terrain"

**See also**:
- [`terrain-tools.md`](../../../research/terrain-tools.md) — RCT3 terrain tool reference; scoped subset of panel **A** (Snap Corners to
  Neighboring Corners), **B** (Remove Cliffs, Flatten Terrain, Flatten for Scenery and Rides), **C**
  (Hill, Mesa), **D** (Trough).
- [`../terrain-heightmap.md`](../terrain-heightmap.md) — single-corner primitives this plan builds on
  (`Terrain.RaiseCorner`/`LowerCorner`/`SetCornerHeight`, `Park.RaiseTerrainCorner`/`LowerTerrainCorner`/
  `SetTerrainCornerHeight`).
- [`water-tool.md`](water-tool.md) — sibling plan, same shape (primitives done, tool decision layer missing).
- [`rct3-terrain-data-layout.md`](../../../research/rct3-terrain-data-layout.md) —
  saved-park file-format evidence (not this plan's live in-memory model) that Panel A's ("Adjust
  Terrain Tiles") "Snap terrain tiles in increments for rides and scenery" tool steps a corner's
  on-disk `float32` height by exactly `+1.0` (one meter) per click. Panel A isn't in this plan's
  scope beyond Snap Corners to Neighboring Corners, but the same 1 m increment is what this plan's
  Panel B **Flatten for Scenery and Rides** rounds to — see that bullet below for what this
  confirms.

## Context

[`Terrain.RaiseCorner`/`LowerCorner`](../../../../OpenRCT3/Simulation/Terrain.cs:191) already propagate
an edit in `HeightStep` (1 cm) units to every tile sharing a corner via `EnumerateSharedCorners`, and
[`Park.RaiseTerrainCorner`/`LowerTerrainCorner`](../../../../OpenRCT3/Simulation/Park.cs:220) wrap them
to invalidate any `WaterPool` covering a changed tile (`InvalidateWaterPoolsSharingCorner`); that layer
doesn't change. Missing: the tool-level decision layer — which corners a brush touches, what each
smoothing behavior does to them, how Hill/Mesa/Trough shapes are derived, and how a screen click/drag
becomes `(tileX, tileY, TerrainCornerSlot)` calls into that layer. Scope: six tools (one
snap-from-outside, three grid/brush, two freeform raise, one freeform lower) covering every decision
shape the rest of the panel will reuse.

**No mouse→tile picking exists yet.** Searched `OpenRCT3/` for raycast/pick/screen-to-world helpers —
none found; `Editor.cs` (the scenery/scenario editor `IWindow`) has no tile-picking either, despite
`Park.TryPlaceScenery` existing. This plan therefore also owns the first world-space picking primitive
in the codebase, not just terrain-tool logic on top of an existing one.

Deferred to a follow-on plan: Pulling, Freeform Corner-Pulling, Spray Mode, Flatten Dynamically,
Create Cliffs, Averager, Mountain, Ridge, Crater, Canyon, and Corner Snapping to Scenery/Coasters
(blocked on missing data models).

## Goals

### Shared primitive: brush corner enumeration

- Brush diameter `N` centered on tile `(x, y)` covers an `N`×`N` footprint; `N=1` is a single tile.
- A footprint's corners form an `(N+1)`×`(N+1)` grid of **distinct** world-space points, not
  `N×N×4` copies — since `RaiseCorner`/`LowerCorner` propagate per shared corner, looping tile-by-tile
  would double- or quadruple-apply shared/interior corners.
- New shared building block: `GetCornersInBrush(centerX, centerY, diameter)`, yielding one canonical
  `(tileX, tileY, slot)` per distinct corner. Each tool below is a different per-corner height decision
  over the same enumeration, feeding straight into `Park.RaiseTerrainCorner`/`LowerTerrainCorner`/
  `SetTerrainCornerHeight` (not the raw `Terrain` methods, so water invalidation stays automatic).
- Canonical-tile tie-break for shared corners is an implementation detail — any consistent choice
  works since `Terrain.EnumerateSharedCorners` keys corner identity off world position
  (`TerrainCornerSlot.SouthWest = (x, y)`, `SouthEast = (x+1, y)`, `NorthWest = (x, y+1)`,
  `NorthEast = (x+1, y+1)`, per `TerrainCornerSlot.cs`), not which tile the call was issued through.

### Panel A — Snap Corners to Neighboring Corners

- For each boundary-ring corner, consider both outside neighbors (both are reachable; diagonal is not
  — see `terrain-heightmap.md`'s edge-vs-corner distinction). Snap to whichever neighbor is nearest
  the cursor's current terrain-relative height. Look up the `Edge` toward that neighbor, read that
  neighbor's matching-slot height via `Terrain.GetEdgeCornerHeights`/`GetCorner` (`Terrain.cs:284`,`120`),
  and `RaiseCorner`/`LowerCorner` the boundary corner to match; interior corners reach their new heights
  through the same calls' existing propagation, not a second pass. Re-joins a freeform-edited region to
  its surroundings.

### Panel B — Smoothing Tools

All three enumerate the brush once, compute a per-tool target, then propagate to reach it:

- **Flatten Terrain**: target = height of the corner under the pointer at drag start; every corner in
  `GetCornersInBrush` gets `RaiseCorner`/`LowerCorner` (delta = target − current, sign picks the call)
  to reach it — a per-corner delta, not a single call, since each starts at a different height.
- **Flatten for Scenery and Rides**: same fixed-at-drag-start target, but rounded to the nearest
  multiple of `Park.AtGradePathMaxRise` (100 `HeightStep` units = 1 m, `Park.cs:61`) before applying —
  same constant `water-tool.md` reuses, and the same flatness gate `Park.IsAtGradePathPlaceable`
  (`Park.cs:118`) and the level-pad check in `Park.TryPlaceScenery` (`Park.cs:282`, `IsFootprintLevel`)
  already assume, so a flattened area is guaranteed placeable without duplicating that logic here.
  **Confirmed (user, in-game observation + file-format evidence)**: Panel A's "Snap terrain tiles
  in increments for rides and scenery" tool (single-tile brush icon, "Adjust Terrain Tiles"
  sub-panel — see `terrain-tools.md`) shares this same 1 m snap concept. Using it to raise one
  corner and re-saving showed the corner's on-disk `float32` height step by exactly `+1.0` per
  click — see
  [rct3-terrain-data-layout.md](../../../research/rct3-terrain-data-layout.md). That's
  independent, real-world confirmation that a 1 m snap increment is correct for this class of tool,
  i.e. `Park.AtGradePathMaxRise`'s existing 1 m value is right, not just an assumption carried over
  from `terrain-tools.md`'s manual-derived Granularity Notes.
- **Remove Cliffs**: for any corner where `Terrain.IsEdgeDetached` (`Terrain.cs:152`) is true on one of
  its edges, `RaiseCorner`/`LowerCorner` it to match the neighbor instead of leaving it set via
  `SetCornerHeight` — inverse of Create Cliffs (deferred). Per `Terrain.SetCornerHeight`'s own doc
  comment ("the edge re-joins automatically if a later raise/lower brings the matching neighbor corner
  back to the same height"), this tool is literally that re-join, invoked deliberately per corner
  rather than waiting for it to happen incidentally.

### Panel C — Freeform Raise (Hill, Mesa) [L79-96]

- Hill and Mesa both use a tunable `FalloffCurve` parameter to control the falloff shape from the brush center.
- Mesa additionally uses a tunable fraction of the brush radius to determine the plateau (flat) region vs. the falloff region.

Continuous-drag, not brush-based (see `terrain-tools.md` Granularity Notes) — same per-corner primitives,
but the decision is a heightfield sampled along the mouse path.

- **Hill**: for each corner along the path, compute a target height = current height + a rounded-peak
  falloff (apex proportional to drag vertical extent, smooth radial falloff to zero at brush radius),
  then reach it via `Park.RaiseTerrainCorner` (delta = target − current). Falloff curve
  (Gaussian/cosine/quadratic) is an implementation choice; the pinned shape is rounded peak + smooth
  falloff to zero. Height deltas are in `Terrain.HeightStep` (1 cm) units, matching `RaiseCorner`'s
  `delta` parameter (`Terrain.cs:191`), which is why the tool needs a finer grid than the 1 m ramp snap
  — see `HeightStep`'s own doc comment (`Terrain.cs:39`).
- **Mesa**: same path/apex, flat-topped instead of pointed — a plateau blend of Hill, still via
  `Park.RaiseTerrainCorner` so edges smooth-join rather than forming a free-standing cube.

Both share one drag-path-sample + radial-falloff evaluator; Mesa only changes the falloff's top.

### Panel D — Freeform Lower (Trough)

- **Trough**: Hill mirrored — `Park.LowerTerrainCorner`, sign flipped, with a depth cap keeping it
  "shallow" vs. Crater/Canyon (deferred). Uses the same tunable `FalloffCurve` parameter as Hill and Mesa.
  Included to lock in the lower-side mirror pattern those tools will reuse.

## Open Questions

- **Trough depth cap**: undocumented; flag for reference comparison or default to ~1 m.
- **`WorldInputLatch`'s one-frame-lagged `CaptureMouse` read**: accepted as harmless for a fixed-position
  panel (see Input wiring below) — if a future consumer of the same primitive has UI that reflows or
  moves under the cursor frame-to-frame, the lag could misclassify a click for one frame; revisit then
  rather than fixing pre-emptively for a case this plan doesn't have.

## Deferred (out of scope for this plan)

- **Panel A**: Pulling, Freeform Corner-Pulling, Spray Mode, Corner Snapping to Scenery/Coasters
  (also blocked on missing data models).
- **Panel B**: Flatten Dynamically, Create Cliffs, Averager — each a small target-height decision on
  the same enumeration, a localized addition later.
- **Panel C/D**: Mountain, Ridge, Crater, Canyon — compose from the drag-path/falloff evaluator built
  here (sharper falloff, axis-stretched, deeper cap); no new data-model work needed.
- **Water pool invalidation under freeform tools**: already handled per-corner by the `Park` wrappers
  (see `terrain-heightmap.md` Status) — noted only so implementation uses those wrappers, not raw
  `Terrain` calls.

### ImGui tool window

- New `TerrainTools` window in `OpenRCT3/UI/`, implementing the same `IWindow` (`OpenCobra.GDK.GUI`)
  that `Debug`/`Editor` do, registered via `Scene.Windows.Add(...)` in `Game.cs` alongside the existing
  `editor`/`Debug` registrations (`Game.cs:186-187`).
- Anchored bottom-left (opposite `Debug`'s top-right, `Debug.cs:30-32`): pivot `(0, 1)`, position
  `(WorkPos.X + Padding, WorkPos.Y + WorkSize.Y - Padding)`, `ImGuiCond.Always`.
- No resize/move/collapse (`ImGuiWindowFlags.NoResize | NoMove | NoCollapse`, matching `Debug.cs:33`);
  `Debug.cs:26` already TODOs extracting this workspace/pivot/padding math to a shared helper — leave a
  matching TODO here rather than duplicating it a second time.
- Layout mirrors the in-game panel (![reference](../../../assets/reference/gui/terrain%20tools%20smoothing.png)):
  brush-size widget left, raise/lower arrows middle, eight tool buttons right (4×2). This cut fills
  Hill/Mesa (Mountain/Ridge slots empty, deferred) and Remove Cliffs/Flatten Terrain/Flatten for
  Scenery and Rides; Snap Corners lives in the "Tweak Terrain" sub-panel, not rendered in v1. Disabled
  slots render greyed, not hidden.
- **Icons**: placeholder `ImGui.Button` with tool name for v1; TODO to swap in RCT3 textures later
  (out of scope to avoid coupling to a rendering concern).
- **Input wiring depends on [`screen-tile-picking.md`](../../../summaries/completed-work/screen-tile-picking.md)**
  (now implemented — `TerrainPicker.TryPickTile`/`CameraExtensions.ToRay`) — button press sets
  `SelectedTool`; world click dispatches to the matching decision function with brush size, pointer
  tile, and (freeform only) sampled drag path. This plan only consumes `TilePickResult`, it doesn't
  implement the ray march itself. Note that plan's "Known follow-ups": the Step Zero platform/DPI
  integration test was never done — recommend a manual check at non-100% Windows display scaling before
  relying on picking for real click input here.
  - **No new per-frame hook needed.** `IWindow.Render()` (`OpenCobra.GDK.GUI.IWindow`, implemented by
    `Editor`/`Debug` today) already runs once per frame, same cadence a dedicated `Update()` would —
    `TerrainTools` can poll mouse state, run the pick, and dispatch drag/click handling directly inside
    its own `Render()`, the same way `Editor.cs` reacts to its "Quit" button inline and raises `Exit`
    for the one case (`Game.cs`'s shutdown) that needs to know about it from outside. No new event is
    needed here since `TerrainTools` is both the source and the only consumer of its own click/drag
    state — an `Exit`-style event would only be justified if some other system needed to react to a
    completed edit, which isn't the case in this plan's scope.
  - **`Controller.CaptureMouse` alone isn't enough for drags — latch ownership at mouse-down.** Checking
    `WantCaptureMouse` (`Controller.cs:28`) every frame is the standard first step — per ImGui's own FAQ,
    "you should always pass mouse/keyboard inputs to Dear ImGui" and gate *your* handling on
    `WantCaptureMouse` ([ImGui FAQ](https://github.com/ocornut/imgui/blob/master/docs/FAQ.md);
    background discussion in
    [WantCaptureMouse behavior #621](https://github.com/ocornut/imgui/issues/621)) — and
    `Controller.cs:56-59` already forwards every raw mouse event to ImGui unconditionally, so that half
    of the standard pattern is already satisfied. But a per-frame re-check breaks mid-drag: a
    continuous-drag tool (Hill/Mesa/Trough) that starts over world space and then strays over the
    `TerrainTools` panel mid-drag would suddenly stop registering world hits — the mirror image of the
    "click/drag that started over your application" edge case ImGui's own maintainers describe handling
    on ImGui's *own* side of the boundary (same #621 thread; also see
    [First click goes through ImGui window #8431](https://github.com/ocornut/imgui/issues/8431) for the
    general class of capture-boundary timing bugs this pattern is meant to avoid). Solution: decide
    "does this press belong to the world?" once, at mouse-down, from `CaptureMouse` at that instant, and
    hold that decision for the full press regardless of where the cursor wanders until mouse-up
    (matching how ImGui itself keeps a widget "active" through a drag rather than re-hit-testing every
    frame).
  - **New shared GDK primitive: `OpenCobra.GDK.Input.WorldInputLatch`** (or similar), not
    terrain-specific — a small stateful helper (`bool Update(bool buttonDown)`: latches
    `!Controller.CaptureMouse` on the down-edge, stays latched until `buttonDown` goes false) that any
    future `Render()`-driven world-interaction consumer can reuse (the flying-camera route editor's
    click/drag gizmo work is the next likely consumer, same reuse rationale as `debug-draw.md`) rather
    than every tool re-deriving this by hand. This is the "extend the GDK's input abstractions" fix
    rather than a one-off `if` check buried in `TerrainTools`.
  - **One-frame lag is accepted, not fixed.** `Renderer.cs:129` calls `gui.StartFrame()`
    (`ImGui.NewFrame()`) before windows' `Render()` runs, so `WantCaptureMouse` read inside
    `TerrainTools.Render()` reflects last frame's widget layout, not this frame's — a known ImGui
    architectural quirk (see Open Questions;
    [One frame lag when clicking outside of the ImGui window/widgets? #1152](https://github.com/ocornut/imgui/issues/1152)).
    Not worth fixing here: `TerrainTools` is a
    fixed-position, fixed-content panel (no reflow frame-to-frame per its own Goals above), so the
    lagged capture boundary is spatially identical to the current frame's in practice.
  - Left-click is currently unbound to any camera action regardless (`DefaultBindings.cs` only binds
    Right/Middle/chords to camera pan/rotate — see `DefaultBindings.cs:124-134`), so there's no other
    left-click consumer to conflict with once the latch is in place.
  - **Brush cursor/preview**: highlight the diameter-N footprint under the picked tile using
    [`debug-draw.md`](debug-draw.md)'s immediate-mode line/quad primitives.
  - **Diameter spinner**: `ImGui.DragInt`/`SliderInt` bound to a `BrushDiameter` field, range 1..map
    size (`Terrain.Width`/`Height` minus `Park.OutOfBoundsBorder`).
  - **Click dispatch**: grid tools (Panel A/B) fire once per mouse-down (or per tick while held, for
    repeat-on-drag tools); freeform tools (Panel C/D) begin a drag re-sampled on mouse-move via
    `MouseState.PressedButtons`.
  - **Drag detection**: compare mouse-down tile vs. current picked tile each frame.
  - **Path sampling for Hill/Mesa/Trough**: one sample per mouse-move (step-capped) feeding the
    radial-falloff evaluator.

## Status

Not started. Builds on existing primitives (`Terrain.RaiseCorner`/`LowerCorner`/`SetCornerHeight`/
`IsEdgeDetached`, `Park.RaiseTerrainCorner`/`LowerTerrainCorner`/`SetTerrainCornerHeight`). Remaining:
brush-enumeration primitive, per-tool target-height decisions for the six tools, the drag-path/
radial-falloff evaluator, the `TerrainTools` window, and the input wiring (brush cursor/preview,
diameter spinner, click dispatch, drag detection, continuous-drag path sampling). Pointer-tile resolution
is no longer blocking — see
[`completed-work/screen-tile-picking.md`](../../../summaries/completed-work/screen-tile-picking.md)
(implemented, with one caveat: its platform/DPI integration test was never done — see that doc's Known
follow-ups). Still blocked on [`debug-draw.md`](debug-draw.md) for the brush cursor overlay.
