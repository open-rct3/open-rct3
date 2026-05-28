// System Primitives
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenCobra.GDK.Game;

/// <summary>
/// Represents a self-contained game system.
/// </summary>
/// <remarks>
/// <h2>Multithreading</h2>
/// <para>Parallel systems iterate over archetypes with internal chunk processing.
/// See <see cref="Parallelizable"/>.</para>
/// </remarks>
public abstract class System(PipelinePhase order) : ISystem {
  public PipelinePhase Order { get; } = order;
  public bool Parallelizable { get; protected set; } = false;

  public virtual void Attach(IWorld world) { }
  public virtual void Start() { }
  public virtual void Update(TimeSpan delta) { }
  public virtual void Stop() { }
  public virtual void Dispose() => GC.SuppressFinalize(this);
}
