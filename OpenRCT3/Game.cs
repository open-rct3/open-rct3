// Game
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System;
using OpenRCT3.Simulation;
using OpenRCT3.Platforms;
using OpenCobra.GDK;

namespace OpenRCT3;

/// <summary>
/// The game world.
/// </summary>
/// <remarks>
///
/// </remarks>
public class Game {
  public static Game? Instance { get; private set; }
  public AppConfig Config { get; }
  public World World { get; }
  public Scene Scene { get; init; } = new();

  /// <param name="config">The loaded application configuration.</param>
  public Game(AppConfig config) {
    Instance = this;
    Config = config;
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
}
