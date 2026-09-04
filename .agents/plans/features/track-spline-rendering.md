---
state: design
---

# Track Spline Rendering Foundation

## Context

The track-spline data model (rail geometry, baking, and query APIs in local/model space) is complete in the codebase under `OpenRCT3/Rides/TrackSpline/`. This plan unblocks visual validation by integrating world-space rendering and fixing a correctness bug in chained track transforms.

**GDK foundation:** This work builds on `OpenCobra.GDK.ImDraw` for visualization (line primitives expanded to screen-space-constant-width quads) and `OpenCobra.GDK.Transform` for world-space coordinate systems. Rendering of track geometry uses `ImDraw` as a debug/validation aid first, then transitions to full mesh geometry if performance testing justifies it (deferred).

OVL track-piece decoding (its plan is archived; see `OpenCobra/OVL/Files/TrackData.cs` and `TODO.md`) will import real RCT3 content. Rendering integration works independently with procedural test pieces first, then supports imported content once decoding is ready.

Two integration gaps block visual validation:

1. **World-space rendering integration**: `BakedSample` positions stay in local space; applying
   `TrackChaining`'s world `Position`/`Heading`/`Bank` transform is the rendering pipeline's responsibility.
   This completes the "render tracked rides" milestone and unblocks visual validation with existing procedural pieces.
   Render using `ImDraw.Line()` to visualize left/right rails as immediate-mode line primitives.
2. **Bank propagation bug**: `TrackChaining.ChainPiece` hardcodes newly-chained piece world-space `Bank` to
   `0f`, breaking roll continuity in banked sequences (loops, corkscrews). Correctness fix after rendering works.

## Gaps and Risks

1. **GDK Transform class uses degrees instead of radians** — `OpenCobra.GDK.Transform` accepts degrees in
   `Rotate()`, `RotateX()`, etc., converting to radians internally. Track splines use radians natively
   (see `SplineTypes.Bank` and `TrackChaining.Heading`). **Out of scope for this plan (YAGNI).** Goal 2
   composes the world transform manually and feeds `ImDraw.Line()` world-space points, so `Transform` is
   not on the rendering path here. The rendering layer converts radians to degrees at the call site when
   it does touch `Transform`. A breaking radians migration of `Transform`, if wanted, is a standalone
   framework change with its own call-site audit.

2. **World-transform seam is undefined (open question)** — applying a piece's world Position/Heading/Bank
   to its local `BakedSample` data has no single home today. `TrackedRide.Center` walks the graph but does
   not apply the transform, though `SplineTypes` (`TrackPiece` remarks) states that applying it is the
   render pipeline's job. Options: (a) a shared helper on the track-spline layer (e.g.
   `TrackPiece.WorldSamples()` or an extension in `Rides/TrackSpline/`) consumed by both
   `TrackSplineVisualizer` and `TrackedRide.Center`; (b) inline in `TrackSplineVisualizer`. Resolve before
   implementing Goal 2.

## Goals

1. **Fix `TrackChaining.ChainPiece` world-space `Bank` computation**: Derive the
   newly-chained piece's world `Bank` from the previous piece's exit bank rather than hardcoding `0f`. Add a
   `TrackChainingTests` case covering banked curve sequences to prevent regression. (Prerequisite: correct
   geometry before rendering it.)
