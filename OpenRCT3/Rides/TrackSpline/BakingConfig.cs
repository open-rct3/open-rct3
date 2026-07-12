// Baking Configuration for Track Splines
//
// Authors:
//   - Chance Snow <git@chancesnow.me>
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenRCT3.Rides.TrackSpline;

/// <summary>
/// Global configuration for adaptive baking of rail splines.
/// Controls the resolution of baked samples based on curvature and bank-angle changes.
/// </summary>
public static class BakingConfig {
  /// <summary>
  /// Maximum chord-height deviation, as a fraction of track gauge (wheel spacing).
  /// Tighter curves and loops are sampled more densely to stay within this tolerance.
  /// Default: 0.01 (1% of gauge = ~4mm for a standard 4-wheel gauge of ~400mm).
  /// </summary>
  public static float ChordHeightToleranceFraction { get; set; } = 0.01f;

  /// <summary>
  /// Absolute floor for chord-height tolerance in world units, in case gauge fraction rounds down too far.
  /// Default: 10mm (0.01 in RCT3's scale).
  /// </summary>
  public static float ChordHeightToleranceAbsoluteMinimum { get; set; } = 0.01f;

  /// <summary>
  /// Maximum rate of bank-angle change, in radians per unit arc-length.
  /// Corkscrews and twisting sections are sampled densely if bank rotates quickly.
  /// Default: 0.1 rad/unit (~5.7° per unit arc-length).
  /// </summary>
  public static float BankRateThreshold { get; set; } = 0.1f;

  /// <summary>
  /// Standard track gauge (wheel spacing), in world units. Used to scale chord-height tolerance.
  /// Default: 0.4 (400mm, typical for coaster trains).
  /// </summary>
  public static float StandardGauge { get; set; } = 0.4f;

  /// <summary>
  /// Compute the effective chord-height tolerance for baking, given a gauge.
  /// </summary>
  public static float ComputeChordHeightTolerance(float gauge = 0.4f) {
    var fractionBased = gauge * ChordHeightToleranceFraction;
    return Math.Max(fractionBased, ChordHeightToleranceAbsoluteMinimum);
  }
}
