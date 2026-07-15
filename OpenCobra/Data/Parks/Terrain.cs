// Decodes the corner-height grid from a saved park's `RCT3Terrain.EngineTerrain` field.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
namespace OpenCobra.Data.Parks;

/// <summary>
/// One terrain grid corner decoded from a saved park's <c>RCT3Terrain.EngineTerrain</c> field.
/// </summary>
/// <remarks>
/// Corners repeat on a fixed 12-byte record; only the leading 4 bytes (<see cref="Height"/>) are
/// decoded so far.
/// </remarks>
public readonly record struct TerrainCorner(
  /// <summary>
  /// Corner height in meters. A single click of the "Adjust Terrain Tiles" panel's "Snap terrain
  /// tiles in increments for rides and scenery" tool changes this value by exactly 1.0.
  /// </summary>
  float Height,
  /// <summary>The remaining 8 bytes of this corner's 12-byte record - not yet decoded.</summary>
  byte[] Unknown
);

/// <summary>Decodes the corner-height grid from a saved park's <c>RCT3Terrain.EngineTerrain</c> field.</summary>
public static class Terrain {
  private const string TerrainEntryName = "RCT3Terrain";
  private const string FieldName = "EngineTerrain";
  private const int CornerRecordSize = 12;

  /// <summary>
  /// Byte offset from the start of the <c>EngineTerrain</c> blob to the first corner record.
  /// </summary>
  /// <remarks>
  /// Not independently confirmed - derived from the only sample captured so far (a fixed
  /// 393,234-byte blob) by finding the header size that makes the diff-confirmed corner offsets
  /// divide evenly by <see cref="CornerRecordSize"/>. Needs re-deriving against a park with a
  /// different map size before this can be trusted.
  /// </remarks>
  private const int AssumedHeaderSize = 6;

  /// <summary>
  /// Decodes every corner record in the given park's terrain grid, in on-disk order.
  /// </summary>
  /// <remarks>
  /// Corner count, and therefore grid dimensions, are derived from the blob's total size divided by
  /// <see cref="CornerRecordSize"/> after <see cref="AssumedHeaderSize"/> - not independently
  /// confirmed; see that field's doc.
  /// </remarks>
  public static IReadOnlyList<TerrainCorner> ExtractCorners(Dat dat) {
    var data = GetEngineTerrainBytes(dat);
    var cornerCount = (data.Length - AssumedHeaderSize) / CornerRecordSize;
    var corners = new List<TerrainCorner>(cornerCount);

    for (var i = 0; i < cornerCount; i++) {
      var offset = AssumedHeaderSize + i * CornerRecordSize;
      var height = BitConverter.ToSingle(data, offset);
      var unknown = data.AsSpan(offset + 4, CornerRecordSize - 4).ToArray();
      corners.Add(new TerrainCorner(height, unknown));
    }

    return corners;
  }

  private static byte[] GetEngineTerrainBytes(Dat dat) =>
    dat.FirstByName(TerrainEntryName).FirstByName(FieldName).AsOpaque().Data;
}
