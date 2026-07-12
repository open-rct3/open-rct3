# Brush Cursor and Preview Overlay

**See also**:
- [`debug-draw.md`](debug-draw.md) — `ImDraw` primitives this plan consumes (visually verified,
  `Axis` confirmed in-game).
- [`raise-lower-smoothing-tools.md`](raise-lower-smoothing-tools.md) — the tool that will own the brush
  cursor (its "Brush cursor/preview" bullet); this plan scopes the cursor itself, not the surrounding
  tool wiring.

## Context

[`debug-draw.md`](debug-draw.md) shipped the `ImDraw` immediate-mode draw path (line/quad batching, clip-
space thickness, CPU-side distance/FOV `screenSpaceExtent`) and the `Axis` primitive has been visually
verified in-game. [`raise-lower-smoothing-tools.md`](raise-lower-smoothing-tools.md) calls for a brush
cursor that highlights the diameter-N footprint under the picked tile — the first real consumer of
`ImDraw`. It also notes that "the only precedent, `Game.cs:154`'s rotation-marker cube, is an opaque
`Model`, not an outline," so there's no existing transient-overlay shape to copy from.

What this plan owns:

- The **cursor shape itself** — the visual representation of "the brush is here" — composed from
  `ImDraw` primitives.
- The **per-frame state machine** that updates the cursor every frame: poll the picked tile, project
  it, recompute footprint, re-submit shapes to `ImDraw`.
- The **wiring into the `TerrainTools` window** that the raise/lower plan will create — not the rest
  of that window (tool selection, diameter spinner, click dispatch), only the cursor/footprint overlay
  piece.

What this plan does NOT own (out of scope, deferred to other plans):

- `TerrainTools` window layout, `SelectedTool` state, button-press dispatch, drag detection, freeform
  drag-path sampling — all of
  [`raise-lower-smoothing-tools.md`](raise-lower-smoothing-tools.md)'s `IWindow` / input sections.
- Picking primitive — already done in
  [`screen-tile-picking.md`](../../../summaries/completed-work/screen-tile-picking.md); this plan
  consumes its `TilePickResult`.
- `WorldInputLatch` (the mouse-down latching helper also called out in
  [`raise-lower-smoothing-tools.md`](raise-lower-smoothing-tools.md)) — small enough to live next to
  the cursor or in its own tiny GDK addition; not designed here.

## Goals

- **New type: `OpenCobra.GDK.BrushCursor`** (or similar — name TBD), a small component that:
  - Holds a `BrushDiameter` (N), a `Position` (the currently-picked tile `(x, y)` or world-space
    anchor), and a `Visibility` (visible / hidden).
  - Exposes `Update(PickResult? pick)` and `Render(ImDraw draw)`: `Update` resolves the new position
    from the latest pick (or null = no hit = hide), `Render` re-submits the cursor's shapes to
    `ImDraw`. Together that's the entire per-frame contract — no event bus, no scene coupling.
  - Owns no GL state, no shader, no buffer — `ImDraw` owns the buffer; this is just the shape
    description.
- **Cursor shape: a `Circle` ring around the footprint, plus a single-vertex `Axis` at the picked
  tile's center.**
  - The `Circle` is the brush footprint outline — uses `ImDraw.Circle` with `normal = (0, 1, 0)`
    (flat on the ground plane), `radius = (N / 2) * tileSize` so the ring's diameter matches the
    footprint's diameter in world units. `screenSpaceExtent = false` (the radius is in world space
    and must track the actual N-tile diameter, per [`debug-draw.md`](debug-draw.md)'s
    `screenSpaceExtent` rationale).
  - The `Axis` is the picked-tile center marker (Blender-style red/green/blue triad) — small fixed
    world-space size (e.g. ~`tileSize` per arm), `screenSpaceExtent = false` so it scales with the
    world the way the rest of the terrain does.
  - Both shapes are world-space-anchored to the picked tile, with a small Y offset (e.g. +1 cm or
    one `HeightStep`) to avoid z-fighting with the terrain surface.
