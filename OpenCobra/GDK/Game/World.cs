// A generic game world.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using OpenCobra.GDK.Streaming;

namespace OpenCobra.GDK.Game;

public abstract class World : IWorld {
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
