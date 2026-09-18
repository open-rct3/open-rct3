// Scenery Definition
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using OpenCobra.OVL;

namespace OpenRCT3.Simulation;

/// <summary>
/// A placeable object's registry entry: footprint shape and animation category, decoupled from
/// whether its mesh/texture has actually been resolved yet. Looked up by raw OVL <c>sid</c>/<c>svd</c>
/// symbol name via <see cref="SceneryRegistry"/> — there is no separate internal ID.
/// </summary>
/// <remarks>
/// Carries no scale field. RCT3's scenery catalog is not runtime-scalable: size variation always comes
/// from picking a different object definition (e.g. a distinct "large tree" OVL entry with its own
/// mesh), never a multiplier applied to a shared mesh.
/// </remarks>
public struct SceneryDefinition {
  /// <summary>
  /// The footprint/snap-position and height-sampling rule for this object, sourced from the <c>sid</c>
  /// entry's <c>sizeflag</c> field. See <see cref="OpenCobra.OVL.Placement"/>.
  /// </summary>
  public Placement Placement;

  /// <summary>The animation category this object uses, if any. See <see cref="Simulation.AnimationKind"/>.</summary>
  public AnimationKind AnimationKind;

  /// <summary>
  /// The object's footprint width in tiles, before rotation. Only meaningful when
  /// <see cref="Placement"/> is <see cref="OpenCobra.OVL.Placement.FullTile"/>; every other
  /// <see cref="OpenCobra.OVL.Placement"/> value places at a fixed sub-tile offset derived from the enum
  /// value itself, not a footprint size. Defaults to 1 (single-tile).
  /// </summary>
  public int FootprintWidth;

  /// <summary>
  /// The object's footprint height (depth) in tiles, before rotation. See
  /// <see cref="FootprintWidth"/> for when this applies. Defaults to 1 (single-tile).
  /// </summary>
  public int FootprintHeight;

  public SceneryDefinition(Placement placement, AnimationKind animationKind = AnimationKind.None, int footprintWidth = 1, int footprintHeight = 1) {
    Placement = placement;
    AnimationKind = animationKind;
    FootprintWidth = footprintWidth;
    FootprintHeight = footprintHeight;
  }
}
