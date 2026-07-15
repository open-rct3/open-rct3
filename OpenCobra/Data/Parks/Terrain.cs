// Decodes the corner-height grid from a saved park's `RCT3Terrain.EngineTerrain` field.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
namespace OpenCobra.Data.Parks;

/// <summary>One tile decoded from a saved park's <c>RCT3Terrain.EngineTerrain</c> field.</summary>
/// <remarks>
/// Each tile owns two adjacent 12-byte records holding four independently-steppable corner
/// heights: the first record packs three back-to-back <c>float32</c>s (<see cref="SouthEast"/>@0,
/// <see cref="SouthWest"/>@4, <see cref="NorthEast"/>@8); the second holds <see cref="NorthWest"/>
/// (<c>float32</c>@0) followed by <see cref="SurfaceType"/>. Named to match
/// <see cref="OpenRCT3.Simulation.TerrainCornerSlot"/>'s SW/SE/NW/NE convention; the game exposes
/// no in-editor coordinate system or compass, so only each corner's adjacency to the other three
/// (which pairs share a "near"/camera-facing side vs. a "left"/"right" side, per the fixture
/// evidence in <c>rct3-terrain-data-layout.md</c>) is confirmed from bytes, not an independently
/// derived absolute world-space direction. All four heights default to <c>0.0</c>.
/// </remarks>
public readonly record struct TerrainTile(
  /// <summary>South-East corner height (first record, byte offset 0).</summary>
  float SouthEast,
  /// <summary>South-West corner height (first record, byte offset 4).</summary>
  float SouthWest,
  /// <summary>North-East corner height (first record, byte offset 8).</summary>
  float NorthEast,
  /// <summary>North-West corner height (second record, byte offset 0).</summary>
  float NorthWest,
  /// <summary>Terrain surface/texture type index (second record, byte offset 4).</summary>
  byte SurfaceType,
  /// <summary>Second record's remaining bytes (offsets 5-11, 7 bytes) - not yet decoded.</summary>
  byte[] Unknown
);

/// <summary>
/// A saved park's terrain grid: its declared tile dimensions plus every decoded tile.
/// </summary>
/// <remarks>
/// <see cref="Width"/>/<see cref="Height"/> vary per park. Parks aren't always the default
/// 128x128 tiles, and aren't always square.
/// </remarks>
public readonly record struct TerrainGrid(
  /// <summary>The grid's width in tiles.</summary>
  byte Width,
  /// <summary>The grid's height in tiles.</summary>
  byte Height,
  /// <summary>Every decoded tile, in on-disk order (<see cref="Width"/> x <see cref="Height"/> entries).</summary>
  IReadOnlyList<TerrainTile> Tiles
);

/// <summary>Decodes the corner-height grid from a saved park's <c>RCT3Terrain.EngineTerrain</c> field.</summary>
public static class Terrain {
  private const string TerrainEntryName = "RCT3Terrain";
  private const string FieldName = "EngineTerrain";

  /// <summary>Byte offset of the grid's declared width (in tiles) within the <c>EngineTerrain</c> blob.</summary>
  private const int WidthOffset = 0;
  /// <summary>Byte offset of the grid's declared height (in tiles) within the <c>EngineTerrain</c> blob.</summary>
  private const int HeightOffset = 1;

  /// <summary>Size, in bytes, of the mini-header carrying <see cref="WidthOffset"/>/<see cref="HeightOffset"/>.</summary>
  private const int MiniHeaderSize = 6;
  /// <summary>Size, in bytes, of the single record immediately following the mini-header - purpose not yet decoded.</summary>
  private const int PreambleRecordSize = 12;
  /// <summary>Size, in bytes, of one of a tile's two records.</summary>
  private const int RecordSize = 12;
  /// <summary>
  /// Byte offset from the start of the <c>EngineTerrain</c> blob to the first tile's first record:
  /// <see cref="MiniHeaderSize"/> + <see cref="PreambleRecordSize"/>.
  /// </summary>
  private const int TileArrayOffset = MiniHeaderSize + PreambleRecordSize;

  /// <summary>Decodes the given park's terrain grid: its declared dimensions and every tile.</summary>
  public static TerrainGrid Extract(Dat dat) {
    var data = GetEngineTerrainBytes(dat);
    var width = data[WidthOffset];
    var height = data[HeightOffset];
    return new TerrainGrid(width, height, ExtractTiles(data, width, height));
  }

  /// <summary>Decodes every tile in the given park's terrain grid, in on-disk order.</summary>
  public static IReadOnlyList<TerrainTile> ExtractTiles(Dat dat) {
    var data = GetEngineTerrainBytes(dat);
    return ExtractTiles(data, data[WidthOffset], data[HeightOffset]);
  }

  private static List<TerrainTile> ExtractTiles(byte[] data, byte width, byte height) {
    var tileCount = width * height;
    var tiles = new List<TerrainTile>(tileCount);

    for (var i = 0; i < tileCount; i++) {
      var pairOffset = TileArrayOffset + i * (RecordSize * 2);
      var southEast = BitConverter.ToSingle(data, pairOffset);
      var southWest = BitConverter.ToSingle(data, pairOffset + 4);
      var northEast = BitConverter.ToSingle(data, pairOffset + 8);

      var secondRecordOffset = pairOffset + RecordSize;
      var northWest = BitConverter.ToSingle(data, secondRecordOffset);
      var surfaceType = data[secondRecordOffset + 4];
      var unknown = data.AsSpan(secondRecordOffset + 5, RecordSize - 5).ToArray();

      tiles.Add(new TerrainTile(southEast, southWest, northEast, northWest, surfaceType, unknown));
    }

    return tiles;
  }

  private static byte[] GetEngineTerrainBytes(Dat dat) =>
    dat.FirstByName(TerrainEntryName).FirstByName(FieldName).AsOpaque().Data;
}
