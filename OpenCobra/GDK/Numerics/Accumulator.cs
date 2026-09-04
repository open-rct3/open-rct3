// Accumulator base class for telemetry metrics.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;

namespace OpenCobra.GDK.Numerics;

/// <summary>Accumulates streaming numeric samples into an aggregated value.</summary>
public abstract class Accumulator<T> where T : INumber<T> {
  /// <summary>Gets the current accumulated value.</summary>
  public T Value { get; protected set; } = T.Zero;

  /// <summary>Updates the accumulator with a new sample.</summary>
  /// <param name="sample">The new sample value.</param>
  public abstract void Update(T sample);

  /// <summary>Resets the accumulator state to zero.</summary>
  public abstract void Reset();
}
