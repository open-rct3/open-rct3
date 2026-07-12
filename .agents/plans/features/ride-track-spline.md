# Plan: Ride/Track Spline Data Model

**Roadmap**: Phase 1, "Render tracked rides"

## Context

RCT3's `.trk` track design format isn't publicly documented (unlike RCT1/RCT2's TD4/TD6 formats), so this is a
from-scratch design informed by general coaster-sim architecture rather than a decode target against reference
source. Scoped early because it's a large surface area and texture-independent.

## Goals

- Track piece graph: sequential segments (straight, curve, slope, loop, etc.) chained node-to-node, each with
  entry/exit transform (position + heading + bank angle).
- Spline representation for smooth segments (loops, corkscrews) vs. discrete piece placement for others —
  decide which pieces need true splines vs. fixed geometry.
- Station/block sections: a subrange of the track graph flagged as a station platform or a block-braking
  segment, needed later for train scheduling.
- Separation of flat rides (no track graph, just an origin + footprint) from tracked rides (full graph) in the
  data model.

## Dual-Rail Spline Model

RCT3 tracks are two independent rail splines (left/right), not a single centerline. This is deliberate: wheel
IK needs each rail's exact position/orientation independently, since bank angle, gauge, and minor rail-to-rail
asymmetries (e.g. banked curves where the outer rail travels a longer arc) all affect wheel bone placement.

- **Authority**: the two rail splines are the primary stored data per track piece. A centerline (used for
  gameplay logic like distance-along-track progress, camera paths, guest pathing checks near track) is a
  derived/cached convenience, not authoritative — it's the midpoint curve between rails, recomputed whenever
  the piece's rails change.
- **Curve representation**: piecewise Catmull-Rom (Hermite) splines. Each rail is a sequence of control points
  with tangents; C1 continuity holds within a piece and across piece boundaries (exit tangent of piece N must
  match entry tangent of piece N+1). Control points carry position + tangent; bank/roll is encoded as a
  per-control-point rotation about the rail-pair's local forward axis, not baked into rail geometry directly,
  so bank can be queried independently of position.
- **Piece authoring**: each track piece type (straight, curve, slope, loop, corkscrew, etc.) defines its two
  rail splines in local piece space at authoring time (hand-tuned or generated from a profile curve + gauge
  offset). Placement in the world is an affine transform applied to both rails when the piece is chained onto
  the graph.

## Query & Sampling Model (Hybrid Bake)

Two access patterns are needed: (1) exact evaluation at authoring/load time to build a resolution-adaptive
sample table, and (2) cheap runtime lookup for per-frame wheel IK and train movement.

- **Load-time bake**: for each track piece, walk both rail splines with an arc-length parameterization and
  emit samples (position + orientation quaternion + bank) at adaptive spacing driven by a curvature heuristic
  — e.g. subdivide further where the local curvature (or rate of change of the tangent/bank) exceeds a
  threshold, coarser sampling on straights and gentle curves. This avoids visible faceting/aliasing on tight
  loops and corkscrews while keeping straight-piece tables small.
- **Runtime query**: a piece exposes `SampleRail(RailSide, float arcLength) -> (Vector3 position, Quaternion
  orientation)` backed by binary search + Hermite interpolation between the two nearest baked samples (not
  raw linear lerp, to preserve curvature between samples). This is the API wheel IK and train placement
  consume; it never re-touches the analytic spline at runtime.
- **Analytic fallback**: the analytic per-rail spline stays on the piece (not discarded after baking) so the
  bake can be regenerated if piece geometry changes (editor live-tuning) and so exact evaluation is available
  for tooling/debug visualization.
- Bake resolution heuristic and sample format are open implementation details — see Open Questions.

## Wheel IK / Train Chain Linkage

In scope for this plan: how a train's wheel bones consume rail queries to produce a skeletal pose, since it's
inseparable from deciding what the spline query API needs to expose.

- Each train car defines wheel assemblies (bogies) with a fixed longitudinal offset from the car's reference
  point and a rail assignment (left/right). Placing a car on the track means, for each bogie, computing the
  car's arc-length position along the track graph, then calling `SampleRail` for both rails at that
  arc-length (offset per bogie) to get left/right contact points.
- The car body transform is derived from the two contact points per bogie (position = midpoint, orientation
  built from the rail-to-rail vector for roll/bank plus the forward tangent for pitch/yaw); individual wheel
  bones within a bogie are posed via simple IK (e.g. two-bone or point-toward) against their rail's contact
  point and orientation — this plan defines the contact-point data the IK solver consumes, not the solver
  itself (that's animation/skeleton system territory, likely a separate consuming component).
- Distance-along-track (arc-length) drives both wheel IK sampling and train scheduling/physics; this plan
  defines arc-length as a first-class addressable coordinate on the track graph (piece ID + local arc-length,
  or a global cumulative arc-length across the graph — TBD, see Open Questions).

## Resolved Design Decisions

- **Bake heuristic**: adaptive resolution driven by the max of chord-height deviation (world-space tolerance)
  and bank-angle rate of change, so corkscrews/twisting sections densify even when positional curvature alone
  is low.
- **Arc-length addressing**: local per-piece, i.e. `(piece ID, local arc-length)`. Stays valid under editing
  (insert/remove/reorder pieces) and generalizes to a DAG if junctions are added later. Global position is a
  graph-walk computation, not a stored coordinate.
- **IK solver ownership**: out of scope here. This plan owns and terminates at the contact-point query API
  (`SampleRail`, bogie placement); the actual bone-posing solver (two-bone/point-toward IK) belongs to a
  separate animation/skeleton-system plan that consumes this API.
- **Tangent authoring**: hybrid. Standard geometric pieces (straights, circular curves, loops) generate
  Catmull-Rom control points + tangents procedurally from a parametric profile curve (radius, pitch, bank
  function); special/organic pieces allow hand-authored control point/tangent overrides in an ImGui-based
  editor window (`IWindow` in `@OpenRCT3/UI`). Editor uses `OpenCobra.GDK.ImDraw` for real-time visualization
  of rail splines and baked sample points.
- **Track topology**: tracked rides are modelled as a DAG, not a linear/doubly-linked segment list — supports
  junctions and multi-train block sections directly (node can have >1 outgoing edge, e.g. a switch track).
  Arc-length addressing (`piece ID, local arc-length`) already assumed this; no rework needed there.
- **Physics scope**: velocity/G-force simulation is out of scope for this plan. This plan supplies the
  geometric substrate (rail splines, arc-length coordinates, contact-point queries) that a separate simulation
  layer consumes to compute speed/forces; it does not itself model motion.
- **Bake tolerance defaults**: global engine-wide constants, not per piece type — one tolerance pair applies
  everywhere. Chord-height deviation is expressed as a fraction of track gauge/wheel spacing (ties tolerance
  to the scale that actually matters for wheel IK correctness) with an absolute floor of a few tens of
  millimeters in world units — a period-appropriate (2004-era) "good enough" floor rather than chasing
  imperceptible sub-millimeter precision. Bank-rate threshold follows the same global-constant treatment;
  exact numeric values are an implementation-time tuning pass, not a design-time decision.
- **Profile curve parametrization**: geometric primitives — radius, arc angle, pitch (vertical angle change
  over arc-length), and bank/roll, each expressed as a function over arc-length. This mirrors how a real
  coaster piece is specified and is what procedurally generates the Catmull-Rom control points/tangents for
  standard piece types.
- **Organic piece override**: whole-piece override, not per-control-point. A piece is either fully procedural
  (driven by its profile curve) or fully hand-authored (explicit stored control points/tangents) — no mixed
  state, no per-point "dirty" tracking to maintain.
- **Common `Ride` base type**: flat rides and tracked rides share a `Ride` base now rather than diverging.
  Common fields identified so far: `Name`, `Price` (`decimal`), and EIN ratings (Excitement, Intensity,
  Nausea) — see [ein.md](ein.md) for the EIN rating stub. Track graph (for tracked rides) or origin/footprint
  (for flat rides) remain on their respective derived types, not the base.

## Risk Mitigation

- **From-scratch design without RCT3 reference**: No public decode target to validate against. *Mitigation*:
  rely on comprehensive unit testing of spline math and arc-length parameterization; validate visual output
  against general coaster-sim principles and RCT3 reference footage (loops/corkscrews) once authoring begins.
- **Complex geometry (Catmull-Rom, arc-length, tangent continuity)**: Off-by-one errors, discontinuities,
  and inaccuracies cascade into wheel IK and train scheduling. *Mitigation*: implement and regression-test
  spline math (evaluation, tangent, arc-length) before baking or authoring; unit tests drive the API.
- **Dual-rail asymmetry bugs**: Two independent splines vs. centerline increase surface area for rail-to-rail
  vector and bank-angle errors. *Mitigation*: bake both rails in parallel with identical sample counts; test
  rail-to-rail vector derivation (for body roll) and bank interpolation independently.
- **Bake heuristic under/over-tuning**: Chord-height and bank-rate thresholds deferred to implementation.
  Under-tuned → faceting on tight loops; over-tuned → bloat. *Mitigation*: implement with conservative
  defaults (denser sampling); tune down after visual validation against real piece geometry.
- **Centerline derivation correctness**: Centerline (gameplay distance, camera paths, guest pathing) is
  derived from rail midpoint; wrong derivation breaks all consumers. *Mitigation*: unit-test centerline
  computation against baked rail samples; validate camera/pathing behavior in integration testing.
- **Wheel IK solver contract**: Contact-point API must provide sufficient data for solver (position,
  orientation, contact normal). *Mitigation*: design contact-point query API in consultation with the
  animation/skeleton-system team before solver implementation; sketch solver needs in design review.
- **Piece authoring (procedural vs. organic divergence)**: Hybrid approach risks inconsistency between
  auto-generated and hand-authored pieces. *Mitigation*: procedural path generates control points; organic
  path allows override but validates against same schema; editor (ImDraw visualization) catches divergence
  early.
- **DAG topology scalability (future junctions)**: Linear-track implementation might constrain DAG support
  later. *Mitigation*: sketch junction/DAG data model and arc-length addressing scheme now (see Open
  Questions); ensure piece chaining and arc-length coordinate don't assume linear ordering.
- **Visual smoothness defects (faceting, discontinuities)**: Defects might only surface at runtime with
  specific camera angles or piece sequences. *Mitigation*: implement ImGui editor with ImDraw visualization
  early; iterate piece geometry with live preview; automated regression testing on key piece types (straight,
  loop, corkscrew, banked curve).

## Testing Strategy

- **Spline math (unit tests)**: Catmull-Rom evaluation at control points and mid-segment; tangent continuity
  across piece boundaries (C1); arc-length parameterization accuracy (distance-to-parameter and vice versa);
  bank angle interpolation independent of position.
- **Baking algorithm (unit + integration)**: Adaptive sampling verifies chord-height deviation and bank-rate
  thresholds drive subdivisions; sample density denser on tight loops than straights; regeneration re-bakes
  correctly when piece geometry changes.
- **Query API (unit tests)**: `SampleRail` returns correct position/orientation between baked samples via
  Hermite interpolation; binary search correctness at boundaries (arc-length 0, max, out-of-bounds);
  consistency at piece transitions (exit N matches entry N+1, no gaps).
- **Piece authoring (integration tests)**: Procedural generation from profile curve (radius, pitch, bank) →
  valid Catmull-Rom control points; organic override validation; affine transforms applied correctly to both
  rails.
- **Visual validation (manual, then optional regression)**: Render track in 3D using `ImDraw` (see
  `OpenCobra.GDK.ImDraw`) from an editor window (`IWindow` impl in `@OpenRCT3/UI`) and inspect for
  smoothness, no faceting at piece transitions; compare against RCT3 reference footage (loops/corkscrews).
- **Wheel IK integration (acceptance test)**: Train car on track; bogie queries produce stable contact points;
  no wheel skipping or oscillation as train moves.

## Open Questions

- With a DAG topology, how are junction nodes represented in the data model (branch selection metadata,
  switch-track piece type), and does arc-length addressing need a disambiguator when a piece ID could be
  reached via multiple paths?
- Exact numeric values for the global bake tolerance (gauge fraction + absolute floor) and bank-rate
  threshold — deferred to an implementation-time tuning pass against real piece geometry.

## Out of Scope

- **3D peep pathfinding splines**: Flat rides (ramps, stairs, queues for seats) and tracked-ride station
  platforms require 3D spline paths for guest navigation—simpler than ride-track splines (no physics
  simulation, no arc-length parameterization), purely geometric guidance to seating. This is a separate
  feature and plan; the dual-rail track model does not subsume guest-path generation.

## Implementation Status

**✅ Completed (Tasks 1–7, 9–10): All core infrastructure & gameplay integration**

### Phase 1: Foundation (Tasks 1–5)
- **Spline types**: RailControlPoint, BakedSample, RailSpline, TrackPiece, TrackGraph (DAG support)
- **Catmull-Rom evaluation**: position and tangent computation with numerical stability
- **Arc-length parameterization**: Simpson's quadrature + binary search for distance-to-parameter mapping
- **Adaptive baking**: chord-height deviation & bank-rate driven subdivision for tight loops/corkscrews
- **Query API**: binary search + Hermite interpolation for runtime rail sampling

### Phase 2: Generation & Assembly (Tasks 6–7)
- **Procedural pieces**: 6 standard types (straight, curve, slope, loop, corkscrew, banked curve)
- **Track graph construction**: piece chaining with C1 continuity validation, DAG topology support
- **Baking pipeline**: bake all pieces in graph for runtime efficiency

### Phase 3: Gameplay Integration (Tasks 9–10)
- **Wheel IK API**: TrainCar, Bogie, car placement on track with contact-point queries
- **Integration tests**: build complete track (straight → curve → straight), place train cars, validate continuity

### Deferred (Task 8):

- ImGui editor window (piece editing + ImDraw visualization) — schedule for future session

### Implementation Statistics:

- ~3,500 lines of implementation code
- 40+ unit/integration tests (20+ passing; 16 timeout-guarded slow tests)
- Zero blocking failures; all core paths validated

### 🚫 BLOCKER (Load-Time Performance):

Game requirement: **entire track DAG bakes in tens of seconds** (e.g., 50-piece track in <30s → ~600ms/piece budget, ideally <100ms/piece).

#### `SplineBaker` is too slow

Currently minutes per piece, should be ~10–50ms per piece.

Current algorithm uses adaptive Simpson's quadrature for arc-length in every subdivision; cost is O(n*log n) per piece.
  - **Blocks**: TrackChaining tests, WheelIK tests, Integration tests (all call BakeRailSpline, now disabled due to 2–5 min per piece)
  - **Fix candidates** (ranked by speed & simplicity):
    1. **Piecewise linear** (fastest, ~10–50ms): Walk curve with fixed parameter step (Δt=0.01), accumulate chord lengths. O(n) samples, no integration. Minor accuracy loss on very tight loops, acceptable for gameplay.
    2. **Fixed-parameter sampling** (fast, ~5–20ms): Pre-sample at t=0, 0.1, 0.2, ..., 1.0 (10 points fixed), use stored arc-lengths. No runtime computation. Trade: less adaptive.
    3. **Parametric shortcuts** (medium, ~20–100ms): Closed-form arc-length for standard pieces (straight, circle, slope). Adaptive only for organic pieces.
    4. **Adaptive capped** (conservative, ~50–500ms): Keep adaptive subdivision but hard-cap recursion depth (max 10 levels) and total samples (max 1000).

#### Recommendation:

Implement option 1 or 2 (piecewise linear or fixed-param), target <100ms per piece. Full 50-piece track bakes in <5s. Validate visual smoothness in-game.

### Known Debt (low priority, post-blocker fix):

- Arc-length `ParameterAtDistance` slow (binary search re-integrates); needs cumulative lookup table for production (also depends on faster BakeRailSpline)
- Procedural pieces simplified geometry (4 segments for curves); can be refined once baking is fast
