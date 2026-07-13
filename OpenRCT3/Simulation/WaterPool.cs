// Water Pool
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Collections.Generic;

namespace OpenRCT3.Simulation;

/// <summary>
/// A single flat body of water: a shared surface height plus the set of tiles it covers.
/// </summary>
/// <remarks>
/// <para>
/// Water is not a single map-wide plane. Each pool is an independent overlay traced over the terrain
/// at creation time; the terrain height under a pool is unaffected by the pool's existence.
/// </para>
/// <para>
/// A pool's tile set is exact (grid-snapped at creation), the same shape <see cref="Park.Paths"/> uses
/// for path tiles, rather than a boundary polygon.
/// </para>
/// </remarks>
public class WaterPool {
  /// <summary>The pool's flat water-surface height, in <see cref="Terrain.HeightStep"/> units.</summary>
  public ushort Height { get; }

  /// <summary>
  /// Whether this pool is an ocean: its traced region reached the edge of the OOB-inclusive grid (the
  /// "island map" case). An ocean's rendered water surface extends to the skybox horizon in every
  /// direction instead of stopping at <see cref="Tiles"/>; <see cref="Tiles"/> still identifies which
  /// of the map's own tiles are "wet" for gameplay/terrain purposes.
  /// </summary>
  public bool IsOcean { get; }

  /// <summary>The set of tiles this pool covers, in the OOB-inclusive grid.</summary>
  public IReadOnlySet<(int X, int Y)> Tiles { get; }

  public WaterPool(ushort height, IEnumerable<(int X, int Y)> tiles, bool isOcean = false) {
    Height = height;
    IsOcean = isOcean;
    Tiles = tiles as IReadOnlySet<(int X, int Y)> ?? new HashSet<(int X, int Y)>(tiles);
  }
}
