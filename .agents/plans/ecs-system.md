# OpenCobra GDK System Design

## Status

ECS types exist in `OpenCobra/GDK/ECS/`:
- `Entity.cs`: `Entity` record with auto-incrementing IDs, `Null` static property
- `Component.cs`: `IComponent` interface and `Component` struct (no-op disposal)
- `ISystem.cs`: `ISystem` interface with `Attach`, `Start`, `Update`, `Stop`; `System` abstract base; `SystemOrder` enum; `IScheduler` interface
- `IWorld.cs`: `IWorld` interface and abstract `World` base class
- `Query.cs`: Stub `Query<T1, T2>` ref struct (does not compile)
- `Archetype.cs`: Static class with stub `GetArray<T>()` — **archetype storage is broken**

## Problems to Solve

1. **Static storage in `World` and `Archetype`**: Both use `static` fields — breaks multi-world scenarios
2. **Static `Archetype` class**: Archetypes must be instance-based; one archetype per unique component signature
3. **`Memory<IComponent>` cannot return refs**: `Span<IComponent>` returns copies of boxed references — cannot do `ref return`
4. **`Set<T>` / `Has<T>` / `Remove<T>`**: Methods are empty stubs
5. **`Query<T>`**: Stub that references non-existent `_world.Query<T1,T2>()` method
6. **`World.Set(Entity, string)`**: Entity naming exists but unused
7. **`IScheduler`**: Interface exists but no implementation
8. **Thread safety**: No locking around entity/component operations for parallel systems

## Architecture

### Entity

```csharp
public readonly record struct Entity {
    private static uint lastId = 0;
    public static Entity Null => new(0);

    public readonly uint Id;

    public Entity() => Id = lastId++;
    public Entity(uint id) => Id = id;

    public bool IsNull => Id == 0;
    public override string ToString() => $"{Id}";
}
```

### Component

```csharp
public interface IComponent : IDisposable { }

public readonly struct Component : IComponent {
    public void Dispose() => GC.SuppressFinalize(this);
}
```

### ISystem / System

```csharp
public interface ISystem : IDisposable {
    bool Parallelizable { get; }

    void Attach();      // Called when added to world — world reference available
    void Start();        // Called once before first Update
    void Update(TimeSpan delta);
    void Stop();
}

public enum SystemOrder : int {
    Early = -1,   // Input, physics
    Normal = 0,   // Game logic
    Late = 1,     // Rendering, GUI
}

public abstract class System : ISystem {
    public bool Parallelizable { get; protected set; } = false;

    public virtual void Attach() { }
    public virtual void Start() { }
    public virtual void Update(TimeSpan delta) { }
    public virtual void Stop() { }
    public virtual void Dispose() => GC.SuppressFinalize(this);
}
```

### IWorld

```csharp
public interface IWorld : IDisposable {
    IDictionary<Entity, Memory<IComponent>> Entities { get; }
    IEnumerable<ISystem> Systems { get; }
    Progress Progress { get; }

    void Set(Entity key, string name);
    void Set<T>(Entity key, T component) where T : struct;
    ref T Get<T>(Entity key) where T : struct;
    bool Has<T>(Entity key) where T : struct;
    void Remove<T>(Entity key) where T : struct;
    void Destroy(Entity key);

    void AddSystem<TSystem>() where TSystem : System, new();
    void RemoveSystem<TSystem>() where TSystem : System;

    void Update(TimeSpan delta);
}
```

### IScheduler

```csharp
public interface IScheduler {
    void Add(System system, SystemOrder order = SystemOrder.Normal);
    void Execute(TimeSpan delta);
}
```

## Implementation Tasks

### 1. Fix World Storage

Remove `static` keyword from `entities`, `entityNames`, `systems` in `World` base class. Instance storage is required for proper multi-world support.

### 2. Implement Archetype-Based Component Storage

**Why `Memory<IComponent>` fails**: `Span<IComponent>` returns **copies** of boxed references, not refs to underlying storage. Cannot do `ref return IComponent`.

