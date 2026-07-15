# World System Scheduling

## Context

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
feeding it time via an `Update` method — the same shape `Scene.Update(delta)` already has, called
from `Game.Run()`'s loop.

**Two relevant things already exist in the codebase that this plan builds on, not around:**

1. **A full ECS was already scaffolded and explicitly rejected.** Branch `prototype/ecs/the-whole-enchilada`,
   commit `3ccc681` ("Scaffold out a full ECS") — the commit message itself reads "Overkill! 🙁".
   That branch's `OpenCobra/GDK/ECS/` (`Entity`/`Component`/`Archetype`/`Query`/full `IWorld` with
   `Set<T>`/`Get<T>`/`Has<T>`/archetype migration) is unfinished (several methods are stubs per its
   own `.agents/plans/ecs-system.md`) and predates most of current `main`'s features - its
   `Game.cs` still says `"Simulation features are unimplemented!"` and has no `ParkChooser`,
   `Editor`, terrain loading, or any of the reverse-engineering work `render-loaded-parks.md`
   landed. **This plan does not adopt it.**
2. **A much lighter, already-integrated-in-shape-but-not-wired-up system exists on `main` today**:
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

- Add a way to actually register a system on a `World`. `GDK.Game.World`'s `systems` field is
  `private` with no add/remove method exposed anywhere - add `protected void AddSystem(ISystem
  system)` / `protected void RemoveSystem(ISystem system)` on the base class (mirroring the
  abandoned ECS branch's `AddSystem<TSystem>()` shape, but simpler: takes an instance, not a `new()`
  type param, since this plan doesn't need generic-constructor system registration).
- Add `GDK.Game.World.Update(TimeSpan delta) => Scheduler.Execute(Systems, delta);` - a thin
  wrapper, but this is the concrete "entry from the game via an Update method" the user asked for,
  matching `Scene.Update(delta)`'s existing shape.
- Wire `World.Update(delta)` into `Game.Tick(TimeSpan delta, double interpolation)`, replacing the
  `// TODO: Scheduler.Execute(delta);` stub. `Tick` already runs at the fixed simulation timestep,
  potentially multiple times per frame if lagging (`MaxSimulationTicks`) - systems should be
  written expecting that cadence, not "once per rendered frame" (unlike `Scene.Update`, which is
  frame-rate-coupled deliberately, per its own existing call site).
- Remove `World.ProcessPendingLoad()`/`pendingParkLoad`/`Game.cs`'s hand-wired call to it entirely,
  replacing them with a new `ParkLoadSystem : System(PipelinePhase.Early)` owned by
  `OpenRCT3.Simulation.World`:
  - Constructed with a reference to the owning `World` (concrete type, not just `IWorld`, since it
    needs to call `World.Load(string?)` - `Attach(WeakReference<IWorld> world)`'s weak reference is
    still respected: the system stores the weak reference, not a strong one, resolving it each
    `Update`).
  - Exposes `public void RequestLoad(string? parkPath)` - what `ParkChooser.ParkSelected` calls
    instead of touching `World`/`pendingParkLoad` directly. Stores the request in an internal
    field, the same shape `pendingParkLoad` had, just now owned by the system instead of `World`.
  - `Update(TimeSpan delta)`: if a load is pending, clears it and calls `World.Load(path)` -
    running in `PipelinePhase.Early`, guaranteed to happen before `PipelinePhase.Render` each tick,
    which is what actually fixes the reentrancy bug (same fix as `ProcessPendingLoad`, just via the
    real systems pipeline instead of a bespoke field+call).
  - `World`'s constructor (or `BuildScene`, on first call) registers one `ParkLoadSystem` instance
    via the new `AddSystem`, and `ParkChooser.ParkSelected` is wired to `parkLoadSystem.RequestLoad`
    instead of the current `path => pendingParkLoad = path`.
- `World.Load(string?)` itself is unchanged - still synchronous/blocking. This plan only changes
  *who* calls it and *when* within the frame, not the loading mechanism's synchronicity (see
  Deferred).

## Gaps and Risks

1. **`System.cs`'s doc comment references `IWorld.IoC`, which doesn't exist** on the current
   `IWorld` interface (only `Progress`, `Systems`, `Load()`). Pre-existing inconsistency, not
   introduced by this plan - worth a one-line fix while touching this file, but not a blocker.
