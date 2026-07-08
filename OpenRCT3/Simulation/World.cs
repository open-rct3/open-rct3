// World
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using OpenRCT3.Streaming;

namespace OpenRCT3.Simulation;

/// <summary>
/// Represents the game world including the current park, terrain, objects, and people.
/// </summary>
public class World {
  private Progress progress = Progress.COMPLETE;

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

  // TODO: Use Task.Factory.StartNew to do slow async work in the background
  // TODO: Implement IDisposable
}
