// DatWaterRecord
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenRCT3.Serialization;

/// <summary>
/// One row-major water-triangle record decoded from an RCT3 DAT file.
/// </summary>
internal readonly struct DatWaterRecord {
  public byte X { get; }
  public byte Y { get; }
  public byte Triangle { get; }
  public byte VertexMask { get; }

  public DatWaterRecord(byte x, byte y, byte triangle, byte vertexMask) {
    X = x;
    Y = y;
    Triangle = triangle;
    VertexMask = vertexMask;
  }
}