- **Height sampling: ring follows terrain height, not a flat Y plane.** The brush outline shouldn't
  slice through hills — for each ring vertex, sample the terrain height at that XZ and use it as
  the vertex's Y. This is the difference between an outline and a sticker; matches how a player
  reads "this is the area that will be affected." Implementation: one `Terrain.GetCorner`
  /heightmap-read per ring vertex (cheap, indexed lookup), or a one-time `ComputeHeight` sampler
  per submit frame — the existing `Terrain` surface already has the height data, no new sampling
  primitive is needed.
- **Visibility rules**:
  - Hidden when `pick` is null (cursor isn't over the world — it's over ImGui or off-screen).
  - Hidden when the picked tile is outside the playable area
    (`pick.X < Park.OutOfBoundsBorder || pick.X >= Terrain.Width - Park.OutOfBoundsBorder || …`).
  - Visible otherwise.
  - These are the only visibility rules; no tool-specific hiding for v1 (the brush cursor is the
    same shape whether the active tool is Hill or Flatten, matching how RCT3 itself draws a single
    cursor shape per active tool family).
- **Per-frame submission timing**: `BrushCursor.Render(ImDraw draw)` runs inside the `IWindow.Render()`
  loop (specifically, inside `TerrainTools.Render()` once that window exists), after `ImDraw.BeginFrame`
  and before the loop ends — same window that needs to read `TilePickResult` for its tool dispatch
  anyway, so the pick happens once per frame and is shared. No second pick, no second submission path.
