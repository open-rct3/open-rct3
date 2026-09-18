// TerrainCameraFraming
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Numerics;

namespace OpenRCT3.Simulation;

/// <summary>Computes camera framing that contains the full rendered terrain.</summary>
public static class TerrainCameraFraming {
  /// <summary>
  /// Extra distance beyond the bounding-sphere minimum for the camera's 60° vertical field of view.
  /// </summary>
  /// <remarks>
  /// The terrain bounds' full 3D diagonal is the exact distance needed to contain its bounding sphere
  /// at a 30° half-FOV. Ten percent keeps the mesh clear of the clip-space edge at landscape aspect
  /// ratios without pulling the camera unnecessarily far away.
  /// </remarks>
  public const float DistanceMargin = 1.1f;

  /// <summary>Calculates the camera target and distance for the complete OOB-inclusive terrain.</summary>
  public static (Vector3 Target, float Distance) Calculate(Terrain terrain) {
    ArgumentNullException.ThrowIfNull(terrain);

    var minHeight = int.MaxValue;
    var maxHeight = int.MinValue;
    for (var tileY = 0; tileY < terrain.Height; tileY++) {
      for (var tileX = 0; tileX < terrain.Width; tileX++) {
        foreach (var corner in terrain.GetCorners(tileX, tileY)) {
          minHeight = Math.Min(minHeight, corner.Height);
          maxHeight = Math.Max(maxHeight, corner.Height);
        }
      }
    }

    var (minXY, maxXY) = terrain.Bounds;
    var min = new Vector3(minXY.X, Terrain.CornerHeightToWorldY(minHeight), minXY.Y);
    var max = new Vector3(maxXY.X, Terrain.CornerHeightToWorldY(maxHeight), maxXY.Y);
    var target = (min + max) * 0.5f;
    var distance = Vector3.Distance(min, max) * DistanceMargin;
    return (target, distance);
  }
}
