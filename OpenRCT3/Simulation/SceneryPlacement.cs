// Scenery Placement
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
namespace OpenRCT3.Simulation;

/// <summary>
/// A single placed scenery instance: object reference, anchor grid position, and orientation. Stored
/// directly on <see cref="Park.SceneryPlacements"/> — no wrapper "layer" type.
/// </summary>
/// <remarks>
/// Stores no Z. Height is derived from a <see cref="Terrain"/> query each time it's needed (see
/// <see cref="Park.GetSceneryHeight"/>) rather than cached, the same treatment terrain height gets
/// generally. This is one-directional: scenery reads terrain height but does not constrain later
/// terrain edits the way ride footprints do.
/// </remarks>
public struct SceneryPlacement {
  /// <summary>The raw OVL <c>sid</c>/<c>svd</c> symbol name identifying the placed object.</summary>
  public string ObjectKey;

  /// <summary>The anchor tile's X index in the OOB-inclusive grid.</summary>
  public int TileX;

  /// <summary>The anchor tile's Y index in the OOB-inclusive grid.</summary>
  public int TileY;

  /// <summary>
  /// The object's quarter-turn orientation (0/90/180/270, per RCT series convention), reusing
  /// <see cref="Simulation.Edge"/> as the four-state rotation value.
  /// </summary>
  /// <remarks>
  /// For edge-mounted <see cref="OpenCobra.OVL.Placement"/> values (<c>PathEdgeInner</c>/
  /// <c>PathEdgeOuter</c>/<c>PathEdgeJoin</c>/<c>Wall</c>), this directly names the tile edge the
  /// object sits on. For <c>FullTile</c> multi-tile footprints, it's a plain 4-state orientation: the
  /// South/North pair leaves <see cref="SceneryDefinition.FootprintWidth"/>/
  /// <see cref="SceneryDefinition.FootprintHeight"/> as-is, the West/East pair swaps them — this holds
  /// regardless of rotation winding direction, since swap-or-not only depends on which axis pair the
  /// object's local "forward" now aligns with.
  /// </remarks>
  public Edge Rotation;

  public SceneryPlacement(string objectKey, int tileX, int tileY, Edge rotation = Edge.South) {
    ObjectKey = objectKey;
    TileX = tileX;
    TileY = tileY;
    Rotation = rotation;
  }
}
