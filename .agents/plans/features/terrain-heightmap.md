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
- Height unit: 1 cm per corner step (`Terrain.HeightStep = 0.01f`), finer than RCT3's 1 m ramp-rise snap (see
  `.agents/plans/features/terrain/tools.md`) so freeform sculpting tools can render smoothly instead of
  stair-stepping to whole meters. Store as a `ushort` corner-height count, not raw meters — convert to
  world-space Z via `Terrain.CornerHeightToWorldZ(count) = count * HeightStep`. Grid-based tools that snap to
  the 1 m ramp rise should snap their edits to 100-unit increments through this same API, not introduce a
  separate unit.
- No discrete slope classification (flat/single-corner-up/diagonal/etc.). That taxonomy exists in the classic
  RCT engine because rendering picks from a finite set of pre-baked slope meshes/sprites — it's a rendering
  constraint, not a property of the terrain itself. This model stores exact corner heights at 1cm granularity,
  so slope is just derived math (e.g. the two triangle normals of the tile's quad, or per-edge rise/run between
  a corner pair) computed on demand wherever it's needed, not classified and stored. It's also not a single
  value per tile: the rise along the North edge and the rise along the West edge can differ independently
  (each is just the height delta between its two corners), so a tile doesn't have "a slope," it has up to four
  independent edge slopes plus whatever the diagonal split of its quad implies for the interior.
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
- **Water is per-pool, not a single map-wide level** (amended from the original single `WaterLevel` value below,
  per user recollection of the original tool's behavior — not yet re-verified against the reference
  implementation the way the corner/cliff model was):
  - The water tool detects a pool's perimeter by tracing the boundary of a connected low region and snaps that
    perimeter to 1 m increments — the same grid-tool snap granularity already used for height edits (100
    `HeightStep` units). A pool's water surface is a single flat height (also 1 m-snapped), not per-corner data.
  - Placing a pool creates a bounded water-surface mesh covering the traced region, not a modification to
    `TerrainCorner` itself — terrain height underneath a pool is unaffected; the pool is a separate overlay
    that happens to be bounded by terrain shape at creation time.
  - **Ownership/representation**: a `WaterPool` (or similar) is a flat water height plus the set of tiles it
    covers — the same sparse "set of tiles keyed by tile coordinate" shape `Park.Paths` already uses for path
    tiles (see `path-network.md`), rather than a bounding polygon: since the perimeter is grid-snapped, a tile
    set is exact and reuses an established pattern instead of inventing boundary-polygon geometry.
  - **Terrain edits invalidate whole pools, not partial regions.** Modifying the terrain height of any tile a
    pool covers deletes that entire connected pool outright, rather than attempting to reshape/re-trace its
    boundary. This is presumed to be a deliberate 2004-era performance tradeoff (avoiding a boundary re-trace
    on every terrain edit near water) rather than a technical necessity — noted here as inherited behavior to
    replicate, not re-derived from first principles. A future placement/tool layer re-creates the pool (or a
    new one) by re-running the same perimeter-detection step, same as the original placement flow.
  - **Ocean special case**: if a placed pool's traced region reaches the edge of the playable/OOB-inclusive
    grid (the "island map" case), the pool is treated as an ocean and its water surface extends to the skybox
    horizon in every direction instead of stopping at a traced tile-set boundary. Data-model-wise this is
    naturally an `IsOcean`/similar flag on the pool rather than actually storing an unbounded tile set — the
    tile-set representation above still identifies which of the map's own tiles are "wet" for gameplay/terrain
    purposes, but rendering treats an ocean pool's mesh as unbounded rather than clipped to that tile set.
  - Water height storage unit/encoding (presumably `HeightStep`-unit `ushort`, matching corner height) and the
    exact tile-set data structure (dictionary keyed like `Park.Paths`, vs. a dedicated per-pool structure) are
    left to implementation-time judgment — the shape above (pool = height + tile set + ocean flag,
    whole-pool invalidation on edit) is the part actually being decided here.

