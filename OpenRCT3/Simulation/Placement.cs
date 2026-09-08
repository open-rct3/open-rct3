// Placement
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
namespace OpenRCT3.Simulation;

/// <summary>
/// The placement/footprint shape a scenery object's <c>sid</c> entry declares via its <c>sizeflag</c>
/// field (<c>rct3-importer</c>'s <c>SIZE_*</c> defines). Drives both where an object snaps within a
/// tile and how its height is sampled — see <see cref="SceneryDefinition"/>.
/// </summary>
public enum Placement {
  /// <summary>
  /// Occupies one whole tile, or a rectangular run of tiles for multi-tile objects (see
  /// <see cref="SceneryDefinition.FootprintWidth"/>/<see cref="SceneryDefinition.FootprintHeight"/>).
  /// Single-sample height; a multi-tile footprint additionally requires a level pad (see
  /// <see cref="Park.TryPlaceScenery"/>).
  /// </summary>
  FullTile = 0,

  /// <summary>Mounted on the inner side of a path edge (e.g. path-adjacent bushes/flowers). Edge-conforming height.</summary>
  PathEdgeInner = 1,

  /// <summary>Mounted on the outer side of a path edge (e.g. fences). Edge-conforming height.</summary>
  PathEdgeOuter = 2,

  /// <summary>Mounted on a tile edge, off the path grid (e.g. walls). Edge-conforming height.</summary>
  Wall = 3,

  /// <summary>Occupies one quarter sub-cell of a tile. Single-sample height.</summary>
  Quarter = 4,

  /// <summary>Occupies one half sub-cell of a tile. Single-sample height.</summary>
  Half = 5,

  /// <summary>Centered on a path tile (e.g. lamps, bins). Single-sample height.</summary>
  PathCenter = 6,

  /// <summary>Occupies a tile corner. Single-sample height.</summary>
  Corner = 7,

  /// <summary>Mounted where two path edges join (e.g. corner fence posts). Edge-conforming height.</summary>
  PathEdgeJoin = 8,
}
