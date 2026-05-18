// Park
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;
using OpenRCT3.Terrain;

namespace OpenRCT3.Simulation;

/// <summary>
/// Represents a park within the game world.
/// </summary>
/// <remarks>
/// <para>
/// The park consists of a buildable area surrounded by an out-of-bounds (OOB) region.
/// The "fence" marks the boundary between these two areas.
/// </para>
/// <para>
/// Standard buildable area is 128x128 tiles (512m x 512m).
/// Total area including the 5-tile OOB border is 138x138 tiles.
/// </para>
/// </remarks>
public class Park {
  /// <summary>
  /// The position of the park entrance.
  /// </summary>
  /// <remarks>
  /// The entrance is typically on the South edge of the buildable area.
  /// </remarks>
  // FIXME: This should be loaded from park data, not hard-coded.
  public Vector3 EntrancePosition { get; set; } = new(0, Terrain.Terrain.OutOfBoundsBorder * Terrain.Terrain.TileSize, 0);

  /// <summary>
  /// The rectangular boundary of the buildable area.
  /// </summary>
  public (Vector2 Min, Vector2 Max) BuildableBounds { get; }

  public Park(int buildableWidth = Terrain.Terrain.DefaultMapSize, int buildableHeight = Terrain.Terrain.DefaultMapSize) {
    float halfWidth = (buildableWidth * Terrain.Terrain.TileSize) / 2.0f;
    float borderOffset = Terrain.Terrain.OutOfBoundsBorder * Terrain.Terrain.TileSize;

    BuildableBounds = (
      new Vector2(-halfWidth, borderOffset),
      new Vector2(halfWidth, borderOffset + (buildableHeight * Terrain.Terrain.TileSize))
    );
  }
}
