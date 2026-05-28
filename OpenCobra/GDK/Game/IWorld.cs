// IWorld
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Streaming;

namespace OpenCobra.GDK.Game;

public interface IWorld : IDisposable {
  /// <summary>
  /// The current progress of the world loading.
  /// </summary>
  Progress Progress { get; }

  /// <summary>
  /// Systems that operate on components of this world.
  /// </summary>
  IReadOnlyCollection<ISystem> Systems { get; }

  void Load();
}

public abstract class World : IWorld {
  private readonly IList<ISystem> systems = [];
  private bool disposed;

  public Progress Progress { get; protected set; } = Progress.COMPLETE;
  public IReadOnlyCollection<ISystem> Systems => systems.AsReadOnly();

  public abstract void Load();

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
