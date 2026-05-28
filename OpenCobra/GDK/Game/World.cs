// World
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using DryIoc;
using OpenCobra.GDK.Streaming;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace OpenCobra.GDK.Game;

public abstract class World : IWorld {
  private readonly ObservableCollection<ISystem> systems = [];
  private bool disposed;

  protected WeakReference<IWorld> WeakReference => new(this);
  public Progress Progress { get; protected set; } = Progress.COMPLETE;
  public IReadOnlyCollection<ISystem> Systems => systems.AsReadOnly();

  protected World() {
    systems.CollectionChanged += SystemsChanged;

    // Provide the current load progress to systems
    IWorld.IoC.Register<Progress>(
      Reuse.Singleton,
      Made.Of(() => Progress),
      Setup.With(weaklyReferenced: true, preventDisposal: true)
    );
  }

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

  private void SystemsChanged(object? sender, NotifyCollectionChangedEventArgs e) {
    switch (e.Action) {
      case NotifyCollectionChangedAction.Add:
      case NotifyCollectionChangedAction.Remove:
      case NotifyCollectionChangedAction.Replace:
      case NotifyCollectionChangedAction.Reset:
        foreach (var system in e.NewItems!.Cast<ISystem>()) system.Attach(WeakReference);
        foreach (var system in e.OldItems!.Cast<ISystem>()) system.Stop();
        break;
      default: return;
    }
  }
}
