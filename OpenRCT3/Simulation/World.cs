// World
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenRCT3.Terrain;

namespace OpenRCT3.Simulation;

/// <summary>
/// Represents the game world including the current park, terrain, objects, and people.
/// </summary>
public class World {
  public Terrain.Terrain? Terrain { get; private set; }
  public Park? Park { get; private set; }

  public void Load() {
    Terrain = Terrain.Terrain.Load();
    Park = new Park();
  }

  // TODO: Use Task.Factory.StartNew to do slow async work in the background

  // TODO: Implement IDisposable
}
