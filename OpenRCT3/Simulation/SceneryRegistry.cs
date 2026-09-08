// Scenery Registry
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using System.Collections.Generic;

namespace OpenRCT3.Simulation;

/// <summary>
/// Looks up a <see cref="SceneryDefinition"/> by raw OVL <c>sid</c>/<c>svd</c> symbol name.
/// </summary>
/// <remarks>
/// Independent of <see cref="Park"/>: this is a catalog of what objects <i>can</i> be placed, sourced
/// from OVL content, not per-park placed-instance data (see <see cref="Park.SceneryPlacements"/>). OVL
/// symbol names are content-addressed by the original game data, not by our tooling, so they're used
/// directly as the key rather than inventing a separate internal ID layer.
/// </remarks>
public class SceneryRegistry {
  private readonly Dictionary<string, SceneryDefinition> _definitions = [];

  /// <summary>Registers or replaces the <see cref="SceneryDefinition"/> for <paramref name="objectKey"/>.</summary>
  public void Register(string objectKey, SceneryDefinition definition) => _definitions[objectKey] = definition;

  /// <summary>Looks up the <see cref="SceneryDefinition"/> registered for <paramref name="objectKey"/>, if any.</summary>
  public bool TryGetDefinition(string objectKey, out SceneryDefinition definition)
    => _definitions.TryGetValue(objectKey, out definition);
}
