# Plan: Path Network Data Model

**Roadmap**: Phase 1, "Render paths"

**See also**:
- [`terrain-heightmap.md`](terrain-heightmap.md) — `Terrain`'s per-tile `TerrainCorner` model (height, slope
  derived from corner equality, ride-imposed height ceilings). This plan follows the same "derive, don't
  store" philosophy where possible, and reuses the ride/track height-constraint pattern for raised paths.

## Context

Paths are the next unclaimed Phase 1 item after the flat empty park. This is purely a data-model design task —
tile connectivity and graph structure — independent of any texture/mesh rendering.

## Goals

- **Ownership**: `Park` owns a sparse dictionary of path-tile data, keyed by `Terrain`'s existing tile
  coordinate type (whatever indexing type/struct terrain uses, including the OOB border, per
  `terrain-heightmap.md`) rather than a new type defined here — path and terrain coordinates stay
  interchangeable by construction. Sparse because most tiles have no path — unlike terrain, which is dense
  over the whole map.
- **At-grade paths** (the common case):
  - A path tile placed at-grade has no stored slope; its slope is derived live from the underlying `Terrain`
    tile's four corner heights, same as terrain's own slope classification.
  - Placement is constrained to terrain with a rise of less than 1m across the tile (i.e. flatter than a full
    slope step) — enforced at placement time, not stored as a flag.
  - Auto-connection: a placed path tile connects to any adjacent (N/S/E/W) path tile automatically.
  - Sloped at-grade connection: connects to any adjacent path tile whose relevant edge height is within half
    the placement-constraint rise (i.e. within 0.5m) of this tile's — not restricted to only the slope
    direction.
- **Raised paths**:
  - A `Raised` flag on the path tile switches it out of the at-grade (terrain-derived) model entirely.
  - When raised, the tile stores its own height and one of a discrete slope set: `Flat`, `Sloped` (2m rise),
    `Stair` (4m rise) — not a free-form corner-height model like terrain's. This is a dedicated encoding
    (base height + slope enum), not `TerrainCorner.Height`'s per-corner unit — a raised path tile never has
    four independent corners, so the terrain unit doesn't fit.
  - Raised paths are decoupled from terrain for slope/connectivity purposes. The only terrain interaction is
    support-post placement: post height = raised path height minus terrain height at that point. Post
    rendering itself is out of scope for this plan (rendering concern).
  - Raised paths constrain terrain edits the same way ride tracks do in `terrain-heightmap.md`: terrain
    beneath a raised path cannot be raised past the path's underside. This reuses the same "external
    height-ceiling query" API shape called out there — no new mechanism needed, just another caller.
- **Queue paths**: a distinct path subtype (at-grade or raised) that stores a direction per edge now, even
  though flow/routing logic is deferred — avoids a data migration once routing lands. Only the direction
  flag is in scope here; what consumes it (capacity, routing to station, merge/split rules) is deferred; see
  below.
- **Path graph structure**: nodes are path tiles (keyed by tile coordinate); edges are N/S/E/W adjacency,
  computed from presence + slope-compatibility rather than stored explicitly, mirroring terrain's
  compute-don't-store approach to cliff detection.

## Explicitly Out of Scope

- **Attachment points** (path tile ↔ ride entrance/exit, path tile ↔ scenery footprint): dropped from this
  plan entirely. Belongs to the ride-placement and scenery-placement plans, once those data models exist.
- **Queue flow/routing logic**: the directional edge flag is stored now; actual capacity/routing/merge
  behavior is deferred to a future ride/gameplay plan.
- **Support-post rendering**: mesh/texture concern, not data model.

## Status

Not started — stub only, but all data-model decisions (ownership, at-grade vs. raised distinction, height
encoding, queue direction storage) are settled. Nothing blocks starting implementation.
