// Edge Extensions
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
namespace OpenRCT3.Simulation;

/// <summary>
/// Helper methods for <see cref="Edge"/>, shared by <see cref="Terrain"/> and path-network code.
/// </summary>
public static class EdgeExtensions {
  /// <summary>Returns the edge on the opposite side of a tile from <paramref name="edge"/>.</summary>
  public static Edge Opposite(this Edge edge) => edge switch {
    Edge.South => Edge.North,
    Edge.North => Edge.South,
    Edge.East => Edge.West,
    Edge.West => Edge.East,
    _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, null),
  };

  /// <summary>Returns the tile-index offset to step from a tile to its neighbor across <paramref name="edge"/>.</summary>
  public static (int dx, int dy) Offset(this Edge edge) => edge switch {
    Edge.South => (0, -1),
    Edge.West => (-1, 0),
    Edge.East => (1, 0),
    Edge.North => (0, 1),
    _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, null),
  };
}
