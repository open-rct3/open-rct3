// Water Surface Triangle
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenRCT3.Simulation;

/// <summary>Identifies one of the two terrain triangles split along the SE-NW diagonal.</summary>
public enum WaterTerrainTriangle : byte {
  /// <summary>The counter-clockwise (SouthWest, SouthEast, NorthWest) triangle.</summary>
  SouthWest = 0,
  /// <summary>The counter-clockwise (NorthEast, NorthWest, SouthEast) triangle.</summary>
  NorthEast = 1,
}

/// <summary>One tile-triangle fragment covered by a flat <see cref="WaterPool"/>.</summary>
/// <remarks>
/// <see cref="VertexMask"/> uses bits zero through two in the selected triangle's documented
/// counter-clockwise vertex order. A set bit means that terrain vertex is submerged. Zero is not a
/// surface, so valid masks range from one through seven.
/// </remarks>
public readonly record struct WaterSurfaceTriangle {
  public int X { get; }
  public int Y { get; }
  public WaterTerrainTriangle Triangle { get; }
  public byte VertexMask { get; }

  public WaterSurfaceTriangle(
    int x,
    int y,
    WaterTerrainTriangle triangle,
    byte vertexMask
  ) {
    if (!Enum.IsDefined(typeof(WaterTerrainTriangle), triangle))
      throw new ArgumentOutOfRangeException(nameof(triangle));
    if (vertexMask is < 1 or > 7)
      throw new ArgumentOutOfRangeException(nameof(vertexMask));

    X = x;
    Y = y;
    Triangle = triangle;
    VertexMask = vertexMask;
  }
}
