# Plan: Path Network Data Model

**Roadmap**: Phase 1, item 3 — "Render paths"

## Context

Paths are the next unclaimed Phase 1 item after the flat empty park. This is purely a data-model design task —
tile connectivity and graph structure — independent of any texture/mesh rendering.

## Goals

- Define a tile-based path graph: nodes (path tiles) and edges (connections to N/S/E/W neighbors).
- Auto-connection rules: a placed path tile connects to any adjacent path tile automatically.
- Sloped paths: only connect across the top/bottom edge in the slope direction (per RCT series behavior).
- Queue paths as a distinct path subtype, with direction/flow implications for ride queuing later.
- Attachment points: how a path tile links to a ride entrance/exit or a scenery item's "footprint."

## Open Questions

- Does the path graph live on the same grid as terrain, or as an overlay keyed by tile coordinate?
- How much of the queue/flow model do we need now vs. defer to Phase 2 gameplay?
- Data ownership: does `Park` own the path graph directly, or a separate `PathNetwork` service?

## Status

Not started — stub only. Needs a deeper design pass before implementation.