2. **Integrate world-space transform into the rendering pipeline**: Apply `TrackChaining`'s
   `Position`/`Heading`/`Bank` to `BakedSample` data **per piece** (not per-sample), composed in order:
   translate by `Position`, rotate by `Heading` (yaw about world-up axis), then rotate by `Bank` (roll about
   the piece's forward tangent axis). This is glue code connecting the track-spline data model to actual frame
   output — scope is "make rendered tracks match their authored position/heading/bank," not full-scene rendering.
   Render using `ImDraw.Line()` to visualize left/right rails as immediate-mode line primitives.

## Implementation

0. **Game type hierarchy foundation** — **COMPLETE.** `OpenRCT3/Rides/Ride.cs`,
   `TrackedRide.cs` (with `Length`, `MaxHeight`, `Center`), `Coaster.cs` (with `Inversions` stub), and
   `TrackPiece.Heartline` (stub) already exist in the tree. Goal 2 renders `LeftRail`/`RightRail.BakedSamples`
   directly, so the `Heartline` and `Inversions` stubs are not on this plan's critical path. Any stats display
   reads `TrackedRide.Length` / `.MaxHeight`; it does not recompute them.

1. **Bank propagation fix (Goal 1, prerequisite)**
   - [ ] Fix `TrackChaining.ChainPiece()` in `OpenRCT3/Rides/TrackSpline/TrackChaining.cs` — the
     `newPiece.Bank = 0f; // TODO: derive from piece geometry` line. Derive `newPiece.Bank` from the previous
     piece's exit bank.
   - [ ] Add a `GetPieceExitBank(TrackPiece)` private accessor beside the existing `GetPieceExitPosition` /
     `GetPieceExitTangent` helpers, reading the last `BakedSample.Bank` of a rail.
   - [ ] Add test case `TrackChainingTests.DerivedBankPropagatesInChainedSequence` covering a banked curve
     chained after a straight piece (extend `OpenRCT3.Tests/Rides/TrackSpline/TrackChainingTests.cs`).

2. **World-space rendering (Goal 2)** — Implement as dockable IWindow panel (debug/editor-only visualization)
   - [ ] Resolve the world-transform seam (Gaps & Risks 2) before writing the visualizer.
   - [ ] Create `OpenRCT3/UI/TrackSplineVisualizer.cs` — IWindow panel that queries track graph pieces and
     renders left/right rails using `ImDraw.Line()`. It calls the shared world-transform helper (or the graph
     walk on `TrackedRide`), not a private re-implementation of graph traversal.
   - [ ] Register window with the UI controller so it appears in the Windows menu (dockable, toggleable)
   - [ ] Add GDK-level ImDraw test cases (`OpenCobra/Tests/GDK/ImDrawTests.cs` extension) for transform composition
   - [ ] Extend `OpenRCT3.Tests/Rides/TrackSpline/IntegrationTests.cs` with
     `RenderingTransformAppliedCorrectlyToBakedSamples`

## Deferred

- **Track authoring UI**: Editor for hand-authoring organic pieces is valuable but deferred. Procedural pieces
  + world-space rendering provide sufficient validation surface for this foundation phase. Editor can follow
  once real OVL content is imported and geometry needs tuning/validation.
- **Tolerance tuning against real content**: the baking config defaults are provisional. A data-driven tuning
  pass will follow once OVL content is imported and geometry can be benchmarked.
- **Procedural piece geometry refinement**: currently 4 Catmull-Rom segments per curve/corkscrew. Can be
  densified once baking is fast enough; deferred until visual validation confirms the current geometry is
  correct.
- **ImGui inspector for train/bogie placement**: placing cars on the track and viewing their IK queries is
  valuable but separate; it's a consumer of the track model, not part of this foundation.

## Testing

### GDK-level tests (OpenCobra.Tests)

- **ImDraw track spline visualization** (`OpenCobra/Tests/GDK/ImDrawTests.cs` extension or new test): Add
  tests verifying that `ImDraw.Line()` primitives correctly render left/right rail `BakedSample` positions
  after per-piece world-space transform application (Position → Heading → Bank order). These assert against
  the shared world-transform helper chosen for Gaps & Risks 2, not against `TrackSplineVisualizer`
  internals; the visualizer stays a thin `ImDraw.Line()` caller. Test cases:
  - Single rail segment at origin with identity transform yields expected line vertices
  - Rail segment at non-origin `Position` is translated correctly
  - Rail segment with `Heading` rotation applies yaw correctly (angle between line direction and expected heading)
  - Rail segment with `Bank` rotation tilts roll around the forward axis correctly
  - Combined Position + Heading + Bank transform applies all three in correct composition order

### Game-level tests (OpenRCT3.Tests)

- **World-space transform integration** (`OpenRCT3.Tests/Rides/TrackSpline/IntegrationTests.cs`): Add test case
  `RenderingTransformAppliedCorrectlyToBakedSamples` verifying that a piece with known position/heading/bank
  yields expected world-space baked sample positions when rendered. Transform composition order is
  Position → Heading → Bank (translate, then yaw, then roll). Test:
  - Create a simple procedural piece (straight or curve)
  - Apply known world-space Position/Heading/Bank via `TrackChaining`
  - Query baked samples and apply the per-piece transform via the shared world-transform helper (Gaps & Risks 2)
  - Verify results match expected world coordinates
  - No new algorithmic tests needed; the math is in existing `TrackChaining` logic. If the helper is added,
    its unit coverage lives with it, and this test exercises the piece-to-world path end to end.

- **Bank propagation fix** (`OpenRCT3.Tests/Rides/TrackSpline/TrackChainingTests.cs`): Add
  `DerivedBankPropagatesInChainedSequence` covering a banked curve chained after a straight piece, validating
  that the chained curve's world-space `Bank` matches the straight's exit bank.

## Status

Review applied. Step 0 complete. Transform radians migration dropped as YAGNI. World-transform seam is an
open question (Gaps & Risks 2) to resolve before Goal 2. Remaining work: Goal 1 bank fix, then Goal 2
rendering. Parallel work: OVL track-piece decoding will import real content; rendering integration works
independently with procedural pieces first.