## Implementation Notes

Implemented in [`Terrain.cs`](../../../OpenRCT3/Simulation/Terrain.cs),
[`TerrainCorner.cs`](../../../OpenRCT3/Simulation/TerrainCorner.cs),
[`TerrainCornerSlot.cs`](../../../OpenRCT3/Simulation/TerrainCornerSlot.cs), and
[`Edge.cs`](../../../OpenRCT3/Simulation/Edge.cs) (renamed from `TerrainEdge` once path-network code needed the
same edge concept — see `path-network.md`). Two deviations from the Goals above, both deliberate:

- **`Terrain.HeightStep = 0.01f` (1 cm), not the 1 m ramp-rise step originally planned.** The 1 m figure is
  the grid-tool snap increment, but the freeform sculpting tools (Hill/Mountain/Mesa/Ridge/etc., see
  `terrain/tools.md`) are continuous-drag and need a finer resolution to render smoothly — a 1 m corner grid
  would make hills/valleys look stair-stepped. `TerrainCorner.Height` stores a count of `HeightStep` units;
  world-space Z is `Terrain.CornerHeightToWorldZ(height) = height * HeightStep`. Grid-based tools should snap
  their edits to 100-unit increments (1 m) when this plan's raise/lower API gets a UI in front of it.
- **`TerrainCorner.Height` is `ushort`, not `short`.** With the finer 1 cm unit, a `short`'s ~327 m range was
  judged too small a safety margin; `ushort` gives 0–655.35 m, covering any plausible theme-park terrain height
  above the Z=0 floor, while keeping `TerrainCorner` at 4 bytes (`ushort` + 2 `byte`s, `[StructLayout(Pack = 1,
  Size = 4)]`). Terrain height is never negative in this model (no below-floor terrain), so unsigned is a
  natural fit rather than a compromise.

Corner addressing (`TerrainCornerSlot`: SouthWest/SouthEast/NorthWest/NorthEast) and edges (`Edge`:
South/West/East/North) got their own small enums rather than raw ints, mirroring `Park.cs`'s existing style.
`Terrain.RaiseCorner`/`LowerCorner` take an optional `Func<int, int, TerrainCornerSlot, ushort>` height-ceiling
(or floor) query per corner, satisfying the ride-constrained-edit API-shape requirement from Goals without
implementing ride placement itself. `SetCornerHeight` writes one corner copy without propagating, which is how
a caller explicitly produces or maintains a detached cliff edge; `RaiseCorner`/`LowerCorner` propagate to every
tile that shares the touched corner, which is how a previously-detached edge automatically rejoins once the
matching corner heights agree again — implementing the confirmed auto-rejoin behavior from Goals with no
separate "rejoin" operation needed.

**Namespace considered, deferred**: after the `TerrainEdge` → `Edge` rename (see `path-network.md`), considered
whether the remaining terrain-prefixed types (`Terrain`, `TerrainCorner`, `TerrainCornerSlot`) should move into
a nested `OpenRCT3.Simulation.Terrain` namespace, dropping the prefix. Deferred: the class `Terrain` sharing a
name with that namespace makes references from other namespaces awkward (the class shadows the namespace,
forcing extra qualification), so this would also require renaming the `Terrain` class itself — a bigger,
more disruptive change than the prefix currently justifies. Only 3 terrain files exist in the flat
`OpenRCT3.Simulation` namespace today; revisit if that count grows enough that the prefix is doing the
namespace's job, and rename `Terrain` at the same time to avoid the collision. A plain `Simulation/Terrain/`
subfolder without a namespace change is the lower-friction intermediate step if organization is needed sooner.

