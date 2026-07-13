// Park
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections.Generic;
using System.Numerics;
using OpenCobra.OVL;

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
  /// Players can only place path pieces on terrain shallower than 1m rise.
  /// </remarks>
  public const ushort AtGradePathMaxRise = 100;

  /// <summary>
  /// Path-tile data, sparse over the map since most tiles have no path. Keyed the same way
  /// <see cref="Terrain"/> indexes tiles: a raw <c>(int X, int Y)</c> pair in the OOB-inclusive grid.
  /// </summary>
  public Dictionary<(int X, int Y), PathTile> Paths { get; } = [];

  /// <summary>
  /// Every placed <see cref="WaterPool"/>. Prefer <see cref="WaterTiles"/> for tile-based lookups; this
  /// list exists for iteration (e.g. rendering every pool) and O(1) removal by reference.
  /// </summary>
  public List<WaterPool> WaterPools { get; } = [];

  /// <summary>
  /// Maps each tile a <see cref="WaterPool"/> covers to that pool, so a tile query resolves its pool
  /// (if any) in O(1) without scanning <see cref="WaterPools"/>. Multiple tiles alias the same
  /// <see cref="WaterPool"/> reference.
  /// </summary>
  public Dictionary<(int X, int Y), WaterPool> WaterTiles { get; } = [];

  /// <summary>
  /// Every placed <see cref="SceneryPlacement"/>. Placement data lives directly on <see cref="Park"/>,
  /// not a separate "scenery layer" type.
  /// </summary>
  public List<SceneryPlacement> SceneryPlacements { get; } = [];

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

  /// <summary>
  /// Places a <see cref="WaterPool"/> covering exactly <paramref name="tiles"/>, at a flat surface
  /// height of <paramref name="height"/>.
  /// </summary>
  /// <remarks>
  /// Terrain height is untouched by this call; a pool is a separate overlay traced over existing
  /// terrain shape at creation time (water is tracked per-pool, not per-tile). Perimeter-tracing/
  /// flood-fill from a seed tile is not implemented here — the caller
  /// supplies the already-decided tile set, the same way <see cref="TryPlacePath"/> takes an
  /// already-decided <see cref="PathTile"/> rather than routing one.
  /// </remarks>
  /// <returns>
  /// <c>true</c> if the pool was placed; <c>false</c> if <paramref name="tiles"/> is empty, any tile is
  /// off-grid, or any tile already belongs to another pool.
  /// </returns>
  public bool TryPlaceWaterPool(IEnumerable<(int X, int Y)> tiles, ushort height, Terrain terrain, bool isOcean = false) {
    var tileList = tiles as ICollection<(int X, int Y)> ?? [.. tiles];
    if (tileList.Count == 0) return false;

    foreach (var tile in tileList) {
      if (!terrain.HasTile(tile.X, tile.Y)) return false;
      if (WaterTiles.ContainsKey(tile)) return false;
    }

    var pool = new WaterPool(height, tileList, isOcean);
    WaterPools.Add(pool);
    foreach (var tile in tileList) WaterTiles[tile] = pool;
    return true;
  }

  /// <summary>
  /// Removes the <see cref="WaterPool"/> covering <c>(tileX, tileY)</c>, if any, along with every other
  /// tile it covers.
  /// </summary>
  /// <remarks>
  /// A terrain edit invalidates the whole pool it touches, not just the edited tile or a partial
  /// region of the pool. Re-creating a pool after an edit means re-running placement, not reshaping
  /// this one.
  /// </remarks>
  /// <returns><c>true</c> if a pool was found and removed.</returns>
  public bool InvalidateWaterPoolAt(int tileX, int tileY) {
    if (!WaterTiles.TryGetValue((tileX, tileY), out var pool)) return false;

    WaterPools.Remove(pool);
    foreach (var tile in pool.Tiles) WaterTiles.Remove(tile);
    return true;
  }

  /// <summary>
  /// Raises a terrain corner via <see cref="Terrain.RaiseCorner"/>, then invalidates any
  /// <see cref="WaterPool"/> covering a tile whose height actually changed as a result (the edited tile
  /// and every neighbor sharing that corner).
  /// </summary>
  public void RaiseTerrainCorner(
    Terrain terrain,
    int tileX,
    int tileY,
    TerrainCornerSlot slot,
    int delta,
    Func<int, int, TerrainCornerSlot, ushort>? maxHeightQuery = null) {
    terrain.RaiseCorner(tileX, tileY, slot, delta, maxHeightQuery);
    InvalidateWaterPoolsSharingCorner(terrain, tileX, tileY, slot);
  }

  /// <summary>
  /// Lowers a terrain corner via <see cref="Terrain.LowerCorner"/>, then invalidates any
  /// <see cref="WaterPool"/> covering a tile whose height actually changed as a result (the edited tile
  /// and every neighbor sharing that corner).
  /// </summary>
  public void LowerTerrainCorner(
    Terrain terrain,
    int tileX,
    int tileY,
    TerrainCornerSlot slot,
    int delta,
    Func<int, int, TerrainCornerSlot, ushort>? minHeightQuery = null) {
    terrain.LowerCorner(tileX, tileY, slot, delta, minHeightQuery);
    InvalidateWaterPoolsSharingCorner(terrain, tileX, tileY, slot);
  }

  /// <summary>
  /// Sets a terrain corner height via <see cref="Terrain.SetCornerHeight"/> (detaching the edge, unlike
  /// <see cref="RaiseTerrainCorner"/>/<see cref="LowerTerrainCorner"/>), then invalidates any
  /// <see cref="WaterPool"/> covering <c>(tileX, tileY)</c>.
  /// </summary>
  /// <remarks>
  /// Only the edited tile's own pool is invalidated: unlike raise/lower, this doesn't propagate to
  /// neighboring tiles, so no other tile's height actually changed.
  /// </remarks>
  public void SetTerrainCornerHeight(Terrain terrain, int tileX, int tileY, TerrainCornerSlot slot, ushort height) {
    terrain.SetCornerHeight(tileX, tileY, slot, height);
    InvalidateWaterPoolAt(tileX, tileY);
  }

  private void InvalidateWaterPoolsSharingCorner(Terrain terrain, int tileX, int tileY, TerrainCornerSlot slot) {
    foreach (var (x, y) in terrain.GetTilesSharingCorner(tileX, tileY, slot))
      InvalidateWaterPoolAt(x, y);
  }

  /// <summary>
  /// Attempts to place a scenery instance per <paramref name="placement"/>.
  /// </summary>
  /// <remarks>
  /// A multi-tile <see cref="OpenCobra.OVL.Placement.FullTile"/> footprint (see
  /// <see cref="SceneryDefinition.FootprintWidth"/>/<see cref="SceneryDefinition.FootprintHeight"/>)
  /// requires a level pad: every corner across the rotated footprint's covered tiles must agree, or
  /// placement is rejected outright — mirroring the "Flatten for Scenery and Rides" terrain tool and
  /// the same flatness-gate shape <see cref="IsAtGradePathPlaceable"/> uses for paths, rather than
  /// averaging or picking an anchor corner. Single-tile footprints and edge-mounted/sub-tile
  /// placements (which conform to slope, see <see cref="GetSceneryHeight"/>) have no such constraint.
  /// </remarks>
  /// <returns>
  /// <c>true</c> if the placement was recorded; <c>false</c> if <see cref="SceneryPlacement.ObjectKey"/>
  /// isn't registered, the anchor tile is off-grid, or a multi-tile footprint isn't level.
  /// </returns>
  public bool TryPlaceScenery(SceneryPlacement placement, SceneryRegistry registry, Terrain terrain) {
    if (!registry.TryGetDefinition(placement.ObjectKey, out var definition)) return false;
    if (!terrain.HasTile(placement.TileX, placement.TileY)) return false;

    if (definition.Placement == Placement.FullTile) {
      var (width, height) = GetRotatedFootprint(definition, placement.Rotation);
      if ((width > 1 || height > 1) && !IsFootprintLevel(placement.TileX, placement.TileY, width, height, terrain))
        return false;
    }

    SceneryPlacements.Add(placement);
    return true;
  }

  /// <summary>
  /// Returns the terrain height(s) a placed scenery instance should render at, per the sampling rule
  /// its <see cref="OpenCobra.OVL.Placement"/> implies.
  /// </summary>
  /// <remarks>
  /// <see cref="OpenCobra.OVL.Placement.FullTile"/>/<see cref="OpenCobra.OVL.Placement.Quarter"/>/
  /// <see cref="OpenCobra.OVL.Placement.Half"/>/<see cref="OpenCobra.OVL.Placement.Corner"/>/
  /// <see cref="OpenCobra.OVL.Placement.PathCenter"/> are single-sample: both returned values are the
  /// average height across the anchor tile's four corners (exactly the anchor corner's height when the
  /// tile is flat, which a multi-tile <c>FullTile</c> footprint is guaranteed to be by
  /// <see cref="TryPlaceScenery"/>). <see cref="OpenCobra.OVL.Placement.PathEdgeInner"/>/
  /// <see cref="OpenCobra.OVL.Placement.PathEdgeOuter"/>/<see cref="OpenCobra.OVL.Placement.PathEdgeJoin"/>/
  /// <see cref="OpenCobra.OVL.Placement.Wall"/> are edge-conforming: the two returned values are the
  /// heights of the two corners bounding <see cref="SceneryPlacement.Rotation"/>'s edge, so the
  /// object's mesh can follow the terrain's slope along that edge instead of sitting at one flat
  /// height.
  /// </remarks>
  public static (ushort Near, ushort Far) GetSceneryHeight(SceneryPlacement placement, SceneryDefinition definition, Terrain terrain) {
    switch (definition.Placement) {
      case Placement.PathEdgeInner:
      case Placement.PathEdgeOuter:
      case Placement.PathEdgeJoin:
      case Placement.Wall: {
        var (c1, c2) = terrain.GetEdgeCornerHeights(placement.TileX, placement.TileY, placement.Rotation);
        return (c1, c2);
      }
      default: {
        var sum = 0;
        foreach (var corner in terrain.GetCorners(placement.TileX, placement.TileY)) sum += corner.Height;
        var average = (ushort)(sum / Terrain.CornersPerTile);
        return (average, average);
      }
    }
  }

  /// <summary>
  /// Returns <paramref name="definition"/>'s footprint, swapping width/height when
  /// <paramref name="rotation"/> is <see cref="Edge.West"/>/<see cref="Edge.East"/> — a 90°/270° turn
  /// relative to the unrotated <see cref="Edge.South"/>/<see cref="Edge.North"/> orientations.
  /// </summary>
  private static (int Width, int Height) GetRotatedFootprint(SceneryDefinition definition, Edge rotation)
    => rotation is Edge.West or Edge.East
      ? (definition.FootprintHeight, definition.FootprintWidth)
      : (definition.FootprintWidth, definition.FootprintHeight);

  /// <summary>
  /// Whether every corner of the <paramref name="width"/>x<paramref name="height"/> tile rectangle
  /// anchored at <paramref name="tileX"/>, <paramref name="tileY"/> is on-grid and shares the same
  /// height.
  /// </summary>
  private static bool IsFootprintLevel(int tileX, int tileY, int width, int height, Terrain terrain) {
    ushort? reference = null;
    for (var dy = 0; dy < height; dy++) {
      for (var dx = 0; dx < width; dx++) {
        var x = tileX + dx;
        var y = tileY + dy;
        if (!terrain.HasTile(x, y)) return false;

        foreach (var corner in terrain.GetCorners(x, y)) {
          reference ??= corner.Height;
          if (corner.Height != reference) return false;
        }
      }
    }
    return true;
  }
}
