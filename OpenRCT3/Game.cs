// Game
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenRCT3.Simulation;
using OpenRCT3.Platforms;

namespace OpenRCT3;

/// <summary>
/// The game world.
/// </summary>
/// <remarks>
///
/// </remarks>
public class Game {
  public AppConfig Config { get; }
  public World World { get; }

  /// <param name="config">The loaded application configuration.</param>
  public Game(AppConfig config) {
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
