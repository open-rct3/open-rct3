// Manages fixed-timestep simulation accumulation and lag clamping per the Game Loop pattern.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

namespace OpenCobra.GDK.Numerics;

/// <summary>
/// Accumulates elapsed wall-clock time and dispenses discrete fixed-timestep ticks for deterministic simulation updates.
/// </summary>
/// <remarks>
/// Implements the Fixed Timestep Update with Variable Rendering pattern (see https://gameprogrammingpatterns.com/game-loop.html).
/// To prevent the "spiral of death" where slow frames accumulate more simulation work than can be processed, accumulated
/// lag is clamped to <c>StepRate * MaxTicksPerFrame</c>.
/// </remarks>
public class FixedTimestepAccumulator(TimeSpan stepRate, int maxTicksPerFrame = 8) {
  public TimeSpan StepRate { get; set; } = stepRate;
  public int MaxTicksPerFrame { get; set; } = maxTicksPerFrame;
  public TimeSpan Lag { get; private set; } = TimeSpan.Zero;

  /// <summary>
  /// Gets the normalized interpolation fraction (between 0.0 inclusive and 1.0 exclusive) representing the
  /// residual progress towards the next simulation step, used for render-state interpolation.
  /// </summary>
  public double Interpolation =>
    StepRate > TimeSpan.Zero ? Math.Clamp(Lag.TotalMilliseconds / StepRate.TotalMilliseconds, 0.0, 1.0) : 0.0;

  /// <summary>
  /// Accumulates newly elapsed wall-clock time into the lag balance, clamping to prevent a spiral of death.
  /// </summary>
  /// <param name="elapsed">The wall-clock duration that elapsed during the last frame.</param>
  public void Accumulate(TimeSpan elapsed) {
    if (elapsed <= TimeSpan.Zero) return;

    Lag += elapsed;
    var maxLag = TimeSpan.FromTicks(StepRate.Ticks * MaxTicksPerFrame);
    if (Lag > maxLag) {
      Lag = maxLag;
    }
  }

  /// <summary>
  /// Consumes one fixed simulation timestep from the accumulated lag if available.
  /// </summary>
  /// <returns><c>true</c> if a tick was consumed; otherwise, <c>false</c>.</returns>
  public bool TryConsumeTick() {
    if (Lag < StepRate || StepRate <= TimeSpan.Zero) return false;

    Lag -= StepRate;
    return true;
  }

  /// <summary>
  /// Resets the accumulated lag to zero (e.g. after resuming from pause).
  /// </summary>
  public void Reset() {
    Lag = TimeSpan.Zero;
  }
}
