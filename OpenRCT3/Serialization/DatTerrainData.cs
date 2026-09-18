// DatTerrainData
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Collections.Generic;

namespace OpenRCT3.Serialization;

/// <summary>
/// Terrain metadata and row-major tile data decoded from an RCT3 DAT file.
/// </summary>
internal sealed class DatTerrainData {
  public int Width { get; }
  public int Height { get; }
  public float OriginX { get; }
  public float OriginY { get; }
  public float TileSizeX { get; }
  public float TileSizeY { get; }
  public IReadOnlyList<DatTerrainCell> Cells { get; }
  public DatWaterManagerData? WaterManager { get; }

  public DatTerrainData(
    int width,
    int height,
    float originX,
    float originY,
    float tileSizeX,
    float tileSizeY,
    DatTerrainCell[] cells,
    DatWaterManagerData? waterManager = null) {
    if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
    if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
    ArgumentNullException.ThrowIfNull(cells);
    if (cells.Length != checked(width * height))
      throw new ArgumentException("Cell count does not match the terrain dimensions.", nameof(cells));

    Width = width;
    Height = height;
    OriginX = originX;
    OriginY = originY;
    TileSizeX = tileSizeX;
    TileSizeY = tileSizeY;
    Cells = Array.AsReadOnly((DatTerrainCell[])cells.Clone());
    WaterManager = waterManager;
  }
}
