// DatTerrainCell
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenRCT3.Serialization;

/// <summary>
/// One terrain tile decoded from an RCT3 DAT file.
/// </summary>
internal readonly struct DatTerrainCell {
  public float SouthWestHeight { get; }
  public float SouthEastHeight { get; }
  public float NorthWestHeight { get; }
  public float NorthEastHeight { get; }
  public byte SurfaceIndex { get; }
  public byte CliffIndex { get; }

  public DatTerrainCell(
    float southWestHeight,
    float southEastHeight,
    float northWestHeight,
    float northEastHeight,
    byte surfaceIndex,
    byte cliffIndex) {
    SouthWestHeight = southWestHeight;
    SouthEastHeight = southEastHeight;
    NorthWestHeight = northWestHeight;
    NorthEastHeight = northEastHeight;
    SurfaceIndex = surfaceIndex;
    CliffIndex = cliffIndex;
  }
}