2. **`Scheduler.Execute`'s parallel-system path uses PLINQ with `ForceParallelism`.** `ParkLoadSystem`
   sets `Parallelizable = false` (the `System` base class's default), so this doesn't affect it, but
   it means *any* future parallel system sharing a `PipelinePhase` with a non-parallel one always
   pays PLINQ's setup cost for that phase, even with just one parallel system. Not this plan's
   problem to fix, flagged for whoever adds the first real parallel system.
3. **`Tick` can run more than once per rendered frame** (`MaxSimulationTicks`, lag catch-up).
   `ParkLoadSystem.Update` is idempotent when no load is pending (checks a nullable field, no-ops if
   null) so multiple `Tick`s in one frame don't double-load - confirmed by reading `Scheduler.Execute`/
   `Game.Tick`'s existing loop, not just assumed.

## Open Questions

- Should `AddSystem`/`RemoveSystem` be `protected` (only the owning `World` subclass can register
  its own systems, e.g. `OpenRCT3.Simulation.World`'s constructor) or `public` (anything holding a
  `World` reference can add systems to it)? This plan uses `protected` since `ParkLoadSystem` is
  registered by `OpenRCT3.Simulation.World` itself, not by external code — revisit if a future
  system needs external registration (e.g. a debug/dev-console-added system).
- Whether `Scheduler.Execute`'s `AggregateException`/`OperationCanceledException` handling (logs
  and rethrows/swallows respectively) is the right behavior for `ParkLoadSystem`'s errors
  specifically (e.g. a corrupt/missing `.dat` file mid-`Park.Load`) - not addressed by this plan;
  `ParkLoadSystem.Update` currently lets any exception from `World.Load` propagate up through
  `Scheduler.Execute` unhandled, same as every other system would.

## Deferred

- Making `World.Load(string?)` itself non-blocking/truly async (the pre-existing FIXME in
  `World.cs`, and the "Make `World.Load` truly async" option not chosen when this plan was
  scoped) - this plan only fixes *when on the frame timeline* the blocking call happens, not the
  blocking itself. A loading-screen UI that polls `Progress` while `ParkLoadSystem` loads in the
  background is a natural follow-on once this lands, not required for it.
- Adopting more of the abandoned ECS branch's ideas (entities/components/archetypes/queries) if a
  real need for them shows up later - this plan deliberately keeps `OpenRCT3.Simulation.World`'s
  existing `Park`/`Terrain` properties as plain fields, not components, since nothing here needs
  per-entity component storage.
- Other systems that could move into this pipeline (input, camera, water invalidation, etc.) -
  `ParkLoadSystem` is this plan's only new `ISystem`; identifying what else belongs here is future
  work once the pattern has one real example to follow.

## Testing

- `GDK.Game.World.AddSystem`/`RemoveSystem`/`Update`: new unit tests in `OpenCobra/Tests/GDK/` (no
  existing test file covers `World`/`Scheduler` at all - both are currently completely untested,
  per `AGENTS.md`'s coverage rule for `OpenCobra/GDK`). Cases: adding a system calls `Attach` with
  the world's weak reference and `Start`; removing calls `Stop`; `Update(delta)` invokes every
  attached system's `Update` with the same `delta` in phase order (`Early` before `Update` before
  `Render` before `Late`); a system added twice isn't double-invoked (or is, if that's the decided
  behavior - currently unspecified, worth pinning down with a test either way).
- `Scheduler.Execute`: currently untested. Cases: systems run in phase order; within a phase,
  parallel systems run via the PLINQ path and linear systems don't; a parallel system throwing
  surfaces as `AggregateException` and is logged; an empty system list no-ops.
- `ParkLoadSystem`: known-good (a `RequestLoad` call followed by one `Update` triggers exactly one
  `World.Load` call with the requested path), edge case (`RequestLoad` called twice before the next
  `Update` - only the latest path should load, matching the current `pendingParkLoad` field's
  last-write-wins semantics), failure case (a `RequestLoad`'d path that doesn't exist - confirm the
  exception surfaces rather than silently no-oping, per Gap #3's note that this isn't specially
  handled).
- `Game.Tick` calling `World.Update(delta)`: likely awkward to unit test directly (same
  `Game.Instance`/live-context problem `render-loaded-parks.md`'s Testing section already notes for
  `World.Load(parkPath)`) - manual verification via `drive-native-app` (open a park via the chooser,
  confirm no freeze, confirm the scene updates) is the practical coverage here, same as that plan's
  existing `ParkChooser`/`World.Load(parkPath)` entries.

## Status

Not started. This is a planning-only pass following the `ProcessPendingLoad` fix landing as a
known-janky stopgap.
