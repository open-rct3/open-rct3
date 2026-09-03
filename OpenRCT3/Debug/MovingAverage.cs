// Calculates a moving average over a sliding window.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using System.Numerics;

namespace OpenRCT3.Debug;

/// <summary>Calculates a moving average over a sliding duration window.</summary>
public class MovingAverage<T> : Accumulator<T> where T : IFloatingPoint<T> {
  /// <summary>Default window duration for the moving average.</summary>
  public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(1.0);

  /// <summary>Defines a custom accumulation strategy for calculating the moving average.</summary>
  public delegate T AccumulationFunc(IReadOnlyList<T> samples, T currentAverage, T newSample);

  private readonly List<T> samples = [];
  private readonly AccumulationFunc accumulate;

  public MovingAverage() : this(DefaultWindow, null) {}

  public MovingAverage(TimeSpan window, AccumulationFunc? accumulate = null) {
    if (window <= TimeSpan.Zero)
      throw new ArgumentOutOfRangeException(nameof(window), "Window must be greater than zero.");
    Window = window;
    this.accumulate = accumulate ?? LinearSum;
  }

  /// <summary>Gets the window duration of the moving average.</summary>
  public TimeSpan Window { get; }

  /// <summary>Gets the recorded sample history.</summary>
  public IReadOnlyList<T> Samples => samples;

  public override void Update(T sample) {
    samples.Add(sample);
    Value = accumulate(samples, Value, sample);
  }

  public override void Reset() {
    samples.Clear();
    Value = T.Zero;
  }

  private static T LinearSum(IReadOnlyList<T> samples, T currentAverage, T newSample) {
    if (samples.Count == 0) return T.Zero;
    var sum = T.Zero;
    for (var i = 0; i < samples.Count; i++) {
      sum += samples[i];
    }
    return sum / T.CreateChecked(samples.Count);
  }

  /// <summary>Creates an exponential weighting accumulation function with the given alpha smoothing factor.</summary>
  public static AccumulationFunc Exponential(T alpha) =>
    (samples, currentAverage, newSample) =>
      samples.Count <= 1 ? newSample : (alpha * newSample) + ((T.One - alpha) * currentAverage);
}
