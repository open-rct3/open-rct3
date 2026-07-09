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

## Rendering Model

Confirmed against the installed game's `Path/` asset tree (`D:\Steam\steamapps\common\RollerCoaster Tycoon 3
Complete Edition\Path`): each path theme (Asphalt, Tarmac, Dirt, Crazy, Sand, Leafy, Marble, Ornate, Steel,
UnderWater) ships two distinct OVL families per piece set, which maps directly onto the at-grade/raised split
already in this plan's data model:

- **At-grade pieces are decals painted onto terrain geometry, not separate 3D models.** The `Flat`, `Straight_A`/
  `_B`, `Corner_A`–`_D`, `Turn_L_A`/`_B`, `Turn_T_A`–`_C`, `Turn_U`, `Turn_X`, and `Stub` OVLs per theme are
  textures (a shared `*_Texture`/`*Texture` OVL backs the whole set) applied as a decal over the existing
  terrain mesh built by [`TerrainMeshBuilder`](../../../OpenRCT3/Simulation/TerrainMeshBuilder.cs) — consistent
  with the at-grade placement rule already in this plan (rise `< 1m`, slope derived live from `Terrain`
  corners): a path piece that never needs its own slope geometry is exactly a piece that can be a flat texture
  layer over whatever terrain shape is already there, rather than a mesh with its own vertices.
- **Raised paths are separate models**, not decals: the `Slope`, `Slope_Mid`, `Slope_Straight`,
  `Slope_Straight_Right`/`_left` OVLs are true 3D geometry for the raised connector pieces, and `*_Scenery`
  OVLs supply the railings/support posts that render alongside them. This matches `PathRaisedSlope`'s discrete
  `Flat`/`Sloped`/`SteepStair` encoding — each enum value corresponds to picking a specific piece model from
  this set, not computing geometry from stored heights the way terrain/at-grade decals do.
- **`UnderWater` is a themed decal skin like the other nine for its at-grade pieces** — `WaterFlat`,
  `WaterFlatFC`, `WaterCornerA`–`D`, `WaterStraightA`/`B`, `WaterTurn*`, and `WaterPaving` fill the same
  at-grade-decal role as e.g. Asphalt's `Flat`/`Corner_A`–`D`/`Straight_A`/`B`/`Turn_*`, just under a `Water*`
  naming convention instead of `UnderWater_*`. Where it actually diverges is the raised pieces: the
  `WaterSlope*` family (`BC`/`FC`/`TC` suffix variants) provides **two distinct raised-piece model sets for
  the same slope shape** — one for a raised path segment that's in open air, and a separate one for a segment
  that's submerged — rather than the single raised-piece set every other theme has. For this plan's data
  model, this doesn't add a new `PathTile` field: `Raised` + the existing `PathRaisedSlope` shape already
  identify *which slope*; whether a given raised segment resolves to the open-air or underwater model variant
  is a per-segment rendering-time lookup against `Terrain.WaterLevel` (is this segment's height above or below
  the water plane), not additional stored state. At-grade `UnderWater` tiles need no special handling at all —
  they're an ordinary decal placement like any other theme, contingent on the open question below of whether
  at-grade paths are even placeable below `Terrain.WaterLevel`.
- **Consequence for this plan's data model**: no changes needed to `PathTile`/`PathRaisedSlope` themselves —
  this confirms the existing raised/at-grade split is the right one to hang a renderer off of, and clarifies
  that "at-grade path rendering" belongs with the terrain mesh/texturing pipeline (a decal pass, likely paired
  with `TerrainCorner.SurfaceIndex` painting), while "raised path rendering" belongs with a piece-model loader
  similar in shape to whatever eventually loads ride track pieces. Both remain out of scope for this plan (see
  below) — this section only records the split so the future rendering plan doesn't have to re-derive it from
  the OVL layout again.

## Open Questions

- Whether at-grade paths are placeable below `Terrain.WaterLevel` at all, or whether an underwater tile is
  always forced into the `Raised` model (using the submerged `WaterSlope*` variant even for a "flat" segment)
  so it always has real geometry instead of a decal sitting under the water plane. Affects whether
  `IsAtGradePathPlaceable` needs a water-level check.

## Explicitly Out of Scope

- **Attachment points** (path tile ↔ ride entrance/exit, path tile ↔ scenery footprint): dropped from this
  plan entirely. Belongs to the ride-placement and scenery-placement plans, once those data models exist.
- **Queue flow/routing logic**: the directional edge flag is stored now; actual capacity/routing/merge
  behavior is deferred to a future ride/gameplay plan.
- **Support-post rendering**: mesh/texture concern, not data model.
- **Path rendering itself** (decal pass for at-grade, piece-model loading for raised — see Rendering Model
  above): both are texture/mesh-loading concerns for a future rendering plan to pick up, not this data-model
  plan.

## Implementation Notes

Implemented in [`PathTile.cs`](../../../OpenRCT3/Simulation/PathTile.cs),
[`PathRaisedSlope.cs`](../../../OpenRCT3/Simulation/PathRaisedSlope.cs), and additions to
[`Park.cs`](../../../OpenRCT3/Simulation/Park.cs); reuses
[`Terrain.GetEdgeCornerHeights`](../../../OpenRCT3/Simulation/Terrain.cs) (added for this plan) and
[`Edge`](../../../OpenRCT3/Simulation/Edge.cs)/[`EdgeExtensions`](../../../OpenRCT3/Simulation/EdgeExtensions.cs)
(renamed from `TerrainEdge` to be reusable here — see `terrain-heightmap.md`'s Implementation Notes).

- **Ownership**: `Park.Paths` is a `Dictionary<(int X, int Y), PathTile>`. No `TileCoord` type exists anywhere
  in the codebase (terrain indexing is raw `(int tileX, int tileY)` parameter pairs), so the dictionary key is
  a plain `(int X, int Y)` value tuple, matching that existing convention rather than introducing a new type.
- **Placement enforcement**: `Park.TryPlacePath`/`IsAtGradePathPlaceable` enforce the <1m at-grade rise
  constraint in the data layer itself (reads `Terrain.GetCorners` and rejects placement if `max - min >=
  AtGradePathMaxRise`), rather than trusting a UI/tool layer — a deliberate divergence from `Terrain`'s own
  permissive raise/lower primitives, decided explicitly for this plan since path placement has a hard legal/
  illegal distinction that terrain sculpting doesn't.
- **Raised-path height/slope encoding**: `PathTile.RaisedHeight` (the low-edge height, in
  `Terrain.HeightStep` units for unit consistency) + `PathRaisedSlope` (`Flat`/`Sloped`/`SteepStair`) +
  `RaisedSlopeDirection` (an `Edge` naming the high side). `PathRaisedSlopeExtensions.RiseInHeightStepUnits()`
  maps `Sloped`/`SteepStair` to 200/400 units (2m/4m). `PathTile.GetRaisedEdgeHeight(Edge)` derives each edge's
  height from these three fields: full rise on the facing edge, none on the opposite edge, half-rise on the
  two side edges. The facing-direction field (`RaisedSlopeDirection`) was a gap the plan's Goals didn't
  cover — added during implementation since connectivity math needs a way to compute per-edge height for a
  sloped raised tile.
- **Connectivity** (`Park.IsPathConnected`): both tiles must have a placed path and agree on `Raised`
  (raised never auto-connects to at-grade, confirmed during planning). At-grade connectivity compares the two
  tiles' shared-edge terrain corner heights via `Terrain.GetEdgeCornerHeights` and passes if the max
  per-corner difference is `<= AtGradePathMaxRise / 2` (0.5m). Raised connectivity passes on an exact
  `GetRaisedEdgeHeight` match across the shared edge — confirmed during planning as "shared edge matches,
  just like at-grade connections," interpreted as exact equality since raised heights are discrete-encoded,
  not continuous terrain.
- **Queue direction**: `PathTile.QueueFlowDirection` is a single `Edge?` (one flow direction per tile, not
  per-edge state) — confirmed during planning once "direction per edge" turned out to mean "the edge this
  queue tile's flow points toward," not independent state on all four edges.
- Removed the stale `// TODO: Tile grid, terrain height data, and ride/path placement models` comment from
  `Park.cs`, which predated `Terrain`'s actual implementation and no longer described a real gap.

Covered by unit tests in
[`OpenRCT3.Tests/Simulation/PathNetworkTests.cs`](../../../OpenRCT3.Tests/Simulation/PathNetworkTests.cs)
(13 tests, passing): at-grade placement accept/reject (steepness, off-grid), raised placement ignoring
terrain steepness, at-grade connectivity at/beyond the half-rise threshold, raised connectivity on
matching/mismatched edge heights, raised-never-connects-to-at-grade, and `GetRaisedEdgeHeight` for both
`Sloped` and `Flat`.

## Status

Implemented: `PathTile`/`PathRaisedSlope` types, `Park.Paths` sparse storage, at-grade placement validation,
and at-grade/raised connectivity, all in `OpenRCT3/Simulation/`. Attachment points and queue flow/routing
remain out of scope per the Goals above, pending the ride/scenery data models.
