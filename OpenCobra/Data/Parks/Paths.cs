// Paths
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
namespace OpenCobra.Data.Parks;

/// <summary>
/// An at-grade path tile, decoded from a saved park's top-level <c>PathTile</c> entries. Unlike
/// terrain, path tiles aren't packed into an opaque blob - each tile is its own regular DAT
/// struct entry, already fully decoded by <see cref="Dat"/>.
/// </summary>
public readonly record struct PathTile(
  byte ColIndex,
  byte RowIndex,
  /// <summary>Always 0 in every sample captured so far - meaning not yet confirmed.</summary>
  byte Direction,
  /// <summary>Path theme/type index. Always 0 in every sample captured so far.</summary>
  byte PathType,
  /// <summary>Reference to the underlying terrain surface this tile sits on.</summary>
  ulong Surface,
  /// <summary>Always 255 (a "none" sentinel, unconfirmed) in every sample captured so far.</summary>
  byte SurfaceType
);

/// <summary>
/// A raised path tile, decoded from a saved park's top-level <c>PathFlying</c> entries - a
/// distinct representation from <see cref="PathTile"/>, not a variant of it: raised tiles carry
/// their own height/slope fields and reference a companion <c>SceneryItem</c> entry for the 3D
/// support piece rendered under them.
/// </summary>
public readonly record struct PathFlyingTile(
  byte ColIndex,
  byte RowIndex,
  byte Direction,
  byte PathType,
  /// <summary>Always -1 in the single sample captured so far - meaning not yet confirmed.</summary>
  int BaseHeight,
  /// <summary>Height in path-height steps above <see cref="BaseHeight"/>.</summary>
  int QuantisedHeight,
  /// <summary>0 (flat) in the single sample captured so far; other values not yet observed.</summary>
  byte SlopeType,
  ulong Surface,
  byte SurfaceType,
  bool UndergroundFlag,
  /// <summary>Reference to the companion <c>SceneryItem</c> entry for this tile's 3D support piece.</summary>
  ulong SceneryItem
);

/// <summary>Decodes at-grade and raised path tiles from a loaded saved-park <see cref="Dat"/>.</summary>
public static class Paths {
  private const string PathTileEntryName = "PathTile";
  private const string PathFlyingEntryName = "PathFlying";

  /// <summary>Decodes every at-grade path tile in the park, in on-disk order.</summary>
  public static IReadOnlyList<PathTile> ExtractAtGrade(Dat dat) =>
    [.. dat.ByName(PathTileEntryName).Select(ReadPathTile)];

  /// <summary>Decodes every raised path tile in the park, in on-disk order.</summary>
  public static IReadOnlyList<PathFlyingTile> ExtractRaised(Dat dat) =>
    [.. dat.ByName(PathFlyingEntryName).Select(ReadPathFlying)];

  private static PathTile ReadPathTile(Entry entry) => new(
    entry.FirstByName("ColIndex").AsUInt8(),
    entry.FirstByName("RowIndex").AsUInt8(),
    entry.FirstByName("Direction").AsUInt8(),
    entry.FirstByName("PathType").AsUInt8(),
    entry.FirstByName("Surface").AsRef(),
    entry.FirstByName("SurfaceType").AsUInt8()
  );

  private static PathFlyingTile ReadPathFlying(Entry entry) => new(
    entry.FirstByName("ColIndex").AsUInt8(),
    entry.FirstByName("RowIndex").AsUInt8(),
    entry.FirstByName("Direction").AsUInt8(),
    entry.FirstByName("PathType").AsUInt8(),
    entry.FirstByName("BaseHeight").AsInt32(),
    entry.FirstByName("QuantisedHeight").AsInt32(),
    entry.FirstByName("SlopeType").AsUInt8(),
    entry.FirstByName("Surface").AsRef(),
    entry.FirstByName("SurfaceType").AsUInt8(),
    entry.FirstByName("UndergroundFlag").AsBool(),
    entry.FirstByName("SceneryItem").AsRef()
  );
}
