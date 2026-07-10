# Plan: Raise/Lower Brush and Smoothing Tools

**Roadmap**: Phase 1, item 4 — "Render fluctuating terrain" (grid-tool follow-on)

**See also**:
- [`tools.md`](tools.md) — RCT3 terrain tool reference; this plan implements panel **A** ("Pulling",
  Freeform Corner-Pulling, Snap Corners to Neighboring Corners, Spray Mode) and panel **B** (the six
  Smoothing Tools).
- [`../terrain-heightmap.md`](../terrain-heightmap.md) — implements the single-corner primitives
  (`Terrain.RaiseCorner`/`LowerCorner`/`SetCornerHeight`, `Park.RaiseTerrainCorner`/`LowerTerrainCorner`/
  `SetTerrainCornerHeight`) this plan builds a multi-corner brush layer on top of.
- [`water-tool.md`](water-tool.md) — a sibling tool-layer plan with the same shape: existing per-edit
  primitives already implemented, tool-level decision logic (there: flood fill; here: brush footprint)
  still missing.

## Context

`Terrain.RaiseCorner`/`LowerCorner` already do the hard part for a *single* corner: apply a delta,
propagate to every tile sharing that world-space corner, and respect an optional per-corner
ceiling/floor query (for ride-constrained edits). `Park`'s wrappers additionally invalidate any
`WaterPool` touched by the edit. None of that needs to change. What's missing is everything the manual's
panels A and B describe as acting on a *brush footprint* — a diameter-N square of tiles — rather than one
corner: nothing today decides which corners a brush touches, or what each of the six smoothing behaviors
does to them. This plan designs that decision layer, in terms of calls into the existing per-corner API.

## Goals

### Shared primitive: brush corner enumeration

- A brush of diameter `N` centered on tile `(x, y)` covers an `N`×`N` tile footprint (matching the
  manual's "diameter spinner controls brush/area of effect in grid squares" — see `tools.md`); at `N = 1`
  the footprint is the single tile, matching the manual's note that size-1 lets you drag a single
  tile's edge or corner directly.
- A footprint's corners form an `(N+1)`×`(N+1)` grid of distinct world-space points, not `N × N × 4`
  corner copies. Every brush operation below must enumerate **distinct corners once**, not once per
  tile-corner-copy — `RaiseCorner`/`LowerCorner` already propagate a single corner's edit to every tile
  sharing it, so naively looping over each tile's four corners would apply an edit twice (once directly,
  once via propagation) to every corner shared between two tiles in the footprint, and up to four times
  to a fully-interior corner.
- This plan's one new shared building block is therefore an enumerator — e.g.
  `GetCornersInBrush(centerX, centerY, diameter)` — yielding one canonical `(tileX, tileY, slot)` per
  distinct world corner point in the footprint. Every tool below (raise, lower, flatten variants,
  averager, cliff remove/create) is a different per-corner *height decision* applied over the same
  enumeration, not a different traversal.
- Canonical-tile tie-break (which of up to four owning tiles represents a shared corner) is left to
  implementation judgment — any consistent choice works since `RaiseCorner`/`LowerCorner`/
  `SetCornerHeight` all key off world-space corner identity, not which tile the call was issued through.

### Panel A — Grid-Based Tools

- **Pulling / Freeform Corner-Pulling**: apply `Park.RaiseTerrainCorner`/`LowerTerrainCorner` once per
  corner from the brush enumeration above, same delta for every corner, passing through the same
  `maxHeightQuery`/`minHeightQuery` ride-constraint hook already on the single-corner API — a brush edit
  is not a new constraint model, just N corners' worth of the existing one.
- **Snap Corners to Neighboring Corners**: for each corner strictly on the footprint's boundary ring,
  read the matching corner height on the adjacent tile just outside the brush (via
  `Terrain.GetCorner`/neighbor lookup, no new API needed) and raise/lower that boundary corner to match
  it, propagating inward implicitly through the interior via the existing propagation in
  `RaiseCorner`/`LowerCorner`. This is the tool the manual describes as smoothing terrain "previously
  shaped with freeform corner-pulling" — i.e. it re-joins a brush's edited region back to its
  surroundings rather than performing a new kind of edit.
- **Spray Mode**: not a new height decision — it's the *Pulling* decision above, re-applied on a timer
  while the mouse button is held, at a rate that increases the longer it's held. The rate-ramp and input
  polling are an input-layer concern (out of scope here, no input system exists yet); this plan's only
  claim is that the underlying per-tick operation is identical to Pulling, so no separate terrain-side
  design is needed once an input layer exists to drive it repeatedly.
