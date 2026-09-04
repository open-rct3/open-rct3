---
state: ready
---

# Track Spline Rendering Foundation

## Context

The track-spline data model (rail geometry, baking, and query APIs in local/model space) is complete in the codebase under `OpenRCT3/Rides/TrackSpline/`. This plan unblocks visual validation by integrating rendering and fixing roll continuity in chained track transforms.

**Model-Space Invariant:** Track pieces (and any other game model) only concern themselves with their own model space. Track piece geometry, control points, and baked samples remain in model space. World-space transformation is strictly a rendering layer concern.

**GDK foundation:** This work builds on `OpenCobra.GDK.ImDraw` for visualization (line primitives expanded to screen-space-constant-width quads) and `OpenCobra.GDK.Transform` / `System.Numerics.Matrix4x4` for model transforms. To keep game models isolated in their model space, `ImDraw` is extended with a model transform stack (`PushTransform` / `PopTransform`). Rendering of track geometry uses `ImDraw` as an immediate visual validation aid; full mesh geometry is future work.

Two integration gaps block visual validation:

1. **Rendering layer model transform & visualizer**: `ImDraw` operates on world-space points directly and lacks model matrix support. Introducing `PushTransform` and `PopTransform` in `ImDraw` allows consumers (such as `TrackSplineVisualizer`) to submit samples in native piece model space while applying `TrackChaining`'s `Position`/`Heading`/`Bank` affine transform via matrix composition.
2. **Bank propagation bug**: `TrackChaining.ChainPiece` hardcodes newly-chained piece world-space `Bank` to `0f`, breaking roll continuity in banked sequences (loops, corkscrews). Correctness fix ensures chained pieces have proper orientation.

## Architecture & Seams

1. **ImDraw Model Transform Stack (`OpenCobra.GDK.ImDraw`)**:
   - Maintains a `Stack<Matrix4x4>` of active transforms.
   - `PushTransform(Matrix4x4 transform)` multiplies against the current top matrix (or identity if empty) and pushes the composite matrix.
   - `PopTransform()` pops the top matrix.
   - `Line(Vector3 a, Vector3 b, ...)` transforms endpoints `a` and `b` by the current transform matrix at submission time before constructing vertex quads. Higher-level primitives (`Axis`, `Circle`, `Arrow`) automatically inherit this behavior.
   - `Clear()` resets the transform stack to prevent cross-frame leaks.

2. **Track Piece Model Matrix Composition**:
   - When placing a piece, the model-to-world transform is composed as:
     `M = CreateFromAxisAngle(Vector3.UnitX, piece.Bank) * CreateRotationY(piece.Heading) * CreateTranslation(piece.Position)`
   - The visualizer submits this matrix to `imDraw.PushTransform(M)` and then draws rail samples directly in model space.

3. **TrackSplineVisualizer Panel (`OpenRCT3/UI/TrackSplineVisualizer.cs`)**:
   - Implements `OpenCobra.GDK.GUI.IWindow`.
   - Queries `TrackGraph` from the active ride or scenario editor.
   - Renders left and right rails using `ImDraw.Line()` within a `PushTransform` / `PopTransform` scope for each piece.
   - Registered in `scene.Windows` and toggleable in the editor UI.

## Goals

1. **Fix `TrackChaining.ChainPiece` world-space `Bank` computation**: Derive the newly-chained piece's world `Bank` from the previous piece's exit bank rather than hardcoding `0f`. Add a `TrackChainingTests` case covering banked curve sequences to prevent regression.
2. **Add Model Transform Stack to `OpenCobra.GDK.ImDraw`**: Implement `PushTransform(Matrix4x4)` and `PopTransform()`, transforming vertex positions at submission time in `Line()`. Add unit tests in `OpenCobra.Tests/GDK/ImDrawTests.cs`.
3. **Integrate Track Spline Visualizer (`OpenRCT3/UI/TrackSplineVisualizer.cs`)**: Create a dockable `IWindow` panel that iterates track graph pieces and renders rail spline samples in model space using `ImDraw`. Register with scene windows. Add integration tests verifying transform composition.

