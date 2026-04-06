// Game
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System;
using OpenCobra.GDK;
using OpenRCT3.Simulation;
using OpenRCT3.Platforms;

namespace OpenRCT3;

/// <summary>
/// The game world.
/// </summary>
public class Game : IDisposable {
  public static Game? Instance { get; private set; }

  public AppConfig Config { get; } = AppConfig.Instance;
  [Unowned("The renderer is owned by the platform abstraction layer.")]
  public WeakReference<IRenderer> Renderer { get; }
  public World World { get; }
  public Scene Scene { get; } = new();

  /// <param name="renderer">The game renderer.</param>
  public Game(WeakReference<IRenderer> renderer) {
    Instance = this;
    Renderer = renderer;
    World = new World();
    // TODO: Log with the info severity: "Simulation features are unimplemented"
  }

  /// <summary>
  /// Advances the simulation.
  /// </summary>
  /// <param name="timeSpan">The time between ticks.</param>
  public void Tick(TimeSpan timeSpan) {
    // TODO: Advance the simulation given the time since the last tick
  }

  public void Dispose() {
    // TODO: World.Dispose();
    GC.SuppressFinalize(this);
    Instance = null;
  }
}
