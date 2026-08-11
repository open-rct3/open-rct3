// A generic game world.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using OpenCobra.GDK.Streaming;

namespace OpenCobra.GDK.Game;

/// <remarks>
/// Systems are managed via <see cref="AddSystem"/> and <see cref="RemoveSystem"/>, executed by
/// <see cref="Update(TimeSpan)"/>, and automatically stopped/disposed when the world is disposed.
/// Weak references passed to <see cref="System.Attach"/> prevent systems from extending world lifetime.
/// </remarks>
public abstract class World : IWorld {
  /// <remarks>
  /// Reference equality prevents duplicate adds. Systems run in phase order (Early → Update → Render → Late);
  /// within a phase, parallel systems run concurrently via PLINQ, linear systems sequentially.
  /// </remarks>
  private readonly HashSet<ISystem> systems = [];
  private bool disposed;

  protected WeakReference<IWorld> WeakReference => new(this);
  public Progress Progress { get; protected set; } = Progress.COMPLETE;
  public IReadOnlyCollection<ISystem> Systems => systems;

  protected World() {
    // Provide the current load progress to systems
    IGame.IoC.Register<Progress>(
      Reuse.Singleton,
      Made.Of(() => Progress),
      Setup.With(weaklyReferenced: true, preventDisposal: true)
    );
  }

  /// <remarks>
  /// Calls <see cref="ISystem.Attach"/>, then <see cref="ISystem.Start"/>, in sequence to fully initialize
  /// the system for immediate use. Returns false if the system was already in the collection (checked via
  /// <see cref="HashSet{T}"/> reference equality, preventing duplicate adds).
  /// </remarks>
  protected bool AddSystem(ISystem system) {
    if (!systems.Add(system))
      return false;
    system.Attach(WeakReference);
    system.Start();
    return true;
  }

  protected void RemoveSystem(ISystem system) {
    if (systems.Remove(system))
      system.Stop();
  }

  public abstract void Load();

  /// <remarks>
  /// Executes all systems in phase order via <see cref="Scheduler.Execute"/>. Catches and swallows
  /// <see cref="OperationCanceledException"/> (matching Scheduler's behavior); allows
  /// <see cref="AggregateException"/> to propagate (from failed parallel systems).
  /// </remarks>
  public void Update(TimeSpan delta) {
    try {
      Scheduler.Execute(systems, delta);
    } catch (OperationCanceledException) {
      // Execution was cancelled, safe to continue
    }
  }

  protected virtual void Dispose(bool disposing) {
    if (disposed) return;
    if (disposing) {
      // Shutdown all systems
      foreach (var system in systems) {
        system.Stop();
        system.Dispose();
      }
      systems.Clear();
    }
    disposed = true;
  }

  public void Dispose() {
    // Do not change this code! Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }

}