## Implementation

0. **Game type hierarchy foundation** — **COMPLETE.** `OpenRCT3/Rides/Ride.cs`,
   `TrackedRide.cs`, `Coaster.cs`, and `TrackPiece.Heartline` already exist in the tree.

1. **Bank propagation fix (Goal 1)**
   - [ ] Add a `GetPieceExitBank(TrackPiece)` helper in `OpenRCT3/Rides/TrackSpline/TrackChaining.cs` reading the last control point or baked sample bank.
   - [ ] Update `TrackChaining.ChainPiece()` to assign `newPiece.Bank = GetPieceExitBank(prevPiece);`.
   - [ ] Add test case `TrackChainingTests.DerivedBankPropagatesInChainedSequence` in `OpenRCT3.Tests/Rides/TrackSpline/TrackChainingTests.cs`.

2. **ImDraw Model Transform Stack (Goal 2)**
   - [ ] Add `Stack<Matrix4x4> transformStack` and `Matrix4x4 currentTransform` to `OpenCobra.GDK.ImDraw`.
   - [ ] Implement `PushTransform(Matrix4x4 transform)` and `PopTransform()`.
   - [ ] Apply `currentTransform` to `a` and `b` in `ImDraw.Line()`.
   - [ ] Clear `transformStack` in `ImDraw.Clear()`.
   - [ ] Add unit tests in `OpenCobra/Tests/GDK/ImDrawTests.cs` verifying single transform, nested transforms, and reset on Clear.

3. **Track Spline Visualizer (Goal 3)**
   - [ ] Create `OpenRCT3/UI/TrackSplineVisualizer.cs` implementing `IWindow`.
   - [ ] Implement piece graph iteration and rail sample rendering within `imDraw.PushTransform(pieceTransform)` scopes.
   - [ ] Wire `TrackSplineVisualizer` into `World.cs` / scene window list and add an editor toggle.
   - [ ] Add integration test in `OpenRCT3.Tests/Rides/TrackSpline/IntegrationTests.cs` verifying that rendering a piece under transform produces expected world-space vertices in `ImDraw`.

## Deferred

- **Track authoring UI**: Editor for hand-authoring organic pieces is valuable but deferred. Procedural pieces + rendering provide sufficient validation surface.
- **Tolerance tuning against real content**: Baking config defaults are provisional until OVL content is imported.
- **Procedural piece geometry refinement**: Catmull-Rom segment count can be tuned once visual validation confirms geometry.
- **Full 3D Mesh Rail Generation**: Transitioning from `ImDraw` lines to extruded mesh tubes/ties is deferred to a subsequent milestone.

## Testing

### GDK-level tests (OpenCobra.Tests)
- `ImDrawTests.PushTransform_TransformsLineEndpoints`: Verifies endpoints `a` and `b` are multiplied by the pushed matrix.
- `ImDrawTests.PushTransform_NestedTransforms_ComposesCorrectly`: Verifies nested push operations concatenate transforms in order.
- `ImDrawTests.PopTransform_RestoresPreviousTransform`: Verifies pop restores parent matrix.
- `ImDrawTests.Clear_ResetsTransformStack`: Verifies transform stack is cleared between frames.

### Game-level tests (OpenRCT3.Tests)
- `TrackChainingTests.DerivedBankPropagatesInChainedSequence`: Verifies that chaining a banked curve or twist onto an existing piece propagates the exit bank.
- `IntegrationTests.RenderingTransformAppliedCorrectlyToBakedSamples`: Verifies track spline visualizer submits rail points through `ImDraw` with correct translation, yaw heading, and roll bank.

## Status

Design updated to preserve model-space isolation for track pieces and game models. Ready for implementation.
