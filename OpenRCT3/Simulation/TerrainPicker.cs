// Terrain Picker
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using OpenCobra.GDK;
using System.Numerics;

namespace OpenRCT3.Simulation;

/// <summary>
/// The result of a successful <see cref="TerrainPicker.TryPickTile"/> hit test.
/// </summary>
/// <param name="TileX">The hit tile's X index in the OOB-inclusive grid.</param>
/// <param name="TileY">The hit tile's Y index in the OOB-inclusive grid.</param>
/// <param name="Point">
/// The exact world-space intersection point - not just the tile snap - so corner-precise tools (e.g. a
/// future size-1 single-corner drag, per <c>.agents/research/terrain-tools.md</c>) don't need a second
/// picking pass.
/// </param>
/// <param name="A">The first corner of the hit triangle.</param>
/// <param name="B">The second corner of the hit triangle.</param>
/// <param name="C">The third corner of the hit triangle.</param>
public readonly record struct TilePickResult(
  int TileX,
  int TileY,
  Vector3 Point,
  TerrainCornerSlot A,
  TerrainCornerSlot B,
  TerrainCornerSlot C);

/// <summary>
/// Grid-stepped heightfield ray picking against a <see cref="Terrain"/>, matching how era-appropriate
/// (circa-2004) engines picked against heightfields rather than raycasting the full render mesh.
/// </summary>
public static class TerrainPicker {
  /// <summary>
  /// The fixed diagonal split <see cref="TerrainMeshBuilder.AddTopFace"/> emits per tile: two
  /// triangles, (SW, SE, NE) then (SW, NE, NW).
  /// </summary>
  private static readonly (TerrainCornerSlot A, TerrainCornerSlot B, TerrainCornerSlot C)[] Triangles = [
    (TerrainCornerSlot.SouthWest, TerrainCornerSlot.SouthEast, TerrainCornerSlot.NorthEast),
    (TerrainCornerSlot.SouthWest, TerrainCornerSlot.NorthEast, TerrainCornerSlot.NorthWest),
  ];

  /// <summary>
  /// Marches <paramref name="ray"/> in <see cref="Park.TileSize"/> increments, testing each stepped-into
  /// tile's two corner-triangles for intersection.
  /// </summary>
  /// <param name="ray">The world-space ray to pick with, e.g. from <see cref="Camera.Unproject"/>.</param>
  /// <param name="terrain">The heightfield to hit-test against.</param>
  /// <param name="maxSteps">
  /// The step budget - callers should derive this from the camera's view distance (e.g.
  /// <c>Camera.MaxDistance ?? distance</c>, divided by <see cref="Park.TileSize"/>) so the march can't
  /// run unbounded, but also can't give up before it could plausibly reach the ground.
  /// </param>
  /// <returns>The first hit tile/triangle, or <c>null</c> if the march exits the grid or step budget
  /// with no hit.</returns>
  public static TilePickResult? TryPickTile(Ray ray, Terrain terrain, int maxSteps) {
    for (var step = 0; step <= maxSteps; step++) {
      var point = ray.Origin + (ray.Direction * (step * Park.TileSize));
      var tileX = (int)MathF.Floor((point.X / Park.TileSize) + (terrain.Width / 2f));
      var tileY = (int)MathF.Floor(point.Y / Park.TileSize);

      if (!terrain.HasTile(tileX, tileY)) return null;

      foreach (var (a, b, c) in Triangles) {
        var v0 = TerrainMeshBuilder.CornerPosition(terrain, tileX, tileY, a);
        var v1 = TerrainMeshBuilder.CornerPosition(terrain, tileX, tileY, b);
        var v2 = TerrainMeshBuilder.CornerPosition(terrain, tileX, tileY, c);

        if (ray.Intersects(v0, v1, v2, out var hitPoint))
          return new TilePickResult(tileX, tileY, hitPoint, a, b, c);
      }
    }

    return null;
  }
}