**Bug caught by testing and fixed**: `IsEdgeDetached`'s corner-pairing was wrong. It compared each of a tile's
two edge corners to the neighbor's *diagonally opposite* corner (e.g., for the North edge, this tile's
NorthWest was compared to the neighbor's SouthEast) instead of the corner occupying the *same world-space
point* across that edge (NorthWest should compare to the neighbor's SouthWest). This produced false positives
whenever only one corner along an edge had a distinct height from its neighbor tile — exactly the case a
raise/lower edit produces. Replaced the diagonal `OppositeCorner` helper with an edge-aware `MirrorAcrossEdge`
that flips only the axis the edge crosses (X for East/West, Y for North/South). Covered by
`RaiseCorner_RejoinsPreviouslyDetachedEdge` in the test project below, which failed before the fix.

## Resolved

- **Max map height**: not documented anywhere (manual, forums) — only per-scenario designer caps exist (e.g.
  "no paths above 49 feet" in one park's editor settings), which is scenario config, not an engine limit.
  Decision: store the height as a safe upper bound and move on rather than block on finding a real number — as
  implemented this is `TerrainCorner.Height` (`ushort`, 1 cm units, 0–655.35 m; see Implementation Notes for why
  `ushort` replaced the originally-planned `short`), still only 4 bytes/corner (trivial at 138x138x4). If a real
  max height turns up later that would fit in fewer bits, downsizing is a mechanical follow-up, not a design
  change.

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
- **Water pool perimeter tracing (flood-fill placement)**: the water tool traces a pool's boundary by
  flood-filling a connected low region from a seed tile and snapping the result to 1 m increments (see Goals,
  "Water is per-pool"). That tracing/placement algorithm is not implemented — `Park.TryPlaceWaterPool` takes an
  already-decided tile set, the same way `Park.TryPlacePath` takes an already-decided `PathTile` rather than
  routing one. A future tool layer needs: a flood-fill/connected-component walk from a seed tile over tiles
  below a candidate water height, height snapping to `Park.AtGradePathMaxRise`-style 100-unit (1 m) increments,
  and running `IsAtGradePathPlaceable`-style terrain queries per tile rather than inventing new ones. The
  ocean special case (traced region reaches the grid edge) also needs its edge-reachability check implemented
  as part of this same tracing step, rather than only being settable via the `isOcean` parameter as it is now.

## Status

Implemented: `TerrainCorner`/`TerrainCornerSlot`/`Edge` types, per-tile corner storage, edge-detach
detection, and the raise/lower/`SetCornerHeight` API with external height-ceiling/floor query hooks, all in
`OpenRCT3/Simulation/`. Covered by unit tests in `OpenRCT3.Tests/Simulation/TerrainTests.cs` (12 tests,
passing). `SurfaceIndex`/`CliffIndex` are stored per the plan but nothing writes them yet (no paint tool) —
storage-only, as scoped. No discrete slope classification is planned (see Goals); mesh-gen/path/ride-placement
code that needs slope info should derive it directly from corner heights rather than looking up a stored
classification.

**Water pools implemented.** `Terrain.WaterLevel` (the single map-wide `ushort` field) is removed; replaced
by [`WaterPool.cs`](../../../OpenRCT3/Simulation/WaterPool.cs) (height + tile set + `IsOcean` flag) and
`Park.WaterPools`/`Park.WaterTiles` (list + tile-to-pool index, mirroring `Park.Paths`'s ownership pattern —
`Terrain` itself stays pool-agnostic). `Park.TryPlaceWaterPool` places a pool over a caller-supplied tile set
(rejecting empty, off-grid, or already-covered tiles). Whole-pool invalidation on terrain edit is implemented
via `Park.RaiseTerrainCorner`/`LowerTerrainCorner`/`SetTerrainCornerHeight`, which wrap the corresponding
`Terrain` methods and then invalidate every pool covering a tile whose height actually changed — using the new
`Terrain.GetTilesSharingCorner` to find every tile a raise/lower propagated to, not just the directly-edited
one. Covered by 12 tests in `OpenRCT3.Tests/Simulation/WaterPoolTests.cs` (40 tests total across the test
project, all passing). Perimeter-tracing/flood-fill placement and ocean edge-detection are not implemented —
see Deferred.