- **Corner Snapping to Scenery / Corner Snapping to Coasters** — **deferred**: both need to query the
  height of nearby placed scenery or ride entrances, which depend on data models that don't exist yet
  (scenery placement, ride placement). Same shape as `terrain-heightmap.md`'s deferred
  "Ride-constrained terrain edit enforcement" — the brush-enumeration primitive above doesn't foreclose
  these, since "snap each corner to a queried height" composes with it the same way Pulling does; only
  the query source (scenery/ride height lookup) is missing.

### Panel B — Smoothing Tools

All six read the brush's corner enumeration once, compute a per-tool target, then either propagate
(`RaiseCorner`/`LowerCorner` — smooth result, matching edges rejoin) or detach (`SetCornerHeight` —
sharp terraced result, matching edges split) to reach it:

- **Flatten Terrain**: target height = the height of whichever corner was under the pointer when the
  drag started, fixed for the whole drag. Every corner in the footprint is raised or lowered
  (`RaiseCorner`/`LowerCorner`, so edges smooth-join) to that one fixed height.
- **Flatten Dynamically**: same as Flatten Terrain, except the target height is re-read from the corner
  currently under the pointer on every tick rather than fixed at drag start — the "dynamic" part is a
  per-tick re-evaluation of the same target computation, not a different target rule.
- **Flatten for Scenery and Rides**: same fixed-at-drag-start target as Flatten Terrain, but the target
  height is first snapped to the nearest 1 m increment (`Park.AtGradePathMaxRise`, 100 `HeightStep`
  units) — the same snap constant `water-tool.md` reuses for its candidate height, so a flattened area is
  guaranteed reachable by an at-grade path/ramp per `Park.IsAtGradePathPlaceable`'s existing rise check.
- **Remove Cliffs**: for every corner in the footprint, if any of its edges is detached
  (`Terrain.IsEdgeDetached`), raise/lower it (propagating) to match its neighbor rather than leaving it
  set independently — converting a sharp edge back into a smooth slope. This is explicitly the inverse
  of Create Cliffs below, and (per `terrain-heightmap.md`'s confirmed auto-rejoin behavior) is really
  just "stop calling `SetCornerHeight` here and let a normal raise/lower propagate instead."
- **Create Cliffs**: for every corner in the footprint, quantize its current height to a coarser terrace
  step (candidate: the same 1 m/100-unit grid used elsewhere, though the manual doesn't specify the
  exact terrace spacing — flagged as an open question below) and write it with `SetCornerHeight`, which
  detaches the edge instead of propagating — turning a smooth slope into cube-shaped terraced edges.
- **Averager**: compute the mean height across every corner in the footprint's enumeration, then
  raise/lower (propagating) each corner some fraction of the way toward that mean rather than snapping
  it there outright — matching the manual's "moderates terrain shape between the extremes" (a damping
  pass, not a hard flatten). The damping fraction is an open question below, not a value the manual
  specifies.

## Open Questions

- **Create Cliffs terrace spacing**: is it the same 1 m grid-tool snap used everywhere else, or a
  distinct step? Not verified against the reference implementation — flag before/during implementation,
  same caveat pattern as the per-pool water amendment in `terrain-heightmap.md`.
- **Averager damping fraction**: single-pass fraction (e.g. move each corner 50% toward the mean) vs.
  an iterative multi-pass relaxation — the manual's wording ("moderates... between the extremes")
  doesn't pin down which. Needs either reference-implementation comparison or an in-game feel check.
- **Snap Corners to Neighboring Corners at a footprint corner (not edge)**: the boundary ring includes
  the four footprint corners, each adjacent to two outside neighbors (diagonal-adjacent tiles aren't
  reachable via a shared edge at all in this data model — see `terrain-heightmap.md`'s edge-vs-corner
  distinction). Whether both matter equally or only the axis-aligned neighbors count needs confirming
  against actual tool behavior.

## Deferred (out of scope for this plan)

- **UI/input wiring**: brush cursor/preview, diameter spinner, drag-start detection, Spray Mode's
  hold-to-accelerate timing — all input-layer, none of which exists yet. This plan only designs the
  terrain-side decision functions such a layer would call.
- **Corner Snapping to Scenery/Coasters** (see Panel A above) — blocked on scenery/ride placement data
  models.
- **Panel C/D freeform sculpting** (Hill/Mountain/Mesa/Ridge/Trough/Crater/Canyon): the manual describes
  these as continuous-drag, not grid/brush-based — a materially different interaction model from the
  brush tools here, and left to its own future plan rather than folded into this one.

## Status

Not started. Builds entirely on already-implemented primitives (`Terrain.RaiseCorner`/`LowerCorner`/
`SetCornerHeight`/`IsEdgeDetached`, `Park.RaiseTerrainCorner`/`LowerTerrainCorner`); this plan covers the
still-missing brush-enumeration primitive and the per-tool target-height decisions listed above.
