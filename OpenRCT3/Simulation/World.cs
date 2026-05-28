// World
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Game;
using OpenCobra.GDK.Streaming;

namespace OpenRCT3.Simulation;

/// <summary>
/// Represents the game world including the current park, terrain, objects, and people.
/// </summary>
public class World : IWorld {
  private Progress progress = Progress.COMPLETE;
  private bool disposed;

  /// <summary>
  /// The current progress of the world loading.
  /// </summary>
  public Progress Progress => progress;
  public Terrain? Terrain { get; private set; }
  public Park? Park { get; private set; }

  public void Load() => progress = Progress.MeasureTasks([
      new(() => Park = new Park(), "Loading park"),
      new(() => Terrain = Terrain.Load(), "Loading terrain"),
    ]).Progress;

  protected virtual void Dispose(bool disposing) {
    if (disposed) return;
    if (disposing) {
      Terrain?.GrassTexture?.Dispose();
    }

    Terrain = null;
    Park = null;
    disposed = true;
  }

  public void Dispose() {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }

  // TODO: Use Task.Factory.StartNew to do slow async work in the background
}
