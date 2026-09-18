// Water Manager Loader
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenRCT3.Serialization;
using System.Linq;

namespace OpenRCT3.Simulation;

/// <summary>Converts decoded RCT3 WaterManager records into simulation pools.</summary>
internal static class WaterManagerLoader {
  public static void Load(Park park, Terrain terrain, DatWaterManagerData manager) {
    ArgumentNullException.ThrowIfNull(park);
    ArgumentNullException.ThrowIfNull(terrain);
    ArgumentNullException.ThrowIfNull(manager);
    if (manager.Width != terrain.Width || manager.Height != terrain.Height)
      throw new InvalidDataException("WaterManager dimensions do not match the terrain grid.");

    foreach (var sourcePool in manager.Pools) {
      if (sourcePool.Records.Count == 0) continue;

      var triangles = sourcePool.Records
        .Select(record => new WaterSurfaceTriangle(
          record.X,
          record.Y,
          (WaterTerrainTriangle)record.Triangle,
          record.VertexMask))
        .ToArray();
      var isOcean = triangles.Any(triangle =>
        triangle.X == 0 || triangle.Y == 0 ||
        triangle.X == terrain.Width - 1 || triangle.Y == terrain.Height - 1);
      int height;
      try {
        height = Terrain.WorldZToCornerHeight(sourcePool.Height);
      }
      catch (ArgumentOutOfRangeException exception) {
        throw new InvalidDataException("Decoded water height exceeds the simulation range.", exception);
      }

      if (!park.TryPlaceWaterTriangles(triangles, height, terrain, isOcean))
        throw new InvalidDataException("Decoded water pools overlap or exceed the terrain grid.");
    }
  }
}
