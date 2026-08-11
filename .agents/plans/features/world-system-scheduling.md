# World System Scheduling

## Context

**Production weak reference pattern**: All major game engines (Unreal, Bevy, Godot, Unity) follow
the same safety invariant: weak references must be validated and dereferenced atomically at access
time via `TryGetTarget`/`Pin`/`upgrade`, never via separate `IsAlive` checks followed by later
dereference. This plan enforces this pattern via a new `SafeWeakReference<T>` class in `OpenCobra.GDK.Threading`.

The reentrancy bug this plan exists to fix (`World.Load` blocking the WinForms UI thread when
called from inside a render pass — see [`render-loaded-parks.md`](terrain/render-loaded-parks.md)'s
Gap entries) went through two stopgaps, not one:

1. A first attempt, `World.ProcessPendingLoad()`: `ParkChooser.ParkSelected` recorded a
   `pendingParkLoad` string field on `World`, and `Game.Run()`'s loop called
   `World.ProcessPendingLoad()` once per frame to pick it up. This was reverted as too janky
   (a one-off field plus a hand-wired call in `Game.cs`'s loop, coupling `Game` directly to
   `World`'s internal "does it need to reload" state) before this plan was written.
2. **Current stopgap**: `ParkChooser.ParkSelected` is wired directly to
   [`World.ReplaceTerrain(string parkPath)`](../../../OpenRCT3/Simulation/World.cs), which skips
   the reentrancy problem entirely rather than working around it — it hand-loads only `Terrain`
   (via `Terrain.LoadFromSave`, no OVL texture I/O) and swaps just the terrain mesh model, keeping
   the on-thread work small enough not to visibly hang. `Park`/paths/water/scenery/camera
   framing/grass texture are all left stale on park switch, flagged with a `TODO` at the
   `ParkSelected` wiring site in `World.BuildScene`. This is even more minimal than
   `ProcessPendingLoad` was — it doesn't call `World.Load(string?)` at all — and this plan's
   `ParkLoadSystem` design (below) is what would eventually let `ParkSelected` drive a real full
   `World.Load(path)` again, safely.

The user asked for `World` to instead own its own "loaded / needs reload" state, with `Game`
feeding it time via an `Update` method — the same shape [`Scene.Update(delta)`](../../../OpenCobra/GDK/Scene.cs) already has, called
from `Game.Run()`'s loop.

**One relevant thing already exists in the codebase that this plan builds on:**

A much lighter, already-integrated-in-shape-but-not-wired-up system exists on `main` today:
   [`OpenCobra/GDK/Game/ISystem.cs`](../../../OpenCobra/GDK/Game/ISystem.cs) (`Attach`/`Start`/
   `Update(TimeSpan)`/`Stop` lifecycle, a `PipelinePhase Order`, `Parallelizable`),
   [`PipelinePhase.cs`](../../../OpenCobra/GDK/Game/PipelinePhase.cs) (`Early`/`Update`/`Render`/`Late`),
   and a working [`Scheduler.Execute(IEnumerable<ISystem>, TimeSpan)`](../../../OpenCobra/GDK/Game/Scheduler.cs)
   that buckets systems by phase and runs each phase's parallel systems via PLINQ, then its linear
   systems sequentially. `GDK.Game.World`'s base class already has a `Systems` collection and wires
   `Attach`/`Stop` on add/remove via `ObservableCollection.CollectionChanged` - but nothing ever
   adds a system to it, and `Scheduler.Execute` is never called: `Game.cs`'s `Tick(TimeSpan, double)`
   has sat as `// TODO: Scheduler.Execute(delta);` with no implementation. **This plan finishes
   wiring this existing framework and gives it its first real consumer**, rather than building a
   third loading mechanism.

## Goals

- Fix `System.cs`'s doc comment that references `IWorld.IoC`, which doesn't exist on the `IWorld`
  interface. Replace with correct API surface description.
- Add a new `SafeWeakReference<T>` class in `OpenCobra.GDK.Threading` that wraps `WeakReference<T>`
  (composition, since `WeakReference<T>` is sealed) and enforces safe access via `TryGetTarget`,
  preventing the race condition where code checks `IsAlive` separately and then dereferences later.
  Systems should prefer this type over raw `WeakReference<T>` for world/context references.
- Add a defensive guard to `System.Update(TimeSpan delta)` base implementation: check `if (!IsRunning)
  return;` at the top to protect against systems being invoked after stopped.
- Add a way to actually register a system on a `World`. `GDK.Game.World`'s `systems` field is
  `private` with no add/remove method exposed anywhere - replace it with a `HashSet<ISystem>`
  (reference equality, prevents duplicate adds by identity) and add `protected void AddSystem(ISystem
  system)` / `protected void RemoveSystem(ISystem system)` on the base class (mirroring the
  abandoned ECS branch's `AddSystem<TSystem>()` shape, but simpler: takes an instance, not a `new()`
  type param, since this plan doesn't need generic-constructor system registration). `AddSystem`
  calls `Attach` and `Start` in sequence to fully initialize the system for immediate use.
  `AddSystem` returns false if the system was already in the collection (checked via reference
  equality).
- Add `GDK.Game.World.Update(TimeSpan delta)` - a block body (not expression-bodied) that calls
  `Scheduler.Execute(Systems, delta)` and handles exceptions: catches and swallows
  `OperationCanceledException` (matching `Scheduler.Execute`'s behavior), allows `AggregateException`
  to propagate. This is the concrete "entry from the game via an Update method" the user asked for,
  matching `Scene.Update(delta)`'s existing shape.
- Wire `World.Update(delta)` into `Game.Tick(TimeSpan delta, double interpolation)`, replacing the
  `// TODO: Scheduler.Execute(delta);` stub. `Tick` already runs at the fixed simulation timestep,
  potentially multiple times per frame if lagging (`MaxSimulationTicks`) - systems should be
  written expecting that cadence, not "once per rendered frame" (unlike `Scene.Update`, which is
  frame-rate-coupled deliberately, per its own existing call site).
- Add `public static Park Park.Load(string? path)` that synchronously loads and returns a park
  from the given path (extracted from current `World.Load()` logic, which today loads the default
  park).
- Add `public void World.Load(string? path)` that asynchronously invokes `Park.Load(path)` and
  updates the `World.Park` field, using the same `Progress.MeasureTasks` / blocking-wait pattern as
  the current parameterless `World.Load()`. Keep the parameterless `World.Load()` as a thin wrapper
  that calls `World.Load(null)` to load the default park (maintaining API compatibility).
- Remove `World.ProcessPendingLoad()`/`pendingParkLoad`/`Game.cs`'s hand-wired call to it entirely,
  replacing them with a new `ParkLoadSystem : System(PipelinePhase.Early)` owned by
  `OpenRCT3.Simulation.World`:
  - No constructor parameters. `Attach(SafeWeakReference<IWorld> world)` provides the world reference
    (using the new `SafeWeakReference<T>` type from `OpenCobra.GDK.Threading`);
    the system resolves it on each `Update` via `TryGetTarget`, atomically checking and dereferencing
    in a single operation (following production game engine patterns), no-oping if the world has been GC'd.
  - Exposes `public void RequestLoad(string? parkPath)` - what `ParkChooser.ParkSelected` calls
    instead of touching `World`/`pendingParkLoad` directly. Stores the request in an internal
    `private string? pendingParkPath` field (thread-safe via `Interlocked.Exchange`), the same shape
    `pendingParkLoad` had, just now owned by the system instead of `World`.
  - `Update(TimeSpan delta)`: if a load is pending, clears it and calls `World.Load(path)` on the
    resolved weak reference — running in `PipelinePhase.Early`, guaranteed to happen before
    `PipelinePhase.Render` each tick, which is what actually fixes the reentrancy bug (same fix as
    `ProcessPendingLoad`, just via the real systems pipeline instead of a bespoke field+call).
  - `World.Load()` method (not constructor) creates one `ParkLoadSystem` instance and registers it
    via the new `AddSystem` before calling `BuildScene()`. `BuildScene()` wires
    `ParkChooser.ParkSelected` to `parkLoadSystem.RequestLoad` instead of the current
    `path => ReplaceTerrain(path)`. On removal from the systems collection, the base class
    `SystemsChanged` event handler calls `system.Stop()`; on `World.Dispose()`, all systems are
    stopped and disposed before the systems collection is cleared.

## Implementation Order

1. [ ] Create `SafeWeakReference<T>` class in `OpenCobra.GDK.Threading` (enforces atomic TryGetTarget pattern)
2. [ ] Fix `System.cs` doc comment (replaces stale `IWorld.IoC` reference)
3. [ ] Add defensive guard to `System.Update(TimeSpan delta)` base implementation
4. [ ] Replace `systems` field (currently ObservableCollection) with `HashSet<ISystem>` (reference equality
   prevents duplicate adds); add `protected bool AddSystem(ISystem system)` / `protected void RemoveSystem(ISystem system)`
   to `GDK.Game.World` base class. `AddSystem` returns false if system was already in collection.
5. [ ] Add `GDK.Game.World.Update(TimeSpan delta)` method (calls `Scheduler.Execute`)
6. [ ] Wire `World.Update(delta)` into `Game.Tick()`; replace `// TODO: Scheduler.Execute(delta);`
7. [ ] Add `public static Park Park.Load(string? path)` method (extracted from `World.Load()`)
8. [ ] Add `public void World.Load(string? path)` method; wrap parameterless `Load()` to call it
9. [ ] Create `ParkLoadSystem : System(PipelinePhase.Early)` in `OpenRCT3.Simulation.World`:
  - `Attach(WeakReference<IWorld> world)` wraps the world in a `SafeWeakReference` for internal use
  - `Update(TimeSpan delta)` uses `TryGetTarget` atomically before accessing the world
10. [ ] Wire `ParkLoadSystem` in `OpenRCT3.Simulation.World.Load()`: create instance, `AddSystem`,
    wire `BuildScene()` to use `parkLoadSystem.RequestLoad`
11. [ ] Remove `ProcessPendingLoad()`, `pendingParkLoad`, and `Game.cs` call site
12. [ ] Verify `GDK004` ReentrancyAnalyzer correctly detects blocking calls in render-phase code

## Gaps and Risks

1. **`System.cs`'s doc comment references `IWorld.IoC`, which doesn't exist** on the current
   `IWorld` interface (only `Progress`, `Systems`, `Load()`). Pre-existing inconsistency, not
   introduced by this plan - fixed in Goals and Implementation Order above.
2. **PLINQ re-enumeration footgun in Scheduler.Execute.** The current implementation computes
   `parallelSystems` as a deferred `ParallelQuery`, then uses `Except(parallelSystems)` for
   `linearSystems`, which forces re-enumeration of the entire phase bucket for each phase with N > 1
   systems. This is O(N²) per phase but invisible for the single-system test case. **At implementation
   time, materialize `parallelSystems` to a `List` or `HashSet` before the `Except` call** to avoid
   the re-enumeration penalty.
3. **`Scheduler.Execute`'s parallel-system path uses PLINQ with `ForceParallelism`.** `ParkLoadSystem`
   sets `Parallelizable = false` (the `System` base class's default), so this doesn't affect it, but
   it means *any* future parallel system sharing a `PipelinePhase` with a non-parallel one always
   pays PLINQ's setup cost for that phase, even with just one parallel system. Not this plan's
   problem to fix, flagged for whoever adds the first real parallel system.
4. **`Tick` can run more than once per rendered frame** (`MaxSimulationTicks`, lag catch-up).
   `ParkLoadSystem.Update` is idempotent when no load is pending (checks a nullable field, no-ops if
   null) so multiple `Tick`s in one frame don't double-load - confirmed by reading `Scheduler.Execute`/
   `Game.Tick`'s existing loop, not just assumed.
5. **Weak reference safety enforced by SafeWeakReference.** `ParkLoadSystem.Attach` wraps the
   `WeakReference<IWorld>` received from ISystem in a `SafeWeakReference<T>`, ensuring `ParkLoadSystem.Update`
   uses atomic `TryGetTarget` dereference (following production game engine patterns, not separate
   `IsAlive` checks). No-ops if the world has been GC'd. Validated by GDK001 (UnownedReferenceAnalyzer).

## Open Questions

- Should `AddSystem`/`RemoveSystem` be `protected` (only the owning `World` subclass can register
  its own systems, e.g. `OpenRCT3.Simulation.World`'s constructor) or `public` (anything holding a
  `World` reference can add systems to it)? This plan uses `protected` since `ParkLoadSystem` is
  registered by `OpenRCT3.Simulation.World` itself, not by external code — revisit if a future
  system needs external registration (e.g. a debug/dev-console-added system).
- Is the reentrancy bug currently reproducible and active? Deferred to implementation phase; GDK004
  analyzer will catch future violations if systems accidentally call blocking operations in render phase.

## Deferred

- Making the `Progress` bar UI actually async — currently `World.Load()`'s `MeasureTasks().Task.Wait()`
  still blocks on the game loop thread, causing a visible UI freeze during park loads. `Park.Load(string?)`
  is synchronous and `World.Load(string?)` uses the same blocking-wait pattern as today. See TODO in
  `Game.cs` line 122 ("Show a progress bar while loading"). This will be addressed as a separate plan
  once the base systems infrastructure lands.

## Future Work

- **Implement progress bar UI** — Create a loading-screen UI that displays `Progress` while `ParkLoadSystem`
  runs asynchronously. May require making `World.Load` itself truly async, or at minimum moving it off
  the render loop thread. Depends on this plan landing first.
- Other systems that could move into this pipeline (input, camera, water invalidation, etc.) -
  `ParkLoadSystem` is this plan's only new `ISystem`; identifying what else belongs here is future
  work once the pattern has one real example to follow.

## Testing

- `GDK.Game.World.AddSystem`/`RemoveSystem`/`Update`: new unit tests in `OpenCobra/Tests/GDK/` (no
  existing test file covers `World`/`Scheduler` at all - both are currently completely untested,
  per `AGENTS.md`'s coverage rule for `OpenCobra/GDK`). Cases: adding a system calls `Attach` with
  the world's weak reference and `Start` (in sequence); removing calls `Stop`; `Update(delta)`
  invokes every attached system's `Update` with the same `delta` in phase order (`Early` before
  `Update` before `Render` before `Late`); adding the same system twice returns false and doesn't
  double-invoke (HashSet reference equality prevents duplicates).
- `Scheduler.Execute`: currently untested. Cases: systems run in phase order; within a phase,
  parallel systems run via the PLINQ path and linear systems don't; a parallel system throwing
  surfaces as `AggregateException` and is logged; an empty system list no-ops.
- `ParkLoadSystem`: known-good (a `RequestLoad` call followed by one `Update` triggers exactly one
  `World.Load` call with the requested path), edge case (`RequestLoad` called twice before the next
  `Update` - only the latest path should load, matching the current `pendingParkLoad` field's
  last-write-wins semantics), failure case (a `RequestLoad`'d path that doesn't exist - confirm the
  exception surfaces rather than silently no-oping, per Gap #5's note that this isn't specially
  handled), weak reference edge case (`ParkLoadSystem.Update` is called while world is being disposed
  - confirm the system safely handles a null weak reference and no-ops), concurrent threading case
  (two threads call `RequestLoad` concurrently with different paths - confirm last-write-wins via
  `Interlocked.Exchange` semantics, only the final path loads).
- `Game.Tick` calling `World.Update(delta)`: likely awkward to unit test directly (same
  `Game.Instance`/live-context problem `render-loaded-parks.md`'s Testing section already notes for
  `World.Load(parkPath)`) - manual verification via `drive-native-app` (open a park via the chooser,
  confirm no freeze, confirm the scene updates) is the practical coverage here, same as that plan's
  existing `ParkChooser`/`World.Load(parkPath)` entries.
- `GDK004 ReentrancyAnalyzer`: diagnostic cases covering blocking operations (`World.Load`, `Task.Wait`,
  `Progress.MeasureTasks`, `Thread.Sleep`) called from a `System.Update` method in `PipelinePhase.Render`,
  with false-positive checks to confirm the analyzer doesn't flag `Early`/`Update` phase systems or
  non-blocking calls in Render phase.

## Status

Not started. This is a planning-only pass following the `ProcessPendingLoad` fix landing as a
known-janky stopgap.
