// Water Pool
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Collections.Generic;
using System.Linq;

namespace OpenRCT3.Simulation;

/// <summary>
/// A single flat body of water: a shared surface height plus the terrain-triangle fragments it
/// covers.
/// </summary>
/// <remarks>
/// <para>
/// Water is not a single map-wide plane. Each pool is an independent overlay traced over the terrain
/// at creation time; the terrain height under a pool is unaffected by the pool's existence.
/// </para>
/// <para>
/// <see cref="Triangles"/> retains exact partial-tile coverage. <see cref="Tiles"/> is the unique set
/// of tiles touched by those records, which preserves the tile-based gameplay API used by
/// <see cref="Park.Paths"/> and water invalidation.
/// </para>
/// </remarks>
public class WaterPool {
  /// <summary>
  /// The pool's flat, signed water-surface height in <see cref="Terrain.HeightStep"/> units.
  /// </summary>
  public int Height { get; }

  /// <summary>
  /// Whether this pool is an ocean: its traced region reached the edge of the OOB-inclusive grid (the
  /// "island map" case). <see cref="Tiles"/> identifies the bounded part of the ocean within the map
  /// for rendering and gameplay. Extending it to a skybox horizon is separate presentation policy.
  /// </summary>
  public bool IsOcean { get; }

  /// <summary>The set of tiles this pool covers, in the OOB-inclusive grid.</summary>
  public IReadOnlySet<(int X, int Y)> Tiles { get; }

  /// <summary>The exact terrain triangles covered by this pool.</summary>
  public IReadOnlyList<WaterSurfaceTriangle> Triangles { get; }

  /// <summary>Creates a pool that fully covers every supplied tile.</summary>
  /// <remarks>
  /// This constructor preserves the original tile-based API. Each unique tile expands to the two
  /// full-mask triangles used by RCT3's SouthEast-to-NorthWest terrain split.
  /// </remarks>
  public WaterPool(int height, IEnumerable<(int X, int Y)> tiles, bool isOcean = false)
    : this(height, BuildFullTileTriangles(tiles), isOcean) { }

  /// <summary>Creates a pool from exact partial terrain-triangle records.</summary>
  public WaterPool(
    int height,
    IEnumerable<WaterSurfaceTriangle> triangles,
    bool isOcean = false
  ) {
    ArgumentNullException.ThrowIfNull(triangles);

    var triangleList = new List<WaterSurfaceTriangle>();
    var triangleKeys = new HashSet<(int X, int Y, WaterTerrainTriangle Triangle)>();
    var tiles = new HashSet<(int X, int Y)>();
    foreach (var triangle in triangles) {
      ValidateTriangle(triangle, nameof(triangles));
      var key = (triangle.X, triangle.Y, triangle.Triangle);
      if (!triangleKeys.Add(key))
        throw new ArgumentException(
          $"Water triangle ({triangle.X}, {triangle.Y}, {triangle.Triangle}) is duplicated.",
          nameof(triangles));

      triangleList.Add(triangle);
      tiles.Add((triangle.X, triangle.Y));
    }

    Height = height;
    IsOcean = isOcean;
    Triangles = Array.AsReadOnly(triangleList.ToArray());
    Tiles = tiles;
  }

  private static WaterSurfaceTriangle[] BuildFullTileTriangles(
    IEnumerable<(int X, int Y)> tiles
  ) {
    ArgumentNullException.ThrowIfNull(tiles);

    return new HashSet<(int X, int Y)>(tiles)
      .OrderBy(tile => tile.Y)
      .ThenBy(tile => tile.X)
      .SelectMany(tile => new[] {
        new WaterSurfaceTriangle(
          tile.X, tile.Y, WaterTerrainTriangle.SouthWest, vertexMask: 7),
        new WaterSurfaceTriangle(
          tile.X, tile.Y, WaterTerrainTriangle.NorthEast, vertexMask: 7),
      })
      .ToArray();
  }

  private static void ValidateTriangle(WaterSurfaceTriangle triangle, string parameterName) {
    if (!Enum.IsDefined(typeof(WaterTerrainTriangle), triangle.Triangle))
      throw new ArgumentException("A water triangle has an invalid triangle index.", parameterName);
    if (triangle.VertexMask is < 1 or > 7)
      throw new ArgumentException("A water triangle has an invalid vertex mask.", parameterName);
  }
}
