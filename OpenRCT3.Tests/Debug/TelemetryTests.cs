// Unit tests for Accumulator, MovingAverage, and FrameRateAccumulator telemetry.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.
using OpenRCT3.Debug;
using OpenCobra.GDK.Numerics;

namespace OpenRCT3.Tests.Debug;

[TestFixture]
public class TelemetryTests {
  [Test]
  public void MovingAverage_DefaultConstructor_UsesDefaultWindowAndLinearAverage() {
    var avg = new MovingAverage<double>();
    Assert.That(avg.Window, Is.EqualTo(MovingAverage<double>.DefaultWindow));

    avg.Update(10.0);
    avg.Update(20.0);
    avg.Update(30.0);

    using (Assert.EnterMultipleScope()) {
      Assert.That(avg.Value, Is.EqualTo(20.0).Within(1e-5));
      Assert.That(avg.Samples, Has.Count.EqualTo(3));
    }
  }

  [Test]
  public void MovingAverage_ExponentialAccumulation_CalculatesSmoothedAverage() {
    var avg = new MovingAverage<double>(TimeSpan.FromSeconds(1), MovingAverage<double>.Exponential(0.5));
    avg.Update(10.0);
    Assert.That(avg.Value, Is.EqualTo(10.0).Within(1e-5));
    avg.Update(20.0);
    Assert.That(avg.Value, Is.EqualTo(15.0).Within(1e-5));
    avg.Update(20.0);
    Assert.That(avg.Value, Is.EqualTo(17.5).Within(1e-5));
  }

  [Test]
  public void MovingAverage_Reset_ClearsValueAndSamples() {
    var avg = new MovingAverage<double>();
    avg.Update(100.0);
    avg.Reset();

    using (Assert.EnterMultipleScope()) {
      Assert.That(avg.Value, Is.Zero);
      Assert.That(avg.Samples, Is.Empty);
    }
  }

  [Test]
  public void FrameRateAccumulator_DefaultConstructor_HasQuarterSecondWindow() {
    var acc = new FrameRateAccumulator();
    Assert.That(acc.Window, Is.EqualTo(TimeSpan.FromMilliseconds(250)));
  }

  [Test]
  public void FrameRateAccumulator_AveragesWindowCorrectly() {
    var acc = new FrameRateAccumulator(TimeSpan.FromSeconds(1.0));
    var delta16ms = TimeSpan.FromMilliseconds(16.0);

    for (var i = 0; i < 59; i++) {
      var updated = acc.RecordFrame(delta16ms);
      Assert.That(updated, Is.False);
    }

    var lastUpdated = false;
    for (var i = 0; i < 4; i++) {
      lastUpdated = acc.RecordFrame(delta16ms);
    }

    using (Assert.EnterMultipleScope()) {
      Assert.That(lastUpdated, Is.True);
      Assert.That(acc.CurrentFps, Is.EqualTo(62.5).Within(0.1));
      Assert.That(acc.CurrentFrameTimeMs, Is.EqualTo(16.0).Within(0.01));
    }
  }
}
