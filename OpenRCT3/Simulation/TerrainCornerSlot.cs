// Terrain Corner Slot
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
namespace OpenRCT3.Simulation;

/// <summary>
/// Identifies one of the four corners owned by a single tile.
/// </summary>
/// <remarks>
/// <para>
/// A tile at index <c>(x, y)</c> owns four corners laid out in world space as:
/// <code>
///   NW (2) ──── NE (3)
///     │           │
///     │   tile    │
///     │           │
///   SW (0) ──── SE (1)
/// </code>
/// </para>
/// <para>
/// Adjacent tiles share the *same* world-space corner point but own independent
/// <see cref="TerrainCorner"/> records for it. Whether two tiles present a smooth slope or a sheer
/// cliff at their shared edge is derived from equality of the corresponding corner heights, not from
/// a stored bitflag.
/// </para>
/// </remarks>
public enum TerrainCornerSlot {
  /// <summary>South-West corner of the tile, at world index <c>(x, y)</c>.</summary>
  SouthWest = 0,
  /// <summary>South-East corner of the tile, at world index <c>(x + 1, y)</c>.</summary>
  SouthEast = 1,
  /// <summary>North-West corner of the tile, at world index <c>(x, y + 1)</c>.</summary>
  NorthWest = 2,
  /// <summary>North-East corner of the tile, at world index <c>(x + 1, y + 1)</c>.</summary>
  NorthEast = 3,
}
