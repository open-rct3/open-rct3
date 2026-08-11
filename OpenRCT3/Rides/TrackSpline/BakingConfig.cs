// Baking Configuration for Track Splines
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
  /// Default: 0.05 (5% of gauge = ~20mm for a standard 4-wheel gauge of ~400mm).
  /// </summary>
  public static float ChordHeightToleranceFraction { get; set; } = 0.05f;

  /// <summary>
  /// Absolute floor for chord-height tolerance in world units, in case gauge fraction rounds down too far.
  /// Default: 20mm (0.02 in RCT3's scale).
  /// </summary>
  public static float ChordHeightToleranceAbsoluteMinimum { get; set; } = 0.02f;

  /// <summary>
  /// Maximum rate of bank-angle change, in radians per unit arc-length.
  /// Corkscrews and twisting sections are sampled densely if bank rotates quickly.
  /// Default: 0.2 rad/unit (~11.5° per unit arc-length).
  /// </summary>
  public static float BankRateThreshold { get; set; } = 0.2f;

  /// <summary>
  /// Standard track gauge (wheel spacing), in world units. Used to scale chord-height tolerance.
  /// Default: 0.4 (400mm, typical for coaster trains).
  /// </summary>
  public static float StandardGauge { get; set; } = 0.4f;

  /// <summary>
  /// <para>
  /// Fixed sample count for the forward-differencing arc-length lookup table built per curve segment.
  /// </para>
  /// <para>
  /// Default: 32
  /// </para>
  /// </summary>
  /// <remarks>
  /// Higher values cost more per segment at bake time (<c>O(n)</c>) in exchange for a finer-grained arc-length
  /// approximation; the adaptive chord-height/bank-rate subdivision (above) is what actually controls
  /// visual sample density, so this only needs to be fine enough that the LUT itself isn't the error source.
  /// </remarks>
  public static ushort ArcLengthSampleCount { get; set; } = 32;

  /// <summary>
  /// Compute the effective chord-height tolerance for baking, given a gauge.
  /// </summary>
  public static float ComputeChordHeightTolerance(float gauge = 0.4f) {
    var fractionBased = gauge * ChordHeightToleranceFraction;
    return Math.Max(fractionBased, ChordHeightToleranceAbsoluteMinimum);
  }
}
