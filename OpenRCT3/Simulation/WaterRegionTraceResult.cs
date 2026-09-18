// Water Region Trace Result
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OpenRCT3.Simulation;

/// <summary>
/// The immutable output of tracing one connected, fillable water region.
/// </summary>
/// <remarks>
/// <see cref="Tiles"/> and <see cref="Boundary"/> are sorted by Y, then X, then edge. A later
/// placement layer can pass <see cref="Tiles"/>, <see cref="Height"/>, and <see cref="IsOcean"/>
/// directly to the pool-placement API without repeating the trace.
/// </remarks>
public sealed class WaterRegionTraceResult {
  /// <summary>The 1 m-snapped water height in signed terrain-height units.</summary>
  public int Height { get; }

  /// <summary>The exact 4-connected tile region covered by water.</summary>
  public IReadOnlyList<(int X, int Y)> Tiles { get; }

  /// <summary>The outward-facing grid edges around <see cref="Tiles"/>.</summary>
  public IReadOnlyList<WaterRegionBoundaryEdge> Boundary { get; }

  /// <summary>Whether the connected fillable region reaches the OOB-inclusive map edge.</summary>
  public bool IsOcean { get; }

  internal WaterRegionTraceResult(
    int height,
    IEnumerable<(int X, int Y)> tiles,
    IEnumerable<WaterRegionBoundaryEdge> boundary,
    bool isOcean) {
    Height = height;
    Tiles = new ReadOnlyCollection<(int X, int Y)>([.. tiles]);
    Boundary = new ReadOnlyCollection<WaterRegionBoundaryEdge>([.. boundary]);
    IsOcean = isOcean;
  }
}
