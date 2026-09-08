// World
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Streaming;
using GDK = OpenCobra.GDK;

namespace OpenRCT3.Simulation;

/// <summary>
/// Represents the game world including the current park, terrain, objects, and people.
/// </summary>
public class World : GDK.Game.World {
  public Terrain? Terrain { get; private set; }
  public Park? Park { get; private set; }

  // FIXME: Load() blocks until every task completes since callers (e.g. Game's constructor) dereference
  // Terrain/Park synchronously right after calling it. Progress.MeasureTasks runs tasks on a background
  // Task.Run and returns immediately without waiting; without this .Wait(), Terrain/Park may still be
  // null when the caller reads them. Revisit once a progress bar actually consumes Progress
  // asynchronously (see the TODO in Game.cs) instead of blocking here.
  public override void Load() {
    var measurement = Progress.MeasureTasks([
      new(() => Park = new Park(), "Loading park"),
      new(() => Terrain = Terrain.Load(), "Loading terrain"),
    ]);
    Progress = measurement.Progress;
    measurement.Task.Wait();
  }

  protected virtual void Dispose(bool disposing) {
    if (disposing) {
      Terrain?.GrassTexture?.Dispose();
    }

    Terrain = null;
    Park = null;
    base.Dispose(disposing);
  }
}
