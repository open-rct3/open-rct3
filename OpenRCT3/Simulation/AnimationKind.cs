// Animation Kind
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
namespace OpenRCT3.Simulation;

/// <summary>
/// The broad category of animation a scenery object uses, if any. Carried on
/// <see cref="SceneryDefinition"/> so the registry is usable before animated rendering exists.
/// </summary>
/// <remarks>
/// Provisional: OVL <c>sid</c> sound references, animation scripts, and flat-ride-specific <c>anr</c>
/// params hint at categories that may not map cleanly onto <see cref="Looping"/>/<see cref="Triggered"/>
/// once decoded (see <c>.agents/plans/features/scenery-placement-registry.md</c>). A future kind can be
/// added without a breaking schema change; no keyframe/timeline model is in scope here.
/// </remarks>
public enum AnimationKind {
  /// <summary>Static mesh, no animation.</summary>
  None = 0,

  /// <summary>Continuously animates (e.g. windmills, fountains).</summary>
  Looping = 1,

  /// <summary>Animates in response to an event rather than continuously (e.g. ride events, doors).</summary>
  Triggered = 2,

  /// <summary>Bone/morph-target animation, e.g. animal meshes.</summary>
  MorphTarget = 3,
}
