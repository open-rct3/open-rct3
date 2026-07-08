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

## Open Questions

- Do we model tracked rides as a doubly-linked segment list, or a more general DAG (to support supports/junctions
  later, e.g. multi-train block sections)?
- How much physics (velocity/G-force simulation) belongs in this data model vs. a separate simulation layer?
- Should flat-ride and tracked-ride share a common `Ride` base type now, or diverge until Phase 2 gameplay work
  clarifies the shared surface?

## Status

Not started — stub only. Needs a deeper design pass before implementation.
