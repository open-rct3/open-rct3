// ECS Archetype Primitives
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Buffers;
using System.Collections.Concurrent;

namespace OpenCobra.GDK.ECS;

/// <summary>
/// Archetype-based component storage for components.
/// </summary>
public class Archetype {
  private readonly ConcurrentDictionary<int, IComponent[]> components = [];

  public IEnumerable<IComponent> Components => components.Values.SelectMany(x => x);

  internal static Archetype From<T>(params T[] values) where T : struct, IComponent {
    var result = new Archetype();
    var storage = ArrayPool<T>.Shared.Rent(values.Length);
    values.CopyTo(storage.AsMemory());
    result.Set(storage);
    return result;
  }

  public ref T Get<T>(int index) where T : struct, IComponent => ref GetArray<T>()[index];

  public T[] GetArray<T>() where T : struct, IComponent {
    var key = typeof(T).GetHashCode();
    if (!components.TryGetValue(key, out var data)) return [];
    return data as T[] ?? throw new InvalidOperationException();
  }

  public void Set<T>(T[] data) where T : struct, IComponent {
    var key = typeof(T).GetHashCode();
    components[key] = data as IComponent[] ?? throw new InvalidOperationException();
  }
}
