// Water Region Tracer
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Collections.Generic;
using System.Linq;

namespace OpenRCT3.Simulation;

/// <summary>
/// Traces the bounded, 4-connected terrain region fillable from one water-tool seed tile.
/// </summary>
public static class WaterRegionTracer {
  /// <summary>One meter in <see cref="Terrain.HeightStep"/> units.</summary>
  public const int SurfaceHeightSnap = Park.AtGradePathMaxRise;

  private static readonly Edge[] OrderedEdges = [
    Edge.South,
    Edge.West,
    Edge.East,
    Edge.North,
  ];

  /// <summary>
  /// Attempts to trace the exact connected region fillable from the seed tile.
  /// </summary>
  /// <param name="terrain">Terrain whose OOB-inclusive tile grid bounds the walk.</param>
  /// <param name="seedX">Seed tile X index.</param>
  /// <param name="seedY">Seed tile Y index.</param>
  /// <param name="isTileOccupied">
  /// Query returning whether a tile already belongs to a water pool. Occupied tiles are barriers and
  /// an occupied seed rejects the trace.
  /// </param>
  /// <param name="result">
  /// The immutable, deterministically ordered region and boundary, or <c>null</c> on rejection.
  /// </param>
  /// <returns>
  /// <c>false</c> when the seed is off-grid, occupied, above the surface derived from its lowest
  /// corner, or that corner cannot be represented after 1 m upward snapping; otherwise <c>true</c>.
  /// </returns>
  public static bool TryTrace(
    Terrain terrain,
    int seedX,
    int seedY,
    Func<int, int, bool> isTileOccupied,
    out WaterRegionTraceResult? result) {
    ArgumentNullException.ThrowIfNull(terrain);
    ArgumentNullException.ThrowIfNull(isTileOccupied);
    result = null;

    if (!terrain.HasTile(seedX, seedY)) return false;
    if (isTileOccupied(seedX, seedY)) return false;
    var lowestCorner = GetLowestCornerHeight(terrain, seedX, seedY);
    if (!TrySnapHeight(lowestCorner, out var height)) return false;
    if (!IsFillable(terrain, seedX, seedY, height)) return false;

    var tileCount = checked(terrain.Width * terrain.Height);
    var examined = new bool[tileCount];
    var queue = new Queue<(int X, int Y)>();
    var region = new HashSet<(int X, int Y)>();
    MarkExaminedAndEnqueue(seedX, seedY, terrain.Width, examined, queue);

    for (var visitedCount = 0; queue.Count > 0 && visitedCount < tileCount; visitedCount++) {
      var tile = queue.Dequeue();
      region.Add(tile);

      foreach (var edge in OrderedEdges) {
        var (dx, dy) = edge.Offset();
        var neighborX = tile.X + dx;
        var neighborY = tile.Y + dy;
        if (!terrain.HasTile(neighborX, neighborY)) continue;

        var neighborIndex = GetTileIndex(neighborX, neighborY, terrain.Width);
        if (examined[neighborIndex]) continue;
        examined[neighborIndex] = true;
        if (isTileOccupied(neighborX, neighborY)) continue;
        if (!IsFillable(terrain, neighborX, neighborY, height)) continue;
        queue.Enqueue((neighborX, neighborY));
      }
    }

    var tiles = region.OrderBy(tile => tile.Y).ThenBy(tile => tile.X).ToArray();
    var boundary = GetBoundary(tiles, region);
    var isOcean = tiles.Any(tile =>
      tile.X == 0 || tile.Y == 0 ||
      tile.X == terrain.Width - 1 || tile.Y == terrain.Height - 1);
    result = new WaterRegionTraceResult(height, tiles, boundary, isOcean);
    return true;
  }

  private static void MarkExaminedAndEnqueue(
    int tileX,
    int tileY,
    int width,
    bool[] examined,
    Queue<(int X, int Y)> queue) {
    examined[GetTileIndex(tileX, tileY, width)] = true;
    queue.Enqueue((tileX, tileY));
  }

  private static int GetTileIndex(int tileX, int tileY, int width)
    => checked((tileY * width) + tileX);

  private static bool IsFillable(Terrain terrain, int tileX, int tileY, int height) {
    foreach (var corner in terrain.GetCorners(tileX, tileY))
      if (corner.Height > height) return false;
    return true;
  }

  private static int GetLowestCornerHeight(Terrain terrain, int tileX, int tileY) {
    var lowest = int.MaxValue;
    foreach (var corner in terrain.GetCorners(tileX, tileY))
      if (corner.Height < lowest) lowest = corner.Height;
    return lowest;
  }

  private static WaterRegionBoundaryEdge[] GetBoundary(
    IEnumerable<(int X, int Y)> tiles,
    IReadOnlySet<(int X, int Y)> region) {
    var boundary = new List<WaterRegionBoundaryEdge>();
    foreach (var tile in tiles) {
      foreach (var edge in OrderedEdges) {
        var (dx, dy) = edge.Offset();
        if (region.Contains((tile.X + dx, tile.Y + dy))) continue;
        boundary.Add(new WaterRegionBoundaryEdge(tile.X, tile.Y, edge));
      }
    }
    return [.. boundary];
  }

  private static bool TrySnapHeight(int cornerHeight, out int height) {
    var remainder = cornerHeight % SurfaceHeightSnap;
    if (remainder == 0) {
      height = cornerHeight;
      return true;
    }

    var adjustment = remainder > 0 ? SurfaceHeightSnap - remainder : -remainder;
    var snapped = Convert.ToInt64(cornerHeight) + adjustment;
    if (snapped > int.MaxValue) {
      height = default;
      return false;
    }

    height = Convert.ToInt32(snapped);
    return true;
  }
}
