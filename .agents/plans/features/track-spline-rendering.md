---
state: design
dependencies:
  - features/ovl/ovl-track-pieces
---

# Track Spline Rendering Foundation

## Context

The track-spline data model (complete in the archived ride-track-spline plan) defines rail geometry, baking, and query APIs in local/model space. This plan unblocks visual validation by integrating world-space rendering and fixing a correctness bug in chained track transforms.

**GDK foundation:** This work builds on `OpenCobra.GDK.ImDraw` for visualization (line primitives expanded to screen-space-constant-width quads) and `OpenCobra.GDK.Transform` for world-space coordinate systems. Rendering of track geometry uses `ImDraw` as a debug/validation aid first, then transitions to full mesh geometry if performance testing justifies it (deferred).

Parallel work on [OVL track-piece decoding](./ovl/ovl-track-pieces.md) will import real RCT3 content; rendering integration works independently with procedural test pieces first, then supports imported content once decoding is ready.

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
   (see `SplineTypes.Bank` and `TrackChaining.Heading`). This creates a unit-mismatch risk when applying
   track transforms. **Fix:** Amend `Transform` to use radians directly; convert the degree-based API to
   accept radians and document this as a breaking change. This is not a blocker for this plan (rendering
   code can convert radians to degrees when calling Transform methods), but it should be corrected to
   prevent future confusion.

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

0. **Game type hierarchy foundation** (prerequisite for Step 3 integration)
   - [ ] Amend `OpenRCT3/Rides/TrackSpline/SplineTypes.cs` — add to `TrackPiece`:
     ```csharp
     /// <summary>
     /// Baked samples along the heartline of this piece, lazy-computed on first access. 
     /// </summary>
     /// <remarks>
     /// The heartline is the centerline of both rails, inset upward to align with the average rider's heart (middle-torso level), accounting for varied seating positions (sitting, standing, lying down).
     /// </remarks>
     Lazy<BakedSample[]> Heartline { get; }
     ```
   - [ ] Create `OpenRCT3/Rides/Ride.cs` — abstract base class with:
     ```csharp
     /// <summary>Player-facing name of this ride.</summary>
     string Name { get; set; }
     /// <summary>Amount guests pay before entering this ride's queue.</summary>
     decimal Price { get; set; }
     ```
   - [ ] Create `OpenRCT3/Rides/TrackedRide.cs` — abstract subclass of `Ride` with:
     ```csharp
     /// <summary>Track, traversible by this ride's trains.</summary>
     readonly TrackGraph Track;
     /// <summary>Total length of this ride, in meters.</summary>
     float Length => /* Derive length from distance along the whole track's heartline, in meters. */;
     /// <summary>Maximum height of this ride, in meters.</summary>
     float MaxHeight => /* Derive from maximum height of the whole track's heartline, in meters. */;
     ```
   - [ ] Create `OpenRCT3/Rides/Coaster.cs` — concrete subclass of `TrackedRide` with:
     ```csharp
     /// <summary>Total number of inversions of this ride's track.</summary>
     ushort Inversions => /* Derive from the rail splines and 3D trigonometry along the whole track's kength. */;
     ```
   - **Rationale:** TrackSplineVisualizer will render track splines for `TrackedRide` instances. Derived properties consume track-spline query APIs to compute ride statistics from geometry.

1. **GDK improvement (prerequisite, noted in Gaps & Risks)**
   - [ ] Amend `OpenCobra/GDK/Transform.cs` to use radians instead of degrees (breaking change; update all call sites)
   - [ ] Verify Transform methods (`Rotate()`, `RotateX()`, `RotateY()`, `RotateZ()`) accept radians and document the change

2. **Bank propagation fix (Goal 1, prerequisite)**
   - [ ] Fix `TrackChaining.ChainPiece()` (line 66 in `OpenRCT3/Rides/TrackSpline/TrackChaining.cs`) — derive `newPiece.Bank` from `prevExitBank` instead of hardcoding `0f`
   - [ ] Add test case `TrackChainingTests.DerivedBankPropagatesInChainedSequence` covering banked curve chained after straight piece

3. **World-space rendering (Goal 2)** — Implement as dockable IWindow panel (debug/editor-only visualization)
   - [ ] Create `OpenRCT3/UI/TrackSplineVisualizer.cs` — IWindow panel that queries track graph pieces and renders left/right rails using `ImDraw.Line()` with per-piece Position/Heading/Bank transform composition
   - [ ] Register window with the UI controller so it appears in the Windows menu (dockable, toggleable)
   - [ ] Add GDK-level ImDraw test cases (`OpenCobra/Tests/GDK/ImDrawTests.cs` extension) for transform composition
   - [ ] Add game-level integration test (`OpenRCT3/Tests/Rides/TrackSpline/IntegrationTests.cs::RenderingTransformAppliedCorrectlyToBakedSamples`)

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
  after per-piece world-space transform application (Position → Heading → Bank order). Test cases:
  - Single rail segment at origin with identity transform yields expected line vertices
  - Rail segment at non-origin `Position` is translated correctly
  - Rail segment with `Heading` rotation applies yaw correctly (angle between line direction and expected heading)
  - Rail segment with `Bank` rotation tilts roll around the forward axis correctly
  - Combined Position + Heading + Bank transform applies all three in correct composition order

### Game-level tests (OpenRCT3.Tests)

- **World-space transform integration** (`OpenRCT3/Tests/Rides/TrackSpline/IntegrationTests.cs`): Add test case
  `RenderingTransformAppliedCorrectlyToBakedSamples` verifying that a piece with known position/heading/bank
  yields expected world-space baked sample positions when rendered. Transform composition order is
  Position → Heading → Bank (translate, then yaw, then roll). Test:
  - Create a simple procedural piece (straight or curve)
  - Apply known world-space Position/Heading/Bank via `TrackChaining`
  - Query baked samples and manually apply the per-piece transform in the correct order
  - Verify results match expected world coordinates
  - No new algorithmic tests needed; the math is in existing `TrackChaining` logic

- **Bank propagation fix** (`OpenRCT3/Tests/Rides/TrackSpline/TrackChainingTests.cs`): Add
  `DerivedBankPropagatesInChainedSequence` covering a banked curve chained after a straight piece, validating
  that the chained curve's world-space `Bank` matches the straight's exit bank.

## Status

Scope refined. Plan ready for implementation. Parallel work: [OVL track-piece decoding](./ovl/ovl-track-pieces.md)
will import real content; rendering integration works independently with procedural pieces first.
