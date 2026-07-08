# Plan: Terrain Heightmap Data Model

**Roadmap**: Phase 1, item 4 — "Render fluctuating terrain"

**See also**:
- [`terrain/tools.md`](terrain/tools.md) — RCT3 terrain tool reference used to derive the height unit and
  smoothing behavior below.
- [`.agents/research/grass-texture-from-terrain-ovl.md`](../../research/grass-texture-from-terrain-ovl.md) —
  `TerrainType` OVL research; source of the surface/cliff paint-index model below and the `type` field
  (Ground Unblended / Cliff / Ground Blended) that drives paint-index count and blending.

## Context

`Park.cs` currently defines a flat buildable-area rectangle with a `// TODO` marking where real park data
(including terrain) needs to load in. This plan scopes the height-grid data layer that terrain rendering, path
placement, and building will all read from — data model only, no texture painting.

## Goals

- Per-tile corner data (not a shared global corner grid): each tile stores its own four corners, added
  directly to `Terrain` (no separate `HeightGrid` type) as a single flat array of a `TerrainCorner` struct,
  sized `Width * Height * 4`, where `Width`/`Height` already include the OOB border (see `Terrain.cs:39-41`).
  - This is a deliberate departure from the "classic heightmap" shape. RCT3 renders sheer cliffs as two
    adjacent tiles whose shared edge corners hold *different* heights, joined by a vertical face — a single
    shared-corner array can't represent two heights at the same (x, y) position, so corners must belong to
    tiles, not to shared grid points.
  - **Smooth slope** (the common case): a tile's corner height equals the matching corner height on its
    neighbor across that edge — no special flag, just equal values.
  - **Cliff / detached edge**: the neighbor's corresponding corner differs — renderer/mesh-gen treats this as
    a vertical face. Detecting a cliff is just an inequality check between two tiles' corners, no separate
    per-edge bitflag needed.
  - `TerrainCorner` layout: `{ short Height; byte SurfaceIndex; byte CliffIndex; }` (4 bytes, packs cleanly).
    `SurfaceIndex`/`CliffIndex` are paint-type indices into the `ter` entries decoded from
    `Terrain_RCT3.*.ovl` (32 entries observed: `Terrain_00`..`Terrain_25` + `Cliff_00`..`Cliff_05`, per
    [`grass-texture-from-terrain-ovl.md`](../../research/grass-texture-from-terrain-ovl.md)) — a `byte` covers
    that range with headroom. `CliffIndex` is stored on every corner unconditionally (simpler than
    conditional storage); it's only meaningful to the renderer where an edge is actually detached, and is
    otherwise don't-care/unused.
  - Corner surface painting (`SurfaceIndex`) is a separate concern from height and is in scope for this plan
    only as *storage* — the actual paint tool/brush is future work, same as height's raise/lower tools.
- Tile index `(0, 0)` aligns 1:1 with the existing OOB-inclusive bounds math already used by `Terrain.Width`/
  `Terrain.Height` — no separate coordinate system for buildable vs. OOB area. `Park.BuildableBounds` continues
  to describe the buildable sub-rectangle within that same index space.
- Height unit: 1 meter per corner step, matching RCT3's ramp rise (see
  `.agents/plans/features/terrain/tools.md`). Store as `short` (or `byte`, TBD on max map height) corner-height
  count, not raw meters — convert to world-space Z via `count * 1.0f` (or a `HeightStep` const) at render/query
  time.
- Slope classification per tile derived from its own four corner heights (flat, single-corner-up, diagonal,
  etc.) — unaffected by whether neighboring tiles are smooth-joined or detached, since classification only
  looks at one tile's four corners at a time.
- Surface blending: the OVL `TerrainType.type` field distinguishes Ground Unblended (0), Cliff (1), and
  Ground Blended (2) — "Blended" implies the renderer smoothly interpolates texture between corners with
  differing `SurfaceIndex` values across a tile (classic RCT-style terrain blending), rather than a hard edge
  per tile. This plan only needs to store `SurfaceIndex` per corner; how blending is resolved is a rendering
  concern, tracked as an open question below rather than decided here.
- Height-change API: raise/lower corner(s), where:
  - The default action on a normal (non-cliff) edit propagates to the matching corner on neighboring tiles,
    keeping edges smooth-joined — matching "Snap Corners to Neighboring Corners" in `terrain/tools.md`.
  - An explicit "detach edge" operation breaks that link for one edge, turning it into a cliff — this is what
    the freeform Cliff/Crater/Canyon-style tools and manual cliff editing produce.
  - No global max-delta clamp is needed: a "steep slope" and a "cliff" aren't points on the same continuum in
    this model, they're different tile-relationships (joined vs. detached edge).
  - **Confirmed (user, in-game observation)**: rejoin is automatic, not a one-way operation — edges that end
    up matching again (e.g. one side raised/lowered back to meet the other) auto-rejoin, and texture blending
    visibly resumes at that moment. This validates that "detached" isn't a stored flag at all: it's purely
    derived from corner-height equality between neighboring tiles, checked live, both for the vertical-face
    render and for whether `SurfaceIndex` blending applies across that edge.
  - **Confirmed (user, in-game observation)**: placed rides constrain terrain edits, not just read from
    terrain. A flat ride's foundation footprint, and a tracked ride's station/track-segment footprint
    (explicitly *not* support footers), cap how far corners under/adjacent to them can be raised — raising
    only goes up to flush with the ride's base, not past it. This means the raise/lower API needs a way to
    query "is this corner constrained by a placed ride, and to what max height" before applying an edit — out
    of scope to design in full here (it depends on ride/building data models that don't exist yet), but the
    corner edit API shape must leave room for a per-corner height ceiling supplied by an external query rather
    than assuming terrain edits are always unconstrained.
- Water level as a separate horizontal plane value, independent of terrain height.

## Resolved

- **Max map height**: not documented anywhere (manual, forums) — only per-scenario designer caps exist (e.g.
  "no paths above 49 feet" in one park's editor settings), which is scenario config, not an engine limit.
  Decision: store `TerrainCorner.Height` as `short` and move on — the struct is still only 4 bytes/corner
  (trivial at 138x138x4), so there's no cost to picking the safe upper bound now. If a real max height turns
  up later that would fit in a `byte`, downsizing is a mechanical follow-up, not a design change.

## Deferred (out of scope for this plan)

These depend on data models that don't exist yet (ride/building placement, rendering); this plan's job is to
make sure the corner/tile API shape doesn't foreclose them, not to resolve them.

- **Ride-placement-validity side effects of cliff detach**: beyond the confirmed path-connectivity and
  texture-blending behavior (see Goals), are there other adjacent-tile side effects of a detached edge? Belongs
  to the ride/building placement plan.
- **Ride-constrained terrain edit enforcement**: mechanics are confirmed (raise-only cap, per-tile for flat-ride
  foundations, per-corner for track segments, footprint-scoped, footers excluded — see Goals), but actually
  wiring up the height-ceiling query requires the ride/building data model, which doesn't exist yet.
- **Blended rendering**: how the renderer resolves texture across a tile whose corners have differing
  `SurfaceIndex` values ("Ground Blended", `TerrainType.type == 2`) is a rendering-plan concern, additionally
  blocked on the BTBL bitmap-table question in `grass-texture-from-terrain-ovl.md`.

## Status

Not started — stub only. Data-model decisions (ownership, storage layout, indexing, height units, per-tile
corners vs. shared grid, `TerrainCorner` struct with surface/cliff paint indices, max-height storage) are
fully settled. Nothing blocks starting the grid storage and slope-classification implementation.
