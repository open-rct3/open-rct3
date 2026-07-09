// Park
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Collections.Generic;
using System.Numerics;

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
  /// The size of a single grid square in meters.
  /// </summary>
  public const float TileSize = 4.0f;
  /// <summary>
  /// The default width and height of the buildable area in tiles.
  /// </summary>
  public const int DefaultMapSize = 128;
  /// <summary>
  /// The width of the out-of-bounds border in tiles.
  /// </summary>
  public const int OutOfBoundsBorder = 5;
  /// <summary>
  /// The position of the park entrance.
  /// </summary>
  /// <remarks>
  /// The entrance is typically on the South edge of the buildable area.
  /// </remarks>
  // FIXME: This should be loaded from park data, not hard-coded.
  public Vector3 EntrancePosition { get; set; } = new(0, OutOfBoundsBorder * TileSize, 0);

  /// <summary>
  /// The rectangular boundary of the buildable area.
  /// </summary>
  public (Vector2 Min, Vector2 Max) BuildableBounds { get; }

  /// <summary>
  /// The maximum corner-height rise across a single tile permitted for an at-grade path placement, in
  /// <see cref="Terrain.HeightStep"/> units (100 units = 1m).
  /// </summary>
  /// <remarks>
  /// Sourced from
  /// <c>.agents/plans/features/path-network.md</c>: "players can only place path pieces on terrain
  /// shallower than 1m rise".
  /// </remarks>
  public const ushort AtGradePathMaxRise = 100;

  /// <summary>
  /// Path-tile data, sparse over the map since most tiles have no path. Keyed the same way
  /// <see cref="Terrain"/> indexes tiles: a raw <c>(int X, int Y)</c> pair in the OOB-inclusive grid.
  /// </summary>
  public Dictionary<(int X, int Y), PathTile> Paths { get; } = [];

  public Park(int buildableWidth = DefaultMapSize, int buildableHeight = DefaultMapSize) {
    float halfWidth = (buildableWidth * TileSize) / 2.0f;
    float borderOffset = OutOfBoundsBorder * TileSize;

    BuildableBounds = (
      new Vector2(-halfWidth, borderOffset),
      new Vector2(halfWidth, borderOffset + (buildableHeight * TileSize))
    );
  }

  /// <summary>
  /// Attempts to place <paramref name="tile"/> at <c>(tileX, tileY)</c>.
  /// </summary>
  /// <remarks>
  /// An at-grade tile (<see cref="PathTile.Raised"/> is <c>false</c>) is rejected if the underlying
  /// <paramref name="terrain"/> tile is missing or steeper than <see cref="AtGradePathMaxRise"/> across
  /// its four corners; a raised tile is never constrained by terrain steepness.
  /// </remarks>
  /// <returns><c>true</c> if the tile was placed, <c>false</c> if the placement was rejected.</returns>
  public bool TryPlacePath(int tileX, int tileY, Terrain terrain, PathTile tile) {
    if (!tile.Raised && !IsAtGradePathPlaceable(tileX, tileY, terrain)) return false;

    Paths[(tileX, tileY)] = tile;
    return true;
  }

  /// <summary>
  /// Whether terrain at <c>(tileX, tileY)</c> is flat enough (rise under <see cref="AtGradePathMaxRise"/>
  /// across the tile's four corners) to place an at-grade path on.
  /// </summary>
  public static bool IsAtGradePathPlaceable(int tileX, int tileY, Terrain terrain) {
    if (!terrain.HasTile(tileX, tileY)) return false;

    var corners = terrain.GetCorners(tileX, tileY);
    var min = ushort.MaxValue;
    var max = ushort.MinValue;
    foreach (var corner in corners) {
      if (corner.Height < min) min = corner.Height;
      if (corner.Height > max) max = corner.Height;
    }
    return max - min < AtGradePathMaxRise;
  }

  /// <summary>
  /// Whether the path tile at <c>(tileX, tileY)</c> connects to its neighbor across <paramref name="edge"/>.
  /// </summary>
  /// <remarks>
  /// <para>
  /// Both tiles must have a placed path, and must agree on <see cref="PathTile.Raised"/> — a raised
  /// path never auto-connects to an at-grade one.
  /// </para>
  /// <para>
  /// Raised tiles connect when their shared edge height matches exactly (see
  /// <see cref="PathTile.GetRaisedEdgeHeight"/>). At-grade tiles connect when the underlying
  /// <paramref name="terrain"/> corner heights on the shared edge differ by no more than half of
  /// <see cref="AtGradePathMaxRise"/> — looser than an exact match, since gentle terrain undulation
  /// under 1m is still placeable at-grade in the first place.
  /// </para>
  /// </remarks>
  public bool IsPathConnected(int tileX, int tileY, Edge edge, Terrain terrain) {
    if (!Paths.TryGetValue((tileX, tileY), out var tile)) return false;

    var (dx, dy) = edge.Offset();
    var neighborX = tileX + dx;
    var neighborY = tileY + dy;
    if (!Paths.TryGetValue((neighborX, neighborY), out var neighbor)) return false;
    if (tile.Raised != neighbor.Raised) return false;

    if (tile.Raised) {
      return tile.GetRaisedEdgeHeight(edge) == neighbor.GetRaisedEdgeHeight(edge.Opposite());
    }

    var (thisC1, thisC2) = terrain.GetEdgeCornerHeights(tileX, tileY, edge);
    var (thatC1, thatC2) = terrain.GetEdgeCornerHeights(neighborX, neighborY, edge.Opposite());
    var diff = Math.Max(Math.Abs(thisC1 - thatC1), Math.Abs(thisC2 - thatC2));
    return diff <= AtGradePathMaxRise / 2;
  }
}
