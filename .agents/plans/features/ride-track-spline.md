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
  function); special/organic pieces allow hand-authored control point/tangent overrides in the editor.
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

## Open Questions

- With a DAG topology, how are junction nodes represented in the data model (branch selection metadata,
  switch-track piece type), and does arc-length addressing need a disambiguator when a piece ID could be
  reached via multiple paths?
- Exact numeric values for the global bake tolerance (gauge fraction + absolute floor) and bank-rate
  threshold — deferred to an implementation-time tuning pass against real piece geometry.

## Status

Not started — stub only, now scoped with a concrete dual-rail spline design and resolved decisions on bake
heuristic, arc-length addressing, IK scope boundary, and tangent authoring. Needs implementation planning
(data structures, file layout, editor authoring workflow) before work begins.