**Solution**: Each archetype stores components in **typed arrays**, enabling `ref return T`:

```csharp
// Component storage per archetype (archetype = unique component signature per entity)
public sealed class Archetype {
    private readonly int _entityCount;
    private readonly Dictionary<Type, (int stride, IMemoryOwner<byte> owner)> _componentArrays;

    public int EntityCount => _entityCount;

    public Archetype(params Type[] componentTypes) {
        _componentArrays = [];
        foreach (var type in componentTypes) {
            var stride = Marshal.SizeOf(type);
            _componentArrays[type] = (stride, MemoryPool<byte>.Shared.Rent(stride * 64));
        }
    }

    public int AddEntity() => _entityCount++;

    public Span<T> GetArray<T>() where T : struct, IComponent {
        var (_, owner) = _componentArrays[typeof(T)];
        return MemoryMarshal.Cast<byte, T>(owner.Memory.Span);
    }

    public ref T Get<T>(int entityIndex) where T : struct, IComponent {
        return ref GetArray<T>()[entityIndex];
    }
}
```

**Entity → Archetype mapping** (in `World`):
```csharp
private readonly Dictionary<Entity, Archetype> _entityArchetypes = [];
private readonly List<Archetype> _archetypes = [];
```

**For `Get<T>`**: Look up entity's archetype, get typed array for `T`, return `ref array[index]`.

### 3. Implement `Set<T>` / `Has<T>` / `Remove<T>`

1. Look up entity's archetype
2. If component type not in archetype → create new archetype, migrate entity
3. `Set<T>`: call `archetype.Get<T>(index) = component`
4. `Has<T>`: check if entity's archetype has `T`
5. `Remove<T>`: create new archetype without `T`, migrate entity

### 4. Implement `Query<T1, T2>`

```csharp
public ref struct Query<T1, T2> where T1 : struct where T2 : struct {
    private readonly World _world;
    private readonly Archetype _archetype;

    internal Query(World world, Archetype archetype) => (_world, _archetype) = (world, archetype);

    public Enumerator GetEnumerator() => new(_archetype);

    public ref struct Enumerator {
        private int _index;
        public Entity Current => _archetype.Entities[_index];
        public bool MoveNext() => ++_index < _archetype.EntityCount;
    }
}
```

### 5. Implement `IScheduler`

```csharp
class Scheduler : IScheduler {
    private readonly List<System>[] _buckets = [[], [], []];

    public void Add(System system, SystemOrder order = SystemOrder.Normal) {
        _buckets[(int)order].Add(system);
    }

    public void Execute(TimeSpan delta) {
        foreach (var bucket in _buckets) {
            foreach (var system in bucket) {
                if (system is ParallelSystem ps) {
                    // Schedule on thread pool
                    Task.Run(() => ps.Update(delta));
                } else {
                    system.Update(delta);
                }
            }
        }
    }
}
```

### 6. Thread Safety

Add `ConcurrentDictionary` for entity->archetype lookup, or use reader-writer lock around archetype access during parallel iteration.

## File Structure

```
OpenCobra/GDK/ECS/
├── Component.cs       ✓ exists
├── Entity.cs          ✓ exists
├── ISystem.cs         ✓ exists
├── IWorld.cs          ✓ partial (storage bug, stubs)
├── Query.cs           ✓ partial (stub)
├── Archetype.cs       ⬅ broken (static, non-functional)
├── Scheduler.cs       ⬅ new
└── World.cs           ⬅ fix static storage, implement methods

OpenRCT3/Simulation/
├── World.cs           ← extends GDK.World with Park, Terrain
├── Park.cs
└── Terrain.cs
```

## Open Questions

- Should `World` support entity hierarchies (parent/child relationships)?
- Do we need component tag types (empty components) vs. data components?
- Should systems declare required components as a query, or iterate explicitly?

## Alternatives Considered

- **EnTT-style single registry**: Simpler but less flexible for parallel iteration
- **Full Flecs port**: Too heavy; archetype/relationship features aren't needed yet
- **Unity JobSystem-style**: Burst compilation useful but adds complexity