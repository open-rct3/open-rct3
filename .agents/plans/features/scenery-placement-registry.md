# Plan: Scenery Placement Registry

**Roadmap**: Phase 1, items 6-7 — "Render built-in static (unanimated) scenery items" / "Render built-in
animated scenery items"

## Context

Scenery placement (where an object sits, at what rotation/scale, static vs. animated) is a data-model concern
separable from whether the underlying mesh/texture for that object has been resolved yet. This lets placement
and object-registry design proceed without depending on the still-open texture decoding work
([ovl-texture-decoding.md](../../bugs/ovl-texture-decoding.md)).

## Goals

- Placement record: object reference (OVL symbol/`svd` key), grid position, rotation (likely quarter-turns to
  start, per RCT series convention), and a footprint (single-tile vs. multi-tile).
- Static vs. animated distinction as a first-class property of the registry entry, not just the underlying OVL
  `FileType` — the registry should be usable before animated-scenery rendering exists.
- Object registry: a lookup from OVL symbol name to a placeable "definition" (footprint, category, whether it's
  static or animated) — decoupled from the still-in-progress `svd`/mesh/texture resolution pipeline.
- Roadmap item 3 (Phase 3) mentions "freeform, off-grid scenery placement" later — confirm the registry's
  position/footprint model can be extended to non-grid-aligned placement without a rewrite.

## Open Questions

- Does the registry key on the raw OVL symbol name, or a stable internal ID that survives OVL re-extraction?
- Where does "placed scenery" live — as part of `Park`, or a separate `SceneryLayer` service alongside the
  future `PathNetwork`?
- How much of the animated-scenery timeline/keyframe model belongs here vs. in the eventual GDK animation
  system?

## Status

Not started — stub only. Needs a deeper design pass before implementation.
