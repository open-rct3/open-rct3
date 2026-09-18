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
/// future size-1 single-corner drag) don't need a second picking pass.
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
  /// The DAT physical diagonal split is SouthEast-to-NorthWest: (SW, SE, NW) and (NE, NW, SE).
  /// <see cref="TerrainMeshBuilder.AddTopFace"/> reverses that winding for Y-up rendering.
  /// </summary>
  private static readonly (TerrainCornerSlot A, TerrainCornerSlot B, TerrainCornerSlot C)[] Triangles = [
    (TerrainCornerSlot.SouthWest, TerrainCornerSlot.SouthEast, TerrainCornerSlot.NorthWest),
    (TerrainCornerSlot.NorthEast, TerrainCornerSlot.NorthWest, TerrainCornerSlot.SouthEast),
  ];

  /// <summary>
  /// Marches <paramref name="ray"/> in the smaller decoded tile dimension, testing each stepped-into
  /// tile's two corner-triangles for intersection.
  /// </summary>
  /// <param name="ray">The world-space ray to pick with, e.g. from <see cref="Camera.Unproject"/>.</param>
  /// <param name="terrain">The heightfield to hit-test against.</param>
  /// <param name="maxSteps">
  /// The step budget - callers should derive this from the camera's view distance (e.g.
  /// <c>Camera.MaxDistance ?? distance</c>, divided by the terrain's smaller tile dimension) so the march can't
  /// run unbounded, but also can't give up before it could plausibly reach the ground.
  /// </param>
  /// <returns>The first hit tile/triangle, or <c>null</c> if the march exits the grid or step budget
  /// with no hit.</returns>
  public static TilePickResult? TryPickTile(Ray ray, Terrain terrain, int maxSteps) {
    var enteredGrid = false;
    var stepLength = MathF.Min(terrain.TileSize.X, terrain.TileSize.Y);
    for (var step = 0; step <= maxSteps; step++) {
      var point = ray.Origin + (ray.Direction * (step * stepLength));
      var tileX = Convert.ToInt32(MathF.Floor(
        (point.X - terrain.Origin.X) / terrain.TileSize.X));
      var tileY = Convert.ToInt32(MathF.Floor(
        (point.Z - terrain.Origin.Y) / terrain.TileSize.Y));

      if (!terrain.HasTile(tileX, tileY)) {
        if (enteredGrid) return null;
        continue;
      }
      enteredGrid = true;

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
