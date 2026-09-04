// Unit tests for FixedTimestepAccumulator.
//
// Copyright © 2026 OpenRCT3 Contributors. All rights reserved.

using OpenCobra.GDK.Numerics;

namespace OVL.Tests.Numerics;

[TestFixture]
public class FixedTimestepTests {
  [Test]
  public void Accumulate_UnderStepRate_DoesNotProduceTicks() {
    var accumulator = new FixedTimestepAccumulator(TimeSpan.FromMilliseconds(16.67));
    accumulator.Accumulate(TimeSpan.FromMilliseconds(10.0));

    using (Assert.EnterMultipleScope()) {
      Assert.That(accumulator.TryConsumeTick(), Is.False);
      Assert.That(accumulator.Lag, Is.EqualTo(TimeSpan.FromMilliseconds(10.0)));
      Assert.That(accumulator.Interpolation, Is.GreaterThan(0.0).And.LessThan(1.0));
    }
  }

  [Test]
  public void Accumulate_SingleStep_ConsumesExactlyOneTick() {
    var step = TimeSpan.FromMilliseconds(16.0);
    var accumulator = new FixedTimestepAccumulator(step);
    accumulator.Accumulate(TimeSpan.FromMilliseconds(20.0));

    using (Assert.EnterMultipleScope()) {
      Assert.That(accumulator.TryConsumeTick(), Is.True);
      Assert.That(accumulator.Lag, Is.EqualTo(TimeSpan.FromMilliseconds(4.0)));
      Assert.That(accumulator.TryConsumeTick(), Is.False);
      Assert.That(accumulator.Interpolation, Is.EqualTo(4.0 / 16.0).Within(1e-5));
    }
  }

  [Test]
  public void Accumulate_MultipleSteps_ConsumesSuccessiveTicks() {
    var step = TimeSpan.FromMilliseconds(10.0);
    var accumulator = new FixedTimestepAccumulator(step);
    accumulator.Accumulate(TimeSpan.FromMilliseconds(35.0));

    var ticks = 0;
    while (accumulator.TryConsumeTick()) {
      ticks++;
    }

    using (Assert.EnterMultipleScope()) {
      Assert.That(ticks, Is.EqualTo(3));
      Assert.That(accumulator.Lag, Is.EqualTo(TimeSpan.FromMilliseconds(5.0)));
      Assert.That(accumulator.Interpolation, Is.EqualTo(0.5).Within(1e-5));
    }
  }

  [Test]
  public void Accumulate_ExcessiveLag_ClampsToMaxTicksPerFrame() {
    var step = TimeSpan.FromMilliseconds(10.0);
    var accumulator = new FixedTimestepAccumulator(step, maxTicksPerFrame: 4);

    // Accumulate a massive 1-second spike
    accumulator.Accumulate(TimeSpan.FromSeconds(1.0));

    // Lag must be clamped to 4 * 10ms = 40ms
    Assert.That(accumulator.Lag, Is.EqualTo(TimeSpan.FromMilliseconds(40.0)));

    var ticks = 0;
    while (accumulator.TryConsumeTick()) ticks++;

    using (Assert.EnterMultipleScope()) {
      Assert.That(ticks, Is.EqualTo(4));
      Assert.That(accumulator.Lag, Is.EqualTo(TimeSpan.Zero));
    }
  }

  [Test]
  public void Reset_ClearsAccumulatedLag() {
    var step = TimeSpan.FromMilliseconds(16.0);
    var accumulator = new FixedTimestepAccumulator(step);
    accumulator.Accumulate(TimeSpan.FromMilliseconds(30.0));

    Assert.That(accumulator.Lag, Is.GreaterThan(TimeSpan.Zero));
    accumulator.Reset();

    using (Assert.EnterMultipleScope()) {
      Assert.That(accumulator.Lag, Is.EqualTo(TimeSpan.Zero));
      Assert.That(accumulator.Interpolation, Is.Zero);
      Assert.That(accumulator.TryConsumeTick(), Is.False);
    }
  }

  [Test]
  public void Accumulate_NonPositiveElapsed_DoesNotChangeLag() {
    var accumulator = new FixedTimestepAccumulator(TimeSpan.FromMilliseconds(16.0));
    accumulator.Accumulate(TimeSpan.Zero);
    accumulator.Accumulate(TimeSpan.FromMilliseconds(-10.0));

    Assert.That(accumulator.Lag, Is.EqualTo(TimeSpan.Zero));
    Assert.That(accumulator.Interpolation, Is.Zero);
  }

  [Test]
  public void Interpolation_WhenStepRateIsZero_ReturnsZero() {
    var accumulator = new FixedTimestepAccumulator(TimeSpan.Zero);
    accumulator.Accumulate(TimeSpan.FromMilliseconds(10.0));

    Assert.That(accumulator.Interpolation, Is.Zero);
    Assert.That(accumulator.TryConsumeTick(), Is.False);
  }
}
