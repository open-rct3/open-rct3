// A moving average over frame time durations, tracking FPS metrics without high-frequency jitter.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Numerics;

namespace OpenRCT3.Telemetry;

/// <summary>
/// A moving average over frame time durations, tracking FPS metrics without high-frequency jitter.
/// </summary>
public class FrameRateAccumulator(TimeSpan? window = null)
  : MovingAverage<double>(window ?? TimeSpan.FromMilliseconds(250)) {
  private TimeSpan accumulatedTime = TimeSpan.Zero;
  private int accumulatedFrames;

  /// <summary>Gets the current FPS estimate computed over the window duration.</summary>
  public double CurrentFps => Value;

  /// <summary>Gets the current average frame time in milliseconds.</summary>
  public double CurrentFrameTimeMs { get; private set; }

  /// <summary>Records the delta time of a rendered frame.</summary>
  /// <param name="delta">The elapsed time since the previous frame.</param>
  /// <returns><c>true</c> if the accumulation window elapsed and metrics were refreshed; otherwise, <c>false</c>.</returns>
  public bool RecordFrame(TimeSpan delta) {
    accumulatedTime += delta;
    accumulatedFrames++;

    if (accumulatedTime < Window) return false;

    if (accumulatedTime.TotalSeconds > 0) {
      var fps = accumulatedFrames / accumulatedTime.TotalSeconds;
      CurrentFrameTimeMs = accumulatedTime.TotalMilliseconds / accumulatedFrames;
      Update(fps);
    }

    accumulatedTime = TimeSpan.Zero;
    accumulatedFrames = 0;
    return true;
  }

  public override void Reset() {
    base.Reset();
    accumulatedTime = TimeSpan.Zero;
    accumulatedFrames = 0;
    CurrentFrameTimeMs = 0.0;
  }
}
