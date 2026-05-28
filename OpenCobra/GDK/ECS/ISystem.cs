// ECS System Primitives
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.ComponentModel;

namespace OpenCobra.GDK.ECS;

public enum SystemOrder : int {
  /// <summary>
  /// Process input, physics, etc.
  /// </summary>
  Early = -1,
  /// <summary>
  /// Process game logic
  /// </summary>
  Normal = 0,
  /// <summary>
  /// Process rendering, GUI, etc.
  /// </summary>
  Late = 1,
}

public interface ISystem : IDisposable {
  [Category("Behavior")]
  SystemOrder SystemOrder { get; }
  [Category("Behavior")]
  bool Parallelizable { get; }

  void Attach();
  void Start();
  void Update(TimeSpan delta);
  void Stop();
}

/// <summary>
/// Represents a self-contained game system.
/// </summary>
/// <remarks>
/// <h2>Multithreading</h2>
/// Parallel systems iterate over archetypes with internal chunk processing.
/// See <see cref="Parallelizable"/>.
/// </remarks>
public abstract class System(SystemOrder order) : ISystem {
  public SystemOrder SystemOrder { get; } = order;
  public bool Parallelizable { get; protected set; } = false;

  public virtual void Attach() { }
  public virtual void Start() { }
  public virtual void Update(TimeSpan delta) { }
  public virtual void Stop() { }
  public virtual void Dispose() => GC.SuppressFinalize(this);
}