- **API shape: component, not singleton.** `BrushCursor` is constructed per-window (one
  `TerrainTools` window → one `BrushCursor` instance), not a single static "the brush cursor." The
  `ImDraw` system itself is shared (it's on `Scene`), but the cursor's state — diameter, position —
  is per-tool-window because a future second tool window might want its own cursor with a different
  shape (route editor's waypoint marker, deferred per
  [`debug-draw.md`](debug-draw.md)'s Deferred list). The component shape leaves that door open
  without committing to it.
- **Color and width**: white-ish ring (e.g. `Color.FromArgb(220, 240, 240, 240)`) and standard axis
  colors. Ring width in pixels is the `ImDraw.Line` default; the actual pixel value is an
  implementation-time eyeball check, matching [`debug-draw.md`](debug-draw.md)'s
  Gaps and Risks #1/#4 approach (don't decide on paper).

## Gaps and Risks

1. **Ring segment count** — `ImDraw.Circle`'s `segments` parameter has no default chosen yet
   ([`debug-draw.md`](debug-draw.md) Gaps and Risks #4 defers the value). A brush-footprint ring
   at map scale (N = 5–20 tiles) is large enough that 16 segments reads as visibly polygonal, 32 is
   the floor, 64 is safe. The same default has to work for small footprints (N=1) where 32 is
   plenty, so 32 is the obvious starting default; bump to 64 if a real brush outline at N=20 still
   looks faceted on visual check.
2. **Y offset for z-fighting** — picking the right lift above the terrain surface to avoid
   z-fighting without floating noticeably. One `HeightStep` (1 cm) is the smallest unit the rest of
   the codebase operates in; try that first. If it still z-fights on a flat surface (unlikely but
   possible at certain camera angles), increase to 2–3 `HeightStep`.
3. **Per-ring-vertex height sampling cost** — sampling terrain height `segments` times per frame is
   trivial work (each sample is a heightmap array read, no allocation, no recursion), but the actual
   API used to read it needs to be confirmed against `Terrain.GetCorner` / heightmap accessors
   during implementation. If no public height-at-XZ sampler exists, add the smallest one that
   works; don't grow this plan to add a full heightfield query API.

## Open Questions

- **Single cursor vs. per-tool cursor shape** — RCT3 uses different cursor shapes per tool family
  (a flat ring for grid tools, a hill preview for freeform raise, etc.). This plan ships one shape
  (ring + axis) and defers per-tool variants; whether per-tool variants are wanted at all is a
  design call for the freeform tools when they ship, not decided here.
- **Color contrast against the terrain palette** — a white-ish ring is the safe default, but the
  actual terrain color palette once it exists may make white too high-contrast (eye-searing) or
  too low-contrast (invisible against snow). Eyeball against the actual palette when one exists;
  not solvable on paper.

## Deferred

- **Freeform-tool cursor variants** — the Hill/Mesa/Trough cursors (likely a translucent
  heightfield preview under the brush, not just a ring) belong to
  [`raise-lower-smoothing-tools.md`](raise-lower-smoothing-tools.md)'s Panel C/D implementation,
  not here. This plan locks in the shared `BrushCursor` component shape so per-tool variants
  compose rather than fork it.
- **`ScreenSpaceExtent` on the cursor** — the ring/axis are deliberately world-space so their size
  reads as "how big a footprint this is." A future zoomed-out camera where the cursor shrinks to
  invisibility is a real but not-yet-pressing problem; `ImDraw.Circle` with `screenSpaceExtent =
  true` is the one-line fix when it becomes one, and doesn't require changes here.
- **Route editor's waypoint cursor** — distinct shape (an `Axis` at each waypoint, a `Line` polyline
  connecting them), same component pattern. Lives in whatever plan owns the route editor, not here.
- **Picking the height under the cursor center for an "anchor height"** — the ring floats on
  per-vertex height samples today. A single anchor height (the picked tile's center height) might
  be wanted if a future per-tool cursor needs it (e.g. snapping a heightfield preview to a base
  level). Trivially derivable from the pick, not a goal here.

## Testing

- **Unit test: `BrushCursor.Update` hides the cursor on null pick / out-of-bounds pick.** A small
  test that constructs a `BrushCursor`, calls `Update(null)` and `Update(outOfBoundsResult)` and
  asserts visibility is off. Verifies the only documented visibility rules.
- **Unit test: ring vertex count is `segments` × the per-circle factor.** A test that calls
  `BrushCursor.Render` against a recording `ImDraw` (a fake that just counts submitted `Line` calls
  in a buffer) and asserts the expected number of `Line` submissions for a given N and `segments`.
  Catches off-by-one or accidental missing-vertex regressions in the height-sampling loop without
  needing a real GL context.
- **Unit test: per-vertex Y matches sampled terrain height.** A test against a fixed synthetic
  heightmap (e.g. a ramp or a flat plane) that asserts each ring vertex's Y equals the terrain
  height at that XZ. Verifies the height-sampling wiring without needing a real `Terrain` instance.
- **Existing untested code this plan modifies**: `OpenCobra/GDK/ImDraw.cs` and `OpenCobra/GDK/Scene.cs`
  (adding a `BrushCursor` field) are currently un-tested in isolation. The component-shape goal
  (`BrushCursor` is independent of GL) is what makes them testable now — the unit tests above don't
  need a real `ImDraw` instance, just a recording one. The recording fake itself is a small test
  utility added alongside the tests.
- **Visual verification**: an actual cursor in-game on real terrain — manual screenshot. Not
  automatable here; the visual checks that failed to be caught in
  [`debug-draw.md`](debug-draw.md)'s `screenSpaceExtent` and
  [Gaps and Risks #1](#gaps-and-risks) only surfaced during in-game visual checks, so this plan's
  visual checks (z-fighting at various camera angles, ring readability against the terrain palette,
  ring vertex count) need the same treatment.

## Implementation Notes

Filled in during/after implementation.

## Status

Not started. `ImDraw` is verified end-to-end (per [`debug-draw.md`](debug-draw.md) Status); this plan
adds the first real caller of it.
