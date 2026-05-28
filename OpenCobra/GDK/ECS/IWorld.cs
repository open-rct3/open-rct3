// ECS World Primitives
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using CommunityToolkit.HighPerformance;
using NLog;
using OpenCobra.GDK.Streaming;

namespace OpenCobra.GDK.ECS;

public interface IWorld : IDisposable {
  IDictionary<Entity, Archetype> Entities { get; }
  IEnumerable<ISystem> Systems { get; }
  Progress Progress { get; }

  public void Set(Entity key, string name);
  void Set<T>(Entity key, T component) where T : struct, IComponent;
  ref T Get<T>(Entity key) where T : struct, IComponent;
  bool Has<T>(Entity key) where T : struct, IComponent;
  void Remove<T>(Entity key) where T : struct, IComponent;
  void Destroy(Entity key);

  void Add<TSystem>() where TSystem : System, new();
  void Remove<TSystem>() where TSystem : System;

  void Update(TimeSpan delta);
}

/// <summary>
/// Represents a game's world.
/// </summary>
public abstract class World : IWorld {
  private readonly static Logger logger = LogManager.GetCurrentClassLogger();
  private static readonly Dictionary<Entity, Archetype> entities = [];
  private static readonly Dictionary<Entity, string> entityNames = [];
  private static readonly Dictionary<int, System> systems = [];
  protected Progress progress = Progress.COMPLETE;

  public IDictionary<Entity, Archetype> Entities => entities;
  public IEnumerable<ISystem> Systems => systems.Values;
  /// <summary>
  /// The current progress of the world loading.
  /// </summary>
  public Progress Progress => progress;

  public void Set(Entity key, string name) => entityNames[key] = name;

  public void Set<T>(Entity key, T component) where T : struct, IComponent =>
    entities[key] = Archetype.From(component);

  public ref T Get<T>(Entity key) where T : struct, IComponent {
    if (!entities.TryGetValue(key, out var entity))
      throw new Exception(string.Format("Entity {0} doesn't exist!", key));

    var components = entity.GetArray<T>().Enumerate();
    foreach (var component in components)
      return ref entity.Get<T>(component.Index);

    throw new Exception(
      string.Format("Component of type \"{0}\" doesn't exist in entity {1}!", typeof(T).FullName, key));
  }

  public bool Has<T>(Entity key) where T : struct, IComponent {
    if (!entities.TryGetValue(key, out var entity)) return false;
    return entity.GetArray<T>().Length > 0;
  }

  public void Remove<T>(Entity key) where T : struct, IComponent {}

  public void Destroy(Entity key) {
    if (!entities.TryGetValue(key, out var entity)) {
      logger.Warn("Could not destroy entity {key}; it doesn't exist!", key);
      return;
    }
    entities.Remove(key);
    var components = entity.Components;
    foreach (var component in components)
      component.Dispose();
  }

  public void Add<TSystem>() where TSystem : System, new() => systems[typeof(TSystem).GetHashCode()] = new TSystem();
  public void Remove<TSystem>() where TSystem : System => systems.Remove(typeof(TSystem).GetHashCode());

  public void Update(TimeSpan delta) {}

  public void Dispose() {
    GC.SuppressFinalize(this);
    foreach (var key in entities.Keys) Destroy(key);
    entityNames.Clear();
    systems.Clear();
  }
}
