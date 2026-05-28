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
/// <h2>Accessing World State</h2>
/// <para>
/// Systems may interrogate the state of their world via <see cref="IWorld.IoC"/>.
/// </para>
/// <h2>Multi-threading</h2>
/// <para>
/// Parallel systems iterate over archetypes with internal chunk processing.
/// See <see cref="Parallelizable"/>.
/// </para>
/// </remarks>
/// <seealso cref="DryIoc.Container"/>
public abstract class System(PipelinePhase order) : ISystem {
  public event Action? Started;
  public event Action? Stopped;

  public bool IsRunning { get; internal set; } = false;
  public PipelinePhase Order { get; } = order;
  public bool Parallelizable { get; protected set; } = false;

  public virtual void Attach(WeakReference<IWorld> world) { }
  public void Start() {
    IsRunning = true;
    Started?.Invoke();
  }

  public virtual void Update(TimeSpan delta) { }
  public void Stop() {
    IsRunning = false;
    Stopped?.Invoke();
  }

  public virtual void Dispose() => GC.SuppressFinalize(this);
}
