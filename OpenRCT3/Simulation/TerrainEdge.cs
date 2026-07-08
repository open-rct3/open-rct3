// Terrain Edge
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
namespace OpenRCT3.Simulation;

/// <summary>
/// Identifies one of the four edges of a tile.
/// </summary>
/// <remarks>
/// Each edge is defined by the two corners that bound it on the owning tile and the matching pair
/// of corners on the neighboring tile across the edge.
/// </remarks>
public enum TerrainEdge {
  /// <summary>The edge running West-to-East along the tile's South side.</summary>
  South = 0,
  /// <summary>The edge running South-to-North along the tile's West side.</summary>
  West = 1,
  /// <summary>The edge running South-to-North along the tile's East side.</summary>
  East = 2,
  /// <summary>The edge running West-to-East along the tile's North side.</summary>
  North = 3,
}
